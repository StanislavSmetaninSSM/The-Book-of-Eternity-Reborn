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
routine PreMerge. The Phase 45 capacity amendment tracked by #1502 likewise
keeps all 358 afterlife spiritual-conflict cases in RegressionIntegration and
admits only an exact ten-method sentinel manifest to routine PreMerge.
The #1526 suite-growth correction keeps the same plan and caps, retains measured
PreMerge costs for underestimated integration classes, and removes repeated
save loading from the browser-presentation audit through prepared templates
with isolated per-row clones.
The #1547 measured deadline correction preserves that complete schedule and
raises only the globally bounded PreMerge deadline from 20 to 30 minutes after
the green core plus isolated process/E2E lower bound stopped fitting the former
cap.
The #1551 Fast-boundary repair keeps the five-minute Fast contract and existing
runner ceilings unchanged. It moves measured canonical I/O, restart,
contention, process, host, and end-to-end workflows into the Integration
project with exact category manifests, while extracting deterministic QTE
grading/input tests from their file-backed fixture so they remain in Fast.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7/Windows PowerShell-compatible lane script.

**Primary Dependencies**: Existing `ValidationService`, `FileSystemManager`, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, `dotnet test`.

**Storage**: Existing file-backed JSON fixtures plus ignored
`TestResults/test-lanes/` logs, summaries, and TRX output.

**Testing**: xUnit focused filters, source/project guards, bounded benchmark
runs, two final Fast controls, the diagnostic Integration categories changed by
the source moves, one conditional DeepValidation control for the historical
category-boundary change, one conditional LifecycleIntegration control, and
one final bounded PreMerge control only when merge is explicitly requested.

**Target Platform**: Local Windows development machine; implementation remains portable .NET code.

**Project Type**: Local console/browser game-client repository with one runtime
project, a non-test TestSupport library, and physically separate fast and
integration test projects.

**Performance Goals**: At least 5x on the fixed two-test guardian benchmark;
Fast at most 5 minutes; Focused defaults to 5 minutes and permits an explicitly
justified bounded override up to 15 minutes for a measured coherent selection;
LifecycleIntegration defaults to 10 minutes and permits the same explicitly
justified 30-minute ceiling when the complete measured class has grown;
DeepValidation at most 15 minutes; PreMerge at most 30 minutes.

**Constraints**: Public validation still runs all 26 phases in canonical order; no gameplay, state schema, issue-code, prompt, documentation-example, console, browser, or frontend behavior changes; no unbounded full-suite run.

**Scale/Scope**: The original baseline contained 6,560 discovered cases, 965
broad validation calls, 460 guardian cases, and 295 broad guardian calls across
eight partial source files. The #1551 amendment starts from 7,797 Fast cases in
29 descriptors and four measured mixed-descriptor walls of approximately
3:30, 3:55, 1:05, and 4:00 without build time.

**Source Issue(s)**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

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
implementation, one Fast control at a meaningful checkpoint, and one PreMerge
control immediately before merge. Do not repeat Fast immediately before
PreMerge or serially run all diagnostic lanes unless a focused failure requires
diagnosis. `Complete` is a temporary alias for `PreMerge`.
LifecycleIntegration and DeepValidation are conditional and explicit; this
branch ran each once because their category boundaries changed.

## Constitution Check

*GATE before research: PASS. Re-check after design: PASS.*

- **GitHub traceability**: #1505/#1526/#1547 retain accepted historical
  evidence; the current Fast project-boundary repair is tracked by open issue
  #1551 and linked from the active Spec Kit artifacts.
