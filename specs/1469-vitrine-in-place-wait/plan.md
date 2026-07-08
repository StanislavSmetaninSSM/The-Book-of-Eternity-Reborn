# Implementation Plan: In-Place Vitrine Preparation Wait

**Source Issue**: #1469 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1469
**Spec**: `specs/1469-vitrine-in-place-wait/spec.md`

## Summary

Replace the current missing-vitrine behavior where `/обучение` and trade commands return a GM action to the normal game loop with an in-place preparation flow. The command still creates the pending request and the engine still uses the ordinary validated GM turn lifecycle, but the engine treats these actions as service refreshes, publishes waiting copy for the originating command, and re-renders that command once after completion.

## Technical Context

- C#/.NET 8 console client.
- `ExplorerMode.TryProcessCommand` returns non-empty strings for local commands that need GM processing.
- `GameEngine.ProcessPlayerTurn` owns GM request staging, validation, repair, rollback, and cleanup.
- Existing services already create pending request files for training, NPC trade, Guardian trade, and Shining trade.

## Approach

1. Add a small command result metadata surface to `ExplorerMode` for in-place GM refresh requests: original command, title, waiting message, and single-refresh policy.
2. Keep `ProcessPlayerTurn` as the single authority for sending GM turns and validation repair.
3. Teach the game loop to recognize in-place vitrine refresh metadata, show the vitrine waiting copy while GM works, then invoke the original command once after the turn lifecycle returns.
4. Update training and trade command copy so players are told to wait rather than reopen commands.
5. Keep ordinary local-turn actions unchanged.

## Constitution Check

- **Issue traceability**: PASS. Source issue #1469 is linked.
- **Spec Kit fit**: PASS. Player-facing UX and runtime flow change.
- **Player-facing integrity**: PASS. New copy is Russian and hides implementation details.
- **Contract/state authority**: PASS. GM-authored request schema remains unchanged; client harness flow changes.
- **Test-first**: PASS. Existing wrong-contract tests will be inverted before implementation.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ConsoleTraining|ConsoleNpcTrade|GameEngineTurnLifecycle"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|ValidationSourceGuardTests"
```

Manual:

- Run `/обучение` with missing Mortal teacher showcase in the console live test.
- Run NPC trade with missing inventory in the console live test.
- Confirm the player stays on the command surface, sees waiting copy, and gets the refreshed vitrine after GM completion.

