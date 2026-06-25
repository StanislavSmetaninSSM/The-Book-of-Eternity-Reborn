# Data Model: Network Pass-The-Turn Multiplayer

Source issue: <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1251>

## NetworkCampaign

Represents one shared-protagonist multiplayer campaign.

Fields:

- `campaignId`: stable campaign identifier.
- `campaignName`: player-facing title.
- `mode`: `shared_protagonist`.
- `realm`: current game realm such as Mortal World, Chaos Sea, or Shining
  Abode.
- `activeSeatId`: seat currently allowed to submit canonical player actions.
- `hostGmAuthoritySeatId`: seat whose GM bridge is canonical.
- `turnOrdinal`: monotonically increasing handoff ordinal.
- `latestAcceptedHandoffHash`: hash of the latest accepted handoff package.
- `previousHandoffHash`: hash link for audit/history.
- `controlledEntity`: reference to `ControlledEntityRef`.
- `resumeStatus`: active, dormant, waiting_for_active_seat,
  waiting_for_gm_authority, or blocked.
- `createdAt` / `updatedAt`: metadata timestamps.

Rules:

- Exactly one active seat exists when the campaign is playable.
- Exactly one canonical GM authority exists at a time.
- Turn ordinal cannot skip or repeat.

## NetworkSeat

Represents a real participant slot.

Fields:

- `seatId`: stable participant slot id.
- `displayName`: player-facing participant name.
- `role`: host, participant, observer, or pending.
- `credentialId`: reference to persistent reconnect credential.
- `status`: active, offline, pending_join, revoked, or removed.
- `permissions`: read_only, active_turn_actions, host_approval,
  gm_authority.
- `lastSeenAt`: reconnect/dormancy metadata.

Rules:

- A seat is never an NPC, soul, incarnation, or persona.
- Non-active seats are read-only for canonical actions.

## SeatCredential

Persistent reconnect identity for a seat.

Fields:

- `credentialId`
- `seatId`
- `campaignId`
- `createdAt`
- `revokedAt`
- `lastUsedAt`

Rules:

- Returning players use seat credentials, not the original invite.
- Revoked credentials cannot synchronize private campaign data.

## MultiplayerInvitation

Revocable invitation used to create or request a seat.

Fields:

- `inviteCode`
- `campaignId`
- `createdBySeatId`
- `expiresAt`
- `revokedAt`
- `requiresHostApproval`
- `allowedDisplayName`
- `joinRequestId`

Rules:

- Invitation failure must be explained in player-facing terms.
- Invitations do not replace persistent seat credentials.

## ControlledEntityRef

Identifies what the shared campaign controls.

Fields:

- `realm`
- `entityKind`: `player_soul`, `mortal_incarnation`, or `mortal_persona`.
- `entityId`
- `personaLedgerId` when Mortal World identity is involved.

Rules:

- Afterlife always uses the shared soul.
- Mortal World must reference the current incarnation and active persona/guise.

## MortalPersonaLedger

Canonical history of in-world identity for Mortal World.

Fields:

- `ledgerId`
- `incarnationId`
- `activePersonaId`
- `entries[]`

Rules:

- A network seat change does not create a ledger entry.
- A disguise, guise, replacement, or identity reveal creates a ledger entry and
  may affect NPC/faction knowledge.

## MortalPersonaEntry

One in-world identity state or transition.

Fields:

- `personaId`
- `displayName`
- `kind`: true_identity, disguise, assumed_name, possession, body_swap,
  illusion, other.
- `startedAtTurn`
- `endedAtTurn`
- `visibleToNpcIds[]`
- `visibleToFactionIds[]`
- `suspicionEffects[]`
- `relationshipEffects[]`
- `notes`

Rules:

- Effects must be GM-authored and validated.
- Persona effects must not be inferred from network seat changes.

## HandoffPackageManifest

Transport manifest for an accepted turn handoff.

Fields:

- `campaignId`
- `turnOrdinal`
- `fromSeatId`
- `toSeatId`
- `hostGmAuthoritySeatId`
- `controlledEntity`
- `previousHash`
- `packageHash`
- `stateHash`
- `validationStatus`
- `blockedReason` if handoff failed.
- `createdAt`

Rules:

- Relay stores and distributes this manifest but does not interpret game-state
  semantics.
- Clients reject mismatched hashes or unexpected turn ordinals.

## RelayResumePoint

Latest accepted campaign state retained for reconnects.

Fields:

- `campaignId`
- `handoffHash`
- `turnOrdinal`
- `activeSeatId`
- `hostGmAuthoritySeatId`
- `resumeStatus`
- `storedAt`

Rules:

- Dormancy does not advance in-fiction time.
- Returning clients must sync from this point before acting.

## HandoffGate

Derived readiness check before handoff.

Fields:

- `canHandoff`
- `blockedReasons[]`
- `hasActiveGmTurn`
- `hasRepairLoop`
- `hasUnfinishedLocalAction`
- `hasPendingQte`
- `isStateValid`
- `hashMatchesRelay`

Rules:

- Any true blocking condition prevents handoff.
- Reasons must be player-facing in console/browser.
