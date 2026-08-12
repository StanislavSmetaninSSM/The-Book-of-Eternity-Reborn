# Data Model: Complete Mortal Location Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

## 1. Authority map

| Surface | Owner | Durable purpose | GM writable |
| --- | --- | --- | --- |
| `game_state/world/world_map.json.locations[]` | Shared contract, client-normalized | Sole durable Mortal location semantics | Only through raw creation/update routes |
| `game_state/world/world_map.json.links[]` | Shared contract, client-normalized | Sole durable directed topology | Only through raw link lifecycle routes |
| `game_state/world/current_location.json` | Client projection plus scene operations | Selected canonical location mirror, weather/interactions/current chronology/storage contents | Only allowlisted raw current route fields |
| `game_state/world/location_identity_index.json` | Client | Permanent identity, receipt, origin history, lifecycle | Never |
| `game_state/control/mortal_bootstrap_scaffold.json` | Client | Reserved initial refs, IDs, coordinates, request evidence | Never |
| Raw `currentLocationData` | GM | One new selected location or exact existing movement | Yes, transient |
| Raw `worldMapUpdates` | GM | Remote creations, narrow updates, discovery and link lifecycle | Yes, transient |

The map is semantic authority. The current projection and identity index are not
alternative stores from which missing GM semantics may be invented.

## 2. Canonical world map root

```json
{
  "schemaVersion": 1,
  "realm": "mortal_world",
  "locations": [],
  "links": []
}
```

Invariants:

- all four fields exist and have exact types;
- no raw command wrapper survives normalization;
- every `locations[]` member is a receipt-bearing `MortalLocation`;
- every `links[]` member is a receipt-bearing `MortalLocationLink`;
- exact permanent IDs are unique;
- case, whitespace, and Unicode-normalization aliases are rejected even when
  exact strings differ;
- coordinates are unique across active locations;
- all active link endpoints resolve exactly once inside `locations[]`;
- hidden entities remain canonical even when omitted from player projections.

## 3. Raw Mortal location creation

```json
{
  "locationId": null,
  "initialId": "locref_turn_12_black_ford",
  "realm": "mortal_world",
  "name": "Чёрный брод",
  "displayName": "Чёрный брод",
  "purpose": "Опасная переправа через весеннюю реку",
  "description": "Холодная река пересекает разбитый тракт между двумя каменистыми берегами.",
  "image_prompt": "A dark fantasy river ford at dusk, cold water around black stones, broken road, no text",
  "locationType": "outdoor",
  "biome": "riverlands",
  "biomeDescription": "Каменистые берега и холодная талая вода.",
  "indoorType": null,
  "features": [],
  "region": "Северная марка",
  "parentLocationId": null,
  "parentInitialId": null,
  "coordinates": { "x": 14, "y": -3, "z": 0 },
  "discovery": {
    "tier": "visited",
    "audience": "player_known",
    "rumorSummary": null
  },
  "internalDifficulty": {},
  "externalDifficulty": {},
  "lastEventsDescription": "Первое каноническое состояние места.",
  "eventDescriptions": [],
  "factionControl": [],
  "actorBindings": [],
  "locationStorages": [],
  "activeThreats": [],
  "loreBindings": [],
  "customStates": [],
  "materialization": {}
}
```

Raw creation invariants:

- `locationId` exists and is JSON null;
- `initialId` is a unique exact non-empty string without leading/trailing whitespace;
- `realm` is exactly `mortal_world`;
- the GM must not send `materializationReceipt`, a seal, an identity-index entry,
  a permanent ID, or any client request/session evidence;
- `parentLocationId` and `parentInitialId` are mutually exclusive;
- current-scene creation uses `discovery.tier=visited` and
  `discovery.audience=player_known`;
- outdoor locations have non-empty `biome` and `biomeDescription` and null
  `indoorType`; indoor locations have non-empty `indoorType` and null outdoor
  biome fields;
- each governed collection is physically present even when empty by design.

## 4. Canonical Mortal location

The canonical object retains all accepted semantic fields above with these
identity changes:

