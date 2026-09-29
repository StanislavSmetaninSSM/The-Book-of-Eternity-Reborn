# Tasks: Test Suite Performance and Verification Lanes

**Input**: Design documents from `specs/1505-test-suite-performance/`

**Source issues**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

**Prerequisites**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [quickstart.md](quickstart.md)

**Active execution:** T066–T071 under CATEGORY-SELECTION-DECISION rev1, approved
2026-09-30. Earlier phases and documented Lane commands are historical evidence;
their broad-control requirements are superseded. Do not execute those commands.

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
- [x] T055 [US4] Add RED exact relative-path and category expectations for the
  reviewed #1551 sources in
  `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` and
  `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`, then
  run both focused guards while the sources are still in Fast. The final-review
  remediation also makes category ownership Roslyn/class-level and adds
  synthetic comment/string/method-attribute decoys.
- [x] T056 [US3] Move the canonical wound and browser transport group into
  `BookOfEternityClient.IntegrationTests/` with `RegressionIntegration`:
  `MortalWoundRecoveryTests.cs`,
  `MortalWoundTreatmentCapabilityAuthorityTests.cs`,
  `WebUi/BrowserMortalWorldGenerationFencingTests.cs`, and
  `WebUi/BrowserStorageTransportParityTests.cs`.
- [x] T057 [US3] Extract every deterministic QTE input/grading assertion into
  fixture-free `BookOfEternityClient.Tests/QteDeterministicLogicTests.cs`, move
  the remaining canonical lifecycle in `QteSceneServiceTests.cs` to
  `BookOfEternityClient.IntegrationTests/` with `RegressionIntegration`, and
  prove every original test method/theory row exists exactly once through an
  executable 66 Fast / 51 Integration Roslyn inventory.
- [x] T058 [US3] Move `BookOfEternityClient.Tests/GmWorkerLiveSmokeTests.cs` to
  Integration with `ProcessIntegration` and
  `BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs` to Integration with
  `E2E`, preserving existing method-level traits and process/host cleanup.
- [x] T059 [US3] Run focused Fast QTE logic, focused Integration selections for
  the three changed categories, and both exact boundary guards through
  `scripts/test-csharp.ps1`.
- [x] T060 [US4] Run Fast PlanOnly plus one bounded Fast checkpoint, retain
  counts/timings/duplicate/cleanup evidence, and compare the wall time with the
  four pre-change descriptor measurements.
- [x] T061 [US3] Because T060's fail-fast contour did not complete full planned
  membership, move the exact measured
  file-backed second group listed in `plan.md` to Integration with
  `RegressionIntegration` (or `ProcessIntegration` for real-worker sources),
  update both manifests, and rerun focused category plus PlanOnly controls.
  Final-review remediation semantically splits the mixed Daren source: 67
  fixture-free methods / 77 rows are Fast
  `DarenQteDeterministicLogicTests`, while its 12 file/service methods / 12 rows
  remain Integration `DarenQteShowcaseTests`; executable Roslyn manifests prove
  exact ownership and row preservation.
- [x] T062 [US3] Synchronize the final #1551 placement rules and evidence in
  `docs/testing.md`, `specs/1505-test-suite-performance/research.md`,
  `data-model.md`, and `quickstart.md`; record that GM prompts, examples,
  manifests, client UI, and afterlife runtime docs are unchanged because this
  is internal test scheduling only.
- [x] T063 [US3] Run two representative Fast controls below the unchanged
  five-minute hard limit, preferably around three minutes, with zero duplicate
  IDs and complete owned-process cleanup. The retained runs are
  `20260904-070944-389-28440-60e246e1148944bb86b2b8bd2d582246-fast`
  (`00:02:17.7762792`) and
  `20260904-071206-972-37312-3696b2f3daf248da93c08092f04e572b-fast`
  (`00:02:13.7896671`). Both had timeout false, zero duplicates, and complete
  cleanup, but remain official RED/incomplete: each executed `4,759`, passed
  `4,694`, and failed the exact retained 65 #1536 T067 rows; fail-fast left 24
  of 29 planned descriptors without complete TRX evidence. The +77 passes are
  exactly the Daren deterministic rows restored to Fast.
- [x] T064 [US4] Inspect exact diffs, run `git diff --check`, complete
  independent code review and Spec Kit consistency analysis, explicitly verify
  source/method-body/assertion preservation not covered by the executable row
  manifests, and retain the branch ready for the wound-materialization
  continuation without running PreMerge until merge is explicitly requested.
  The final independent review of `3dbf0572..4c8dc6aa` confirmed every moved
  QTE/Daren method body and assertion was preserved, all three implementation
  findings were closed, the duration-driven taxonomy contradiction was removed,
  and no Critical or Important finding remained. Its sole non-blocking stale
  internal-report range was corrected before closure. The final prerequisite
  and consistency pass found all 18 FRs and 12 SCs represented by the 64-task
  plan with no #1551 coverage or constitution gap; historical T043/T048 remain
  unrelated open bookkeeping. `git diff --check 3dbf0572..HEAD` was clean,
  status contained only the preserved untracked `.serena/`, and PreMerge was
  intentionally not run.

---

## Phase 12: Issue #1536 Treatment Resolver Lane Boundary

