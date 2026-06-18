# Implementation Plan: World News Overview Summaries

**Branch**: `fix/1109-world-news-overview-summaries` | **Date**: 2026-06-18 | **Spec**: `specs/1109-world-news-overview-summaries/spec.md`

**Input**: Feature specification from `specs/1109-world-news-overview-summaries/spec.md`

## Summary

Tighten `/новости_мира` so the overview is a useful player-facing list of selectable entries and the detail remains full-depth. The implementation will extend the shared mortal world news command result builder, preserving console/browser parity through existing DTO blocks and actions.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: Spectre.Console for console rendering; shared command protocol DTOs for console/browser output

**Storage**: Existing file-backed JSON game state under `game_state/world/`

**Testing**: xUnit tests in `BookOfEternityClient.Tests`

**Target Platform**: Local console client and browser client command-service DTO consumers

**Project Type**: Local game client

**Performance Goals**: Overview should remain bounded to current news lists and avoid raw full-state dumps.

**Constraints**: No debug/API/raw/path/url leakage; dynamic text must be escaped by existing renderer paths.

**Scale/Scope**: One command surface: `/новости_мира` / `/world_news`.

**Source Issue(s)**: [#1109](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1109)

**Contract Scope**: player-facing, console, browser, shared command DTO

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true -p:UseSharedCompilation=false --filter "WorldNewsOverview|WorldNewsEventDetail|WorldNewsFlagDetail|WorldNewsProgressionDetail|WorldNews_ConsoleSelection" --logger "console;verbosity=minimal"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true -p:UseSharedCompilation=false --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore -p:UseSharedCompilation=false`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: Pass. Issue #1109 exists and is linked.
- **Spec Kit fit**: Pass. This is player-facing UX and shared console/browser command DTO behavior.
- **Player-facing integrity**: Pass. Output remains Russian/in-world and technical fields stay hidden.
- **Contract/state authority**: Pass. No new state authority or GM contract is introduced; existing canonical news records are rendered more usefully.
- **Test-first path**: Pass. Regression tests will be updated before production code.
- **Verification evidence**: Pass. Focused and broader C# verification commands are listed.
- **Agent orchestration**: Pass. Superpowers TDD/debugging/review/verification apply.

## Project Structure

### Documentation (this feature)

```text
specs/1109-world-news-overview-summaries/
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
```

**Structure Decision**: Keep behavior in the existing shared world news command result builder so console and browser receive the same summary/detail DTOs. Console-specific code should only preserve selection/back navigation.

## Complexity Tracking

No constitution violations.
