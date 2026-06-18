# Feature Specification: World News Detail Depth

**Feature Branch**: `fix/1106-world-news-detail-depth`

**Created**: 2026-06-18

**Status**: Ready for implementation

**Input**: Player feedback after #1104: selecting a world-news item shows too little information even when the underlying GM-authored record contains richer data, and console detail view has no way back to the news list.

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1106](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1106)
- **Related prior issue**: [#1104](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1104)
- **Issue type**: Bug / player-facing console UX.
- **Spec Kit justification**: This changes player-facing command detail rendering and console navigation flow.
- **Contract scope**: Shared console/browser command-result content plus console interaction flow. No GM-authored schema, validation, or afterlife contract change is intended.
- **Out of scope**: New world-news state fields, GM prompt rewrites, and browser visual redesign.

## User Scenarios & Testing

### User Story 1 - Read Rich World-News Details (Priority: P1)

As a player, I can select a world event, flag, or progression record and see the meaningful GM-authored data stored on that record, not only a minimal whitelist.

**Why this priority**: The selected detail is the main payoff of the compact overview. If it hides most information, the selector feels broken.

**Independent Test**: Seed a world event with extra scalar, array, and nested object fields. Execute its detail command and verify the readable output includes those fields without raw JSON/debug leakage.

**Acceptance Scenarios**:

1. **Given** a world event has additional GM-authored fields beyond the known core fields, **When** the player opens the event detail, **Then** those fields are shown in readable labels/values.
2. **Given** a field is an array or object, **When** the detail is rendered, **Then** it is summarized as structured readable text rather than raw JSON.
3. **Given** technical id keys exist, **When** the detail is rendered, **Then** they remain hidden unless already intentionally shown as player-facing metadata.

### User Story 2 - Return From Detail To List (Priority: P1)

As a console player, after selecting a world-news detail I can return to the world-news list through the menu instead of typing `/новости_мира` again.

**Why this priority**: The selector must support normal browsing, not a one-shot dead end.

**Independent Test**: Queue a console selection for one world event, then queue the back option, and verify the selector is shown again.

**Acceptance Scenarios**:

1. **Given** the player opens `/новости_мира`, **When** they select an event, **Then** the event detail is shown.
2. **Given** the event detail is shown, **When** the player chooses "Назад к списку", **Then** the world-news selector is shown again without requiring typed command input.

## Edge Cases

- Missing or malformed world-news files still show warnings/empty state and do not crash.
- Unknown additional fields with empty values are skipped.
- Dynamic GM-authored text remains escaped by existing renderers.
- Console detail navigation must not enter an infinite loop when the user chooses the final back/exit option.

## Requirements

- **FR-001**: World event, flag, and progression detail views MUST include meaningful additional GM-authored fields not already covered by core detail rows.
- **FR-002**: Additional fields MUST be rendered as player-readable key/value rows, not `UiRawJsonBlock` dumps.
- **FR-003**: Technical identifier keys MUST remain hidden from additional field rendering unless the field is intentionally shown as a core metadata row.
- **FR-004**: Console selected-detail flow MUST offer a menu path back to the world-news list.
- **FR-005**: Existing direct detail commands MUST remain valid and keep a "back to overview" action in the shared command result.
- **FR-006**: Browser/shared command result details MUST receive the richer detail content without requiring browser-specific code.

## Key Entities

- **World News Detail Record**: One event, flag, or progression JSON object rendered as readable fields.
- **Additional Detail Field**: A non-technical field on a detail record that was not consumed by the known core fields.
- **Console Detail Navigation**: A menu loop that lets the player browse details and return to the world-news overview selector.

## Success Criteria

- **SC-001**: Rich detail tests show extra scalar, array, and nested object fields in detail output.
- **SC-002**: Detail output contains no raw world-news JSON keys such as `worldEventsLog` or file paths.
- **SC-003**: Console navigation tests prove selecting a detail and returning to the list without typed command input.
- **SC-004**: Focused world-news command tests pass.

## Assumptions

- The current known-field rows remain useful and should stay at the top of detail panels.
- Unknown content-bearing fields should be shown under their field name when no better localization exists.
- Hiding technical identifiers is more important than showing every single raw key.