- [x] T065 [US3] Extend the exact reviewed-heavy manifest from 32 to 58 paths
  by moving `MortalWoundTreatmentAcceptedStateRegistryTests.cs` and the exact
  25-file `MortalWoundTreatmentResolverTests*.cs` partial family into
  Integration. Keep only the primary partial declaration and standalone
  registry in the 35-entry class-level `RegressionIntegrationSources` manifest,
  with literal class-level `RegressionIntegration` traits. The source guards
  must require exact single Integration ownership and the complete partial
  inventory. Record that accepted-state files, persistence, leases, resource
  claims, publication, restart, and replay are Integration behavior, while
  fixture-free treatment parsers, projectors, fingerprints, reducers, and pure
  policy tests remain Fast. Do not change Fast's five-minute limit or remove
  assertions; no production, gameplay, GM, or afterlife contract changes are
  in scope.

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
  second group in T061 is required; accepted whole-range findings are remediated
  within T055/T057/T061 before T062 follows the final placement; T063 is exactly
  two post-remediation Fast controls; T064 performs independent re-review
  without a merge-only PreMerge run.

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

## Current direction — category selection, 2026-09-30

The owner's latest instruction stops the lane-split verification below and
supersedes mandatory Fast/PreMerge/full-suite execution. No broad controls may
resume; the exact category strategy was approved on 2026-09-30. Historical completed
tasks retain their evidence; unfinished old controls are suspended, not passed.

- [x] T065-CATEGORY-STRATEGY-SPEC (#1505 / #1536) Prepare and present
  `CATEGORY-SELECTION-DECISION`, revision 1, in `spec.md`: evolving documented
  categories, executor-owned category maintenance, impact-based selection with
  no all-suite control, discovery/accounting guards, local/CI/documentation
  migration, and isolated fixture policy. Obtain approval of the exact written
  revision. Then update `plan.md` and implementation tasks through Spec Kit and
  run consistency analysis before changing the runner or tests. This task does
  not authorize gameplay work, commit, push, merge, or resuming old controls.
  Exact revision 1 approved by owner on 2026-09-30: «подтверждаю».

### Active category implementation (#1505 / #1536)

- [x] T066 [US1/US2] Implement catalog schema/selection in
  `scripts/testing/TestCategoryCatalog.psm1` with RED/GREEN evidence in
  `scripts/tests/test-category-catalog.tests.ps1`; populate `tests/categories.json`
  from actual C#/frontend inventory, split large partials and document each
  responsibility. Cover unknown/empty/stale selectors, unmapped tests,
  multi-membership deduplication and extension without a runner enum.
- [x] T067 [US1/US3] Implement bounded category execution in
  `scripts/test-csharp.ps1` and `scripts/testing/TestCategoryExecution.ps1`.
  Preserve owned processes, cleanup, result completeness and exact method filters;
  reject old lanes and implicit full runs. Migrate obsolete boundary tests in
  `FastTestBoundaryTests.cs` and `IntegrationTestBoundaryTests*.cs` to category
  contracts, proving cross-project/dynamic-theory/partial-failure cases.
- [x] T068 [US1/US4] Add selected frontend file execution and reporting; migrate
  frontend `package.json`, `.github/workflows/dotnet-ci.yml` and explicit
  `tests/selection.json` to category selection. Verify Node/Vitest selection,
  missing reports, skips and failed assertions without any full suite.
- [x] T069 [US2/US4] Audit existing fixture caches and document decisions in
  `docs/testing.md`; change only caches with confirmed risks/unjustified
  complexity and verify affected isolation boundaries. Migrate `AGENTS.md`,
  `docs/development-workflow.md`, GitHub templates and active #1505/#1536
  verification instructions; add practical category creation/splitting examples.
- [x] T070 [US1/US3] Validate all catalog ownership through discovery only;
  execute the selected infrastructure/process/frontend categories and any
  actually changed fixture categories. Check XML builds, exact selection,
  result completeness, timings and owned cleanup. Record evidence in `plan.md`.
- [x] T071 [US4] Obtain independent Astra XHigh review of the exact completed
  migration, original CAT-001–010 requirements and verification choices. Fix
  validated findings, rerun affected categories, record process assessment and
  report to owner; stop before gameplay work or external publication.

Dependencies: T066 → T067 → T068 → T069/T070 → T071. Catalog data can be
prepared independently of runner implementation once its schema is fixed.

### Owner-requested GitHub checkpoint — 2026-09-30

- [ ] T072-PUBLISH-CHECKPOINT (#1505 / #1536) Preserve all accumulated wound
  materialization and category-migration source, tests, prompts, examples and
  tracked planning history on GitHub. The owner's direct request authorizes
  staging, committing and pushing the existing feature branch and its local
  history, superseding the earlier publication pause for this checkpoint only.
  Exclude machine-local Serena configuration, ignored test results, runtime
  state and build/dependency outputs. Independently review the publication
  inventory and status wording, verify the committed tree matches the intended
  files, push without force, and confirm the remote commit. This is durable WIP
  preservation, not completion of #1536 or merge approval; do not resume gameplay.

## Suspended cross-feature amendment — #1536 / #1505, 2026-09-30

Implementation and verification of the owner-approved #1536 test-lane boundary are tracked under
`specs/1536-complete-wound-materialization/tasks.md`
(`T081-D-PERFORMANCE-LANE-SPLIT-*`). Those tasks were the checklist for
FR-014/SC-013: a separate complete spiritual cutover lane, exact PreMerge
sentinels, unchanged process cases, robust planned/completed accounting, both
30-minute controls and independent review. Earlier completed tasks above remain
historical; they do not authorize counting an incomplete PreMerge as the final
control. Both unfinished full timing controls are now suspended by the owner's
new category strategy; they must not be resumed or marked as passed.
