# Local Web Host

Tracked tasks: #565, #567, #569, #570, #571, #572, #573, #574, #576, #577, #585, #586, #587, #588, #589, #590, #591, #592, #593, #594
Parent epic: #559

## Local-Only Model

The browser UI is a local shell over the same C# game client and the same `game_session` data. It is not a cloud service, does not require an account, and binds to loopback addresses only.

Console mode and browser mode are two frontends over one local save/session root. If both modes are launched with the same base path, they read and write the same files under:

```text
<base path>/game_session/
```

Use different base paths only when you intentionally want different local saves.

Default URL:

```text
http://127.0.0.1:8787
```

The host rejects non-loopback bind addresses such as `0.0.0.0`.

## Launch

### Console Mode

Console mode remains the default. From the repository root:

```powershell
dotnet run --project BookOfEternityClient
```

To use an explicit local session root, pass the existing base-path argument:

```powershell
dotnet run --project BookOfEternityClient -- "E:\Games\The Book of Eternity Reborn\BookOfEternityClient"
```

### Browser Mode

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

Open the printed local URL in the browser, normally:

```text
http://127.0.0.1:8787
```

The URL must stay local. `localhost`, `127.0.0.1`, and other loopback addresses are valid; public bind addresses such as `0.0.0.0` are rejected by the host.

Use the same base path as console mode when you want the browser to continue the same save.

## Current Browser MVP

The local host exposes:

```text
GET /
GET /api/health
GET /api/session
GET /api/lifecycle/dashboard
POST /api/lifecycle/validate
POST /api/explorer/command
GET /api/explorer/prompt-sessions/{sessionId}
POST /api/explorer/prompt-sessions/submit
POST /api/explorer/prompt-sessions/cancel
GET /api/qte/state
POST /api/qte/offer
POST /api/qte/action
```

`/` serves the first browser command shell. It renders `ExplorerCommandResult` DTOs from the command API and does not duplicate game logic in JavaScript.

The renderer currently supports these DTO surfaces:

- `text`, `panel`, `table`, `list`, `keyValueGrid`, `message`, and `rawJson` blocks.
- `notifications` as message cards.
- `actions` as command buttons when an action has a direct command.
- `prompts` as browser form cards when an `interactiveSession` is present, otherwise as read-only prompt cards showing prompt text, kind, requirement flag, and selection options.
- empty, loading, HTTP error, and command failure states.

`/api/health` and `/api/session` return local session metadata: status, local-only flag, base path, `game_session` path, whether the directory exists, and the browser write-owner state. The write state includes:

- `canStartBrowserWrite`: false when a GM turn, rollback/snapshot artifact, or active non-stale UI lock blocks local writes.
- `pendingTurn`: the actionable list of GM-turn and rollback artifacts the player/repair flow must resolve first.
- `localUiLock`: current owner, kind, heartbeat, lease, stale/readable flags, and last operation for stale lock recovery.

`/api/lifecycle/dashboard` feeds the browser **Панель состояния**. It combines the same local session status with a lightweight soul summary, pending-turn artifacts, local UI lock state, validation summary, and Russian repair/continue guidance. The browser uses this endpoint to show whether the save is in ordinary play, waiting for a GM turn, ready for accepted-turn processing, blocked by a turn error, or blocked by validation errors. The endpoint is informational: it does not mutate the save.

`POST /api/lifecycle/validate` runs the same `ValidationService` used by console mode and returns grouped validation issues for browser rendering. The response includes total issue/error/warning counts, groups by severity/category/section, and a bounded issue list with file path, code, message, expected/actual values, and repair hints where available. This is the browser lifecycle entrypoint for repair triage; it does not replace the console repair flow yet.

`/api/explorer/command` accepts a JSON body:

```json
{
  "command": "/help"
}
```

It returns an `ExplorerCommandResult` DTO. The command API uses the shared Explorer slash-command parser: the raw input is separated into canonical command identity, alias token, arguments, and recognized subcommand where applicable. This means browser calls may use the same base command aliases, argument tails, and supported subcommands as console-oriented command metadata. Browser-executable commands are executed through browser-safe DTO builders; planned, unknown, malformed, or blocked commands return structured `Blocked`/`Failed` DTOs in Russian instead of invoking console-bound handlers.

If a migrated command returns `RequiresInput`, the browser host creates a local prompt session and attaches:

```json
{
  "interactiveSession": {
    "sessionId": "prompt_...",
    "submitEndpoint": "/api/explorer/prompt-sessions/submit",
    "cancelEndpoint": "/api/explorer/prompt-sessions/cancel",
    "requiresLocalUiLock": true,
    "ownerId": "browser:..."
  }
}
```

Prompt sessions let the browser pause, resume by `GET /api/explorer/prompt-sessions/{sessionId}`, submit answers, or cancel. Sessions for potentially mutating local-turn commands acquire the same `game_state/control/local_ui_session_lock.json` ownership used by console mode. Invalid submissions return the original prompts plus validation notifications instead of invoking Spectre.Console.

