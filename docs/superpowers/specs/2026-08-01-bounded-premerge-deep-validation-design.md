# Bounded PreMerge and DeepValidation Design

Date: 2026-08-01
Issue: #1505
Branch: `work/1505-test-suite-performance`

## Decision

The C# verification workflow will use two disjoint integration tiers:

1. `PreMerge` is the required, globally bounded merge gate. It includes all
   fast tests, representative and equivalence sentinels, ordinary integration
   tests, `ProcessIntegration`, and `E2E`.
2. `DeepValidation` contains exhaustive validation matrices and other
   high-multiplicity diagnostic checks. It is explicit, globally bounded, and
   is not an ordinary post-edit or every-merge command.

No test or assertion is deleted. Moving a test to `DeepValidation` changes when
it runs, not what it verifies.

`Complete` remains a temporary alias for `PreMerge`; it must not silently run
both tiers serially.

## Evidence Requiring the Change

The accepted Fast architecture works:

- Fast run 1: 2,585 passed in 2:30.987 runner time.
- Fast run 2: 2,585 passed in 2:33.913 runner time.
- Both runs had zero failures, skipped tests, duplicate IDs, timeouts, or
  cleanup failures.

The current all-inclusive PreMerge architecture cannot meet its own budget:

- hard stop: 15:00.393 runner time, exit 124;
- 4,738 completed tests, all passing;
- zero duplicate IDs;
- owned-process cleanup succeeded;
- 22 descriptors completed, 4 were stopped at the deadline, and 17 never
  started.

Using actual TRX wall times and retained matching descriptor times:

- projected parallel slot-work is 5,088.928 seconds;
- the four-host work bound is 21:12.232;
- serial preflight is 1:07.853;
- serial `ProcessIntegration` plus `E2E` is 3:17.656;
- the optimistic total lower bound is 25:37.741.

The existing order is already within 19.3 seconds of perfect four-host packing.
Changing shard order or adding more shards cannot close a roughly ten-minute
capacity gap. At least 50.1% of current parallel slot-work must leave the
ordinary PreMerge path or be eliminated through genuine work reduction.

## Alternatives Considered

### Preserve all 6,614 planned cases in PreMerge

This requires deep optimization across many fixtures before the gate can be
used. The largest repeated costs include validator matrices, Guardian
regression matrices, repeated save loading, and snapshot materialization. The
work is valuable but too uncertain to be the only route to restoring a usable
merge gate.

### Increase concurrency above four

Eight hosts only approach the budget under ideal conditions and leave almost
no allowance for preflight, process tests, E2E, machine contention, or timing
variance. Prior evidence also records lock-related failures under greater
external concurrency. This would make the workflow machine-dependent.

### Two bounded tiers

This preserves exhaustive coverage, gives normal development a predictable
gate, and makes the expensive checks explicit. It matches the intended
workflow: frequent Fast feedback, one bounded PreMerge before merge, and deep
diagnostics only when their domain changes or a failure requires them.

This is the selected approach.

## Lane Contract

### Fast

- Project: `BookOfEternityClient.Tests`.
- Default command: `.\scripts\test-csharp.ps1`.
- Global hard limit: five minutes.
- It selects the fast project directly and does not depend on category
  exclusion.
- Acceptance: two consecutive successful runs below five minutes during final
  verification.

### PreMerge

- Global hard limit: fifteen minutes across frontend verification, builds,
  discovery, test execution, and cleanup.
- Preferred wall time: below ten minutes.
- Parallelism remains at most four test processes, with at most two Fast
  processes.
- Parallel content:
  - all Fast descriptors;
  - Integration descriptors matching:

    ```text
    Category!=FullValidation
    &Category!=DeepValidation
    &Category!=ProcessIntegration
    &Category!=E2E
    ```

- Exclusive tail:
  - exactly one `ProcessIntegration` selection;
  - exactly one `E2E` selection.
- It contains representative/equivalence sentinels for every family removed
  to `DeepValidation`.
- `Complete` resolves to this same plan and produces the same descriptors.

### DeepValidation

- Project: `BookOfEternityClient.IntegrationTests`.
- Global hard limit: fifteen minutes.
- Filter:

  ```text
  (Category=FullValidation|Category=DeepValidation)
  &Category!=ProcessIntegration
  &Category!=E2E
  ```

- It excludes `ProcessIntegration` and `E2E`.
- It uses the existing bounded descriptor scheduler and owned-process cleanup.
- The existing `FullValidation` lane remains available as the narrower
  `Category=FullValidation` diagnostic.
- The existing `RegressionIntegration` lane remains available for regression
  diagnosis; membership in that category does not force inclusion in
  PreMerge when a test also has `Category=DeepValidation`.

### When DeepValidation Runs

Run `DeepValidation` when at least one of these is true:

- validation rules, phase profiles, state schemas, snapshots, save/archive
  generation, or exhaustive fixture data changed;
- a Fast, PreMerge, or focused failure points to those domains;
- a release-risk review explicitly requests exhaustive validation.

Do not run it after ordinary edits or automatically after a green PreMerge.

Because this branch changes the tier boundary itself, its final verification
must run `DeepValidation` once.

## Initial Classification

The implementation reuses the established `Category=FullValidation` trait as
the primary exhaustive tier. Those tests move out of PreMerge without source
rewrites.

