# Feature Specification: Map and Locations Command Semantics

**Feature Branch**: `fix/880-881-map-locations`

**Created**: 2026-06-07

**Status**: Draft

**Input**: GitHub issues #880 and #881 report that `/карта` / `/map` and `/локации` / `/locations` have drifted from their player-facing meanings. `/карта` must open or render the real visual map, while `/локации` must own the current/adjacent/discovered/updated location list and details flow.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #880: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/880
  - #881: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/881
- **Issue type**: medium severity browser/console bug, player-facing command semantics, navigation/functionality.
- **Spec Kit justification**: This work changes player-facing command semantics across console and browser, touches React presentation, C# command-result/Explorer surfaces, tests, and requires visual evidence. It meets the constitution and `AGENTS.md` Spec Kit policy for console/browser parity and player-facing UX.
- **Player-facing contract**:
  - `/карта` / `/map` means the visual map.
  - `/локации` / `/locations` means the location list/details flow.
- **Contract scope**: console player commands, browser command-result rendering, existing local map viewer assets/DTOs, location-list state projection from `current_location.json` and `world_map.json`.
- **GM-facing docs scope**: no GM-authored runtime contract or pending/control shape should change. Update docs only if implementation changes documented command behavior, map DTO semantics, or local web map renderer documentation.
- **Out of scope**:
  - New map gameplay rules, new GM-authored state fields, or new pending/control files.
  - Rewriting `LocalMapViewService` beyond fixes needed to preserve existing map projections.
  - Turning `/локации` into a map; it should be list/details, not visual map.
  - Broad Browser Client redesign unrelated to map/location command results.

## User Scenarios & Testing

### User Story 1 - Console `/карта` opens the visual map (Priority: P1)

A Mortal World player enters `/карта` or `/map` in the console and receives the same visual-map pathway used by the current afterlife branch: a `MapViewDto` is built for the current realm, rendered as a map block, and `LocalMapViewerLauncher.WriteAndOpenAsync(...)` opens or saves `output/map_viewer.html` with player-facing path copy.

**Why this priority**: #880 is a regression in the core meaning of `/карта`; the console currently shows the location selector instead of the visual map in Mortal World.

**Independent Test**: A focused C# console/Explorer test or source guard proves the Mortal `/карта` path calls the shared local map viewer flow and no longer constructs the current/adjacent/discovered selection prompt.

**Acceptance Scenarios**:
1. **Given** Mortal World state with `current_location.json` and `world_map.json`, **When** console `/карта` runs, **Then** it builds a current-realm `MapViewDto` and writes/opens `output/map_viewer.html`.
2. **Given** Chaos Sea or Shining Abode state, **When** console `/карта` runs, **Then** the existing afterlife map viewer behavior still works.
3. **Given** the console map completes, **Then** player copy states the opened or saved HTML map path and does not show the old location-selection menu.

---

### User Story 2 - Console `/локации` owns location list/details (Priority: P1)

A Mortal World player enters `/локации` or `/locations` in the console and gets the rich location browser currently embedded in the Mortal `/карта` branch: current location, adjacent locations, discovered and updated locations, plus detail panels.

**Why this priority**: #881 reports `/локации` appears to do nothing while `/карта` opens the list; the command meanings must be separated and tested together.

**Independent Test**: Focused C# tests or source guards prove the location-list helper is reachable from `/локации` / `/locations` and not from `/карта` / `/map`.

**Acceptance Scenarios**:
1. **Given** a current location with `adjacencyMap`, **When** console `/локации` runs, **Then** the list contains the current location and adjacent location labels with player-facing details.
2. **Given** `world_map.json.worldMapUpdates.newLocations[]` and `locationUpdates[]`, **When** console `/локации` runs, **Then** discovered and updated locations appear.
3. **Given** root-level `newLocations[]` / `locationUpdates[]`, **When** console `/локации` runs, **Then** the supported legacy shape still appears.
4. **Given** no location data exists, **When** console `/локации` runs, **Then** it returns graceful player-facing empty-state copy.

---

### User Story 3 - Browser `/карта` renders a visual map surface (Priority: P1)

A browser player runs `/карта` / `/map` from the command result path and sees a real map visualization using the shared map DTO and local SVG/map renderer, not only a text count or node list.

