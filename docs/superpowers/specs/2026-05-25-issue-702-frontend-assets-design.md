# Issue #702 Frontend Asset Host Design

Tracked issue: #702 — [Browser Client Architecture] Serve framework frontend assets from the C# local web host.

## Context

`LocalWebUiHost` currently serves `/` from a very large C# raw string (`BuildShellHtml()`), while the framework frontend workspace created for #701 lives in `BookOfEternityClient.WebFrontend/`. The browser shell must stop growing inside C# source while preserving the loopback-only API host, existing browser endpoints, and a usable `--web` launch.

Stanislav authorized unattended work, so this design is self-approved after review and kept conservative: it extracts/serves assets without moving gameplay rules or changing browser API DTO semantics.

## Approach

Use a transitional asset host layer:

1. Add a small `LocalWebUiFrontendAssets` resolver responsible for finding the browser frontend root and index file.
2. Add `LocalWebUiHostOptions.FrontendAssetsPath` as an optional test/dev override.
3. Prefer a Vite production build at `BookOfEternityClient.WebFrontend/dist/` when it exists.
4. Allow a publish/output candidate such as `wwwroot/browser/` for future packaged builds.
5. Fall back to a tracked standalone shell asset at `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html` so `--web` remains usable even before a local `npm run build`.
6. Configure ASP.NET Core static-file serving for the selected asset root while keeping `/api/*` and existing C# API routes authoritative.
7. Map `/` and non-API/non-asset SPA fallback paths to the resolved frontend index; missing `/api/*` and `/assets/*` paths remain 404 instead of returning the app shell.

The legacy player-facing shell remains functionally unchanged in this slice, but it moves out of `LocalWebUiHost.cs`. Future issues can replace it with React components without changing the host contract.

## Components

- `BookOfEternityClient/WebUi/LocalWebUiFrontendAssets.cs`
  - Finds the frontend asset root from override, repo `dist`, output `wwwroot/browser`, or tracked fallback.
  - Opens `index.html`/fallback shell as a physical file result.
  - Exposes the chosen root for `UseStaticFiles`.
- `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
  - Removes `BuildShellHtml()`.
  - Calls the asset resolver and static-file middleware.
  - Keeps existing Minimal API endpoints unchanged.
- `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html`
  - Contains the extracted current browser shell so the player-facing UI still works without compiled React assets.
- `BookOfEternityClient/BookOfEternityClient.csproj`
  - Copies built `dist` files to `wwwroot/browser` when present during build/publish, without tracking generated `dist/` output.
- Tests/docs
  - Host tests assert external asset serving, fallback behavior, API preservation, and no giant inline shell in C#.
  - `docs/web-ui/local-web-host.md` documents the new build/serve contract.

## Data and control flow

`dotnet run --project BookOfEternityClient -- --web` still builds the same local API app. At startup, the host resolves frontend assets. Static middleware serves built CSS/JS/media from that root. `/` returns the frontend index/shell. `/api/*` routes remain Minimal API handlers in C#. Browser rendering code continues to call C# endpoints for all game state and write coordination.

## Error handling and safety

- Non-loopback URLs remain rejected before any asset serving is configured.
- Missing explicit override paths fail fast in tests/dev with a clear error.
- `/api/*` and `/assets/*` misses return 404, not the app shell, to avoid hiding broken API or asset references.
- The default player-facing shell remains separate from advanced diagnostics.
- No afterlife or mortal-world runtime contract changes are made.

## Testing

1. Add failing tests for explicit frontend asset override, root fallback, built asset serving, API preservation, and source guard that prevents reintroducing a large inline shell.
2. Implement the resolver and host wiring until focused LocalWebUi tests pass.
3. Run npm typecheck/build, focused .NET browser-host/workspace/docs tests, broader LocalWebUi tests, `git diff --check`, and a basic added-line security scan.

## Self-review

- Placeholder scan: no TBD/TODO placeholders.
- Consistency: the host serves external assets while keeping C# API/runtime authority.
- Scope: one closure unit for #702; typed API contracts and CI frontend checks remain #703/#705.
- Ambiguity resolved: generated `dist/` stays ignored/untracked; the tracked fallback shell preserves usability until a local/frontend build exists.
