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

Issue #701 only creates the frontend workspace. Until issue #702 is implemented, `dotnet run --project BookOfEternityClient -- --web` still serves the existing inline MVP shell from `LocalWebUiHost`.

Issue #702 is responsible for making the C# local host serve the built `dist/` assets and preserve existing Minimal API endpoints. Later issues add typed API contracts, app routing, game screens, settings, media/QTE UI, and smoke verification.

## Boundaries for future agents

- Do not move gameplay mechanics, command execution, validation, persistence, or GM/afterlife contract rules into React.
- Do not copy old-project prompts or mortal-life-only mechanics into this workspace.
- Keep default UI player-facing. Advanced diagnostics and raw slash commands require explicit opt-in.
- Treat API DTO changes as a C# contract concern; TypeScript should consume typed contracts once issue #703 lands.
