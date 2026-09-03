# Fast Lane Boundary Repair Design

**Issue:** [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

**Related work:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), existing test-lane specification in `specs/1505-test-suite-performance/`

**Status:** Approved in conversation on 2026-09-04

## Goal

Restore `Fast` as ordinary broad development feedback without deleting tests,
hiding coverage, increasing its timeout, or increasing its process ceiling.
The preferred wall time is approximately three minutes and the existing hard
limit remains five minutes, including build, discovery, test execution, result
merge, and owned-process cleanup.

The work remains on `1536-complete-wound-materialization`. A separate branch is
explicitly forbidden for this correction so the wound implementation and the
test boundary that it exposed cannot be merged or forgotten independently.
The implementation still uses separate commits and issue #1551 for auditability.

## Evidence

The B.5 Fast checkpoint produced this measured baseline:

- current discovery: 7,797 cases in 29 descriptors;
- exact Fast timeout: five minutes, no cleanup leak;
- four ordinary mixed shards measured without build at approximately 3:30,
  3:55, 1:05, and 4:00;
- the slow shards contain canonical filesystem/restart lifecycles, real worker
  processes, in-process HTTP hosts, browser service parity, canonical-lock
  contention, save/load, and large state-machine matrices;
- pure wound/effect contract, reducer, parser, and source-guard tests remain
  comparatively cheap even when they contain many cases.

The primary result artifacts are:

- `20260904-000550-919-43636-b42382f2bb2841d38db26b40ed621cbc-fast`;
- `20260904-001555-433-24344-01486f5ea356479c873a6c64584ec8a9-fast`;
- `20260904-002056-551-17984-dd8cad0e4e614b5b97da9844ee857f25-focused`;
- `20260904-002511-084-54336-7c0b492eb7a447f99948812aaea7d382-focused`;
- `20260904-002652-980-43412-c00c4d721189421daac0eda6c6cb4544-focused`.

The observations invalidate two shortcuts. Test-count balancing is not a useful
proxy for elapsed time, and the five-minute deadline is not the cause: multiple
individual shards already consume most of it before normal build overhead.

## Decision

Repair the physical test-project boundary and preserve the lane contract.

### Integration-heavy classes

A class moves in full from `BookOfEternityClient.Tests` to
`BookOfEternityClient.IntegrationTests` when its behavior is materially defined
by one or more of these boundaries:

- physical canonical files, restart/reload, save/load, or multi-file rollback;
- canonical lease contention, real-time waits, or concurrency fencing;
- a real child process or worker executable;
- an HTTP listener/host or end-to-end browser transport flow;
- a full file-backed lifecycle rather than a detached reducer/parser contract.

The initial mandatory review set includes
`MortalWoundRecoveryTests`,
`MortalWoundTreatmentCapabilityAuthorityTests`,
`QteSceneServiceTests`,
`GmWorkerLiveSmokeTests`,
`LocalWebUiSmokeTests`,
`BrowserMortalWorldGenerationFencingTests`, and
`BrowserStorageTransportParityTests`. The measured shard rankings are then
reviewed in descending elapsed-time order until two representative Fast runs
have honest headroom below the hard limit. A file is never moved merely because
its name contains `Browser`, `Service`, or `Validation`.

### Large pure matrices

A detached, deterministic parser/reducer/contract class stays in Fast by
default. If its measured class time still threatens the lane, split its source
at a semantic boundary:

- keep a small, explicitly reviewed happy-path and fail-closed sentinel set in
  the fast project;
- move the exhaustive matrix to the integration project with
  `RegressionIntegration` and `RegressionIntegrationOnly`;
- add the exact sentinels to `PreMergeSentinel` only when the ordinary core
  integration filter would otherwise exclude the complete matrix.

No theory row or assertion is deleted. The split must use named source and
method manifests, not a runtime duration check or an order-dependent filter.

`DarenQteShowcaseTests` is a concrete mixed-source example. Its 67 detached
route/prose/reducer/contract methods (77 discovered rows) belong in fixture-free
Fast `DarenQteDeterministicLogicTests`; its 12 canonical profile, filesystem,
service, and browser-projection methods (12 rows) remain in Integration
`DarenQteShowcaseTests` with `RegressionIntegration`. The split duplicates only
the static constants/helpers required by both owners and preserves every test
method, theory row, and assertion exactly once.

### Category routing

