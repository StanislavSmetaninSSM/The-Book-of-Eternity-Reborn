# Isolated Fast and Integration Test Projects

**Issue:** #1505
**Date:** 2026-07-31
**Status:** Approved for implementation

## Objective

Make the ordinary C# test command usable during development while retaining a
bounded, comprehensive pre-merge control.

The measured suite has two different workloads:

- fast tests suitable after every code change;
- filesystem-heavy, full-validation, process, and end-to-end tests suitable
  only for diagnosis and final pre-merge verification.

They must not share a test assembly. Category filters alone are insufficient
because an unfiltered `dotnet test` still starts the entire suite.

## Performance Budgets

| Control | Target | Hard limit | Intended use |
|---|---:|---:|---|
| Fast project | under 5 minutes | 5 minutes | after ordinary code changes |
| Focused selection | seconds to a few minutes | 5 minutes | active development |
| Full pre-merge control | under 10 minutes | 15 minutes | once before merge/issue closure |
| Individual diagnostic integration lane | as measured | 15 minutes | only when investigating a relevant problem |

The standard command's wall time includes its incremental build, discovery,
test execution, result collection, and owned-process cleanup. Performance is
not accepted by increasing production timeouts or by hiding failures.

## Project Architecture

### `BookOfEternityClient.TestSupport`

A non-test class library containing reusable fixture infrastructure:

- repository path discovery;
- temporary-root and independent fixture materialization;
- canonical snapshot preparation;
- JSON/file builders;
- pending-turn snapshot authority helpers;
- process ownership and test-only synchronization helpers.

It contains no discoverable tests. Helpers should be framework-neutral where
practical. Assertion-specific helpers may remain in the consuming test project
until moving them produces a clear benefit.

### `BookOfEternityClient.Tests`

The default, fast test assembly. It contains:

- unit tests;
- scoped validation tests;
- deterministic in-memory or small temporary-root tests;
- source guards that enforce the project boundary.

It must not contain:

- direct public full-pipeline validation calls;
- `FullValidation`, `RegressionIntegration`, `ProcessIntegration`, or `E2E`
  tests;
- tests that start real child processes;
- large file-backed regression classes whose measured inclusion pushes the
  standard command past five minutes.

### `BookOfEternityClient.IntegrationTests`

The explicit slow/pre-merge test assembly. It contains:

- intentional full-validation sentinels;
- large file-backed validation regressions;
- Guardian, Explorer, GameEngine, and host workflow regressions;
- real process tests;
- console, Agent Console, and built-frontend E2E tests;
- the large afterlife spiritual-conflict validation suite.

Files are physically moved into this project. Every partial test class moves
atomically with all of its partial source files.

The integration project references production code and
`BookOfEternityClient.TestSupport`; it does not reference the fast test
assembly.

The existing production-only solution remains production-only. Neither test
project is added to it merely for discovery. The repository scripts target the
test projects explicitly, preventing an ordinary solution command from
accidentally launching integration tests.

## Test Optimization Rules

Project separation protects the daily workflow, but the integration suite must
also be optimized.

1. The public `ValidateGameStateAsync()` pipeline is reserved for a small set
   of sentinels. At most eight direct broad calls may exist across the
   integration project, and every one must be explicitly categorized and
   justified by a source guard.
2. Domain tests use `GameStateValidationSelection` with only the required
   phases and, where applicable, a state-file allow-list.
3. Negative and positive assertions must execute the phase that owns the
   asserted diagnostic. A positive `DoesNotContain` assertion may not become
   vacuous by selecting no relevant phase.
4. Immutable repository fixtures are prepared once per test host. Each test
   receives an independent materialized root; shared mutable fixture files and
   hard-linked writable state are forbidden.
5. Polling and fixed delays are replaced with explicit hooks,
   `TaskCompletionSource`, or observable process events. Timeouts remain
   failure guards, not synchronization mechanisms.
6. Process cleanup is ownership-based. A runner may terminate only process
   trees it started.
7. Test assertions and gameplay contracts are preserved. Optimization may
   reduce setup and validation scope, but must not weaken expected behavior.

## Runner Contract

`scripts/test-csharp.ps1` remains the single C# entry point.

### Daily commands

```powershell
# Default: fast project only
.\scripts\test-csharp.ps1

# One fast class or method
.\scripts\test-csharp.ps1 -Lane Focused -Filter '<VSTest filter>'
```

`Fast` and the default invocation build and run only
`BookOfEternityClient.Tests`.

### Diagnostic integration commands

```powershell
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane RegressionIntegration
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
```

These lanes build and run only
`BookOfEternityClient.IntegrationTests`, with existing category filters.

### Final control

```powershell
.\scripts\test-csharp.ps1 -Lane PreMerge
```

`PreMerge` performs:

1. a fresh build of both test projects;
2. the complete fast project;
3. the complete integration project using bounded, non-overlapping shards;
4. the frontend prerequisite before built-frontend E2E tests;
5. TRX aggregation, duplicate detection, wall-time reporting, and owned-tree
   cleanup verification.

`Complete` remains a temporary compatibility alias for `PreMerge` during the
migration.

The pre-merge lane has one global 15-minute deadline. It does not grant a new
15-minute timeout to each phase.

## Source Guards

The fast project verifies:

- no slow category appears in its source files;
- no direct public full-validation call appears in its test sources;
- integration-only source files are excluded from its compile items;
- the integration project exists and includes the reviewed heavy sources.

The integration project verifies:

- the global broad-validation sentinel budget;
- every broad sentinel carries `FullValidation`;
- every process/E2E entry point has the reviewed category;
- scoped validation remains internal to production and test assemblies;
- all partial-class source groups are compiled into exactly one test project.

## Migration Sequence

1. Create `TestSupport` and integration projects with source-boundary guards.
2. Move slow category files and complete partial-class groups.
3. Restore compilation by moving only genuinely shared fixture infrastructure
   into `TestSupport`.
4. Make the default runner target the fast project without a slow-category
   exclusion filter.
5. Migrate remaining repeated broad calls to reviewed scoped profiles.
6. Replace polling/delay synchronization in measured timing-sensitive tests.
7. Add `PreMerge`, aggregate results, and retain `Complete` as an alias.
8. Run and measure fast and pre-merge controls; rebalance or optimize until
   both budgets pass.

## Acceptance Criteria

- An unfiltered test of `BookOfEternityClient.Tests` cannot discover an
  integration test.
- Two consecutive standard Fast runs pass within five minutes.
- No fast-project test directly invokes the public full validation pipeline.
- The integration project retains every moved baseline test plus reviewed new
  tests, without duplicate execution.
- A pre-merge run passes within 15 minutes, with a target below 10 minutes.
- Every bounded run writes machine-readable results and leaves no owned child
  process running.
- The production solution builds with zero warnings and errors.
- No gameplay, GM contract, or player-facing behavior changes as part of this
  work.

## Non-Goals

- Changing gameplay validation semantics.
- Increasing production timeouts to make overloaded tests pass.
- Running integration tests after every local edit.
- Adding the integration project to the default production solution.
