# Implementation Plan: World News Detail Footer Depth

**Branch**: `fix/1112-world-news-detail-footer-depth` | **Date**: 2026-06-19 | **Spec**: `specs/1112-world-news-detail-footer-depth/spec.md`

**Input**: Feature specification from `specs/1112-world-news-detail-footer-depth/spec.md`

## Summary

Fix selected `/новости_мира` detail quality and console navigation clarity. The shared detail DTO should carry useful player-facing fields, while console interactive mode should avoid rendering a stray footer before the back/close prompt. Local test `game_session` data should also contain rich Valmont event details for manual inspection.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: Spectre.Console; shared command protocol DTOs

**Storage**: Existing file-backed JSON game state; ignored local `BookOfEternityClient/game_session`

**Testing**: xUnit tests in `BookOfEternityClient.Tests`

**Target Platform**: Local console client and browser command DTO consumers

**Project Type**: Local game client

**Performance Goals**: Detail rendering remains bounded to selected record fields.

**Constraints**: No raw/debug/path/url leakage; no GM contract change.

**Scale/Scope**: `/новости_мира` event detail and console interactive detail navigation.

**Source Issue(s)**: [#1112](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1112)

**Contract Scope**: player-facing, console, browser, shared command DTO, local test data

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true -p:UseSharedCompilation=false --filter "WorldNewsEventDetail|WorldNews_ConsoleSelection|WorldNews_ConsoleExposesSharedEventFlagAndProgressionDrilldowns" --logger "console;verbosity=minimal"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true -p:UseSharedCompilation=false --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore -p:UseSharedCompilation=false`
- `git diff --check`

**Local verification note**: if a manually launched console client locks `BookOfEternityClient\bin\Debug\net8.0`, add `-p:UseAppHost=false -p:BaseOutputPath="E:\Games\The Book of Eternity Reborn\artifacts\test-bin\1112\"` to test/build commands.

## Constitution Check

- **GitHub traceability**: Pass. Issue #1112 exists and is linked.
- **Spec Kit fit**: Pass. Player-facing command UX and console/browser shared DTO behavior are affected.
- **Player-facing integrity**: Pass. Output remains Russian/in-world and technical fields stay hidden.
- **Contract/state authority**: Pass. No new GM-authored contract; existing player-facing fields are rendered/seeding test data is local.
- **Test-first path**: Pass. Regression tests precede production code.
- **Verification evidence**: Pass. Focused and broader C# commands are listed.
- **Agent orchestration**: Pass. Superpowers TDD/debugging/verification apply.

## Project Structure

### Documentation (this feature)

```text
specs/1112-world-news-detail-footer-depth/
├── spec.md
├── plan.md
├── tasks.md
└── checklists/
    └── requirements.md
```

### Source Code

```text
BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs
BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs
BookOfEternityClient.Tests/ExplorerModeCommandTests.RivalAndWorld.cs
BookOfEternityClient/game_session/game_state/world/world_events.json  # ignored local manual test data
```

**Structure Decision**: Keep shared event detail content in the command builder and handle only interactive footer suppression in console mode.

## Complexity Tracking

No constitution violations.
