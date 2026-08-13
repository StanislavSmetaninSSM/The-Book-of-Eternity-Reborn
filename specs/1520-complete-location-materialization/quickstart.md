# Quickstart: Complete Mortal Location Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

This quickstart is an implementation and authoring reference. The exact
production JSON will be synchronized into Rule 20, Example 20, CLI guidance,
daemon reminders, fixtures, and source guards during implementation.

## 1. Mental model

- The GM creates complete location/link semantics once.
- The client assigns permanent IDs, receipts, seals, and identity-index entries.
- `world_map.locations[]` owns every durable Mortal location.
- `world_map.links[]` owns every directed route.
- `current_location.json` mirrors one accepted map location and adds current
  weather/interactions/chronology/storage contents.
- `location_storage_contents.json` is closed client-owned state for non-empty
  storage contents at non-current exact location/storage coordinates; it is not
  a GM command, repair target, or player DTO.
- Names and coordinates never identify a location or link.
- `knownExits` and `adjacencyMap` are derived, never authored.
- Exact destination identity does not authorize movement: a changed selection
  needs one exact open, requirement-free, player-known/non-hidden directed link
  from the pre-turn current location.
- Storage/threat children change only through their six governed commands;
  section dispositions remain immutable creation evidence.
- Receipt-less canonical data is invalid; no old-save promotion exists.

## 2. Fresh canonical roots

Before the first accepted Mortal scene, `game_state/world/world_map.json` is:

```json
{
  "schemaVersion": 1,
  "realm": "mortal_world",
  "locations": [],
  "links": []
}
```

`game_state/world/current_location.json` is:

```json
{
  "schemaVersion": 1,
  "realm": "mortal_world",
  "locationId": null,
  "state": "pending_materialization"
}
```

`game_state/world/location_identity_index.json` is:

```json
{
  "schemaVersion": 1,
  "realm": "mortal_world",
  "locationEntries": [],
  "linkEntries": []
}
```

The offscreen carrier may be absent as the legacy empty baseline. Once first
published, `game_state/world/location_storage_contents.json` uses:

```json
{
  "schemaVersion": 1,
  "entries": []
}
```

Only the client writes it. Every persisted entry has exact `locationId`, exact
`storageId`, and non-empty `contents[]`; the selected current location never
also appears there.

The bootstrap scaffold separately reserves the exact start/neighbor/link
temporary references and coordinates. The GM must not edit the index or copy
reserved permanent IDs into output.

## 3. Worked example A: visited start, neighbor, and explicit link

Assume the scaffold authorizes:

```text
request: mortal_bootstrap_turn_1
start ref: locref_life_7_start
neighbor ref: locref_life_7_nearby
link ref: linkref_life_7_start_to_nearby
start coordinates: (0, 0, 0)
neighbor coordinates: (1, 0, 0)
```

The GM sends one complete start through `currentLocationData`:

```json
{
  "currentLocationData": {
    "locationId": null,
    "initialId": "locref_life_7_start",
    "realm": "mortal_world",
    "name": "Двор постоялого дома",
    "displayName": "Двор старого постоялого дома",
    "purpose": "Первая безопасная точка новой смертной жизни",
    "description": "Каменный двор окружён конюшней, кухней и низкой стеной. За воротами начинается тракт.",
    "image_prompt": "A grounded dark fantasy inn courtyard before dawn, wet cobbles, timber stable, low stone wall, no text",
    "locationType": "outdoor",
    "biome": "settled_lowlands",
    "biomeDescription": "Обжитая низина у торгового тракта.",
    "indoorType": null,
    "features": ["каменный колодец", "ворота на тракт", "крытая конюшня"],
    "region": "Приграничье Этернии",
    "parentLocationId": null,
    "parentInitialId": null,
    "coordinates": { "x": 0, "y": 0, "z": 0 },
    "discovery": { "tier": "visited", "audience": "player_known", "rumorSummary": null },
    "internalDifficulty": { "danger": "low", "recommendedLevel": 1, "description": "Двор охраняется хозяевами." },
    "externalDifficulty": { "danger": "low", "recommendedLevel": 1, "description": "У ворот начинается спокойный участок тракта." },
    "lastEventsDescription": "Герой очнулся до рассвета и впервые осмотрел двор.",
    "eventDescriptions": [],
    "factionControl": [],
    "actorBindings": [],
    "locationStorages": [
      {
        "storageId": "storage_inn_guest_chest",
        "name": "Гостевой сундук",
        "description": "Запираемый сундук у комнаты героя.",
        "image_prompt": "A sturdy dark fantasy guest chest beside an inn room, no text",
        "capacity": 12,
        "volume": 3.5,
        "owner": null,
        "authorizedUsers": [],
        "hasFullAccess": true,
        "contents": []
      }
    ],
    "activeThreats": [],
    "loreBindings": [],
    "customStates": [],
    "currentWeather": { "summary": "Холодная морось", "visibility": "normal" },
    "currentInteractions": [],
    "materialization": {
      "schemaVersion": 1,
      "materializationId": "mlocmat_life_7_start",
      "entityKind": "mortal_location",
      "realm": "mortal_world",
      "route": "current_scene_creation",
      "sourceTurn": 1,
      "sourceAuthority": { "kind": "mortal_bootstrap_scaffold", "authorityId": "mortal_bootstrap_turn_1" },
      "initialId": "locref_life_7_start",
      "state": "complete",
      "sections": {
        "presentation": { "disposition": "populated", "reason": null },
        "physical": { "disposition": "populated", "reason": null },
        "placement": { "disposition": "populated", "reason": null },
        "discovery": { "disposition": "populated", "reason": null },
        "difficulty": { "disposition": "populated", "reason": null },
        "chronicle": { "disposition": "populated", "reason": null },
        "factionControl": { "disposition": "empty_by_design", "reason": "Двор не является территорией отдельной фракции." },
        "actorBindings": { "disposition": "empty_by_design", "reason": "Постоянные акторы будут материализованы отдельно." },
        "storageMetadata": { "disposition": "populated", "reason": null },
        "activeThreats": { "disposition": "empty_by_design", "reason": "Непосредственной угрозы во дворе нет." },
        "loreBindings": { "disposition": "empty_by_design", "reason": "Сюжетная привязка пока не требуется." },
        "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния не нужны." },
        "topology": { "disposition": "populated", "reason": null }
      }
    }
  }
}
```

The GM sends the complete neighbor and explicit directed link through
`worldMapUpdates` in the same turn:

```json
{
  "worldMapUpdates": {
    "newLocations": [
      {
        "locationId": null,
        "initialId": "locref_life_7_nearby",
        "realm": "mortal_world",
        "name": "Развилка у старого дуба",
        "displayName": "Развилка у старого дуба",
        "purpose": "Первая точка выбора пути",
        "description": "Тракт расходится у дерева, пережившего пожар.",
        "image_prompt": "A dark fantasy crossroads beside a fire-scarred ancient oak at dawn, no text",
        "locationType": "outdoor",
        "biome": "settled_lowlands",
        "biomeDescription": "Поля и перелески вдоль старого тракта.",
        "indoorType": null,
        "features": ["обгоревший дуб", "два дорожных указателя"],
        "region": "Приграничье Этернии",
        "parentLocationId": null,
        "parentInitialId": null,
        "coordinates": { "x": 1, "y": 0, "z": 0 },
        "discovery": { "tier": "discovered", "audience": "player_known", "rumorSummary": null },
        "internalDifficulty": { "danger": "low", "recommendedLevel": 1, "description": "На развилке нет постоянной угрозы." },
        "externalDifficulty": { "danger": "moderate", "recommendedLevel": 1, "description": "За развилкой тракт становится менее безопасным." },
        "lastEventsDescription": "Развилка ещё не посещена героем.",
        "eventDescriptions": [],
        "factionControl": [],
        "actorBindings": [],
        "locationStorages": [],
        "activeThreats": [],
        "loreBindings": [],
        "customStates": [],
        "materialization": {
          "schemaVersion": 1,
          "materializationId": "mlocmat_life_7_nearby",
          "entityKind": "mortal_location",
          "realm": "mortal_world",
          "route": "world_map_creation",
          "sourceTurn": 1,
          "sourceAuthority": { "kind": "mortal_bootstrap_scaffold", "authorityId": "mortal_bootstrap_turn_1" },
          "initialId": "locref_life_7_nearby",
          "state": "complete",
          "sections": {
            "presentation": { "disposition": "populated", "reason": null },
            "physical": { "disposition": "populated", "reason": null },
            "placement": { "disposition": "populated", "reason": null },
            "discovery": { "disposition": "populated", "reason": null },
            "difficulty": { "disposition": "populated", "reason": null },
            "chronicle": { "disposition": "populated", "reason": null },
            "factionControl": { "disposition": "empty_by_design", "reason": "Развилку никто не удерживает." },
            "actorBindings": { "disposition": "empty_by_design", "reason": "Постоянных обитателей нет." },
            "storageMetadata": { "disposition": "empty_by_design", "reason": "Хранилищ нет." },
            "activeThreats": { "disposition": "empty_by_design", "reason": "Постоянной угрозы нет." },
            "loreBindings": { "disposition": "empty_by_design", "reason": "Сюжетных привязок пока нет." },
            "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния не нужны." },
            "topology": { "disposition": "populated", "reason": null }
          }
        }
      }
    ],
    "newLinks": [
      {
        "linkId": null,
        "initialId": "linkref_life_7_start_to_nearby",
        "sourceLocationId": null,
        "sourceInitialId": "locref_life_7_start",
        "targetLocationId": null,
        "targetInitialId": "locref_life_7_nearby",
        "name": "Тракт к развилке",
        "description": "Короткий участок утоптанной дороги.",
        "directionLabel": "на восток",
        "linkType": "road",
        "travelMode": "foot",
        "access": { "state": "open", "reason": null, "requirements": [] },
        "discovery": { "tier": "discovered", "audience": "player_known", "rumorSummary": null },
        "customStates": [],
        "materialization": {
          "schemaVersion": 1,
          "materializationId": "mlinkmat_life_7_start_to_nearby",
          "entityKind": "mortal_location_link",
          "realm": "mortal_world",
          "route": "world_map_link_creation",
          "sourceTurn": 1,
          "sourceAuthority": { "kind": "mortal_bootstrap_scaffold", "authorityId": "mortal_bootstrap_turn_1" },
          "initialId": "linkref_life_7_start_to_nearby",
          "state": "complete",
          "sections": {
            "endpoints": { "disposition": "populated", "reason": null },
            "presentation": { "disposition": "populated", "reason": null },
            "traversal": { "disposition": "populated", "reason": null },
            "access": { "disposition": "populated", "reason": null },
            "discovery": { "disposition": "populated", "reason": null },
            "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния пути не нужны." }
          }
        }
      }
    ]
  }
}
```

