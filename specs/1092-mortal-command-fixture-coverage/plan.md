# Implementation Plan: Mortal Command Fixture Coverage

**Branch**: `codex/1092-mortal-command-test-data` | **Date**: 2026-06-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/1092-mortal-command-fixture-coverage/spec.md`

## Summary

Inventory every Mortal World command from the command catalog, map each command to the state files its result builders read, repair the user's local ignored `game_session` fixture so the command surfaces are reviewable, and document repeatable verification steps.

## Technical Context

**Language/Version**: C#/.NET 8, PowerShell for local verification

**Primary Dependencies**: Existing command protocol, `ExplorerMortalWorldCommandResultBuilder`, `ExplorerLifecycleLocalTurnCommandResultBuilder`, `ExplorerWebCommandService`, `ValidationService`

**Storage**: File-backed JSON state under `BookOfEternityClient/game_session`

**Testing**: xUnit through `dotnet test`, manual console/browser command smoke checks

**Target Platform**: Local Windows desktop console client and local browser host

**Project Type**: Local game client with console and browser player-facing clients

**Performance Goals**: Command smoke checks should complete quickly enough for manual fixture validation; no runtime performance change is expected.

**Constraints**: The live fixture folder is ignored by git, so durable repo output must be matrix/spec/test coverage rather than committed game-session JSON unless the project later changes ignore policy.

**Scale/Scope**: 34 cataloged `ExplorerCommandGroup.MortalWorld` commands plus practical universal Mortal World preview commands.

**Source Issue(s)**: #1092 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1092

**Contract Scope**: player-facing, runtime-state fixture, validation, console, browser, docs

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Mortal|BrowserMortalWorld|Inventory|Trade|Storage|ExplorerWebCommandService"`
- Manual command smoke list in `quickstart.md`

## Constitution Check

- **GitHub traceability**: Pass. Source issue #1092 is linked in spec, plan, and tasks.
- **Spec Kit fit**: Pass. This is cross-command fixture coverage with console/browser preview implications.
- **Player-facing integrity**: Pass. Fixture data must produce Russian player-facing command output, not raw-only debug views.
- **Contract/state authority**: Pass. No contract changes are planned; gaps that require command/validator behavior changes become follow-up issues.
- **Test-first path**: Pass. Coverage matrix and smoke-check expectations are defined before fixture changes.
- **Verification evidence**: Pass. Focused dotnet tests and manual smoke commands are listed.
- **Agent orchestration**: Pass. This plan is the handoff context for issue #1092.

## Project Structure

### Documentation (this feature)

```text
specs/1092-mortal-command-fixture-coverage/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── mortal-command-fixture-matrix.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs
BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs
BookOfEternityClient/WebUi/ExplorerWebCommandService.cs
BookOfEternityClient/game_session/        # ignored local fixture
BookOfEternityClient.Tests/               # focused tests if a durable helper is added
```

**Structure Decision**: Keep fixture changes in the user's local ignored `game_session` and keep durable coverage documentation in the Spec Kit matrix.

## Complexity Tracking

No constitution violations are expected.
