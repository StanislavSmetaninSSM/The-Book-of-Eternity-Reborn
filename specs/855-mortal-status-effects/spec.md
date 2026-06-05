# Feature Specification: Mortal Status Effect Fallback

**Feature Branch**: `fix/855-mortal-status-effects`
**Created**: 2026-06-05
**Status**: Implemented and locally verified
**Source Issue**: [#855 Mortal status conditions can hide effect details when structured effects are missing](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/855)
**Related Audit**: [#857 Enforce player-facing summary/detail authority links](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/857)

## User Stories & Testing

### User Story 1 - Inspect visible mortal conditions when structured effects are absent (Priority: P1)

A player in the Mortal World sees current condition text and active condition strings in HUD/status. When they run `/эффекты` or `/effects`, the command must still give them a readable place to inspect those visible conditions even when `game_state/player/effects.json` is missing or empty.

**Independent Test**: Seed Mortal World state with `game_state/core/player_status.json` containing `currentCondition`, `currentConditionDescription`, and `activeConditions`, omit `game_state/player/effects.json`, then run `/эффекты`; assert the rendered command result includes the current condition, condition description, active condition strings, and a clear fallback note.

**Acceptance Scenarios**:

1. **Given** Mortal World status contains `Лёгкое недомогание`, headache penalty, and magical resonance, **When** the player opens `/эффекты`, **Then** those conditions are visible in the effects surface instead of an empty “data not created” message.
2. **Given** `currentConditionDescription` is present, **When** `/эффекты` renders fallback status, **Then** the description is shown with the current condition.
3. **Given** structured `effects.json` exists with active effects/wounds/temporary conditions, **When** `/эффекты` renders, **Then** the existing structured summary/raw detail remains available and the fallback does not hide structured authority.

---

### User Story 2 - Preserve player-facing wording (Priority: P2)

The fallback explanation should be understandable to a player and must not expose file paths, DTO/API terminology, or debug framing in the default command result.

**Independent Test**: Assert fallback copy uses in-world/plain Russian wording and does not include `game_state/player/effects.json`, `DTO`, `API`, or `debug` in default text.

---

## Requirements

### Functional Requirements

- **FR-001**: `/эффекты` and `/effects` MUST render visible Mortal World status conditions from aggregated/player status when structured effect data is missing or has no visible rows.
- **FR-002**: The fallback MUST include `currentCondition` when it is non-empty and not an uninformative healthy/default value.
- **FR-003**: The fallback MUST include `currentConditionDescription` when present in `game_state/core/player_status.json` or another existing player status source already loaded by the command path.
- **FR-004**: The fallback MUST include each non-empty `activeConditions[]` entry from player status.
- **FR-005**: If structured `effects.json` exists and has active effects, wounds, or temporary conditions, the command MUST keep the structured summary/raw detail behavior and may additionally include status fallback only when it adds non-duplicated visible status context.
- **FR-006**: Default player-facing output MUST avoid raw file paths, API/DTO/debug language, and developer acceptance-criteria phrasing.
- **FR-007**: Add focused regression coverage for the missing/empty structured effects case from #855.

### Contract / Documentation Scope

- This feature is intended as a client-owned player-facing fallback for an already-accepted `player_status.json` shape. It should not add or rename GM-authored fields, runtime contracts, pending/control files, or validation issue codes.
- If implementation instead changes validation rules, canonical state, or GM-authored output requirements, update GM-facing docs/examples/manifests in the same PR per AGENTS.md and the constitution.

## Out of Scope

- Full #857 summary/detail authority audit.
- Inventory document readability (#858), mechanical item bonus authority (#859), and quest reward reference authority (#860).
- Browser visual redesign or new React gameplay logic.
- New effect schema or normalizer materialization rules unless required by tests.

## Success Criteria

- `/эффекты` is no longer empty for the #855 Mortal World status fixture when structured effects are missing.
- Regression test proves the old behavior fails before the fix and passes after.
- Focused ExplorerMode command tests pass with real test execution (`-p:IsTestProject=true` on this Windows/.NET 10 host).
- `git diff --check origin/main...HEAD` passes.