The client assigns permanent IDs, seals receipts, writes both map locations and
the directed link, consumes scaffold reservations, and builds the current
projection. The GM does not guess or echo assigned IDs.

If the neighbor is intentionally not yet materialized, omit both its location
object and the link object and narrate only an unresolved possible road. Do not
invent a temporary reference for that prose.

## 4. Worked example B: hidden remote location, one-way route, reveal

Create a hidden remote location through `newLocations[]` and a hidden one-way
link from an existing exact source ID:

```json
{
  "worldMapUpdates": {
    "newLocations": [
      {
        "locationId": null,
        "initialId": "locref_turn_8_drowned_archive",
        "realm": "mortal_world",
        "name": "Затопленный архив",
        "displayName": "Затопленный архив под мельницей",
        "purpose": "Скрытое хранилище документов",
        "description": "Подземные своды наполовину заполнены чёрной водой.",
        "image_prompt": "A submerged dark fantasy archive under a ruined mill, black water, candle reflections, no text",
        "locationType": "indoor",
        "biome": null,
        "biomeDescription": null,
        "indoorType": "subterranean_archive",
        "features": ["затопленные стеллажи", "односторонний спуск"],
        "region": "Низовья Тарны",
        "parentLocationId": "loc_existing_ruined_mill",
        "parentInitialId": null,
        "coordinates": { "x": 22, "y": 9, "z": -1 },
        "discovery": { "tier": "hidden", "audience": "gm_only", "rumorSummary": null },
        "internalDifficulty": { "danger": "high", "recommendedLevel": 5, "description": "Вода скрывает ловушки." },
        "externalDifficulty": { "danger": "moderate", "recommendedLevel": 4, "description": "Вход находится под охраной контрабандистов." },
        "lastEventsDescription": "Архив подготовлен, но ещё неизвестен герою.",
        "eventDescriptions": [],
        "factionControl": [],
        "actorBindings": [],
        "locationStorages": [],
        "activeThreats": [],
        "loreBindings": [],
        "customStates": [],
        "materialization": {
          "schemaVersion": 1,
          "materializationId": "mlocmat_turn_8_drowned_archive",
          "entityKind": "mortal_location",
          "realm": "mortal_world",
          "route": "world_map_creation",
          "sourceTurn": 8,
          "sourceAuthority": { "kind": "turn_outcome", "authorityId": "turn_8" },
          "initialId": "locref_turn_8_drowned_archive",
          "state": "complete",
          "sections": {
            "presentation": { "disposition": "populated", "reason": null },
            "physical": { "disposition": "populated", "reason": null },
            "placement": { "disposition": "populated", "reason": null },
            "discovery": { "disposition": "populated", "reason": null },
            "difficulty": { "disposition": "populated", "reason": null },
            "chronicle": { "disposition": "populated", "reason": null },
            "factionControl": { "disposition": "empty_by_design", "reason": "Ни одна фракция не удерживает затопленные своды." },
            "actorBindings": { "disposition": "empty_by_design", "reason": "Постоянных обитателей в архиве нет." },
            "storageMetadata": { "disposition": "empty_by_design", "reason": "Пригодных для использования хранилищ нет." },
            "activeThreats": { "disposition": "empty_by_design", "reason": "Конкретная активная угроза пока не материализована." },
            "loreBindings": { "disposition": "empty_by_design", "reason": "Точные сюжетные привязки появятся отдельным событием." },
            "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния места не требуются." },
            "topology": { "disposition": "populated", "reason": null }
          }
        }
      }
    ],
    "newLinks": [
      {
        "linkId": null,
        "initialId": "linkref_turn_8_mill_drop_to_archive",
        "sourceLocationId": "loc_existing_ruined_mill",
        "sourceInitialId": null,
        "targetLocationId": null,
        "targetInitialId": "locref_turn_8_drowned_archive",
        "name": "Сброс через колодец",
        "description": "Спуск возможен только вниз; обратный механизм разрушен.",
        "directionLabel": "вниз",
        "linkType": "one_way",
        "travelMode": "climb",
        "access": { "state": "conditional", "reason": "Нужна прочная верёвка.", "requirements": [{ "kind": "item_capability", "value": "rope" }] },
        "discovery": { "tier": "hidden", "audience": "gm_only", "rumorSummary": null },
        "customStates": [],
        "materialization": {
          "schemaVersion": 1,
          "materializationId": "mlinkmat_turn_8_mill_drop_to_archive",
          "entityKind": "mortal_location_link",
          "realm": "mortal_world",
          "route": "world_map_link_creation",
          "sourceTurn": 8,
          "sourceAuthority": { "kind": "turn_outcome", "authorityId": "turn_8" },
          "initialId": "linkref_turn_8_mill_drop_to_archive",
          "state": "complete",
          "sections": {
            "endpoints": { "disposition": "populated", "reason": null },
            "presentation": { "disposition": "populated", "reason": null },
            "traversal": { "disposition": "populated", "reason": null },
            "access": { "disposition": "populated", "reason": null },
            "discovery": { "disposition": "populated", "reason": null },
            "customStates": { "disposition": "empty_by_design", "reason": "Особые состояния прохода не требуются." }
          }
        }
      }
    ]
  }
}
```

Before reveal, neither entity appears in player rows, counts, map edges, details,
news joins, or actions. The client does not infer a link back to the mill.

After the player finds a water-damaged plan, use exact permanent IDs returned by
accepted canonical state in structured discovery transitions:

```json
{
  "worldMapUpdates": {
    "locationDiscoveryTransitions": [
      {
        "locationId": "loc_2856832894634f199dcc902622426db9",
        "fromTier": "hidden",
        "toTier": "rumored",
        "toAudience": "player_known",
        "rumorSummary": "Под старой мельницей будто бы сохранились затопленные своды.",
        "reason": "Герой изучил план водоотводов."
      }
    ],
    "linkUpdates": [
      {
        "linkId": "lnk_bf2321db6e18492c807a24d26c6f636d",
        "discovery": {
          "fromTier": "hidden",
          "toTier": "rumored",
          "toAudience": "player_known",
          "rumorSummary": "В колодце мельницы может быть скрытый спуск."
        }
      }
    ]
  }
}
```

The rumor projection shows only the safe summaries and no precise coordinate,
full archive description, endpoint ID, or actionable path.

## 5. Worked example C: invalid package and bounded repair

Invalid raw candidate:

```json
{
  "worldMapUpdates": {
    "newLocations": [
      {
        "locationId": null,
        "initialId": "locref_turn_12_black_ford",
        "realm": "mortal_world",
        "name": "Чёрный брод",
        "locationType": "outdoor",
        "biome": null,
        "coordinates": { "x": 14, "y": -3 },
        "factionControl": [],
        "materialization": {
          "schemaVersion": 1,
          "materializationId": "mlocmat_turn_12_black_ford",
          "entityKind": "mortal_location",
          "realm": "mortal_world",
          "route": "world_map_creation",
          "sourceTurn": 12,
          "sourceAuthority": { "kind": "turn_outcome", "authorityId": "turn_12" },
          "initialId": "locref_turn_12_black_ford",
          "state": "complete",
          "sections": {
            "presentation": { "disposition": "populated", "reason": null },
            "physical": { "disposition": "populated", "reason": null }
          }
        }
      }
    ]
  }
}
```

The validator rejects missing complete presentation, missing z, missing outdoor
biome, difficulty, discovery, chronicle, physical governed collections, and
missing section dispositions. No permanent ID, receipt, map entry, or index
entry is committed.

Because the raw candidate has one exact new identity and no replay/confusable
history, repair may emit one bounded packet:

