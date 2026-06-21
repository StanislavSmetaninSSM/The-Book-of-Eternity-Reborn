# Quickstart: Browser Map Atlas Drilldown

## Prerequisites

- Restore .NET packages.
- Install frontend dependencies with `npm ci --prefix BookOfEternityClient.WebFrontend`.
- Build frontend assets with `npm run build --prefix BookOfEternityClient.WebFrontend`.

## Automated Verification

```powershell
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npx vitest run test/playerCopyRobustness.test.ts test/blockRenderer.render.test.tsx
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "LocalMapViewerServiceTests|LocalWebUiHostTests" --logger "console;verbosity=minimal"
```

## Browser Verification

1. Copy a known `game_session` to an isolated temp base path.
2. Add a generated image under `game_session/images/locations/<location-id>.png`.
3. Start the browser host:

```powershell
dotnet run --project BookOfEternityClient -- <temp-base-path> --web --web-url http://127.0.0.1:8791
```

4. Open `http://127.0.0.1:8791`, continue the chapter, enable advanced mode if the copied session blocks composer input, and run `/карта`.
5. Verify:
   - the atlas has nodes, links, legend, compass/runes;
   - a placeholder exit is visually distinct and its detail panel says it is not a full location yet;
   - the current/generated-location detail panel shows an image thumbnail;
   - clicking the thumbnail opens an enlarged image dialog.
