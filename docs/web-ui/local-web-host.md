# Local Web Host

Tracked tasks: #565, #567, #569, #570, #571, #572, #573, #574
Parent epic: #559

## Local-Only Model

The browser UI is a local shell over the same C# game client and the same `game_session` data. It is not a cloud service, does not require an account, and binds to loopback addresses only.

Default URL:

```text
http://127.0.0.1:8787
```

The host rejects non-loopback bind addresses such as `0.0.0.0`.

## Launch

From the repository root:

```powershell
dotnet run --project BookOfEternityClient -- --web
```

Use a custom session root with the existing base-path argument:

```powershell
dotnet run --project BookOfEternityClient -- "E:\Games\The Book of Eternity Reborn\BookOfEternityClient" --web
```

Use a custom local URL:

```powershell
dotnet run --project BookOfEternityClient -- --web --web-url http://127.0.0.1:8788
```

Console mode remains the default:

```powershell
dotnet run --project BookOfEternityClient
```

## Current Browser MVP

The local host exposes:

```text
GET /
GET /api/health
GET /api/session
POST /api/explorer/command
```

`/` serves the first browser command shell. It renders `ExplorerCommandResult` DTOs from the command API and does not duplicate game logic in JavaScript.

The renderer currently supports these DTO surfaces:

- `text`, `panel`, `table`, `list`, `keyValueGrid`, `message`, and `rawJson` blocks.
- `notifications` as message cards.
- `actions` as command buttons when an action has a direct command.
- `prompts` as read-only prompt cards showing prompt text, kind, requirement flag, and selection options.
- empty, loading, HTTP error, and command failure states.

`/api/health` and `/api/session` return local session metadata: status, local-only flag, base path, `game_session` path, and whether the directory exists.

`/api/explorer/command` accepts a JSON body:

```json
{
  "command": "/help"
}
```

It returns an `ExplorerCommandResult` DTO. Migrated commands are executed through browser-safe DTO builders; planned, unknown, or blocked commands return structured `Blocked`/`Failed` DTOs instead of invoking console-bound handlers.

Migrated universal/meta read-only surfaces currently include:

- `/help`, `/помощь`
- `/status`, `/статус`
- `/soul`, `/душа`, `/soul_relics`, `/реликвии`, `/afterlife_archive`, `/архив_души`, `/archive_candidates`, `/архив_кандидаты`, `/soul_quests`, `/квесты_души`
- `/codex`, `/кодекс`, `/achievements`, `/достижения`, `/chronicle`, `/хроника`, `/story`, `/рассказ`, `/история`, `/behavior`, `/поведение`, `/lives`, `/жизни`, `/feathers`, `/перья`, `/world_rules`, `/правила_мира`
- `/gallery`, `/галерея`, `/gm`, `/гм`, `/debug`, `/отладка`, `/mods`, `/моды`, `/system_guardians`, `/системные_хранители`, `/извечные_хранители`
- `/saref`, `/сареф`, `/saref_story`, `/история_сарефа`, `/wings_of_angels`, `/крылья_над_бездной` as the read-only hidden main-story view. The mutating Wings search subcommand remains outside this read-only migration.

Migrated Mortal World read-only surfaces currently include:

- `/inv`, `/inventory`, `/инв`, `/инвентарь`
- `/npc`, `/npcs`, `/characters`, `/нпс`, `/персонажи`
- `/quests`, `/квесты`
- `/map`, `/карта`, `/where_am_i`, `/где_я`
- `/factions`, `/фракции`
- `/skills`, `/навыки`, `/stats`, `/статы`, `/характеристики`
- `/world_news`, `/новости_мира`
- `/rival_threads`, `/чужие_нити`
- `/guardian_corrections`, `/коррективы_хранителя`
- `/locations`, `/локации`, `/transport`, `/транспорт`
- `/effects`, `/эффекты`, `/combat`, `/бой`
- `/weather`, `/погода`
- `/books`, `/книги`, `/читать`
- `/storage_access`, `/доступ_к_хранилищам`
- `/interactions`, `/взаимодействия`

Migrated Mortal World local-turn protocol surfaces currently include:

- `/distribute`
- `/companion_directive`
- `/faction_directive`
- `/craft`

These return `ExplorerCommandResult` DTOs with local GM-turn status, target lists, current raw JSON where useful, and input prompts. They do not yet commit multi-step prompt submissions from the browser; that interactive submit layer is tracked by #575.

Migrated Chaos Sea read-only surfaces currently include:

- `/chaos_sea`, `/море_хаоса`
- `/guardians`, `/хранители`
- `/abode_power`, `/сила_обители`
- `/guardian_projects`, `/проекты_хранителей`
- `/abodes`, `/обители`
- `/gacha`, `/гача`

Migrated Chaos Sea pending-contract protocol surfaces currently include:

- `/abode_offering`, `/подношение_обители`
- `/found_guardian_mantle`, `/учредить_хранителя`

These show the pending contract state, active GM-turn blockers, target choices, and browser DTO prompts. Destructive local writes such as consuming a Soul Relic or Archive entry still require the interactive/write protocol tracked by #575 before the browser can submit them directly.

Migrated Shining Abode read-only surfaces currently include:

- `/shining_abode`, `/сияющая_обитель`
- `/shining_politics`, `/сияющая_политика`
- `/shining_treasury`, `/казначейство`
- `/source_of_light`, `/источник_света`

The browser versions of `/shining_treasury` and `/source_of_light` are status-only surfaces for now. They do not mutate feathers, sparks, pending files, or capstone request state until the interactive/write protocol is implemented in #575.

Migrated afterlife combat and entity read-only surfaces currently include:

- `/afterlife_profiles`, `/профили_загробья`
- `/afterlife_inbox`, `/уведомления_загробья`
- `/spiritual_conflict`, `/духовный_конфликт`
- `/spiritual_combat_log`, `/журнал_духовного_боя`
- `/spiritual_combat_help`, `/духовный_бой`
- `/spiritual_arts`, `/духовные_искусства`

The browser afterlife combat/entity status surfaces are read-only. `/afterlife_inbox` does not mark notifications as read and `/spiritual_arts` does not perform local upgrades.

Migrated lifecycle/local-turn protocol surfaces currently include:

- `/validate`, `/валидация`: runs the same `ValidationService` as the console command and renders grouped issues.
- `/world_setup`, `/настройка_мира`: shows `incarnation_world_setup.json`, `next_life_scenario_core.json`, current realm, and browser prompts for future editing/clearing.
- `/spiritual_action`, `/духовное_действие`: shows active afterlife conflict state, response surface, and prompts for the player's spiritual action route tag.

Every migrated local-turn protocol result includes a `Локальный ход / GM-turn protocol` panel. It reports whether these artifacts exist:

- `input/turn_request.json`
- `ready/turn_complete.json`
- `ready/turn_error.json`
- `game_state/control/pending_turn_snapshot.json`
- `game_state/control/pending_turn_snapshot/`
- `game_state/control/explorer_local_turn_rollback/`

If any active GM-turn or rollback/snapshot artifact exists, local-turn command DTOs return `Pending` so the browser can observe the long-running or late-response state without invoking console-only prompts.

Interactive multi-step prompt submission and QTE support are tracked by the follow-up Web UI issue #575.