```json
{
  "kind": "mortal_location_materialization_repair",
  "actor": "mortal_location:new:locref_turn_12_black_ford",
  "transitionClass": "world_map_creation",
  "targetFiles": ["game_state/world/world_map.json"],
  "rawCarrier": "worldMapUpdates.newLocations",
  "rawCoordinate": "worldMapUpdates.newLocations[0]",
  "missingFields": [
    "description",
    "displayName",
    "purpose",
    "image_prompt",
    "coordinates.z",
    "biome",
    "biomeDescription",
    "discovery",
    "internalDifficulty",
    "externalDifficulty",
    "lastEventsDescription",
    "eventDescriptions",
    "actorBindings",
    "locationStorages",
    "activeThreats",
    "loreBindings",
    "customStates"
  ],
  "safeCorrectionRules": [
    "Исправьте только этот исходный объект новой удалённой локации.",
    "Оставьте locationId равным null и сохраните точный initialId.",
    "Заполните все секции либо объявите допустимые пустыми по замыслу с причиной.",
    "Не создавайте постоянный ID, receipt, seal или identity index."
  ]
}
```

If `initialId` were missing, duplicated, reused from history, or a case/Unicode
variant, the system would emit no normal repair packet and would stop before GM
dispatch.

## 6. Existing movement

After the client has assigned an exact permanent destination ID, movement sends
only selection and current operational chronology:

```json
{
  "currentLocationData": {
    "locationId": "loc_a438080e95864edc8256ec1054be5dab",
    "lastEventsDescription": "Герой вошёл в башню после заката.",
    "currentWeather": { "summary": "Сильный ветер" },
    "currentInteractions": []
  }
}
```

Do not resend the location name, description, coordinates, difficulty,
inhabitants, topology, envelope, receipt, `locationStorages`, or `contents`.
The client rebuilds shared fields from `world_map.locations[]` and visible exits
from `world_map.links[]`.

For a changed selection the client atomically parks every non-empty source
storage in its offscreen carrier and hydrates the destination storage into the
new current projection. Both physical locations retain the same logical
`location_storage(locationId, storageId)` item carrier, so travel does not add
an item transition or rewrite the item identity index. Same-location refreshes
reuse the validated pre-turn current contents. Any malformed/unresolved/
duplicate coordinate or publication failure rejects the turn and restores all
tracked files byte-for-byte.

Exact identity is not permission to teleport. Re-selecting the exact pre-turn
current location is allowed, but a changed destination must have one exact
pre-turn directed link from current to destination. The link must be
player-known/non-hidden, `access.state` must be `open`, requirements must be
empty, and the destination must be player-known/non-hidden. A reverse-only,
hidden, conditional, sealed, or unmet route fails closed without changing the
current projection.

## 6.1 Governed storage and threat lifecycle

Use exact IDs from accepted Context and only these command families:

Keep `activeThreats: []` in every raw new-location object. To create a threat
in that same turn, send it separately through `threatsToAdd[]` with
`threatId: null` and the exact new-location `initialTargetLocationId`; the
client assigns its permanent ID.

| Command | Accepted effect |
| --- | --- |
| `storageUpdates[]` | Patch `newName/newDescription/newCapacity/newOwner/newAuthorizedUsers/newHasFullAccess` while preserving item contents; owner and authorization changes carry synchronized access fields |
| `storagesToRemove[]` | Remove one exact empty storage; non-empty removal fails closed |
| `threatsToAdd[]` | Add one complete null-ID threat and let the client assign `threat_...` |
| `threatsToUpdate[]` | Deep-merge one exact non-terminal threat/activity patch |
| `threatsToRemove[]` | Remove one exact threat |
| `completeThreatActivities[]` | Archive one exact active activity and clear `currentActivity` |

For an existing location, each command uses exact `targetLocationId`. A new
threat for a same-turn new location instead uses null `targetLocationId` plus
exact `initialTargetLocationId`. Do not combine conflicting update/removal/
completion commands for one child. The plan mutates canonical map, selected
current projection when applicable, and client transition evidence atomically.
It never rewrites the location envelope or receipt: their section dispositions
describe the creation payload, not later live child counts. See the executable
`mortal_location_governed_storage_threat_lifecycle_v1` example.

## 7. Same-turn actor, faction, threat, and storage references; canonical lore

Use only the explicitly supported temporary reference field for a same-turn new
entity. Example actor binding before normalization:

```json
{
  "initialActorId": "npc_gate_keeper_turn_12",
  "actorId": null,
  "role": "staff",
  "description": "Смотритель переправы"
}
```

The actor's own accepted authority must place it at this exact location. The
location planner rewrites the field only after both raw packages validate. A
name such as `"Смотритель"` never binds the actor.

Lore is deliberately different: `loreBindings[]` may use only an exact
permanent `entryId`, `questId`, or `eventId` already present in the validated
pre-turn codex, quest, or world-event authority. This feature does not assign or
rewrite lore identities in the same turn; create that lore first and bind it in
a later accepted turn.

Map storage is semantic metadata. Current storage may include complete raw item
creation objects, each governed independently by #1511. The location-owned
portion of such a storage remains:

```json
{
  "storageId": "storage_inn_guest_chest",
  "name": "Гостевой сундук",
  "description": "Запираемый сундук у комнаты героя.",
  "image_prompt": "A sturdy dark fantasy guest chest beside an inn room, no text",
  "capacity": 12,
  "volume": 3.5,
  "owner": null,
  "authorizedUsers": [],
  "hasFullAccess": true,
  "contents": []
}
```

When `contents` is non-empty, every member is the full raw #1511 item creation
object from that contract—never a location-defined abbreviated item. Location
normalization accepts the storage and preserves each raw item object; item
normalization then assigns item identity and receipt. A location repair does not
repair the item.

For a storage created with the same current scene, the raw item's
`materialization.sourceAuthority` is exact and uses:

```json
{
  "kind": "location_storage",
  "authorityId": "locref_turn_12_inn:storage_inn_guest_chest"
}
```

The first half is the exact same-turn `initialId`, not a name or a guessed
permanent ID. The location plan validates that coordinate once. After location
normalization, the item transition destination uses the client-assigned
permanent `locationId` with the same exact `storageId`, while the source
authority remains the original accepted same-turn evidence. Wrong-case,
whitespace, Unicode-confusable, remote-map, duplicated, or unaccepted storage
coordinates do not authorize item sealing.

## 8. Negative authoring examples

All are invalid:

```json
{ "locationId": "loc_chosen_by_gm", "initialId": "temp" }
```

```json
{ "locationName": "Чёрный брод", "lastEventsDescription": "К ночи вода поднялась." }
```

```json
{ "sourceLocationId": "loc_a", "targetCoordinates": { "x": 3, "y": 4, "z": 0 } }
```

```json
{ "knownExits": ["Старая башня"], "adjacencyMap": { "east": "Старая башня" } }
```

```json
{ "locationId": "loc_existing", "description": "full resend", "materialization": {} }
```

```json
{ "initialId": "LOCREF_TURN_12_BLACK_FORD" }
```

The last example is not a distinct location when a differently cased historical
origin exists; it is a fail-closed confusable replay.

## 9. Expected player behavior

- Hidden location/link: no row, map node, edge, count, detail, name, or action.
- Rumored location/link: safe Russian rumor only, no exact coordinate or action.
- Discovered/visited: accepted in-world semantics and visible directed links.
- Current scene: canonical shared semantics plus current weather/interactions/
  chronology and accepted item contents.
- Rejected or receipt-less object: absent from all console/browser/locality/news
  surfaces.
- Internal envelope/receipt/index/repair/route fields: absent recursively.

## 10. Implementation verification loop

Use PowerShell 7 from the feature worktree. During a TDD slice, run only the
smallest matching filter. At the first meaningful integrated checkpoint:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

For documentation/manifest and accepted-turn lifecycle boundaries:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

Immediately before merge, run exactly one final control:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Do not run an unbounded full-solution `dotnet test`, and do not repeat Fast just
before PreMerge.

## 11. Documentation boundary

This feature changes Mortal World location authoring and therefore updates
Mortal prompts, rules, examples, manifest, daemon entrypoints, and source guards.
It does not change Shining Abode halls, Chaos Sea Guardian planes, or their
pending/control surfaces. `OtherGuides/Afterlife_Contract_Matrix.md` and the
afterlife worked examples therefore receive a recorded no-update rationale;
#1514 owns their location materialization contract.

The T088 duplicate audit produced these dispositions:

- `Rules/Block_18.A.txt` remains unchanged because it describes chronicle
  formatting and does not define location identity, routes, topology, or
  materialization authority.
- `Rules/Block_19.txt` retained its NPC semantics, but the genuine same-turn
  Mortal location reference was migrated to an exact `locref_...` temporary
  identity and complete raw route; name-derived authority was removed.
- `Rules/Block_21.txt` retained exact faction territory references, while the
  genuine stale `internalDifficultyProfile` / `externalDifficultyProfile`
  duplicate was migrated to the current difficulty fields.
- `Examples/E_Block_11.B.txt` no longer treats coordinates as authority when
  selecting an existing storage location.
- Both the static launcher guide and its generator now require current raw
  routes, exact existing IDs, client-derived topology, and full-turn location
  repair. The translation guide now distinguishes raw GM commands from the
  client-owned canonical map/current/index writes they cause.

The T092 afterlife audit found no #1513-owned contract change. No Chaos Sea or
Shining Abode pending/control file, `actionType`, response field, receipt,
report, scheduler contour, lifecycle mode, normalizer side effect, or
player-visible afterlife command changed. The shared repair loop selects the
new behavior only for `mortal_location_materialization_repair`; all other
packet families keep their existing bounded workflow. Therefore
`OtherGuides/Afterlife_Contract_Matrix.md`,
`Examples/E_CLI_Afterlife_Turns.txt`, the afterlife manifest entries, and the
afterlife documentation/source guards are intentionally unchanged. Their
future location materialization work remains owned by #1514.

