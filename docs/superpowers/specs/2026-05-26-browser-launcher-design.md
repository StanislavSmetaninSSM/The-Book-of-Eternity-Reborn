# Browser Launcher Design (#720)

## Context

Issue #720 is the next Browser Client visual follow-up after #719. The current `HomeRoute` already has player-facing copy, but it still reads like a list of same-priority action buttons inside a dashboard panel. The old React reference (`components/StartScreen.tsx` in `E:\Games\(test-version-0.9.14)-copy-of-the-book-of-eternity_-chronicle-of-the-unwritten-0.9`) uses a stronger launcher pattern: a centered game window, segmented mode choice, a clear primary path, and lower-priority save/config/about actions. Reborn should copy that information hierarchy only, not old mortal-life mechanics, prompts, providers, or API-key configuration.

Stanislav explicitly authorized unattended execution, so this design records the approval gate decision: proceed conservatively inside the tracked task #720 and keep all gameplay/application authority in existing C# APIs.

## Goals

- Make the default `Главная` screen feel like a start launcher rather than a dashboard.
- Show one visually dominant primary CTA derived from the existing C# `BrowserMainMenuDto.actions` state.
- Present save/load, new chapter, settings, and about actions as player-facing launcher choices with secondary visual weight.
- Keep advanced/debug mode visually secondary and outside the launcher hierarchy.
- Keep React as presentation/UI plumbing only; do not add separate gameplay rules.

## Approach Chosen

Use a focused React presentation refactor in `BookOfEternityClient.WebFrontend/src/App.tsx` plus CSS/source guards. `HomeRoute` will compute a `LauncherPrimaryAction` from `menu.actions`: prefer enabled `continue`, then enabled `load`, then enabled `new-game`, then the first available action, otherwise a disabled open-book CTA. The CTA will navigate within existing UI surfaces: `continue` opens the game route, `load` selects the save tab, `new-game` selects the new-chapter tab and explains that the guided form is tied to the existing C# command flow. Save slots call the already-existing `browserApi.loadSave()` endpoint and refresh shared state through the parent `loadBrowserState` callback.

## Components and Data Flow

- `App` passes `setActiveRoute` and `loadBrowserState` into `HomeRoute`.
- `HomeRoute` owns local launcher mode (`continue`, `load`, `new-game`, `settings`, `about`) and load notice state.
- `LauncherPrimaryAction` and `LauncherSecondaryAction` are view models built from `BrowserMainMenuDto.actions`; they do not invent game availability.
- Save loading uses `BrowserSaveSlotDto.saveId` and `browserApi.loadSave({ saveId })`; on success the app refreshes menu/session/game/audio state from C#.
- Settings and about render existing `menu.options` and `menu.about` as lower-priority launcher panels.

## Error Handling

- Disabled C# actions remain disabled with the C# disabled reason converted through `toPlayerFacingText`.
- Save-load failures show a player-facing notice from the API result without exposing raw local paths.
- Technical diagnostics and raw command/API details remain in `AdvancedDiagnosticsPanel` behind explicit `advancedEnabled`.

## Testing

- Add a failing xUnit source guard proving the launcher has `function GameLauncher`, `launcher-primary-action`, `launcher-mode-tabs`, `launcher-save-list`, `browserApi.loadSave`, one primary CTA copy, and advanced toggle remains separate.
- Run the focused guard RED before production code.
- Implement minimal React/CSS changes.
- Verify with the focused guard, `npm run verify --prefix BookOfEternityClient.WebFrontend`, relevant Browser/LocalWebUi .NET tests, `git diff --check`, and an added-line security/static scan.

## Scope Boundaries

No C# runtime contract change is planned. No Afterlife or mortal mechanics change is planned. No GM-facing prompt/documentation update is required. Visual screenshot artifacts are deferred to #723; this task adds source/smoke guards only.

## Self-Review

- Placeholder scan: no TBD/TODO placeholders.
- Consistency: chosen approach matches #720 acceptance criteria and Book Browser Client architecture references.
- Scope: one UI closure unit; #721/#722/#723 remain separate.
- Ambiguity: primary CTA selection is explicit and based on C# menu action availability.
