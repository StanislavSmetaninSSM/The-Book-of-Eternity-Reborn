# Feature Specification: Test Suite Performance and Verification Lanes

**Feature Branch**: `work/1505-test-suite-performance`

**Created**: 2026-07-31

**Status**: Approved; amended from bounded implementation evidence

**Input**: Reduce the 40–60 minute C# test-suite runtime without weakening production validation or test coverage, and provide predictable local verification lanes.

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)
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

Phase 45 subsequently added enough integration regressions for the exact
PreMerge control to complete `4606/4606` available results green but reach the
15-minute deadline before its final 15 E2E cases. The approved amendment keeps
the exhaustive 358-case `AfterlifeSpiritualConflictValidationTests` matrix in
RegressionIntegration, retains an exact ten-method PreMerge sentinel, and
applies measured long-first costs to overlapping PreMerge classes. No test or
assertion is removed.

Later suite growth reproduced a capacity-only failure under #1526: all
`4,326/4,326` completed core results passed, but the 15-minute deadline expired
during ProcessIntegration. TRX and focused evidence showed the two unchanged
browser-presentation shards roughly doubled in duration only while they
overlapped the internally parallel Fast hosts. A Fast-drain-only exact run still
timed out. Source tracing then showed that every browser and console audit row
repeated save copy/load setup. PreMerge now uses measured retained costs for
underestimated classes, while the audit loads each source save once and clones
that prepared tree per row to preserve isolation. Each generated audit
descriptor has its own test host and fixture.

The ExplorerWeb class also repeated large deterministic seed families for
every parameterized row. Its class fixture now owns one hashed empty canonical
skeleton per test host, clones a distinct writable root for every row, and
reuses immutable keyed seed profiles. The full class passes `436/436` in
`3:51` test time. Because multiple exact 15-minute runs remained
correctness-green but capacity-red before both exclusive tail phases could
finish, #1526 explicitly changes only the PreMerge hard limit to 20 minutes.
The accepted exact control then passed `4,836/4,836` results in `14:18.302`,
including ProcessIntegration `490/490` and E2E `15/15`, with zero duplicate
IDs and complete owned-tree cleanup.

Further legitimate suite growth under #1535/#1543/#1546 exhausted that
headroom. The exact 20-minute PreMerge attempt completed every available core
result green (`6,608/6,608`) with zero duplicates and complete cleanup, but the
deadline terminated the exclusive process tail before it could publish a TRX.
The isolated ProcessIntegration lane then passed `523/523` in `3:32` wall time;
combined with the approximately 17-minute green core phase and the retained E2E
tail, the measured lower bound no longer fits 20 minutes. Issue #1547 therefore
raises only the globally bounded PreMerge deadline to 30 minutes. It does not
remove coverage or change filters, cases, assertions, scheduling phases,
ordering, or concurrency ceilings.

Later Fast growth exposed a separate physical-boundary regression under #1551.
The lane now discovers 7,797 cases across 29 descriptors and reaches its
five-minute hard stop. Four isolated mixed Fast descriptors measured about
3:30, 3:55, 1:05, and 4:00 without build time. Their dominant classes perform
canonical file/restart/rollback lifecycles, real worker execution, in-process
HTTP hosting, browser transport parity, lease contention, or large file-backed
state-machine workflows. Detached wound/effect parsers, reducers, contracts,
and source guards remain comparatively cheap. This amendment restores the
already-approved physical project boundary: semantic integration sources move
to Integration with explicit categories, while deterministic logic extracted
from a mixed source remains in Fast. The five-minute limit, runner filters,
process ceilings, test cases, and assertions remain unchanged.

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
PreMerge schedules and the reviewed lifecycle/spiritual-conflict sentinel
overlaps, then run one Fast checkpoint, the diagnostic lane required by the
changed boundary, and one final PreMerge control.

**Acceptance Scenarios**:

1. **Given** an ordinary local code change, **when** the documented default
   command runs, **then** it selects only
   `BookOfEternityClient.Tests.csproj`, uses no category-exclusion filter,
   cannot discover integration tests, and completes within five minutes on the
   baseline Windows machine, with approximately three minutes as the preferred
   operating target so normal suite growth retains headroom.
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
   control, **then** one 30-minute deadline covers frontend verification,
   builds, tests, and cleanup; at least 4,240 non-duplicate results complete,
   including ProcessIntegration, E2E, and exactly ten reviewed GameEngine
   lifecycle sentinels rather than the complete lifecycle class, plus exactly
   ten reviewed spiritual-conflict sentinels rather than the complete 358-case
   matrix.

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
  ten-method `PreMergeSentinel` manifest. `RegressionIntegrationOnly`
  overlaps PreMerge only through the exact reviewed ten-method
  spiritual-conflict sentinel manifest.
