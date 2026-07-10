# Feature Specification: Universal Realm-Aware Trade Command

**Feature Branch**: `task/1491-universal-trade`
**Created**: 2026-07-10
**Status**: Approved for implementation
**Source Issue**: [#1491](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1491)
**Related Issues**: [#1469](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1469), [#1459](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1459), [#1476](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1476), [#805](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/805), [#1367](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1367)

## Context

The game exposes working trade systems in every playable realm, but each one has a different command name. During a live Chaos Sea test, `/торговля` silently returned the player to the previous screen even though Guardian trade was available through a longer specialized command. Players need one memorable command that opens the trade appropriate to their current realm while preserving all existing specialized commands.

## User Scenarios and Testing

### User Story 1 - One familiar command in every realm

As a player, I can enter `/торговля` or `/trade` in any playable realm and reach the trade surface appropriate to that realm.

**Acceptance Scenarios**

1. **Given** the soul is in a Mortal World, **when** the player enters `/торговля`, **then** the client lists merchant NPCs in the player's current location by player-facing name and lets the player choose one.
2. **Given** the soul is in the Chaos Sea, **when** the player enters `/торговля`, **then** the client lists Guardians available in the soul's current abode/location by player-facing name and lets the player choose one.
3. **Given** the soul is in the Shining Abode, **when** the player enters `/торговля`, **then** the client lists player-visible Shining factions available for trade by player-facing name and lets the player choose one.
4. **Given** the player selects a listed entity, **when** the client opens its trade surface, **then** the entity ID is carried only as an internal UI command value and is never requested as player input.
5. **Given** an internal deep-link target argument is supplied, **when** the selected realm-specific flow accepts a target, **then** the argument is preserved for compatibility.

### User Story 2 - Existing trade behavior remains authoritative

As a player, I receive the same offers, prices, purchase rules, pending-request behavior, and results whether I use the universal command or the corresponding specialized command.

**Acceptance Scenarios**

1. Missing or stale stock uses the existing in-place waiting message and automatically refreshes after the GM prepares the vitrine.
2. Ready stock uses the existing local purchase, sale, and buyback operations supported by that realm.
3. The universal command does not create a new trade inventory, pricing rule, receipt, or GM request shape.

### User Story 3 - Failure is explicit

As a player, I receive a clear localized explanation when the current realm cannot be determined or trade cannot be opened.

**Acceptance Scenarios**

1. An unresolved realm never causes a silent return to the game loop.
2. An unresolved realm does not mutate state, create a pending request, or submit a GM turn.
3. Console and browser expose equivalent routing and failure semantics.

## Functional Requirements

- **FR-001**: The command catalog MUST register `/trade` and `/торговля` as one universal, argument-capable trade command.
- **FR-002**: The universal command MUST route Mortal World play to the existing NPC trade command.
- **FR-003**: The universal command MUST route Chaos Sea play to the existing Guardian trade command.
- **FR-004**: The universal command MUST route Shining Abode play to the existing Shining trade command.
- **FR-005**: Routing MUST preserve non-empty arguments without interpreting realm-specific target semantics.
- **FR-006**: Existing specialized trade commands MUST remain available and behaviorally unchanged.
- **FR-007**: Console and browser command boundaries MUST use the same realm-routing decision.
- **FR-008**: The help surface MUST describe the universal command and explain that the destination depends on the current realm.
- **FR-009**: An unresolved or unsupported realm MUST produce localized player-facing guidance and MUST NOT invoke any trade mutation or GM request.
- **FR-010**: Existing in-place vitrine preparation and auto-refresh behavior MUST remain intact after routing.
- **FR-011**: A no-argument universal command MUST first render a non-mutating player-facing selection of trade entities available in the current location/realm.
- **FR-012**: Selection labels, cards, help, and prompts MUST use player-facing names and descriptions; they MUST NOT ask the player to know or type an entity ID.
- **FR-013**: Selecting an entity MAY carry its stable ID in an internal action command, but the ID MUST remain hidden from ordinary player copy.
- **FR-014**: Opening the selection list MUST NOT create a pending GM request or acquire a persistent local mutation lock; those begin only after the player chooses an entity and performs an applicable trade action.

## Edge Cases

- `currentRealm` is absent, blank, or unrecognized.
- The realm is Shining Abode pending bootstrap rather than ordinary active Shining play.
- The routed system has no eligible merchant, Guardian, or Shining faction in the current location/realm.
- An argument names a target that the routed specialized command cannot resolve.
- A trade vitrine is already being prepared by the GM.

## Out of Scope

- New currencies, prices, stock-generation rules, trade receipts, or economy balance.
- Redesigning trade cards, selectors, or offer details.
- Removing or renaming specialized trade commands.
- Changing GM-authored pending/control contracts.

## Assumptions

- Existing realm-specific commands remain the authority for eligibility, target selection, vitrine preparation, and mutations.
- Ordinary Mortal World realms are any resolved realms that are not Chaos Sea or Shining Abode, consistent with current realm semantics.
- Shining Abode bootstrap restrictions remain enforced by the existing Shining trade flow.

## Success Criteria

- **SC-001**: In automated coverage, both universal aliases list and open the correct location-aware trade entities in all three playable realm categories without player-entered IDs.
- **SC-002**: In automated coverage, unresolved realm use produces an explicit localized message and zero state or pending-request changes.
- **SC-003**: All existing focused trade tests continue to pass without changed expected economy values.
- **SC-004**: In the Chaos Sea Agent Console replay, `/торговля` opens the Guardian trade panel on the first invocation instead of returning silently.

## Contract Impact

This feature changes player-facing command routing only. It intentionally does not alter Mortal World or afterlife GM-authored state, pending/control files, receipts, validation rules, prices, or examples. The implementation must verify that this assumption remains true; if it does not, afterlife and Mortal GM documentation synchronization becomes mandatory before completion.
