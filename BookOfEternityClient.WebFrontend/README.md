# Book of Eternity Reborn Browser Frontend

This workspace is the Vite + React + TypeScript foundation for the long-term Browser Client.

The C# runtime remains the authority for game rules, persistence, command handling, validation, afterlife/mortal contracts, and local-write safety. TypeScript owns presentation, request state, UI composition, and interaction plumbing only.

## Commands

From the repository root:

```powershell
npm install --prefix BookOfEternityClient.WebFrontend
npm ci --prefix BookOfEternityClient.WebFrontend
npm run dev --prefix BookOfEternityClient.WebFrontend
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend
npm run preview --prefix BookOfEternityClient.WebFrontend
```

Or from this directory:

```powershell
npm install
npm ci
npm run dev
npm run typecheck
npm run build
npm run verify
npm run preview
```

`npm run dev` and `npm run preview` bind to `127.0.0.1` for local development. `npm run build` writes production assets to `dist/`. `npm run verify` is the CI/local frontend gate: it typechecks both TypeScript projects and then builds the production bundle.

## Relationship to `dotnet run -- --web`

Issue #702 connects this workspace to the C# local web host. Issue #704 turns the Vite entry point into the first real React app shell. `dotnet run --project BookOfEternityClient -- --web` keeps serving the existing loopback-only Minimal API endpoints from C#, then serves frontend assets in this order:

1. `BookOfEternityClient.WebFrontend/dist/index.html` for the built React app shell.
2. `BookOfEternityClient.WebFrontend/dist/local-web-ui-shell.html` only if a build root somehow has the copied fallback shell but no React index.
3. `BookOfEternityClient` output `wwwroot/browser/` for packaged/published builds that copied the Vite output or fallback shell.
4. The source fallback shell `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html` when no build output is present.

The fallback shell is the extracted player-facing MVP shell. It keeps `--web` usable without a frontend build, but the preferred built root is now `dist/index.html`. Generated `dist/`, `node_modules/`, `.vite/`, and `*.tsbuildinfo` stay ignored and should not be committed.

## React app shell (#704, #727)

The React app shell defines player-facing routes as presentation routes only: they consume `src/api/client.ts`, render typed C# DTOs, and leave all game/application authority in the C# runtime.

Issue #727 refines the player navigation taxonomy. The primary chain is now `Главная → Игра → Душа → Мир → Журнал → Инвентарь`, matching the game-client order: summary/current scene, character/soul, world/location/map, journal/quests/notes, and inventory/craft. `Медиа` and `Настройки` remain player utility sections below the primary chain.

The shell keeps default UI Russian-first and player-facing. Command IDs, `/api/*` endpoint details, lifecycle validation internals, command coverage, and slash-command diagnostics stay behind explicit `Расширенный режим` opt-in. Player route failures should render `playerMessage`; `technicalDetails` belongs in the advanced diagnostics/details surface.

Issue #688 makes the `Медиа` route consume `/api/game-screen.media` and render gallery images, the realm atlas, and QTE controls as player-facing sections. Raw endpoint diagnostics for media/QTE remain advanced-only.