## 12. Implementation evidence

The pre-implementation control was run from the isolated feature worktree after
the specification and plan were committed. It is the accepted baseline for the
TDD slices below; it is recorded here rather than repeated.

Task 7 retained the existing single-lease `AcceptedTurnCanonicalStateRefresh`
transaction instead of adding a second writer. That transaction already owns
the complete before-image set, applies the bound normalizer, aggregates Mortal
item and Mortal location post-seal validation, and restores every tracked path
before releasing the lease. The new injected-write, forged-item-postcheck,
retry, and stale-output tests make that contour executable evidence.

No `BookOfEternityClient/UI/` or `BrowserMortalWorldWriteService` source change
was required for T058. Neither client currently performs a local Mortal
location transition write: location selection is an ordinary GM turn, while
the browser write service has no location-mutation command. The new repair-loop
failure branch is in `GameEngine`, retains exact diagnostics in the operator
log, and emits only generic Russian console text with no path, ID, receipt,
index, or validation code. Existing storage/vehicle local writes remain owned
by the Mortal item sanitizer. Discovery/navigation projections are migrated
separately in User Story 5.

The T069 payload inspection used the shared projection fixtures exercised by
the console, browser, and news tests. A hidden canonical location and a
receipt-less otherwise-valid location were absent from rendered text, actions,
and serialized DTOs. A rumored location retained only its label and
`rumorSummary`: it had no coordinates, description, region, features, detail
selector, or travel action. A visited current location retained its accepted
world semantics, but permanent selectors remained only in action bindings and
did not render. The one-way accepted link appeared only from its declared
source; no reverse exit was invented. Current storage rendered the accepted
receipt-bearing item and omitted the raw candidate.

T068 required a small frontend contract update because the C# game-screen DTO
now removes player-visible validation state/labels rather than adding a new
location field. `contracts.ts`, `TurnStatePanel.tsx`, the static shell, and the
two affected React fixtures were updated. TypeScript typecheck and the 11
targeted React tests passed; location DTO shape and map rendering required no
additional React field or component.

