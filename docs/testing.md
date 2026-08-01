# C# Test Lanes

Use PowerShell 7 and run the lane script from the repository root. The default
command is the ordinary fast feedback control:

```powershell
.\scripts\test-csharp.ps1
```

The runner owns only the process trees it starts. It writes logs, TRX files,
and `summary.json` below `TestResults/test-lanes/`, enforces one lane-wide
deadline, and reports cleanup failures as non-zero evidence.

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
`DeepValidation`, `ProcessIntegration`, `E2E`, and ordinary
`LifecycleIntegration` tests. Exactly ten reviewed
`Category=PreMergeSentinel` lifecycle methods remain in core Integration.
ProcessIntegration and E2E then run sequentially with non-overlapping filters.
External test-process concurrency is capped at four overall and two for the
fast project.

DeepValidation is conditional and explicit. It selects the Integration-only
union of `FullValidation` and `DeepValidation`, excluding ProcessIntegration
E2E, and LifecycleIntegration. LifecycleIntegration is also conditional and
explicit: it runs all 186 GameEngine lifecycle cases in one external process.
Neither lane is part of ordinary post-edit feedback or an automatic companion
to every PreMerge run.

## Working Rhythm

During implementation, run the smallest relevant Focused filter first and one
Fast control at a meaningful checkpoint. Do not run every lane after every
edit. At final verification, run two consecutive Fast controls and one
PreMerge control:

```powershell
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1 -Lane PreMerge
```

Do not serially run all slow diagnostic lanes before a green PreMerge. If a
bounded control fails, inspect its summary, log, and TRX evidence, then run only
the smallest diagnostic lane or focused filter needed to identify the cause.
Run LifecycleIntegration or DeepValidation when the changed boundary requires
it, when diagnosing a related failure, or for an explicitly requested
exhaustive control.

`Focused` is the only lane that accepts `-Filter`. Use `-NoBuild` only after a
fresh successful build. `-PlanOnly` inspects a composed schedule without
starting frontend verification, builds, or tests.

## Category Boundaries

- `FullValidation` identifies intentional complete-pipeline sentinels.
- `RegressionIntegration` identifies file-backed Guardian, Explorer,
  GameEngine, browser-command, and local-host workflow regressions.
- `ProcessIntegration` identifies tests that start a real child process.
- `E2E` identifies end-to-end console, Agent Console, and built-frontend
  workflows.
- `LifecycleIntegration` identifies the complete GameEngine turn-lifecycle
  class.
- `PreMergeSentinel` admits a reviewed small lifecycle/full-validation sample
  into PreMerge without admitting its complete heavy class or matrix.
- `DeepValidation` identifies the exhaustive Guardian regression matrix;
  the DeepValidation lane also includes `FullValidation`.

A slow test may carry more than one diagnostic category. PreMerge prevents
overlap by excluding FullValidation, DeepValidation, and ordinary
LifecycleIntegration from core Integration, excluding E2E from its
ProcessIntegration phase, and selecting E2E alone. The only intentional
LifecycleIntegration/PreMerge overlap is the exact ten-method sentinel
manifest. DeepValidation and PreMerge remain disjoint.

## Results and Cleanup

Every invocation writes to a unique directory:

```text
TestResults/test-lanes/<timestamp>-<pid>-<guid>-<lane>/
```

The directory contains `dotnet-test.log`, one or more `.trx` files, and
`summary.json`. The JSON summary records requested/effective lane, hard limit,
wall time, exit code, timeout state, owned-tree cleanup result, test counters,
and cross-descriptor duplicate test IDs. Skipped tests are `Total - Executed`.

On failure or timeout the runner stops scheduling work and uses
`Process.Kill(true)` only on exact process roots it created. It never
enumerates or kills processes by name. A timeout returns exit code 124; any
failed descriptor, TRX parse error, duplicate composed-lane test ID, or
incomplete owned-tree cleanup is non-zero evidence.

## Fresh Final Evidence

The accepted controls on the baseline Windows machine are:

| Control | Result | Tests | Runner wall | Result directory |
|---|---|---:|---:|---|
| Fast 1 | `PASS` | `2585/2585` | `4:21.152` | `20260801-180837-789-31004-7619904f0c9e49ba8d1716bec5a682f2-fast` |
| Fast 2 | `PASS` | `2585/2585` | `3:16.237` | `20260801-181321-123-18724-6adc52d358924335aaaaadf784f6ce9e-fast` |
| LifecycleIntegration | `PASS` | `186/186` | `5:31.972` | `20260801-181656-093-3652-2665d79ca44447b685df3a20ddee9ca9-lifecycleintegration` |
| DeepValidation (retained) | `PASS` | `2142/2142` | `14:15.857` | `20260801-125643-609-35532-e202ce76a0004beda7e59ab8c0fe72f8-deepvalidation` |
| PreMerge | `PASS` | `4518/4518` | `14:16.500` | `20260801-182302-896-28696-51cbcbdce4604dd48780a69819761273-premerge` |

The DeepValidation result was retained rather than repeated after PlanOnly
proved its 23-descriptor/1,950-case selection remained unchanged and excluded
GameEngine lifecycle tests. PreMerge included ProcessIntegration `440/440`,
E2E `15/15`, and exactly the ten reviewed lifecycle sentinels.

Both Fast controls met five minutes, LifecycleIntegration met ten minutes,
DeepValidation met 15 minutes and its 1,950-result floor, and PreMerge met its
single 15-minute deadline and 4,490-result floor. Every accepted control
reported exit `0`, no failures, no duplicate IDs, no timeout, complete
owned-tree cleanup, and zero remaining owned processes. PreMerge did not meet
the preferred below-ten-minute target; its accepted runner time was
`14:16.500`.

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
