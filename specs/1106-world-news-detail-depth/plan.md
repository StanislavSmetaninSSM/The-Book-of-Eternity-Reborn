# Implementation Plan: World News Detail Depth

**Branch**: `fix/1106-world-news-detail-depth` | **Date**: 2026-06-18 | **Spec**: `specs/1106-world-news-detail-depth/spec.md`

**Input**: Feature specification from `specs/1106-world-news-detail-depth/spec.md`

## Summary

Expand world-news detail panels so selected records expose meaningful GM-authored fields beyond the small core whitelist, while keeping raw JSON hidden. Update the console world-news interaction so players can return from a selected detail to the selector.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: Existing `ExplorerCommandResult`, `UiKeyValueGridBlock`, and Spectre.Console `SelectionPrompt`

**Storage**: Existing file-backed JSON state under `game_state/world`

**Testing**: xUnit in `BookOfEternityClient.Tests`

**Target Platform**: Local console client and local browser command service

**Source Issue(s)**: [#1106](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1106)

**Contract Scope**: Client-owned player-facing rendering and console navigation. No GM prompt/schema change.

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. Source issue is #1106.
- **Spec Kit fit**: PASS. Player-facing UX/navigation change.
- **Player-facing integrity**: PASS. Rich details improve readable output without raw JSON dumps.
- **Contract/state authority**: PASS. No state schema changes.
- **Test-first path**: PASS. Add failing detail-depth and back-navigation tests before production changes.
- **Verification evidence**: PASS. Focused C# tests and build are listed.

## Project Structure

### Documentation

```text
specs/1106-world-news-detail-depth/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code

```text
BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs
BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs
BookOfEternityClient.Tests/ExplorerModeCommandTests.RivalAndWorld.cs
AGENTS.md
.specify/feature.json
```

## Research

- **Decision**: Add additional fields to the existing detail panel grids.
  **Rationale**: This keeps details readable and works for console/browser through the shared command result.
  **Alternatives considered**: Raw JSON panel. Rejected because it recreates the original player-facing problem.

- **Decision**: Use a console selector loop for overview/detail/back.
  **Rationale**: The shared command result already has actions, but console browsing needs a real menu path instead of a printed command table.
  **Alternatives considered**: Print the back action command. Rejected because the user specifically wants selector-style browsing.

## Data Model

- **Core detail field**: A known field intentionally rendered near the top with a localized label.
- **Additional detail field**: A content-bearing field not already shown by core rows and not technical-only.
- **Back navigation choice**: A console-only menu item that returns from a selected detail to the action list.

## Quickstart

1. Seed rich mortal world-news data.
2. Run `/новости_мира`.
3. Select a world event.
4. Confirm the detail includes core rows plus extra GM-authored fields.
5. Choose "Назад к списку".
6. Confirm the news selector appears again.

## Complexity Tracking

No constitution violations.
