# Feature Specification: Test Suite Performance and Verification Lanes

**Feature Branch**: `work/1505-test-suite-performance`

**Created**: 2026-07-31

**Status**: Approved; amended from bounded implementation evidence

**Input**: Reduce the 40–60 minute C# test-suite runtime without weakening production validation or test coverage, and provide predictable local verification lanes.

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)
- **Issue type**: Test-infrastructure performance, reliability, and developer experience.
- **Spec Kit justification**: The implementation spans the production validation orchestrator, a large multi-file guardian regression suite, test classification and source guards, verification scripts or documentation, and performance evidence across multiple sessions.
- **Contract scope**: Internal validation orchestration and test infrastructure. There is no player-facing, GM-facing, gameplay, canonical-state schema, console, browser, frontend, prompt, documentation-example, or afterlife contract change.
- **Out of scope**: Changing validation rules or issue semantics; skipping required coverage; changing canonical game state; changing gameplay behavior; optimizing every fixture in the repository; rewriting the xUnit runner; making the default production validator partial; using parallel execution as the only performance fix.
- **Follow-up issue policy**: A regression in production validation equivalence, lost assertions, failure to meet the performance gates, or unsafe process cleanup remains blocking under #1505. Independent fixture-copy or runner-level optimizations may move to a linked follow-up only after this feature meets its acceptance criteria.

## Current Evidence

- The suite discovers 6,560 cases across 228 classes and 273 C# test files.
- Test source contains 965 calls to the broad `ValidateGameStateAsync()` entry point.
- Every broad call runs 26 ordered validation phases.
- `GuardianSystemRegressionTests` is one partial xUnit class with 460 discovered cases, 436 declared test methods, and 295 broad validation calls; its tests run sequentially.
- A targeted guardian validation sample takes about 1 second, while a comparable broad validation sample takes about 10 seconds. Two broad guardian validations take about 20 seconds in both Debug and Release.
- The roughly 9-second avoidable delta multiplied by 295 sequential broad calls predicts about 44 minutes, matching the observed 40–60 minute full-suite duration.
- A sampled live Agent Console process smoke test takes about 3 seconds. Process-host tests and repeated 47-file fixture copies are secondary contributors, but are not the primary measured multiplier.

Accepted final evidence records Fast at `2587/2587` in `2:59.057` and
`2:28.905`, LifecycleIntegration at `186/186` in `5:31.972`, retained
DeepValidation at `2142/2142` in `14:15.857`, and PreMerge at `4522/4522` in
`12:12.687`. Every control exited successfully with no failures, duplicate
IDs, timeout, cleanup failure, or remaining owned process. PreMerge met the
mandatory below-15-minute ceiling but not the preferred below-ten-minute
target.

## User Scenarios & Testing

### User Story 1 - Fast Rule-Focused Guardian Tests (Priority: P1)

As a developer changing one validation domain, I can run guardian regression tests against only the validation phases relevant to their assertions, so focused feedback arrives in seconds rather than repeatedly paying for the entire validation pipeline.

**Why this priority**: Repeating unrelated validation work 295 times inside one sequential test class is the dominant measured cause of the 40–60 minute suite.

**Independent Test**: Run the fixed two-test guardian benchmark before and after migration under the same bounded harness. The migrated benchmark must preserve its assertions and complete at least five times faster.

**Acceptance Scenarios**:

1. **Given** a guardian regression that asserts issues from one validation phase, **when** the test requests its scoped validation profile, **then** only the selected phase and its explicit prerequisites run, and the expected issues remain unchanged.
2. **Given** a guardian regression that spans multiple validation domains, **when** the test requests a combined profile, **then** each selected phase runs exactly once in canonical production order.
3. **Given** the guardian suite after migration, **when** source guards count direct calls to the broad validation entry point, **then** no more than eight intentional full-pipeline sentinel calls remain.
4. **Given** the migrated guardian tests, **when** their assertions and discovered cases are compared with the baseline, **then** no case or assertion is silently removed to achieve the speedup.

---

### User Story 2 - Unchanged Production Validation (Priority: P1)

As a maintainer, I retain one public full-validation entry point whose behavior, phase order, issue output, and runtime callers are unchanged by test optimization.

**Why this priority**: Faster tests are not acceptable if they weaken canonical-state protection or cause tests to exercise a path different from production without full-pipeline coverage.

**Independent Test**: On representative valid and invalid fixtures, compare the public full-validation result with the explicit all-phases path and verify identical ordered issues.

**Acceptance Scenarios**:

