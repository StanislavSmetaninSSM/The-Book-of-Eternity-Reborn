# PreMerge Contention Remediation Plan

> Issue: #1505
> Branch: `work/1505-test-suite-performance`
> Required base: `06b6c1bbccecd85044d377aa83cec36ac2dfc3ac`

## Goal

Make the bounded PreMerge lane complete below its existing fifteen-minute
deadline without:

- increasing the deadline or process concurrency;
- deleting tests, changing categories, or weakening assertions;
- retrying a failed full lane until it happens to pass;
- sharing mutable test roots or changing production behavior.

## Accepted Evidence

Fresh post-R1 verification at the required base produced:

- Fast #1: `2585/2585`, `00:02:59.0594052`, green;
- Fast #2: `2585/2585`, `00:02:57.0681967`, green;
- DeepValidation: `2142/2142`, `00:14:15.8568407`, green;
- PreMerge: exit `124`, `4145/4145` completed results passed,
  `00:15:00.2132745`, duplicates `0`, cleanup complete.

PreMerge artifact:

```text
TestResults/test-lanes/
20260801-131126-308-32936-620c433b1ac64066a9d9cfbf6aad1daa-premerge
```

All completed parallel descriptors had emitted green TRX files. The remaining
descriptor was:

```text
Integration-Base-GameEngineTurnLifecycleTests-13
93 discovered cases / 79 unique method filters
```

It occupied a process slot for an exact retained bound of
`00:07:58.657` to `00:08:28.021`, was killed at the lane deadline, and
prevented ProcessIntegration and E2E from starting.

The same exact 93-case filter has completed historically in
`00:03:29.5368488` to `00:03:44.0654535`. The one allowed focused diagnostic
after the failed lane passed `93/93` in a TRX window of
`00:02:44.8669022`; its slowest individual case was `35.7001131s`, and no
owned test process or test-artifact directory remained.

This proves a contention/scheduling failure, not a deterministic test hang or
correctness failure.

## Root Cause

`GameEngineTurnLifecycleTests` is explicitly assigned to:

```csharp
[CollectionDefinition(CollectionName, DisableParallelization = true)]
```

The runner balances large classes by method count and starts each resulting
descriptor as a separate `dotnet test` process. xUnit can enforce collection
serialization only inside one test process. The current PreMerge plan splits
the lifecycle class into two 93-case descriptors, so the runner bypasses the
class's declared non-parallel contract and may execute both pieces in
different processes at the same time.

Case-count sorting also gives each split descriptor cost `93`, placing this
I/O- and validation-heavy class late behind nominally larger descriptors.
Once merged, its cost is the full `186`, so the existing long-first ordering
starts it in the first available Integration slot while two Fast processes
remain separately capped.

## R5: Preserve External Serialization for Non-Parallel Large Classes

### Runner design

Add an explicit, reviewable set of large Integration classes whose xUnit
non-parallel contract must also be preserved across external descriptors. The
initial exact member is:

```text
BookOfEternityClient.Tests.GameEngineTurnLifecycleTests
```

When `New-SelectionRuns -Balanced` encounters one of these classes:

1. create exactly one descriptor for the class;
2. select it with a short exact class-prefix filter plus the lane's category
   filter;
3. retain the discovered class case count as both `EstimatedCases` and
   `EstimatedCost`;
4. do not create method bins for that class.

All other large classes keep the existing balanced method sharding. Process
limits, Fast limits, lane filters, case floors, phase ordering, and the single
deadline remain unchanged.

Expected PreMerge plan change:

- parallel descriptors: `22 -> 21`;
- total descriptors: `24 -> 23`;
- GameEngine descriptors: `2 x 93 -> 1 x 186`;
- the required floor remains `4,666`;
- live discovery is `4,679`:
  `2,574 Fast + 1,650 core Integration + 440 ProcessIntegration + 15 E2E`;
  this is the retained `4,678` discovery plus the new R5 boundary Fact;
- ProcessIntegration and E2E remain exclusive final phases.

The single class filter avoids joining both existing method filters into a
near-Windows-command-line-limit expression.

### TDD

Before changing the runner, add a boundary contract that proves:

- the source class still declares `DisableParallelization = true`;
- the runner's external-serialization set contains the exact class;
- externally serialized large classes take the one-descriptor class-filter
  path before normal method binning;
- ordinary large classes still use the existing method-bin path;
- deadlines, concurrency caps, filters, and case floors are unchanged.

Capture the focused boundary RED, implement the smallest runner change, then
require the focused boundary GREEN.

### Plan verification

Run PreMerge `-PlanOnly` and require:

- exit `0`;
- `23` unique plan rows;
- `21` parallel rows plus one ProcessIntegration and one E2E row;
- `4,679` currently discovered cases and at least the unchanged `4,666`
  required floor;
- exactly one GameEngine row with `186` cases;
- no duplicate filters or result paths;
- DeepValidation plan remains `23` descriptors / `1,950` cases and contains
  no GameEngine row;
- maximum concurrency remains four total and two Fast.

Run the existing runner self-tests and `git diff --check`. Commit and
independently review R5 before another PreMerge attempt.

## Resume Task 17

After R5 is accepted:

1. rerun both Fast controls from the new runner commit;
2. retain the already accepted DeepValidation result because R5 is gated to a
   class excluded by the DeepValidation filter, and prove the Deep plan is
   byte-equivalent in coverage/count;
3. run PreMerge exactly once;
4. if green, reconcile documentation, run Serena and final static acceptance,
   and commit exactly the seven documentation files;
5. if red, inspect once and do not rerun blindly.
