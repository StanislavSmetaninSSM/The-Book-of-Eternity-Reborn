# Implementation Plan: Player-facing Newline Harness

**Source Issue**: [#1492](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1492)

## Approach

1. Extend the shared C# player-facing text normalizer for literal PowerShell newline tokens.
2. Normalize `response`, dialogue `text`, and dialogue `inputValue` in `Write-BoeJson` for the two transient player-facing output files.
3. Normalize and persist accepted narrative/interface payloads before validation as a direct-write fallback.
4. Update the compact GM output template, main GM guide/example, and launch guidance.
5. Run focused helper/validation/runtime/docs tests, then replay the saved Chaos Sea spiritual-conflict opening.

## Contract Impact

This is a realm-agnostic player-facing output/harness contract. It does not add or change afterlife pending/control/state authority. Mortal and afterlife prompt guidance must stay synchronized.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "PlayerFacingTextNormalizer|StateManagerTests|AcceptedTurnNarrativePayloadValidationTests|AcceptedTurnInterfacePayloadNormalizationTests|GmTurnHelperContractTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```