| Behavior | Project/category |
|---|---|
| Canonical file/restart/rollback/lifecycle | Integration / `RegressionIntegration` |
| Exhaustive integration matrix | Integration / `RegressionIntegration` + `RegressionIntegrationOnly` |
| Real child process | Integration / `ProcessIntegration` |
| True end-to-end host/browser flow | Integration / `E2E`, plus `ProcessIntegration` when a child process is started |
| Detached deterministic unit/contract/source guard | Fast project, no diagnostic category |

Moving a class must preserve its ability to run through `Focused` with
`-FocusedProject Integration`. PreMerge continues to cover ordinary regression
integration sources; excluded exhaustive matrices retain only their reviewed
sentinels in routine PreMerge and remain complete in `RegressionIntegration`.

## Boundary Enforcement

Extend the existing exact source/category guards rather than adding a permissive
`Category!=Slow` filter to Fast:

- `FastTestBoundaryTests` names every reviewed integration-heavy source and
  proves it exists only in the integration project;
- `IntegrationTestBoundaryTests` owns the exact category manifest and parses
  the expected test class with Roslyn so only class-level
  `Trait("Category", ...)` attributes satisfy ownership; comments, string
  literals, and method-level traits are rejected;
- executable Roslyn manifests pin every Fact/Theory method and normalized
  `InlineData` row for QTE (66 Fast / 51 Integration) and Daren (77 Fast / 12
  Integration), failing on missing, extra, duplicated, or changed rows;
- the integration project must not reference the fast test project;
- shared helpers required by both projects move to the dependency-free
  `BookOfEternityClient.TestSupport` assembly only when duplication cannot be
  avoided;
- the runner retains `Fast` project selection, five-minute limit, two-fast-host
  ceiling, duplicate detection, and owned-process containment unchanged.

The guard must fail when a reviewed heavy source is copied back into Fast or
loses its required category.

## Failure Handling

The current feature branch contains intentional RED contours for later wound
work. Categorization follows test semantics, not whether a test is currently
green. An intentional RED is recorded against its owning issue/spec and remains
discoverable in its correct lane; it is never skipped or moved solely to make a
summary green.

Unrelated timeout failures are rerun only in the smallest exact Focused
selection. A test that passes alone but times out under Fast load is still
evidence that its real-time or contention behavior belongs outside ordinary
unit feedback.

## Alternatives Rejected

### Increase the Fast timeout

Rejected. It renames a growing broad suite without repairing its feedback
contract and contradicts the approved five-minute boundary.

### Add `Category=Slow` and exclude it in the fast project

Rejected. It leaves integration dependencies in the unit assembly, makes
coverage depend on negative filters, and weakens the existing physical source
guards.

### Increase Fast process parallelism

Rejected as the first remedy. Prior measurements show resource contention can
turn normally green canonical-lock and worker tests into timeouts. The current
two-host ceiling remains until the project boundary is correct and a separately
tracked measurement proves a safe reason to change it.

### Move every class above a numeric duration threshold

Rejected. Aggregate xUnit class time includes useful parallel work and does not
identify semantics. Duration selects the review order; dependencies and
behavior determine the destination.

## Verification

Implementation follows RED to GREEN:

1. add exact source/category guard expectations and observe the unmoved or
   uncategorized sources fail them;
2. move one coherent behavior group at a time and run its exact Focused
   integration selection;
3. run Fast PlanOnly after every group to confirm unique, non-overlapping
   membership and descriptor changes;
4. measure Fast after the mandatory set, then continue down the measured
   ranking only when the result lacks reasonable headroom;
5. finish with two representative bounded Fast controls, each no more than five
   minutes and preferably approximately three minutes;
6. run conditional `RegressionIntegration`, `ProcessIntegration`, or `E2E`
   only for categories changed by the final set;
7. immediately before an explicitly requested merge, run one `PreMerge`
   control instead of duplicating Fast.

Every accepted run must report complete owned-tree cleanup and zero duplicate
test IDs. Known branch REDs are classified by exact test and owning contour;
they do not excuse a timeout or missing result membership.

## Documentation and Product Scope

Update `specs/1505-test-suite-performance/`, `docs/testing.md`, and the test
boundary/source guards with the measured #1551 amendment. This changes test
placement and verification scheduling only. It changes no production code,
gameplay behavior, state schema, player command, GM-authored contract, prompt,
example, manifest, browser UI, console UI, or afterlife runtime contract, so no
GM or afterlife documentation update is required.
