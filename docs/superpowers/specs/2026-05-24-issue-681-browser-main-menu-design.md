# Issue 681 Browser Main Menu Design

## Problem

Issue #681 tracks the first Browser Client slice after the debug-oriented Local Web UI. The current root page identifies itself as a technical “Local Web UI”, shows `/help` and API endpoints first, and exposes the command palette before the player sees a console-equivalent main menu.

## Goal

Make `/` open a player-facing Russian main menu for Book of Eternity Reborn. The menu should mirror the console client’s meaning: Continue, New Game, Load, Options, About, and Exit/session guidance, while preserving the existing command shell as an advanced/debug area rather than the primary experience.

## Constraints

- The tracked task is GitHub issue #681.
- Browser UI must remain a frontend over the same C# runtime and local `game_session`; no separate gameplay logic.
- The old TypeScript project is a UI/UX reference only: use its centered dark card, tabs/actions, disabled states, and load/about/settings affordances; do not copy old mechanics or prompts.
- No afterlife runtime contract is changed by this slice, so GM-facing afterlife docs are not required.
- Save/load must use browser-friendly DTOs/API, not manual slash-command text.

## Approaches considered

1. **Pure HTML rearrangement:** Move the command shell below a static main menu. Fast, but it cannot report real session/save state and would not satisfy save/load DTO criteria.
2. **Dedicated menu DTO service (selected):** Add a small `LocalWebUiMainMenuService` that composes existing session/lifecycle/status data, enumerates saves, exposes safe save IDs, and loads selected saves through `SaveLoadService`. The HTML consumes `/api/main-menu` and `/api/saves/load`.
3. **Full SPA extraction:** Split the inline host HTML into a real frontend bundle. Better long-term, but too broad for this closure unit and risks delaying the foundational main-menu behavior.

## Selected design

Add a dedicated browser main-menu service and endpoints:

- `GET /api/main-menu` returns a DTO with:
  - session summary: soul name, realm, turn label, current-session availability, continue blocker/warning text;
  - action list for Continue/New Game/Load/Options/About/Exit with enabled/disabled states and browser actions;
  - manual save list with opaque server-issued save IDs;
  - options/about/session guidance text.
- `POST /api/saves/load` accepts `{ saveId }`, resolves it only against the current enumerated save list, calls `SaveLoadService.LoadGameAsync`, and returns a refreshed menu DTO or a structured error.

The root HTML becomes a Reborn game shell:

- main menu hero is the first visible section;
- Continue enters the existing game shell section client-side;
- New Game opens the existing browser `/world_setup` flow through `executeCommand('/world_setup')` when local writes are available;
- Load opens a browser save list and calls `/api/saves/load` for selected saves;
- Options/About render player-facing panels with current known/browser-local state;
- Advanced/debug command palette remains present but starts collapsed behind an explicit advanced button.

## Tests

- `GET /` no longer contains “Local Web UI” as the primary identity and contains the main-menu landmarks/actions.
- `GET /api/main-menu` returns a current session summary, continue availability, Russian realm/soul labels, and disabled reasons when session state is missing, malformed, terminally dissipated, or invalid.
- Save list DTO includes manual saves and `POST /api/saves/load` loads only save IDs produced by the service.
- Existing command/lifecycle/QTE endpoint tests continue to pass.

## Self-review

No placeholders remain. The slice is limited to browser menu/session/save DTO surfaces and root-page presentation. It does not alter GM-authored afterlife contracts or C# gameplay rules.