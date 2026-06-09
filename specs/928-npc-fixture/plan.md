# Implementation Plan: NPC fixture fallback for /npc

**Source issue**: #928 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/928

**Branch**: `work/928-npc-fixture`

**Spec**: `specs/928-npc-fixture/spec.md`

## Summary

Implement a read-only `/npc` fallback that surfaces `npc_journals.json` data when strict `npc_core.json` is absent from `FileSystemExample/game_session`. Keep mutating NPC talk/trade contracts unchanged and do not restore stale `pending_turn_snapshot` files.

## Technical Context

- Main code: C#/.NET 8 in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Browser command pipeline uses `ExplorerWebCommandService` and `ExplorerMortalWorldCommandResultBuilder`.
- Console command/menu NPC path currently reads `game_state/npcs/npc_core.json` in `ExplorerMode.Npcs.ListAndDetails.cs`.
- Fixture data lives under tracked `FileSystemExample/game_session/`; the live `BookOfEternityClient/game_session` path is ignored and must not be committed.

## Architecture Decision

Use the issue's second proposed option: display existing NPC journals when strict `npc_core.json` is absent. The fallback is read-only and player-facing. It must not relax validation for strict NPC update contracts and must not enable `/npc_talk` or `/npc_trade` without `npc_core.json` authority.

Preferred shape:

1. Add a small helper/projection near the existing NPC command result code, or a focused service if both console and browser need to share it.
2. `ExplorerMortalWorldCommandResultBuilder` uses the helper to build a table/list from `npc_journals.json` if `npc_core.json` has no visible NPCs.
3. The console-visible NPC path either uses the same helper/result renderer or adds a console fallback panel that renders the same name/latest-journal summary.
4. Tests capture the journal-only case first (RED), then implementation makes it pass (GREEN).

## Files likely to change

- `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` — browser/read-only command DTO fallback for `/npc`.
- `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs` and/or a shared NPC display helper — console NPC fallback evidence.
- `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` or a new focused test file — browser command result fallback coverage.
- `BookOfEternityClient.Tests/GameInterfaceTests.cs`, `ConsoleE2E*`, or a new focused console-friendly test/source guard — console fallback coverage if a direct console test is practical.
- `FileSystemExample/game_session/game_state/npcs/npc_journals.json` — only if the existing data is insufficient for the required fallback assertions.
- `specs/928-npc-fixture/*` — update task checkboxes/evidence after implementation.

## Verification Commands

Baseline already observed on this branch before implementation:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ConsoleE2ESandboxTests|FullyQualifiedName~AgentConsoleLiveSmokeTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~ValidatorFixtureTests" --logger "console;verbosity=minimal"
# Result: 238 passed, 0 failed, 0 skipped
```

Required after implementation:

```bash
# focused RED/GREEN tests added for #928
 dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "<new #928 test filter or affected class names>" --logger "console;verbosity=minimal"

# affected command/fixture slice
 dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ConsoleE2ESandboxTests|FullyQualifiedName~AgentConsoleLiveSmokeTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~ValidatorFixtureTests" --logger "console;verbosity=minimal"

# builds
 dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
 dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

# Spec Kit and hygiene
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
 git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files are changed.

## Risks and Non-goals

- Do not add `npc_core.json` fixture data if doing so requires stale signed pending-turn state or accepted-turn authority.
- Do not modify afterlife contracts or GM prompts; this slice is client-owned read-only fixture display.
- Do not treat journal fallback as canonical NPC authority for mutating operations.
