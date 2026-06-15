# Mortal World-News Drill-Down Contract (#1055)

## Source

- GitHub issue: #1055 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1055
- Parent audit: #948 mortal read-only drill-down audit
- Scope: read-only Mortal World `/world_news` / `/новости_мира` detail surfaces for console and browser command-result flows.

## Runtime Boundary

This feature reads existing canonical Mortal World state files that the command already uses. It must not create a new GM-authored schema or validation contract in this slice.

Expected existing authorities include:

- `game_state/world/world_events.json` for world-event entries.
- `game_state/world/world_flags.json` for world-state flags or equivalent non-event world-news items currently rendered by the command.
- `game_state/world/progression.json` for progression entries.
- Any additional optional source already rendered by the current `/новости_мира` implementation may be used only after source inspection confirms it is existing canonical read-only state.

## Player-Facing Command Contract

- Overview commands remain `/world_news` and `/новости_мира`.
- The overview must preserve existing summary behavior and add discoverable player-facing detail affordances for covered sections.
- Browser/default command-result blocks should expose details via existing `ExplorerCommandResult` blocks/actions and should not require React gameplay logic.
- Console must expose semantically equivalent detail paths. The implemented and tested syntax is:
  - `/новости_мира событие <id-or-slug>` / `/world_news event <id-or-slug>`
  - `/новости_мира флаг <id-or-slug>` / `/world_news flag <id-or-slug>` for the representative non-event subsection covered by this slice.
  - `/новости_мира прогресс <id-or-slug>` / `/world_news progression <id-or-slug>`
- Detail output must use Russian/in-world copy in ordinary player-facing blocks.

## Detail Content Expectations

### World event detail

Display available player-facing fields such as:

- title/name and stable id;
- location/region/time/date when available;
- status/phase/importance when available;
- involved actors, NPCs, factions, or locations when available;
- description/narrative and consequences/aftermath when available.

### Non-event subsection detail

Display one representative major subsection item already rendered by the command, such as world flags, location threats/news, NPC activity, or faction-project style items. Display available player-facing fields such as:

- title/key/name and stable id;
- scope/location/faction/NPC when available;
- status/state/severity when available;
- description, current effect, or consequence when available.

If a named subsection from the issue body is not backed by an existing command source, record the observed source boundary and create/link a follow-up rather than inventing new state.

### Progression detail

Display available player-facing fields such as:

- title/name and stable id;
- stage/status/current step;
- description/summary;
- trigger/source/time when available;
- consequence/next visible outcome when available.

## Copy and Safety Boundaries

- Ordinary output must not expose local file paths, `game_state/...` paths, raw JSON as the only detail, `DTO`, `API`, `endpoint`, or agent/debug framing.
- Existing raw sidecar/advanced diagnostic behavior may remain only where the project already exposes it behind established raw/advanced blocks.
- Dynamic GM-authored text must be escaped/sanitized before Spectre.Console markup or browser HTML rendering.
- Missing/sparse files must produce graceful player-facing empty states.

## Verification Contract

Before PR/merge, the branch must provide evidence for:

- RED tests for at least world-event, non-event subsection, and progression detail paths.
- GREEN focused tests for those paths.
- A broader mortal read-only command-result/console/browser slice.
- `dotnet build` for client and tests when C# source changes.
- Spec Kit prerequisite check resolving `specs/1055-mortal-world-news-drilldowns`.
- `git diff --check origin/main...HEAD` and added-line static/security scan.