| Stage | Command | Result directory | Passed | Failed | Timed out | Duplicate tests | Cleanup |
|---|---|---|---:|---:|---:|---:|---|
| Baseline | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` | `TestResults/test-lanes/20260812-114707-101-31724-220f790e8f1b43a797dd7a5c968d90af-fast` | 2937 | 0 | 0 | 0 | Successful |
| T006–T010 RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationTestFixtureTests"` | `TestResults/test-lanes/20260812-133040-766-29608-8b9a4217dd3e43e1887a017a9e902b0a-focused` | 0 | Expected compile failure: missing `MortalLocationTestFixture` | 0 | 0 | Complete |
| T006–T010 GREEN + map control | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationTestFixtureTests|FullyQualifiedName~LocalMapViewerServiceTests"` | `TestResults/test-lanes/20260812-134015-431-48380-f77489f98dd948d0895c532e9ef098fd-focused` | 23 | 0 | 0 | 0 | Complete |
| T006–T010 bootstrap/integration compile control | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapValidationTests"` | `TestResults/test-lanes/20260812-133806-711-47084-fa4285b3f16b4a009726dc5bb5048ea9-focused` | 32 | 0 | 0 | 0 | Complete |
| T011 contract RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationMaterializationContractTests"` | `TestResults/test-lanes/20260812-134315-279-48468-41b75ca8a7e34779a9851b97f1198680-focused` | 0 | Expected compile failure: missing contract | 0 | 0 | Complete |
| T011 contract GREEN | same focused filter | `TestResults/test-lanes/20260812-134915-859-37076-738c301032c34515a8e9b26c344b1b4c-focused` | 50 | 0 | 0 | 0 | Complete |
| T012/T017 identity RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationIdentityStateTests"` | `TestResults/test-lanes/20260812-135149-882-31596-44db3434561b4985a70012622b42737c-focused` | 0 | Expected compile failure: missing identity authority | 0 | 0 | Complete |
| T012/T017 identity GREEN | same focused filter | `TestResults/test-lanes/20260812-135618-606-42864-e1e85192c5434aec8105f13541c093e1-focused` | 20 | 0 | 0 | 0 | Complete |
| T013/T014 planner RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260812-135907-610-50572-a1c2440b69374713999312b4a08c710c-focused` | 0 | Expected compile failure: missing accepted-turn planner | 0 | 0 | Complete |
| T016 phase RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"` | `TestResults/test-lanes/20260812-140501-809-12736-b8ab3570f30a40f39b03c21150de46d3-focused` | 0 | Expected compile failure: missing phase registration | 0 | 0 | Complete |
| T016 phase GREEN | same focused filter | `TestResults/test-lanes/20260812-140628-273-12320-070085c9e6dc48868956a7172727d2eb-focused` | 15 | 0 | 0 | 0 | Complete |
| T022 structured-context RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests.ValidateResponse_RawMortal"` | `TestResults/test-lanes/20260812-141328-926-3852-5ff5106f5f6844a0b6995f8a85af9242-focused` | 0 | Expected compile failure: missing `ValidationIssue.MortalLocationRepairContext` | 0 | 0 | Complete |
| T023 current-route bypass RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests.ValidateResponse_CurrentCreationWithClientOwnedPermanentId_IsRejected"` | `TestResults/test-lanes/20260812-141543-425-31836-3c7826053afb41829354705703288790-focused` | 0 | 1 expected assertion failure | 0 | 0 | Complete |
| T023 accepted-turn entrypoint RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests.ValidateAcceptedTurnRawState_UsesLocationContractBeforeNormalization"` | `TestResults/test-lanes/20260812-141823-494-18672-b803fbc3f47144cd8afb74ee6380ee09-focused` | 0 | Expected compile failure: missing raw-state entrypoint | 0 | 0 | Complete |
| T011–T023 final unit GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationMaterializationContractTests|FullyQualifiedName~MortalLocationIdentityStateTests|FullyQualifiedName~ValidationPhaseSelectionTests"` | `TestResults/test-lanes/20260812-142001-715-50244-95ba678fccf945178deb525178d05adb-focused` | 88 | 0 | 0 | 0 | Complete |
| T011–T023 final integration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260812-142026-616-46956-9906d6a3ddb649aa8724e81fc28c45ca-focused` | 28 | 0 | 0 | 0 | Complete |
| T015 normalizer RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.MortalLocations"` | `TestResults/test-lanes/20260812-142744-595-39248-549bd15169de4191b72da6efc3097a0e-focused` | 0 | Expected compile failure: missing location normalizer | 0 | 0 | Complete |
| T015/T024–T025 normalizer GREEN | same focused normalizer filter | `TestResults/test-lanes/20260812-142947-624-49480-f795a03a7ee846c185eeed1ecd0da20f-focused` | 4 | 0 | 0 | 0 | Complete |
| T026/T027 exact-state boundary RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"` | `TestResults/test-lanes/20260812-143850-398-49812-cffb4d836f9a4d0aaa11b77a2fbbb00a-focused` | 18 | 4 expected assertion failures | 0 | 0 | Complete |
| T026 exact canonical reader RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~ValidationPhaseSelectionTests.ExactCanonicalMortalLocationReader"` | `TestResults/test-lanes/20260812-144613-184-2360-964b2b315fd14e4d83daa0f60e190f73-focused` | 0 | Expected compile failure: missing exact reader | 0 | 0 | Complete |
| T026 receipt-less reader RED | same exact-reader focused filter | `TestResults/test-lanes/20260812-145606-343-16552-105ad5b2692d44b2b904486436be5d84-focused` | 0 | 1 expected assertion failure | 0 | 0 | Complete |
| T026 faction-location regression GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests"` | `TestResults/test-lanes/20260812-150244-485-11632-344065cf70e74a23a27f21f76aa81aa4-focused` | 175 | 0 | 0 | 0 | Complete |
| T028 final US1 unit GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~MortalLocationMaterializationContractTests|FullyQualifiedName~MortalLocationIdentityStateTests|FullyQualifiedName~ValidationPhaseSelectionTests"` | `TestResults/test-lanes/20260812-150452-988-45196-180babf2e3a64634a709cb54c65d3374-focused` | 96 | 0 | 0 | 0 | Complete |
| T028 final US1 integration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.MortalLocations|FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260812-150526-582-17012-781dc568a819482e985cc31f36fc811c-focused` | 32 | 0 | 0 | 0 | Complete |
| US1 integrated Fast checkpoint | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` | `TestResults/test-lanes/20260812-151041-124-49748-a99c3a4fc0ec42798dbb06e443251056-fast` | 3026 | 0 | 0 | 0 | Complete |
| T029 neutral-root RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapValidationTests.MortalBootstrapStateBuilder"` | `TestResults/test-lanes/20260812-152133-239-42320-c482115bc2234a4ebc0ff8688c493d90-focused` | 0 | 3 expected assertion failures | 0 | 0 | Complete |
| T030/T031 planner RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapLocationPlan"` | `TestResults/test-lanes/20260812-153020-791-12076-2a7beacf226d44ecb1857c24ca8630a1-focused` | 0 | Expected compile failure: missing bootstrap-aware planner input | 0 | 0 | Complete |
| T029–T037 final bootstrap/snapshot GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~PendingTurnSnapshotTests"` | `TestResults/test-lanes/20260812-155429-887-39196-d142d81507454c7ba8c141ebc4c83271-focused` | 50 | 0 | 0 | 0 | Complete |
| T034/T036 source-guard GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~GameEngineSourceGuardTests"` | `TestResults/test-lanes/20260812-155547-855-46748-bfe80255e24446999b8308bb69913e85-focused` | 111 | 0 | 0 | 0 | Complete |
| US2 full Mortal-location unit GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocation"` | `TestResults/test-lanes/20260812-155608-008-34924-1d2cfebf05724d26b6ef404bc3836c11-focused` | 84 | 0 | 0 | 0 | Complete |
| US2 accepted-turn integration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterialization"` | `TestResults/test-lanes/20260812-155627-400-42452-cbe545161c204cebb03db8cd11bdffd9-focused` | 28 | 0 | 0 | 0 | Complete |
| US2 integrated Fast checkpoint | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` | `TestResults/test-lanes/20260812-155649-581-17956-49bb26bf446c4920a44d02261e9a8034-fast` | 3026 | 0 | 0 | 0 | Complete |
| T038–T040 topology/lifecycle RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260812-161434-046-13840-17627338e2b5430b9eb7a55b43cd607d-focused` | 37 | 18 expected assertion failures | 0 | 0 | Complete |
| T041 map GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~LocalMapViewerServiceTests"` | `TestResults/test-lanes/20260812-163520-109-18244-50f823c24e454b84bf85606074cc6e5c-focused` | 17 | 0 | 0 | 0 | Complete |
| T042 exact-locality RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~LocalInteractionScopeServiceTests"` | `TestResults/test-lanes/20260812-163741-108-13208-7ad5f73cead446de8c1128a5465e3846-focused` | 1 | 3 expected assertion failures | 0 | 0 | Complete |
| T038–T048 final locality/service GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~LocalMapViewerServiceTests|FullyQualifiedName~LocalInteractionScopeServiceTests|FullyQualifiedName~TrainingServiceTests|FullyQualifiedName~ConsoleTrainingCommandTests|FullyQualifiedName~NpcTradeServiceRequestFlowTests|FullyQualifiedName~ActorMemoryServiceTests|FullyQualifiedName~ValidationPhaseSelectionTests"` | `TestResults/test-lanes/20260812-170514-348-33520-33e4d7907e8140f09862becdf25efc0d-focused` | 161 | 0 | 0 | 0 | Complete |
| T038–T048 final topology/lifecycle GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests.Topology|FullyQualifiedName~MortalLocationMaterializationValidationTests.Lifecycle"` | `TestResults/test-lanes/20260812-170615-144-33604-1e4337af42a542408db03bd7450372f2-focused` | 27 | 0 | 0 | 0 | Complete |
| T049 client-protocol authority RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~Build_GmAuthoredClientProtocolFieldFailsClosedWithoutReceiptPathHint"` | `TestResults/test-lanes/20260812-175036-967-11184-802f2c4e8126481ead225dd120930e0c-focused` | 0 | 1 expected assertion failure | 0 | 0 | Complete |
| T049/T053/T054 final packet + serialization GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationRepairPacketBuilderTests|FullyQualifiedName~ValidationRepairRequestTests"` | `TestResults/test-lanes/20260812-175320-306-17452-16e023623ef14500a0b669790f4e016d-focused` | 25 | 0 | 0 | 0 | Complete |
| T055 raw planner/repair-context GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260812-173715-268-50124-b8641398f9004534b7586e2eea8bface-focused` | 57 | 0 | 0 | 0 | Complete |
| T055 legacy location packet retirement GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedRepairReady_WritesAcceptedTrajectoryRecord|FullyQualifiedName~LegacyLocationTransitionCodesDoNotEmitLegacyPacket|FullyQualifiedName~LegacyLocationShapeCodesDoNotEmitLegacyPacket|FullyQualifiedName~LegacyAdjacencyCodesDoNotEmitLegacyPacket|FullyQualifiedName~LegacyOutdoorBiomeCodeDoesNotEmitLegacyPacket"` | `TestResults/test-lanes/20260812-174252-819-49036-646c48bb441a46de8fe720a20668eaaf-focused` | 5 | 0 | 0 | 0 | Complete |
| T050–T052/T056–T059 final repair/lifecycle/rollback GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedRefresh_|FullyQualifiedName~MortalLocationCanonicalRepair_|FullyQualifiedName~MortalLocationRollback_|FullyQualifiedName~WriteValidationRepairRequestAsync_MortalLocationErrors_|FullyQualifiedName~WaitForContractRepairAsync_ProtectedMortalLocationAuthority|FullyQualifiedName~WaitForContractRepairAsync_UnsafeMortalLocationAuthority|FullyQualifiedName~WaitForContractRepairAsync_UnresolvedMortalLocation"` | `TestResults/test-lanes/20260812-175342-296-6020-a80c0742d0164b2abbfb8eeea67a936e-focused` | 19 | 0 | 0 | 0 | Complete |
| T073 same-turn current-storage RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~StoragePlacement_SameTurnCurrentLocationSealsIntoPermanentCarrier|FullyQualifiedName~StoragePlacement_MalformedItemKeepsRepairOwnershipWithItemContract|FullyQualifiedName~StoragePlacement_RemoteMapContentsAreRejectedByLocationOnly"` | `TestResults/test-lanes/20260813-101048-906-27916-d8c57658b7154d068c12c4ed0a15e76d-focused` | 2 | 1 expected authority failure | 0 | 0 | Complete |
| T070/T071/T074 actor/faction authority GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationActorBinding_|FullyQualifiedName~MortalLocationControl_|FullyQualifiedName~AcceptedRefresh_SameTurnNpcLocationReferenceRewritesToPermanentIdentity|FullyQualifiedName~LocationNormalizer_SameTurnFactionControlWritesExactEffectiveIdentity|FullyQualifiedName~ValidatePreNormalizationNpcCoreChanges_NestedLocationIdentityIsNotPermanentAuthority|FullyQualifiedName~ValidatePreNormalizationNpcCoreChanges_DuplicateSameTurnLocationIdentityIsNotAuthority"` | `TestResults/test-lanes/20260813-102638-526-27744-4ebc2afb93344c01aa611d3082925587-focused` | 16 | 0 | 0 | 0 | Complete |
| T072/T074/T079 lore/threat/storage/canonical-reader GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~Companions_|FullyQualifiedName~CrossReferences_CaseVariantStorageIdDoesNotResolveCanonicalLocationStorage|FullyQualifiedName~CrossReferences_CaseVariantThreatIdDoesNotResolveCanonicalLocationThreat"` | `TestResults/test-lanes/20260813-102705-586-45188-2370296becdd444f8270bfc60254390a-focused` | 17 | 0 | 0 | 0 | Complete |
| T074 direct NPC/faction reader GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalAuthorityReaders_IgnoreCurrentProjectionAliasesAndUseOrdinalIds"` | `TestResults/test-lanes/20260813-102846-470-40648-45486e2ca8384e4fba43549d83d66e2d-focused` | 1 | 0 | 0 | 0 | Complete |
| T073/T078 complete item-route authority GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests%2BRoutes"` | `TestResults/test-lanes/20260813-102207-094-1932-5e481bdcd9fd4e63a3b8646d39ff9c32-focused` | 47 | 0 | 0 | 0 | Complete |
| T081 active fixture RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests.GameSessionFixture_MortalLocationUsesCurrentMaterializationAndIdentityIndex"` | `TestResults/test-lanes/20260813-103205-061-50460-4c793131fc284c1c9c329b9fe5791de1-focused` | 0 | 1 expected missing-map failure | 0 | 0 | Complete |
| T081/T082 active/shared fixture GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests"` | `TestResults/test-lanes/20260813-110503-604-17312-7e2e8d2b3f33445da8ffdf3dc4975440-focused` | 21 | 0 | 0 | 0 | Complete |
| T082 legacy fixture migration RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ValidatorFixtureTests"` | `TestResults/test-lanes/20260813-104058-376-30852-16aa14c68bb54f8c9b2ba3e9e189727d-focused` | 93 | 5 expected stale-fixture failures | 0 | 0 | Complete |
| T082 migrated location validator fixtures GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ValidatorFixtureTests.MortalLocationMigratedFixture_BrokenAndFixedVariantsRespectCurrentContract"` | `TestResults/test-lanes/20260813-110100-861-33416-32423413bb2c4545a5d60bb9f0e28553-focused` | 4 | 0 | 0 | 0 | Complete |
| T082 deterministic fixture/negative-label GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationTestFixtureTests"` | `TestResults/test-lanes/20260813-110234-612-10852-bc4ec1eeb67f4bc19c26922c4c46bda2-focused` | 7 | 0 | 0 | 0 | Complete |
| T060 projection compile RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationPlayerProjectionTests"` | `TestResults/test-lanes/20260813-071729-165-48424-482bad1b145448549816153753c21748-focused` | 0 | Expected compile failure: missing shared player projection | 0 | 0 | Complete |
| T060 projection behavior RED | same focused filter | `TestResults/test-lanes/20260813-071945-690-40516-deb09801dee64f6d9120b3270dd553ae-focused` | 4 | 1 expected current-projection assertion failure | 0 | 0 | Complete |
| T061 console projection RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~TryProcessCommand_Locations_UsesAcceptedDiscoveryProjectionOnly|FullyQualifiedName~TryProcessCommand_CurrentLocation_FailsClosedWhenCurrentProjectionDiffersFromMap"` | `TestResults/test-lanes/20260813-072937-374-47468-a552fb0306a34f9da2714135eb3f6fdb-focused` | 0 | 2 expected projection failures | 0 | 0 | Complete |
| T062 browser exact-projection RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerWebCommandServiceTests.ExecuteAsync_Locations_UsesAcceptedDiscoveryProjectionAndExactSelectors"` | `TestResults/test-lanes/20260813-075132-328-34436-e4eeab6c914948c2bf6aced0e7ba6bbd-focused` | 0 | 1 expected browser projection failure | 0 | 0 | Complete |
| T063 news exact/discovery projection RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ExplorerMortalWorldNewsCommandResultBuilderTests"` | `TestResults/test-lanes/20260813-081251-621-35388-0852dc2bf4f54804b8a453530a145f9f-focused` | 0 | 2 expected news projection failures | 0 | 0 | Complete |
| T060/T063 final shared projection/news GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationPlayerProjectionTests|FullyQualifiedName~ExplorerMortalWorldNewsCommandResultBuilderTests"` | `TestResults/test-lanes/20260813-114429-911-48800-9cd5ed9aff474abbb5bda4df570a75d0-focused` | 8 | 0 | 0 | 0 | Complete |
| T061/T062/T065–T067/T069 final console/browser projection GREEN | bounded 15-method location/map/current/storage filter in `ExplorerModeCommandTests` and `ExplorerWebCommandServiceTests` | `TestResults/test-lanes/20260813-114456-596-47920-51a5ea5b69fa4b7c964c77d348ed73e9-focused` | 16 | 0 | 0 | 0 | Complete |
| T068 browser game-screen DTO privacy GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~BrowserApiContractTests.OrdinaryGameScreenFailureState_HidesValidationRepairAndRollbackVocabularyRecursively"` | `TestResults/test-lanes/20260813-114713-318-33124-15eadea57470450187e9447269e8c886-focused` | 1 | 0 | 0 | 0 | Complete |
| T068 frontend type/behavior GREEN | `npm.cmd run typecheck`; `npm.cmd exec -- vitest run test/browserSceneComposerPolish.test.tsx test/browserSoulEmptyStates.test.tsx` | direct frontend verification | 11 React tests plus TypeScript typecheck | 0 | 0 | 0 | Complete |
| T083–T090 CLI/daemon documentation source guard RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests.MortalLocationCliAndDaemonGuidance_UsesCurrentSchemaAndFullTurnRepair"` | `TestResults/test-lanes/20260813-111815-610-40776-5bd4ee9528764287919c1c2a6fa166db-focused` | 0 | 1 expected missing-current-contract failure | 0 | 0 | Complete |
| T083–T090 CLI/daemon documentation source guards GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests.MortalLocationCliAndDaemonGuidance_UsesCurrentSchemaAndFullTurnRepair|FullyQualifiedName~PromptDocumentationCoverageTests.MortalLocationRuleAndWorkedExamples_UseCurrentMaterializationContract"` | `TestResults/test-lanes/20260813-113830-272-2196-44e4a425555a42b180688b1563a77299-focused` | 2 | 0 | 0 | 0 | Complete |
| T103 offscreen coordinate validator RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~Lifecycle_StorageItems_CanonicalOffscreenCoordinate"` | `TestResults/test-lanes/20260813-181205-948-30748-db7f9018e6d8444c8db24f9a712e1f22-focused` | 0 | 3 expected coordinate failures | 0 | 0 | Complete |
| T103–T106 offscreen storage lifecycle GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests+OffscreenStorage|FullyQualifiedName~Lifecycle_StorageItems"` | `TestResults/test-lanes/20260813-182011-381-56312-0bd864115d3d46d8a8ec225853e73d8c-focused` | 12 | 0 | 0 | 0 | Complete |
| T103–T106 offscreen catalog/transition GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~MortalItemCarrierCatalogTests|FullyQualifiedName~MortalItemIdentityTransitionTests|FullyQualifiedName~MortalLocationStorageContentsStateTests"` | `TestResults/test-lanes/20260813-181941-806-48868-89ee4f66c490409492c9ca6797bbb06c-focused` | 57 | 0 | 0 | 0 | Complete |
| T107 recursive offscreen DTO privacy GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~MortalLocationPlayerProjectionTests|FullyQualifiedName~MortalItemPlayerProjectionTests.MortalMaterializationProjection"` | `TestResults/test-lanes/20260813-182833-151-55468-7804124aa4e1467e8013bd35b32cbd6f-focused` | 7 | 0 | 0 | 0 | Complete |
| T108 existing-movement storage guidance GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~PromptDocumentationCoverageTests.MortalLocationMaterializationGuidance|FullyQualifiedName~PromptDocumentationCoverageTests.MortalLocationCliAndDaemonGuidance"` | `TestResults/test-lanes/20260813-183620-151-55100-672675d289bf4c13b3ee300494d9bf9d-focused` | 2 | 0 | 0 | 0 | Complete |
| T107/T108 combined privacy/documentation GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~MortalItemPlayerProjectionTests|FullyQualifiedName~MortalLocationPlayerProjectionTests|FullyQualifiedName~PromptDocumentationCoverageTests"` | `TestResults/test-lanes/20260813-184427-833-27768-83157e6e3c6e452683c962dd18f6fed3-focused` | 32 | 0 | 0 | 0 | Complete |
| T104 metadata-only existing-movement resend RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~Lifecycle_StorageItems_ExistingMovementPayloadCannotEchoStorageMetadata"` | `TestResults/test-lanes/20260813-183928-620-9400-a7dc44535852429ba36db0bbc37bc657-focused` | 0 | 1 expected acceptance failure | 0 | 0 | Complete |
| T104 minimal/forbidden existing-movement payloads GREEN | focused Integration filter for `Lifecycle_StorageItems_ExistingMovementPayload` and `Lifecycle_StorageItems_SameLocationMinimal` | `TestResults/test-lanes/20260813-184022-866-25996-c4b80ec0dbf74fa78e6c5ac98d804fd0-focused` | 3 | 0 | 0 | 0 | Complete |
| T089/T091 storage-continuity example/manifest GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests.MortalLocationMaterialization"` | `TestResults/test-lanes/20260813-184347-989-27708-14c2a1c835b540b1b85ac7f4e9e43958-focused` | 2 | 0 | 0 | 0 | Complete |
| T096 complete-threat shared fixture RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` | `TestResults/test-lanes/20260813-184649-242-48280-380db963017949aaad1c867ef24e59aa-fullvalidation` | 268 | 1 expected stale canonical threat fixture | 0 | 0 | Complete |
| T096 complete-threat shared fixture focused GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests.ValidatorFixture_MortalLocationBackupUsesCurrentCanonicalMapAndIndex"` | `TestResults/test-lanes/20260813-185339-407-38624-576ae28e91374a6991973f90649c5502-focused` | 1 | 0 | 0 | 0 | Complete |
| T089/T091 executable manifest compile RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests.MortalLocationMaterialization"` | `TestResults/test-lanes/20260813-113356-008-17496-859f47db8208408b8ead432ccd20f7c3-focused` | 0 | Expected compile failure while executable manifest model/helpers were absent | 0 | 0 | Complete |
| T089/T091 executable example parser RED | same focused Integration filter | `TestResults/test-lanes/20260813-113533-840-34868-45ea9264d49c449eaf32cffd7a199d1e-focused` | 1 | 1 expected heading-parser failure | 0 | 0 | Complete |
| T089/T091 executable manifest/examples GREEN | same focused Integration filter | `TestResults/test-lanes/20260813-113626-189-17212-90ba88db6566432daca4b7fdafcd6167-focused` | 2 | 0 | 0 | 0 | Complete |
| T087 daemon compact-template integration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GmTurnHelperContractTests.DaemonCompactTemplates_PreventFirstIncarnationBootstrapRepairPatterns"` | `TestResults/test-lanes/20260813-113930-261-12704-ee4cfa5492824541a136d981a589da3b-focused` | 1 | 0 | 0 | 0 | Complete |
| T083–T092 full documentation source-guard GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests"` | `TestResults/test-lanes/20260813-114341-595-25404-ad80a3f7baf242e6966a89060431e72b-focused` | 24 | 0 | 0 | 0 | Complete |
| T094 bounded group 1 GREEN | contract, identity, projection, and local-map Focused group from `plan.md` | `TestResults/test-lanes/20260813-115504-538-32780-b74d3e423f5b4866bd029095e1fe156e-focused` | 97 | 0 | 0 | 0 | Complete |
| T094 bounded group 2 GREEN | prompt, console-training, training-service, and training-browser Focused group from `plan.md` | `TestResults/test-lanes/20260813-115803-165-18664-bee8a95833ae446c82c185859c7545b0-focused` | 106 | 0 | 0 | 0 | Complete |
| T094 bounded group 3 GREEN | Mortal-location validation, canonical normalizer, and bootstrap Focused Integration group from `plan.md` | `TestResults/test-lanes/20260813-120904-904-27748-d8bc75e6092b4bfdbe1bd10b41c04ea1-focused` | 385 | 0 | 0 | 0 | Complete |
| T094 bounded group 4a GREEN | NPC-core and actor-materialization Focused Integration split | `TestResults/test-lanes/20260813-123226-251-34068-944e355d0145438b965585addf5f7c39-focused` | 338 | 0 | 0 | 0 | Complete |
| T094 bounded group 4b GREEN | faction- and Mortal-item-materialization Focused Integration split | `TestResults/test-lanes/20260813-123707-281-28388-746cbb9d2f5540039625a475906c0ae5-focused` | 268 | 0 | 0 | 0 | Complete |
| T094 bounded group 5a GREEN | changed console location/map/quest/news projection methods | `TestResults/test-lanes/20260813-132128-971-2336-9f94ef7dc3b1456da42041d9fd0f96d3-focused` | 16 | 0 | 0 | 0 | Complete |
| T094 bounded group 5b GREEN | changed browser location/map/quest/news projection methods | `TestResults/test-lanes/20260813-132733-187-35100-d77026722c28471a9e649c3b2b89b79e-focused` | 20 | 0 | 0 | 0 | Complete |
| T094 bounded group 5c GREEN | exact packet classification and protected/unresolved repair branches | `TestResults/test-lanes/20260813-133406-984-34980-dd50ddaebd4c480b88a84467a460dd47-focused` | 15 | 0 | 0 | 0 | Complete |
| T094 bounded group 5d GREEN | baseline restore, coherent resubmission, and formatting-only rejection branches | `TestResults/test-lanes/20260813-133436-429-46436-65185ebbfadf40a4a66e3ee1bfae0bb3-focused` | 5 | 0 | 0 | 0 | Complete |
| T094 bounded group 5e GREEN | diagnostic/write-failure and caller-owned rollback branches | `TestResults/test-lanes/20260813-133539-554-17020-d83460edb52942ff9dc2b5e98e4a43e4-focused` | 5 | 0 | 0 | 0 | Complete |
| T094 bounded group 5f GREEN | legacy packet retirement, session replacement, accepted trajectory, and bootstrap-stall control | `TestResults/test-lanes/20260813-133617-540-22812-5323d46f5bdb4e8a8e20d83e0a6418fb-focused` | 8 | 0 | 0 | 0 | Complete |
| T094 bounded group 6 GREEN | active fixture integrity and executable example/manifest validation | `TestResults/test-lanes/20260813-131835-881-32496-bb20fec433c84278a76733f8e5121fe7-focused` | 41 | 0 | 0 | 0 | Complete |
| T095 authority/full-feature fixture RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260813-191206-739-24288-9630d67b230440c98065b4315a4d5b4c-focused` | 160 | 3 stale incomplete-threat authority fixtures | 0 | 0 | Complete |
| T095 authority fixture focused GREEN | focused Integration filter for the three exact/case-sensitive authority tests | `TestResults/test-lanes/20260813-191544-868-21840-89bf7f125b04438fbf0ba6c7ecf6c05d-focused` | 3 | 0 | 0 | 0 | Complete |
| T095 full Mortal-location integration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260813-191629-475-20060-5c3eddb8787a4424abedfcd55fd277ff-focused` | 163 | 0 | 0 | 0 | Complete |
| T095 Fast stale-fixture RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` | `TestResults/test-lanes/20260813-191947-808-48372-f2ee519e2c234780ae08f3fba2761b3e-fast` | 3132 | 1 stale news threat fixture | 0 | 0 | Complete |
| T095 news fixture focused GREEN | focused Fast filter for `BuildAsync_ThreatSummaryCountsOnlyAcceptedDiscoverySafeLocationsOnce` | `TestResults/test-lanes/20260813-192341-962-55484-6b205c3b467d4ddf9fc70bf132a3f506-focused` | 1 | 0 | 0 | 0 | Complete |
| T095 Fast GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` | `TestResults/test-lanes/20260813-192419-159-40020-50b8700b32054e258dc54d02e26bd557-fast` | 3133 | 0 | 0 | 0 | Complete |
| T096 FullValidation GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` | `TestResults/test-lanes/20260813-185403-344-50544-7fe3817a060f4923854219638d47cc75-fullvalidation` | 1787 | 0 | 0 | 0 | Complete |
| T097 LifecycleIntegration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration` | `TestResults/test-lanes/20260813-190246-192-55860-8215c872ab4649aaafb7f021b3a0e4d7-lifecycleintegration` | 243 | 0 | 0 | 0 | Complete |
| Review movement authorization RED | focused Integration filter for `Lifecycle_MovementRequiresOneVisibleOpenOutgoingLink` | `TestResults/test-lanes/20260813-144615-260-33060-48e7e6c864904bee88c5b4701155517e-focused` | 0 | 6 expected assertion failures | 0 | 0 | Complete |
| Review movement authorization GREEN | same focused Integration filter | `TestResults/test-lanes/20260813-144741-033-31384-904a00d2558a46cd8d7729b8559130c7-focused` | 6 | 0 | 0 | 0 | Complete |
| Review governed storage/threat lifecycle RED | focused Integration governed-command filter | `TestResults/test-lanes/20260813-145408-021-29712-ccd209656c03414b8868f15d171af6a7-focused` | 0 | 9 expected assertion failures | 0 | 0 | Complete |
| Review governed storage/threat lifecycle GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests.GovernedCommands"` | `TestResults/test-lanes/20260813-161724-131-44612-e86f5077f7a24eb186f8e787762fd331-focused` | 26 | 0 | 0 | 0 | Complete |
| Review full location integration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"` | `TestResults/test-lanes/20260813-154212-777-50204-04131260dc9048bbbf76f012c573c7f1-focused` | 141 | 0 | 0 | 0 | Complete |
| Review contract/repair/request GREEN | focused unit filter for location contract, repair-packet builder, and repair-request serialization | `TestResults/test-lanes/20260813-154258-981-13488-5407916eb2cb4955add201c1545e9f6d-focused` | 116 | 0 | 0 | 0 | Complete |
| Governed worked-example manifest RED | focused Integration Mortal-location documentation filter | `TestResults/test-lanes/20260813-152301-224-31308-a3fbb10701794fb3a61c2fe7da31429f-focused` | 0 | 2 expected missing example/manifest failures | 0 | 0 | Complete |
| Governed worked-example manifest GREEN | same focused Integration filter | `TestResults/test-lanes/20260813-152518-282-32424-5994be14dd6e41b9b7b4525fe3759096-focused` | 2 | 0 | 0 | 0 | Complete |
| Review GM source guards GREEN | focused prompt rules/examples/CLI/daemon filter | `TestResults/test-lanes/20260813-153204-782-18104-e903cceecedb410f91b5fb5f2e8892a5-focused` | 2 | 0 | 0 | 0 | Complete |
| Review daemon compact-template GREEN | focused Integration daemon-template filter | `TestResults/test-lanes/20260813-153232-588-25076-ba83fa57b3fb4ce0afe281119a82b41d-focused` | 1 | 0 | 0 | 0 | Complete |
| Review FullValidation stale-exemption RED | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` | `TestResults/test-lanes/20260813-154705-825-18872-65f85727070d4d849e9ef412946fba62-fullvalidation` | 285 | 1 stale manifest exemption | 0 | 0 | Complete |
| Review syntax-exemption correction GREEN | focused Integration `JsonExamples_AreParseableOrExplicitlyExempted` | `TestResults/test-lanes/20260813-155253-000-29208-28577eac0a6b48878acefc7ffa17086a-focused` | 1 | 0 | 0 | 0 | Complete |
| Review FullValidation GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` | `TestResults/test-lanes/20260813-155317-877-40416-4a667e146ca342dc8e75b37aa569550e-fullvalidation` | 1783 | 0 | 0 | 0 | Complete |
| Review LifecycleIntegration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration` | `TestResults/test-lanes/20260813-160234-322-29364-0e1262cd4b56445a934e845b41683dc5-lifecycleintegration` | 243 | 0 | 0 | 0 | Complete |
| Review Fast external timing failure | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` | `TestResults/test-lanes/20260813-161005-084-48264-bf60a0e9698c4b329a86600bd8bb256a-fast` | 2340 | 1 unrelated `AgentConsoleLiveInputSourceTests` race | 0 | 0 | Complete |
| External timing failure isolated GREEN | focused exact `AgentConsoleLiveInputSourceTests.EnqueueLine_WhenReadKeyIsPending_RejectsWithoutPoisoningQueue` | `TestResults/test-lanes/20260813-154632-289-49216-4a74cbb62239401eb813dcd1867b76bf-focused` | 1 | 0 | 0 | 0 | Complete |
| Review source-storage movement item RED | focused Integration `StoragePlacement_AuthorizedMovementSealsRawSourceItemInOffscreenCarrier` | `TestResults/test-lanes/20260813-221557-222-20688-4b4320d0358c46a5ac1fdec6eb75dfe7-focused` | 0 | 1 expected post-seal identity conflict | 0 | 0 | Complete |
| Review source-storage movement item GREEN | same focused Integration test | `TestResults/test-lanes/20260813-221709-125-44960-be3fc71962184bfab1c22e268b4a08f5-focused` | 1 | 0 | 0 | 0 | Complete |
| Review item-owned `itemIds` projection RED | full focused `MortalItemMaterializationValidationTests` | `TestResults/test-lanes/20260813-222059-060-43820-bce53a75036c4d3982d8a8160d204c22-focused` | 94 | 1 current/map projection mismatch | 0 | 0 | Complete |
| Review item-owned `itemIds` projection GREEN | focused `SameTurnReference_ResolvesOnlyToPermanentItemId` | `TestResults/test-lanes/20260813-222622-642-56460-1a7e3cbfe0414b76a520c4021bfdbcfd-focused` | 7 | 0 | 0 | 0 | Complete |
| T098 final Mortal-item integration GREEN | full focused `MortalItemMaterializationValidationTests` | `TestResults/test-lanes/20260813-222741-090-55020-fa8ce78f5110422b912a802b7425db3f-focused` | 95 | 0 | 0 | 0 | Complete |
| T098 final Fast GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` | `TestResults/test-lanes/20260813-223114-013-52152-fc0c036057d84738bddf02114e792737-fast` | 3165 | 0 | 0 | 0 | Complete |
| T098 final LifecycleIntegration GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration` | `TestResults/test-lanes/20260813-223506-365-54820-5dc3b9563bc041ffa3e2b909dc375b27-lifecycleintegration` | 243 | 0 | 0 | 0 | Complete |
| T098 final FullValidation GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` | `TestResults/test-lanes/20260813-224107-637-9272-4e1712699a9c4b1bb5175a24a6cade17-fullvalidation` | 1792 | 0 | 0 | 0 | Complete |
| T100 rebased-candidate PreMerge fixture failure | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` | `TestResults/test-lanes/20260813-225152-191-12900-6af39fa5cbb747de8173e898801c4e49-premerge` | 109 | 1 stale browser location fixture; two cancelled sibling shards logged the same fixture class | 0 | 0 | Complete |
| T100 stale browser location fixtures GREEN | focused Integration filter for world-news detail, `/where_am_i`, and storage/vehicle prompt presentation | `TestResults/test-lanes/20260813-225659-989-55064-466ca17309db426a8ac098d408168a48-focused` | 4 | 0 | 0 | 0 | Complete |
| T100 second rebased-candidate PreMerge fixture failure | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` | `TestResults/test-lanes/20260813-225817-137-55912-31ce559afc534a84894c29b4e876f573-premerge` | 441 | 2 stale browser location/weather fixture assertions | 0 | 0 | Complete |
| T100 remaining browser fixture corrections GREEN | focused Integration weather/privacy and accepted location-storage detail filter | `TestResults/test-lanes/20260813-230325-081-28396-953130eacb7a4dae89ff6281ccede369-focused` | 2 | 0 | 0 | 0 | Complete |
| T100 complete browser command presentation GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerWebCommandServiceTests"` | `TestResults/test-lanes/20260813-230408-769-14440-266b0d6727d2437ca6b4ffc6f19b480a-focused` | 475 | 0 | 0 | 0 | Complete |
| T100 third rebased-candidate PreMerge external guard failure | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` | `TestResults/test-lanes/20260813-230814-834-44144-78622b89dda64540b0aa7285293d4203-premerge` | 2034 | 1 #1525 LICENSE raw-byte hash mismatch under Windows `core.autocrlf=true` | 0 | 0 | Complete |
| #1525 cross-platform publication guard GREEN | focused Fast `RepositoryPublicationDocumentationTests` | `TestResults/test-lanes/20260813-231559-336-33216-cfd68d230c054f05ac4a73fe29cfd17f-focused` | 7 | 0 | 0 | 0 | Complete |
| T100 fourth rebased-candidate PreMerge fixture failure | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` | `TestResults/test-lanes/20260813-231642-056-4268-8a60e246f4624b5894fba7020091c0bf-premerge` | 4171 | 3 stale console storage fixtures | 0 | 0 | Complete |
| T100 complete console command presentation GREEN | `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests"` | `TestResults/test-lanes/20260813-233206-639-34692-d1e982a0ff144dfd818d55847246e866-focused` | 370 | 0 | 0 | 0 | Complete |

