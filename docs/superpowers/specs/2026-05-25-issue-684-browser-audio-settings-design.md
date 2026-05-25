# Issue #684 Browser Audio Settings Design

## Context

Issue #684 asks the Browser Client to feel like a game by adding an explicit music/sound layer and browser settings UI. The existing C# client already owns shared settings through `GameSettings.MusicEnabled`, `MusicVolume`, `SoundEnabled`, and `SoundVolume`, and console playback uses `AudioService`. The React browser shell already has a `Настройки` route, typed API contract files, and player/advanced separation.

Relevant constraints checked for this design:

- `AGENTS.md` requires a tracked task; this work is tied to GitHub issue #684.
- Browser UI remains presentation-only. C# remains the settings, file catalog, persistence, and game/application authority.
- Browser playback must respect autoplay restrictions: no music/sound starts until the player clicks an explicit unlock/play control.
- Audio asset endpoints must not expose local filesystem paths and must fail safely when files are absent.
- The old 0.9.14 project is a UI/UX reference only. Its useful lesson for this task is a clear notification-sound enable flow explaining browser autoplay policy; old generated music mechanics and prompts are not product truth for Reborn.

## Approaches considered

### Option A — React-only local preferences

Store browser-only mute/volume values in `localStorage` and play assets directly from hard-coded `/Music/...` paths.

- Pros: fastest frontend-only change.
- Cons: diverges from console settings, exposes asset paths, duplicates settings authority, and fails the shared settings acceptance criterion.

### Option B — C# browser audio DTO + safe asset catalog + React unlock flow (chosen)

Add a C# `BrowserAudioService` that loads/saves the shared `GameSettings`, projects safe playlist/cue metadata from existing local audio folders, and serves only catalogued files through opaque asset IDs. React consumes the typed DTO, renders player-facing controls, and starts playback only after a user gesture.

- Pros: shared settings stay authoritative, local paths stay hidden, missing files become normal unavailable metadata/404s, and React remains presentation/playback glue.
- Cons: touches C#, typed TypeScript contracts, fixtures, and React UI.

### Option C — Reuse `AudioService` directly for browser playback

Call `AudioService.PlayMainMenuMusicAsync()` from browser endpoints.

- Pros: reuses console playback.
- Cons: plays audio on the host machine rather than in the browser tab, bypasses browser autoplay semantics, and does not give the player tab-local controls.

## Chosen architecture

Create `BookOfEternityClient/WebUi/BrowserAudioService.cs` as the browser-facing audio/settings adapter. It will:

- call `StateManager.LoadSettingsAsync()` before building the DTO so browser values match `game_session/config.json`;
- expose `BrowserAudioSettingsDto` with shared booleans/volumes, Russian autoplay guidance, playlist summaries, cue summaries, and no filesystem paths;
- update only the four shared audio settings through `BrowserAudioSettingsUpdateRequest`, clamp volumes to `0..100`, persist via `StateManager.SaveSettingsAsync()`, and call `AudioService.ApplySettingsAsync()` so console-side playback reacts to changed settings;
- resolve audio files from the same local folders as `AudioService`: `<base>/BookOfEternityClient/Music`, `<base>/Music`, `<base>/BookOfEternityClient/Sounds`, and `<base>/Sounds`;
- serve assets through `/api/audio/assets/{assetId}` only when `assetId` came from the current catalog, returning 404/400 instead of path traversal or local path leakage.

React extends the typed browser API with `getAudioSettings()` and `updateAudioSettings()`. The shell loads audio settings together with menu/session/game state. A persistent audio panel is mounted in the browser shell sidebar so playback survives player route changes, while the `Настройки` route points players to the same controls. The panel has toggles, sliders, and a clear `Включить музыку в браузере` button. That button is the browser user gesture: it chooses the main-menu playlist on the home route and the in-game playlist elsewhere, creates/updates an `HTMLAudioElement`, applies shared volume, and catches `play()` failures with a Russian player-facing explanation. Missing assets render as “файлы не найдены” rather than crashes. A cue preview button plays an available notification/QTE cue only after its own explicit click path.

## Data flow

1. `GET /api/audio/settings` loads shared settings and returns browser-safe playlist/cue metadata.
2. React renders settings and the autoplay explanation. It does not start playback during page load.
3. The player clicks `Включить музыку в браузере` from the persistent shell audio panel.
4. React selects a playlist from the DTO according to the active player route, picks the first available track, sets `audio.src` to `/api/audio/assets/{assetId}`, sets `audio.volume = musicVolume / 100`, enables looping, and calls `play()` inside the click handler.
5. If the browser blocks playback or the file is missing, React shows a concise Russian notice and keeps the UI usable.
6. Slider/toggle changes call `POST /api/audio/settings`, which persists the same `GameSettings` fields used by the console client and returns the refreshed DTO.
7. Asset requests validate the opaque ID against the current C# catalog; no direct local path is accepted from the browser.

## Error handling and safety

- Missing music/sound directories produce empty playlist/cue availability and a Russian “аудиофайлы не найдены” notice.
- Invalid asset IDs and path traversal attempts return 404/400 and never serialize local paths.
- Volume inputs are clamped server-side, so browser bugs cannot persist out-of-range settings.
- Network/API failures use the existing `BrowserApiResult` path and are shown as player-facing messages; technical details remain in advanced diagnostics.
- This task changes browser UI/settings/audio asset surfaces only. It does not change Chaos Sea / Shining Abode afterlife runtime contracts, GM-authored control files, or mortal-world mechanics; no GM-facing afterlife contract docs are required.

## Testing strategy

- Add failing C# host tests for audio settings GET, settings POST/persistence/clamping, and safe asset serving/path rejection.
- Add typed contract fixture tests for `BrowserAudioSettingsDto` and `BrowserAudioSettingsUpdateRequest`.
- Add React source/contract guard tests proving the settings route has an explicit autoplay unlock button, no auto-play on load, shared settings update call, and no raw local path usage.
- Run focused browser/audio tests first, then TypeScript typecheck/build, then broader browser web UI smoke/parity filters.

## Self-review

- No unresolved TBD/TODO placeholders.
- Scope is a single closure unit for #684: shared audio settings API, safe asset metadata/serving, and player-facing browser controls.
- C# remains authoritative for settings and asset catalog; React only renders and plays browser-tab audio after a user gesture.
- Acceptance criteria are covered: main-menu/in-game playlists when assets exist, shared persisted settings, missing-file safety, and explicit autoplay explanation.
