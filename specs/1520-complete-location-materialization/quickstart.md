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
- Names and coordinates never identify a location or link.
- `knownExits` and `adjacencyMap` are derived, never authored.
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
        "ownerActorId": null,
        "capacity": { "slots": 12 },
        "access": { "state": "open", "reason": "Сундук выделен герою." },
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
inhabitants, topology, envelope, or receipt. The client rebuilds shared fields
from `world_map.locations[]` and visible exits from `world_map.links[]`.

## 7. Same-turn actor, faction, lore, and storage references

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

Map storage is semantic metadata. Current storage may include complete raw item
creation objects, each governed independently by #1511. The location-owned
portion of such a storage remains:

```json
{
  "storageId": "storage_inn_guest_chest",
  "name": "Гостевой сундук",
  "description": "Запираемый сундук у комнаты героя.",
  "ownerActorId": null,
  "capacity": { "slots": 12 },
  "access": { "state": "open", "reason": "Сундук выделен герою." },
  "contents": []
}
```

When `contents` is non-empty, every member is the full raw #1511 item creation
object from that contract—never a location-defined abbreviated item. Location
normalization accepts the storage and preserves each raw item object; item
normalization then assigns item identity and receipt. A location repair does not
repair the item.

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
