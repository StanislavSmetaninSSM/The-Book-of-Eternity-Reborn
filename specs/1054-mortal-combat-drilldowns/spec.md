# Feature Specification: Mortal Combat Read-Only Detail Drill-Downs

**Feature Branch**: `1054-mortal-combat-drilldowns`

**Created**: 2026-06-16

**Status**: Draft for autonomous implementation

**Input**: GitHub issue #1054 — "[Task] Add mortal combat read-only detail drill-downs for /бой"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1054 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1054
- **Issue type**: task / audit follow-up / player-facing console-browser parity
- **Spec Kit justification**: #1054 is multi-file player-facing Mortal World command UX work spawned by #948. It touches command-result rendering, console/browser parity, regression/source-guard tests, and possibly audit documentation. Durable requirements are needed so Codex and later Hermes reconciliation do not broaden the slice into all mortal read-only commands.
- **Contract scope**: player-facing, console, browser, docs/audit, tests. No GM-facing prompt, runtime-state schema, validation, normalizer, pending/control, afterlife, Chaos Sea, or Shining Abode contract change is intended.
- **Out of scope**: afterlife spiritual combat commands and afterlife combat files; broad Browser Client redesign; resolving #1055 `/world_news`, #1056 `/interactions`, or #1057 reference-command detail actions; changing GM-authored Mortal combat response schema unless an unavoidable gap is found and documented with a follow-up.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspect a mortal enemy without raw JSON (Priority: P1)

A player who opens `/бой` can choose or invoke a detail path for one visible enemy and read its name, threat/status, health or condition, intent/action, effects, and notes in player-facing Russian/in-world copy without reading raw JSON sidecars.

**Why this priority**: Enemy inspection is the most important combat drill-down because it directly affects player decisions in Mortal World fights.

**Independent Test**: Seed `game_state/combat/enemies.json` with at least one rich enemy and verify the shared command-result surface exposes a player-facing enemy detail/table/action path, no raw-only fallback, and no debug/API terms in ordinary blocks.

**Acceptance Scenarios**:

1. **Given** canonical mortal enemy state exists, **When** the player opens `/combat` or `/бой`, **Then** the overview still appears and exposes a player-facing way to inspect the enemy.
2. **Given** the player selects or invokes the enemy detail path, **When** the detail renders, **Then** it shows the enemy's playable attributes in Russian/in-world copy and does not require raw JSON.

---

### User Story 2 - Inspect a mortal ally without raw JSON (Priority: P2)

A player can inspect one ally/companion in the same Mortal World combat scene and understand the ally's condition, role, current action, effects, and notes through player-facing output.

**Why this priority**: Ally state is part of the same command and must not remain raw-only if enemy detail parity is introduced.

**Independent Test**: Seed `game_state/combat/allies.json` with at least one rich ally and verify the command-result surface exposes a player-facing ally detail path and detail content.

**Acceptance Scenarios**:

1. **Given** canonical ally state exists, **When** the player opens `/бой`, **Then** the overview lists allies separately from enemies.
2. **Given** the player inspects one ally, **When** the detail renders, **Then** it shows condition/action/effect notes in player-facing terms and preserves dynamic text safely.

---

### User Story 3 - Inspect a combat-log entry without raw JSON (Priority: P3)

A player can inspect one combat-log entry from `game_state/combat/combat_log.json` and read what happened, participants, result, round/turn, and consequences without navigating a long raw JSON blob.

**Why this priority**: Combat logs can grow into long all-in-one surfaces; entry detail prevents the command from becoming unreadable.

**Independent Test**: Seed `combat_log.json` with multiple entries and verify the command-result surface provides a player-facing list and individual entry detail path.

**Acceptance Scenarios**:

1. **Given** multiple log entries exist, **When** the player opens `/бой`, **Then** recent entries are summarized in a player-facing list.
2. **Given** the player inspects one log entry, **When** the detail renders, **Then** it shows the entry narrative/result and related participants without exposing raw state paths in ordinary output.

