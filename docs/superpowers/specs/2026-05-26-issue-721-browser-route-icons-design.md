# Issue #721 Browser Route Icons Design

## Context

Issue #721 asks the Browser Client to replace emoji route tiles with a unified, maintainable iconography and route-state system. The work is a frontend presentation slice under #718/#680: React may improve navigation visuals and state language, but the C# client/runtime remains the authority for game state, saves, commands, and contracts.

The old React project at `E:\Games\(test-version-0.9.14)-copy-of-the-book-of-eternity_-chronicle-of-the-unwritten-0.9` uses consistent Heroicons-style SVG route/tab icons for player-facing menus such as character, inventory, journal, and factions. For Reborn we will copy the pattern, not the dependency or product mechanics: use local inline SVG glyphs with Reborn route names and the current C# DTO-derived shell state.

## Goals

- Remove emoji literals from the default `playerRoutes` icon metadata and player-facing route cards.
- Add a single maintainable route icon system in `BookOfEternityClient.WebFrontend/src/App.tsx` with no external runtime dependency.
- Add semantic route card states: `active`, `available`, `locked`, `loading`, and `attention`.
- Make locked/no-session state feel intentional and muted, not like an error.
- Keep route cards accessible with keyboard focus, selected state, route labels, and state text exposed through aria labels.
- Add source guards so future changes do not reintroduce emoji icons into `playerRoutes`.

## Non-goals

- Do not add `@heroicons/react` or any other icon dependency.
- Do not move gameplay/session availability rules into React beyond presentation derived from already-fetched C# endpoint results.
- Do not alter C# browser API contracts or afterlife/runtime contracts.
- Do not hide real API failures; attention state should still surface failures while normal no-session locked state remains calm.

## Design

### Route metadata

`RouteCard.icon` becomes a route icon identifier such as `book`, `flame`, `soul`, `map`, `journal`, `satchel`, `gallery`, or `settings`. These identifiers are plain strings in a TypeScript union, not emoji display text. The `playerRoutes` array remains near the top of `App.tsx` so existing source guards and route-order tests keep working.

### Inline SVG renderer

Add a `RouteGlyph` component in `App.tsx`. It maps each `RouteIconId` to a small set of inline SVG paths using `viewBox="0 0 24 24"`, `stroke="currentColor"`, and `fill="none"`. The route card uses `<RouteGlyph icon={route.icon} />` inside a decorative `.route-card__icon` wrapper. The wrapper is `aria-hidden="true"`; the button aria label carries the route name and semantic state.

### Route states

Add `RouteAvailabilityState = 'active' | 'available' | 'locked' | 'loading' | 'attention'` and `RouteStateDetails` with a short Russian label:

- `active`: the route is currently selected.
- `available`: the route can be opened normally.
- `locked`: no active game screen exists yet for game-dependent routes (`game`, `soul`, `world`, `journal`, `inventory`, `media`). This is the ordinary no-session state.
- `loading`: the shell is still collecting endpoint state.
- `attention`: the relevant endpoint failed or the game turn state reports `error`/`repair`.

The state is presentation-only and derived from `BrowserShellState`, `readyState`, and current `activeRoute`. React does not invent save/load/gameplay rules.

### Styling

Update `src/styles/components.css` so route cards have a consistent icon medallion, state pill, and richer active/hover/focus/locked/attention treatment. Locked cards use muted/dashed styling. Attention cards use the existing repair/warning palette and only appears for real failures/repair states. Active route remains visually obvious through border, glow, icon medallion, and state pill.

### Documentation

Update `BookOfEternityClient.WebFrontend/README.md` to record issue #721 route icon/state guidance for future agents.

## Testing and verification

- Add a focused C# source guard in `BrowserFrontendWorkspaceTests` asserting:
  - `playerRoutes` no longer contains known emoji icon literals.
  - route icons use `RouteIconId` and `RouteGlyph`.
  - route state helpers and CSS classes exist for active/available/locked/loading/attention.
- Follow TDD: run the focused test and watch it fail before production changes.
- Run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- Run focused .NET browser frontend tests.
- Run `git diff --check`.

## Self-review notes

- No placeholders or deferred requirements remain.
- Scope is limited to React/CSS/docs/tests for route cards.
- No C# runtime, afterlife contract, or GM-facing prompt change is needed because this is a frontend presentation-only slice.