Future Browser Client tasks (#683-#689) should extend these route regions rather than recreating ad-hoc DOM manipulation:

- main menu/session flow under `Главная`;
- narrative, turn state, QTE, and prose action composer under `Игра`;
- character/soul/status cards under `Душа`;
- map, location, factions, and world actions under `Мир`;
- quests, chronicle, archive, notes, and story-facing sections under `Журнал`;
- items, equipment, craft, and storage-facing sections under `Инвентарь`;
- gallery, media, and QTE visuals under `Медиа`;
- local profile, language, audio, and comfort settings under `Настройки`.

Verify shell changes with `npm run typecheck --prefix BookOfEternityClient.WebFrontend`, `npm run build --prefix BookOfEternityClient.WebFrontend`, and focused browser .NET tests such as `BrowserFrontendWorkspaceTests` / `LocalWebUiHostTests`.

## Route iconography and states (#721)

Issue #721 replaces default route emoji tiles with local inline SVG glyphs. `playerRoutes` should store `RouteIconId` values, and `RouteGlyph` renders the decorative SVG inside each route card. Do not add external icon packages or return to emoji literals for default player routes.

Route cards expose semantic presentation states derived from the existing browser shell results: `active`, `available`, `locked`, `loading`, and `attention`. `locked` is the ordinary no-session/no-active-chapter state and should stay muted, not red. `attention` is reserved for real endpoint failures or repair/error turn states. These states are visual/accessibility hints only; C# remains authoritative for session, save/load, turn, and gameplay rules.

## First-screen visual QA (#723)

Issue #723 adds a dependency-light HTML visual smoke artifact for the Browser Client default first screen. Run:

```powershell
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

The test writes `TestResults/browser-smoke/first-screen-visual-qa.html` next to `root.html`, `game-route.html`, `network.json`, `navigation-ia.html`, `detail-surfaces.html`, and `reborn-panels.html`. CI uploads the directory as `browser-smoke-artifacts`.

The artifact is an HTML visual smoke artifact, not automated PNG screenshots. It intentionally remains dependency-light until a future tracked task selects a browser screenshot automation stack. It is local/offline and has no external API key required. Review it against the old React UI/UX reference only: central game launcher, primary CTA, polished cards, tabs/sections, and save/config actions. Do not copy old prompts, mortal-life-only mechanics, or runtime rules.

Regression checklist: primary CTA present; no technical hero copy; no repeated unavailable alerts; no emoji route icons; advanced debug secondary and behind explicit opt-in. Source guards live in `BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist` in `BrowserFrontendWorkspaceTests`, and documentation guards live in `LocalWebHostDocs_DocumentBrowserVisualQaArtifactWorkflow` in `LocalWebUiDocumentationTests`.

## Detail surfaces (#728)

Issue #728 adds a shared Browser Client `card → modal/full-panel` pattern for detail-rich player-facing data. Compact cards keep the route/sidebar overview readable; opening a card shows a consistent detail surface with header, back/fullscreen/close controls, readable sections, player-facing empty/error/loading copy, Escape handling, and focus restoration.

The first representative surfaces are the `Душа`, `Герой`, and `Локация` cards. They render existing `/api/game-screen` DTO data only; they do not add gameplay rules, raw API details, raw JSON, or slash-command diagnostics. Those remain behind explicit `Расширенный режим` where appropriate.

The built-frontend smoke test writes `TestResults/browser-smoke/detail-surfaces.html` as a dependency-light visual smoke artifact for compact cards, an opened desktop modal, and mobile full-panel behavior. Full screenshot automation remains a separate future task.

## Reborn panels (#729)

The Browser Client renders player-facing Reborn panels for Afterlife, Shining Abode, and Chaos Sea from the existing game-screen state and player-default action metadata. This is a UI-only mapping: the C# runtime remains the authority for afterlife state, command availability, validation, and all gameplay rules.

The panels intentionally avoid raw contract filenames, pending/control files, endpoint names, GM thoughts, and debug logs in the default player UI. Those details stay in advanced diagnostics or GM-facing documentation.

Because #729 does not add or rename afterlife action types, pending/control files, validation rules, canonical state fields, normalizer behavior, or GM-authored prompts, GM-facing afterlife contract docs were not changed for this UI slice.

## Browser design system (#685)

Issue #685 splits the Browser Client styling into a maintainable plain-CSS design system:

- `src/styles.css` remains the Vite import entrypoint.
- `src/styles/tokens.css` defines dark-fantasy color, realm, state, typography, spacing, shadow, and motion tokens.
- `src/styles/base.css` owns document reset, typography, background texture, scrollbars, and focus treatment.
- `src/styles/components.css` owns reusable cards, panels, buttons, forms, alert states, action cards, audio controls, and advanced diagnostics.
- `src/styles/layout.css` owns shell, hero, route, workspace, route-grid, and responsive layout rules.
- `src/styles/motion.css` owns restrained panel/QTE/waiting motion plus `prefers-reduced-motion` safeguards.

The visual direction is dark-fantasy chronicle UI: ink/obsidian background, parchment/gold narrative hierarchy, realm-aware accents from the C# game-screen DTO, clear desktop/mobile breakpoints, and technical labels only inside explicit advanced mode. CSS/React stay presentation-only; gameplay, validation, saves, commands, and afterlife contracts remain in the C# runtime.

## Contextual action menu (#683)

Issue #683 adds a player-facing contextual action menu to the `Мир` route. The menu is built from the C# `ExplorerCommandCatalog` and `/api/game-screen` action-menu DTO, then rendered by React as Russian game sections: `Персонаж / Душа`, `Мир`, `Квесты`, `Карта`, `Фракции`, `Хранители`, `Посмертие`, `Бой`, `Архив`, and `Настройки`.

Default UI does not show raw slash command IDs. Advanced/debug commands remain grouped under `Расширенный режим`. Mutating actions show guided forms, realm availability, disabled reasons, and mutation warnings; form opening/submission uses the existing C# browser command and prompt-session flow, so safe execution still belongs to the C# local-write/lifecycle services.

## Browser audio and settings (#684)

Issue #684 adds browser-tab music and cue controls to a persistent browser audio panel that stays mounted while the player moves between routes, with the `Настройки` route pointing to the same controls. React consumes `browserApi.getAudioSettings()` and `browserApi.updateAudioSettings()` from `src/api/client.ts`, but the local C# host remains authoritative for the shared `GameSettings` audio fields (`MusicEnabled`, `MusicVolume`, `SoundEnabled`, and `SoundVolume`). Browser slider/toggle changes persist to the same settings file the console client uses, and the host applies the updated values back to the existing C# audio service.

The player must click `Включить музыку в браузере` before music playback starts. This is intentional browser-autoplay handling: the React shell may load metadata on startup, but it does not call `play()` for music until a user gesture chooses the main-menu or in-game playlist. Cue previews use their own explicit preview click. Missing local audio files render as ordinary unavailable metadata and player-facing notices rather than crashes.

Audio assets are served only through opaque `/api/audio/assets/{assetId}` URLs returned by the C# catalog. The DTO exposes no local filesystem paths, and invalid/path-traversal asset IDs return safe failures.

## Typed Browser API Contract (#703)

Issue #703 adds the typed API contract layer under:

```text
src/api/contracts.ts
src/api/client.ts
src/api/contract-fixture-checks.ts
src/api/contract-fixtures/
```

`src/api/contracts.ts` defines the browser-facing DTOs, request shapes, `BrowserApiResult<T>`, and normalized error kinds. `src/api/client.ts` exposes `BrowserApiClient`; React components should call this client instead of using raw `fetch()` from screens. The current contract is hand-written and protected by fixture guards rather than generated OpenAPI.

Contract update workflow:

1. Update the C# DTO/endpoint first; C# remains the authority.
2. Update `src/api/contracts.ts` and any affected `BrowserApiClient` method in `src/api/client.ts`.
3. Update the matching JSON under `src/api/contract-fixtures/`.
4. Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`.
5. Run `npm run typecheck --prefix BookOfEternityClient.WebFrontend` and `npm run build --prefix BookOfEternityClient.WebFrontend`.

`BrowserApiContractTests` serializes representative C# DTOs and compares them to the tracked `contract-fixtures`; `contract-fixture-checks.ts` imports the same fixtures so TypeScript verifies their shape. Default player UI should show the normalized `playerMessage` from failed requests, while `technicalDetails` belongs behind explicit advanced diagnostics.

Later issues deepen the route content (menus, game screens, settings, media/QTE UI) and add the broader smoke verification pipeline.

## Verification pipeline (#705)

Use the same frontend-first sequence locally and in CI when changing browser frontend, local host, typed API contracts, or smoke coverage:

```powershell
npm ci --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiBuiltFrontend|Category=BrowserWebUiSmoke|Category=BrowserWebUiParity" --logger "console;verbosity=minimal"
```

`Category=BrowserWebUiBuiltFrontend` starts the C# `LocalWebUiHost` against the built Vite `dist/` output and verifies the root shell, SPA route fallback, player-state APIs, and non-masked `/api/*` plus `/assets/*` misses. The smoke writes HTML/network/navigation diagnostics to `TestResults/browser-smoke/` (`root.html`, `game-route.html`, `main-menu.json`, `session.json`, `game-screen.json`, `network.json`, `navigation-ia.html`). CI uploads those diagnostics as `browser-smoke-artifacts` when present. `navigation-ia.html` is the dependency-light desktop/mobile visual smoke artifact for #727; full screenshots require a future tracked browser automation dependency, so this pipeline intentionally stays local/offline-friendly and dependency-light.

## Boundaries for future agents

- Do not move gameplay mechanics, command execution, validation, persistence, or GM/afterlife contract rules into React.
- Do not copy old-project prompts or mortal-life-only mechanics into this workspace.
- Keep default UI player-facing. Advanced diagnostics and raw slash commands require explicit opt-in.
- Treat API DTO changes as a C# contract concern; update `src/api/contracts.ts`, `src/api/client.ts`, `contract-fixtures`, and `BrowserApiContractTests` evidence together.
