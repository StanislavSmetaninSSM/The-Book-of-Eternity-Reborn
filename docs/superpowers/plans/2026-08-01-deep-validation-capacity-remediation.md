# DeepValidation Capacity Remediation Plan

> Issue: #1505
> Branch: `work/1505-test-suite-performance`
> Required base: `c2cdf7dfacb1756d7011387a10b7e9e41c247883`

## Goal

Make the exhaustive conditional verification fit below its existing
fifteen-minute hard deadline without:

- increasing timeout or process concurrency;
- deleting tests or weakening assertions;
- sharing mutable roots, services, locks, requests, or output directories;
- changing production/gameplay behavior;
- hiding capacity failures behind retries.

This plan resumes
`docs/superpowers/plans/2026-08-01-bounded-premerge-deep-validation.md`
after Task 17's first DeepValidation measurement.

## Measured RED

The single accepted diagnostic run is:

```text
artifact:
TestResults/test-lanes/
20260801-112027-861-6688-8e863717fa9a4f85ab9ed215706e4b35-deepvalidation

runner wall: 00:15:00.6717049
external wall: 902.3 seconds
exit: 124
completed: 1,551/1,551 passed
failures: 0
duplicates: 0
cleanup: succeeded
```

The plan contained 23 descriptors and 1,950 discovery cases.

- Completed: 14 descriptors, 1,404 planned cases, 1,551 actual TRX results.
- Completed descriptor slot-work: `00:52:12.4110628`.
- Even that incomplete work has a perfect four-slot lower bound of
  `00:13:03.1027657`, before build/discovery and all unfinished work.
- Killed: 4 descriptors / 364 planned cases.
- Never started: 5 descriptors / 182 planned cases.
- Remaining: 9 descriptors / 546 planned cases.

The failure is capacity-only. No assertion, build, duplicate, TRX, or cleanup
failure occurred.

The largest completed test-class active durations were:

| Class | Cases | Active time |
| --- | ---: | ---: |
| `GuardianSystemRegressionTests` | 405 | 1,268.35 s |
| `GuardianArchiveAndTradeRequestValidationTests` | 58 | 615.56 s |
| `ValidatorFixtureTests` | 98 | 589.40 s |
| `SarefMainStoryStateValidationTests` | 61 | 292.73 s |
| `MortalCommandDisplaySaveTests` | 95 | 291.83 s |
| `ChaosSeaCommandDisplaySaveTests` | 95 | 289.67 s |
| `ShiningAbodeCommandDisplaySaveTests` | 100 | 225.40 s |

The three exhaustive command-display classes perform the same expensive setup
for every theory row: create a root, copy clean dependencies, copy/load the
same save archive, load settings, refresh state, and then execute one read-only
display command.

## Remediation Rule

Follow the risk order already approved in
`docs/superpowers/specs/2026-08-01-bounded-premerge-deep-validation-design.md`.
Complete, review, and measure one stage before authorizing the next. Do not
stack speculative fixes.

## R1: Reuse Immutable Prepared Command-Display Templates

### Scope

Expected files:

- add one xUnit-free or integration-test-only prepared-template helper;
- modify:
  - `MortalCommandDisplaySaveTests.cs`;
  - `ChaosSeaCommandDisplaySaveTests.cs`;
  - `ShiningAbodeCommandDisplaySaveTests.cs`;
  - `IntegrationTestBoundaryTests.cs`.

Do not modify production code, fixture archives, assertions, theory data,
categories, runner filters, floors, deadlines, or concurrency.

### Required design

For each of the three save families:

1. Prepare the source save once per xUnit class fixture/process:
   - create a private template root;
   - create the required directory structure;
   - copy clean checkout dependencies once;
   - copy and load the source archive once;
   - load settings and refresh state once;
   - never execute a test command against the template.
2. For every theory case:
   - create a new unique mutable case root;
   - clone the immutable prepared template into that root;
   - create a new `FileSystemManager`, `StateManager`,
     `ValidationService`, and `ExplorerWebCommandService`;
   - load settings/refresh from the cloned root;
   - execute exactly the original command and original assertions;
   - delete only that case's owned root.
3. No service instance, mutable state, lock, pending request, or output path is
   shared between theory cases.
4. The existing lightweight PreMerge sentinel facts retain their independent
   archive loadability/repeatability checks.

### TDD

Before implementation, add exact boundary expectations proving:

- all three exhaustive classes use the prepared-template helper;
- their per-case execution helpers no longer copy/load the source archive;
- every case still receives a unique root and new service objects;
- all existing method/category manifests remain exact.

Run the focused boundary test and capture the intended RED. Implement the
smallest helper/change, then run parser/build/boundary GREEN.

### Focused performance verification

Run exactly one focused command after implementation:

```powershell
dotnet test BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj `
  --no-restore `
  --filter "(FullyQualifiedName~MortalCommandDisplaySaveTests|FullyQualifiedName~ChaosSeaCommandDisplaySaveTests|FullyQualifiedName~ShiningAbodeCommandDisplaySaveTests)&Category=FullValidation" `
  --logger "trx;LogFileName=command-display-template.trx" `
  --results-directory TestResults/command-display-template `
  --verbosity minimal
```

Require:

- every previously discovered FullValidation row still executes and passes;
- failures/skips remain zero;
- no assertion/category/member-data source changes;
- aggregate active duration falls materially from the measured
  `807.90` seconds (`291.83 + 289.67 + 225.40`);
- no owned temporary root remains after the process exits.

Commit and independently review R1 before any DeepValidation retry.

## R2: Narrow Repeated Validation Profiles

Only authorize R2 if R1 evidence plus the retained duration model still cannot
fit with useful safety margin.

Use full-versus-targeted equivalence guards before replacing any broad
validation call. Start with
`GuardianArchiveAndTradeRequestValidationTests`, whose 58 independent cases
consume `615.56` active seconds and repeatedly use
`IntegrationValidationProfiles.GuardianArchiveTrade`.

Preserve every case and assertion. A targeted profile must include every phase
capable of producing the assertion-owned issue codes for that scenario.

## R3: Reuse Immutable Guardian Snapshots

Only authorize R3 if R1/R2 are insufficient.

Build immutable prepared Guardian snapshots and apply copy-on-write per-case
overlays. Every case keeps a unique root, manifest/hashes, services, and
assertions.

## R4: Duration-Aware Scheduling

Only after test-internal work reduction is measured, use retained descriptor
durations for long-first scheduling. Scheduling cannot repair a work-capacity
deficit and must not be presented as the primary fix.

## Resume Task 17

After the minimum accepted remediation stages:

1. run fresh sequential builds;
2. run Fast twice;
3. run DeepValidation once;
4. only after green DeepValidation, run PreMerge once;
5. finish documentation, Serena, static acceptance, commit, review, and
   integration exactly as the parent plan requires.

Any failed full lane is inspected once and is never blindly rerun.
