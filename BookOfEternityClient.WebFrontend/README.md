# Book of Eternity Reborn Browser Frontend

This workspace is the Vite + React + TypeScript foundation for the long-term Browser Client.

The C# runtime remains the authority for game rules, persistence, command handling, validation, afterlife/mortal contracts, and local-write safety. TypeScript owns presentation, request state, UI composition, and interaction plumbing only.

## Commands

From the repository root:

```powershell
npm install --prefix BookOfEternityClient.WebFrontend
npm run dev --prefix BookOfEternityClient.WebFrontend
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
npm run preview --prefix BookOfEternityClient.WebFrontend
```

Or from this directory:

```powershell
npm install
npm run dev
npm run typecheck
npm run build
npm run preview
```

`npm run dev` and `npm run preview` bind to `127.0.0.1` for local development. `npm run build` writes production assets to `dist/`.

## Relationship to `dotnet run -- --web`

Issue #702 connects this workspace to the C# local web host. `dotnet run --project BookOfEternityClient -- --web` keeps serving the existing loopback-only Minimal API endpoints from C#, then serves frontend assets in this order:

1. `BookOfEternityClient.WebFrontend/dist/local-web-ui-shell.html` after `npm run build` copies the tracked player-facing fallback shell into the build output.
2. `BookOfEternityClient.WebFrontend/dist/index.html` for the React/Vite shell when a later migration removes the fallback-shell preference.
3. `BookOfEternityClient` output `wwwroot/browser/` for packaged/published builds that copied the Vite output or fallback shell.
4. The source fallback shell `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html` when no build output is present.

The fallback shell is the extracted player-facing MVP shell. It keeps `--web` usable while later issues migrate screens into React components. Generated `dist/`, `node_modules/`, `.vite/`, and `*.tsbuildinfo` stay ignored and should not be committed.

Later issues add typed API contracts, app routing, game screens, settings, media/QTE UI, and smoke verification.

## Boundaries for future agents

- Do not move gameplay mechanics, command execution, validation, persistence, or GM/afterlife contract rules into React.
- Do not copy old-project prompts or mortal-life-only mechanics into this workspace.
- Keep default UI player-facing. Advanced diagnostics and raw slash commands require explicit opt-in.
- Treat API DTO changes as a C# contract concern; TypeScript should consume typed contracts once issue #703 lands.
