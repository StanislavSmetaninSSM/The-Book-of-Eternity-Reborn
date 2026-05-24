# Local Web Host

Tracked tasks: #565, #567, #569, #570, #571, #572, #573, #574, #576, #577, #585, #586, #587, #588, #589, #590, #591, #592, #593, #594, #619, #620, #621, #622, #682, #691
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
GET /api/game-screen
GET /api/lifecycle/dashboard
POST /api/lifecycle/validate
POST /api/explorer/command
GET /api/explorer/prompt-sessions/{sessionId}
POST /api/explorer/prompt-sessions/submit
POST /api/explorer/prompt-sessions/cancel
GET /api/media/{mediaId}
GET /api/qte/state
POST /api/qte/offer
POST /api/qte/action
```

`/` serves the browser game shell. The root page defaults to the player-facing main menu plus a player-facing game screen: current session summary, Continue/New Game/Load/Options/About/Exit actions, the current realm/narrative/status surface, and short Russian guidance. It does not present the raw command console, endpoint hints, lifecycle validation, or debug controls as the primary player flow. The default surface includes a primary prose action composer for ordinary player intent; slash commands are intentionally rejected from automatic execution and can only be prefixed into the explicit Advanced / developer panel for a second deliberate action.

The **Advanced / developer panel** is available through the explicit `Расширенный режим` button. It contains the raw command console, the migrated command renderer, lifecycle dashboard controls, validation controls, QTE probes, and `/api/*` endpoint details needed for development or repair. Continue/New Game do not open this panel automatically; when a player flow still needs a technical bridge, the UI shows a short game-facing explanation and a separate opt-in button. This keeps the normal browser landing page player-facing while preserving the shared command/API tools; the browser still renders `ExplorerCommandResult` DTOs from the command API and does not duplicate game logic in JavaScript.

Normal player-menu errors are short Russian messages. Technical exception/HTTP details are placed behind a `Подробности` disclosure so they are available for repair without becoming primary player content.

Inside the Advanced / developer panel, the shell includes Russian navigation and a filterable command palette for the major play areas:

- Мир смертных
- Море Хаоса
- Сияющая Обитель
- Духовный бой
- История и архив
- Диагностика

The manual slash-command input remains available for power users. Navigation buttons simply execute the same command API as typed commands, so the browser shell does not fork gameplay logic.

The renderer currently supports these DTO surfaces:

- `text`, `panel`, `table`, `list`, `keyValueGrid`, `message`, `rawJson`, and `image` blocks.
- `notifications` as message cards.
- `actions` as command buttons when an action has a direct command.
- `prompts` as browser form cards when an `interactiveSession` is present, otherwise as read-only prompt cards showing prompt text, kind, requirement flag, and selection options.
- empty, loading, HTTP error, and command failure states.

`/api/health` and `/api/session` return local session metadata: status, local-only flag, base path, `game_session` path, whether the directory exists, and the browser write-owner state. The write state includes:

- `canStartBrowserWrite`: false when a GM turn, rollback/snapshot artifact, or active non-stale UI lock blocks local writes.
- `pendingTurn`: the actionable list of GM-turn and rollback artifacts the player/repair flow must resolve first.
- `localUiLock`: current owner, kind, heartbeat, lease, stale/readable flags, and last operation for stale lock recovery.

`/api/game-screen` returns the read-only game-screen state DTO used by browser smoke tests and the default player-facing game screen. It refreshes the shared `StateManager` and exposes soul summary, player condition, world/location/time/session fields, narrative text, dialogue options, combat log, realm theme, lifecycle/turn state, QTE state, the primary prose action composer metadata, and realm flags such as `isInChaosSea` or `isInAfterlifeRealm`. GM/debug notes stay in the explicit Advanced / developer command surface and are not exposed through the default game-screen DTO. The turn state separates waiting GM turns, ready GM responses, GM-turn errors, pending-turn repair artifacts, validation repair, QTE, ready, and blocked states. This read-only game-screen endpoint is presentation-only: it does not write to `game_session`, does not normalize or delete QTE runtime files, does not start local turns, and does not replace the C# game/application logic with JavaScript rules. The composer currently accepts prose in the UI as the primary player intent surface, but safe browser turn-writing remains a separate lifecycle task; no `turn_request.json` is created from the default screen in this slice.

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

For read-only or not-yet-migrated interactive commands, successful submissions complete the browser prompt session and return a DTO containing the accepted answers. Mortal World write commands that reached mutating parity now execute their domain write during prompt submission through the browser local-write coordinator. Domain validation errors keep the prompt session and local UI lock open so the player can correct the form instead of losing the staged input.

`GET /api/media/{mediaId}` serves local image files referenced by browser DTOs. `mediaId` is an opaque id generated by the client; the endpoint revalidates every request and only serves supported image files under approved local media roots:

- `game_session/images/**`
- `game_session/output/**`

It does not accept arbitrary filesystem paths. Traversal, non-media roots, missing files, and unsupported extensions return a bounded Russian JSON error instead of exposing local paths.

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
- `/saref`, `/сареф`, `/saref_story`, `/история_сарефа`, `/wings_of_angels`, `/крылья_над_бездной` as the hidden main-story view. Before the story is discovered, the browser keeps the same no-spoiler answer: `ты пока не знаешь, что искать`.
- `/воспоминание`, `/воспоминание_статус`, `/воспоминание_начать`, `/воспоминание_способности`, plus spaced subcommand forms such as `/воспоминание начать`, as the read-only Saref memory-scene view.

`/gallery` and `/галерея` render saved images from `game_session/images/**` as browser `image` blocks using safe `/api/media/{mediaId}` URLs. Console image behavior is unchanged: console mode still uses the existing image service and external viewer options.

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

`/map` and `/карта` now return a shared `map` DTO block in addition to the raw JSON repair payloads. The browser renderer draws it locally as SVG with pan/zoom, z-level filtering, layer filtering, node selection, and detail cards. The same renderer package is reused by the console-launched standalone HTML viewer and the embedded WebUI: `LocalMapViewerAssets.StyleSheet` and `LocalMapViewerAssets.Script` are inlined into `output/map_viewer.html`, and the WebUI serves the identical package through `/assets/map-viewer.css` and `/assets/map-viewer.js`. New realm projections should extend `LocalMapViewService` and the `MapViewDto`; they should not add a second JavaScript map implementation.

The DTO is realm-agnostic (`realm`, `nodes`, `links`, `regions`, `layers`, `zLevels`, owner/influence fields) so Mortal World, Chaos Sea, and Shining Abode projections can reuse the same renderer. The map service chooses the projection from `game_state/meta/soul_state.json.currentRealm`: Mortal World reads `game_state/world/current_location.json` and `game_state/world/world_map.json`; Chaos Sea reads `game_state/meta/guardians.json` and builds a non-geographic Guardian Abode constellation from `activeGuardian`, `chaosSeaNavigation.currentAbodeId`, `discoveredAbodes`/`knownAbodes`, and Guardian `abode` data; Shining Abode reads `game_state/meta/shining_abode_state.json` and builds a civic mandala from `halls[]`, `factions[]`, residents, leadership, and projects. It keeps console fallback behavior unchanged and writes no game state.

The map visual direction is a dark-fantasy parchment atlas, not a technical graph. The reusable renderer uses Russian-first controls and labels:

- `Уровень`, `Слой`, `Приблизить`, `Отдалить`, `Сброс`.
- `Легенда карты` with current point, ordinary point, and faction influence swatches.
- parchment texture, ink-like route lines, selected/hover node states, and an empty-state card saying there are no points on the selected level/layer.
- selected node cards with details and faction ownership/influence where the projection can provide them.
- Mortal location cards include available type, region, biome, known/discovered state, description, last events, exits, storage/threat counts, and coordinates. Missing coordinates degrade into stable schematic coordinates and are marked as schematic in the card instead of breaking the viewer.
- The political overlay toggle (`Политическое влияние`) renders existing faction-control data only. Dominant factions come from `location.factionControl[]` and `factions[].controlledTerritories[]`; disputed locations are shown as `Спорная зона` when multiple meaningful influences are close. The viewer uses soft halos/cluster regions rather than pretending to know exact borders.
- Chaos Sea cards show Guardian, domain, reputation, Abode power, residents, projects, available actions, and discovery state when those fields exist. The current Abode is centered and marked as current, the active Guardian is called out in the card, and other discovered Abodes use deterministic constellation coordinates derived from stable ids/domain instead of fake world coordinates.
- Shining Abode cards show hall descriptions, dominant faction ownership, faction strength, leadership, residents, and projects when those fields exist. Factions without a valid `hallId` are grouped under a visible `Без закреплённого зала` fallback instead of disappearing.

Mortal World mutating parity currently includes:

- `/distribute`
- `/companion_directive`
- `/faction_directive`
- `/craft`

These return `ExplorerCommandResult` DTOs with local GM-turn status, target lists, current raw JSON where useful, and input prompts. On submit they use the shared local UI lock and rollback capture before mutating:

- `/distribute`: updates `game_state/misc/characteristics.json` and `game_state/player/stat_points.json`, rejecting unknown stats, non-positive allocations, over-budget spends, and stat values above 100.
- `/companion_directive`: updates `playerCompanionDirective` in `game_state/npcs/npc_core.json` for a matching active companion.
- `/faction_directive`: updates `playerStrategyDirective` in `game_state/factions/faction_core.json` for a player-owned or player-member faction.
- `/craft`: writes `game_state/control/pending_craft_request.json` with `recipeId`, `craftIntent`, source, status, and request id for GM resolution; the next Mortal World turn reminder surfaces the pending craft so the GM does not have to infer it from disk manually.

Chaos Sea read-only parity currently includes:

- `/chaos_sea`, `/море_хаоса`
- `/guardians`, `/хранители`
- `/abode_power`, `/сила_обители`
- `/guardian_projects`, `/проекты_хранителей`
- `/abodes`, `/обители`
- `/gacha`, `/гача`

Chaos Sea mutating parity currently includes:

- `/abode_offering`, `/подношение_обители`
- `/found_guardian_mantle`, `/учредить_хранителя`

These show pending contract state, active GM-turn blockers, target choices, and browser DTO prompts. On submit, the browser uses the shared local write coordinator to create the relevant pending request and to apply local resource consumption where the console flow also does it, including Ink Feather spends, Soul Relic consumption, and Archive-entry consumption.

Shining Abode browser surfaces currently include:

- `/shining_abode`, `/сияющая_обитель`
- `/shining_politics`, `/сияющая_политика`
- `/shining_treasury`, `/казначейство`
- `/source_of_light`, `/источник_света`

`/shining_treasury` and `/source_of_light` have browser mutating parity. Treasury submit operations can deposit, withdraw, claim interest, and exchange through the shared Shining treasury service. Source of Light submit creates the client-owned `game_state/control/pending_source_of_light_capstone.json` request after the same unlock and pending-contract blockers used by console mode.

Saref/Wings story browser surfaces currently include:

- `/сареф найти_крылья`, `/saref find_wings`: mutating parity for creating `game_state/control/pending_saref_wings_infiltration.json` after the same Shining Abode, route-unlock, pending-turn, and local UI lock checks as console mode.
- `/сареф преимущество`, `/saref use_advantage`: browser form that returns a GM-facing `SAREF_ADVANTAGE_USE` payload for spending a discovered Saref advantage in the surrounding turn.
- `/сареф конфронтация`, `/saref confrontation`: browser form that returns a GM-facing `SAREF_FINAL_CONFRONTATION` payload for resolving the final Saref scene through `sarefMainStoryUpdate`.
- `/сареф разорвать_клятву`, `/saref break_oath`: browser form that returns a GM-facing `SAREF_OATH_BREAK` payload for the oath-break arc.
- `/сареф поручение`, `/saref agenda`: browser form that returns a GM-facing `SAREF_OATHBOUND_AGENDA` payload for post-deal Wings assignments.

Only `/сареф найти_крылья` writes a local pending contract directly. The other Saref action forms mirror console route-tag behavior: they produce structured GM-action payloads and rely on the accepted-turn response to mutate canonical story state.

Afterlife combat and entity browser surfaces currently include:

- `/afterlife_profiles`, `/профили_загробья`
- `/afterlife_inbox`, `/уведомления_загробья`
- `/spiritual_conflict`, `/духовный_конфликт`
- `/spiritual_combat_log`, `/журнал_духовного_боя`
- `/spiritual_combat_help`, `/духовный_бой`
- `/spiritual_arts`, `/духовные_искусства`

`/afterlife_inbox` and `/spiritual_arts` have browser mutating parity. Inbox submit can mark one notification or all notifications read. Spiritual Arts submit can upgrade standard arts, learned special arts from `afterlife_entity_profiles.json`, or `spirit_focus` with Чернильные Перья or, in Shining Abode, Искры Света while respecting active-conflict and pending-contract blockers.

Lifecycle/local-turn browser surfaces currently include:

- `/validate`, `/валидация`: runs the same `ValidationService` as the console command and renders grouped issues.
- `/world_setup`, `/настройка_мира`: shows `incarnation_world_setup.json`, `next_life_scenario_core.json`, current realm, and browser prompts for editing or clearing. On submit, `create_or_edit` writes `game_state/control/incarnation_world_setup.json` and refreshes `game_state/control/next_life_scenario_core.json`; `clear` deletes both.
- `/spiritual_action`, `/духовное_действие`: shows active afterlife conflict state, response surface, and prompts for the player's spiritual action route tag. Submit returns a structured GM-action payload (`AFTERLIFE_SPIRITUAL_ACTION`) for the surrounding turn flow; it does not directly mutate canonical state by itself, matching the console command's route-tag behavior.

Every migrated local-turn protocol result includes a `Локальный ход / GM-turn protocol` panel. It reports whether these artifacts exist:

- `input/turn_request.json`
- `ready/turn_complete.json`
- `ready/turn_error.json`
- `game_state/control/pending_turn_snapshot.json`
- `game_state/control/pending_turn_snapshot/`
- `game_state/control/explorer_local_turn_rollback/`

If any active GM-turn or rollback/snapshot artifact exists, local-turn command DTOs return `Pending` so the browser can observe the long-running or late-response state without invoking console-only prompts.

Interactive prompt submission is active in the current browser shell. Migrated Mortal World, Chaos Sea, Shining Abode, and afterlife combat/entity write commands above commit local files directly from forms where the console command writes local state. QTE offer/scene/action protocol is available through the `/api/qte/*` endpoints and the “Проверить QTE” shell panel.

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

See also `docs/web-ui/browser-parity-checklist.md` for the manual shell parity checklist used when changing browser navigation, forms, progress states, QTE, media, and raw JSON rendering.

## Automated Browser Smoke And Parity Verification

Run the focused browser contract suite before changing browser root/menu/session/game-screen state, lifecycle dashboards, command migration metadata, prompt sessions, QTE, media, or command rendering:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity"
```

On MSYS/bash shells the same command uses slash paths:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity"
```

`BrowserWebUiSmoke` covers the local host root page, main menu, session status, read-only game-screen state, lifecycle dashboard, command DTO execution, and browser prompt/form submission. `BrowserWebUiParity` guards command metadata so every Explorer command definition carries an explicit browser UX decision instead of relying on a silent default.

## Temporary Browser Limitations

The browser is no longer just a read-only shell, but several flows remain intentionally console-only or browser-status-only until their write UX is fully migrated:

- Interactive multi-step prompt submission is available as a browser prompt-session protocol. Some domain-specific local-turn writes still return accepted answers only; console mode remains the complete path until each write command is migrated.
- `/spiritual_action` returns the same route-tag payload shape as the console command, but the browser shell does not yet provide a full typed turn composer around that payload. Use the returned JSON/text as the GM-action payload for the active turn flow until the broader browser game shell is completed.
- Saref/Wings action forms are migrated as local pending creation or GM-action payloads, but the browser shell does not yet provide a full typed turn composer around those payloads.
- Browser gallery display is migrated for saved local files. Entity-specific image generation, regeneration, cleanup, and export actions remain console-command behavior until their own browser forms are migrated.

These limitations are intentional migration boundaries, not separate game rules. Console and browser still use the same local `game_session` data.

## Troubleshooting

- Browser cannot connect: confirm the process is still running and open the exact loopback URL printed at startup, usually `http://127.0.0.1:8787`.
- Port already in use: launch with `--web-url http://127.0.0.1:<free port>`.
- Host rejects the URL: use `localhost` or `127.0.0.1`; the local web host refuses non-loopback addresses.
- Browser shows a different save: restart browser mode with the same base path you use for console mode.
- Command stays `Pending`: resolve the active GM turn or remove stale pending-turn artifacts only after confirming no GM response is still expected.
- Mutating command is blocked by a local UI session lock: close the other UI owner or wait for the lease to expire; inspect `game_state/control/local_ui_session_lock.json` before repair.
- A command says it is status-only or not fully interactive in browser: run the same command in console mode for the complete workflow until that browser write path is migrated.

