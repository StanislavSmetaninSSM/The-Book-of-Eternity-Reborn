# Feature Specification: Network Pass-The-Turn Multiplayer

**Feature Branch**: `1266-universal-command-audit`

**Created**: 2026-06-24

**Status**: Draft

**Input**: GitHub issue #1251 and follow-up comments about shared protagonist
network multiplayer, relay-based turn handoff, dormant campaign resume, and
host GM authority.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1251>
- **Issue type**: epic / enhancement / design
- **Spec Kit justification**: This changes runtime state authority, save/load,
  validation, GM bridge behavior, console/browser UX, and GM-facing
  documentation. It is cross-session work and must be decomposed before code.
- **Contract scope**: player-facing, GM-facing prompts, runtime-state,
  validation, docs, examples, console, browser.
- **Out of scope**: competitive multi-character play, peer-to-peer connection,
  manual save transfer as the primary UX, multiple canonical GMs writing the
  same campaign at once, and public matchmaking.

## User Scenarios & Testing

### User Story 1 - Host creates a shared-protagonist campaign (Priority: P1)

A host starts a multiplayer campaign, invites known players, and the campaign
tracks seats, active turn owner, host GM authority, and the controlled entity
without changing the existing fiction.

**Why this priority**: Without explicit campaign metadata and identity
separation, every later network feature risks corrupting game authority.

**Independent Test**: Create a local/reference multiplayer campaign and inspect
metadata showing campaign id, seats, active seat, GM authority seat, turn ordinal,
controlled entity, and handoff hashes.

**Acceptance Scenarios**:

1. **Given** a local single-player session, **When** the host creates a network
   campaign, **Then** the campaign records one host seat, the host GM authority,
   the active seat, and the current controlled entity.
2. **Given** a Mortal World campaign, **When** campaign metadata is created,
   **Then** it records the current incarnation and active persona/guise ledger
   entry separately from the network seat.
3. **Given** an afterlife campaign, **When** campaign metadata is created,
   **Then** it records the shared `player_soul` as the controlled entity.

---

### User Story 2 - Player joins and receives the active state (Priority: P1)

An invited player joins through a relay/invite-code flow without IP addresses,
open ports, router setup, or manual save transfer.

**Why this priority**: The accepted direction explicitly rejects manual save
transfer as the normal multiplayer UX.

**Independent Test**: Join a local/reference relay campaign from a second client,
approve or accept the seat, synchronize the latest accepted campaign state, and
show read-only mode if the seat is not active.

**Acceptance Scenarios**:

1. **Given** a valid invitation, **When** a player joins, **Then** the relay
   creates or reuses a persistent seat credential and the client downloads the
   latest accepted resume point.
2. **Given** a non-active seat, **When** the player opens the campaign, **Then**
   the client shows the game state read-only with a clear reason.
3. **Given** an invalid, expired, or revoked invitation, **When** a player tries
   to join, **Then** the client explains that the invitation cannot be used.

---

### User Story 3 - Active player completes a turn handoff (Priority: P1)

The active player performs actions, the canonical host GM authority resolves the
turn, validation accepts the resulting state, and the relay distributes a signed
handoff package to the next active seat.

**Why this priority**: This is the minimum viable multiplayer loop.

**Independent Test**: In a two-seat local relay campaign, perform one active-seat
turn, block handoff until the state is valid and no pending turn/QTE/repair loop
remains, then advance the active seat and synchronize the other client.

**Acceptance Scenarios**:

1. **Given** an active player seat, **When** the player submits a GM action,
   **Then** the action is routed to the canonical host GM authority, not to a
   player-local AI.
2. **Given** an accepted state, **When** the active seat ends the turn, **Then**
   the client uploads a handoff package with manifest, hashes, turn ordinal, and
   controlled entity metadata.
3. **Given** an unfinished GM turn, pending QTE, invalid state, or repair loop,
   **When** the player attempts handoff, **Then** handoff is blocked with a
   player-facing reason.

