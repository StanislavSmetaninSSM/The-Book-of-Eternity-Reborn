# Feature Specification: Console QTE Live Rendering

**Feature Branch**: `work/944-console-qte-live-render`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Input**: GitHub issue [#944](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/944)

## Source Issues & Scope

- **Source GitHub issue(s)**: [#944 Console QTE timed mini-games flicker due to full-screen redraw loop](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/944)
- **Issue type**: Bug / player-facing console UX hardening.
- **Spec Kit justification**: The defect affects player-facing console QTE UX across multiple timed mini-game loops and needs durable handoff, source guards, and verification evidence. It meets the constitution policy for player-facing UX and multi-session implementation. It does not change QTE scoring, validation, GM-authored contracts, save state, browser behavior, or afterlife contracts.
- **Contract scope**: Console-client rendering only. Automated source/test coverage. No GM-facing prompts, examples, validation schema, runtime-state, browser/frontend, Mortal/afterlife contract, or save-format change is intended.
- **Out of scope**: QTE scoring/difficulty/input rules/rewards, QTE practice catalog content, Browser QTE rendering, non-QTE console screens, GM prompt/example changes, afterlife/Chaos Sea/Shining Abode contracts, and broad Spectre.Console UI rewrites.

## User Scenarios & Testing

### User Story 1 - Timed QTEs Update Without Full-Screen Flicker (Priority: P1)

A console player runs a timed QTE mini-game and sees the same QTE frame remain stable while the timer, progress, marker, or current state updates in place.

**Why this priority**: This is the user-visible defect in #944. The current `AnsiConsole.Clear()` per tick causes visible blinking on Windows consoles.

**Independent Test**: A source/regression guard proves high-frequency mini-game rendering no longer clears the terminal every tick, and a focused console/QTE test exercises the renderer update path for representative timed mini-games.

**Acceptance Scenarios**:

1. **Given** a timed QTE loop such as TimingBar, **When** the marker advances, **Then** the QTE frame is updated in place without calling `AnsiConsole.Clear()` for each tick.
2. **Given** MashInput or RhythmPulse updates every ~20ms, **When** the timer/progress changes, **Then** only the live renderable target is refreshed rather than clearing and repainting the entire console.
3. **Given** the player presses Esc during a live-rendered QTE, **When** the loop handles cancellation, **Then** the result remains `Fail` and the live rendering session exits cleanly.

---

### User Story 2 - Scene Transitions Still May Clear Deliberately (Priority: P2)

A console player can still see clean transitions into offers, menus, preludes, and results where a one-time clear is intentional; only high-frequency animation ticks are prohibited from clearing.

**Why this priority**: The fix must address the root cause without breaking existing scene/menu behavior or producing stacked panels in non-animated flows.

**Independent Test**: Source guards distinguish the mini-game live-rendering path from deliberate one-time clears outside animation ticks.

**Acceptance Scenarios**:

1. **Given** a QTE offer/prelude/result screen, **When** it is shown once, **Then** deliberate clears remain allowed where already used for scene transitions.
2. **Given** a high-frequency mini-game loop, **When** it renders repeated frames, **Then** the repeated path does not call `AnsiConsole.Clear()` directly or indirectly through a helper that clears.

---

### User Story 3 - Regression Evidence Covers Representative Mini-Games (Priority: P2)

Maintainers need evidence that the fix covers the mini-game families named by #944 and cannot silently regress back to full-screen redraws.

**Why this priority**: Flicker is visual and easy to reintroduce when adding new QTE types or refactoring loops.

**Independent Test**: Source guards and focused tests cover TimingBar, MashInput, RhythmPulse, and at least one newer type such as LockPinSet or StealthNoise.

**Acceptance Scenarios**:

1. **Given** the test suite runs, **When** QTE source guards inspect `QteSceneService.cs`, **Then** `RenderMiniGamePanel` or its replacement does not call `AnsiConsole.Clear()`.
2. **Given** the test suite runs, **When** it scans high-frequency loop sections, **Then** TimingBar, MashInput, RhythmPulse, and a newer type use the live/update rendering path rather than clear-per-tick rendering.
3. **Given** implementation evidence is recorded, **When** reviewers inspect this Spec Kit feature, **Then** the local verification commands and any manual/automated visual-smoke limitations are explicit.

## Edge Cases

- Output may be redirected or not support interactive live rendering; the fallback must not clear on every tick. It may use a low-frequency/simple write fallback only if it avoids full-screen flicker and does not spam unbounded output in normal interactive consoles.
- Dynamic QTE body strings contain Spectre markup generated by trusted helpers, while title/instruction text and layout support notes must remain escaped where user/GM authored text could enter.
- Live rendering must clean up correctly after success, partial, fail, timeout, or Esc cancellation so subsequent result panels and menus still render normally.
- Existing QTE input normalization (#920) must keep working; this issue must not widen keyboard-layout behavior beyond QTE surfaces.
- If true human visual inspection cannot be performed in the autonomous environment, automated source guards and renderer tests are mandatory, and the final report must state that live manual console observation remains a residual risk.

## Requirements

### Functional Requirements

- **FR-001**: High-frequency console QTE mini-game render ticks MUST NOT call `AnsiConsole.Clear()`.
- **FR-002**: The QTE mini-game rendering path MUST update a stable renderable target in place, preferably through Spectre.Console live rendering or a small renderer abstraction that can be tested.
- **FR-003**: TimingBar, MashInput, RhythmPulse, and at least one newer timed type such as LockPinSet or StealthNoise MUST be covered by the no-clear regression guard.
- **FR-004**: Scene transitions, offer/prelude/result screens, menus, and blocking selection prompts MAY continue to clear once when appropriate; the prohibition applies to repeated animation/timer ticks.
- **FR-005**: The fix MUST preserve existing QTE grade outcomes, input controls, timeout/cancel behavior, scoring summaries, and practice/test mode semantics.
- **FR-006**: The fix MUST keep TypeScript/browser/frontend, validation schema, GM-authored QTE contracts, examples, and afterlife contracts unchanged unless an implementation reality conflict is discovered and documented before changes.
- **FR-007**: The implementation MUST add automated regression/source-guard coverage that fails on the current clear-per-tick implementation before production code changes.
- **FR-008**: Verification evidence MUST include focused QTE/source-guard tests, client build, `git diff --check`, and an added-line security/static scan.

### Key Entities

- **QTE Mini-Game Renderer**: The console rendering path or abstraction responsible for repeated QTE frame updates.
- **Static QTE Shell**: Title, instructions, frame/border, and layout support note that should remain visually stable during a mini-game.
- **Dynamic QTE Body**: Timer, progress bar, marker position, press counters, rhythm state, pin/noise/balance state, and current prompt details.
- **Scene Transition Screen**: One-time QTE offer/prelude/result/menu rendering where clear-once behavior remains allowed.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A focused RED test/source guard fails on `origin/main` because `RenderMiniGamePanel` currently calls `AnsiConsole.Clear()` or high-frequency loops use the clearing helper.
- **SC-002**: After implementation, the focused QTE/source-guard command passes with zero failures and non-zero test count.
- **SC-003**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passes with zero errors.
- **SC-004**: `git diff --check origin/main...HEAD` passes.
- **SC-005**: Added-line static scan over `origin/main...HEAD` reports no hardcoded secrets, shell/eval/pickle/SQL injection patterns, or accidental run artifacts.

## Verification Plan

- **Baseline before Spec Kit edits**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|GameEngineSourceGuardTests" --logger "console;verbosity=minimal"` — passed 288/288 on 2026-06-11 before creating this feature directory.
- **Focused implementation tests**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|QteSceneRenderingSourceGuardTests|GameEngineSourceGuardTests" --logger "console;verbosity=minimal"`
- **Broader QTE regression tests**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"`
- **Build**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
- **Spec Kit prerequisite check**: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from this branch after tasks exist.
- **Hygiene**: `git diff --check origin/main...HEAD` plus added-line static security scan excluding no files unless a docs-only false positive is proven.
- **Manual/visual note**: If an unattended run cannot perform human observation, the PR and final report must state the visual-smoke limitation and rely on the renderer/source-guard evidence.

## Assumptions

- #944 is the active tracked task; Hermes owns PR creation, issue closure, and final acceptance.
- Current repo `origin/main` has the clear-per-tick defect in `BookOfEternityClient/Services/QteSceneService.cs`.
- The correct implementation can stay within C# console service/tests and Spec Kit artifacts; no docs/prompts/examples are required because no GM-authored QTE contract changes.
- A small renderer abstraction is acceptable if it reduces coupling and makes no-clear behavior testable.