`POST /api/explorer/prompt-sessions/submit` accepts:

```json
{
  "sessionId": "prompt_...",
  "answers": {
    "world_setup_mode": "create_or_edit",
    "world_title": "Королевство пепельных колоколов"
  }
}
```

For the first interactive protocol layer, successful submissions complete the browser prompt session and return a DTO containing the accepted answers. Domain-specific file writes are still migrated command-by-command in the later browser parity tasks.

`/api/qte/state` exposes the current local QTE state. It returns one of:

- `Offer`: `output/qte_offer.json` is pending and the browser can accept or decline it.
- `Active`: `game_state/control/qte_runtime.json` has an accepted scene and exposes the current chapter/action choices.
- `Completed`: the last submitted action reached a terminal outcome.
- `Declined`, `Failed`, or `NoScene` for the corresponding local states.

`POST /api/qte/offer` accepts `{ "decision": "accept" }` or `{ "decision": "decline" }`. Accepting starts the QTE runtime without invoking console prompts; declining records the decline in the same runtime file and clears the ready signal files.

`POST /api/qte/action` accepts `{ "actionId": "...", "grade": "success|partial|fail" }`. Branch-choice actions can omit `grade`; timed/check actions currently submit the resolved grade from the browser UI. Terminal outcomes use the same local state distributor and normalizer as console QTE. The browser path tolerates pre-existing unrelated validation errors in the save, but still rejects new validation errors introduced by the QTE outcome.

Browser command parity is tracked per command alias, not as a single coarse migrated flag:

- `read-only parity`: the browser can execute the same non-mutating command surface as console and render the shared DTO.
- `interactive form pending`: the browser can parse the command and render status/prompt DTOs, but the full domain write/repair flow is still tracked by the relevant follow-up task.
- `status-only`: the browser shows current state for a command that mutates in console, but intentionally does not perform the mutation yet.
- `mutating parity`: the browser owns the same write path, lock behavior, pending-turn behavior, and rollback behavior as console.
- `planned`, `blocked`, and `console-only temporarily`: the command is known but not browser-executable yet, with an explicit follow-up or reason.

Universal/meta read-only parity currently includes:

- `/help`, `/помощь`
- `/status`, `/статус`
- `/soul`, `/душа`, `/soul_relics`, `/реликвии`, `/afterlife_archive`, `/архив_души`, `/archive_candidates`, `/архив_кандидаты`, `/soul_quests`, `/квесты_души`
- `/codex`, `/кодекс`, `/achievements`, `/достижения`, `/chronicle`, `/хроника`, `/story`, `/рассказ`, `/история`, `/behavior`, `/поведение`, `/lives`, `/жизни`, `/feathers`, `/перья`, `/world_rules`, `/правила_мира`
- `/gallery`, `/галерея`, `/math`, `/математик`, `/gm`, `/гм`, `/debug`, `/отладка`, `/mods`, `/моды`, `/system_guardians`, `/системные_хранители`, `/извечные_хранители`
- `/saref`, `/сареф`, `/saref_story`, `/история_сарефа`, `/wings_of_angels`, `/крылья_над_бездной` as the read-only hidden main-story view. The mutating Wings search subcommand remains outside this read-only migration.
- `/воспоминание`, `/воспоминание_статус`, `/воспоминание_начать`, `/воспоминание_способности`, plus spaced subcommand forms such as `/воспоминание начать`, as the read-only Saref memory-scene view.

`/math` and `/математик` are read-only calculator surfaces. They accept a formula plus optional `name=value` variables and return existing DTO block types (`panel`, `keyValueGrid`, `table`, `message`, `rawJson`) with the normalized expression, variables, result, rounding, warnings, and structured error details.

Mortal World read-only parity currently includes:

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

Mortal World interactive form pending surfaces currently include:

- `/distribute`
- `/companion_directive`
- `/faction_directive`
- `/craft`

These return `ExplorerCommandResult` DTOs with local GM-turn status, target lists, current raw JSON where useful, and input prompts. They do not yet commit the full domain-specific write flow from the browser; that Mortal World write parity is tracked by #590.

Chaos Sea read-only parity currently includes:

- `/chaos_sea`, `/море_хаоса`
- `/guardians`, `/хранители`
- `/abode_power`, `/сила_обители`
- `/guardian_projects`, `/проекты_хранителей`
- `/abodes`, `/обители`
- `/gacha`, `/гача`

Chaos Sea interactive form pending surfaces currently include:

- `/abode_offering`, `/подношение_обители`
- `/found_guardian_mantle`, `/учредить_хранителя`

These show the pending contract state, active GM-turn blockers, target choices, and browser DTO prompts. Destructive local writes such as consuming a Soul Relic or Archive entry still require the afterlife write protocol tracked by #591 before the browser can submit them directly.

Shining Abode browser surfaces currently include:

