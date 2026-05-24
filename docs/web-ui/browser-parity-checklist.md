# Browser Parity Checklist

Tracked tasks: #594, #619, #620, #621, #622, #623, #624, #625

Use this checklist for manual smoke testing when the local browser UI changes. The browser must stay a local shell over the same `game_session` data and must not duplicate game rules in JavaScript.

Map rendering parity is enforced through the shared map package: console `output/map_viewer.html` and WebUI map blocks both consume the same `MapViewDto` and `LocalMapViewerAssets` renderer. WebUI should load `/assets/map-viewer.css` and `/assets/map-viewer.js`; no realm-specific map renderer should be reimplemented inside the browser shell.

## Shell And Navigation

- The root page keeps the player-facing default: title, current session summary, Continue/New Game/Load/Options/About/Exit actions, and short Russian guidance before any developer tools.
- Continue/New Game actions must not reveal diagnostics automatically; if a player path still needs a technical bridge, show a short game-facing message plus an explicit opt-in advanced button.
- Player-facing errors stay concise in Russian, with technical details behind a `Подробности` disclosure.
- Advanced / developer panel is opened intentionally through `Расширенный режим`; it contains the raw command console, command palette, lifecycle dashboard, validation controls, QTE probes, raw JSON, and API endpoint hints.
- Командная палитра remains available inside the Advanced / developer panel for direct slash commands and filters visible buttons without hiding the manual command input.
- Persistent advanced navigation includes: Мир смертных, Море Хаоса, Сияющая Обитель, Духовный бой, История и архив, Диагностика.
- Desktop layout keeps navigation and results visible side by side after advanced mode is opened.
- Мобильный layout stacks navigation, status, forms, tables, image blocks, and raw JSON without horizontal overflow.
- Player-facing labels use Russian first; English appears only as technical command IDs, raw JSON keys, or advanced-mode endpoint references.

## Mortal World

- Мир смертных navigation opens status, quests, inventory, map, NPCs, factions, combat, and gallery surfaces.
- Карта renders as a local SVG viewer, supports pan/zoom, z-level filter, layer filter, node selection, and detail cards without fetching anything from the network.
- Карта uses the shared dark-fantasy parchment atlas visual system: Russian controls, visible legend, clear selected/hover states, readable labels, and a Russian empty-state message for hidden/empty levels.
- Карта reads wrapped and unwrapped Mortal World location state, separates z-levels, marks the current location, and uses schematic fallback coordinates when the GM has not authored exact coordinates yet.
- Карта exposes a `Политическое влияние` toggle. Controlled locations show faction halos/regions, disputed locations are visually distinct, and location cards include faction control type/level when present.
- Tables, warnings, actions, and raw JSON render consistently.
- Mutating forms that are already migrated still use the shared local UI lock and show blocked/pending states when a GM turn is active.

## Chaos Sea

- Море Хаоса navigation opens overview, Guardians, Abode power, Guardian projects, offering, and mantle-founding surfaces.
- `/map` / `/карта` in Chaos Sea renders a Guardian Abode constellation, not a Mortal World coordinate map.
- The Chaos Sea map highlights the current Abode and active Guardian, keeps discovered Abode layout stable between renders, and shows Guardian/domain/reputation/power/resident/project/action details when available.
- Pending-contract blockers render as Russian messages rather than silent failures.
- Mutating forms show progress while submitting and release locks after completion/cancel.

## Shining Abode

- Сияющая Обитель navigation opens overview, politics, treasury, Source of Light, hidden Saref story, and Wings search.
- `/map` / `/карта` in Shining Abode renders a civic mandala of halls and factions rather than fake terrain coordinates.
- The Shining map shows hall ownership/influence, faction strength, leadership, residents, projects, and a Russian fallback for factions without a hall.
- Treasury and Source forms preserve their validation errors and pending-contract messages.
- Saref/Wings actions keep no-spoiler behavior before discovery and show GM-action payloads after unlock.

## Afterlife Combat

- Духовный бой navigation opens current conflict, combat log, help, arts progression, and spiritual action.
- Spiritual Arts upgrade forms show resource errors in Russian.
- Spiritual action returns the GM payload visibly and keeps raw JSON readable.

## Story And Archive

- История и архив navigation opens story, chronicle, codex, soul, afterlife archive, archive candidates, and memory-scene surfaces.
- Memory-scene views use the player-facing term `Воспоминание`.
- Archive candidate forms retain clear success/cancel/error outcomes.

## Diagnostics And Tools

- Диагностика navigation opens validation, debug, math assistant, GM notes, mods, and system Guardians.
- Validation results group errors/warnings and keep raw JSON available where useful.
- The math assistant renders formula, variables, result, warnings, and raw JSON.
- QTE buttons still show offer/active/completed states and visible progress.

## Media And Raw Data

- Gallery image blocks load through `/api/media/{mediaId}`, never through raw filesystem paths.
- Missing images show a Russian error placeholder.
- Raw JSON remains collapsible/readable enough for repair work and does not replace player-facing summaries.
