# Mortal Player-Interaction Drill-Down Contract (#1056)

## Source

- GitHub issue: #1056 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1056
- Parent audit: #948 mortal read-only drill-down audit
- Scope: read-only Mortal World `/interactions` / `/взаимодействия` detail surfaces for console and browser command-result flows.

## Runtime Boundary

This feature reads existing canonical Mortal World state that the command already uses. It must not create a new GM-authored schema, validation contract, pending/control file, write path, or normalizer behavior in this slice.

Expected existing authority:

- `game_state/misc/player_interactions.json` for other-player interaction summaries and nested records.
- `otherPlayersInteractions` as the mapped `GameResponse`/GM response key for the same state.
- `otherPlayersInteractions[playerId]` may be either a rich player object or a direct array of canonical command objects such as `{ "UpdateInventory": [...] }`.
- Current code may also encounter top-level `interactions` arrays in existing tests/fixtures. Support real observed read-only shapes without inventing new required schema.

## Player-Facing Command Contract

- Overview commands remain `/interactions` and `/взаимодействия`.
- The overview must preserve existing summary behavior and add discoverable player-facing detail affordances for covered player entries and record entries.
- Browser/default command-result blocks should expose details via existing `ExplorerCommandResult` blocks/actions and should not require React gameplay logic.
- Console must expose semantically equivalent detail paths. Suggested syntax is:
  - `/взаимодействия игрок <id-or-slug>` / `/interactions player <id-or-slug>` for a player-level entry.
  - `/взаимодействия запись <id-or-slug>` / `/interactions record <id-or-slug>` for an interaction record when records are globally addressable or the selected player context is encoded in the id/slug.
  - Codex may refine the exact words after inspecting existing command conventions, but final syntax must be recorded in this contract and covered by tests.
- Detail output must use Russian/in-world copy in ordinary player-facing blocks.

## Detail Content Expectations

### Player entry detail

Display available player-facing fields such as:

- player display name, stable id/key, or documented fallback label;
- relationship/context, role, faction/location, or relation summary when available;
- current status/availability/visibility when available;
- short summary and current hooks/active interactions;
- a list of inspectable interaction records belonging to that player.

### Interaction record detail

Display available player-facing fields such as:

- title/summary and stable id;
- participants, counterpart, or source player when available;
- location, turn, timestamp, or scene context when available;
- status/stage/type when available;
- description, notes, outcome, consequence, next visible step, tags, or visibility marker when available.
- nested canonical command payload content when the record is shaped as a command object, with ordinary Russian/player-facing labels for visible fields such as inventory item name, quantity, and description.

If a named subsection from the issue body is not backed by existing command source, record the observed source boundary and create/link a follow-up rather than inventing new state.

## Copy and Safety Boundaries

- Ordinary output must not expose local file paths, `game_state/...` paths, raw JSON as the only detail, `DTO`, `API`, `endpoint`, or agent/debug framing.
- Existing raw sidecar/advanced diagnostic behavior may remain only where the project already exposes it behind established raw/advanced blocks.
- Dynamic GM-authored text must be escaped/sanitized before Spectre.Console markup or browser HTML rendering.
- Missing/sparse files must produce graceful player-facing empty states.
- This feature is read-only. It must not mutate `player_interactions.json` or any pending social interaction files.

## Verification Contract

Before PR/merge, the branch must provide evidence for:

- RED tests for at least player-entry detail and interaction-record detail paths.
- GREEN focused tests for those paths.
- A broader mortal read-only command-result/console/browser slice.
- `dotnet build` for client and tests when C# source changes.
- Spec Kit prerequisite check resolving `specs/1056-mortal-interactions-drilldowns`.
- `git diff --check origin/main...HEAD` and added-line static/security scan.