- Splitting the partial guardian class must preserve isolated temporary roots and must not introduce shared mutable fixture state, file collisions, or nondeterministic parallel failures.
- External process tests must use ownership-aware cleanup. On Windows, a
  target must remain behind a launch gate until its launcher belongs to a
  dedicated kill-on-close Job Object; the target and inherited descendants
  must remain contained even if an intermediate root exits first. A timeout
  must terminate only the run's owned process tree and must not target
  unrelated developer processes.
- Performance evidence must distinguish `dotnet test` startup wall time from runner-reported test duration and use the same build configuration for comparisons.
- A measured slow class may mix deterministic logic with file-backed lifecycle
  tests. The deterministic portion remains in Fast under a fixture-free test
  class, while the canonical I/O portion moves to Integration; no row or
  assertion may be duplicated, deleted, skipped, or hidden behind a negative
  Fast filter.

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
  `DeepValidation`, `RegressionIntegration`, `RegressionIntegrationOnly`,
  `LifecycleIntegration`, `PreMergeSentinel`, `ProcessIntegration`, and `E2E`
  categories as applicable.
- **FR-012**: The repository MUST provide documented default fast, focused,
  full-validation, regression-integration, process-integration, E2E, and
  LifecycleIntegration, DeepValidation, and PreMerge commands with expected
  local time ranges; `Complete` MAY remain only as a temporary alias for
  `PreMerge`.
- **FR-013**: The fast lane MUST select the physically isolated fast project
  directly, without a slow-category exclusion filter, and that project MUST NOT
  discover integration sources. Exact source guards MUST name every reviewed
  integration-heavy source and fail if it is copied back into Fast or loses its
  required diagnostic category.
- **FR-014**: DeepValidation MUST select the Integration-only union of
  FullValidation and DeepValidation, excluding LifecycleIntegration,
  ProcessIntegration, and E2E. LifecycleIntegration MUST select the complete
  reviewed lifecycle class as one external process under a ten-minute cap.
  PreMerge MUST exclude both deep categories, ordinary lifecycle tests, and
  exhaustive RegressionIntegrationOnly matrices from core Integration; admit
  exactly ten reviewed lifecycle sentinels and exactly ten reviewed
  spiritual-conflict sentinels; include ProcessIntegration and E2E through
  exclusive non-overlapping phases; and use one deadline across frontend
  verification, builds, tests, and cleanup. It MUST retain measured scheduling
  costs for materially underestimated integration classes and MUST prepare each
  selected browser-presentation source save once per fixture while preserving
  isolated state for every browser and console assertion. Neither external-process concurrency
  ceiling may change. The four ExplorerWeb descriptors MUST form a startup wave
  that drains before any remaining parallel descriptor starts. The remaining
  descriptors MUST then use the ordinary scheduler, and ProcessIntegration/E2E
  phase membership and ordering MUST remain unchanged.
  PreMerge's lane-wide hard limit MUST be 30 minutes. Every other lane timeout,
  external-process ceiling, filter, case, assertion, and phase boundary MUST
  remain unchanged.
- **FR-015**: Performance comparisons and final controls MUST use bounded
  execution, retain JSON/TRX/log results, detect cross-descriptor duplicate test
  IDs, and verify cleanup of owned child processes.
- **FR-017**: Shared TestSupport code MUST NOT depend on xUnit or
  `Microsoft.NET.Test.Sdk`; IntegrationTests MUST NOT reference Tests; every
  partial test class MUST belong to exactly one test project.
- **FR-016**: Production validation rules, canonical-state schemas, issue codes, player-facing behavior, GM prompts, gameplay documentation, worked examples, console behavior, and browser behavior MUST remain unchanged.
- **FR-018**: Test placement MUST be determined by behavior rather than elapsed
  time alone. Canonical file/restart/rollback/lifecycle and lease-contention
  workflows MUST use `RegressionIntegration`; real child-process workflows MUST
  use `ProcessIntegration`; true host/browser end-to-end workflows MUST use
  `E2E`; detached deterministic unit, parser, reducer, contract, and source-guard
  coverage MUST remain in Fast. A mixed source MUST be split at that semantic
  boundary when moving it wholesale would remove meaningful deterministic
  feedback from Fast. Exact executable Roslyn manifests MUST preserve each QTE
  and Daren Fact/Theory method and InlineData row once across that split, and
  category ownership MUST be read from class-level attributes on the expected
  test class rather than source-text tokens or method-level traits.

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
- **Reviewed heavy source**: A source whose canonical I/O, process, host, or
  end-to-end behavior has been explicitly assigned to Integration and protected
  by exact source/category manifests.

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
   non-overlapping filters, exact ten-method lifecycle and
   spiritual-conflict sentinel exceptions, and one deadline. Route the
   complete lifecycle class to LifecycleIntegration and the complete
   spiritual-conflict matrix to RegressionIntegration.
