# Implementation Plan: Mortal Command Fixture Coverage

**Branch**: `codex/1092-mortal-command-test-data` | **Date**: 2026-06-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/1092-mortal-command-fixture-coverage/spec.md`

## Summary

Inventory every Mortal World command from the command catalog, map each command to the state files its result builders read, repair the user's local ignored `game_session` fixture so the command surfaces are reviewable, document repeatable verification steps, package the rich Mortal World fixture as a tracked reusable save/load-compatible artifact for #1095, extend the reusable-save pattern to a dedicated Chaos Sea afterlife command-display save for #1096, and add the dedicated Shining Abode afterlife command-display save for #1097.

## Technical Context

**Language/Version**: C#/.NET 8, PowerShell for local verification

**Primary Dependencies**: Existing command protocol, `ExplorerMortalWorldCommandResultBuilder`, `ExplorerLifecycleLocalTurnCommandResultBuilder`, `ExplorerWebCommandService`, `ValidationService`

**Storage**: File-backed JSON state under `BookOfEternityClient/game_session`; for #1095, #1096, and #1097, tracked reusable save/package paths compatible with existing save/load code and tests

**Testing**: xUnit through `dotnet test`, manual console/browser command smoke checks

**Target Platform**: Local Windows desktop console client and local browser host

**Project Type**: Local game client with console and browser player-facing clients

**Performance Goals**: Command smoke checks should complete quickly enough for manual fixture validation; no runtime performance change is expected.

**Constraints**: The original live fixture folder is ignored by git, so durable repo output for #1092 was matrix/spec/test coverage; #1095, #1096, and #1097 add tracked reusable save/packages without committing a live mutable session root. #1097 must stay focused on Shining Abode and avoid becoming a second Chaos Sea fixture.

**Scale/Scope**: 34 cataloged `ExplorerCommandGroup.MortalWorld` commands plus practical universal Mortal World preview commands; for #1096, every command available in the loaded Chaos Sea afterlife save plus relevant universal afterlife/status commands; for #1097, every command available in the loaded Shining Abode afterlife save plus relevant universal afterlife/status commands.

- **Source Issue(s)**: #1092 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1092; #1095 reusable Mortal World save continuation - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1095; #1096 reusable Chaos Sea save continuation - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1096; #1097 reusable Shining Abode save continuation - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1097

**Contract Scope**: player-facing, runtime-state fixture/save, validation, console, browser, docs

**#1096 Implementation Note**: The Chaos Sea save is an at-rest manual save. It uses validated Chaos Sea/afterlife state plus `guardian_project_journal.json` for project display, while leaving canonical `guardian_projects.json` free of active tracker state because manual save/load strips the validated pre-turn tracker baseline required for `activeProjects`. No GM prompt, runtime afterlife contract, or dedicated Shining Abode #1097 fixture scope changes are planned.

**#1097 Implementation Note**: The Shining Abode save is an at-rest manual save using `FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture.zip` and `FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture_metadata.json`. It avoids live `input/` and `pending_turn_snapshot` artifacts, documents clear unavailable states where needed, and limits the validation adjustment to idle manual saves with no live turn or guardian mutation authority.

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalCommandDisplaySaveTests" --logger "console;verbosity=minimal"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ChaosSeaCommandDisplaySaveTests" --logger "console;verbosity=minimal"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ShiningAbodeCommandDisplaySaveTests" --logger "console;verbosity=minimal"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~FileSystemExampleAfterlifeStateExamplesTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~Validation" --logger "console;verbosity=minimal"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Mortal|BrowserMortalWorld|Inventory|Trade|Storage|ExplorerWebCommandService"`
- Manual command smoke list in `quickstart.md`

## Constitution Check

- **GitHub traceability**: Pass. Source issues #1092, #1095, #1096, and #1097 are linked in spec, plan, tasks, and the relevant reusable save metadata/checklist artifacts.
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
│   ├── mortal-command-fixture-matrix.md
│   ├── chaos-sea-command-fixture-checklist.md
│   └── shining-abode-command-fixture-checklist.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs
BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs
BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs
BookOfEternityClient/WebUi/ExplorerWebCommandService.cs
BookOfEternityClient/game_session/        # ignored local fixture
FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture.zip
FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture.zip
FileSystemExample/game_session/saves/manual_saves/chaos_sea_command_display_fixture_metadata.json
FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture.zip
FileSystemExample/game_session/saves/manual_saves/shining_abode_command_display_fixture_metadata.json
BookOfEternityClient.Tests/               # focused tests if a durable helper is added
```

**Structure Decision**: Keep #1092 fixture coverage/matrix in this Spec Kit directory; for #1095, #1096, and #1097, use the existing save/load-compatible tracked manual-save location and package dedicated command-display fixtures there rather than relying on ignored `BookOfEternityClient/game_session` as the only source.

## Complexity Tracking

No constitution violations are expected.
