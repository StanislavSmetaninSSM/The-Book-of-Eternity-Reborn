# Research: Network Pass-The-Turn Multiplayer

Source issue: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1251>

## Decision: Shared protagonist only

**Decision**: Network seats are real participants who take turns controlling one
shared protagonist/soul. They are not in-world characters.

**Rationale**: The user explicitly accepted the shared-protagonist model. It
preserves the current game premise and avoids splitting canonical state across
multiple simultaneous heroes.

**Alternatives considered**:

- Multi-character party play: rejected because it would require separate GM
  authority, faction/NPC relation models, party logistics, and combat balance.
- Per-player AI/GM authority: rejected for MVP because it would create competing
  writers of canonical state.

## Decision: One canonical GM authority

**Decision**: A campaign has one canonical GM authority at a time, initially the
host GM bridge. Non-host active-player actions route through the relay to the
host GM authority.

**Rationale**: This keeps the existing accepted-turn validation model intact and
prevents local player AIs from writing conflicting canonical state.

**Alternatives considered**:

- Each player has their own GM: rejected because canonical state conflicts would
  become a primary gameplay bug.
- Relay-hosted GM: deferred because relay must remain transport/coordinator only
  in the accepted direction.

## Decision: Relay-based join and handoff

**Decision**: Primary UX uses relay/invite-code flow. Direct IP, port forwarding,
router setup, and manual save transfer are not normal UX.

**Rationale**: The user explicitly rejected manual save transfer as network
multiplayer. A relay provides the simplest player-facing connection model.

**Alternatives considered**:

- Manual export/import: allowed only as recovery/debug/offline fallback.
- Peer-to-peer: rejected for MVP due connection setup friction.

## Decision: Handoff packages remain state-authority units

**Decision**: Relay transports opaque accepted handoff packages with manifests,
hashes, validation status, and turn metadata. The relay does not interpret game
semantics.

**Rationale**: This reuses local file-backed authority and validation concepts
while making the relay safe to keep simple.

**Alternatives considered**:

- Relay mutates JSON state: rejected because it would duplicate runtime and GM
  authority outside the client.

## Decision: Dormancy is explicit resume, not game-time simulation

**Decision**: If all participants disconnect, the latest accepted handoff becomes
the campaign resume point. Real-world downtime does not advance in-fiction time.

**Rationale**: Players can stop and resume safely without hidden world changes.
If a time skip is desired, it remains an explicit GM-resolved game action.

**Alternatives considered**:

- Real-time simulation while offline: rejected as surprising and destructive.

## Decision: Mortal persona/guise ledger is prerequisite

**Decision**: Mortal World network play requires a canonical persona/guise ledger
before turn handoff is enabled.

**Rationale**: Network seat changes are not fiction events. Mortal identity
changes are fiction events and must affect NPC/faction knowledge through explicit
rules.

**Alternatives considered**:

- Treat seat changes as character changes: rejected as it would confuse real
  players with in-world identities.

## Known risk: hidden GM-only data privacy

**Decision**: MVP may be trust-based for private campaigns, but privacy
limitations must be documented. Full encrypted per-seat payload separation is a
later hardening slice.

**Rationale**: Solving privacy perfectly before the handoff model exists would
delay the core loop. The limitation must be visible so the feature is not
misrepresented.
