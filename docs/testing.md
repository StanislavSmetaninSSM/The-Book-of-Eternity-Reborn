# C# Test Lanes

Use PowerShell 7 and run the lane script from the repository root. The default
command is the ordinary fast feedback control:

```powershell
.\scripts\test-csharp.ps1
```

The runner owns only the process trees it starts. On Windows, each target
starts behind a gated launcher: the launcher enters a dedicated kill-on-close
Job Object before the gate opens, so the target and its descendants inherit
exact containment even when an intermediate root exits first. The runner
writes logs, TRX files, and `summary.json` below `TestResults/test-lanes/`,
enforces one lane-wide deadline, and reports cleanup failures as non-zero
evidence.

## Commands

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

`Complete` is a temporary alias for `PreMerge`; it has the same schedule and
the same 15-minute hard limit. New automation and documentation should use
`PreMerge`.

## Project and Lane Boundaries

| Lane | Project/selection | Hard limit | Intended use |
|---|---|---:|---|
| `Fast` (default) | Entire `BookOfEternityClient.Tests` project, with no category filter | 5 min | Ordinary post-edit feedback |
| `Focused` | Caller-supplied VSTest filter in the fast project | 5 min | One class, method, or domain during implementation |
| `FullValidation` | Integration project, `Category=FullValidation` | 15 min | Diagnostic full-pipeline checks |
| `RegressionIntegration` | Integration project, `Category=RegressionIntegration` | 15 min | Diagnostic file-backed workflow checks |
| `ProcessIntegration` | Integration project, `Category=ProcessIntegration` | 15 min | Diagnostic real-process checks |
| `E2E` | Integration project, `Category=E2E` | 15 min | Diagnostic end-to-end checks |
| `LifecycleIntegration` | Integration project, `Category=LifecycleIntegration`, one external test process | 10 min | Conditional complete GameEngine lifecycle control |
| `DeepValidation` | Integration project, union of `FullValidation` and `DeepValidation`, excluding lifecycle/process/E2E | 15 min | Conditional exhaustive validation control |
| `PreMerge` | Both projects in a non-overlapping schedule | 15 min total | One final integration control |

Fast selects `BookOfEternityClient.Tests.csproj` directly. It does not rely on
category exclusions to hide slow tests; source/project-boundary guards keep
integration sources out of that assembly. Fast uses balanced descriptors with
at most two fast test hosts.

The diagnostic lanes select
`BookOfEternityClient.IntegrationTests.csproj`. They are available when a
focused failure or a change in that boundary needs diagnosis; they are not
ordinary post-edit controls.

PreMerge uses one deadline across frontend verification, both test-project
builds, discovery, all tests, and owned-tree cleanup. It runs the full fast
assembly together with core integration tests excluding `FullValidation`,
`DeepValidation`, `ProcessIntegration`, `E2E`, ordinary
`LifecycleIntegration` tests, and exhaustive `RegressionIntegrationOnly`
matrices. Exactly ten reviewed lifecycle methods and ten representative
`AfterlifeSpiritualConflictValidationTests` methods carry
`Category=PreMergeSentinel` and remain in core Integration.
ProcessIntegration and E2E then run sequentially with non-overlapping filters.
External test-process concurrency is capped at four overall and two for the
fast project.

`AfterlifeSpiritualConflictValidationTests` remains complete and selectable:
`RegressionIntegration` runs all 358 cases. Run that diagnostic lane when
changing spiritual-conflict validation/normalization, investigating a related
failure, or when an exhaustive control is explicitly requested. Ordinary
changes and unrelated final merges use the exact ten-method sentinel manifest
through PreMerge; no test or assertion is deleted.

DeepValidation is conditional and explicit. It selects the Integration-only
union of `FullValidation` and `DeepValidation`, excluding ProcessIntegration
E2E, and LifecycleIntegration. LifecycleIntegration is also conditional and
explicit: it runs all 186 GameEngine lifecycle cases in one external process.
Neither lane is part of ordinary post-edit feedback or an automatic companion
to every PreMerge run.

