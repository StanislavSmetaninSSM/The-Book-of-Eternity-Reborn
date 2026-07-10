# Quickstart: Verify Local Training And Trade Scope

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Automated

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TrainingServiceTests|ConsoleTraining|BrowserTraining|ConsoleNpcTradeCommandTests|BrowserTradeParityTests|GuardianTrade"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|ExplorerModeSourceGuardTests"
```

## Manual Mortal Flow

1. Put two teacher NPCs in different locations.
2. Set `current_location.json` to the first location.
3. Open `/обучение`; only the first teacher is visible.
4. Attempt the second teacher's hidden direct action; verify no money/XP/receipt/request change.
5. Repeat `/торговля` with local and remote merchants.

## Manual Chaos Sea Flow

1. Put two Guardian mentors in different abodes.
2. Set active Guardian and `currentAbodeId` to the first.
3. Open `/обучение` and `/торговля`; only the current Guardian is visible.

## Manual Shining Flow

1. Put one Shining mentor and one Chaos mentor in profile state.
2. Set soul realm to Shining Abode.
3. Open `/обучение`; only the Shining mentor is visible.
4. Open `/торговля`; only active Shining player-visible factions are listed.
