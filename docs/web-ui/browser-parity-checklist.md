# Browser Parity Checklist

Tracked tasks: #594, #619, #620, #621, #622, #623, #624, #625, #682, #691, #880, #881

Use this checklist for manual smoke testing when the local browser UI changes. The browser must stay a local shell over the same `game_session` data and must not duplicate game rules in JavaScript.

Map rendering parity is enforced through the shared `MapViewDto`: console `/карта` writes `output/map_viewer.html` with `LocalMapViewerAssets`, while React command-result map blocks render a local SVG atlas from the same DTO. `/карта` / `/map` must stay the visual map surface; `/локации` / `/locations` must stay the current/adjacent/discovered/updated location list and details flow.

## Shell And Navigation

- #738 player-copy boundary: Player UI speaks to the player. Default screens use Russian in-world or plain player-readable wording for loading, launcher, tabs, help, settings, audio, connection, and command results.
- Implementation comments stay in code, docs, and advanced mode. Raw command coverage, endpoint names, DTO/API wording, file paths, validation internals, raw JSON, and debug/repair details require explicit `Расширенный режим` or a technical disclosure.
- Generated HTML smoke artifacts are local/offline evidence. Review `TestResults/browser-smoke/*.html` for default-player copy drift, but do not commit generated smoke output.
- The root page keeps the player-facing default: title, current session summary, Continue/New Game/Load/Options/About/Exit actions, a player-facing game screen, and short Russian guidance before any developer tools.
- Continue/New Game actions must not reveal diagnostics automatically; Continue refreshes/scrolls to the game screen, and any player path that still needs a technical bridge shows a short game-facing message plus an explicit opt-in advanced button.
- Player-facing errors stay concise in Russian, with technical details behind a `Подробности` disclosure. The primary prose action composer stays in the default player area, must not expose the command palette as the main action model, and must not auto-execute slash commands without an explicit advanced-mode action.
- Advanced / developer panel is opened intentionally through `Расширенный режим`; it contains the raw command console, command palette, lifecycle dashboard, validation controls, QTE probes, raw JSON, and API endpoint hints.
- Командная палитра remains available inside the Advanced / developer panel for direct slash commands and filters visible buttons without hiding the manual command input.
- Persistent advanced navigation includes: Мир смертных, Море Хаоса, Сияющая Обитель, Духовный бой, История и архив, Диагностика.
- Desktop layout keeps navigation and results visible side by side after advanced mode is opened.
- Мобильный layout stacks navigation, status, forms, tables, image blocks, and raw JSON without horizontal overflow.
- Player-facing labels use Russian first; English appears only as technical command IDs, raw JSON keys, or advanced-mode endpoint references.
- Automated guards: `BrowserWebUiSmoke` covers root/menu/session/game-screen state/lifecycle/command/form flow, and `BrowserWebUiParity` forces explicit browser UX decisions for Explorer commands before new aliases can land silently.

## Mortal World

- Мир смертных navigation opens status, quests, inventory, map, NPCs, factions, combat, and gallery surfaces.
- Карта renders as a local SVG viewer, supports zoom/reset, z-level filter, layer filter, node selection, and detail cards without fetching anything from the network.
- Карта uses the dark-fantasy parchment atlas visual system: Russian controls, visible legend, clear selected/hover states, readable labels, and a Russian empty-state message for hidden/empty levels.
- Карта reads wrapped and unwrapped Mortal World location state, separates z-levels, marks the current location, and uses schematic fallback coordinates when the GM has not authored exact coordinates yet.
- Карта shows controlled-location regions and disputed locations when faction influence is present, and location cards include faction ownership details when the projection can provide them.
- Локации show the current location, adjacent exits, discovered locations, and updated locations from both root-level `world_map` arrays and wrapped `worldMapUpdates`.
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
