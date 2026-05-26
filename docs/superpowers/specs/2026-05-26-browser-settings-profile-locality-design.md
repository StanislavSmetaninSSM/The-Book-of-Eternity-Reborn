# Browser Settings, Profile, and Locality Design

Issue: #689 — [Browser Client] Настройки, профиль и локальность клиента

## Context

The browser client already has a React/Vite shell, a player-facing settings route placeholder, and a persistent audio panel backed by shared `GameSettings`. The remaining gap is that the settings route still reads like a status summary and does not let the player edit core client preferences from the browser.

The implementation must keep C# as the settings authority. React may render controls and call typed local endpoints, but it must not introduce browser-only gameplay rules or a separate settings store.

## Approach

Add a safe browser settings API over the existing `StateManager.Settings` object:

- `GET /api/client/settings` returns only player-safe settings and local client status.
- `POST /api/client/settings` updates a whitelisted subset of `GameSettings`, clamps numeric values, persists `game_session/config.json`, applies audio settings, and refreshes `game_state/core/game_settings.json` for GM-readable difficulty/QTE flags.
- Technical/dangerous settings such as API keys, bridge backend, pipe names, launch commands, image provider internals, and system mod contents remain out of the player-default DTO.

The React settings route will fetch this DTO, render grouped player-facing controls, and post partial updates. It will use the same typed contract/fixture guard pattern as other browser endpoints.

## Player-facing settings

Default settings route exposes:

- Language: `ru` / `en` labels, persisted through `GameSettings.Language`.
- Difficulty: `normal`, `hard`, `impossible`, persisted through `GameSettings.Difficulty`.
- Show GM thoughts: persisted through `GameSettings.ShowGmThoughts` as an explicit opt-in.
- Audio: music/sound enabled and volumes, using the same shared settings as the persistent audio panel.
- Accessibility: browser font scale, reduced motion, and contrast-friendly mode, stored in new shared `GameSettings` fields.
- Locality summary: localhost-only status, safe session label, and GM bridge status without exposing raw backend/command/pipe settings.

## Data flow

1. `LocalWebUiHost` registers `BrowserClientSettingsService`.
2. Browser shell load includes `browserApi.getClientSettings()` together with menu/session/game/audio.
3. `SettingsRoute` receives the typed result and the current `/api/session` result.
4. UI changes call `browserApi.updateClientSettings(partialRequest)`.
5. The service serializes writes through the existing browser settings gate, updates the shared `GameSettings`, saves config, writes the GM-facing difficulty projection, applies audio settings, and returns the updated DTO.
6. React applies accessibility classes/variables from the DTO so changes affect the browser client without a separate settings store.

## Error handling and safety

- Endpoint request fields are optional; omitted fields leave existing settings unchanged.
- Volumes clamp to 0–100.
- Font scale clamps to a comfortable browser range.
- Unknown language/difficulty values normalize to safe defaults instead of creating invalid config.
- Raw local filesystem paths are not displayed in the player settings route. Existing shared session DTO may still contain raw paths for advanced/diagnostic use, but the route renders a short safe label.
- Technical bridge details stay behind advanced diagnostics; player-default settings show only whether the local GM bridge is enabled.

## Testing and verification

- Add LocalWebUiHost endpoint tests for GET/POST settings persistence, clamping, GM projection, and safe DTO content.
- Add API contract tests and fixture checks for `BrowserClientSettingsDto` / update request.
- Add frontend source guard tests proving the settings route uses the typed endpoint, exposes player controls, applies accessibility state, and does not display dangerous technical setting names in the default settings route.
- Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and focused .NET browser tests, then broader .NET tests before merge.

## Autonomous approval note

Stanislav explicitly authorized unattended worker execution. The approval gate is handled by this conservative design, self-review, and PR evidence rather than waiting for human confirmation during the scheduled run.

## Self-review

- No placeholders remain.
- Scope is limited to issue #689 settings/profile/locality and does not add new gameplay logic.
- Browser frontend remains presentation-only over C# settings authority.
- Dangerous technical settings are explicitly excluded from the player-default DTO/UI.
