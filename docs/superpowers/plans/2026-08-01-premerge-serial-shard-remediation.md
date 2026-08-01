# PreMerge Serial-Shard Remediation Plan

> Issue: #1505
> Branch: `work/1505-test-suite-performance`
> Required base: `2083def2`

## Goal

Make the bounded PreMerge lane complete below its existing fifteen-minute
deadline while preserving both:

- xUnit's declared non-parallel contract for
  `GameEngineTurnLifecycleTests`; and
- the process isolation provided by the two retained method shards.

Do not increase the deadline or process concurrency, delete tests, change
categories, weaken assertions, retry a failed full lane, share mutable test
roots, or change production behavior.

## Accepted Evidence

R5 replaced two `93`-case GameEngine method shards with one early `186`-case
class descriptor. Its independent review and PlanOnly evidence were clean, but
the one allowed fresh PreMerge run proved that this process shape is not
viable:

- runner wall: `00:15:00.2801897`, exit `124`;
- `4053/4053` completed results passed;
- failures `0`, duplicates `0`, cleanup complete;
- the one `186`-case GameEngine descriptor started in the first parallel wave,
  remained active for at least about `14:14`, emitted no TRX, and was killed at
  the deadline;
- ProcessIntegration and E2E never started.

Artifact:

```text
TestResults/test-lanes/
20260801-142856-329-37792-80f011103fc6417abf5c86d3b03a5658-premerge
```

The pre-R5 two-shard shape has stronger retained process-isolation evidence:

- each shard contains `93` discovered cases;
- matching historical descriptor walls were
  `00:03:29.5368488` to `00:03:44.0654535`;
- the one allowed exact focused diagnostic for the previously killed half
  passed `93/93` in a TRX window of `00:02:44.8669022`;
- neither shard has a deterministic assertion or cleanup failure.

The R5 result therefore narrows the failure to process shape: concurrent
shards violate external serialization, while one combined testhost loses the
bounded behavior supplied by isolated method shards.

## R6: Externally Serialized Method Shards

### Descriptor contract

Extend a run descriptor with an optional `SerialGroup` string. Empty groups
remain ordinary descriptors. Descriptors with the same non-empty ordinal
group may never be active at the same time.

Keep the exact external-serialization set member:

```text
BookOfEternityClient.Tests.GameEngineTurnLifecycleTests
```

Balanced planning for every large class continues to use the existing method
bins. For a class in the external-serialization set:

1. retain the ordinary method-bin filters and isolated result paths;
2. set every resulting descriptor's `SerialGroup` to the exact class name;
3. retain each bin's real case count as `EstimatedCases`;
4. use the full discovered class count as `EstimatedCost` for each shard so
   the first shard starts in the initial long-first wave and the second becomes
   the first eligible member of its group after the first exits.

Ordinary large classes retain `SerialGroup = null`,
`EstimatedCases = bin.Weight`, and `EstimatedCost = bin.Weight`.

Expose `SerialGroup` in PlanOnly rows so the scheduling contract is auditable.

### Scheduler contract

Before selecting each pending descriptor, derive the ordinal set of non-empty
serial groups held by active descriptors. A pending descriptor is eligible
only when:

- it satisfies the unchanged Fast-project cap; and
- its serial group is empty or absent from the active-group set.

Recompute this set on every scheduling iteration. Keep the existing total
parallelism, Fast parallelism, one global deadline, owned-process lifecycle,
failure propagation, and exclusive ProcessIntegration/E2E phases unchanged.

### Expected plan

PreMerge:

- total descriptors: `24`;
- parallel descriptors: `22`;
- exclusive descriptors: one ProcessIntegration plus one E2E;
- current discovery: `4,679`;
- fixed required floor: `4,666`;
- GameEngine: exactly two rows, `93` cases each;
- both GameEngine rows: `EstimatedCost = 186`;
- both GameEngine rows: identical exact `SerialGroup`;
- no duplicate names, filters, or result paths.

DeepValidation remains `23` descriptors / `1,950` cases, contains no
GameEngine descriptor, and has no non-empty serial group. Complete remains a
PreMerge alias with a byte-identical raw plan block.

## TDD and Focused Verification

### RED

First replace the R5 boundary expectations in
`IntegrationTestBoundaryTests.CSharpLaneRunner_PreservesExternalSerializationForGameEngineTurnLifecycleTests`
with R6 expectations. Require the boundary to prove:

- source collection semantics remain `DisableParallelization = true`;
- the external-serialization set contains exactly the GameEngine class;
- large classes still pass through method grouping and balanced bins;
- special-class shards receive the exact serial group and full-class
  scheduling cost while retaining bin case counts and method-union filters;
- ordinary large classes retain null serial group and bin scheduling cost;
- the batch scheduler excludes an active serial group;
- PlanOnly exposes `SerialGroup`;
- concurrency, deadline, floors, categories, and exclusive phases are
  unchanged.

Run the exact boundary fact before changing the runner and capture the intended
failure.

### GREEN

Implement the smallest runner change and rerun:

1. the exact R6 boundary fact;
2. the existing non-overlapping PreMerge schedule fact;
3. all Integration runner boundary facts;
4. all Fast runner process/result/cleanup facts;
5. the IntegrationTests build;
6. PowerShell parser validation and `git diff --check`.

No Fast, DeepValidation, PreMerge, Complete, or diagnostic lane execution is
allowed during R6 implementation.

### PlanOnly

Run PreMerge, DeepValidation, and Complete in PlanOnly mode. Require the exact
plan invariants above and byte-identical Complete/PreMerge raw plan blocks.

Commit only:

- `scripts/test-csharp.ps1`;
- `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`.

Independently review the implementation before another full lane.

## Resume Task 17

After R6 is accepted:

1. rerun both Fast controls because the scheduler changed;
2. retain the accepted `2142/2142` DeepValidation result because the verified
   Deep plan still excludes GameEngine;
3. run PreMerge exactly once;
4. if green, reconcile documentation, run Serena and final static acceptance,
   and commit exactly the seven documentation files;
5. if red, inspect the exact artifacts once and do not rerun blindly.
