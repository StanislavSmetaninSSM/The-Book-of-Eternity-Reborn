# Implementation Plan: Mortal Bootstrap Promised Merchant TradeState Guard

**Source Issue**: #1470 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1470
**Spec**: `specs/1470-mortal-bootstrap-trade-state/spec.md`

## Technical Approach

1. Add a regression test that reproduces the live failure: mortal bootstrap request promises a merchant, but NPC state contains only a trade-flavored NPC without `tradeState.canTrade=true`.
2. Extend the existing Mortal bootstrap validation guard near the promised-teacher logic to detect requested/promised trade surfaces.
3. Reuse existing `NpcTradeService` semantics where possible: a usable merchant requires `tradeState.canTrade=true` and a valid/resolvable merchant profile.
4. Update GM-facing Mortal documentation/example guidance so the GM knows the required `tradeState` shape.
5. Verify targeted validation tests plus trade/bootstrap source guards.

## Files Expected

- `BookOfEternityClient.Tests/MortalBootstrapValidationTests.cs` or nearby validation tests.
- `BookOfEternityClient/Services/Validation/ValidationService.Training.cs` or the existing Mortal bootstrap guard location.
- `Examples/E_CLI_Step_Main.txt` and/or `Examples/E_CLI_NPC_Trade.txt`.
- `specs/1470-mortal-bootstrap-trade-state/tasks.md`.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "MortalBootstrapValidationTests|NpcTrade|GameEngineSourceGuardTests|ExampleDocumentationValidationTests"
```