---

### User Story 4 - Preserve existing overview and scope boundaries (Priority: P1)

The existing `/бой` overview remains available for quick use, and afterlife spiritual combat commands remain unchanged.

**Why this priority**: #1054 is a drill-down improvement, not a rewrite or afterlife combat contract change.

**Independent Test**: Existing mortal read-only command tests continue to pass, `/бой` still reports overview counts/state, and afterlife command tests/source guards do not change except for unrelated pre-existing drift.

**Acceptance Scenarios**:

1. **Given** combat files are missing or sparse, **When** the player opens `/бой`, **Then** the command returns a graceful player-facing empty/sparse state rather than failing or leaking file paths.
2. **Given** afterlife spiritual combat state exists, **When** afterlife commands are used, **Then** this feature does not alter their behavior or docs.

### Edge Cases

- Missing `enemies.json`, `allies.json`, or `combat_log.json` must produce a useful overview/empty state, not an exception.
- Entries with missing optional fields must still render stable identifiers, names/titles, and available notes.
- Dynamic GM-authored text must be escaped/sanitized before Spectre/browser rendering.
- Default player-facing blocks must not expose raw JSON, file paths, `DTO`, `API`, `endpoint`, or debug/meta-language. Existing advanced/raw diagnostic blocks may remain only where the current architecture already exposes them behind the established raw sidecar behavior.
- If console/browser cannot both receive identical interaction mechanics in this slice, the branch must document the narrower gap and create or link a follow-up before merge.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/combat` and `/бой` MUST continue to render the existing Mortal World combat overview.
- **FR-002**: The shared browser command-result path MUST expose player-facing enemy list/detail content sourced from `game_state/combat/enemies.json` when canonical enemy state exists.
- **FR-003**: The shared browser command-result path MUST expose player-facing ally list/detail content sourced from `game_state/combat/allies.json` when canonical ally state exists.
- **FR-004**: The shared browser command-result path MUST expose player-facing combat-log list/detail content sourced from `game_state/combat/combat_log.json` when canonical log entries exist.
- **FR-005**: The console client MUST expose semantically equivalent mortal combat detail affordances for enemy, ally, and combat-log inspection, or the implementation MUST document the exact console gap and create/link a narrower follow-up before merge.
- **FR-006**: Default player-facing output MUST use Russian/in-world terminology and MUST NOT rely on raw JSON-only output for enemy, ally, or log entry inspection.
- **FR-007**: The implementation MUST NOT change afterlife spiritual combat command behavior, afterlife combat contracts, GM prompts, or runtime validation unless a newly tracked follow-up explicitly covers that change.
- **FR-008**: Regression tests/source guards MUST cover rich combat output for enemy, ally, and combat-log detail paths and must fail if `/бой` regresses to raw-only or all-in-one-only output.

### Key Entities

- **Mortal enemy**: A visible opponent entry from `game_state/combat/enemies.json`; may contain id/name, health/condition, role/threat, current action/intent, status/effects, notes, and other GM-authored fields.
- **Mortal ally**: A companion/allied combatant entry from `game_state/combat/allies.json`; may contain id/name, role, condition, current action, effects, notes, and related state.
- **Combat-log entry**: A chronological event from `game_state/combat/combat_log.json`; may contain round/turn/timestamp, participants, narrative/description, result/consequence, and tags.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove at least one enemy, one ally, and one combat-log entry can be inspected through player-facing command-result output without raw JSON dependency.
- **SC-002**: Existing `/бой` overview behavior remains covered by existing or updated tests.
- **SC-003**: Browser and console parity is demonstrated by tests, source guards, or a documented follow-up for any intentionally deferred parity sub-slice.
- **SC-004**: Verification includes focused C# tests, `dotnet build` where relevant, `git diff --check`, and an added-line security/static scan over the implementation diff.
- **SC-005**: The final PR body and issue evidence comment link #1054, state that GitHub Actions were not required, and record any follow-ups created for narrower remaining gaps.