- `/shining_abode`, `/сияющая_обитель`
- `/shining_politics`, `/сияющая_политика`
- `/shining_treasury`, `/казначейство`
- `/source_of_light`, `/источник_света`

The browser versions of `/shining_treasury` and `/source_of_light` are status-only surfaces for now. They do not mutate feathers, sparks, pending files, or capstone request state until the broader interactive/write protocol is implemented.

Afterlife combat and entity browser surfaces currently include:

- `/afterlife_profiles`, `/профили_загробья`
- `/afterlife_inbox`, `/уведомления_загробья`
- `/spiritual_conflict`, `/духовный_конфликт`
- `/spiritual_combat_log`, `/журнал_духовного_боя`
- `/spiritual_combat_help`, `/духовный_бой`
- `/spiritual_arts`, `/духовные_искусства`

The browser afterlife combat/entity status surfaces are read-only. `/afterlife_inbox` does not mark notifications as read and `/spiritual_arts` does not perform local upgrades.

Lifecycle/local-turn browser surfaces currently include:

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

Interactive multi-step prompt submission remains outside the current browser shell. QTE offer/scene/action protocol is available through the `/api/qte/*` endpoints and the “Проверить QTE” shell panel.

## Session Lock And Pending Turns

The shared save/session model means local writes must not happen concurrently from two UI owners. The lock file is:

```text
game_state/control/local_ui_session_lock.json
```

Current rules:

- Read-only command DTOs may render while another UI owner holds the lock.
- Mutating console commands acquire or refresh the lock before writing local state.
- Browser write flows use the shared browser write coordinator: it checks pending-turn artifacts, acquires or refreshes the local UI lock, captures rollback baselines for targeted files, restores those files on failed staging, and releases the lock after the write attempt.
- Browser local-turn DTOs create interactive prompt sessions that can collect, validate, resume, submit, and cancel prompt answers. The shared prompt layer does not yet perform every domain-specific file write; those write paths are migrated command-by-command.
- Browser QTE endpoints write through the QTE runtime and state distributor; do not run the same QTE flow simultaneously in console and browser.

If the browser shows `Pending`, inspect the local-turn panel. It normally means one of these artifacts exists:

- `input/turn_request.json`
- `ready/turn_complete.json`
- `ready/turn_error.json`
- `game_state/control/pending_turn_snapshot.json`
- `game_state/control/pending_turn_snapshot/`
- `game_state/control/explorer_local_turn_rollback/`

Finish, accept, cancel, or repair the pending GM turn before starting another local write flow.

Stale lock recovery:

- First close the other console/browser instance if it is still running.
- If `local_ui_session_lock.json` has an old `heartbeatAtUtc` beyond its `leaseSeconds`, a different owner may replace it on the next mutating command.
- If a malformed lock is fresh, it blocks mutation. If it is stale by file timestamp and no UI process is active, it may be replaced or deleted during repair.
- Do not delete a fresh lock just to force a command; that risks two frontends writing the same save at once.

See also `docs/web-ui/local-ui-session-lock.md` for the lock-file shape and ownership rules.

## Temporary Browser Limitations

The browser is no longer just a read-only shell, but several flows remain intentionally console-only or browser-status-only until their write UX is fully migrated:

- Interactive multi-step prompt submission is available as a browser prompt-session protocol. Many domain-specific local-turn writes still return accepted answers only; console mode remains the complete path until each write command is migrated.
- `/shining_treasury` and `/source_of_light` are browser status surfaces. Use console mode for treasury mutations and Source of Light request creation until those write paths are explicitly migrated.
- `/afterlife_inbox` does not mark notifications as read in the browser.
- `/spiritual_arts` does not perform local spiritual-art upgrades in the browser.
- The hidden Saref/Wings read-only views are migrated. The parser recognizes `/сареф найти_крылья`, `/сареф find_wings`, and equivalent spaced forms, but the mutating Wings search/join/story actions still need their own browser write protocol (#592) and return a structured blocker instead of silently falling back to the story overview.

These limitations are intentional migration boundaries, not separate game rules. Console and browser still use the same local `game_session` data.

## Troubleshooting

- Browser cannot connect: confirm the process is still running and open the exact loopback URL printed at startup, usually `http://127.0.0.1:8787`.
- Port already in use: launch with `--web-url http://127.0.0.1:<free port>`.
- Host rejects the URL: use `localhost` or `127.0.0.1`; the local web host refuses non-loopback addresses.
- Browser shows a different save: restart browser mode with the same base path you use for console mode.
- Command stays `Pending`: resolve the active GM turn or remove stale pending-turn artifacts only after confirming no GM response is still expected.
- Mutating command is blocked by a local UI session lock: close the other UI owner or wait for the lease to expire; inspect `game_state/control/local_ui_session_lock.json` before repair.
- A command says it is status-only or not fully interactive in browser: run the same command in console mode for the complete workflow until that browser write path is migrated.