The following high-multiplicity regression family receives the additional
`Category=DeepValidation` trait:

- `GuardianSystemRegressionTests`.

This family is already separately sharded by reviewed Guardian profiles and
accounted for at least 1,240.68 active-test seconds in the partial PreMerge.

Before adding further families, implementation must generate a fresh PlanOnly
schedule and calculate the projected duration using the retained TRX map.
Additional `RegressionIntegration` tests may receive `DeepValidation` only
when both conditions hold:

1. retained evidence shows that the remaining PreMerge cannot fit with
   practical headroom under fifteen minutes; and
2. a representative test or equivalence sentinel for the same behavior
   remains in PreMerge.

This prevents category creep from becoming an unreviewed coverage reduction.

## Sentinel Contract

PreMerge must retain inexpensive evidence that the deep tier remains connected
to the same production behavior:

- full-versus-targeted validation equivalence remains in PreMerge;
- validation phase/profile selection guards remain in PreMerge;
- Guardian profile coverage and shard-boundary guards remain in PreMerge;
- at least one representative Guardian validation scenario remains in
  PreMerge outside the exhaustive matrix;
- archive/save loadability and browser/console command-display smoke checks
  remain in PreMerge even when their full theory matrices are deep;
- boundary tests prove that no deep category leaks into PreMerge.

Sentinels use a dedicated `Category=PreMergeSentinel` trait:

- `FullValidationEquivalenceTests` changes from `FullValidation` to
  `PreMergeSentinel`.
- a dedicated Guardian representative scenario is added outside the
  class-level deep category;
- existing lightweight archive/loadability and command-display smoke Facts
  move outside class-level `FullValidation`, while their exhaustive theories
  retain `FullValidation`.

`PreMergeSentinel` is included by the normal integration filter and is never
selected by `DeepValidation`. Combining `PreMergeSentinel` with
`FullValidation` or `DeepValidation` is forbidden by the partition guard.

## Partition and Coverage Invariants

The runner and boundary tests must prove all of the following:

1. `PreMerge` and `DeepValidation` integration selections are disjoint.
2. Their union, together with `ProcessIntegration` and `E2E`, covers every
   discovered Integration test.
3. No Integration test is unclassified by that partition.
4. No descriptor selection token appears twice within a lane.
5. `ProcessIntegration` and `E2E` appear exactly once in PreMerge and never in
   DeepValidation.
6. All `FullValidation` tests appear in DeepValidation and not in PreMerge.
7. All `DeepValidation` tests appear in DeepValidation and not in PreMerge.
8. All named sentinels appear in PreMerge and not in DeepValidation.
9. The Fast project still cannot discover Integration tests.
10. No test combines `PreMergeSentinel` with `FullValidation` or
    `DeepValidation`.

After classification, PlanOnly establishes reviewed numeric floors for both
lanes. Those exact floors are committed in runner/boundary tests; lowering
either floor later requires an explicit test and documentation change.

Executed theory-row counts can exceed discovery counts. Final acceptance
records both planned discovery counts and actual TRX result counts.

## Performance Work

Tiering restores a usable gate first. Test-internal optimization remains
valuable and follows this risk order:

1. Reuse an immutable prepared save template for command-display matrices,
   while cloning a unique mutable root and services per case.
2. Replace repeated broad validation in ordinary representative fixtures with
   reviewed phase selections, guarded by full-versus-targeted equivalence.
   Exhaustive broken/fixed matrices remain unchanged in DeepValidation.
3. Build an immutable Guardian snapshot template and use copy-on-write
   per-case overlays. Every case keeps its own root, manifest checks, hashes,
   and assertions.
4. Only after accepted tier timings exist, use retained descriptor durations
   for long-first scheduling. Current modeling shows this is a tail
   improvement, not the primary fix.

No cache may share mutable state, service instances, locks, pending requests,
or output directories between test cases.

## Failure and Cleanup Behavior

- Both lanes use one absolute deadline.
- A timed-out lane exits nonzero and records the active/incomplete descriptor.
- The runner stops only exact process trees it owns.
- A live owned process is never removed from the registry or disposed until
  exit is confirmed.
- Duplicate detection ignores repeated theory rows within one TRX but rejects
  the same assembly/test ID across descriptors.
- Missing TRX storage mappings fail closed.
- PlanOnly does not build, run frontend verification, or start tests.

## Acceptance

Implementation is accepted only when fresh evidence shows:

- three sequential project/solution builds pass with zero warnings and errors;
- two consecutive Fast runs pass below five minutes;
- one DeepValidation run passes below fifteen minutes;
- one PreMerge run passes below fifteen minutes, preferably below ten;
- both lanes have zero failed tests, duplicate IDs, and cleanup failures;
- partition/union guards pass;
- planned and actual counts are documented;
- the production solution membership is unchanged;
- Serena indexing succeeds and health-check is green;
- `.serena/`, `bin/`, `obj/`, TestResults, logs, and TRX files are not
  committed.

Do not serially run all diagnostic lanes before or after these controls.

## Non-Goals

- Increasing the fifteen-minute deadline.
- Increasing test-process concurrency above four.
- Weakening or deleting assertions.
- Reclassifying production behavior as untested.
- Making DeepValidation part of the default command.
- Rewriting the Serena installation.
- Changing gameplay behavior or production timeouts.
