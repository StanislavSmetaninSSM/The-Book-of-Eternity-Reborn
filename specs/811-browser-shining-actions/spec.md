# Feature Specification: Browser Shining Abode Actions

**Feature Branch**: `task/811-browser-shining-actions`
**Created**: 2026-06-07
**Status**: Draft for implementation
**Input**: GitHub issue #811, "feat(web): Действия Сияющей Обители - открытие фракций, инвестиции, проекты"
**Source Issue**: [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open native faction discovery from the browser (Priority: P1)

As a player in the Shining Abode, I can open a browser action for discovering a native Shining faction, review the canonical Ink Feather and Light Spark cost, and confirm the spend through the guided form.

**Why this priority**: Console already exposes this core Shining Abode action. Browser players need parity for the first faction-opening action without falling back to console.

**Independent Test**: In a Shining Abode state with sufficient resources and no pending core action, execute the browser command and submit the confirmation; verify a single existing `pending_shining_abode_actions.json` request with `actionType: discover_native_faction`, the canonical quoted costs, radiance tier, and next turn.

**Acceptance Scenarios**:

1. **Given** the player is in Shining Abode with enough Ink Feathers and Light Sparks, **When** they open native faction discovery, **Then** the browser shows a player-facing confirmation prompt with the exact canonical costs.
2. **Given** the confirmation prompt is still valid, **When** the player submits it, **Then** the browser writes the existing Shining core action pending request and does not invent a new state shape.
3. **Given** the player is outside Shining Abode, has insufficient resources, or a Shining core action is already pending, **When** they open or submit the form, **Then** the browser returns a player-facing blocker and writes nothing.

---

### User Story 2 - Invest in a visible Shining faction from the browser (Priority: P1)

As a player in the Shining Abode, I can select an eligible visible faction, review the canonical investment cost, and confirm an investment through the browser.

**Why this priority**: Console supports investment in visible Shining factions; browser must preserve faction choice and resource-spend confirmation parity.

**Independent Test**: In a Shining Abode state with at least one visible eligible faction and one ineligible/hidden faction, execute and submit the browser investment form; verify only eligible visible factions are listed and the pending request uses `actionType: invest_in_faction` with the selected canonical faction.

**Acceptance Scenarios**:

1. **Given** visible operational Shining factions exist, **When** the player opens faction investment, **Then** the browser lists only canonical player-visible eligible factions and the canonical investment cost.
2. **Given** a stale form selects a hidden, missing, or no-longer-eligible faction, **When** the player submits, **Then** C# validation blocks the write with player-facing text.

---

### User Story 3 - Support, unsupport, and retire Shining projects from the browser (Priority: P1)

As a player in the Shining Abode, I can select visible completed projects to support, remove support, or retire them through browser guided forms.

**Why this priority**: Console project gate flows already support these mutations; browser parity requires project selection without exposing raw state details.

**Independent Test**: Seed visible completed supported and unsupported projects; verify support lists only unsupported completed projects, unsupport lists only supported projects, retirement lists visible completed projects, and submissions write the existing Shining core action request types.

**Acceptance Scenarios**:

1. **Given** the support cap has room, **When** the player opens project support, **Then** the browser lists only visible completed unsupported projects.
2. **Given** supported projects exist, **When** the player opens unsupport, **Then** the browser lists only visible supported projects.
3. **Given** visible completed projects exist, **When** the player opens retirement, **Then** the browser lists completed projects and submits `retire_project` through the existing pending action contract.

---

### User Story 4 - Browser metadata, help, and guards reflect support (Priority: P2)

As a browser player, I see the new Shining Abode actions in player-facing command metadata/help, and direct open or stale submit paths enforce realm and pending/local-write blockers.

**Why this priority**: Browser parity is not complete if commands work only through hidden paths or show raw command/API details.

**Independent Test**: Command coverage reports the five #811 action forms as browser-supported guided forms, help/menu text uses Russian player-facing labels, and command-open plus stale submit tests verify blocker enforcement.

**Acceptance Scenarios**:

1. **Given** browser command coverage is collected, **When** Shining Abode commands are inspected, **Then** the five action forms are covered as mutating guided forms and #811 no longer appears as an open browser coverage gap.
2. **Given** the player opens a prompt in Shining Abode and then state changes to Mortal World before submit, **When** the stale prompt is submitted, **Then** the write path re-checks realm and writes nothing.

### Edge Cases

- Existing Shining core action pending request or malformed pending file blocks open/submit with player-facing text.
- Resource costs are quoted from existing C# authority and validated again on submit.
- Hidden factions/projects and raw IDs that are not canonical player-visible choices are not offered in default forms.
- Stale prompt values for missing factions/projects or changed project support state fail through existing service validation.
- Browser labels and blockers must not expose `.json`, `pending_`, DTO/API/endpoint/debug wording, or internal validation field names in default UI.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The browser command catalog MUST define player-facing Shining Abode commands for native faction discovery, faction investment, project support, project unsupport, and project retirement.
- **FR-002**: Browser prompt builders MUST open guided forms only in Shining Abode actionable context and MUST use C# Shining Abode state authority for factions, projects, costs, resource availability, and blockers.
- **FR-003**: Native faction discovery MUST quote `ShiningAbodeState.GetNativeDiscoveryCost()` and write `ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction` through the existing pending action writer.
- **FR-004**: Faction investment MUST quote `ShiningAbodeState.GetFactionInvestmentCost()`, list only visible eligible factions, and write `ActionTypeInvestInFaction` through the existing pending action writer.
- **FR-005**: Project support, unsupport, and retirement MUST list only canonical visible eligible projects according to existing C# semantics and write `ActionTypeSupportProject`, `ActionTypeUnsupportProject`, or `ActionTypeRetireProject`.
- **FR-006**: Direct command-open paths and stale prompt-submit paths MUST re-check realm, Shining availability, pending core action state, local-write/active GM blockers, and canonical validation before writing.
- **FR-007**: Written pending/control payloads MUST keep the existing runtime contract in `game_state/control/pending_shining_abode_actions.json`; this feature MUST NOT add, rename, or remove pending/control fields.
- **FR-008**: Browser help, command menu metadata, and command coverage MUST recognize the five actions as supported browser guided forms while default UI stays player-facing and Russian.
- **FR-009**: Focused tests/source guards MUST be added before production implementation and must include command-open and stale-submit guard coverage where existing browser parity patterns support both.

### Key Entities

- **Shining Core Action Request**: Existing pending control request written by `ShiningCoreActionRequestState`, with action types for discovery, investment, support, unsupport, and retirement.
- **Visible Shining Faction**: Player-visible canonical faction from existing Shining/Saref state authority, eligible for the requested action only when current C# semantics allow it.
- **Visible Shining Project**: Canonical project attached to a visible faction and filtered by completion/support/retirement eligibility for the requested action.
- **Browser Prompt Session**: Existing browser guided prompt flow that opens from command metadata and submits to C# write service authority.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused browser parity tests prove each of the five #811 actions can open a player-facing prompt and submit to the existing Shining core action request contract.
- **SC-002**: Realm and stale-submit tests prove writes are blocked outside Shining Abode and no pending request is created.
- **SC-003**: Command/help/coverage tests prove the actions are browser-supported guided forms and #811 is removed from open browser coverage gaps.
- **SC-004**: No afterlife runtime contract, GM-authored response surface, prompt contract, or example manifest shape changes are introduced; therefore Afterlife contract matrix/example updates are not required for this slice.

## Out of Scope

- Sibling issues #812-#816 and umbrella issue #817 closure or broader afterlife parity.
- New game mechanics, new resources, new project/faction lifecycle states, or new GM-authored response fields.
- React/TypeScript UI redesign beyond existing browser command/prompt metadata consumed by current frontend.
- PR creation, merge, GitHub issue closure, or cron/job changes.

## Assumptions

- Existing C# service validation and pending writer remain the authority for resource costs, Shining realm checks, project support caps, faction visibility, and request shape.
- Browser forms may use explicit command aliases different from console menus as long as metadata/help labels are player-facing and the write path reuses existing C# authority.
- If implementation discovers a required runtime contract shape change, this spec must be revised and afterlife contract matrix/examples/coverage tests must be updated before completion.