---

### User Story 4 - Campaign goes dormant and later resumes (Priority: P2)

All players may disconnect and later resume from the last accepted campaign
point without advancing game time automatically.

**Why this priority**: Real play sessions will stop and resume later; losing
authority or advancing fiction accidentally would be worse than having no
network mode.

**Independent Test**: Disconnect every client from a local relay campaign,
restart clients, reconnect by campaign/seat identity, and verify no game-time or
world-state progression happened until the GM explicitly resolves it.

**Acceptance Scenarios**:

1. **Given** every participant disconnects, **When** the relay remains available,
   **Then** it keeps the latest accepted resume point and current active seat.
2. **Given** a returning player, **When** they reconnect with their seat
   credential, **Then** the client synchronizes from the latest accepted resume
   point before allowing action.
3. **Given** the host GM authority is offline, **When** a non-host active player
   attempts a GM-resolved action, **Then** the campaign waits/read-only with a
   clear reason.

---

### User Story 5 - Mortal persona and guise changes stay fictional (Priority: P2)

In Mortal World, changing active persona/guise is a fiction event and can affect
NPC and faction knowledge, suspicion, relations, and histories; changing the
network active seat is not a fiction event.

**Why this priority**: The shared-protagonist model depends on separating real
players from in-world identity.

**Independent Test**: Switch active network seats without changing NPC/faction
state, then trigger a mortal persona/guise change and verify it creates
fictional consequences and canonical ledger entries.

**Acceptance Scenarios**:

1. **Given** a Mortal World campaign, **When** turn ownership passes from one
   seat to another, **Then** NPCs and factions do not see a different person.
2. **Given** the character changes disguise or persona, **When** the GM accepts
   the result, **Then** the persona ledger records the change and affected
   NPC/faction knowledge can update through normal world logic.

---

### Edge Cases

- Relay is reachable but the host GM authority is offline.
- Active seat disconnects mid-turn before an accepted handoff exists.
- Two clients attempt to upload handoffs for the same turn ordinal.
- Client has a local state whose hash does not match the relay resume point.
- Campaign is afterlife-only and the controlled entity remains the same soul.
- Mortal World persona/guise changes while a turn handoff is pending.
- Hidden GM-only data exists in the handoff bundle; MVP is private-campaign
  trust-based and must document privacy limitations.
- Relay loses or corrupts a package; clients must reject mismatched hashes.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST represent a multiplayer campaign with campaign id,
  seats, active seat, host/GM authority seat, turn ordinal, controlled entity,
  latest accepted handoff hash, and resume status.
- **FR-002**: The system MUST distinguish network seats from the player soul,
  Mortal World incarnation, and active mortal persona/guise.
- **FR-003**: The system MUST support a Mortal persona/guise ledger before
  Mortal World turn handoff is enabled.
- **FR-004**: The system MUST support a relay/invite-code join flow that does
  not require IP addresses, port forwarding, router setup, or manual save
  transfer.
- **FR-005**: The system MUST support persistent seat credentials for reconnects
  and must not require reusing the original invitation after a seat exists.
- **FR-006**: The system MUST support revocable invitations, optional host
  approval, and pending join requests.
- **FR-007**: The relay MUST act as transport and campaign coordination only; it
  MUST NOT interpret, validate, or mutate game-state semantics.
- **FR-008**: The campaign MUST have one canonical GM authority at a time,
  initially the host GM bridge.
- **FR-009**: Player-local AI MAY assist with wording, but MUST NOT write
  canonical campaign state.
- **FR-010**: Non-host active-player GM actions MUST route through the relay to
  the canonical host GM authority.
- **FR-011**: If the host GM authority is unavailable, GM-resolved actions MUST
  wait or block with a clear player-facing reason.
- **FR-012**: Handoff MUST be blocked when there is an active GM turn, repair
  loop, unfinished local action, pending QTE, invalid state, or hash mismatch.
