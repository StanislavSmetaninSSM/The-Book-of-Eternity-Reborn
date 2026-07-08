# Implementation Plan: Mortal Bootstrap Placeholder Name Guard

**Source GitHub issue**: #1461 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1461

## Summary

Add a Mortal bootstrap validation guard that permits client-owned scaffold names before first GM materialization, but rejects the same names after the accepted first Mortal bootstrap turn. The validator reports exact player-visible fields and repair hints that ask the GM to replace placeholders with concrete in-world names.

## Technical Context

- C#/.NET 8 validation service with partial files under `BookOfEternityClient/Services/Validation/`.
- Test target: `BookOfEternityClient.Tests/MortalBootstrapValidationTests.cs`.
- Bootstrap baseline source: `BookOfEternityClient/Services/MortalBootstrapStateBuilder.cs`.
- GM-facing prompt source: `BookOfEternityClient/game_master_daemon.ps1`, `Examples/E_CLI_Step_Main.txt`.

## Approach

1. Add failing tests for accepted Mortal bootstrap placeholder names and allowed pre-materialization baseline.
2. Add validation helper in the Mortal bootstrap/validation area:
   - Determine whether current state is Mortal World.
   - Determine whether the current validation context is an accepted first Mortal bootstrap turn using the pending snapshot source label and `ready/turn_complete.json`.
   - Scan player-visible name fields in current location, world map, faction core/resources, and NPC core.
3. Emit `mortal_bootstrap_placeholder_player_visible_name` errors with exact path, actual value, expected in-world name, and repair hint.
4. Update GM-facing first Mortal bootstrap prompt/example guidance.
5. Run focused validation/docs tests and record results.

## Files

- Modify: `BookOfEternityClient.Tests/MortalBootstrapValidationTests.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs`
- Modify: `BookOfEternityClient/game_master_daemon.ps1`
- Modify: `Examples/E_CLI_Step_Main.txt`
- Optional modify: `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs` or source guards if existing docs tests require explicit coverage.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~MortalBootstrapValidationTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|ValidationSourceGuardTests"
```

## Prompt/Docs Rationale

This is GM-affecting. Mortal prompts/examples must be updated because the GM must understand that client scaffold labels are temporary and must be replaced before accepted first Mortal output.