```json
{
  "locationId": "loc_4b0f6d6f0b074ba49ac520715e1984fa",
  "realm": "mortal_world",
  "name": "Чёрный брод",
  "description": "Холодная река пересекает разбитый тракт между двумя каменистыми берегами.",
  "materialization": {
    "schemaVersion": 1,
    "materializationId": "mlocmat_turn_12_black_ford",
    "entityKind": "mortal_location",
    "realm": "mortal_world",
    "route": "current_scene_creation",
    "sourceTurn": 12,
    "sourceAuthority": { "kind": "turn_outcome", "authorityId": "turn_12" },
    "initialId": "locref_turn_12_black_ford",
    "state": "complete",
    "sections": { "presentation": { "disposition": "populated", "reason": null } }
  },
  "materializationReceipt": {
    "schemaVersion": 1,
    "receiptId": "mlocrec_821ca66d5c7b48acb10ccf91d492118c",
    "locationId": "loc_4b0f6d6f0b074ba49ac520715e1984fa",
    "initialId": "locref_turn_12_black_ford",
    "materializationId": "mlocmat_turn_12_black_ford",
    "route": "current_scene_creation",
    "sourceTurn": 12,
    "sourceAuthorityKind": "turn_outcome",
    "sourceAuthorityId": "turn_12",
    "seal": "v1:6f96d2a8bb3e4a86a95f479129ba8b39"
  }
}
```

Canonical invariants:

- top-level `initialId`, `parentInitialId`, route commands, and raw source
  selectors are removed;
- `locationId`, envelope, receipt, and seal match one exact active index entry;
- envelope and receipt never change during ordinary updates;
- mapped section values agree with their declared dispositions;
- canonical actor/faction/lore/threat/storage references are permanent or the
  accepted effective identity owned by that entity's current contract;
- a map storage contains metadata only and never `contents`.