- **FR-013**: Accepted handoff packages MUST include manifest data sufficient to
  prove turn ordinal, previous hash, new hash, active seat transition,
  controlled entity, and validation status.
- **FR-014**: Non-active seats MUST be read-only and must see why they cannot
  submit canonical actions.
- **FR-015**: When every participant disconnects, the relay MUST keep the latest
  accepted resume point; real-world downtime MUST NOT automatically advance
  game time, living-world schedules, NPC/faction actions, afterlife progression,
  or GM-authored state.
- **FR-016**: Returning clients MUST synchronize from the latest accepted resume
  point before acting.
- **FR-017**: Host migration MUST be represented in the model, even if the MVP
  only supports waiting until the original host GM authority returns.
- **FR-018**: MVP privacy limitations for hidden GM-only data MUST be documented
  before enabling network play outside trusted/private campaigns.
- **FR-019**: Console and browser clients MUST expose equivalent player-facing
  multiplayer status, blocked-state reasons, invite/join controls, and handoff
  controls when the feature reaches implementation.
- **FR-020**: GM-facing prompts, examples, and validation documentation MUST be
  updated before any GM-authored multiplayer state or persona/guise contract is
  accepted by runtime validation.

### Key Entities

- **Network Campaign**: A shared campaign record with metadata, seats, active
  turn owner, canonical authority, and latest accepted handoff.
- **Seat**: A real participant slot. It is not a character, soul, incarnation,
  NPC, or faction identity.
- **Seat Credential**: Persistent reconnect identity for a seat.
- **Invitation**: Revocable join token that can create or request a seat.
- **Controlled Entity**: The canonical in-game entity currently controlled by
  the campaign: `player_soul` in afterlife or current incarnation/persona in
  Mortal World.
- **Mortal Persona / Guise Ledger**: Canonical history of active in-world
  identity, disguises, replacements, and their fictional consequences.
- **Handoff Package**: Transport bundle containing manifest, state snapshot or
  delta, hashes, validation result, and next active seat.
- **Relay Resume Point**: Latest accepted handoff retained by the relay for
  reconnects and dormant campaigns.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Two local clients can complete a full create, join, act, handoff,
  sync loop without manual save export/import.
- **SC-002**: Handoff attempts are rejected in 100% of tested blocked states:
  active GM turn, pending QTE, invalid state, repair loop, unfinished local
  action, and hash mismatch.
- **SC-003**: Reconnecting clients can resume from the latest accepted relay
  resume point in a local/reference relay test without automatic in-fiction time
  advancement.
- **SC-004**: Non-active seats cannot submit canonical state-changing actions in
  automated tests.
- **SC-005**: Mortal seat handoff causes zero NPC/faction identity changes unless
  a separate persona/guise fiction event is accepted.
- **SC-006**: The feature ships with GM-facing documentation/examples and
  validation coverage for any new state contract it introduces.

## Verification Plan

- **C# verification**:
  `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "NetworkMultiplayer|Handoff|PersonaLedger|Relay"`
- **Documentation/contract verification**:
  `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`
- **Frontend verification**: when browser UI is touched, run
  `npm run verify` from `BookOfEternityClient.WebFrontend/`.
- **Manual/player-facing verification**: run a two-seat local/reference relay
  playthrough: host creates campaign, guest joins, active seat acts, handoff
  advances, all clients disconnect, clients reconnect, no game time advances
  until a GM-resolved action explicitly advances it.

## Assumptions

- MVP targets private/trusted groups and local/reference relay first.
- Central hosted relay deployment, authentication hardening, encrypted
  GM-private payload separation, and full host migration can be implemented in
  later tracked issues after the local/reference protocol proves the model.
- Manual handoff/export-import may exist only as recovery/debug/offline fallback.
- Network play does not replace the single-player local game loop.
- Browser and console parity is required for user-visible multiplayer controls,
  but visual layout can differ.
