# Local Web Host

Tracked tasks: #565, #567, #569, #570, #571  
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

Mortal World mutating commands remain blocked in the browser until the local-turn write UX is implemented in #574:

- `/distribute`
- `/companion_directive`
- `/faction_directive`
- `/craft`

Migrated Chaos Sea read-only surfaces currently include:

- `/chaos_sea`, `/море_хаоса`
- `/guardians`, `/хранители`
- `/abode_power`, `/сила_обители`
- `/guardian_projects`, `/проекты_хранителей`
- `/abodes`, `/обители`
- `/gacha`, `/гача`

Chaos Sea pending-contract commands remain blocked in the browser until the local-turn write UX is implemented in #574:

- `/abode_offering`, `/подношение_обители`
- `/found_guardian_mantle`, `/учредить_хранителя`

Interactive multi-step prompt submission, Shining Abode command migration, lifecycle/local-turn operations such as `/validate` and `/world_setup`, and QTE support are tracked by the follow-up Web UI issues.