### Duration-Aware Diagnostic Scheduling

DeepValidation, RegressionIntegration, and PreMerge discover their complete
bounded selections before building an execution plan. Retained duration costs
affect bin balancing and long-first ordering only; they never add, remove,
skip, or recategorize a discovered test. PreMerge applies the retained
RegressionIntegration costs to its overlapping core classes, while the
ten-case spiritual-conflict sentinel keeps its discovered-case cost instead
of inheriting the exhaustive class cost. Parameterized rows discovered
dynamically can make `-PlanOnly` `EstimatedCases` lower than the final count,
so the merged TRX summary is the authoritative coverage result.

Within DeepValidation, storage-heavy validation descriptors share one
scheduling group and therefore do not overlap. The state-only validator
reserves two of the existing four external-process slots while it runs, leaving
capacity for two ordinary descriptors; this is a capacity weight, not
additional parallelism. The weight is bounded by a caller's lower
`-Parallelism`, so a serial diagnostic run still makes progress. Both lanes
keep the same four-process ceiling and 15-minute hard deadline.

When either lane approaches its deadline, inspect one plan and the completed
TRX durations before changing it. Preserve every discovered case and
assertion; adjust retained costs, binning, or resource weights only from
measured evidence. Do not raise the timeout or concurrency to turn a capacity
failure green.

## Working Rhythm

During implementation, run the smallest relevant Focused filter first and one
Fast control at a meaningful checkpoint. Do not run every lane after every
edit. Immediately before merge, run one PreMerge control:

```powershell
.\scripts\test-csharp.ps1 -Lane PreMerge
```

Do not repeat Fast immediately before PreMerge solely as a ritual: PreMerge
already includes the complete fast project. Do not serially run all slow
diagnostic lanes before a green PreMerge. If a bounded control fails, inspect
its summary, log, and TRX evidence, then run only the smallest diagnostic lane
or focused filter needed to identify the cause. Run LifecycleIntegration or
DeepValidation when the changed boundary requires it, when diagnosing a
related failure, or for an explicitly requested exhaustive control.

`Focused` is the only lane that accepts `-Filter`. Use `-NoBuild` only after a
fresh successful build. `-PlanOnly` inspects a composed schedule without
starting frontend verification, builds, or tests.

## Category Boundaries

- `FullValidation` identifies intentional complete-pipeline sentinels.
- `RegressionIntegration` identifies file-backed Guardian, Explorer,
  GameEngine, browser-command, and local-host workflow regressions.
- `RegressionIntegrationOnly` keeps an exhaustive matrix in the explicit
  RegressionIntegration lane; reviewed `PreMergeSentinel` methods are its only
  routine PreMerge overlap.
- `ProcessIntegration` identifies tests that start a real child process.
- `E2E` identifies end-to-end console, Agent Console, and built-frontend
  workflows.
- `LifecycleIntegration` identifies the complete GameEngine turn-lifecycle
  class.
- `PreMergeSentinel` admits a reviewed small lifecycle/full-validation or
  regression-integration sample into PreMerge without admitting its complete
  heavy class or matrix.
- `DeepValidation` identifies the exhaustive Guardian regression matrix;
  the DeepValidation lane also includes `FullValidation`.

A slow test may carry more than one diagnostic category. PreMerge prevents
overlap by excluding FullValidation, DeepValidation, ordinary
LifecycleIntegration, and exhaustive RegressionIntegrationOnly tests from
core Integration, excluding E2E from its ProcessIntegration phase, and
selecting E2E alone. The intentional overlaps are exact ten-method lifecycle
and spiritual-conflict sentinel manifests. DeepValidation and PreMerge remain
disjoint.

## Results and Cleanup

Every invocation writes to a unique directory:

