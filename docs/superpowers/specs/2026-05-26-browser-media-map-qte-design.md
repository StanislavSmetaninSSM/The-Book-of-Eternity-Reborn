# Browser Media, Map, and QTE Game Sections Design

Issue: [#688](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/688)

## Context

The Browser Client already has local services for media (`LocalMediaService`), map rendering (`LocalMapViewService` and `LocalMapViewerAssets`), and QTE interaction (`QteWebInteractionService`). The React shell currently exposes only a minimal Media route: a QTE status card and a placeholder gallery note. That leaves the available runtime surfaces feeling like technical endpoints instead of game sections.

The old React project was inspected as a UI/UX reference only. The useful pattern is a comfortable game-client section with cards, gallery tiles, and route-specific panels. Its old prompts and mortal-life mechanics are not product truth for Reborn.

## Goal

Make `/media` in the React Browser Client a player-facing game section for quick scenes, gallery images, and the local atlas. The C# client remains the gameplay/application authority; React only renders typed DTOs and calls the existing local web APIs.

## Architecture

`BrowserGameScreenService` will add a read-only `media` DTO to `/api/game-screen`. The DTO contains:

- a safe gallery list built from `LocalMediaService.EnumerateGallery()` with only `mediaId`, browser URL, file name, content type, size, and modification time;
- the current scene image prompt from `output/interface_updates.json` via the existing narrative DTO;
- a `MapViewDto` from `LocalMapViewService.BuildCurrentRealmMapAsync()` so mortal-world and afterlife maps use existing realm-aware logic.

The React app will render that DTO inside `MediaRoute` as three player-facing panels:

1. **Быстрые сцены** — offer/active/completed QTE state with accept/decline/action controls using the existing `browserApi.resolveQteOffer()` and `browserApi.resolveQteAction()` methods.
2. **Галерея** — scene prompt plus local image cards linked by safe `/api/media/{mediaId}` URLs, without showing absolute local paths.
3. **Атлас** — an inline map preview with z-level/layer/political controls driven by the `MapViewDto` already used by command protocol map blocks.

## Player/advanced boundary

The default Media route must not present `/api/qte/state`, `/api/media/{id}`, raw JSON, or command IDs as the main interaction model. Technical endpoint metadata remains in advanced diagnostics only. Player copy uses Russian game terms such as “быстрая сцена”, “галерея”, “атлас”, and “политическое влияние”.

## Error and empty states

- If no game session is loaded, the existing route-level no-session empty state remains.
- If the gallery is empty, the section says images will appear when the chapter creates saved scenes.
- If a media image cannot render, the `<img>` keeps player-safe alt text; the underlying URL remains a loopback API reference, not a local path.
- If the map has no nodes for the current layer/z-level, the atlas shows a player-facing empty message.
- QTE errors use `toPlayerFacingText()` so raw service messages are normalized before display.

## Testing

Add tests before implementation:

- C# contract test proving `BrowserGameScreenDto.media` exists, includes a safe gallery item, includes the scene image prompt, and includes a realm-aware map with layer/z-level controls.
- React source guard proving `MediaRoute` renders dedicated gallery, atlas, and quick-scene panels, calls QTE APIs through typed client methods, and does not hard-code raw QTE/media endpoint literals in the route.
- TypeScript fixture and contract fixture updates so C# and frontend stay aligned.

Verification commands:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|Browser|LocalMapViewer|LocalMedia|Qte" --logger "console;verbosity=minimal"
```

## Documentation impact

This is a browser presentation/DTO expansion only. It does not change Afterlife runtime contracts, pending files, validation rules, GM response fields, or mortal-world mechanics. GM-facing Afterlife contract docs do not need updates. Browser frontend docs should mention that the Media route now consumes the `media` part of `/api/game-screen` and that raw endpoint interaction remains advanced-only.

## Self-review

- No placeholders remain.
- Scope is one closure unit for #688: player-facing Media route plus read-only DTOs.
- The design keeps C# runtime authority and React presentation boundaries separate.
- Acceptance criteria map to implementation tasks: gallery, map, QTE, empty/error states, no local path leakage, and no raw endpoint-first UI.
