# Browser Reborn Panels Design (#729)

## Context

Issue #729 is the remaining child of the Browser Client IA umbrella #726. Issues #727 and #728 already established the player navigation taxonomy and reusable `DetailSurfaceCard` pattern. The old React reference was inspected as a UI/UX baseline only: it reinforces a full game-client structure with side panels, character/world/inventory sections, and modal/detail panels, but its mortal-life mechanics and prompts are not product truth for Reborn.

## Goal

Add explicit player-facing Browser Client sections for Reborn-specific systems—Afterlife, Shining Abode, and Chaos Sea—without moving gameplay logic to React and without exposing GM contracts, raw pending/control files, endpoints, or debug diagnostics in the default UI.

## Approach Selected

Use a UI-only React slice inside the existing `Мир` route rather than adding more top-level route cards. The new `RebornSystemsPanel` will render three `DetailSurfaceCard` cards:

1. **Afterlife overview** — current realm, Ink Feathers, enlightenment, guardian, and a graceful locked/empty state when the save is mortal-only.
2. **Shining Abode** — radiance, light sparks, halls/factions, gate draft status, and available player-safe actions from the existing action menu.
3. **Chaos Sea** — current Chaos Sea availability, guardian/abode-oriented action summaries, and a locked state when the current realm is not Chaos Sea.

This keeps mortal-world and afterlife panels conceptually separated while sharing the same card → modal/full-panel visual system from #728.

## Alternatives Considered

- **Add top-level Afterlife/Shining/Chaos route cards.** Rejected for this slice because #729 explicitly warns against mixing afterlife systems into the route grid as random cards. A later IA issue can promote a dedicated top-level route if the whole route taxonomy is redesigned.
- **Change `/api/game-screen` to expose richer afterlife DTOs.** Rejected for this closure unit. Existing `BrowserGameScreenDto.afterlife`, `flags`, `soul`, and C# command action metadata are enough for player-safe overview panels. Avoiding DTO changes also avoids runtime contract/GM-doc changes.
- **Render raw command results inline by default.** Rejected because raw command/API surfaces belong behind explicit advanced mode or action-card execution.

## Architecture

- `BookOfEternityClient.WebFrontend/src/App.tsx`
  - Add `RebornSystemsPanel`, helper functions for afterlife action filtering, status copy, and locked-state copy.
  - Render the panel from `WorldRoute` above the general `ActionMenu` so Reborn systems are visible as a distinct conceptual section.
  - Reuse `DetailSurfaceCard` for compact cards and modal/full-panel details.
- `BookOfEternityClient.WebFrontend/src/styles/components.css`
  - Add light presentation styles for a Reborn panel header and panel grid; reuse existing `detail-surface-grid`/`summary-card` language.
- `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
  - Add source guards proving the panel exists, uses the shared detail-surface pattern, separates Afterlife/Shining/Chaos from mortal panels, documents UI-only mapping, and keeps raw contract/debug terms out of the player-default source slice.
- `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`
  - Extend the dependency-light visual smoke artifact generation with `reborn-panels.html` covering locked and active Reborn states.
- `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md`
  - Document issue #729 as UI-only mapping over existing C# DTO/action metadata and explain why GM-facing afterlife contract docs do not change.

## Data Flow

`/api/game-screen` remains read-only and C# authoritative. React consumes:

- `game.flags` for realm/availability state;
- `game.afterlife` for safe summary counts and resources;
- `game.soul` for realm, guardian, ink feathers, and enlightenment;
- `game.actionMenu.sections` for player-safe action labels/descriptions/availability.

No new endpoint, pending/control file, action type, validation rule, scheduler behavior, normalizer effect, or GM-authored contract is introduced.

## Error, Empty, and Locked States

- No active game session keeps using the existing `WorldRoute` empty/failure path.
- Mortal-world saves show the Reborn panel as a locked but calm section: “посмертные панели откроются, когда душа перейдёт в посмертие.”
- Shining/Chaos cards display availability from existing realm flags and action metadata; unavailable actions stay as player-facing disabled/locked copy.
- Advanced/debug details are not shown in this panel; raw filenames and endpoint language remain in the advanced diagnostics or GM-facing documentation.

## Testing and Verification

TDD source-guard tests will be added first and watched fail. Implementation must then satisfy:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserRebornPanels|FullyQualifiedName~BuiltFrontendSmoke" --logger "console;verbosity=minimal"`
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests|Browser" --logger "console;verbosity=minimal"`
- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `git diff --check`

## Documentation and Contract Impact

This closure unit is UI-only. It does not change Afterlife runtime contracts, pending/control files, validation rules, canonical state surfaces, action types, normalizer side effects, or GM-authored behavior. Therefore `OtherGuides/Afterlife_Contract_Matrix.md`, examples/manifests, and afterlife documentation coverage tests do not require content updates beyond running the suggested afterlife documentation tests as evidence.

## Self-Review

- No placeholders remain.
- Scope is one implementation slice: player-facing Reborn panels using existing data.
- Acceptance criteria are covered by source guards, docs, and a visual smoke artifact.
- The design avoids adding gameplay logic to React and avoids runtime contract changes.