For T076, `ValidationService.MortalFactionMaterialization` owns the exact
receipt-bearing canonical map plus validated same-turn location authority.
`CanonicalStateNormalizer.Factions` has no separate location-authority reader,
so no parallel normalizer index was retained or introduced.

The plan's fourth and fifth commands exceeded the five-minute Focused lane when
combined. They were therefore split along project/class and changed-boundary
contours without changing filters, assertions, or coverage ownership; the
single Fast and final PreMerge controls retain full-suite coverage.

The first Fast attempts exposed stale current-location trade fixtures, a
misspelled pending-snapshot-authority protected path, and source/document guards
that still described the pre-hardening control flow. Each was closed with the
smallest related Focused filter. One later parallel attempt hit two unrelated
live-input/worker timing flakes; both passed together in isolation (2/2 at
`TestResults/test-lanes/20260813-135448-713-12780-e02da83ff0fa4190a29dcf1353ed4598-focused`)
before the complete 3114-test Fast run passed.

After the review fixes, two Fast attempts again reached the same unchanged
`AgentConsoleLiveInputSourceTests.EnqueueLine_WhenReadKeyIsPending_RejectsWithoutPoisoningQueue`
race under parallel load while every other completed Fast test passed. The
exact test passed in isolation, and no Agent Console source or test is part of
the #1513 diff. This external timing risk is recorded without expanding the
location task; the final PreMerge remains the authoritative current-candidate
suite gate.