1. **Given** any runtime caller of `ValidateGameStateAsync()`, **when** validation runs after this change, **then** all 26 existing phases execute once in their existing order.
2. **Given** a fixture that produces issues in multiple phase groups, **when** public full validation and explicit all-phase validation run independently, **then** they return equivalent ordered issue sequences.
3. **Given** an empty phase selection or a selection containing an unknown phase, **when** a test attempts scoped validation, **then** the call fails explicitly instead of returning a false-green empty result.
4. **Given** a scoped validation run followed by another run on the same service instance, **when** each run starts, **then** per-run caches and mutable orchestration state are initialized consistently and no prior selection leaks into the next result.

---

### User Story 3 - Predictable Project-Routed Verification (Priority: P2)

As a local contributor, I can use a physically isolated fast project for
ordinary feedback, focused diagnostic lanes for slow integration boundaries,
explicit conditional LifecycleIntegration and DeepValidation tiers, and one
globally bounded PreMerge control with documented commands and time
expectations.

**Why this priority**: A single undifferentiated command currently makes ordinary verification unpredictable and tempts contributors either to wait nearly an hour or skip testing entirely.

**Independent Test**: Enumerate both test projects, prove the fast project
cannot discover integration sources, inspect the disjoint DeepValidation and
PreMerge schedules and the reviewed lifecycle-sentinel overlap, then run two
Fast controls, one LifecycleIntegration control, retain the unchanged
DeepValidation control for this category-boundary change, and run one PreMerge
control.

**Acceptance Scenarios**:

1. **Given** an ordinary local code change, **when** the documented default
   command runs, **then** it selects only
   `BookOfEternityClient.Tests.csproj`, uses no category-exclusion filter,
   cannot discover integration tests, and completes within five minutes on the
   baseline Windows machine.
2. **Given** a validation-orchestration change, **when** the documented full-validation command runs, **then** all intentional full-pipeline sentinels can be selected explicitly.
3. **Given** a file-backed Guardian, Explorer, GameEngine, browser-command, or host change, **when** the regression-integration lane runs, **then** its workflow regressions are selected explicitly without destabilizing the Fast lane.
4. **Given** a process-host or end-to-end change, **when** its documented lane runs, **then** process-starting tests are selected explicitly and retain bounded cleanup.
5. **Given** a GameEngine turn-lifecycle change or related diagnostic need,
   **when** LifecycleIntegration runs explicitly, **then** all 186 reviewed
   lifecycle cases execute in one external process below ten minutes.
6. **Given** a change to FullValidation/DeepValidation category boundaries,
   **when** DeepValidation runs explicitly, **then** the Integration-only union
   completes below 15 minutes with at least 1,950 non-duplicate results.
7. **Given** the PreMerge command, **when** it runs under the final bounded
   control, **then** one 15-minute deadline covers frontend verification,
   builds, tests, and cleanup; at least 4,490 non-duplicate results complete,
   including ProcessIntegration, E2E, and exactly ten reviewed GameEngine
   lifecycle sentinels rather than the complete lifecycle class.

---

### User Story 4 - Visible Performance Regressions (Priority: P3)

As a maintainer, I receive a deterministic guard when guardian tests drift back to broad validation or when a verification lane exceeds its agreed budget.

**Why this priority**: Without an enforceable boundary, new tests can gradually recreate the same full-pipeline multiplier.

**Independent Test**: Temporarily introduce a ninth unapproved broad guardian call and verify that the source guard fails with the current count, allowed budget, and remediation guidance.

**Acceptance Scenarios**:

1. **Given** the approved maximum of eight guardian full-pipeline sentinels, **when** another broad call is added, **then** a source guard fails and names the violation.
2. **Given** a sentinel broad call, **when** source guards inspect it, **then** the test is explicitly categorized as `FullValidation`.
3. **Given** a bounded benchmark or lane run, **when** it completes, times out, or fails, **then** its wall time, runner-reported duration, result, and retained log or TRX location are recorded.
4. **Given** a bounded test run that launches child processes, **when** the run finishes or is terminated, **then** the harness verifies that no owned test process remains.

### Edge Cases

- A rule-focused test may assert issues owned by more than one phase. Its profile must name every required phase rather than relying on incidental work from full validation.
- A selected phase may depend on data normally prepared by another phase. The dependency must be explicit in the selected profile or made a safe phase-local prerequisite; selected execution must not silently depend on a prior test.
- Multiple selected phases must preserve their canonical production order regardless of selection order.
- Empty selections and selections containing undefined values must fail closed.
- Full-validation sentinels may cover multiple phase groups, but each remaining broad guardian call must have a documented reason and the `FullValidation` category.
- A test can belong to more than one slow category. DeepValidation, ordinary
  PreMerge core Integration, ProcessIntegration, and E2E remain disjoint.
  LifecycleIntegration overlaps PreMerge only through the exact reviewed
  ten-method `PreMergeSentinel` manifest.
