# Browser Fresh Launcher / No-Session State Design

Issue: #741 — [Browser Client][Audit] Fresh launcher/no-session state reads as broken repair state

## Goal
Make the first-launch Browser Client experience read as a calm game launcher when no playable chapter exists, while keeping real repair/validation diagnostics available behind advanced mode.

## Current root-cause analysis
The local web host creates the base `game_session` directory on startup. That makes `/api/session` report that a session directory exists even when no playable soul/chapter has been created yet. `/api/game-screen` then always builds a full `BrowserGameScreenDto` from empty/default state; the React shell treats that as an active game and renders repair/validation/waiting surfaces from lifecycle data. Separately, the default shell uses `menu.session.validationLabel` and a fixed sidebar card title `Ожидание ГМа` even when no chapter is playable.

## Approach
Use the C# lifecycle/menu state as the authority for whether a playable chapter exists. If the lifecycle dashboard has no readable soul/current session, `/api/game-screen` should return a player-safe 404 payload that the existing TypeScript API client classifies as `no-active-session`. The React shell should then render the established empty/launcher states instead of an active game screen.

In the React shell, no-session rendering should avoid validation/repair/waiting labels in default UI:
- the hero status falls back to “Глава ещё не открыта” rather than `Есть ошибки валидации`;
- the layer/save sidebar summaries treat an auto-created empty `game_session` as “no active chapter” when the menu says there is no readable soul;
- the turn card title becomes “Ход ещё не начат” while no game screen exists, so `Ожидание ГМа` is reserved for real playable turn state.

## Data flow
1. `LocalWebUiHost` still initializes directory structure as before.
2. `BrowserGameScreenService.BuildAsync()` refreshes C# state, builds lifecycle, and checks `lifecycle.Session.GameSessionExists && lifecycle.Soul.IsReadable`.
3. If no playable chapter exists, it throws a dedicated no-active-session exception with a sanitized error mentioning `game_session` so the existing API client maps it to `no-active-session`.
4. `LocalWebUiHost` maps that exception to HTTP 404 JSON `{ error = ... }`.
5. React already treats `no-active-session` as an empty state for routes; small shell helpers ensure default hero/sidebar copy is onboarding-first.

## Testing
- Add a C# host test proving `/api/game-screen` returns 404 and a player-safe no-active-session payload for a fresh empty host root.
- Add source-guard assertions that the React sidebar has conditional no-turn title copy and no longer directly renders menu validation labels for the no-session hero/layer default.
- Extend the dependency-light first-screen visual QA artifact to include an explicit `data-state="fresh-empty"` launcher state with neutral no-active-chapter copy and no `Ожидание ГМа` text.
- Run focused Local Web UI/frontend guard tests, frontend verification, browser smoke/parity filters, and the broader test project before PR/merge.

## Docs/prompts impact
This is Browser Client UI/API behavior only. It does not change Afterlife runtime contracts, pending/control files, response fields, validation rules, or GM-authored surfaces. GM-facing Afterlife docs are not touched.

## Self-review
- No placeholders/TBD remain.
- Scope is a single closure unit for #741 and does not include audio diagnostics (#746), prompt form failures (#745), or world action hierarchy (#744).
- The design keeps C# as gameplay/application authority and React as presentation.
- The implementation avoids masking real active-session validation/repair states: the no-session branch only applies when the lifecycle dashboard lacks a readable soul/current session.