The integrated Fast checkpoint, conditional
FullValidation/LifecycleIntegration evidence, and the single final PreMerge
control are appended here as the corresponding tasks complete.

The T093 artifact audit completed before final review. `git diff --check`
returned exit code 0 with no whitespace errors or unresolved merge entries.
All 14 untracked paths were inspected and are intentional #1513 source files,
tests, canonical world-state examples, or the shared Mortal-location fixture
baseline. No feature-owned temporary, backup, reject, or patch file remained.
Ignored `bin`, `obj`, `TestResults`, and dependency directories were preserved;
they are local build/test evidence rather than publication inputs.

## 13. Final requirement reconciliation

The T099 reconciliation counted exactly 60 functional requirements, 12 success
criteria, 108 tasks, and 55 path rows in the fixture inventory. There are no
unresolved placeholders. Every inventory path either exists in its final role
or is explicitly marked as a retired/removed legacy fixture.

| Requirement range | Owning implementation and verification |
|---|---|
| FR-001–FR-010 | US1 contract, exact identity/index, canonical map/current ownership, no-legacy validation, and T011–T028 evidence |
| FR-011–FR-019 | Exclusive current/remote creation routes and neutral bootstrap reservations in T013–T037 |
| FR-020–FR-033 | Complete envelope, physical/discovery/section semantics, companions, storage metadata, and topology disposition in T011–T033 and T070–T080 |
| FR-034–FR-041 | Exact directed links, pre-turn authorized existing movement, derived topology, narrow lifecycle, all six governed storage/threat commands, immutable creation evidence, conflict/confusable rejection, and T038–T048/T102 evidence |
| FR-042–FR-050 | Composed-state validation, one bounded transaction, byte-exact rollback, protected authority, coherent full-turn repair resubmission, and scaling controls in T049–T059 |
| FR-051–FR-060 | Accepted-only console/browser/news projections, recursive privacy, player-safe failures, synchronized GM guidance/examples/manifest, current-schema fixtures, and explicit #1514 afterlife isolation in T060–T092 |