**Why this priority**: #880 requires real browser visual evidence; backend `UiMapBlock` tests are insufficient if React renders only `карта содержит N точек`.

**Independent Test**: Frontend tests/source guards fail if the default map block renderer returns only text/count/list and pass when it renders a visual map surface with SVG or shared map viewer integration. A visual smoke artifact/screenshot must be produced before closure.

**Acceptance Scenarios**:
1. **Given** an `UiMapBlock` for Mortal World, **When** the browser command result renders it, **Then** the DOM contains a player-facing visual map surface (`svg`, map nodes/links, controls, or equivalent shared renderer) and no text-only fallback as the primary result.
2. **Given** Chaos Sea or Shining Abode map projections, **When** the browser command result renders them, **Then** the renderer supports those realms without raw JSON leakage.
3. **Given** visual verification is run, **Then** a screenshot or dependency-light HTML visual smoke artifact is saved under `TestResults/` and linked in the PR/issue evidence.

---

### User Story 4 - Browser `/локации` displays meaningful location rows/details (Priority: P1)

A browser player runs `/локации` / `/locations` and sees current location, adjacent locations, discovered locations, and updates as player-facing rows/details, including when `world_map.json` wraps data under `worldMapUpdates`.

**Why this priority**: #881 identifies a current no-op/empty result caused by generic bundle code checking root-level properties only.

**Independent Test**: Focused C# browser command-result tests seed wrapped and root-level `world_map.json` shapes and assert `/локации` produces non-empty player-facing rows/details, while `/карта` still produces a `UiMapBlock`.

**Acceptance Scenarios**:
1. **Given** `world_map.json.worldMapUpdates.newLocations[]`, **When** browser `/локации` runs, **Then** the result includes those locations in default player-facing blocks.
2. **Given** root-level `newLocations[]`, **When** browser `/locations` runs, **Then** the result includes those locations.
3. **Given** `current_location.json.adjacencyMap`, **When** browser `/локации` runs, **Then** current and adjacent locations are visible.
4. **Given** `/карта` and `/локации` are both tested, **Then** tests prevent them from drifting back into swapped behavior.

## Functional Requirements

- **FR-001**: `/карта` / `/map` MUST mean visual map in both console and browser.
- **FR-002**: Mortal console `/карта` MUST use the shared `LocalMapViewService` / `UiMapBlock` / `LocalMapViewerLauncher` path and MUST NOT show the location selector/list.
- **FR-003**: Console afterlife `/карта` behavior for Chaos Sea and Shining Abode MUST remain intact.
- **FR-004**: Browser `UiMapBlock` rendering MUST provide a visual map surface by default, not only text counts or node lists.
- **FR-005**: `/локации` / `/locations` MUST own the location list/details flow in console and browser.
- **FR-006**: Browser `/локации` MUST unwrap `worldMapUpdates.newLocations[]` and `worldMapUpdates.locationUpdates[]` and also preserve root-level shape support.
- **FR-007**: Location list/details surfaces MUST include current location and adjacent locations when `current_location.json` is present.
- **FR-008**: Default browser and console copy MUST remain player-facing Russian/in-world copy and MUST NOT expose raw API/DTO/debug framing outside advanced mode.
- **FR-009**: Tests MUST cover Russian and English aliases for both command families where practical.
- **FR-010**: Closure evidence MUST include a Browser Client screenshot or visual artifact proving `/карта` renders a visual map surface.

## Edge Cases

- `current_location.json` missing: `/локации` shows a graceful empty/unknown-location message, while `/карта` handles map service empty-state behavior without crashing.
- `world_map.json` missing: `/локации` still shows current/adjacent data when possible.
- Wrapped and root-level `world_map` update shapes both exist: the renderer should avoid duplicate rows where stable IDs/names match.
- Map has zero nodes: browser map renderer shows an intentional empty map state, not raw JSON or a crash.
- Dynamic GM-authored text in names/descriptions/directions must be escaped/sanitized before Spectre.Console markup or browser HTML.
- Browser advanced/raw JSON blocks may still exist behind existing advanced behavior, but default player UI must be meaningful without using them.

## Success Metrics

- Focused C# tests fail on current `main` for the reported swapped/no-op behavior and pass after the fix.
- Frontend verification passes and includes a map renderer guard.
- Browser visual evidence is produced and linked.
- PR can close #880 and #881 together without closing unrelated map/locations roadmap issues.
