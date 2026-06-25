# Contract: Relay Protocol For Shared-Protagonist Turn Handoff

Source issue: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1251>

This contract describes the player-visible and runtime-level relay behavior. It
does not prescribe final HTTP route names or serialization details.

## Relay Responsibilities

The relay is allowed to:

- Create campaign coordination records.
- Store invitations, join requests, seats, and seat credentials.
- Store opaque accepted handoff packages and manifests.
- Return the latest accepted resume point.
- Track online/offline seat presence.
- Route non-host active-player GM actions to the canonical host GM authority.

The relay is not allowed to:

- Interpret game-state semantics.
- Mutate `game_state` fields.
- Resolve GM actions.
- Validate Mortal World, Chaos Sea, or Shining Abode contracts.
- Become a second canonical GM authority.

## Create Campaign

Input:

- campaign name
- host display name
- current realm
- controlled entity reference
- latest local state hash

Output:

- campaign id
- host seat id
- host seat credential
- active seat id
- host GM authority seat id
- turn ordinal
- latest accepted handoff hash

Failure examples:

- local state is invalid
- no controlled entity can be resolved
- Mortal World campaign has no persona ledger

## Create Invitation

Input:

- campaign id
- host seat credential
- optional display-name restriction
- optional expiration
- host approval requirement

Output:

- invite code
- expiration
- invitation status

Failure examples:

- host seat credential is invalid
- campaign is closed
- host is not allowed to invite

## Join Campaign

Input:

- invite code
- desired display name
- optional existing seat credential for reconnect

Output:

- pending join request or accepted seat
- seat credential when accepted
- latest relay resume point
- read-only/active mode

Failure examples:

- invite expired
- invite revoked
- host approval required and still pending
- seat credential revoked

## Upload Handoff Package

Input:

- campaign id
- active seat credential
- handoff manifest
- opaque package bytes or file bundle

Required checks:

- seat is active
- turn ordinal matches expected next turn
- previous hash matches latest accepted relay hash
- host GM authority id matches campaign authority
- manifest says validation accepted

Output:

- accepted handoff hash
- new active seat id
- new resume point

Failure examples:

- wrong active seat
- stale previous hash
- duplicate turn ordinal
- validation not accepted
- package hash mismatch

## Download Resume Point

Input:

- campaign id
- seat credential

Output:

- latest accepted handoff manifest
- opaque package bytes or download reference
- active seat id
- host GM authority seat id
- resume status

Failure examples:

- credential revoked
- no accepted handoff exists
- relay storage corruption or hash mismatch

## Route GM Action

Input:

- campaign id
- active seat credential
- player action text or command envelope

Output:

- queued for host GM authority
- blocked waiting for host GM authority
- rejected because seat is not active

Rules:

- If host GM authority is online, the action routes to it.
- If host GM authority is offline, the action waits or blocks. It must not be
  resolved by a player-local AI.

## Dormancy And Resume

When every seat disconnects:

- relay keeps the latest accepted resume point;
- active seat remains unchanged;
- host GM authority remains unchanged;
- real-world downtime does not advance game time or afterlife progression.

When a seat reconnects:

- credential is verified;
- latest resume point is downloaded;
- client synchronizes before actions are enabled;
- if host GM authority is absent, GM-resolved actions remain blocked.