```text
TestResults/test-lanes/<timestamp>-<pid>-<guid>-<lane>/
```

The directory contains `dotnet-test.log`, one or more `.trx` files, and
`summary.json`. The JSON summary records requested/effective lane, hard limit,
wall time, exit code, timeout state, owned-tree cleanup result, test counters,
and cross-descriptor duplicate test IDs. Skipped tests are `Total - Executed`.

On failure or timeout the runner stops scheduling work. On Windows it
terminates only the dedicated Job Objects it created and verifies that every
containment is empty; the fallback for an uncontained root uses
`Process.Kill(true)` only while that exact owned root is alive. It never
enumerates or kills processes by name. A timeout returns exit code 124; any
failed descriptor, TRX parse error, duplicate composed-lane test ID, or
incomplete owned-tree cleanup is non-zero evidence.

## Historical #1505 Evidence

The accepted controls on the baseline Windows machine are:

| Control | Result | Tests | Runner wall | Result directory |
|---|---|---:|---:|---|
| Fast 1 | `PASS` | `2587/2587` | `2:59.057` | `20260801-195606-147-20340-c486827ab39b4cdf914e0c72bc8fde60-fast` |
| Fast 2 | `PASS` | `2587/2587` | `2:28.905` | `20260801-195915-638-5272-2d2fa823c35b48f08be4368f1a96dd16-fast` |
| LifecycleIntegration | `PASS` | `186/186` | `5:31.972` | `20260801-181656-093-3652-2665d79ca44447b685df3a20ddee9ca9-lifecycleintegration` |
| DeepValidation (retained) | `PASS` | `2142/2142` | `14:15.857` | `20260801-125643-609-35532-e202ce76a0004beda7e59ab8c0fe72f8-deepvalidation` |
| PreMerge | `PASS` | `4522/4522` | `12:12.687` | `20260801-200153-781-36812-b84a1ae9818741b9a67590fa9b40711e-premerge` |

The DeepValidation result was retained rather than repeated after PlanOnly
proved its 23-descriptor/1,950-case selection remained unchanged and excluded
GameEngine lifecycle tests. PreMerge included ProcessIntegration `440/440`,
E2E `15/15`, and exactly the ten reviewed lifecycle sentinels.

Both Fast controls met five minutes, LifecycleIntegration met ten minutes,
DeepValidation met 15 minutes and its 1,950-result floor, and the historical
#1505 PreMerge met its single 15-minute deadline and then-current 4,490-result
floor. Every accepted control
reported exit `0`, no failures, no duplicate IDs, no timeout, complete
owned-tree cleanup, and zero remaining owned processes. PreMerge did not meet
the preferred below-ten-minute target; its accepted runner time was
`12:12.687`.

The Phase 45 amendment uses a 4,240-result floor. Its final PlanOnly contract
contains 19 non-overlapping descriptors and 4,262 estimated cases; Theory rows
make the merged TRX count authoritative. The exact clean-checkout executable
result is retained in the #1502 PR/issue evidence before merge.

## Rejected All-Inclusive Evidence

The historical all-inclusive attempt was correctness-clean but
capacity-invalid: `15:00.393`, exit `124`, `4,738/4,738` completed tests
passed, failures `0`, duplicates `0`, cleanup succeeded, with a projected lower
bound of `25:37.741`. This capacity limit motivated the approved two-tier
design; it was not a correctness failure.

## Guardian Benchmark

The fixed benchmark is:

```powershell
.\scripts\test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~GuardianProjectValidation_OffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReason|FullyQualifiedName~GuardianProjectValidation_CompleteOffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReasonWhenActiveProjectLacksIt" `
  -TimeoutMinutes 2 `
  -NoBuild
```

Three post-change runs each reported about 3 seconds of test duration, with
wall times of 7.92, 7.32, and 7.64 seconds. The 3-second median is at least
6.7 times faster than the approximately 20-second pre-change test duration.
