# Feature Specification: Mortal Bootstrap Placeholder Name Guard

**Feature Branch**: `main`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User confirmed that Mortal World bootstrap must not leave player-visible scaffold names such as `Стартовая сцена`, `Силы стартовой сцены`, and `Путь из стартовой сцены`.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1461 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1461
- **Issue type**: golden route blocker / validation contract.
- **Spec Kit justification**: This changes Mortal World validation, GM-facing bootstrap guidance, repair behavior, and live-test quality gates.
- **Contract scope**: Mortal World bootstrap, validation, repair guidance, GM prompts/examples.
- **Out of scope**:
  - Auto-generating artistic names in the client.
  - Renaming client-owned pre-materialization baseline files before the GM receives the first Mortal bootstrap task.
  - Browser or console presentation changes.

## User Scenarios & Testing

### User Story 1 - Reject Lazy Bootstrap Names (Priority: P1)

As a player starting a new Mortal World life, I should never see scaffold names like `Стартовая сцена новой жизни` or `Силы стартовой сцены` as the actual world.

**Independent Test**: Build a fresh Mortal bootstrap baseline, mark the first Mortal bootstrap turn as accepted, leave placeholder names in current location, map, factions, exits, and NPCs, then validate.

**Acceptance Scenarios**:

1. **Given** accepted first Mortal bootstrap state contains `Стартовая сцена новой жизни` in player-visible location fields, **When** validation runs, **Then** it reports an error with code `mortal_bootstrap_placeholder_player_visible_name`.
2. **Given** accepted first Mortal bootstrap state contains `Силы стартовой сцены` in canonical faction fields, **When** validation runs, **Then** it reports the same blocking issue and points to `game_state/factions/faction_core.json`.
3. **Given** accepted first Mortal bootstrap state contains `Путь из стартовой сцены`, `Ближайший выход из стартовой сцены`, or `Наставник стартовой сцены` in visible map/link/NPC fields, **When** validation runs, **Then** repair guidance tells the GM to replace the scaffold label with an in-world name derived from the player-authored character/world/start prompt.

### User Story 2 - Preserve Client-Owned Baseline (Priority: P1)

As the runtime, I can still create a safe temporary baseline before the GM materializes the first Mortal scene.

**Independent Test**: Build a fresh Mortal bootstrap baseline with scaffold names, keep it client-owned before accepted first Mortal turn, then validate.

**Acceptance Scenarios**:

1. **Given** the state is the client-owned handoff baseline and no first Mortal bootstrap accepted turn exists, **When** validation runs, **Then** placeholder names do not block validation.
2. **Given** the first Mortal bootstrap accepted turn exists, **When** the same placeholders remain, **Then** validation fails and repair packet generation can route the GM to rewrite the player-visible names.

## Functional Requirements

- **FR-001**: Validator MUST reject player-visible Mortal World bootstrap scaffold names after accepted first Mortal bootstrap materialization.
- **FR-002**: Validator MUST allow the same scaffold names only while they are client-owned pre-materialization baseline.
- **FR-003**: The forbidden pattern list MUST cover Russian start-scene placeholders and English generic placeholders used as visible names.
- **FR-004**: Repair hints MUST instruct the GM to author concrete in-world names rather than delete entities or rely on the client to invent names.
- **FR-005**: GM-facing first Mortal bootstrap guidance MUST explicitly require replacing scaffold labels with in-world names for locations, exits, factions, and starter NPCs.
- **FR-006**: The change MUST include focused regression tests and documentation/source-guard coverage.

## Non-Goals

- The client will not infer faction names from prompt text.
- The normalizer will not silently rewrite accepted player-visible names.
- Existing old saves are not bulk-migrated by this issue.

## Success Criteria

- Focused tests fail before implementation and pass after implementation.
- First Mortal bootstrap validation blocks accepted placeholder names with actionable file paths.
- Existing client-owned baseline tests continue to pass.
- GM prompt/example guidance mentions that scaffold names are temporary and not player-visible accepted content.
