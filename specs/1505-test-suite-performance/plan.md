# Implementation Plan: Test Suite Performance and Verification Lanes

**Branch**: `work/1505-test-suite-performance` | **Date**: 2026-07-31 | **Spec**: [spec.md](spec.md)

**Input**: Approved feature specification from `specs/1505-test-suite-performance/spec.md`.

## Summary

Keep the public production validator behaviorally unchanged while adding an
internal, non-empty flags-based selection of the existing 26 ordered validation
phases. Prove equivalence and fail-closed selection behavior first, then migrate
295 broad guardian-suite calls to reviewed test-side profiles. Physically split
fast and integration sources around a dependency-free TestSupport library, add
enforceable slow-test traits and project/source guards, retain an isolated
prepared Guardian fixture snapshot, and use a project-routed,
discovery-balanced PowerShell runner with one deadline and retained
JSON/TRX/log evidence.
Keep the complete GameEngine turn-lifecycle class in an explicit
LifecycleIntegration lane and retain only ten reviewed lifecycle sentinels in
routine PreMerge.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7/Windows PowerShell-compatible lane script.

**Primary Dependencies**: Existing `ValidationService`, `FileSystemManager`, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, `dotnet test`.

**Storage**: Existing file-backed JSON fixtures plus ignored
`TestResults/test-lanes/` logs, summaries, and TRX output.

**Testing**: xUnit focused filters, source/project guards, bounded benchmark
runs, two final Fast controls, one conditional DeepValidation control for this
category-boundary change, one conditional LifecycleIntegration control, and
one final bounded PreMerge control.

**Target Platform**: Local Windows development machine; implementation remains portable .NET code.

**Project Type**: Local console/browser game-client repository with one runtime
project, a non-test TestSupport library, and physically separate fast and
integration test projects.

**Performance Goals**: At least 5x on the fixed two-test guardian benchmark;
Fast at most 5 minutes; LifecycleIntegration at most 10 minutes;
DeepValidation and PreMerge at most 15 minutes;
PreMerge preferably below 10 minutes on the baseline machine.

**Constraints**: Public validation still runs all 26 phases in canonical order; no gameplay, state schema, issue-code, prompt, documentation-example, console, browser, or frontend behavior changes; no unbounded full-suite run.

**Scale/Scope**: 6,560 discovered cases, 965 broad validation calls, 460 guardian cases, and 295 broad guardian calls across eight partial source files.

**Source Issue(s)**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

**Contract Scope**: Internal validation orchestration and test infrastructure only.

**Verification Commands**:

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.sln --no-restore --verbosity minimal
dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal
dotnet build BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj --no-restore --verbosity minimal
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

The diagnostic lanes are not serial final gates. Run focused controls during
implementation, two consecutive Fast controls at final verification, and one
PreMerge control. Do not serially run all diagnostic lanes before PreMerge
unless a focused failure requires diagnosis. `Complete` is a temporary alias
for `PreMerge`. LifecycleIntegration and DeepValidation are conditional and
explicit; this branch runs each once because their category boundaries change.

## Constitution Check

*GATE before research: PASS. Re-check after design: PASS.*

- **GitHub traceability**: Issue #1505 is open, in progress, and linked from all active Spec Kit artifacts.
- **Spec Kit fit**: The implementation spans production validation orchestration, many guardian test files, traits/source guards, scripts, documentation, and multiple sessions.
- **Player-facing integrity**: No console, browser, copy, or player interaction changes.
- **Contract/state authority**: Validation rule bodies, issue codes, canonical schemas, state normalization, and GM-authored contracts remain unchanged. Mortal/afterlife prompts, examples, manifests, and contract matrices therefore need no update.
- **Test-first path**: Selection API compilation/fail-closed/equivalence/order tests precede production edits; source guards precede guardian migration and lane categorization.
- **Verification evidence**: Focused tests, bounded benchmark, project/source
  guards, three builds, two Fast summaries, one LifecycleIntegration summary,
  one DeepValidation summary, one PreMerge summary, Serena health, and final
  diff checks are required.
- **Agent orchestration**: Work remains in the current Codex session. No subagent report will be treated as verification evidence.

## Project Structure

### Documentation (this feature)

```text
specs/1505-test-suite-performance/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

No external API or persisted-data contract is added, so `contracts/` is not required.

### Source Code

```text
BookOfEternityClient/
└── Services/
    ├── ValidationService.cs
    └── Validation/
        ├── GameStateValidationPhase.cs
        └── ValidationService.ValidationPhases.cs

BookOfEternityClient.Tests/
├── ValidationPhaseSelectionTests.cs
├── FastTestBoundaryTests.cs
└── ordinary fast sources

BookOfEternityClient.TestSupport/
└── shared fixtures and helpers without test-SDK/xUnit dependencies

