# Console Client Polish Pass 2

Source issue: #1163 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1163

Audit date: 2026-06-20

## Scope

This pass covers non-browser player-facing console readiness work. Browser UI
parity and visual redesign are intentionally out of scope.

Primary reusable saves and modes:

- Mortal World: `FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture.zip`
- Chaos Sea: `FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture.zip`
- Shining Abode: `FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture.zip`

## Mortal World Command Audit

The reusable Mortal World save was loaded through `SaveLoadService`, validated
with `ValidationService`, executed through `ExplorerWebCommandService`, and
rendered through `ExplorerCommandResultConsoleRenderer`.

Covered command groups:

| Command group | Commands covered | Result |
| --- | --- | --- |
| Inventory and item actions | `/inv`, `/inventory`, `/инв`, `/инвентарь`, equip/unequip/drop/split/merge aliases | Player-facing result renders without raw JSON blocks or technical file markers. |
| NPCs and social entry points | `/npc`, `/npcs`, `/characters`, `/нпс`, `/персонажи`, `/npc_talk`, `/поговорить_с_нпс` aliases | Player-facing overview/prompt output renders without raw JSON blocks or technical file markers. |
| Quests, map, locations, travel | `/quests`, `/квесты`, `/map`, `/карта`, `/where_am_i`, `/где_я`, `/locations`, `/локации`, `/transport`, `/транспорт` | Player-facing output renders and preserves read-only detail/action surfaces where applicable. |
| Character state | `/status`, `/статус`, `/skills`, `/навыки`, `/stats`, `/характеристики`, `/effects`, `/эффекты` | Player-facing output renders without malformed markup or raw JSON blocks. |
| World and social context | `/world_news`, `/новости_мира`, `/factions`, `/фракции`, `/rival_threads`, `/чужие_нити`, `/guardian_corrections`, `/коррективы_хранителя`, `/interactions`, `/взаимодействия` | Player-facing output renders without raw JSON blocks; world-news visibility enum localization was fixed in this pass. |
| Combat and utility | `/combat`, `/бой`, `/weather`, `/погода`, `/books`, `/книги`, `/storage_access`, `/доступ_к_хранилищам`, `/craft`, `/ремесло` | Player-facing output renders without raw JSON blocks or technical file markers. |
| Universal previews in mortal mode | `/help`, `/статус`, `/душа`, `/достижения`, `/хроника`, `/story`, `/поведение`, `/жизни`, `/перья`, `/кодекс`, `/правила_мира`, `/галерея`, `/моды`, `/валидация` | Player-facing output renders through the same fixture harness. |

## Fixes From This Pass

### World News Visibility Enum Localization

Problem:

- The reusable Mortal World save stores world-news visibility as canonical
  values such as `local` and `rumor`.
- Overview/detail output was otherwise player-facing, but selected details could
  still show the raw English value in the status row.

Fix:

- `ExplorerMortalWorldNewsCommandResultBuilder` now maps common visibility
  values to Russian player-facing labels, including `local` -> `местные новости`
  and `rumor` -> `слух`.
- `MortalCommandDisplaySaveTests` now loads the reusable Mortal save and checks
  both overview and selected event details for raw visibility leakage.

Verification:

- RED: `LoadedMortalCommandDisplaySave_WorldNewsLocalizesVisibilityEnums` failed
  before the builder change because `/новости_мира событие ...` exposed `local`
  or `rumor`.
- GREEN: the same test passed 4/4 after the localization change.
- Reusable Mortal command fixture suite passed 96/96 after the fix.

## Afterlife Output Sweep Links

The afterlife sweep in this branch made one narrow player-facing fix and split
larger audit-mode work into follow-up issues:

- #1167 - split afterlife `/status` into player summary and explicit audit mode.
- #1168 - split Shining Abode player details from audit payloads.
- #1169 - split afterlife action previews from GM contract audit payloads.

## Remaining Work

The next required step for #1163/#1166 is the second live console playtest with a
GM bridge and Codex worker. That playtest should focus on qualitative friction:
unclear text, dead-end menus, raw enum leakage missed by fixture tests, and
whether a player can continue without manual JSON/file inspection.
