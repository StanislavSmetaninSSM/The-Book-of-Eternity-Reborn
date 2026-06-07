# Implementation Plan: Browser Storage and Transport Item Moves

**Source Issue**: [#814](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/814)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Feature Spec**: [spec.md](spec.md)
**Feature Branch**: `task/814-browser-storage-transport`

## Summary

Implement Browser Client parity for the console storage/transport item-move flows: deposit/retrieve between inventory and local location storage, and deposit/retrieve between inventory and vehicle inventory. Reuse existing C# file-backed item movement authority or extract shared C# helpers from the console flow; React remains generic prompt/result presentation and must not own gameplay mutation.

## Technical Context

- Runtime: .NET 8 C# client in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Browser shell: C# web command/prompt-session services plus tracked React fixtures in `BookOfEternityClient.WebFrontend/`.
- Existing console reference: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs` methods `ShowStorageInteractivePanel` and `ShowVehicleInventoryInteractivePanel`.
- Existing storage/vehicle file authority: `game_state/inventory/items.json`, `game_state/world/current_location.json.locationStorages[]`, and `game_state/misc/vehicles.json`.
- Existing browser local-turn patterns: #806 inventory management, #812 incarnation gates, and #813 relic forge implementations in `ExplorerCommandCatalog.cs`, `ExplorerWebPromptSessionService.cs`, `BrowserAfterlifeWriteService.cs` and/or local-turn write services, `BrowserPlayerCommandMenuBuilder.cs`, `BrowserCommandCoverageService.cs`, `ExplorerHelpCommandResultBuilder.cs`, and focused browser parity tests.

## Constitution / Governance Checks

- GitHub issue traceability: all implementation changes are tied to #814 and umbrella #817.
- Spec Kit fit: #814 changes browser/console parity, player-facing browser prompt UX, and local state-write behavior across multiple code/test surfaces, so Spec Kit is required.
- Player-facing integrity: default labels/blockers/results must be Russian/in-world and must not expose raw `.json`, local paths, DTO/API, endpoint, validation, or debug wording.
- Contract/state authority: no new GM-authored pending/control contract is planned. If a contract/state shape change becomes required, stop, revise `spec.md`/`plan.md`/`tasks.md`, and update GM-facing docs/examples/tests before continuing.
- Test-first path: add focused RED tests/source guards before production implementation.
- Verification evidence: focused C# tests, docs coverage if contract docs change, frontend verify when fixtures/React change, `git diff --check`, and static scan are required before merge.
- Agent orchestration: Hermes owns final PR/merge/issue closure; Codex implements and reports evidence.

## Project Structure / Files

### Expected production files

- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
  - Add or update browser-discoverable command descriptors/aliases for #814 guided storage/transport move flows.
- `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs`
  - Add player-facing help rows for browser storage/transport move actions if they are command-addressable.
- `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs` and/or related Explorer command-result builders
  - Add command-result blocks/open-prompt metadata for #814, following existing local-turn prompt patterns.
- `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs`
  - Route #814 mutating command(s) through browser prompt sessions and local write/GM-turn safety gates.
  - Build prompt forms for storage vs transport, direction, target storage/vehicle, item, and confirmation.
- `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs` and/or existing local web write service used by #806
  - Add write handlers or extracted helpers for storage/transport submissions.
  - Re-read files and re-check selected storage/vehicle/item on submit before writing.
  - Serialize writes through existing local write coordination and keep state mutation in C#.
- `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`
  - Expose player-facing action entries when storage/transport moves are available; labels remain Russian/player-facing.
- `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs`
  - Remove #814 as an open browser parity gap once storage/transport moves are covered; keep #817 open for #815/#816 and any other siblings.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json`
  - Refresh if command coverage changes.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`
  - Refresh if command/action metadata changes affect the fixture.

### Expected test files

- `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs` (create)
  - RED/GREEN tests for opening prompts and submitting storage deposit/retrieve and transport deposit/retrieve.
  - Stale submit and missing-state tests for storage/vehicle/item changes.
  - Player-facing copy guards for blockers and results.
- Existing browser command/menu/help/coverage tests as needed:
  - `BrowserInventoryManagementTests`
  - `ExplorerWebPromptSessionService` / `ExplorerWebCommandServiceTests`
  - `BrowserPlayerCommandMenuBuilderTests`
  - `BrowserCommandCoverageServiceTests`
  - `BrowserApiContractTests`
  - relevant source-guard tests for default browser copy.
- Existing console tests:
  - `ExplorerModeCommandTests.TradeAndInventory.cs` remains the console behavior reference and should stay green.

### Spec Kit artifacts

- `specs/814-browser-storage-transport/spec.md`
- `specs/814-browser-storage-transport/plan.md`
- `specs/814-browser-storage-transport/tasks.md`

## Implementation Phases

### Phase 1 — Spec Kit setup and source inspection

1. Confirm `AGENTS.md`, `.specify/memory/constitution.md`, issue #814, umbrella #817, and this feature directory are aligned.
2. Inspect the console storage/transport flow completely before implementing browser parity:
   - `ShowStorageInteractivePanel`
   - `ShowVehicleInventoryInteractivePanel`
   - `GetPlayerInventoryArrayNode`
   - `FindVehicleNode`
   - `GetInventoryItemIdentity`
   - `MakeUniqueChoiceLabels`
3. Inspect #806 browser inventory management and #812/#813 prompt/write patterns and reuse their local-write safety model instead of inventing a React gameplay handler.

### Phase 2 — RED tests and source guards

1. Add focused browser parity tests for opening storage deposit/retrieve prompts with storage and item choices.
2. Add storage submit tests proving deposit and retrieve move exactly one selected JSON item between inventory and `locationStorages[].contents`.
3. Add browser parity tests for opening transport deposit/retrieve prompts with vehicle and item choices.
4. Add transport submit tests proving deposit and retrieve move exactly one selected JSON item between inventory and selected vehicle `inventory`.
5. Add stale guard tests for removed/renamed storage, removed vehicle, removed selected item, duplicate display names, malformed files, and local write/GM-turn blockers.
6. Add/update command coverage, command menu/help, API fixture, and source guard tests proving #814 actions are browser-supported and player-facing.
7. Run the focused RED command and record expected failures in `tasks.md` before production implementation.

### Phase 3 — Minimal implementation

1. Add/adjust command descriptors, aliases, and player-facing metadata for #814 flows.
2. Add prompt builders that enumerate storage/vehicle/action/item choices from current C# state using stable submit values and player-facing labels.
3. Extract shared C# move helpers if the console mutation is embedded in Spectre prompt methods; keep helpers local and focused.
4. Add browser write handlers that re-read state at submit time, validate the selected target and item, and write only the existing files.
5. Update menu/help/coverage fixtures and command results.
6. Keep runtime contract shape unchanged; do not modify GM-facing docs unless implementation discovers a required contract change.

### Phase 4 — GREEN verification and reconciliation

1. Run the focused RED/GREEN filter and record exact counts.
2. Run broader storage/transport/browser/API parity sweep and console reference tests.
3. Run docs-sensitive tests if any contract/doc-impacting surface changed, otherwise record why not required.
4. Run C# builds, frontend verification when frontend fixtures/assets change, `git diff --check`, and added-line static scan excluding Spec Kit docs if necessary.
5. Update `tasks.md` with actual verification evidence and final task statuses.
6. Commit focused changes with `[skip ci]`; leave PR/merge/issue closure to Hermes.

## Acceptance Criteria Mapping

- Spec US1 / FR-001 through FR-004 -> storage command/prompt builder/write handler plus storage deposit tests.
- Spec US2 / FR-005 -> storage retrieve prompt/write handler plus stale submit tests.
- Spec US3 / FR-006/FR-007 -> transport prompt/write handler plus deposit/retrieve tests.
- Spec US4 / FR-009/FR-010 -> help/menu/coverage/API/source guard tests.
- Spec FR-011 / SC-005 -> no contract shape changes; docs tests only required if a contract shape or GM-facing guidance changes.

## Verification Plan

Baseline before implementation in the fresh worktree:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"
```

Baseline observed by Hermes before implementation on 2026-06-07: passed, 0 failed / 442 passed / 0 skipped / 442 total. Restore/build ran first in the fresh worktree and produced test binaries normally. Spec Kit prerequisite check returned `FEATURE_DIR=E:\Games\worktrees\boe-814-storage-transport\specs\814-browser-storage-transport` and `AVAILABLE_DOCS=["tasks.md"]`.

Focused expected after adding RED tests:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests|BrowserInventoryManagement|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Final local gates to run when relevant:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransportParityTests|BrowserInventoryManagement|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/TypeScript frontend files or generated browser fixtures/build artifacts change in a way that affects frontend verification.

## Risks and Mitigations

- **Risk**: Browser submit path bypasses local write/GM-turn safety. **Mitigation**: use existing prompt-session/local-write owner patterns and stale-submit tests.
- **Risk**: Browser item movement duplicates or loses JSON item data. **Mitigation**: test exact before/after item presence and preserve the moved JSON node without projection into a browser-specific DTO.
- **Risk**: Duplicate display names move the wrong item. **Mitigation**: stable submit values plus stale re-check against current arrays and tests with duplicate labels.
- **Risk**: Browser copy leaks raw local files or debug terms. **Mitigation**: source/result guards for `.json`, file paths, DTO/API/endpoint/debug wording.
- **Risk**: Runtime contract shape change becomes necessary. **Mitigation**: stop, revise Spec Kit artifacts, and update GM-facing docs/examples/coverage tests before continuing.
