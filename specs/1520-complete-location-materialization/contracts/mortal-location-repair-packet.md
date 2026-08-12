# Contract: Mortal Location Repair Packet

**Feature**: [Complete Mortal Location Materialization](../spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

## Purpose

Give the GM one bounded, exact, GM-owned correction task for a malformed Mortal
location/link command without exposing or delegating client identity authority.
Repair is attempted only after the accepted turn has been restored to its
validated pre-turn baseline.

## Packet eligibility

A normal GM repair packet is emitted only when all are true:

1. The issue belongs to the Mortal location materialization phase.
2. Exactly one raw creation/update/movement/discovery/link carrier is identified.
3. Its exact temporary or permanent identity is present and unambiguous.
4. No active/retired history, case/whitespace/Unicode alias, or duplicate carrier
   makes target ownership ambiguous.
5. The requested correction changes only GM-owned raw semantic fields.
6. The raw target paths are inside the allowlisted Mortal location command files.
7. Pre-turn rollback has succeeded and no canonical partial write remains.

If any condition fails, the repair packet list is empty and the turn stops
before GM dispatch.

## Packet shape

The existing validation repair harness envelope is reused with a location-specific
payload:

```json
{
  "kind": "mortal_location_materialization_repair",
  "priority": "blocking",
  "title": "Исправить один пакет материализации локации",
  "actor": "mortal_location:new:locref_turn_12_black_ford",
  "transitionClass": "current_scene_creation",
  "targetFiles": ["game_state/world/current_location.json"],
  "rawCarrier": "currentLocationData",
  "rawCoordinate": "currentLocationData",
  "expectedAuthority": {
    "realm": "mortal_world",
    "route": "current_scene_creation",
    "sourceTurn": 12
  },
  "actualEvidence": [],
  "missingFields": [],
  "invalidFields": [],
  "conflicts": [],
  "requiredCompanionTargets": [],
  "safeCorrectionRules": [],
  "steps": [],
  "doNotDo": []
}
```

The implementation may reuse existing packet record property names, but must
preserve these semantics and deterministic ordering.

## Exact actor coordinates

Allowed location actors:

- `mortal_location:new:<initialId>`;
- `mortal_location:existing:<locationId>`;
- `mortal_location_link:new:<initialId>`;
- `mortal_location_link:existing:<linkId>`.

If the corresponding identity field is absent, malformed, or ambiguous, the
diagnostic actor is `mortal_location:unknown` or
`mortal_location_link:unknown`; it is not actionable and triggers fail-closed
rollback. Literal valid identity value `unknown` remains distinguishable from a
missing identity.

Array targets include an exact raw coordinate such as
`worldMapUpdates.newLocations[2]` for diagnostics. Array ordinal alone is never
identity and cannot authorize editing a different candidate after state changes.

## Repairable categories

Examples of bounded GM-owned repair:

- a missing required semantic field;
- a missing governed section disposition or empty-by-design reason;
- an outdoor/indoor physical-shape mismatch;
- incomplete difficulty or discovery shape;
- an exact dangling same-turn reference where the intended raw carrier remains
  uniquely identified;
- invalid parent cycle evidence that can be corrected in one named raw field;
- a duplicate coordinate where one exact target must choose a new coordinate;
- an invalid link direction/access/discovery field;
- a full existing-location resend that can be reduced to its exact narrow route.

The packet names the invalid evidence and permitted GM-owned correction. It does
not invent replacement prose or mechanics.

## Fail-closed categories

No normal GM packet is emitted for:

- missing, duplicate, replayed, or confusable `initialId` or
  `materializationId`;
- duplicate ownership across current and remote creation routes;
- any attempt to author permanent IDs, receipts, seals, identity index, client
  request/session/reservation state, or transition evidence;
- malformed or ambiguous canonical index/map ownership;
- receipt/index/envelope mismatch in an already accepted entity;
- a historical origin match on any available field, even if another origin
  field is absent;
- ambiguous cross-entity target resolution;
- a write-lease, rollback, before-image, or post-validation failure;
- any repair requiring item ID/receipt/transition changes;
- any afterlife location surface.

These cases receive an operator-facing path-bound diagnostic outside player
output. They cannot reach the GM prompt as a normal repair task.

## Target boundaries

Creation repair may target only the exact raw `currentLocationData`, one
`worldMapUpdates.newLocations[]` member, or one `newLinks[]` member. Existing
operations may target only one exact location/link lifecycle member.

The packet never targets:

- canonical `world_map.locations[]` or `links[]` directly;
- `current_location` shared projection fields after normalization;
- `location_identity_index.json`;
- pending snapshot or rollback files;
- actor/faction/item/lore/threat canonical state unless a separate issue from
  that owning contract is independently actionable;
- documentation or bootstrap scaffold client fields.

Cross-contract issues are grouped by owner. Location repair can say that an
exact actor/faction/lore/threat/storage reference is invalid; it cannot repair
the referenced entity. Item materialization issues remain in item packets.

## Safe correction rules

Every actionable packet includes rules equivalent to:

- preserve the exact route and candidate identity;
- edit only named raw GM-owned fields;
- keep `locationId`/`linkId` null for creation;
- retain one unique exact `initialId` and independent materialization ID;
- do not author a receipt, seal, permanent ID, index entry, transition, request,
  session, or file path;
- do not duplicate the candidate into another route;
- do not add `knownExits`, `adjacencyMap`, reverse links, or name/coordinate aliases;
- do not edit item identity/content to solve location metadata errors;
- resubmit one complete coherent raw package.

## Retry and idempotence

Repair starts from the restored pre-turn snapshot, not partially normalized
files. A corrected retry runs the complete raw validation and accepted-turn
planner again. No failed attempt may reserve or settle a permanent ID, receipt,
index entry, discovery transition, or link transition. Once a transition has
settled, replay is rejected rather than repaired as another creation.

## Privacy

Repair packets are GM/operator-only. Player-facing console/browser errors use a
generic in-world safe failure and never echo:

- raw paths or coordinates;
- envelope/receipt/index/seal fields;
- permanent or temporary internal IDs;
- repair kinds, validation issue codes, expected/actual authority, or agent
  instructions.

Recursive map/location/quest/news DTO projection must suppress a complete repair
packet object even when it is nested beneath a setting-specific semantic field.

## Required test matrix

Tests cover:

- one valid bounded packet for current creation, remote creation, narrow update,
  discovery, new link, and link update;
- deterministic missing/invalid section lists;
- exact raw target and no unrelated target files;
- replay/confusable/duplicate/unknown/client-field/index/seal failures producing
  no packet and stopping before GM dispatch;
- location issue not absorbing an item or actor repair;
- byte-exact rollback before retry;
- no packet/internal vocabulary in console or browser player output.
