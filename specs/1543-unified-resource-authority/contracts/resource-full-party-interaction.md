# FullParty Resource Interaction Contract

## 1. Authority boundary

`otherPlayersInteractions` is an outbound recipient-scoped packet surface. It does not grant the active client authority over another player's canonical ledger and it never aliases the active client's `player_current`.

The origin client validates the packet as part of the complete accepted response and stages it atomically with the turn. Delivery and admission by another client are a separate trust boundary. A receiving client must use its own accepted-turn authority; the staged packet is not, by itself, authenticated canonical state.

## 2. Root shape

The only accepted root shape is an object keyed by exact recipient `playerId`:

```text
otherPlayersInteractions: {
  <exact playerId>: [ <one or more closed command packets> ]
}
```

Recipient IDs are nonempty, trimmed, exact/confusable-unique, and may not identify the active player. Array-form legacy interaction roots are rejected. Each bucket is a nonempty array and each packet is a nonempty closed object.

## 3. Resource packet

A resource packet contains exactly:

```text
resourceChanges
```

`resourceChanges` is a nonempty array of ordinary commands from [resource-definition-and-command.md](resource-definition-and-command.md), with these additional restrictions:

- target is exactly `{kind:"player",targetId:"player_current"}`;
- `targetRef` is forbidden;
- source kind is one of `action_cost`, `combat_outcome`, or `narrative_outcome`, and raw `sourceId` is forbidden;
- definition creation, capacity change, item-local, afterlife, effect, receipt, canonical state, history, path, seal, fingerprint, and repair fields are forbidden;
- each event is the exact next ordinal in one global resource-command sequence shared with top-level local resource commands and all earlier recipient packets in deterministic exact recipient/bucket/array order;
- duplicate, reused, swapped, stale, future, or cross-packet event identity rejects the complete originating response.

Non-resource packet kinds remain governed by their own tracked contracts. They cannot be smuggled into a resource packet as sibling fields.

## 4. Publication and replay

Origin validation produces a client-owned packet fingerprint over session/request/turn, recipient ID, packet position, exact commands, and accepted events. The packet path and before-image participate in the same write lease, cache binding, rollback, and stale-output suppression as the accepted turn.

No resource mutation, history row, effect trigger, projection delta, or local receipt is created on the origin client from a remote packet. Re-emitting an identical packet during exact accepted-response retry is a transport replay and must not create a second staged record; any semantic mismatch under the same accepted event is a protected conflict.

## 5. Failure behavior

Malformed recipient identity, packet shape, target scope, source shape, event order, duplicate property, unknown field, limit overflow, late mutation, or local-application attempt is protected. The complete accepted turn fails with zero local resource/effect/interaction after-images. It is not converted into an actionable partial repair that could authorize a different recipient or mechanic.
