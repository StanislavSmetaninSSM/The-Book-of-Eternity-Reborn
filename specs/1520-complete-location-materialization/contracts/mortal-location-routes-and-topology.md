# Contract: Mortal Location Routes and Topology

**Feature**: [Complete Mortal Location Materialization](../spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

## Purpose

Define the only accepted raw routes for creating, selecting, updating, and
discovering Mortal locations and for creating/updating/removing their directed
links. Command carriers are transient; canonical state contains no route wrapper.

## Route table

| Operation | Raw carrier | Identity input | Canonical effect |
| --- | --- | --- | --- |
| New selected location | `currentLocationData` | null `locationId` + exact `initialId` | Add one map location, seal it, select it, build current projection |
| New remote location | `worldMapUpdates.newLocations[]` | null `locationId` + exact `initialId` | Add one map location without selecting/revealing beyond authored discovery |
| Bootstrap start/neighbor | same ordinary carriers + exact scaffold authority | reserved exact `initialId` | Same effects as ordinary creation; consume scaffold reservation |
| Move to existing location | `currentLocationData` | exact permanent `locationId` | Select canonical map location and rebuild current projection |
| Narrow semantic update | `worldMapUpdates.locationUpdates[]` | exact permanent `locationId` | Patch only closed mutable fields |
| Discovery advance | `worldMapUpdates.locationDiscoveryTransitions[]` | exact permanent `locationId` | Advance one closed discovery state |
| New link | `worldMapUpdates.newLinks[]` | null `linkId` + exact `initialId` | Resolve endpoints, assign ID, seal, add directed link |
| Link access/visibility update | `worldMapUpdates.linkUpdates[]` | exact permanent `linkId` | Patch closed access/discovery fields |
| Link removal | `worldMapUpdates.linkRemovals[]` | exact permanent `linkId` | Remove active link, retire index evidence, rebuild derived exits |

No other property, file, nested object, name, coordinate, or adjacency summary
authorizes a Mortal location or link operation.

## Current-scene creation

`currentLocationData` is a full creation only when `locationId` is explicit JSON
null and `initialId` is exact. It must contain the complete location and envelope
in one object. It may additionally contain current-scene operational fields and
storage contents, but those do not fill missing map semantic sections.

The same `initialId`, materialization identity, coordinates, or semantic object
must not occur in `newLocations[]`. The normalizer inserts exactly one canonical
map object and builds current projection from that object.

## Remote creation

Every `newLocations[]` member is independently complete and independently
repairable only when its raw coordinate is unambiguous. A remote location:

- may be hidden, rumored, discovered, or visited;
- does not become current;
- does not inherit current weather/interactions/storage contents;
- cannot be a partial placeholder for a future current scene;
- may participate in accepted same-turn parent or link references.

## Bootstrap route

Bootstrap writes neutral roots and one client-owned scaffold request. The first
accepted GM result must provide:

1. one complete `visited/player_known` start through `currentLocationData`;
2. either one complete neighbor through `newLocations[]` and one explicit link,
   or a narrative-only unresolved exit with no `initialId`, `linkId`, endpoint,
   coordinate reservation settlement, or canonical map entry.

The scaffold may constrain exact reserved references and coordinates. The GM
cannot substitute aliases. No bootstrap-specific reduced location schema exists.

## Movement to existing location

An existing move carries:

```json
{
  "locationId": "loc_exact_destination",
  "lastEventsDescription": "Что изменилось при прибытии",
  "currentWeather": {},
  "currentInteractions": []
}
```

Only the exact permanent ID and explicitly allowlisted operational/current
chronology fields are accepted. A name, ordinal, coordinates, `initialId`, full
description, placement, difficulty, envelope, receipt, or embedded exits makes
the operation invalid. Re-selecting the exact pre-turn current location is
allowed. A changed destination requires one accepted pre-turn link with exact
directed source=current and target=destination, `access.state=open`, empty
requirements, and non-hidden `player_known` discovery for both link and
destination. A reverse-only, hidden, conditional, or sealed link is not
traversal authority. This route never grants reachability itself.

Existing movement contains no `locationStorages` or `contents`. The client
copies same-location contents from validated pre-turn state. For a changed
selection it atomically parks non-empty source contents in the closed
client-owned offscreen state and hydrates destination contents into the new
current projection. Both physical representations bind the same exact
`location_storage(locationId, storageId)` carrier; movement does not create an
item transition or rewrite the item identity index.

## Narrow location update

An update is an object with exact permanent `locationId` and one or more fields
from the closed mutable catalog:

- `name`, `displayName`, `purpose`, `description`, `image_prompt`;
- `internalDifficulty`, `externalDifficulty`;
- `lastEventsDescription`;
- `factionControl`, `actorBindings`, `loreBindings`, `customStates` through
  their governed schemas;
- discovery only through the dedicated discovery transition form.

The update cannot contain permanent-ID aliases, `initialId`, realm, coordinates,
parent fields, storage identity/capacity, envelope, receipt, derived exits, or
link operations. `eventDescriptions` is client-managed history, while
`activeThreats` changes only through atomic threat commands. If later
requirements need coordinate/parent mutation, they require a separate tracked
contract rather than a wider patch.

## Discovery transition

```json
{
  "locationId": "loc_exact",
  "fromTier": "hidden",
  "toTier": "rumored",
  "toAudience": "player_known",
  "rumorSummary": "За болотами видели огни старой крепости.",
  "reason": "Игрок получил точный слух от картографа."
}
```

The exact pre-state must match `fromTier`. Allowed forward edges are:

```text
hidden -> rumored -> discovered -> visited
hidden -> discovered
hidden -> visited
rumored -> visited
discovered -> visited
```

No transition can reveal a link whose own discovery/access state remains hidden
or sealed. Moving into a location settles `visited/player_known` atomically.

## New link creation

Each link includes:

- null `linkId` and unique exact `initialId`;
- independent link materialization envelope;
- exactly one source selector and one target selector;
- name, description, `directionLabel`, `linkType`, `travelMode`;
- structured `access` state/reason/requirements;
- structured discovery tier/audience/rumor summary;
- optional structured custom states after internal-field rejection.

Endpoint resolution is field-aware:

```text
sourceLocationId -> one pre-turn or same-turn permanent canonical location
sourceInitialId  -> one accepted same-turn location creation
targetLocationId -> one pre-turn or same-turn permanent canonical location
targetInitialId  -> one accepted same-turn location creation
```

The planner rewrites temporary endpoint selectors to permanent IDs and removes
the temporary fields. It rejects missing, duplicate, self, cross-realm,
case-variant, Unicode-confusable, or ambiguous endpoints.

## Link lifecycle

Link update allows only access and discovery properties plus their player-facing
descriptions/requirements. It cannot change endpoints, identity, route,
envelope, receipt, or link type. A change of endpoints or link type requires
removal plus a genuinely new independently materialized link and cannot reuse
retired origin evidence.

Link removal requires exact `linkId`, exact expected endpoints or transition
precondition where provided, and accepted source authority. It removes only the
link, retires the index entry, and rebuilds current derived exits. It does not
delete either location or infer removal of a reverse link.

## Derived topology

`knownExits`, `adjacencyMap`, route lists, map edges, navigation choices, and
location detail exits are derived from `world_map.links[]`. They are never raw
GM authority.

A derived edge appears only when:

- the source/current location is visible to the player;
- the link is player-visible at its discovery tier;
- the target projection is allowed for its discovery tier;
- the access state permits a visible blocked/open representation;
- both endpoint IDs resolve exactly once.

One link creates one directed edge. A reverse edge requires a second accepted
link. A sealed visible link may be shown as blocked without revealing a hidden
target. A hidden link contributes no count, label, endpoint, or action.

## Parent and coordinate invariants

- Every active location has integer/numeric x/y/z values accepted by the current
  coordinate type; omission of z is invalid.
- Active coordinate tuples are unique.
- Parent references resolve exactly once to a Mortal location.
- Parent graphs are acyclic, and a location cannot parent itself.
- Coordinates and parent are placement, not identity.
- Ordinary updates cannot move/reparent a location.

## Governed storage and threat lifecycle

Storage and threat lifecycle commands are materialized by the same pure plan:

- every raw new-location object carries an empty `activeThreats` array; a
  same-turn threat is a separate `threatsToAdd[]` command keyed by the exact
  new-location `initialTargetLocationId`, never an embedded GM-selected ID;

- `storageUpdates[]` binds exact location/storage identity and maps only
  `newName`, `newDescription`, `newCapacity`, `newOwner`,
  `newAuthorizedUsers`, and `newHasFullAccess`, preserving item contents.
  Owner changes carry both access fields atomically, and authorization-list
  changes carry the synchronized access flag;
- `storagesToRemove[]` removes only an exact empty storage and fails closed when
  accepted item contents remain in either current or offscreen client authority;
- `threatsToAdd[]` accepts a complete null-ID threat for one exact existing or
  same-turn location, then assigns a client-owned permanent `threat_...` ID;
- `threatsToUpdate[]` deep-merges one exact non-terminal partial update;
- `threatsToRemove[]` removes one exact threat;
- `completeThreatActivities[]` requires exact name/ID plus a non-null pre-turn
  activity, appends one system-owned history entry, and clears the activity.

Conflicting commands on the same storage/threat, wrong-case or confusable IDs,
unknown targets, unknown command names, and removal of a non-empty storage fail
closed. Every accepted command updates canonical map, current projection when
present, and location-index transition evidence in one transaction. Immutable
materialization section dispositions remain creation evidence; later accepted
storage/threat count changes do not rewrite the envelope or receipt seal.

## Governed reference rewrites

The accepted-turn planner rewrites only an explicit catalog, including:

- link endpoint temporary fields;
- location parent temporary field;
- NPC `initialLocationId` to exact canonical `currentLocationId`;
- exact location IDs in governed faction, actor, threat, lore, storage, movement,
  and current-selection fields;
- same-turn current-location storage coordinates consumed by item route
  authority.

It never rewrites arbitrary strings, descriptions, names, prompts, journal text,
custom-state values, file paths, or afterlife fields.

## Item route integration

A raw item in a new current location's storage is accepted only when:

- the current location creation itself is raw-valid and uniquely owned;
- the storage metadata is exact, complete, unique in that location, and declared
  populated;
- the item route authority ID is the exact
  `<initialLocationId>:<storageId>` coordinate and binds that accepted temporary
  reference to the same storage;
- item creation passes the independent #1511 envelope/route contract.

After location normalization the transition destination binds the permanent
location ID while retaining the accepted same-turn source coordinate as route
evidence. The item normalizer owns item ID, receipt, seal, index, and contents
mutation.
Remote map locations cannot contain item contents.

Persistent accepted contents for a non-current location live only in the
closed client-owned offscreen state. Every entry resolves one exact active map
storage, is non-empty, and cannot use the selected current location coordinate.
The GM never authors or repairs that state. Raw validation compares it with the
validated pre-turn snapshot; canonical validation combines it with the current
projection and item index, requiring exactly one physical occurrence per item.

## Removal of obsolete behavior

The current-schema implementation removes positive support for:

- receipt-less canonical locations or links;
- `knownLocations`, durable `newLocations`, `newLinks`, or `locationUpdates` wrappers;
- name/slug/case-insensitive location identity;
- target coordinates as link identity;
- missing z compatibility;
- GM-authored `knownExits` or `adjacencyMap`;
- full existing-location resend on movement;
- automatic reverse links;
- runtime promotion of development fixtures.