BookOfEternityClient.IntegrationTests/
├── GuardianValidationProfiles.cs
├── GuardianSystemRegressionTests*.cs
├── GameEngineTurnLifecycleTests.cs
├── IntegrationTestBoundaryTests.cs
├── TestLaneSourceGuardTests.cs
└── full-validation, regression-integration, process, and E2E sources

scripts/
└── test-csharp.ps1

docs/
└── testing.md
```

**Structure Decision**: Keep phase selection in the runtime validation namespace
because the public facade and internal test overload share the same dispatcher.
Keep shared fixtures in a non-test support library. Keep ordinary fast sources
and slow integration sources in separate test projects without a reverse
IntegrationTests-to-Tests reference. Use one repository script and one testing
guide as the stable local interface.

## Phase 0: Research and Baseline

1. Preserve the issue comment's bounded measurements and fixed counts.
2. Pin the two-test guardian benchmark in `research.md` and `quickstart.md`.
3. Inventory direct broad validation files and real process/E2E classes.
4. Record why test filtering alone, Release builds, fixture-copy optimization,
   and parallelization alone do not address the measured multiplier.

## Phase 1: Validation Selection, Test First

1. Add RED tests that reference the missing internal selection API.
2. Cover empty and unknown masks, single-phase isolation, canonical combined
   order, all-phase/public equivalence, and repeated-call state isolation.
3. Add an internal 32-bit flags enum with 26 single-bit phases and `All`.
4. Route the public no-argument method through `All`.
5. Make the existing three phase groups conditionally dispatch phases in their
   original order, with one mask validation at the top.
6. Run the new focused tests and representative existing validation tests.

## Phase 2: Guardian Migration

1. Add test-side named profiles for the eight guardian partial-domain files.
2. Add a RED source guard enforcing no more than eight broad guardian calls and
   requiring any survivor to carry `FullValidation`.
3. Mechanically replace broad calls with the appropriate reviewed profile.
4. Run representative methods from each domain; add only the missing phase
   demonstrated by a failed assertion.
5. Run all guardian cases under a bounded command and compare discovery/results.
6. If the complete/fast budget is still missed, use discovery-validated,
   non-overlapping domain/method chunks. Keep the shared partial class intact
   and do not introduce shared mutable fixtures merely to obtain parallelism.
7. If repeated fixture initialization remains material, capture one prepared
   fixture snapshot in memory per test host and prove independent materialized
   roots.

## Phase 3: Verification Lanes

1. Add class- or method-level `FullValidation`, `RegressionIntegration`,
   `LifecycleIntegration`, `PreMergeSentinel`, `ProcessIntegration`, and `E2E`
   traits to the inventoried slow groups.
2. Extract common fixtures/helpers into `BookOfEternityClient.TestSupport`
   without a test SDK or xUnit dependency.
3. Move every reviewed slow source into
   `BookOfEternityClient.IntegrationTests`; keep the fast project independent
   from integration discovery.
4. Add source/project guards for partial-class ownership, dependency direction,
   direct/fixture-mediated full validation, file-backed regression integration,
   and known process/E2E entry points.
5. Implement `scripts/test-csharp.ps1` with explicit project routing, hard
   five-/fifteen-minute caps, one deadline, timestamp/PID/GUID result
   directories, JSON/TRX/log output, cross-descriptor duplicate detection,
   a gated Windows launcher assigned to kill-on-close Job Object containment
   before target release, exact owned-tree verification after root exit, and
   `Process.Kill(true)` only as an uncontained-live-root fallback.
6. Test plan construction, executable process lifecycle, TRX aggregation, and
   source/project guards without launching an actual full suite.

## Phase 4: Performance and Integration Verification

1. Run the fixed two-test guardian benchmark at least three times after build;
   compare median runner time with the approximately 20-second baseline.
2. Run focused migration batches, every reviewed Guardian domain selection,
   and the retained broad-sentinel manifest under bounded controls.
3. Build the production solution and both test projects sequentially.
4. Run two consecutive Fast controls below five minutes each.
5. Run LifecycleIntegration exactly once; require all 186 reviewed cases below
   its ten-minute cap.
6. Retain the accepted DeepValidation result because PlanOnly proves the
   23-descriptor/1,950-case selection is unchanged and excludes lifecycle
   tests; require at least 1,950 results below 15 minutes.
7. Run exactly one PreMerge control below its single 15-minute deadline,
   retaining JSON/TRX/log evidence and at least 4,490 results, including
   completed ProcessIntegration and E2E phases.
8. Re-index Serena, confirm green health, no owned child processes,
   `git diff --check`, Spec Kit consistency, and review findings.

Final verification does not serially execute all diagnostic lanes. A failing
bounded control is narrowed with only the smallest relevant focused or
diagnostic selection.

## Complexity Tracking

No constitution violations require justification. The production enum/overload
is internal, uses the existing phase methods, and adds no new persistence or
external service.
