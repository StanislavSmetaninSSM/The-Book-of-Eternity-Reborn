# Issue #704 React Browser App Shell Design

Tracked issue: #704 — [Browser Client Architecture] Build React app shell, routing, and player/advanced UI separation.

## Context

Issues #701, #702, and #703 created the Vite/React/TypeScript workspace, connected it to the C# local web host, and added a typed browser API client. Issue #682 already gave the legacy fallback shell a player-facing game screen, but the React entry point is still a roadmap card page. Future Browser Client feature issues (#683-#689) need a reusable framework shell with routes, shared request state, realm theming, and an explicit advanced/debug boundary.

Stanislav authorized unattended execution, so this design is self-approved after review. The implementation stays conservative: React owns presentation, navigation, request state, and UI composition; C# remains the only gameplay/application authority for saves, commands, validation, afterlife/mortal contracts, local-write gating, and QTE/domain mutation.

The old TypeScript project was inspected as a UI/UX reference only. The useful lessons are comfortable three-column game layout, tabbed/player sections, inventory/factions/character-style panels, and secondary debug views. Its prompts and mortal-life-only mechanics are not product truth for Reborn.

## Approaches considered

1. **Port the old TypeScript app shell.** This would provide rich UI quickly, but it risks importing obsolete mechanics, prompt assumptions, and a large unreviewable architecture.
2. **Build a focused Reborn React shell using the typed #703 API (selected).** Add small framework components, typed shared state hooks, route descriptors, player-facing placeholders, and an advanced panel. This satisfies #704 foundations without duplicating game logic or finishing all downstream feature screens.
3. **Keep expanding `public/local-web-ui-shell.html`.** This preserves current smoke coverage but contradicts the #701/#702 architecture direction and makes #683-#689 continue to grow a raw HTML/JS shell.

## Selected architecture

The React workspace becomes the default framework shell when a Vite build exists:

- `LocalWebUiFrontendAssets.Resolve()` should prefer `dist/index.html` over a copied `dist/local-web-ui-shell.html`. The tracked `public/local-web-ui-shell.html` remains the no-build fallback only.
- `BookOfEternityClient.WebFrontend/src/App.tsx` becomes a real app shell: top navigation, route buttons, player/default route content, game layout regions, settings/media/placeholders, and an explicit `Расширенный режим` / technical panel.
- `src/api/client.ts` remains the only endpoint call boundary. React components use `browserApi` / `BrowserApiClient` methods; they do not scatter raw endpoint strings or duplicate C# rules.
- Shared state is local React state plus effects: session status, main menu, game screen, lifecycle/repair state, active route, advanced-mode state, loading and player-facing error states. Errors render `playerMessage` by default and put `technicalDetails` behind details/disclosure in advanced context.
- Reusable UI units stay in the frontend workspace and remain presentation-only: shell frame, cards/panels, alert/loading/error components, route/nav buttons, status bars, realm theme tokens, action composer placeholder, and advanced diagnostics summary.

## Player-facing route model

The root player journey is Russian-first and does not require slash command knowledge:

- `Главная` / main menu and session management.
- `Игра` / in-game screen with narrative, soul/player/world summaries, turn/lifecycle status, QTE status, and primary prose composer placeholder.
- `Душа` / character-soul-status panel.
- `Мир` / map, journal, quests, actions, factions placeholder regions.
- `Медиа` / gallery/QTE/media placeholder region.
- `Настройки` / local profile/settings placeholder region.
- `Расширенный режим` / opt-in technical route or drawer for command/API diagnostics.

Command IDs, endpoint names, and API details appear only after explicit advanced opt-in. Default routes may mention that technical details are available in advanced mode, but they must not make `/api/*`, `/debug`, raw command palette, or endpoint IDs the central player experience.

## Host/static asset behavior

Issue #702 deliberately kept the copied fallback shell preferred while the React app was only a placeholder. Issue #704 is the transition point: when `npm run build --prefix BookOfEternityClient.WebFrontend` creates both `dist/index.html` and `dist/local-web-ui-shell.html`, the C# local host must serve the React `index.html`. If no build exists, it may still serve `public/local-web-ui-shell.html` to keep `--web` usable.

SPA fallback behavior remains unchanged: browser routes can fall back to the shell, but `/api/*` and `/assets/*` misses must return real 404s and must not be swallowed by HTML fallback.

## Testing strategy

1. RED: add .NET/source guard tests that fail until:
   - build-root resolution prefers React `index.html` over copied fallback shell;
   - React source defines player route descriptors, shared state fields, advanced opt-in, typed API client loading, reusable components, and Russian player labels;
   - docs mention #704 shell/routing/player-vs-advanced contracts.
2. GREEN: implement the minimal React shell and host preference needed for the tests.
3. Verify with focused .NET tests, frontend typecheck/build, broader browser/local web UI tests, `dotnet publish`, and `git diff --check`.
4. Use an independent review for spec compliance and code quality before PR/merge.

## Documentation impact

Update:

- `BookOfEternityClient.WebFrontend/README.md` with the #704 shell structure, route/component boundaries, and verification commands.
- `docs/web-ui/local-web-host.md` with #704 tracked task, React shell behavior, build-root preference, player/advanced separation, and no-build fallback notes.
- `docs/superpowers/plans/2026-05-25-issue-704-react-app-shell.md` for implementation handoff.

No Afterlife runtime contract, pending/control file, GM-authored prompt, or mortal mechanics change is planned. GM-facing afterlife/mortal documentation updates are not required for this shell slice.

## Self-review

- Placeholder scan: no TBD/TODO placeholders.
- Consistency: React remains presentation/UI infrastructure and consumes typed C# DTOs instead of adding game rules.
- Scope: one closure unit for #704; detailed feature screens remain in #683-#689, and CI/smoke expansion remains in #705.
- Ambiguity resolved: issue #704 is the point where built React `index.html` becomes preferred over the copied fallback shell; the legacy fallback remains only for no-build scenarios.