- **Spec Kit fit**: The implementation spans production validation orchestration, many guardian test files, traits/source guards, scripts, documentation, and multiple sessions.
- **Player-facing integrity**: No console, browser, copy, or player interaction changes.
- **Contract/state authority**: Validation rule bodies, issue codes, canonical schemas, state normalization, and GM-authored contracts remain unchanged. Mortal/afterlife prompts, examples, manifests, and contract matrices therefore need no update.
- **Test-first path**: Selection API compilation/fail-closed/equivalence/order tests precede production edits; source guards precede guardian migration and lane categorization.
- **Verification evidence**: Focused tests, project/source guards, the retained
  Fast checkpoint and relevant historical diagnostic controls, one final
  PreMerge summary, Serena health, and final diff checks are required.
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
├── QteDeterministicLogicTests.cs
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
├── QteSceneServiceTests.cs
├── MortalWoundRecoveryTests.cs
├── MortalWoundTreatmentCapabilityAuthorityTests.cs
├── GmWorkerLiveSmokeTests.cs
├── LocalWebUiSmokeTests.cs
├── WebUi/BrowserMortalWorldGenerationFencingTests.cs
├── WebUi/BrowserStorageTransportParityTests.cs
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
5. Implement `scripts/test-csharp.ps1` with explicit project routing,
   lane-specific hard caps including the amended bounded PreMerge deadline,
   timestamp/PID/GUID result
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
7. Run exactly one PreMerge control below its single 30-minute deadline,
   retaining JSON/TRX/log evidence and at least 4,240 results, including
   completed ProcessIntegration and E2E phases, the exact ten lifecycle
   sentinels, and the exact ten spiritual-conflict sentinels. The full
   358-case spiritual-conflict matrix remains in RegressionIntegration.
8. Re-index Serena, confirm green health, no owned child processes,
   `git diff --check`, Spec Kit consistency, and review findings.

Final verification does not serially execute all diagnostic lanes. A failing
bounded control is narrowed with only the smallest relevant focused or
diagnostic selection.

## Phase 5: Suite-Growth PreMerge Capacity Correction

This section records the accepted historical #1526 decision. Phase 6 changes
only its now-obsolete final-gate deadline after new measured suite growth.

1. Retain the #1526 capacity-red PreMerge and compare its descriptor TRX
   timings with isolated and browser-only concurrent focused controls.
2. Reject the Fast-drain-only experiment after its exact run still exceeds the
   deadline, then trace the repeated setup on the remaining critical path.
3. Add RED/GREEN executable guards for retained PreMerge class costs and for
   one loaded browser-audit template per selected save in each test-host fixture
   with isolated per-row clones.
   Preserve filters, cases, assertions, phase order, the four-host ceiling, and
   the two-Fast ceiling.
4. Run the four ExplorerWeb shards as the first resource-isolation wave because
   paired evidence shows severe Fast-overlap contention. Drain that wave before
   starting the remaining parallel descriptors; do not change either cap,
   filter, or exclusive phase boundary.
5. Benchmark all 166 browser-audit rows, verify template immutability at fixture
   disposal, measure the two generated shards together, and replay the updated
   plan before the final control.
6. Reuse one empty ExplorerWeb directory skeleton per test host and immutable,
   hashed seed profiles for repeated deterministic theory setup; preserve a
   distinct writable root for every row.
7. Verify the focused guards, one PlanOnly contract, one meaningful Fast
   checkpoint, and one exact PreMerge control under the explicitly approved
   20-minute deadline. No other default lane timeout, concurrency cap, filter,
   phase, case, or assertion changes. A later measured coherent Focused
   selection or complete LifecycleIntegration run may opt into its separately
   bounded override (15 minutes for Focused, 30 minutes for LifecycleIntegration)
   without changing the five- or ten-minute default or any final-gate deadline.

## Phase 6: Measured PreMerge Deadline Correction

1. Retain the #1547 capacity-red run: `6,608/6,608` available core results
   green with zero duplicates and complete cleanup, followed by a global
   timeout before the exclusive process/E2E tail completed.
2. Retain the isolated ProcessIntegration result (`523/523` in `3:32`) and the
   prior isolated E2E timing to prove that the approximately 17-minute core plus
   exclusive tail cannot fit the 20-minute deadline.
3. Add a RED/GREEN executable runner guard for a 30-minute PreMerge default.
4. Change only the PreMerge/Complete lane-wide deadline to 30 minutes. Preserve
   every other lane timeout, concurrency ceiling, filter, case, assertion,
   phase boundary, phase order, retained cost, and scheduling wave.
