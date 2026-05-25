# Browser Sidebar Player Status Design (#722)

## Issue and intent

Issue #722 asks the Browser Client sidebar to stop reading as a technical/debug status dashboard. The default sidebar should be a player-facing "book/world status" column that helps a player understand the current realm, soul/hero, save/session, turn/GM waiting state, and audio availability without seeing raw validation, repair, endpoint, or unavailable-noise language. Technical diagnostics remain available only after the explicit advanced-mode toggle.

This is a UI/presentation change only. It must not move gameplay, save/load, turn lifecycle, audio settings, or afterlife contract authority into React. React continues to render shared C# DTOs from `BrowserMainMenuDto`, `LocalWebUiSessionStatus`, `BrowserGameScreenDto`, and `BrowserAudioSettingsDto`.

## Reference context

- Current Reborn frontend: `BookOfEternityClient.WebFrontend/src/App.tsx` renders three default sidebar panels: `Сессия`, `Ход и ремонт`, and `AudioSettingsPanel`, followed by a visually prominent advanced toggle.
- Old React UI reference: `E:\Games\(test-version-0.9.14)-copy-of-the-book-of-eternity_-chronicle-of-the-unwritten-0.9` uses a persistent right side panel with tabbed/player-facing character, world, faction, map, log, and settings surfaces. For Reborn we should borrow the information hierarchy and panel feel, not old mortal-only mechanics.
- Loaded Book references: player-vs-advanced separation, start launcher UX, React app shell, contextual action menu, smoke/parity, and design-system visual smoke.

## Chosen approach

Use a small presentational sidebar component family inside `App.tsx` and CSS additions in `components.css`:

1. Replace the raw `ShellPanel title="Сессия"` / `ShellPanel title="Ход и ремонт"` sidebar block with `PlayerStatusSidebar`.
2. `PlayerStatusSidebar` composes small `StatusSummaryCard` sections:
   - `Слой книги`: realm, turn label, validation in player language, no-session empty copy.
   - `Герой и душа`: soul/hero summary and health/energy/poise when game data exists; otherwise soft locked/no-session guidance.
   - `Сохранение`: save/session availability, local-only note, browser-write availability in player wording.
   - `Ход`: pending/waiting/repair/ready state using existing `formatTurnStateTitle` and `formatTurnStateMessage`; raw validation/repair details are only mentioned as advanced-only.
3. Keep `AudioSettingsPanel` in the sidebar but wrap it under the same player-status visual hierarchy; if audio API is unavailable, its existing empty state remains soft and non-technical.
4. Move the advanced toggle into a low-priority `advanced-sidebar-entry` block with explanatory copy, not as the most prominent standalone action.

Rejected alternatives:

- Add new C# DTO fields for a sidebar contract: unnecessary for #722 because existing shared DTOs already expose enough state; adding runtime contract surface would broaden scope and require additional docs.
- Implement a broad IA/nav refactor from #727/#728: explicitly out of scope; #722 stays focused on the default sidebar/status panel.

## Data flow

- `BrowserShell` already loads menu/session/game/audio DTOs through `loadBrowserState`.
- The new sidebar receives the existing `readyState`, resolved `menu`, `session`, `gameScreen`, `realmTheme`, `activeRoute`, and advanced toggle state.
- `PlayerStatusSidebar` renders only derived labels and summaries. It never mutates session state and never calls save/load or turn APIs.
- Advanced diagnostics still load lazily only when `advancedEnabled` is true.

## Error and empty-state handling

- Normal `no-active-session`/no game screen is not an error: render soft locked cards like “Книга ждёт открытия” and “Откройте или загрузите главу”.
- Real API failures still use `EmptyOrFailure`/`ApiFailure`, but player copy remains concise by default and technical details require advanced mode.
- Validation/repair details, raw file paths, endpoint/HTTP/API terminology, command coverage, and lifecycle issue lists stay inside `AdvancedDiagnosticsPanel`.

## Tests and guards

Add a focused guard in `BrowserFrontendWorkspaceTests` that fails until the source contains the new player-status sidebar markers and no longer contains the default sidebar titles `Сессия` and `Ход и ремонт`. The guard also checks that the advanced toggle appears after the player status sidebar in source order and that CSS includes the sidebar card classes.

Verification commands:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserFrontendWorkspaceTests"`
- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|WebUi|LocalWebUi|Browser" --logger "console;verbosity=minimal"`
- `git diff --check`
- Vite preview visual smoke of `/` after build.

## Documentation impact

This is Browser Client presentation work only. It does not change afterlife pending/control files, action types, validation rules, canonical state surfaces, or GM-authored behavior. No GM-facing afterlife contract docs are required. The design/spec and implementation plan provide the agent-facing documentation for #722.

## Self-review

- Placeholder scan: no unresolved placeholders or incomplete sections.
- Scope check: one closure unit, focused on #722 sidebar/status presentation.
- Ambiguity check: advanced diagnostics remain explicit opt-in; no new runtime contract; no gameplay logic in React.
