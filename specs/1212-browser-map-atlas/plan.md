# Implementation Plan: Browser Map Atlas Drilldown

**Branch**: `work/1212-browser-map-atlas` | **Date**: 2026-06-21 | **Spec**: `specs/1212-browser-map-atlas/spec.md`

## Summary

Close #1212 by verifying the browser map atlas behavior and fixing the remaining media-preview regression. The atlas, selected-node details, placeholder styling, and decorative map UI already exist in the current browser map renderer; live verification found that trusted `/api/media/...` image URLs were being stripped by the player-copy sanitizer, so location thumbnails did not render. The implementation keeps the C# map DTO authoritative and limits the frontend change to a trusted map media URL resolver plus regression coverage.

## Technical Context

**Language/Version**: React/TypeScript browser frontend; C#/.NET 8 local web host and map DTO tests.  
**Primary Dependencies**: `MapBlock`, command-result block rendering, `LocalMapViewService`, local media endpoint.  
**Testing**: Vitest/render tests for frontend, xUnit for C# map service/host, Browser Act for local UI evidence.  
**Source Issue(s)**: #1212 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1212

## Verification Commands

- `npm run typecheck --prefix BookOfEternityClient.WebFrontend`
- `npx vitest run test/playerCopyRobustness.test.ts test/blockRenderer.render.test.tsx`
- `npm run build --prefix BookOfEternityClient.WebFrontend`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "LocalMapViewerServiceTests|LocalWebUiHostTests" --logger "console;verbosity=minimal"`
- Browser Act local smoke on `http://127.0.0.1:8791` using an isolated copied game session.
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. #1212 is linked in spec, plan, and tasks.
- **Task tracking**: PASS. Implementation is tied to #1212.
- **Player-facing copy**: PASS. The fix preserves player-facing atlas output and keeps raw JSON behind advanced mode.
- **TDD**: PASS. A failing frontend render test captured the missing thumbnail before the fix.
- **GM contract docs**: PASS. No map state contract or GM-authored output field changed.

## Likely Source Areas

```text
BookOfEternityClient.WebFrontend/
├── src/components/MapBlock.tsx
└── test/blockRenderer.render.test.tsx

BookOfEternityClient.Tests/
└── LocalMapViewerServiceTests.cs
```

## Risk Notes

- Do not run player-copy text sanitization on URLs; use a URL allowlist instead.
- Keep the URL allowlist narrow enough to avoid rendering arbitrary unsafe URL schemes.
- Browser Act can require advanced mode to launch commands from the help catalog when the copied test session has validation warnings.
