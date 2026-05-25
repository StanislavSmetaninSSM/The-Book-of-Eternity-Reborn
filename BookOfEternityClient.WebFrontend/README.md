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

## React app shell (#704)

The React app shell defines player-facing routes for `Главная`, `Игра`, `Душа`, `Мир`, `Медиа`, and `Настройки`. These are presentation routes only: they consume `src/api/client.ts`, render typed C# DTOs, and leave all game/application authority in the C# runtime.

The shell keeps default UI Russian-first and player-facing. Command IDs, `/api/*` endpoint details, lifecycle validation internals, and slash-command diagnostics stay behind explicit `Расширенный режим` opt-in. Player route failures should render `playerMessage`; `technicalDetails` belongs in the advanced diagnostics/details surface.

Future Browser Client tasks (#683-#689) should extend these route regions rather than recreating ad-hoc DOM manipulation:

- main menu/session flow under `Главная`;
- narrative, turn state, QTE, and prose action composer under `Игра`;
- character/soul/status cards under `Душа`;
- map, journal, quests, factions, and contextual actions under `Мир`;
- gallery, media, and QTE visuals under `Медиа`;
- local profile, language, audio, and comfort settings under `Настройки`.

Verify shell changes with `npm run typecheck --prefix BookOfEternityClient.WebFrontend`, `npm run build --prefix BookOfEternityClient.WebFrontend`, and focused browser .NET tests such as `BrowserFrontendWorkspaceTests` / `LocalWebUiHostTests`.

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

`Category=BrowserWebUiBuiltFrontend` starts the C# `LocalWebUiHost` against the built Vite `dist/` output and verifies the root shell, SPA route fallback, player-state APIs, and non-masked `/api/*` plus `/assets/*` misses. The smoke writes HTML/network diagnostics to `TestResults/browser-smoke/` (`root.html`, `game-route.html`, `main-menu.json`, `session.json`, `game-screen.json`, `network.json`). CI uploads those diagnostics as `browser-smoke-artifacts` when present. Screenshots require a future tracked browser automation dependency; this pipeline intentionally stays local/offline-friendly and dependency-light.

## Boundaries for future agents

- Do not move gameplay mechanics, command execution, validation, persistence, or GM/afterlife contract rules into React.
- Do not copy old-project prompts or mortal-life-only mechanics into this workspace.
- Keep default UI player-facing. Advanced diagnostics and raw slash commands require explicit opt-in.
- Treat API DTO changes as a C# contract concern; update `src/api/contracts.ts`, `src/api/client.ts`, `contract-fixtures`, and `BrowserApiContractTests` evidence together.
