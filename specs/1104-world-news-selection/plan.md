# Implementation Plan: World News Selectable Details

**Branch**: `fix/1104-world-news-selection` | **Date**: 2026-06-18 | **Spec**: `specs/1104-world-news-selection/spec.md`

**Input**: Feature specification from `specs/1104-world-news-selection/spec.md`

## Summary

Make `/новости_мира` summary-first: the overview should show compact section counts and selectable detail actions, while existing event/flag/progression detail commands remain the path for full readable records. Remove raw JSON blocks and full detail lists from the default overview.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: Spectre.Console through existing console renderer, shared `ExplorerCommandResult` blocks/actions

**Storage**: Existing file-backed JSON game state under `game_state/world`, `game_state/npcs`, and `game_state/factions`

**Testing**: xUnit in `BookOfEternityClient.Tests`

**Target Platform**: Local console client and local browser command service

**Project Type**: Desktop/local game client

**Performance Goals**: Overview should avoid rendering large raw JSON sections and remain readable with multiple records.

**Constraints**: Preserve existing detail commands, dynamic text escaping, and browser/console semantic parity.

**Scale/Scope**: One shared command-result builder and focused console/browser tests.

**Source Issue(s)**: [#1104](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1104)

**Contract Scope**: Player-facing console/browser command-result content. No GM prompt/schema change.

**Verification Commands**:
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "WorldNews|ExplorerWebCommandServiceTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`
- `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. Source issue is #1104.
- **Spec Kit fit**: PASS. Player-facing console UX redesign with shared browser command-service parity.
- **Player-facing integrity**: PASS. Overview removes raw/debug-like JSON and keeps Russian player-facing labels.
- **Contract/state authority**: PASS. No state schema changes. Existing detail commands remain canonical detail authority.
- **Test-first path**: PASS. Add failing console/browser world-news overview tests before implementation.
- **Verification evidence**: PASS. Focused C# tests, build, and diff hygiene are listed.
- **Agent orchestration**: PASS. Work is executed directly in Codex with Superpowers TDD/verification discipline.

## Project Structure

### Documentation (this feature)

```text
specs/1104-world-news-selection/
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
```

**Structure Decision**: Keep behavior in the existing shared world-news command-result builder so console and browser command-service surfaces stay aligned. Only touch `ExplorerMode.FactionsAndWorldNews.cs` if console needs to invoke the existing action selector after rendering actions.

## Research

- **Decision**: Use existing `UiAction` list as the selectable detail mechanism.
  **Rationale**: The overview already creates detail commands; the reported failure is that the overview body is still too verbose and raw.
  **Alternatives considered**: Add a new prompt session for world-news selection. Rejected because it would duplicate the existing command action protocol and risk browser/console drift.

- **Decision**: Remove default raw JSON blocks from overview, not from details.
  **Rationale**: Detail commands already avoid raw JSON in tests. Raw overview blocks are the main visual problem in screenshots.
  **Alternatives considered**: Hide raw JSON behind an advanced flag. Rejected for this issue because there is no explicit advanced-mode requirement and the player-facing overview should be clean by default.

- **Decision**: Keep threats, NPC activities, and faction projects as summary counts in this issue.
  **Rationale**: The requested selectable details focus on news/event/flag/progression records. Additional drilldowns can be a separate scoped task.
  **Alternatives considered**: Add detail commands for all six sections. Rejected as wider than the screenshot-driven bug fix.

## Data Model

- **WorldNewsOverview**: Counts/statuses for available sections and a collection of `UiAction` drilldowns.
- **WorldNewsDetailAction**: Existing action with `Id`, `Label`, `Command`, `Style`, `RequiresConfirmation`, and `Payload`.
- **WorldNewsDetailRecord**: Existing event/flag/progression snapshots rendered by detail panels.

## Quickstart

1. Seed rich world-news test data through existing test helpers.
2. Execute `/новости_мира`.
3. Confirm output contains `Новости мира` summary and drilldown actions.
4. Confirm output does not contain raw JSON keys (`worldEventsLog`, `worldStateFlags`, `updateWorldProgressionTracker`) or the full event/flag/progression tables.
5. Execute `/новости_мира событие <id>`, `/новости_мира флаг <id>`, and `/новости_мира прогресс <id>`.
6. Confirm each detail view contains readable data and no raw JSON/debug leakage.

## Complexity Tracking

No constitution violations.
