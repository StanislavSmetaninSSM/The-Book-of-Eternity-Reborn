# Feature Specification: Browser QTE Interactive Mini-Games

**Feature Branch**: `work/918-browser-qte-parity`
**Created**: 2026-06-10
**Status**: Draft for autonomous implementation
**Source Issues**: [#918 Browser QTE parity](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918), parent browser epic [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680), QTE v2 parent [#911](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911)

## User Scenarios & Testing

### Scenario 1 - Browser player resolves an ordinary QTE through an in-client mini-game (Priority: P1)

A player accepts a pending QTE in the Browser Client and sees an interactive mini-game for the current action instead of a manual `success` / `partial` / `fail` grade selector. The player completes the mini-game, the browser submits the resulting grade to the existing C# QTE action endpoint, and the same resolution/result/completion surfaces appear as in the current shared QTE flow.

**Independent Test**: Render `QteScenePanel` with active actions for TimingBar, PromptChain, BalanceMeter, ChargeRelease, MashInput, PatternMemory, RhythmPulse, PrecisionChoice, StealthNoise, and LockPinSet. Verify the default player-facing UI does not show a manual grade selector/quick grade buttons for supported interactive checks and does show a check-specific play surface plus a submit path that posts `success`, `partial`, or `fail`.

**Acceptance Scenarios**:

1. **Given** an active browser QTE action with a supported check type and config, **When** the panel renders, **Then** the browser shows a check-specific mini-game with instructions and hides the manual grade dropdown in default player mode.
2. **Given** the player completes the mini-game with a successful local outcome, **When** the browser submits the action, **Then** the request to `/api/qte/action` carries the action id and grade `success` while C# remains responsible for routing, state writes, and completion.
3. **Given** the same action resolves to partial or fail based on mini-game input/time, **When** the browser submits, **Then** the request carries `partial` or `fail` using the existing grade vocabulary.

---

### Scenario 2 - BranchChoice and unsupported checks remain explicit and safe (Priority: P1)

BranchChoice remains a static player choice because its authored grade is selected by the branch, while unknown/future check types do not expose a broken manual-grade cheat path.

**Independent Test**: Render BranchChoice and an unknown check type. BranchChoice must provide a direct action button without a grade selector. Unknown/future types must show a player-facing unsupported-state message and not submit until the player explicitly uses an advanced/debug path, if such a path exists.

**Acceptance Scenarios**:

1. **Given** a BranchChoice action, **When** the panel renders, **Then** it shows a direct choice button and does not ask the player to pick a grade.
2. **Given** an unsupported check type, **When** the panel renders in the default player UI, **Then** it explains that this QTE type needs a client update and does not show raw DTO/config fields or slash-command diagnostics.

---

### Scenario 3 - Browser receives enough check configuration without changing the GM QTE contract (Priority: P2)

The C# browser DTO exposes only the read-only check configuration needed by React to present mini-games. The browser does not introduce new save files, pending/control files, GM-authored fields, or runtime scoring contracts.

**Independent Test**: C# API/fixture tests verify `QteWebActionDto` includes normalized check metadata/config for every currently supported QTE type and keeps existing offer/accept/action endpoint semantics. Frontend contract fixtures typecheck against the updated DTO.

**Acceptance Scenarios**:

1. **Given** existing v1/v2 QTE offers, **When** Browser Client state is built, **Then** each action contains `checkType`, difficulty/characteristic, `requiresSubmittedGrade`, and a typed/minimally normalized config payload sufficient for the matching mini-game.
2. **Given** the browser submits a computed grade, **When** C# resolves the action, **Then** routing/history/completion still use `QteSceneService.ResolveActiveActionAsync`; React never edits game-state files directly.

---

### Scenario 4 - Responsive and accessible browser QTE controls (Priority: P2)

The mini-games are usable with keyboard and pointer input and do not rely on Russian/English keyboard layout quirks fixed by #920.

**Independent Test**: Frontend tests cover keyboard-triggered and pointer-triggered paths for representative mini-games and assert visible instructions/focusable controls. Static/player-facing tests assert no default player UI leaks raw grade/debug language for supported checks.

**Acceptance Scenarios**:

1. **Given** desktop or mobile-width layout, **When** a QTE mini-game renders, **Then** controls fit in the panel without horizontal overflow and with readable Russian labels.
2. **Given** keyboard input, **When** a player uses the displayed key/control, **Then** QTE input normalizes through the existing browser QTE key-layout helper where relevant.
3. **Given** reduced-motion or timing-sensitive checks, **When** the player cannot use precise input, **Then** the UI exposes clear instructions and a deterministic fail/partial path rather than silently hanging.

## Requirements

### Functional Requirements

- **FR-001**: Browser default QTE UI MUST replace the manual `success`/`partial`/`fail` selector for supported interactive checks with player-facing mini-game controls.
- **FR-002**: Browser mini-games MUST cover existing v1 types `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, and static `BranchChoice` handling.
- **FR-003**: Browser mini-games MUST cover v2 types `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, and `LockPinSet`.
- **FR-004**: Browser and console resolution MUST use the same grade vocabulary: `success`, `partial`, `fail`.
- **FR-005**: React MAY compute the local mini-game grade, but C# MUST remain the write authority for action resolution, routing, history, completion, and state mutation via the existing browser QTE action endpoint.
- **FR-006**: The browser DTO MUST expose enough read-only check configuration for each supported mini-game without adding new GM-authored QTE fields.
- **FR-007**: Unknown/future check types MUST show an explicit player-facing unsupported-state message and MUST NOT show the old manual grade selector in the default player UI.
- **FR-008**: Advanced/debug affordances, if retained for diagnostics, MUST be behind explicit advanced mode and not visible in the normal player-facing panel.
- **FR-009**: Browser QTE controls MUST support keyboard and pointer usage and preserve #920 RU/EN layout-independent key guidance where relevant.
- **FR-010**: Browser QTE implementation MUST NOT grant rewards, scoring/ranks, practice-mode progress, achievements, Ink Feathers, or Daren-showcase state; those are owned by #924, #925, and #919.
- **FR-011**: Documentation/source guards MUST be updated where browser QTE behavior, API projection, or player-facing QTE guidance changes.

### Key Entities

- **QteWebStateDto**: Browser state projection for offer, active scene, resolution, completion, and notifications.
- **QteWebActionDto**: Browser action projection. It will include check identity, difficulty, characteristic, whether grade submission is required, and normalized check config for UI-only mini-game presentation.
- **Browser QTE Mini-Game**: React component or helper that renders one check type, collects local player input, calculates `success`/`partial`/`fail`, and submits through the existing browser API client.
- **QTE Check Config Projection**: Read-only subset of `QteSceneService.QteCheck.Config` normalized enough for frontend type safety and tests.

## Out of Scope

- Standard score metrics, ending ranks, or scenario score history (#924).
- Daren training/showcase mini-adventure (#919).
- Standalone QTE Practice Mode (#925).
- New GM-authored QTE fields, validation contract changes, save format changes, or new afterlife/Mortal runtime contracts.
- Replacing console QTE implementation or changing console input behavior.
- Full visual redesign of Browser Client outside the QTE panel (#930).

## Success Criteria

- Browser QTE default UI no longer asks ordinary players to manually choose `success`, `partial`, or `fail` for supported interactive QTE checks.
- All currently supported QTE check types have either a browser mini-game implementation or an explicit unsupported-state test path.
- C# QTE action resolution remains the mutation authority, and React remains presentation/request-state code.
- Frontend verify passes and focused C# browser/QTE/docs tests pass with non-zero test counts.
- Issue #918 can be closed without changing #924/#925/#919 scope.
