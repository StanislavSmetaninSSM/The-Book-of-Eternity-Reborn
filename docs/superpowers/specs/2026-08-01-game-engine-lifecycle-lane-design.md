# Game Engine Lifecycle Lane Separation Design

**Issue:** [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

**Goal:** Make tests support a short, predictable development loop while
preserving the complete `GameEngineTurnLifecycleTests` coverage as an explicit,
bounded verification option.

## Decision

The ordinary development loop is:

1. run the smallest focused test selection that covers the change;
2. run the default `Fast` lane when the change is ready for a broad local
   check;
3. run `PreMerge` once before final integration.

Exhaustive validation and lifecycle suites are not automatic steps after every
change. `DeepValidation` and the new `LifecycleIntegration` lane are explicit,
conditional controls. They run when their domain is changed, while diagnosing
a related failure, or once before final merge when the branch changes that
domain or its test infrastructure.

`PreMerge` keeps ten fast, representative lifecycle sentinels. The remaining
176 lifecycle cases move out of its normal Integration selection. The full 186
cases remain available through `LifecycleIntegration`.

## Evidence

The accepted Task 17 attempt 5 produced:

- two green Fast controls: `2585/2585` in `4:32.204` and `3:48.665`;
- a capacity-red PreMerge at `15:00.251`;
- `4053/4053` completed results passed with no duplicates and clean owned-tree
  cleanup;
- the first 93-case GameEngine shard remained alive for more than `13:25`
  beside the broader parallel workload and emitted no TRX;
- the second GameEngine shard and the exclusive ProcessIntegration/E2E phases
  never started.

The same 93-case shard completes alone in approximately `2:45`. Retained
successful GameEngine halves put the complete isolated class at approximately
five to seven minutes, comfortably inside a ten-minute dedicated-lane cap.

The selected ten sentinels have four retained successful samples each, no
failures, and a combined median active duration of `5.521` seconds. A fresh
exact focused control passed `10/10` with six seconds of test duration and
`13.329` seconds external wall time.

## Alternatives Considered

### Keep all lifecycle cases in PreMerge but isolate them

Running the full class alone inside PreMerge would avoid the catastrophic
contention, but it would add approximately five to seven serial minutes before
ProcessIntegration/E2E. That leaves little deadline margin and keeps routine
feedback unnecessarily slow.

### Optimize every lifecycle test before changing the lane boundary

Several scenarios contain deliberate timeout and recovery paths, while others
exercise broad file-backed lifecycle behavior. Optimizing them remains useful
when supported by focused evidence, but it is a larger and less predictable
project. It is not a prerequisite for making the development loop usable.

### Separate the exhaustive lane and retain sentinels

This is the selected design. It immediately removes the proven contention
boundary from routine PreMerge, preserves high-signal lifecycle coverage in the
ordinary gate, and keeps every exhaustive case available under a clear,
bounded command.

## Lane Contracts

| Lane | Purpose | Automatic during ordinary development | Hard cap |
|---|---|---:|---:|
| `Focused` | Smallest relevant TDD/debugging selection | Yes | caller-bounded, never above its lane cap |
| `Fast` | Default broad local feedback | Yes | 5 minutes |
| `PreMerge` | Final broad fast/core/process/E2E signal plus sentinels | Once before final integration | 15 minutes, target below 10 |
| `LifecycleIntegration` | All GameEngine lifecycle cases in one isolated testhost | No; conditional | 10 minutes |
| `DeepValidation` | Exhaustive full/deep validation groups | No; conditional | 15 minutes |
| `FullValidation`, `RegressionIntegration`, `ProcessIntegration`, `E2E` | Narrow diagnostics or owned phases | No; only when relevant | existing caps |

`Complete` remains a compatibility alias for `PreMerge`; it does not silently
add the conditional heavy lanes.

## Category and Filter Model

`GameEngineTurnLifecycleTests` changes from class-level
`Category=RegressionIntegration` to class-level
`Category=LifecycleIntegration`.

The core Integration filter becomes:

```powershell
$coreIntegrationFilter =
    "Category!=FullValidation&Category!=DeepValidation&" +
    "Category!=ProcessIntegration&Category!=E2E&" +
    "(Category!=LifecycleIntegration|Category=PreMergeSentinel)"
```

This excludes the exhaustive lifecycle class while admitting only methods
that also carry the reviewed `PreMergeSentinel` trait.

The dedicated filter is:

```powershell
$lifecycleIntegrationFilter =
    "Category=LifecycleIntegration&" +
    "Category!=ProcessIntegration&Category!=E2E"
```

The DeepValidation filter additionally excludes `LifecycleIntegration`.
ProcessIntegration and E2E remain exclusive phases after the ordinary
PreMerge parallel batch.

`LifecycleIntegration` is not balanced into multiple descriptors. It runs one
descriptor with effective external parallelism one. The R6 `SerialGroup`
scheduler machinery becomes dead after this boundary change and is removed
rather than retained as an unused second serialization mechanism.

## Exact Sentinel Manifest

Exactly these methods carry method-level `Category=PreMergeSentinel`:

1. `CheckLevelUpAsync_DoesNotAwardAlreadyProcessedLevelAfterEngineRestart`
2. `CollectAcceptedTurnRawStateIssuesAsync_DirectNpcCoreMutation_IsRejectedBeforeNormalization`
3. `RebindRuntimeAfterSessionReplacement_ActiveReplacementRebindsLoopAndClearsTransientState`
4. `WriteValidationRepairRequestAsync_GuardianScopeErrors_AddsConcreteHarnessRepairPacket`
5. `ProcessPlayerTurn_UnresolvedRealm_DoesNotCreatePendingDiceState`
6. `CleanupAcceptedTurnTerminalArtifactsAsync_WithoutIncarnationTrigger_RemovesTerminalContext`
7. `ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_ValidActiveManifest_Authorizes`
8. `TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_ResetsEnlightenmentAndPreservesInkFeathers`
9. `CreateCanonicalBaselineSnapshotAsync_PreservesAndHashesExactSnapshotBytes`
10. `RestorePreTurnBackup_BrowserDirectGachaPreservesExactPreSpendSoulBytes`

The manifest covers restart idempotence, accepted-state validation fencing,
session rebinding, repair-packet construction, turn admission, accepted-turn
cleanup, life-end authority, afterlife transition, canonical snapshot bytes,
and rollback bytes. It deliberately excludes long timeout/recovery scenarios
and lock-contention tests from the routine gate.

Boundary tests enforce:

- the exact class-level lifecycle category;
- the exact ten method-level sentinels;
- no additional lifecycle sentinel;
- no other lifecycle method-level lane category;
- the dedicated runner lane, filter, cap, result floor, and serial execution;
- the PreMerge sentinel exception and lifecycle exclusion;
- unchanged ProcessIntegration/E2E ownership and ordering.

## Coverage Floors

The existing reviewed PreMerge floor is:

```text
2574 Fast + 1637 core Integration + 440 ProcessIntegration + 15 E2E = 4666
```

Removing 186 exhaustive lifecycle cases and adding back ten sentinels gives:

```text
2574 + (1637 - 186 + 10) + 440 + 15 = 4490
```

Therefore:

- `PreMergeMinimumCases = 4490`;
- `LifecycleIntegrationMinimumCases = 186`;
- `DeepValidationMinimumCases = 1950` remains unchanged.

Current live discovery is expected to produce 4503 PreMerge planned cases
because it is 13 cases above the reviewed historical core baseline. PlanOnly
must confirm the exact current counts without executing the suite.

## Verification Strategy

Implementation follows TDD:

1. strengthen the boundary tests and observe them fail because the category,
   lane, filters, manifest, caps, and floors do not exist;
2. make the minimal category and runner changes;
3. pass the focused boundary class and runner self-tests;
4. pass the exact ten-sentinel selection;
5. verify `PreMerge`, `LifecycleIntegration`, and `DeepValidation` PlanOnly
   output and non-overlap;
6. run two Fast controls;
7. run `LifecycleIntegration` once;
8. run PreMerge exactly once.

The retained green DeepValidation runtime remains valid only if PlanOnly proves
that its filter and discovered membership are unchanged. Otherwise
DeepValidation runs once.

No production behavior, validation phase, gameplay contract, prompt, example,
schema, console, browser, or frontend behavior changes.
