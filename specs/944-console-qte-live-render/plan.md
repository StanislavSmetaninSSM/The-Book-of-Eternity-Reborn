# Implementation Plan: Console QTE Live Rendering

**Branch**: `work/944-console-qte-live-render` | **Date**: 2026-06-11 | **Spec**: `specs/944-console-qte-live-render/spec.md`

**Input**: Feature specification from `specs/944-console-qte-live-render/spec.md`; GitHub issue [#944](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/944)

## Summary

Replace the console QTE timed mini-game clear-and-redraw loop with a stable live/update rendering path. Keep one-time scene transition clears intact, preserve all QTE gameplay/scoring/input behavior, and add source/regression coverage that fails if high-frequency mini-game ticks call `AnsiConsole.Clear()` again.

## Technical Context

**Language/Version**: C# on .NET 8.

**Primary Dependencies**: Spectre.Console for console rendering; existing QTE services in `BookOfEternityClient/Services/QteSceneService.cs`.

**Storage**: No storage or save-state changes.

**Testing**: xUnit via `BookOfEternityClient.Tests`; source guards for render-loop regressions; focused QTE tests.

**Target Platform**: Local Windows console client and cross-platform .NET console execution.

**Project Type**: Local game client / console UI.

**Performance Goals**: Timed QTE render loops avoid full-screen clear/repaint at 20ms/50ms tick frequencies. Refresh rate may be throttled if needed, but not as the sole fix.

**Constraints**: Do not change QTE scoring, input rules, GM contracts, browser code, save files, afterlife contracts, or non-QTE console screens. Dynamic player/GM text entering Spectre markup must remain escaped.

**Scale/Scope**: One console service plus focused C# tests/source guards and Spec Kit artifacts.

**Source Issue(s)**: [#944](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/944)

**Contract Scope**: Console player-facing rendering only; no GM-facing/runtime-state/validation/browser contract changes.

**Verification Commands**:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|QteSceneRenderingSourceGuardTests|GameEngineSourceGuardTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

## Constitution Check

- **GitHub traceability**: Source issue #944 is linked in spec, plan, and tasks.
- **Spec Kit fit**: Required because the issue is player-facing UX hardening across several timed QTE loops with durable handoff/review needs.
- **Player-facing integrity**: Console UI remains Russian/in-world; no debug/API/agent language is added to player surfaces.
- **Contract/state authority**: No game contract, validation schema, GM prompt/example, save, runtime-state, or afterlife contract changes are planned. If implementation reveals a contract change is necessary, stop and update this plan/spec before proceeding.
- **Test-first path**: Add `QteSceneRenderingSourceGuardTests` or equivalent RED coverage before changing `QteSceneService.cs`.
- **Verification evidence**: Focused QTE/source-guard tests, broader QTE/docs/browser-contract neighborhood, client build, diff hygiene, and static scan are required.
- **Agent orchestration**: Hermes will pass this feature directory, constitution, issue, current git status, and Superpowers TDD/debugging/review requirements into Codex. Hermes owns final PR/merge/closure.

## Project Structure

### Documentation (this feature)

```text
specs/944-console-qte-live-render/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── console-qte-rendering.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/QteSceneService.cs
    Existing QTE runtime and console mini-game loops. Introduce or use a live/update renderer here unless Codex finds a cleaner existing home.

BookOfEternityClient.Tests/QteSceneRenderingSourceGuardTests.cs
    New focused source guard for no-clear high-frequency QTE rendering and representative mini-game coverage.

BookOfEternityClient.Tests/QteSceneServiceTests.cs
    Existing behavior tests; extend only if a testable renderer abstraction can be covered without brittle console timing.
```

**Structure Decision**: Keep the change local to QTE console rendering. Prefer a small private/internal renderer helper inside or near `QteSceneService` if it avoids a large service split. Create a separate test file for source guards so visual-regression constraints are explicit and cheap to run.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| None | N/A | N/A |

## Implementation Approach

1. Add a RED source guard that fails on current `RenderMiniGamePanel` calling `AnsiConsole.Clear()` and on representative high-frequency loops using a clearing helper.
2. Inspect Spectre.Console live rendering support in the installed version. Prefer `AnsiConsole.Live(...).StartAsync(...)` or equivalent for interactive consoles.
3. If direct live rendering makes the loops too tangled, introduce a minimal `QteMiniGameRenderer`/helper abstraction that owns begin/update/end and can fall back without clear-per-tick.
4. Update TimingBar, MashInput, RhythmPulse, and at least one newer timed type (prefer LockPinSet or StealthNoise if they use the same helper) to render repeated frames through the live/update path.
5. Keep one-time `AnsiConsole.Clear()` calls for QTE offers/preludes/results and non-QTE screens where they are scene transitions.
6. Rerun focused tests after each implementation step, then run the broader QTE neighborhood, build, Spec Kit prerequisites, diff check, and static scan.

## Risks and Mitigations

- **Spectre.Console Live API mismatch**: Codex must inspect the installed package/API and compile before relying on a method name.
- **Hidden gameplay regression**: Focused tests must include QTE behavior neighborhoods; implementation must not touch grade helpers or validation except as needed for rendering signatures.
- **Fallback spam**: If output is non-interactive, fallback must not print hundreds of panels in normal automated runs. Prefer controlled/no-clear behavior and document any limitation.
- **Visual evidence gap**: Autonomous cron may not provide human-visible console screenshots. Use source guards and renderer tests as required evidence and report the limitation honestly.
