# Implementation Plan: Console QTE Live Playability

**Branch**: `work/1081-qte-live-pacing` | **Date**: 2026-06-18 | **Spec**: `specs/1081-qte-live-pacing/spec.md`

**Input**: Feature specification from `specs/1081-qte-live-pacing/spec.md`; GitHub issue [#1081](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1081)

## Summary

Diagnose the reopened live console QTE usability bugs, add regression/harness evidence that exercises the actual live mini-game path, and fix TimingBar pacing, PromptChain startup timing, BalanceMeter control readability, and PatternMemory input-phase reveal leakage without changing QTE contracts or unrelated Daren/browser behavior.

## Technical Context

**Language/Version**: C# on .NET 8.

**Primary Dependencies**: Spectre.Console, existing QTE runtime in `BookOfEternityClient/Services/QteSceneService.cs`, QTE browser/config DTOs only if shared compilation requires inspection, xUnit tests in `BookOfEternityClient.Tests`.

**Storage**: No save-state or canonical game-state changes planned.

**Testing**: xUnit via `dotnet test`; focused source/regression tests; deterministic live-path harness or manual console evidence for QTE loops; optional console E2E artifacts if a suitable harness exists.

**Target Platform**: Local Windows console client; tests must remain stable under normal .NET test execution.

**Project Type**: Local game client / console UI.

**Performance Goals**: QTE live loops should remain responsive without adding unbounded sleeps or busy waits. PromptChain startup should be readable; TimingBar high difficulty should not be trivial.

**Constraints**: Do not change QTE scoring/rank contracts, GM-authored schema, browser interactive parity, Daren story/prose/reward data, save/runtime-state files, or afterlife contracts unless root-cause evidence proves the issue cannot be fixed otherwise and this Spec Kit feature is updated first.

**Scale/Scope**: One QTE service/runtime area plus focused tests/source guards/harness evidence and Spec Kit artifacts.

**Source Issue(s)**: [#1081](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1081)

**Contract Scope**: Console player-facing live QTE UX and test/harness evidence; no GM-facing/runtime-state/browser contract change intended.

**Verification Commands**:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Qte|Daren|RhythmPulse|ConsoleE2ESmokeTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|QteSceneRenderingSourceGuardTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Add any new live/harness command(s) to this list once implemented.

For Spec Kit prerequisite checks in this branch, use `SPECIFY_FEATURE_DIRECTORY=specs/1081-qte-live-pacing` because the tracked `.specify/feature.json` on current `main` still points at the previous #1092 feature. Do not merge transient active-feature pointer churn unless explicitly reconciled.

## Constitution Check

- **GitHub traceability**: Source issue #1081 is linked in spec, plan, and tasks.
- **Spec Kit fit**: Required because the issue is a reopened player-facing console UX bug across several live QTE loops, and closure needs durable live/harness evidence beyond static tests.
- **Player-facing integrity**: New or changed console copy must stay Russian/in-world and avoid debug/API/agent language.
- **Contract/state authority**: No GM prompt/example/schema, save-state, browser parity, Daren reward, or afterlife contract change is planned. If implementation reveals such a change is needed, stop and update spec/plan/tasks before proceeding.
- **Test-first path**: Add focused RED tests or live-path harness probes before production fixes for each reported bug where practical.
- **Verification evidence**: Focused QTE tests, live/harness/manual evidence for the four mini-games, build, diff hygiene, static scan, and independent review are required.
- **Agent orchestration**: Hermes passes this feature directory, constitution, issue, current git status, baseline evidence, and Superpowers TDD/debugging/review requirements into Codex. Hermes owns PR/merge/closure.

## Project Structure

### Documentation (this feature)

```text
specs/1081-qte-live-pacing/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── qte-live-playability.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.cs
    Existing console QTE live loops and helpers. Likely root-cause area for TimingBar, PromptChain, BalanceMeter, and PatternMemory.

BookOfEternityClient.Tests/QteSceneServiceTests.cs
    Existing QTE behavior tests; extend if public/internal seams already support deterministic loop assertions.

BookOfEternityClient.Tests/QteSceneRenderingSourceGuardTests.cs
    Existing source guard area from #944; extend only if useful for reveal/copy/live-path regressions. Do not rely on it as the sole closure proof.

BookOfEternityClient.Tests/*Qte* or new focused test file
    Add live-playability regression tests or deterministic harness probes for #1081.

TestResults/ or repo-approved artifact path
    Preserve generated live/harness artifacts only if they are lightweight and intentionally part of closure evidence. Do not commit bulky transient logs unless the issue/spec requires them.
```

**Structure Decision**: Keep runtime changes close to the existing QTE service unless root-cause investigation proves a small internal test seam/harness helper is needed. Prefer deterministic tests over broad infrastructure. If a test-only helper is necessary, keep it internal and production-safe rather than adding debug UI.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| None expected | N/A | N/A |

## Implementation Approach

1. Reproduce/classify the reopened symptoms from current `origin/main`: inspect live loops, existing #944 renderer changes, issue comments, and current tests.
2. Identify root causes separately for TimingBar, PromptChain, BalanceMeter, and PatternMemory. Do not patch until the failing behavior is captured or the live-path probe shows the boundary.
3. Add focused RED tests/source guards/harness probes:
   - TimingBar effective speed/difficulty monotonicity or live marker progression.
   - PromptChain startup/readability window and non-immediate failure.
   - BalanceMeter player-facing control/step readability and movement consistency.
   - PatternMemory input phase without full reveal sequence.
4. Implement minimal fixes in the existing live QTE path, preserving grade semantics, input normalization, QTE contracts, and browser/Daren boundaries.
5. Produce live/harness/manual evidence for each reported mini-game. If true human visual smoke is not possible, create deterministic artifacts from the same live path and state the limitation.
6. Reconcile Spec Kit tasks with evidence, run focused and broader QTE gates, build, diff hygiene, static scan, and independent review before PR/merge.

## Risks and Mitigations

- **Prior false confidence from static guards**: Require live/harness/runtime evidence, not source guards alone.
- **Brittle timer tests**: Prefer deterministic clocks/config/state probes or a test seam over wall-clock sleeps. If wall-clock is unavoidable, use generous bounds and short timeouts.
- **Console harness fragility on Windows**: Prefer in-app deterministic script/harness evidence when ConPTY automation is too brittle; preserve artifacts and state residual manual risks honestly.
- **Gameplay regression**: Keep fixes local to live mini-game UX and rerun QTE/Daren/browser-contract neighborhoods.
- **Contract drift**: If fixes require GM-authored QTE schema/copy/example changes, update this plan/spec and relevant GM docs/tests before implementation continues.
