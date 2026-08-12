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

## 12. Implementation evidence

The pre-implementation control was run from the isolated feature worktree after
the specification and plan were committed. It is the accepted baseline for the
TDD slices below; it is recorded here rather than repeated.

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

The integrated Fast checkpoint, conditional
FullValidation/LifecycleIntegration evidence, and the single final PreMerge
control are appended here as the corresponding tasks complete.