- Splitting the partial guardian class must preserve isolated temporary roots and must not introduce shared mutable fixture state, file collisions, or nondeterministic parallel failures.
- External process tests must use ownership-aware cleanup. On Windows, a
  target must remain behind a launch gate until its launcher belongs to a
  dedicated kill-on-close Job Object; the target and inherited descendants
  must remain contained even if an intermediate root exits first. A timeout
  must terminate only the run's owned process tree and must not target
  unrelated developer processes.
- Performance evidence must distinguish `dotnet test` startup wall time from runner-reported test duration and use the same build configuration for comparisons.

## Requirements

### Functional Requirements

- **FR-001**: The public no-argument validation entry point MUST continue to execute all 26 existing phases exactly once and in the existing canonical order.
- **FR-002**: Runtime callers outside the test assembly MUST continue using the public full-validation contract; scoped validation MUST remain internal to the runtime/test boundary.
- **FR-003**: Tests MUST be able to select one or more named validation phases, reviewed internal rule-group scopes, optional relevant state files, or reviewed profiles without running unrelated phases.
- **FR-004**: Scoped execution MUST run each selected phase exactly once, preserve canonical production order, initialize per-run state consistently, and return issues using the existing issue types and ordering rules.
- **FR-005**: Empty and unknown selections MUST be rejected explicitly.
- **FR-006**: The explicit all-phases selection MUST be behaviorally equivalent to the public full-validation entry point on representative valid and multi-error fixtures.
- **FR-007**: Guardian regression tests MUST migrate to the narrowest reviewed phase selection that preserves their assertions.
- **FR-008**: No more than eight direct broad-validation calls MAY remain across `GuardianSystemRegressionTests*.cs`; each MUST be an intentional full-pipeline sentinel categorized as `FullValidation`.
- **FR-009**: The guardian regression suite MUST be partitioned into independently runnable, non-overlapping domain chunks by the bounded runner; shared mutable fixture state MUST NOT be introduced.
- **FR-010**: A source guard MUST enforce the guardian broad-call budget, sentinel category, and scoped-validation API boundary.
- **FR-011**: Slow tests MUST use explicit `FullValidation`,
  `DeepValidation`, `RegressionIntegration`, `LifecycleIntegration`,
  `PreMergeSentinel`, `ProcessIntegration`, and `E2E` categories as applicable.
- **FR-012**: The repository MUST provide documented default fast, focused,
  full-validation, regression-integration, process-integration, E2E, and
  LifecycleIntegration, DeepValidation, and PreMerge commands with expected
  local time ranges; `Complete` MAY remain only as a temporary alias for
  `PreMerge`.
- **FR-013**: The fast lane MUST select the physically isolated fast project
  directly, without a slow-category exclusion filter, and that project MUST NOT
  discover integration sources.
- **FR-014**: DeepValidation MUST select the Integration-only union of
  FullValidation and DeepValidation, excluding LifecycleIntegration,
  ProcessIntegration, and E2E. LifecycleIntegration MUST select the complete
  reviewed lifecycle class as one external process under a ten-minute cap.
  PreMerge MUST exclude both deep categories and ordinary lifecycle tests from
  core Integration, admit exactly ten reviewed lifecycle sentinels, include
  ProcessIntegration and E2E through exclusive non-overlapping phases, and use
  one deadline across frontend verification, builds, tests, and cleanup.
- **FR-015**: Performance comparisons and final controls MUST use bounded
  execution, retain JSON/TRX/log results, detect cross-descriptor duplicate test
  IDs, and verify cleanup of owned child processes.
- **FR-017**: Shared TestSupport code MUST NOT depend on xUnit or
  `Microsoft.NET.Test.Sdk`; IntegrationTests MUST NOT reference Tests; every
  partial test class MUST belong to exactly one test project.
- **FR-016**: Production validation rules, canonical-state schemas, issue codes, player-facing behavior, GM prompts, gameplay documentation, worked examples, console behavior, and browser behavior MUST remain unchanged.

### Key Entities

- **Validation phase**: One of the 26 existing ordered validation operations that appends zero or more `ValidationIssue` values.
- **Validation selection**: A non-empty internal request identifying phases or a reviewed rule-group scope to execute once in canonical order, optionally restricted to relevant generic state-file walkers.
- **Validation profile**: A reviewed, named selection used by a coherent guardian test domain, including explicit prerequisite phases.
- **Full-validation sentinel**: A small intentional test that exercises the same complete validation path used by production.
- **Lifecycle-integration control**: The complete GameEngine turn-lifecycle
  class, selected explicitly and separately from ordinary PreMerge.
- **Verification lane**: A documented project/selection with a purpose, hard
  limit, expected duration, and retained-result policy.
- **Performance baseline**: Reproducible pre-change counts and bounded timings used to compare the same benchmark and complete suite after implementation.

## Design Direction

