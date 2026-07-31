# Implementation Plan: Test Suite Performance and Verification Lanes

**Branch**: `work/1505-test-suite-performance` | **Date**: 2026-07-31 | **Spec**: [spec.md](spec.md)

**Input**: Approved feature specification from `specs/1505-test-suite-performance/spec.md`.

## Summary

Keep the public production validator behaviorally unchanged while adding an
internal, non-empty flags-based selection of the existing 26 ordered validation
phases. Prove equivalence and fail-closed selection behavior first, then migrate
295 broad guardian-suite calls to reviewed test-side profiles. Add enforceable
slow-test traits and a bounded PowerShell lane runner that retains TRX/log
evidence and owns only the process tree it starts.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7/Windows PowerShell-compatible lane script.

**Primary Dependencies**: Existing `ValidationService`, `FileSystemManager`, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, `dotnet test`.

**Storage**: Existing file-backed JSON fixtures plus ignored `TestResults/test-lanes/` logs and TRX output.

**Testing**: xUnit focused filters, source guards, bounded benchmark runs, and one final bounded complete C# suite.

**Target Platform**: Local Windows development machine; implementation remains portable .NET code.

**Project Type**: Local console/browser game-client repository with one runtime project and one C# test project.

**Performance Goals**: At least 5x on the fixed two-test guardian benchmark; fast lane at most 5 minutes; complete C# suite at most 15 minutes on the baseline machine.

**Constraints**: Public validation still runs all 26 phases in canonical order; no gameplay, state schema, issue-code, prompt, documentation-example, console, browser, or frontend behavior changes; no unbounded full-suite run.

**Scale/Scope**: 6,560 discovered cases, 965 broad validation calls, 460 guardian cases, and 295 broad guardian calls across eight partial source files.

**Source Issue(s)**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

**Contract Scope**: Internal validation orchestration and test infrastructure only.

**Verification Commands**:

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.sln --no-restore
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidationPhaseSelectionTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~TestLaneSourceGuardTests"
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~GuardianProjectValidation_OffensiveIntrigueAgainstTrustedTarget" -TimeoutMinutes 2
.\scripts\test-csharp.ps1 -Lane Fast
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
.\scripts\test-csharp.ps1 -Lane Complete -TimeoutMinutes 20
```

## Constitution Check

*GATE before research: PASS. Re-check after design: PASS.*

- **GitHub traceability**: Issue #1505 is open, in progress, and linked from all active Spec Kit artifacts.
- **Spec Kit fit**: The implementation spans production validation orchestration, many guardian test files, traits/source guards, scripts, documentation, and multiple sessions.
- **Player-facing integrity**: No console, browser, copy, or player interaction changes.
- **Contract/state authority**: Validation rule bodies, issue codes, canonical schemas, state normalization, and GM-authored contracts remain unchanged. Mortal/afterlife prompts, examples, manifests, and contract matrices therefore need no update.
- **Test-first path**: Selection API compilation/fail-closed/equivalence/order tests precede production edits; source guards precede guardian migration and lane categorization.
- **Verification evidence**: Focused tests, bounded benchmark, lane runs, build, diff checks, and final bounded complete TRX are required.
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
├── GuardianValidationProfiles.cs
├── GuardianSystemRegressionTests*.cs
├── TestLaneSourceGuardTests.cs
└── slow/full-validation and process/E2E test classes receiving traits

scripts/
└── test-csharp.ps1

docs/
└── testing.md
```

**Structure Decision**: Keep phase selection in the runtime validation namespace
because the public facade and internal test overload share the same dispatcher.
Keep profiles, source guards, and classification entirely in the test project.
Use one repository script and one testing guide as the stable local interface.

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
6. If the complete/fast budget is still missed, extract safe domain classes
   from the partial class. Do not introduce shared mutable fixtures merely to
   obtain parallelism.

## Phase 3: Verification Lanes

1. Add class- or method-level `FullValidation`, `ProcessIntegration`, and `E2E`
   traits to the inventoried slow groups.
2. Add a source guard for direct full-validation files and known process/E2E
   entry points.
3. Implement `scripts/test-csharp.ps1` with lane-to-filter mapping, configurable
   timeout, timestamped TRX/log output, and `Process.Kill(true)` only for the
   started `dotnet` process tree on timeout.
4. Document fast, focused, full-validation, process-integration, E2E, and
   complete commands plus expected durations in `docs/testing.md`.
5. Test filter construction and source-guard behavior without launching the
   entire suite.

## Phase 4: Performance and Integration Verification

1. Run the fixed two-test guardian benchmark at least three times after build;
   compare median runner time with the approximately 20-second baseline.
2. Run every guardian domain selection and the complete guardian class bounded.
3. Run fast and slow lanes separately, retaining TRX/log evidence.
4. Run one final complete suite with a 20-minute external bound.
5. Confirm discovered-case delta, no owned child processes, Release build,
   `git diff --check`, Spec Kit consistency, and review findings.

## Complexity Tracking

No constitution violations require justification. The production enum/overload
is internal, uses the existing phase methods, and adds no new persistence or
external service.
