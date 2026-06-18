# Mortal Command Fixture Matrix

Source issues: #1092 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1092; #1095 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1095

This matrix is the durable checklist for the ignored local `BookOfEternityClient/game_session` fixture and the tracked reusable #1095 save package.

## Reusable #1095 Save Package

- Source archive: `FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture.zip`
- Sidecar metadata: `FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture_metadata.json`
- Internal save name: `Mortal World Command Display Fixture (#1095)`
- Normal load path: copy the source archive into `BookOfEternityClient/game_session/saves/manual_saves/`, then load it from the console/browser manual-save list.
- Repeatability rule: the live-session copy is disposable because the loader replaces `game_session`; recopy from the tracked source archive for repeated manual QA. Automated tests load the tracked source archive directly into disposable roots.
- Clean-checkout dependency: validation of the save relies on the tracked `BookOfEternityClient/system_guardians` built-in preset library, including the `azalia` Eternal Guardian preset referenced by the rival-thread fixture.

## Verification Evidence

Last verified against the local ignored fixture on 2026-06-18:

- JSON/JSONL syntax: `81 json, 1 jsonl`, all parsed successfully.
- `ValidationService.ValidateGameStateAsync()`: `VALIDATION ISSUES 0`.
- Browser command service smoke: all 77 Mortal World aliases returned non-failed, non-blocked command results.
- Practical universal Mortal preview smoke: 14/14 commands returned non-failed, non-blocked command results.
- Console command renderer smoke: 91 command results rendered without Spectre markup exceptions.

Last verified against the tracked reusable #1095 save on 2026-06-18:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalCommandDisplaySaveTests" --logger "console;verbosity=minimal"`: 92 passed / 0 failed / 0 skipped / 92 total.
- `ValidationService.ValidateGameStateAsync()`: zero `IssueSeverity.Error` blocking issues after load in a clean-checkout-like disposable root with tracked `system_guardians`.
- Browser command service smoke: all 77 cataloged Mortal World aliases plus 14 practical universal Mortal preview commands returned non-failed, non-blocked, non-empty command results.
- Console command renderer smoke: all 91 covered command results rendered without Spectre markup exceptions.

## Cataloged Mortal World Commands

| Command id | Mode | Representative command | Fixture expectation | Status |
| --- | --- | --- | --- | --- |
| inventory | ReadOnly | `/инв` | Inventory has equipped items, equippable items, stackable items, readable documents, resources, structural bonuses, and durability examples. | covered |
| npcs | ReadOnly | `/нпс` | NPC core and related NPC files contain several NPCs with thoughts, quests, memory, inventory, effects, relationships, activities, and detail-worthy fields. | covered |
| npc_talk | LocalTurn | `/поговорить_с_нпс` | At least one visible NPC can be selected for a conversation prompt. | covered |
| quests | ReadOnly | `/квесты` | Active, completed, historical, and plot-outline quests contain named entries and at least one detail target. | covered |
| map | ReadOnly | `/карта` | Current mortal map has multiple locations, links, and current-location data. | covered |
| where_am_i | ReadOnly | `/где_я` | Current location has name, region, description, and useful context. | covered |
| factions | ReadOnly | `/фракции` | Faction core/projects/chronicles/custom state include several factions and detail targets. | covered |
| skills | ReadOnly | `/навыки` | Active and passive skills include named skill examples and detail targets. | covered |
| stats | ReadOnly | `/характеристики` | Player status and characteristics files show non-empty core/computed stats. | covered |
| world_news | ReadOnly | `/новости_мира` | World events/news contain recent entries and details. | covered |
| rival_threads | ReadOnly | `/чужие_нити` | Rival soul arcs include at least one active rival thread. | covered |
| guardian_corrections | ReadOnly | `/коррективы_хранителя` | Guardian correction entries include at least one inspectable correction. | covered |
| locations | ReadOnly | `/локации` | Location references include multiple locations, exits, services, and detail targets. | covered |
| transport | ReadOnly | `/транспорт` | Transport data includes at least one vehicle or route and storage/cargo context. | covered |
| effects | ReadOnly | `/эффекты` | Active effects, wounds, and temporary conditions have names, descriptions, duration, and mechanical data. | covered |
| combat | ReadOnly | `/бой` | Enemies, allies, and combat log have representative entries and detail targets. | covered |
| weather | ReadOnly | `/погода` | World time and weather have current state and description. | covered |
| books | ReadOnly | `/книги` | Readable inventory documents and text sources include selectable books with readable content. | covered |
| storage_access | ReadOnly | `/доступ_к_хранилищам` | Storage access includes at least one storage entry, access state, and detail target. | covered |
| interactions | ReadOnly | `/взаимодействия` | Player interactions include records with NPC/object interaction details. | covered |
| ink_feather_reveal_fate | LocalTurn | `/открыть_судьбу` | Soul state has enough ink feathers for a previewable fate action. | covered |
| ink_feather_rewrite_fate | LocalTurn | `/переписать_судьбу` | Soul state has enough ink feathers for a previewable rewrite action. | covered |
| distribute | LocalTurn | `/распределить` | Stat points and characteristics show allocatable points or a clear no-points state. | covered |
| companion_directive | LocalTurn | `/директива_компаньону` | NPC data includes at least one companion. | covered |
| faction_directive | LocalTurn | `/директива_фракции` | Faction data includes at least one controllable faction or a clear unavailable state. | covered |
| inventory_equip | LocalTurn | `/экипировать` | Inventory contains at least one equippable unequipped item. | covered |
| inventory_unequip | LocalTurn | `/снять` | Equipment contains at least one equipped ordinary item. | covered |
| inventory_drop | LocalTurn | `/выбросить_предмет` | Inventory contains a droppable item. | covered |
| inventory_split | LocalTurn | `/разделить_стопку` | Inventory contains a stackable item with quantity greater than one. | covered |
| inventory_merge | LocalTurn | `/объединить_стопки` | Inventory contains compatible split stacks. | covered |
| storage_item_move | LocalTurn | `/хранилище_предметы` | Inventory and storage access contain movable items/storage. | covered |
| vehicle_item_move | LocalTurn | `/транспорт_предметы` | Inventory and transport contain movable items/vehicle cargo. | covered |
| npc_trade | LocalTurn | `/торговля_нпс` | At least one NPC has trade inventory/offers. | covered |
| craft | LocalTurn | `/ремесло` | Inventory/resources include craftable materials or a clear no-recipe state. | covered |

## Practical Universal Mortal Preview Commands

| Command id | Representative command | Fixture expectation | Status |
| --- | --- | --- | --- |
| help | `/help` | Help reflects Mortal World command availability. | covered |
| status | `/статус` | Status shows health, energy, poise, effects, incarnation, resources, and current state. | covered |
| soul | `/душа` | Soul state has readable realm, lives, feathers, and persistent data. | covered |
| achievements | `/достижения` | Achievements contain at least one permanent and one ordinary example. | covered |
| chronicle | `/хроника` | Character chronicle has readable entries. | covered |
| story | `/story` | Story output has current narrative context. | covered |
| behavior | `/поведение` | Player behavior data has representative traits/statistics. | covered |
| lives | `/жизни` | Life history has current and past-life data. | covered |
| feathers | `/перья` | Ink feather state is visible and non-zero. | covered |
| codex | `/кодекс` | Lore codex has at least one readable entry. | covered |
| world_rules | `/правила_мира` | Current world directives/rules are present. | covered |
| gallery | `/галерея` | Gallery has at least one scene image reference. | covered |
| mods | `/моды` | System mod data is present. | covered |
| validate | `/валидация` | Validation output can be run against the fixture. | covered |