1. Preserve `ValidateGameStateAsync()` as the production all-phases facade.
2. Introduce an internal phase-selection model and one internal scoped execution path shared by tests and the public facade. The public facade always supplies the complete selection.
3. Keep the existing 26 phase methods and their canonical ordering as the single source of truth. Selection controls whether a phase runs; it does not duplicate validation rules.
4. Define reviewed guardian profiles close to the test fixture, migrate broad calls to the narrowest profile, and retain no more than eight full-pipeline sentinels.
5. Keep the shared partial Guardian fixture intact and create non-overlapping domain/method chunks in the bounded runner after discovery validates complete assignment.
6. Extract reusable fixtures into a non-test TestSupport library, keep fast and
   integration sources in separate test projects, and enforce dependency,
   partial-class, source, broad-call, and category boundaries.
7. Route Fast directly to the fast project and route diagnostic categories to
   the integration project. Compose PreMerge from both projects with
   non-overlapping filters, an exact ten-method lifecycle sentinel exception,
   and one deadline. Route the complete lifecycle class to its explicit lane.
8. Treat fixture-copy optimization as secondary. Because bounded post-selection evidence missed the Fast budget, capture one immutable in-memory prepared Guardian snapshot per test host and materialize independent roots per test.

## Success Criteria

### Measurable Outcomes

- **SC-001**: The fixed two-test guardian benchmark is at least five times faster than its approximately 20-second baseline while preserving both test results and assertions.
- **SC-002**: Direct broad-validation calls in `GuardianSystemRegressionTests*.cs` decrease from 295 to no more than eight.
- **SC-003**: Two consecutive documented Fast controls complete within their
  five-minute hard limit on the baseline Windows machine.
- **SC-004**: One explicit DeepValidation control completes within 15 minutes
  with at least 1,950 results, zero failures, and zero duplicate IDs.
- **SC-005**: One explicit LifecycleIntegration control completes within ten
  minutes with all 186 reviewed cases, zero failures, and complete owned-tree
  cleanup.
- **SC-006**: One PreMerge control completes within one 15-minute deadline on
  the baseline Windows machine, preferably below ten minutes, and retains
  JSON/TRX/log evidence.
- **SC-007**: PreMerge produces at least 4,490 results, completes
  ProcessIntegration and E2E, executes exactly ten reviewed lifecycle
  sentinels, and reports zero failures and zero cross-descriptor duplicate test
  IDs.
- **SC-008**: Public full validation and explicit all-phase validation produce identical ordered issues on representative valid and invalid fixtures.
- **SC-009**: The guardian source guard fails deterministically when the broad-call budget or sentinel category rule is violated.
- **SC-010**: Bounded verification leaves no owned `dotnet`, testhost, client, worker-host, PowerShell helper, Agent Console, or related child process running.

## Verification Plan

- **C# verification**:
  - Build the solution without restoring after dependencies are present.
  - Run new validation-selection equivalence, ordering, invalid-selection, and state-isolation tests.
  - Run the guardian source guard and the fixed before/after guardian benchmark under an external process-tree timeout.
  - Run focused controls during implementation, two consecutive Fast controls
    at final verification, and one PreMerge control. Do not serially run all
    diagnostic lanes before PreMerge unless a focused failure requires
    diagnosis.
  - Run DeepValidation once on this branch because it changes the category
    boundary; retain the accepted result when PlanOnly proves the selection is
    unchanged. Run LifecycleIntegration once because its boundary changes;
    both lanes remain conditional and explicit.
  - Retain the two Fast summaries plus the LifecycleIntegration,
    DeepValidation, and PreMerge JSON/TRX/log and wall-time evidence. Do not use
    an unbounded 40–60 minute control.
- **Documentation/contract verification**: Run the new test-lane/source-guard coverage. GM prompts, Mortal/afterlife docs, worked examples, manifests, and contract matrices are N/A because FR-016 prohibits gameplay or GM-authored contract changes.
- **Frontend verification**: N/A; no frontend files or browser behavior are in scope.
- **Manual/player-facing verification**: N/A; compare process inventory before and after bounded integration runs to verify owned child cleanup.

The documented runner interface is:

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

## Assumptions

- The same Windows machine and repository checkout used for the recorded baseline remain available for before/after measurements.
- The 26 current validation phases are the correct production set and their existing order is authoritative.
- The bounded runner can execute non-overlapping Guardian method chunks independently while every test retains an isolated temporary session root.
- Eight full-pipeline guardian sentinels are sufficient to cover orchestration boundaries while keeping repeated broad cost bounded; any increase requires revising this specification with evidence.
- The initial optimization focuses on guardian broad-validation multiplication because measured evidence identifies it as the primary contributor. Secondary fixture and process-host work is conditional on the accepted performance gates.
