# Contract: Mortal Location Identity Authority

**Feature**: [Complete Mortal Location Materialization](../spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

## Purpose

Define client-owned permanent location/link identity, sealed receipt evidence,
origin history, exact matching, lifecycle, and replay prevention.

## Identity classes

| Class | Prefix/example | Author | Lifetime |
| --- | --- | --- | --- |
| Location temporary reference | `locref_turn_12_black_ford` | GM within authorized route or client bootstrap reservation | One accepted creation turn |
| Location materialization identity | `mlocmat_turn_12_black_ford` | GM | Historical, never reusable |
| Permanent location identity | `loc_<32 lower hex>` | Client | Durable, never reusable |
| Location receipt identity | `mlocrec_<32 lower hex>` | Client | Durable, immutable |
| Link temporary reference | `linkref_turn_12_ford_to_tower` | GM within authorized route or client reservation | One accepted creation turn |
| Link materialization identity | `mlinkmat_turn_12_ford_to_tower` | GM | Historical, never reusable |
| Permanent link identity | `lnk_<32 lower hex>` | Client | Durable, never reusable |
| Link receipt identity | `mlinkrec_<32 lower hex>` | Client | Durable, immutable |
| Lifecycle transition identity | `mltrn_<32 lower hex>` | Client | Durable audit evidence |

Prefixes document client output but do not replace exact uniqueness checks.
Every supplied identity must be a JSON string, non-empty, and equal to its own
trimmed value.

## Exact and confusable rules

Operational matching uses `StringComparer.Ordinal`. In addition, creation is
rejected when any new temporary/materialization/permanent/receipt identity is:

- an exact duplicate of active or retired history;
- equal under ordinal case folding to historical evidence;
- unequal only because of leading/trailing whitespace;
- unequal only under Unicode normalization;
- a catalogued Unicode confusable of historical evidence.

Names, display names, descriptions, slugs, indices, and coordinates never stand
in for identity. Confusable detection is for rejection only; it never selects a
target.

## Identity index root

Path: `game_state/world/location_identity_index.json`.

```json
{
  "schemaVersion": 1,
  "realm": "mortal_world",
  "locationEntries": [],
  "linkEntries": []
}
```

The file is client-owned, canonical-write-lease protected, included in pending
snapshots and rollback, and absent from GM writable targets and player DTOs.

### Location entry

```json
{
  "locationId": "loc_4b0f6d6f0b074ba49ac520715e1984fa",
  "initialId": "locref_turn_12_black_ford",
  "materializationId": "mlocmat_turn_12_black_ford",
  "receiptId": "mlocrec_821ca66d5c7b48acb10ccf91d492118c",
  "realm": "mortal_world",
  "route": "current_scene_creation",
  "sourceTurn": 12,
  "sourceAuthorityKind": "turn_outcome",
  "sourceAuthorityId": "turn_12",
  "coordinatesAtCreation": { "x": 14, "y": -3, "z": 0 },
  "state": "active",
  "transitions": []
}
```

### Link entry

```json
{
  "linkId": "lnk_332543ee212f403f963d95ca890633bf",
  "initialId": "linkref_turn_12_ford_to_tower",
  "materializationId": "mlinkmat_turn_12_ford_to_tower",
  "receiptId": "mlinkrec_067615bd752c48869a98be004701e9b1",
  "realm": "mortal_world",
  "route": "world_map_link_creation",
  "sourceTurn": 12,
  "sourceAuthorityKind": "turn_outcome",
  "sourceAuthorityId": "turn_12",
  "sourceLocationId": "loc_4b0f6d6f0b074ba49ac520715e1984fa",
  "targetLocationId": "loc_7ced8725e923402d886b74cf95cfd04d",
  "state": "active",
  "transitions": []
}
```

Every active map entity has exactly one matching active index entry and receipt.
Every active index entry resolves to exactly one canonical entity. Retired
entries remain in the file and have no active canonical carrier.

## Location receipt

The client constructs and seals:

```json
{
  "schemaVersion": 1,
  "receiptId": "mlocrec_821ca66d5c7b48acb10ccf91d492118c",
  "locationId": "loc_4b0f6d6f0b074ba49ac520715e1984fa",
  "initialId": "locref_turn_12_black_ford",
  "materializationId": "mlocmat_turn_12_black_ford",
  "realm": "mortal_world",
  "route": "current_scene_creation",
  "sourceTurn": 12,
  "sourceAuthorityKind": "turn_outcome",
  "sourceAuthorityId": "turn_12",
  "seal": "client-computed-versioned-seal"
}
```

The seal covers every field above and the normalized accepted creation envelope.
The canonical map object and current projection carry the same immutable receipt
while selected. A narrow update cannot reseal an entity.

## Link receipt

The link receipt contains the same common evidence plus permanent
`sourceLocationId` and `targetLocationId`. Its seal covers both endpoints and the
accepted link envelope. Later access/discovery updates do not replace it.

## Bootstrap reservation

The client bootstrap scaffold may reserve:

- exact start and neighbor temporary location references;
- exact start-link temporary reference;
- permanent ID values or reservation tokens;
- exact coordinate constraints;
- one scaffold request/authority ID.

The GM copies only instructed temporary references and authors semantic
envelopes. Reserved permanent IDs, request/session state, and index entries
remain client-owned. Acceptance consumes reservations exactly once; failed
attempts do not settle them.

## Lifecycle transitions

Location transitions in #1513 are creation, narrow semantic update, discovery
advance, and current selection. There is no location retirement operation in
this checkpoint unless required solely for rollback of an uncommitted plan.

Link transitions are creation, narrow access/visibility update, and retirement.
Each settled transition appends an immutable client record with:

- `transitionId`, `kind`, and turn;
- exact entity ID;
- before/after lifecycle or discovery state as applicable;
- source authority and operation request evidence;
- endpoint IDs for link transitions.

The record is a closed object. Every transition contains exactly
`transitionId`, `kind`, `turn`, `entityId`, `beforeState`, `afterState`,
`sourceAuthorityKind`, `sourceAuthorityId`, `operationRef`,
`sourceLocationId`, and `targetLocationId`. Storage/threat child transitions
add the required exact `childId` and no other field. `beforeState` and
`afterState` are objects; `turn` is not earlier than the entry's creation turn;
the authority is exactly `turn_outcome:turn_<turn>`; and `operationRef` is an
exact non-empty command coordinate.

Location transition kinds are `location_update`, `location_discovery`, and
`current_selection`; their endpoint fields are null. Child kinds are
`storage_update`, `storage_removal`, `threat_addition`, `threat_update`,
`threat_removal`, and `threat_activity_completion`; both endpoint fields equal
the owning location ID. Link kinds are `link_update` and `link_retirement`;
their endpoints equal the immutable link-entry endpoints. Transition IDs are
globally unique across location and link history under both exact and
confusable matching.

Retrying already-settled transition evidence is rejected or returns an
idempotent no-op only when the exact current client request protocol already
guarantees that behavior; it never generates a second receipt or transition.

## Anti-replay behavior

The validator independently checks all available historical fields. Missing one
field does not suppress a match on another. Examples:

- historical `initialId` plus missing `materializationId` still fails closed;
- retired `linkId` plus new `materializationId` cannot recreate the link;
- a case/Unicode variant never becomes a repairable new entity;
- an existing permanent ID with a new envelope is a forbidden recreation;
- a historical materialization ID on another route is still replay.

Replay, index ambiguity, missing identity coordinates, and protected-state edits
produce an empty normal GM repair packet and stop before dispatch.

## Current projection coherence

When a location is selected, its current projection must contain the same
permanent identity, envelope, receipt, and shared semantic fields as the map
object. A projection never creates an index entry and cannot be used as
historical evidence if the map/index pair is absent or invalid.

## Prohibited readers and writers

- The GM never receives `location_identity_index.json` as a writable target.
- Generic state distribution never merges GM data into it.
- Player console/browser/map/news/locality responses never serialize the index,
  receipts, envelopes, seals, origin IDs, route/request evidence, or transition
  records.
- Repair packets may describe missing GM fields but never prescribe permanent
  identity, receipt, seal, index, or transition values.
