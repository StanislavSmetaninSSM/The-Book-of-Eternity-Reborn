# Contract: Mortal Location Materialization Envelope

**Feature**: [Complete Mortal Location Materialization](../spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

## Purpose

Define the complete GM-authored evidence required when a durable Mortal World
location or topology link first enters canonical state. This contract applies
only to first creation. Existing-state updates never resend or replace it.

## Ownership

The GM owns:

- the complete location or link semantics;
- null permanent identity at creation;
- exact same-turn `initialId`;
- independent `materializationId`;
- route, source turn, source authority, and section dispositions.

The client owns:

- permanent `locationId` / `linkId`;
- `materializationReceipt`, receipt ID, and seal;
- identity-index records and transition history;
- derived topology summaries and current projection reconciliation.

Any GM-authored client field is a validation error and cannot be repaired by
asking the GM to choose a different client value.

## Location envelope

Required exact shape:

```json
{
  "schemaVersion": 1,
  "materializationId": "mlocmat_turn_12_black_ford",
  "entityKind": "mortal_location",
  "realm": "mortal_world",
  "route": "current_scene_creation",
  "sourceTurn": 12,
  "sourceAuthority": {
    "kind": "turn_outcome",
    "authorityId": "turn_12"
  },
  "initialId": "locref_turn_12_black_ford",
  "state": "complete",
  "sections": {
    "presentation": { "disposition": "populated", "reason": null },
    "physical": { "disposition": "populated", "reason": null },
    "placement": { "disposition": "populated", "reason": null },
    "discovery": { "disposition": "populated", "reason": null },
    "difficulty": { "disposition": "populated", "reason": null },
    "chronicle": { "disposition": "populated", "reason": null },
    "factionControl": { "disposition": "empty_by_design", "reason": "Ни одна фракция не удерживает это место." },
    "actorBindings": { "disposition": "empty_by_design", "reason": "Постоянных обитателей здесь нет." },
    "storageMetadata": { "disposition": "empty_by_design", "reason": "Оборудованных хранилищ здесь нет." },
    "activeThreats": { "disposition": "empty_by_design", "reason": "Постоянной активной угрозы здесь нет." },
    "loreBindings": { "disposition": "empty_by_design", "reason": "Сюжетные и справочные привязки пока не требуются." },
    "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния места не требуются." },
    "topology": { "disposition": "populated", "reason": null }
  }
}
```

Closed values:

- `schemaVersion`: `1`;
- `entityKind`: `mortal_location`;
- `realm`: `mortal_world`;
- `route`: `current_scene_creation` or `world_map_creation`;
- `state`: `complete`;
- disposition: `populated` or `empty_by_design`.

Envelope/root agreement:

- envelope `initialId` equals root `initialId` exactly;
- route matches the only raw carrier that contains the candidate;
- source turn matches the accepted turn;
- ordinary routes require `sourceAuthority.kind=turn_outcome` and the exact
  turn authority ID;
- a bootstrap-reserved candidate still uses the ordinary carrier route and
  requires `sourceAuthority.kind=mortal_bootstrap_scaffold` plus the exact open
  scaffold request ID;
- `materializationId` is independent from `initialId`, permanent ID, receipt ID,
  source authority ID, and any actor/faction/item identity.

## Section mapping

| Envelope section | Canonical evidence |
| --- | --- |
| `presentation` | `name`, `displayName`, `purpose`, full `description`, English `image_prompt` |
| `physical` | `locationType`; outdoor biome fields or indoor type; `features[]` |
| `placement` | `region`, optional parent selector, exact x/y/z coordinates |
| `discovery` | valid discovery tier/audience pair and rumor summary rule |
| `difficulty` | complete `internalDifficulty` and `externalDifficulty` profiles |
| `chronicle` | non-empty `lastEventsDescription`, physical `eventDescriptions[]` |
| `factionControl` | complete `factionControl[]` or exact empty array |
| `actorBindings` | complete `actorBindings[]` or exact empty array |
| `storageMetadata` | complete `locationStorages[]` metadata or exact empty array |
| `activeThreats` | complete `activeThreats[]` or exact empty array |
| `loreBindings` | complete `loreBindings[]` or exact empty array |
| `customStates` | structured `customStates[]` or exact empty array |
| `topology` | at least one accepted link or an exact empty/no-link disposition |

Rules:

- `populated` requires meaningful structured evidence; a placeholder string or
  an array of empty objects is not meaningful.
- `empty_by_design` requires a non-empty in-world `reason` and the exact physical
  empty/null canonical surface.
- presentation, physical, placement, discovery, difficulty, and chronicle are
  always populated.
- topology may be empty only with an explicit isolated/sealed/non-topological
  reason and no fabricated link identity.
- a narrative-only possible exit is prose, not populated topology.
- section dispositions attest to the complete accepted creation payload. They
  are immutable historical evidence, not live counters. Later accepted atomic
  storage/threat lifecycle commands may change `locationStorages[]` or
  `activeThreats[]` without rewriting the envelope, receipt, or seal.
- storage `contents[]` are never map semantics. The selected current projection
  and closed client-owned offscreen state physically carry them under the item
  contract; existing movement never resubmits them.
- raw creation still must agree with every declared disposition at the moment
  of creation; only already-sealed canonical state uses the historical rule.

## Link envelope

Every new link has an independent envelope with the same common fields and:

```json
{
  "schemaVersion": 1,
  "materializationId": "mlinkmat_turn_12_ford_to_tower",
  "entityKind": "mortal_location_link",
  "realm": "mortal_world",
  "route": "world_map_link_creation",
  "sourceTurn": 12,
  "sourceAuthority": {
    "kind": "turn_outcome",
    "authorityId": "turn_12"
  },
  "initialId": "linkref_turn_12_ford_to_tower",
  "state": "complete",
  "sections": {
    "endpoints": { "disposition": "populated", "reason": null },
    "presentation": { "disposition": "populated", "reason": null },
    "traversal": { "disposition": "populated", "reason": null },
    "access": { "disposition": "populated", "reason": null },
    "discovery": { "disposition": "populated", "reason": null },
    "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния пути не требуются." }
  }
}
```

The link root supplies exactly one selector for each endpoint:

- `sourceLocationId` xor `sourceInitialId`;
- `targetLocationId` xor `targetInitialId`.

Link endpoints, materialization identity, and temporary identity must not equal
one another. A self-link is allowed only if an explicit future contract enables
it; #1513 rejects it.

## Required validation failures

Validation rejects:

- receipt-less canonical locations or links;
- missing, duplicate, unknown, or mistyped envelope fields;
- whitespace, case, or Unicode-confusable identity variants;
- source turn/authority/route mismatch;
- the same location candidate in both creation carriers;
- client-owned permanent identity, receipt, seal, index, or request/session data;
- a section marked populated without meaningful evidence;
- a section marked empty without a physical empty value and reason;
- a full existing location/link resend disguised as creation or update;
- a link whose endpoint cannot resolve exactly once.

## Immutability

After acceptance, the complete root envelope is immutable byte-for-semantic-byte.
Ordinary updates may change only their explicit semantic allowlist and must not
replace, add aliases to, or remove the envelope. A differing envelope or receipt
is an authority violation and must roll back before repair dispatch if the
repair would require editing a client-owned field.
