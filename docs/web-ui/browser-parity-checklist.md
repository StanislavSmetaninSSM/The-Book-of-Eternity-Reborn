# Browser Parity Checklist

Tracked task: #594

Use this checklist for manual smoke testing when the local browser UI changes. The browser must stay a local shell over the same `game_session` data and must not duplicate game rules in JavaScript.

## Shell And Navigation

- Командная палитра remains available for direct slash commands and filters visible buttons without hiding the manual command input.
- Persistent navigation includes: Мир смертных, Море Хаоса, Сияющая Обитель, Духовный бой, История и архив, Диагностика.
- Desktop layout keeps navigation and results visible side by side.
- Мобильный layout stacks navigation, status, forms, tables, image blocks, and raw JSON without horizontal overflow.
- Player-facing labels use Russian first; English appears only as technical command IDs or raw JSON keys.

## Mortal World

- Мир смертных navigation opens status, quests, inventory, map, NPCs, factions, combat, and gallery surfaces.
- Tables, warnings, actions, and raw JSON render consistently.
- Mutating forms that are already migrated still use the shared local UI lock and show blocked/pending states when a GM turn is active.

## Chaos Sea

- Море Хаоса navigation opens overview, Guardians, Abode power, Guardian projects, offering, and mantle-founding surfaces.
- Pending-contract blockers render as Russian messages rather than silent failures.
- Mutating forms show progress while submitting and release locks after completion/cancel.

## Shining Abode

- Сияющая Обитель navigation opens overview, politics, treasury, Source of Light, hidden Saref story, and Wings search.
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
