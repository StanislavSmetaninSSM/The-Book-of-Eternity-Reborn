# Feature Specification: Browser Map Atlas Drilldown

**Feature Branch**: `work/1212-browser-map-atlas`  
**Created**: 2026-06-21  
**Status**: Draft  
**Input**: GitHub issue #1212 - "Browser: improve map interactions, location details, and magical atlas styling"

## Source Issues & Scope

- **Source GitHub issue(s)**: #1212 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1212
- **In scope**: Browser rendering of map command output, selected-location details, placeholder exits, and generated location image previews.
- **Out of scope**: Changing GM-authored world-map contracts, changing canonical map state, or redesigning the console map.

## User Scenarios & Testing

### User Story 1 - Read a useful atlas instead of a raw graph

A player opens `/карта` in the browser and sees a visual atlas with a useful selected-location panel, not a passive node list or raw JSON.

**Acceptance Scenarios**:

1. Given map data exists, when the player runs `/карта`, then the browser shows a visual map with location nodes, links, a legend, decorative map framing, and a selected-location detail panel.
2. Given a map has multiple z-levels or layers, when the player changes controls, then visible nodes update without losing the atlas presentation.

### User Story 2 - Distinguish known locations from unresolved exits

A player can tell which map points are real known locations and which points are exits that have not become full locations yet.

**Acceptance Scenarios**:

1. Given a map node is a known location, when it is shown, then it appears as a normal location and can be selected for details.
2. Given a map node is only an outgoing exit placeholder, when it is shown, then it is visually distinct and its detail panel explains that the full location is not open yet.

### User Story 3 - Inspect generated location images

A player can see generated location art from the selected location without leaving the map context.

**Acceptance Scenarios**:

1. Given a selected known location has an image, when the detail panel renders, then a thumbnail appears in the panel.
2. Given the player clicks the thumbnail, when the image opens, then an enlarged view is shown and can be closed.

## Functional Requirements

- **FR-001**: Browser map blocks MUST render as a visual atlas with nodes, links, legend, compass/rune decoration, and selected-location details.
- **FR-002**: Existing known locations MUST be visually distinct from unresolved exit placeholders.
- **FR-003**: Selecting a map node MUST update the detail panel with player-facing location data.
- **FR-004**: Placeholder exit details MUST clearly explain that the full location is not open yet.
- **FR-005**: If the selected known location has a local generated image URL, the browser map MUST show a thumbnail and allow an enlarged view.
- **FR-006**: Map media URL handling MUST preserve trusted local media URLs while still rejecting unsafe/non-image URL shapes.
- **FR-007**: Default player output MUST not expose raw API/file-path wording except in explicit advanced diagnostics.
- **FR-008**: No GM-facing contract update is required unless map state fields or GM-authored output contracts change.

## Key Entities

- **Map block**: Command-result block containing a browser-renderable map view.
- **Map node**: Existing location or unresolved exit placeholder.
- **Location media**: Trusted generated image URL for a known location.
- **Selected-location panel**: Browser detail panel for the currently selected map node.

## Success Criteria

- **SC-001**: Frontend regression tests prove local `/api/media/...` map image URLs render as thumbnails.
- **SC-002**: C# map service/host tests continue to prove placeholder exits, generated image attachment, and atlas assets.
- **SC-003**: Browser Act evidence covers `/карта`, placeholder selection, visible thumbnail, and enlarged image dialog.
- **SC-004**: Frontend build and typecheck pass after the map media fix.

## Verification Plan

- `npm run typecheck --prefix BookOfEternityClient.WebFrontend`
- `npx vitest run test/playerCopyRobustness.test.ts test/blockRenderer.render.test.tsx`
- `npm run build --prefix BookOfEternityClient.WebFrontend`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "LocalMapViewerServiceTests|LocalWebUiHostTests" --logger "console;verbosity=minimal"`
- Browser Act against local web host: run `/карта`, select placeholder, return to current location, open image thumbnail.

## Assumptions

- The existing C# map DTO remains authoritative.
- Browser frontend may use a small allowlist for renderable map media URLs.
- Advanced diagnostics may still show raw JSON when the player explicitly enables advanced mode.