## 5. Materialization envelope

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
    "factionControl": { "disposition": "empty_by_design", "reason": "Никто не удерживает переправу." },
    "actorBindings": { "disposition": "empty_by_design", "reason": "Постоянных обитателей нет." },
    "storageMetadata": { "disposition": "empty_by_design", "reason": "Оборудованных хранилищ нет." },
    "activeThreats": { "disposition": "populated", "reason": null },
    "loreBindings": { "disposition": "empty_by_design", "reason": "Сюжетных привязок пока нет." },
    "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния не требуются." },
    "topology": { "disposition": "populated", "reason": null }
  }
}
```

Allowed location routes are `current_scene_creation` and
`world_map_creation`. Bootstrap uses those ordinary routes and is recognized by
`sourceAuthority.kind=mortal_bootstrap_scaffold`, which must match an open
client scaffold reservation.

Every section key is required exactly once. `populated` requires the mapped
canonical surface to contain meaningful structured evidence.
`empty_by_design` requires a non-empty in-world reason and the mapped canonical
empty/null value. Presentation, physical, placement, discovery, difficulty, and
chronicle cannot be empty by design.

## 6. Discovery model

Allowed pairs:

| Tier | Audience | Player projection |
| --- | --- | --- |
| `hidden` | `gm_only` | Entire location/link absent |
| `rumored` | `player_known` | Safe rumor label/summary only; no exact coordinates, full description, hidden endpoints, or action |
| `discovered` | `player_known` | Permitted location details and visible topology |
| `visited` | `player_known` | Full permitted player-facing detail and revisit/current actions |

`rumorSummary` is required only for `rumored` and must be null otherwise.
Transitions are forward-only unless a separately tracked rule later authorizes
concealment. Selecting a current location moves it to `visited/player_known` in
the same accepted operation.

## 7. Cross-reference collections

### 7.1 Faction control

Each member contains an exact `factionId`, a closed control/claim role, optional
structured intensity or share, and player-facing description. A same-turn raw
member may use exactly one `initialFactionId`; normalization replaces it with
the accepted effective faction identity. Names do not bind authority.

### 7.2 Actor bindings

Each member contains an exact `actorId` and one role:
`resident`, `owner`, `staff`, `prisoner`, or `other`. A same-turn raw member may
use exactly one `initialActorId`. The composed actor authority must place the
actor at this exact location for physical roles. A narrative mention belongs in
description/lore rather than this collection.

### 7.3 Storage metadata

Each member has exact `storageId`, name, description, owner reference or null,
capacity metadata, access state, and optional structured custom semantics.
Storage IDs are unique within the location and reconcile exactly between the
map object and current projection. Only current projection members may add a
`contents` array, whose elements remain governed by item receipts/transitions.

### 7.4 Active threats

Each member contains one exact accepted threat identity or a complete
same-turn threat authority supported by the existing world-event contract,
plus location-specific role/status. Name-only threats are invalid.

### 7.5 Lore bindings

Each member has a closed `kind` (`codex`, `quest`, or `world_event`) and exactly
one matching permanent `codexEntryId`, `questId`, or `worldEventId`. Names and
file paths are not authority.

### 7.6 Custom states

Setting-specific structured state is allowed after recursive rejection of
client/protocol/repair fields. It cannot override any governed identity,
placement, discovery, topology, difficulty, or companion surface.

## 8. Raw and canonical link

Raw link:

```json
{
  "linkId": null,
  "initialId": "linkref_turn_12_ford_to_watchtower",
  "sourceLocationId": null,
  "sourceInitialId": "locref_turn_12_black_ford",
  "targetLocationId": "loc_existing_watchtower",
  "targetInitialId": null,
  "name": "Тропа к башне",
  "description": "Узкая тропа под обрывом.",
  "directionLabel": "на северо-восток",
  "linkType": "path",
  "travelMode": "foot",
  "access": { "state": "open", "reason": null, "requirements": [] },
  "discovery": { "tier": "discovered", "audience": "player_known", "rumorSummary": null },
  "materialization": {}
}
```

Canonical link replaces `initialId` and endpoint temporary fields with
permanent `linkId`, `sourceLocationId`, and `targetLocationId`, retains immutable
envelope, and adds `materializationReceipt` binding link ID and both endpoints.

Allowed link types include `road`, `path`, `passage`, `portal`, `one_way`,
`hidden_path`, `sealed_passage`, and `other`. Link type does not infer a reverse
edge. Access states are `open`, `conditional`, and `sealed`; conditional/sealed
states require a player-facing reason and structured requirements as applicable.

## 9. Current-location projection

The current root contains:

- every shared semantic field copied from exactly one canonical map location;
- that location's immutable envelope and receipt;
- `currentWeather` / normalized weather state supported by the existing scene contract;
- current interactions/opportunities supported by the existing scene contract;
- current chronology extension while the map stores the durable accepted chronicle;
- storage metadata identical to the map plus active `contents` under item authority;
- client-derived `knownExits` and `adjacencyMap` only if retained for existing UI/service compatibility.

Derived exit fields are never accepted from the GM and are rebuilt from exact
canonical links after every location/link/discovery change. Hidden or sealed
links are omitted or projected according to player visibility/access rules.

## 10. Identity index

```json
{
  "schemaVersion": 1,
  "realm": "mortal_world",
  "locationEntries": [],
  "linkEntries": []
}
```

Location entry fields:

- `locationId`, `initialId`, `materializationId`, `receiptId`;
- `sourceTurn`, `sourceAuthorityKind`, `sourceAuthorityId`, `route`;
- exact coordinate tuple at creation;
- `state`: `active` or `retired`;
- accepted/retired transition evidence.

Link entry fields:

- `linkId`, `initialId`, `materializationId`, `receiptId`;
- accepted source and target location IDs;
- source turn/authority/route;
- `state`: `active` or `retired`;
- transition evidence.

The index retains origin identities after retirement. Current and historical
identity sets reject exact reuse, case variants, leading/trailing whitespace,
and Unicode normalization/confusable variants. The GM never sees the index in a
repair target or player DTO.

## 11. Existing-state operations

### Movement selection

Raw current data for an existing destination contains exact `locationId` and
allowlisted current chronology/operational fields only. It cannot contain full
location semantics, coordinates, envelope, receipt, or name identity. The
client rebuilds the shared projection from the map.

### Narrow location update

An update has exact `locationId` plus only mutable fields: presentation text and
image guidance, chronicle, difficulty, discovery transition, faction/actor/lore/
threat/custom semantics through their governed shapes. Identity, realm,
original envelope/receipt, coordinates, parent, storage identity, and topology
cannot be smuggled through a location update.

### Link lifecycle

Create uses a full raw link. Update/reveal/seal/remove uses exact `linkId` and a
closed operation-specific patch. Removal retires the link/index entry and
rebuilds current derived exits; it does not retire endpoint locations.

## 12. Accepted-turn plan

`MortalLocationAcceptedTurnPlan` is an in-memory immutable result containing:

- route classification and exact raw carrier coordinates;
- pre-turn location/link/index snapshots;
- exact `initialId -> permanent ID` maps for new locations and links;
- planned receipts/index transitions;
- final canonical `world_map` and `current_location` objects;
- a field-aware set of governed companion rewrites;
- accepted same-turn current-storage coordinates for item route authority;
- touched paths and composed-state validation inputs;
- repair contexts for rejected raw candidates.

The plan performs no file writes. It is discarded on any issue. Random client
IDs are generated once per accepted plan and the same plan supplies all writes;
validation must not independently regenerate them.

## 13. State transitions

### Location

```text
absent --authorized complete creation--> active
active --narrow update/discovery/current selection--> active
active --retirement (future explicit contract only)--> retired
retired --creation/revival--> forbidden
```

### Link

```text
absent --authorized complete creation--> active
active --access/visibility update--> active
active --remove--> retired
retired --recreate/reuse origin--> forbidden
```

### Accepted turn

```text
raw candidate
  -> raw-valid plan
  -> composed-valid in-memory state
  -> leased writes
  -> post-normalization valid commit

any failure -> exact pre-turn before-images + no partial receipt/index/link/current state
```
