# Browser Detail Surfaces Design

Tracked issue: [#728 — Browser Client IA: card → modal/full-panel detail surfaces](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/728)
Parent: #726. Related: #680, #722, #723.

## Context

The Browser Client already has the #727 player navigation taxonomy, the Vite/React frontend workspace, and a dark-fantasy CSS design-system split. The current in-game route content still uses flat `summary-card` blocks for detail-rich data such as soul/player/world state. Issue #728 asks for a reusable player-facing pattern: compact cards in the ordinary route grid/sidebar and a consistent modal/full-panel surface for deep detail.

Relevant constraints from repo guidance and project references:

- C# remains the runtime/gameplay/application authority. React may only render existing typed DTO data and interaction plumbing.
- Default Browser Client UI must remain Russian-first and player-facing; raw API/debug/JSON/command details stay behind explicit `Расширенный режим`.
- This task is UI/presentation-only. It must not change Afterlife runtime contracts, pending/control files, validation rules, GM prompts, or canonical state surfaces.
- The old React project was inspected as a UI/UX baseline only. Its useful pattern here is compact inventory/status cards that open focused detail modals/panels; its old mechanics/prompts are not product truth for Reborn.

## Approaches considered

1. **CSS-only expansion of existing `summary-card` blocks.** This is the smallest diff, but it would not create a reusable interaction contract, focus handling, or visual smoke artifact. It would satisfy styling but not the issue's card → deep-detail requirement.
2. **A lightweight reusable React detail-surface component.** Compact cards own short summaries and open a shared modal/full-panel with header/back/fullscreen/close controls, sections, empty/error/loading copy, and focus/keyboard handling. This satisfies the issue without moving logic into TypeScript.
3. **Introduce a full modal manager/router.** This would centralize all overlays, but it is larger than #728 needs and risks turning a focused UI task into an architecture refactor.

Chosen approach: **Approach 2**. It adds a reusable presentational component and applies it to representative player-facing surfaces, while keeping data and gameplay authority unchanged.

## Design

### Component pattern

Add a small React component module under `BookOfEternityClient.WebFrontend/src/components/DetailSurface.tsx`:

- `DetailSurfaceCard` renders a compact, focusable player-facing card with eyebrow, title, optional icon/status, short summary, and an explicit “Открыть детали” action.
- `DetailSurfaceModal` is internal to the component module. It renders `role="dialog"`, `aria-modal="true"`, consistent header, back, fullscreen/expand, and close controls.
- The component manages its own open/fullscreen state, focuses the close button when opened, restores focus to the originating card when closed, and closes on Escape.
- Detail content is passed as sections (`title`, optional `eyebrow`/`icon`, `content`) so route code can structure details without raw JSON dumps.
- Empty/loading/error states are player-facing props on the card rather than technical response dumps.

### Applied surfaces

Use the shared component in at least two normal player-facing surfaces:

1. `SoulRoute`: replace the flat soul/player summary cards with `DetailSurfaceCard` instances for “Душа” and “Герой”. Compact cards show short status. Detail panels show readable sections for identity, realm/incarnation, guardian/enlightenment, and health/energy/poise.
2. `WorldRoute`: replace the flat location summary with a `DetailSurfaceCard` for “Локация”. Compact card shows where/when the player is. Detail panel shows current location, world time, turn/session summary, and route guidance.

`ActionMenu`, advanced diagnostics, command execution, prompt sessions, and raw JSON handling remain unchanged and stay outside this player-default detail-surface pattern.

### Styling and responsive behavior

Add CSS to the existing design-system files:

- component styles in `src/styles/components.css` for `.detail-surface-card`, `.detail-surface-modal`, `.detail-surface-overlay`, `.detail-surface-sections`, controls, sections, and fullscreen state;
- layout styles in `src/styles/layout.css` for `.detail-surface-grid` and responsive behavior;
- mobile breakpoints make the overlay fill the viewport and the panel behave like a full-screen sheet rather than a small desktop modal.

### Tests and visual artifact

Add source guards to `BrowserFrontendWorkspaceTests`:

- require the DetailSurface component file and key accessibility/focus strings;
- require `SoulRoute` and `WorldRoute` to use the shared pattern;
- require detail-surface CSS classes and mobile behavior;
- guard against raw `/api/`, debug, command, and JSON copy in the detail-surface component.

Extend `LocalWebUiBuiltFrontendSmokeTests` to write `TestResults/browser-smoke/detail-surfaces.html`, a dependency-light visual smoke artifact containing:

- compact-card overview frame;
- opened desktop modal frame;
- mobile full-panel frame;
- player-facing sample labels from the current React source;
- assertions that raw debug/API/JSON wording is absent.

Update `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md` to document the #728 pattern and artifact.

## Non-goals

- No C# DTO or API contract changes.
- No gameplay/application logic in React.
- No Afterlife/Chaos Sea/Shining Abode runtime contract changes.
- No Playwright/Selenium dependency; full screenshots remain future browser automation work. This task uses the existing dependency-light HTML visual smoke approach.

## Self-review

- Placeholder scan: no TBD/TODO placeholders remain.
- Scope check: one focused implementation plan can complete #728 without absorbing #729 Afterlife panels or #723 broader visual QA.
- Consistency check: component, applied route usage, CSS, source guards, docs, and smoke artifact all point to the same card → modal/full-panel pattern.
- Ambiguity resolved: “visual artifacts” means a generated HTML smoke artifact in this PR, not screenshot automation.