# Implementation Plan: Universal Realm-Aware Trade Command

**Branch**: `task/1491-universal-trade` | **Date**: 2026-07-10 | **Spec**: `specs/1491-universal-trade-command/spec.md`

**Source Issue**: [#1491](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1491)

## Summary

Register a universal `/trade` / `/торговля` command and resolve it at each client command boundary into one of the existing canonical trade commands. Keep all trade business logic in `NpcTradeService`, `GuardianTradeService`, `ShiningTradeService`, and their current console/browser flows.

## Technical Context

**Language/Version**: C# / .NET 8
**Primary Dependencies**: `ExplorerCommandCatalog`, `ExplorerCommandParser`, `RealmSemantics`, `ExplorerMode`, `ExplorerWebCommandService`, existing trade result builders and write services
**Storage**: No new storage; existing game-session JSON state and pending requests remain unchanged
**Testing**: xUnit focused catalog, parser, console trade, browser trade, help, and afterlife documentation guard tests
**Target Platform**: Windows console client and local browser client
**Constraints**: TDD; no new economy rules; no duplicated trade flow; no raw technical player copy; preserve in-place GM wait contract

## Architecture

1. Add a small shared command-protocol resolver that maps a resolved realm and optional internal arguments to the existing canonical trade command.
2. Register the universal aliases in the command catalog as a local-turn, browser-executable command.
3. At the console boundary, dispatch the generic command through a small adapter that resolves the realm and invokes the existing realm-specific handler while the ordinary local-turn lock remains authoritative.
4. At the browser result-builder boundary, resolve and reparse the generic command before selecting the existing result builder. Return the canonical routed command so prompt sessions and submissions continue through current write services.
5. For no-argument entry, project location-aware NPC, Guardian, or Shining-faction choices with player-facing labels and internal deep-link actions; do not create a write session until an entity is chosen.
6. Add one help row for the universal command while retaining specialized rows.

## File Map

- Create `BookOfEternityClient/CommandProtocol/ExplorerRealmTradeCommandResolver.cs`: shared, side-effect-free realm-to-command routing.
- Modify `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`: register `trade` aliases.
- Modify `BookOfEternityClient/UI/ExplorerMode.cs` and add `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Trade.cs`: register a generic console adapter that delegates to existing trade handlers.
- Modify `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs`: resolve the generic browser command into the existing realm-specific result builder and canonical prompt command.
- Modify `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs`: document universal command.
- Modify `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`: provide player-facing metadata for the generic command.
- Add or modify focused tests under `BookOfEternityClient.Tests/` and `BookOfEternityClient.Tests/WebUi/`.

## Data Flow

`/торговля` -> parser recognizes universal descriptor -> shared resolver reads current realm -> realm-specific location-aware target list -> player selects a named entity -> internal canonical command (`/npc_trade`, `/guardian_trade`, or `/shining_trade`) carries its stable ID -> existing trade view/write service -> existing in-place pending GM wait where required.

An explicit target remains a supported internal deep link and skips the selection list. Ordinary player copy never asks for that ID.

An unresolved realm stops at the boundary with localized guidance and no downstream handler invocation.

## Constitution Check

- **Issue traceability**: PASS; #1491 exists before repository edits.
- **Spec Kit fit**: PASS; cross-client player UX and afterlife realm routing require durable artifacts.
- **Player-facing integrity**: PASS; Russian command and explicit localized errors; no technical leakage.
- **State authority**: PASS; all mutations remain in existing authoritative trade services.
- **Test first**: PASS; command and client regressions will fail before production edits.
- **GM synchronization**: Expected no GM contract change. Verify the diff and record the no-update rationale; run documentation guards because afterlife routing is touched.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~RealmTrade|FullyQualifiedName~ConsoleNpcTradeCommandTests|FullyQualifiedName~BrowserTradeParityTests|FullyQualifiedName~ExplorerCommandCatalog|FullyQualifiedName~ExplorerHelp"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Manual Agent Console replay:

1. Load a Chaos Sea session with an active Guardian.
2. Enter `/торговля`.
3. Confirm the Guardian trade panel opens immediately or remains on the established in-place vitrine waiting screen.
4. Confirm no narrative turn is submitted solely because the alias was resolved.

## Risks

- Browser prompt submissions can fail if the prompt session stores `/торговля` instead of the canonical routed command. The browser test must submit an operation, not only inspect the initial form.
- A selection screen can accidentally acquire the local mutation lock or create a pending vitrine request before the player has chosen a merchant. Tests must assert both remain absent.
- Shining Abode pending-bootstrap state is not ordinary trade availability. Existing Shining guards must remain authoritative.
- Generic command arguments must be copied, not parsed or reinterpreted by the resolver.