8. Treat fixture-copy optimization as secondary. Because bounded post-selection evidence missed the Fast budget, capture one immutable in-memory prepared Guardian snapshot per test host and materialize independent roots per test.
9. Repair later Fast drift through semantic source placement under #1551. Move
   measured canonical workflows to the Integration project with exact category
   manifests, and split QTE deterministic grading/input coverage away from its
   file-backed scene lifecycle so the former remains fixture-free in Fast.
   Apply the same boundary to Daren: 77 detached route/prose/reducer/contract
   rows remain in Fast and 12 profile/filesystem/service/browser-projection rows
   remain categorized in Integration. Pin QTE `66/51` and Daren `77/12` with
   executable method/row manifests.

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
- **SC-006**: One PreMerge control completes within one 30-minute deadline on
  the baseline Windows machine and retains JSON/TRX/log evidence.
- **SC-007**: PreMerge produces at least 4,240 results, completes
  ProcessIntegration and E2E, executes exactly ten reviewed lifecycle
  sentinels and ten reviewed spiritual-conflict sentinels, and reports zero
  failures and zero cross-descriptor duplicate test IDs.
- **SC-008**: Public full validation and explicit all-phase validation produce identical ordered issues on representative valid and invalid fixtures.
- **SC-009**: The guardian source guard fails deterministically when the broad-call budget or sentinel category rule is violated.
- **SC-010**: Bounded verification leaves no owned `dotnet`, testhost, client, worker-host, PowerShell helper, Agent Console, or related child process running.
- **SC-011**: Two consecutive #1551 Fast controls complete within five minutes,
  preferably around three minutes, with zero duplicate test IDs, no timeout,
  and complete owned-process cleanup.
- **SC-012**: Every moved test remains discoverable through its explicit
  Integration category and Focused Integration selection; exact guards prove
  that no reviewed source exists in both projects and no test or assertion was
  removed to obtain the Fast result. QTE manifests contain exactly 66 Fast and
  51 Integration rows; Daren manifests contain exactly 77 Fast and 12
  Integration rows. The guard rejects missing, extra, duplicated, or changed
  rows and ignores category text outside the expected class-level attributes.

## Verification Plan

- **C# verification**:
  - Build the solution without restoring after dependencies are present.
  - Run new validation-selection equivalence, ordering, invalid-selection, and state-isolation tests.
  - Run the guardian source guard and the fixed before/after guardian benchmark under an external process-tree timeout.
  - Run focused controls during implementation, one Fast control at a
    meaningful checkpoint, and one final PreMerge control. Do not serially run all
    diagnostic lanes before PreMerge unless a focused failure requires
    diagnosis.
  - Run DeepValidation once on this branch because it changes the category
    boundary; retain the accepted result when PlanOnly proves the selection is
    unchanged. Run LifecycleIntegration once because its boundary changes;
    both lanes remain conditional and explicit.
  - Retain the meaningful Fast checkpoint plus relevant conditional-lane and
    final PreMerge JSON/TRX/log and wall-time evidence. Do not use an unbounded
    40–60 minute control.
  - For #1526, retain the capacity-red PreMerge, isolated and browser-only
    shard timings, the rejected Fast-drain exact run, executable scheduling and
    fixture guards, a full 166-row audit benchmark, updated PlanOnly contract,
    one meaningful Fast checkpoint, and one exact final PreMerge control.
  - For #1547, retain the exact 20-minute capacity-red summary, the green
    `6,608/6,608` core evidence, the isolated green `523/523`
    ProcessIntegration control, a RED/GREEN runner-contract guard, one updated
    PlanOnly contract, and one exact final PreMerge control under the new
    30-minute bound.
  - For #1551, add RED/GREEN exact source/category guards, move each semantic
    group with focused Integration verification, replay Fast PlanOnly after
    each group, run only the diagnostic categories actually changed, and retain
    two final Fast controls under five minutes with approximately three minutes
    as the preferred target. Known wound-contour REDs remain discoverable and
    classified; they are not skipped or used to excuse a timeout. Final-review
    remediation additionally requires exact Roslyn QTE/Daren row inventories,
    a semantic 77/12 Daren split, synthetic category-decoy coverage, refreshed
    PlanOnly membership, and two new sequential Fast controls.
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
