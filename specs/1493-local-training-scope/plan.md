# Implementation Plan: Local Training And Trade Scope

**Branch**: `1493-local-training-scope` | **Date**: 2026-07-11 | **Spec**: `specs/1493-local-training-scope/spec.md`

**Source issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)

## Summary

Replace global teacher discovery with a shared fail-closed local interaction scope. Mortal World resolves the canonical current location; Chaos Sea resolves the active Guardian and current abode; Shining Abode resolves `currentHallId` and actor/faction hall relationships. Apply the same predicate to listing, pending showcase creation, purchase-time enforcement, and Shining faction trade, then prove console/browser parity and trade regression.

## Technical Context

**Language/Version**: C# / .NET 8.

**Primary Dependencies**: existing `TrainingService`, `NpcTradeService`, `GuardianTradeService`, `RealmSemantics`, Explorer console/browser structured command results.

**Storage**: existing file-backed JSON only; no new persistent file.

**Testing**: xUnit service, console command, browser command-result, source-guard, and documentation coverage tests.

**Target Platform**: Windows console client and local browser client.

**Project Type**: local desktop/console game with loopback browser UI.

**Performance Goals**: resolve local targets in one bounded pass over ordinary actor collections; no player-visible wait beyond existing showcase preparation.

**Constraints**: offline-capable, fail closed, no internal IDs in visible UI, use existing Shining hall authority, no resource mutation from remote actions.

**Scale/Scope**: dozens of NPCs/afterlife profiles and factions in one local session.

**Contract Scope**: player-facing, runtime-state, pending/control, GM prompts/docs/examples, console, browser, validation/source guards.

**Verification Commands**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TrainingServiceTests|ConsoleTraining|BrowserTraining|ConsoleNpcTradeCommandTests|BrowserTradeParityTests|GuardianTrade"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|ExplorerModeSourceGuardTests"
```

## Technical Approach

1. Add a shared `LocalInteractionScopeService` under `BookOfEternityClient/Services/` that resolves realm-specific authority, including Shining current-hall actor/faction links, and exposes exact ID/name matching helpers.
2. Reuse its Mortal matcher in `NpcTradeService` and `TrainingService` rather than maintaining command-specific comparison logic.
3. Filter teachers before showcase freshness evaluation and before pending request creation.
4. Re-resolve locality inside `BuyTrainingAsync` before any resource, skill/art, receipt, or pending mutation.
5. Filter Shining faction trade by the same resolved current hall in console and structured browser command paths.
6. Keep other console/browser UI rendering unchanged except for useful empty/block messages; both already consume shared service views.
7. Add explicit tests for every realm and trade route, then update GM guidance and examples.

## Constitution Check

- **GitHub traceability**: PASS; issue #1493 is linked throughout.
- **Spec Kit fit**: PASS; cross-realm, cross-client, pending/control and GM contract behavior.
- **Player-facing integrity**: PASS; named Russian selectors and no IDs/debug terms.
- **Contract/state authority**: PASS; realm-specific authority and existing Shining hall state are explicit.
- **Test-first path**: PASS; RED service and parity tests precede implementation.
- **Verification evidence**: PASS; focused C# and docs/source-guard commands are defined.
- **Agent orchestration**: PASS; implementation remains local and independently reviewed before merge.

## Project Structure

```text
specs/1493-local-training-scope/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/local-interaction-scope.md
├── checklists/requirements.md
└── tasks.md

BookOfEternityClient/Services/
├── LocalInteractionScopeService.cs
├── TrainingService.cs
└── NpcTradeService.cs

BookOfEternityClient.Tests/
├── TrainingServiceTests.cs
├── ConsoleTrainingCommandTests.cs
├── TrainingWebCommandServiceTests.cs
├── ConsoleNpcTradeCommandTests.cs
├── ExplorerModeCommandTests.Afterlife.cs
├── WebUi/BrowserTradeParityTests.cs
└── ExplorerModeSourceGuardTests.cs

OtherGuides/Afterlife_Contract_Matrix.md
Examples/E_CLI_Training_Showcases.txt
TaskGuides/CLI_Step_Main.txt
BookOfEternityClient/Launcher/CLI_Launch_Script.md
BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1
```

**Structure Decision**: One shared runtime scope service owns location resolution. Training and Mortal trade consume it; existing Chaos/Shining trade authority stays in place but gains regression tests.

## Complexity Tracking

No constitution violations or new persisted contracts are required.