5. Synchronize `docs/testing.md`, the #1505 data model/research/quickstart, and
   active implementation-plan final-gate expectations.
6. Run one updated PlanOnly contract and one exact PreMerge control. Require
   completed ProcessIntegration and E2E, all official results green, zero
   duplicate IDs, timeout false, and complete owned-tree cleanup.

## Phase 7: Fast Physical-Boundary Repair

1. Extend `FastTestBoundaryTests.ReviewedHeavySources` into an exact relative
   path manifest and add the #1551 canonical, process, and host sources before
   moving them. Extend `IntegrationTestBoundaryTests` with their required
   `RegressionIntegration`, `ProcessIntegration`, or `E2E` categories. Run the
   two boundary tests first and retain the expected RED result while the files
   are still in Fast.
2. Move the mandatory canonical group into Integration with
   `RegressionIntegration`: `MortalWoundRecoveryTests.cs`,
   `MortalWoundTreatmentCapabilityAuthorityTests.cs`,
   `WebUi/BrowserMortalWorldGenerationFencingTests.cs`, and
   `WebUi/BrowserStorageTransportParityTests.cs`.
3. Split `QteSceneServiceTests.cs` at its semantic boundary. Keep all
   deterministic key normalization and grading/effective-requirement tests in
   fixture-free `QteDeterministicLogicTests.cs` under Fast. Move the canonical
   runtime, persistence, rollback, console, and save/archive tests plus their
   fixture helpers to Integration as `QteSceneServiceTests.cs` with
   `RegressionIntegration`. Preserve every theory row and assertion exactly
   once.
4. Move `GmWorkerLiveSmokeTests.cs` into Integration with
   `ProcessIntegration`, because it launches a real PowerShell worker. Move
   `LocalWebUiSmokeTests.cs` into Integration with `E2E`, because it exercises
   an in-process HTTP host and complete browser API flow without launching a
   child process.
5. Run `Focused` Integration selections for each moved category group and a
   focused Fast selection for `QteDeterministicLogicTests`. Replay Fast
   `PlanOnly` and require unique, non-overlapping membership.
6. Run one bounded Fast checkpoint. If wall time is above four minutes or does
   not retain at least one minute of hard-limit headroom, continue through the
   already-measured ranking and move only sources that satisfy the same
   semantic rule. The reviewed second group is:
   `ShiningCoreActionResolutionValidationTests.cs`,
   `GuardianCorrectionServiceTests.cs`,
   `ShiningBlessingEffectStateTests.cs`,
   `AfterlifeNotificationStateTests.cs`, `DarenQteShowcaseTests.cs`,
   `ShiningTradeRequestStateTests.cs`, `BrowserLocalWriteCoordinatorTests.cs`,
   `TrainingServiceTests.cs`, `NpcTradeServiceRequestFlowTests.cs`,
   `MortalWoundOpportunityAdapterTests.cs`,
   `ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs`, and the measured
   browser parity sources for relic forge, resident interactions, archive,
   incarnation gates, ink-feather Fate, NPC social, and trade. Classify these
   as `RegressionIntegration`; any measured real-worker source instead uses
   `ProcessIntegration`. Update both exact manifests in the same group.
7. Repeat focused category verification and Fast PlanOnly after any second
   group, then run two representative Fast controls. Both must finish below the
   unchanged five-minute hard limit; approximately three minutes is the
   preferred operating target. Require zero duplicate test IDs and complete
   owned-process cleanup.
8. Update `docs/testing.md`, `research.md`, `data-model.md`, and `quickstart.md`
   with the final source/category manifest and retained run IDs. This is an
   internal test-scheduling correction only, so Mortal World and afterlife GM
   prompts, examples, manifests, client commands, and runtime contract docs do
   not change.

## Complexity Tracking

No constitution violations require justification. The production enum/overload
is internal, uses the existing phase methods, and adds no new persistence or
external service.