| Success criterion | Evidence owner |
|---|---|
| SC-001 | Complete current/remote creation and missing-section route parity tests in T013–T028 |
| SC-002 | Bootstrap start/neighbor/link and narrative-only exit tests in T029–T037 |
| SC-003 | T081–T082 active-state and 55-row fixture migration audit |
| SC-004 | Directed/one-way/portal/hidden/sealed/isolated topology tests in T038–T048 |
| SC-005 | Exact current selection plus no-link/reverse-only/hidden/conditional/sealed rejection, narrow update, immutable receipt, and full-resend rejection tests in T038–T048/T102 |
| SC-006 | Actor/faction/lore/threat/storage/topology exact-reference tests in T070–T080 |
| SC-007 | Eight-path injected publication failure and post-check rollback evidence in T049–T059 and LifecycleIntegration |
| SC-008 | Bounded packet, protected target, ambiguity, history, and diagnostic-only tests in T053–T059 |
| SC-009 | Console/browser/news semantic parity and recursive privacy tests in T060–T069 |
| SC-010 | Deterministic doubled-population work bound in T017 |
| SC-011 | Five worked Mortal flows plus receipt-less rejection and documentation/source/manifest validation in T083–T091/T102 and FullValidation |
| SC-012 | Focused, Fast, FullValidation, and LifecycleIntegration are green; the single clean-candidate PreMerge remains exclusively owned by T100 |

The final requesting-code-review gate reported no Critical or Important
findings and returned `Ready: Yes`. The last two review findings were closed
with explicit RED-to-GREEN evidence: raw source-storage creations survive an
authorized location move and are sealed in client-owned offscreen state, while
exact `locationStorages[].itemIds` remains item-owned and is excluded only from
the current/map metadata comparison. A follow-up delta review confirmed that
all other storage/location fields remain exact and that item references still
pass through the separate item-authority catalog and reconciliation path.

The task audit leaves only T100 (one final PreMerge) and T101
(commit/PR/owner approval) open. Mortal GM prompts, rules, CLI guidance,
daemon reminders, worked examples, manifest entries, and source guards are all
present in the same candidate. The detailed T092 rationale above remains the
authoritative afterlife disposition: #1513 changes no Chaos Sea or Shining
Abode contract, and #1514 owns their later location materialization work.
