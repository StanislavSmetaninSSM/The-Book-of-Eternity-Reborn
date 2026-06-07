# Feature Specification: Browser Shining Abode Incarnation Gates

**Feature Branch**: `task/812-browser-incarnation-gates`
**Created**: 2026-06-07
**Status**: Implemented locally pending Hermes acceptance
**Input**: GitHub issue #812, "feat(web): Врата инкарнации — открытие, выбор благословений, реролл, финализация"
**Source Issue**: [#812](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/812)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open the Shining Gates draft from the browser (Priority: P1)

As a player in the Shining Abode, I can open a browser guided action for the Gates of incarnation and confirm the existing Shining core-action request that asks the GM to draft blessing cards.

**Why this priority**: Console already exposes "Открыть Врата" as the first step of the incarnation lifecycle. Browser players need the same entry point without falling back to console.

**Independent Test**: In an active Shining Abode state with no pending core action and no prepared incarnation package, open and submit the browser form; verify a single existing `pending_shining_abode_actions.json` request with `actionType: open_gates`, the canonical `createdAtTurn`, and no invented pending/control shape.

**Acceptance Scenarios**:

1. **Given** the player is in active Shining Abode and the Gates draft is closed, **When** they open Gates from the browser, **Then** the browser shows a player-facing confirmation prompt that explains the GM will draft blessing cards.
2. **Given** the confirmation prompt is still valid, **When** the player submits it, **Then** the browser writes the existing Shining core action pending request for `open_gates` through C# authority.
3. **Given** the player is outside Shining Abode, a core action is already pending, or an incarnation package is already prepared, **When** they open or submit the form, **Then** the browser returns player-facing blocker text and writes nothing.

---

### User Story 2 - Select or deselect blessing cards from the browser (Priority: P1)

As a player with an open Gates draft, I can choose visible blessing cards or remove a previous selection in the browser while the shared C# Gates state remains authoritative.

**Why this priority**: Console supports local selection/deselection against `gates.selectedBlessingCardIds`; browser parity requires the same local state mutation with stale-form guards.

**Independent Test**: Seed an open non-stale Gates draft with visible blessing cards and a pick cap; submit browser selection and deselection forms; verify `selectedBlessingCardIds` changes exactly according to `ShiningAbodeState.TrySelectBlessingCard` / `TryDeselectBlessingCard`, rejects hidden/missing/stale cards, and never exposes raw ids in default copy except as internal values in prompt answers.

**Acceptance Scenarios**:

1. **Given** an open non-stale Gates draft with available blessing cards, **When** the browser opens the selection form, **Then** the form lists visible player-facing card names/summaries and current selection state.
2. **Given** selecting a card is valid, **When** the player submits it, **Then** the browser updates `gates.selectedBlessingCardIds` through existing C# selection semantics.
3. **Given** a card is already selected, **When** the player submits the deselect action, **Then** the browser removes only that card from `selectedBlessingCardIds`.
4. **Given** the Gates draft becomes stale, closes, or the submitted card id is no longer available, **When** a stale prompt is submitted, **Then** the browser blocks with player-facing text and writes nothing.

---

### User Story 3 - Reroll the blessing draft from the browser (Priority: P1)

As a player with rerolls remaining, I can reroll the Shining Gates draft from the browser using the same local C# Gates state mutation as the console.

**Why this priority**: Console exposes "Обновить набор благословений" as a local Gates mutation. Browser players need parity for managing the draft before finalizing a package.

**Independent Test**: Seed an open draft with `rerollsRemaining > 0`, submit the browser reroll confirmation, and verify `ShiningAbodeState.TryRerollGatesDraft` updates shown/available cards, decrements rerolls, preserves contract shape, and rejects stale/no-reroll states.

**Acceptance Scenarios**:

1. **Given** an open non-stale draft with rerolls remaining, **When** the player opens reroll, **Then** the browser shows a confirmation prompt describing the current and next draft consequences in player-facing terms.
2. **Given** the reroll remains valid on submit, **When** the player confirms, **Then** the browser saves the Shining Abode state with the existing C# reroll semantics.
3. **Given** no rerolls remain, the draft is stale, or a core action is pending, **When** the browser opens or submits reroll, **Then** the browser blocks and writes nothing.

---

### User Story 4 - Prepare the incarnation package from the browser (Priority: P1)

As a player with selected blessings, I can finalize the current Gates draft into the existing Shining core-action request for preparing a new life package.

**Why this priority**: Console can prepare the next-life package after blessing selection. Browser parity must submit the same pending request so the GM closes the lifecycle consistently.

**Independent Test**: Seed a valid open draft with selected blessing card ids, submit the browser finalization form, and verify `pending_shining_abode_actions.json` contains `actionType: prepare_incarnation_package`, `sourceDraftVersion`, `selectedCardIds`, and `selectedCards` snapshots matching the canonical selected cards.

**Acceptance Scenarios**:

1. **Given** selected blessing cards exist in the current non-stale draft, **When** the player opens finalization, **Then** the browser shows a player-facing package summary with selected blessing names.
2. **Given** the draft remains valid, **When** the player confirms, **Then** the browser writes the existing Shining core action pending request for `prepare_incarnation_package`.
3. **Given** the selection is stale, empty when current rules require a selection, or no longer matches available cards, **When** a stale prompt is submitted, **Then** validation blocks the write with player-facing text.

---

### User Story 5 - Browser metadata, help, coverage, and player-facing guards reflect support (Priority: P2)

As a browser player, I can discover the new Gates lifecycle actions through player-facing command metadata/help, and default UI never exposes internal pending/control diagnostics.

**Why this priority**: Browser parity is incomplete if commands work only through hidden slash paths or if blockers leak raw state contract names in the normal player UI.

**Independent Test**: Command coverage reports the Gates lifecycle commands as supported guided forms or local mutations, browser help/menu text uses Russian player-facing labels, and direct command-open plus stale submit tests verify realm and pending/local-write guard enforcement.

**Acceptance Scenarios**:

1. **Given** browser command coverage is collected, **When** Shining Abode commands are inspected, **Then** #812 actions are covered and #812 is removed from open browser coverage gaps.
2. **Given** the default browser UI renders blockers or results for #812 actions, **Then** no `.json`, `pending_`, DTO/API/endpoint/debug wording, raw validation field names, or English internal action ids are shown to the player.

### Edge Cases

- Existing or malformed `pending_shining_abode_actions.json` blocks open/submit for core-action requests and local Gates mutations with player-facing copy.
- Active local write / GM turn blockers must be enforced through the existing local write coordinator, not only disabled frontend controls.
- Local Gates mutations (`select`, `deselect`, `reroll`) must re-check Shining realm, active availability, open draft, stale flag, current card availability, and pending core-action blockers on submit.
- Core-action requests (`open_gates`, `prepare_incarnation_package`) must re-check realm, active availability, prepared-package mode, pending core-action state, and canonical validation on submit.
- Browser forms may carry internal ids as submitted values, but default labels, summaries, blockers, and result copy must be player-facing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The browser command catalog MUST define player-facing commands for opening Gates, selecting/deselecting blessing cards, rerolling the Gates draft, and preparing the incarnation package.
- **FR-002**: Browser prompt builders MUST open guided forms only in Shining Abode actionable context and MUST use existing C# Shining Abode/Gates state authority for draft state, card lists, pick cap, reroll availability, selected cards, and blockers.
- **FR-003**: Open Gates MUST write `ShiningCoreActionRequestState.ActionTypeOpenGates` through the existing `pending_shining_abode_actions.json` writer and MUST NOT add a new runtime contract.
- **FR-004**: Blessing select/deselect MUST mutate `shining_abode_state.json` through existing `ShiningAbodeState.TrySelectBlessingCard` and `TryDeselectBlessingCard` semantics under the local write coordinator.
- **FR-005**: Reroll MUST mutate `shining_abode_state.json` through existing `ShiningAbodeState.TryRerollGatesDraft` semantics under the local write coordinator.
- **FR-006**: Prepare incarnation package MUST write `ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage` with canonical selected card ids and card snapshots through the existing pending action writer.
- **FR-007**: Direct command-open paths and stale prompt-submit paths MUST re-check realm, Shining availability, pending core action state, prepared package mode, local-write/active GM blockers, and canonical validation before writing.
- **FR-008**: Browser help, command menu metadata, API contract fixtures, and command coverage MUST recognize the #812 actions as browser-supported guided forms/local mutations while default UI stays player-facing and Russian.
- **FR-009**: Focused tests/source guards MUST be added before production implementation and must include command-open and stale-submit guard coverage where existing browser parity patterns support both.
- **FR-010**: This feature MUST keep existing afterlife runtime contract shapes unchanged. If implementation requires adding, renaming, or removing any pending/control/state field, the spec must be revised and afterlife contract docs/examples/tests must be updated before completion.

### Key Entities

- **Shining Gates Draft**: Existing `shining_abode_state.json.gates` state containing draft version, availability, shown/available candidate blessing cards, selected card ids, rerolls remaining, and stale flag.
- **Blessing Card**: Existing Gates candidate with canonical `cardId`, display name, summary, rarity/effect metadata, source metadata, and dedupe key.
- **Prepared Incarnation Package Request**: Existing Shining core action pending request that snapshots selected card ids/cards and source draft version for GM resolution.
- **Browser Prompt Session**: Existing browser guided prompt flow that opens from command metadata and submits to C# write/local-mutation authority.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused browser parity tests prove browser open-gates and prepare-package prompts submit to the existing Shining core action request contract.
- **SC-002**: Focused browser parity tests prove browser select/deselect and reroll mutate only the existing Gates state through shared C# semantics.
- **SC-003**: Realm, pending-core-action, stale-draft, missing-card, no-reroll, and prepared-package blockers are covered by command-open and/or stale-submit tests with player-facing copy.
- **SC-004**: Command/help/coverage/API fixture tests prove the #812 actions are browser-supported and #812 is removed from open browser coverage gaps.
- **SC-005**: No afterlife runtime contract, GM-authored response surface, prompt contract, or example manifest shape changes are introduced; therefore Afterlife contract matrix/example updates are not required unless implementation discovers a contract change.

## Out of Scope

- Sibling issues #813-#816 and umbrella issue #817 closure.
- New blessing/card mechanics, new resources, new lifecycle states, or new GM-authored response fields.
- Automatic execution of `TriggerIncarnation`; console explicitly prepares the package first and incarnation trigger happens later.
- React/TypeScript UI redesign beyond existing browser command/prompt metadata consumed by current frontend.
- PR creation, merge, GitHub issue closure, or cron/job changes.

## Assumptions

- Existing C# service validation and state helpers remain the authority for Gates draft state, blessing selection, rerolls, core action requests, and package finalization.
- Browser forms may use explicit command aliases different from console menu text as long as metadata/help labels are player-facing and the write path reuses existing C# authority.
- If implementation discovers a required runtime contract shape change, this spec must be revised and afterlife contract matrix/examples/coverage tests must be updated before completion.
