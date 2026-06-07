# Feature Specification: Browser Storage and Transport Item Moves

**Feature Branch**: `task/814-browser-storage-transport`
**Created**: 2026-06-07
**Status**: Draft for autonomous implementation
**Input**: GitHub issue #814, "feat(web): Хранилища и транспорт — положить/забрать предметы"
**Source Issue**: [#814](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/814)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#814](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/814), [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
- **Issue type**: browser-client parity enhancement for console interactive item movement.
- **Spec Kit justification**: #814 is a browser/console parity slice that changes player-facing browser action flows, prompt/write behavior, local game-state mutation, and command/action discovery. It needs durable handoff across C# runtime, browser prompt sessions, tests, and command coverage.
- **Contract scope**: player-facing browser UI, console/browser parity, local runtime-state writes to existing inventory/location/vehicle files, C# tests, browser command metadata/fixtures. GM-facing prompts/docs/examples are not expected to change because the console flow already performs direct local item moves without new GM-authored pending/control contracts.
- **Out of scope**: sibling issues #815/#816, umbrella #817 closure, new storage-capacity/economy rules, item stack split/merge/drop work already tracked by #806, trade/social/Shining/afterlife mechanics, new GM pending/control contracts, cloud/remote storage, and React-side gameplay authority.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Move an inventory item into a local storage from the browser (Priority: P1)

As a player standing in a location with accessible `locationStorages`, I can open a browser guided action, choose a storage, choose one item from my inventory, and move that item into the selected storage without using the console prompt.

**Why this priority**: It covers the first console flow called out in #814: Deposit to Storage.

**Independent Test**: Seed `items.json` with player inventory and `current_location.json` with a storage; open and submit the browser storage prompt; verify the item is removed from player inventory, appended to the selected storage `contents`, and the browser result is Russian/player-facing.

**Acceptance Scenarios**:

1. **Given** a location has an accessible storage and the player inventory has at least one item, **When** the player opens the browser storage deposit flow, **Then** the prompt lists storage and item choices using player-facing labels.
2. **Given** the prompt remains valid, **When** the player confirms a storage deposit, **Then** the existing local file-backed state moves exactly the selected item from `game_state/inventory/items.json` to `game_state/world/current_location.json.locationStorages[].contents`.
3. **Given** the player has no movable inventory items, **When** they open the deposit flow, **Then** the browser explains that there is nothing to place without exposing raw JSON, file paths, DTO/API, endpoint, or debug wording.

---

### User Story 2 - Retrieve an item from a local storage into inventory (Priority: P1)

As a player, I can choose an item inside a location storage and retrieve it into my inventory from the browser.

**Why this priority**: It covers the second console flow called out in #814: Retrieve from Storage.

**Independent Test**: Seed a location storage with an item and an inventory file; submit the browser retrieve prompt; verify the item is removed from storage `contents`, appended to player inventory, and the result summarizes the move.

**Acceptance Scenarios**:

1. **Given** a storage contains items, **When** the browser retrieve flow opens, **Then** the prompt lists only items currently inside that storage.
2. **Given** a prompt references an item that still exists in storage, **When** the player confirms retrieval, **Then** the item is moved back to player inventory and no duplicate remains in the storage.
3. **Given** a stale prompt references a storage or item that no longer exists, **When** it is submitted, **Then** the browser blocks, writes nothing, and gives a player-facing stale-state explanation.

---

### User Story 3 - Move inventory items into and out of active transport from the browser (Priority: P1)

As a player with a vehicle/transport entry, I can move items between inventory and the vehicle inventory through browser prompt forms.

**Why this priority**: It covers the two remaining console flows called out in #814: Deposit to Vehicle and Retrieve from Vehicle.

**Independent Test**: Seed `items.json` and `game_state/misc/vehicles.json`; submit browser deposit-to-vehicle and retrieve-from-vehicle prompts; verify the selected item moves between the player inventory array and the selected vehicle `inventory` array.

**Acceptance Scenarios**:

1. **Given** the player has transport with an inventory array, **When** the browser opens the transport move flow, **Then** it offers player-facing deposit/retrieve choices based on available inventory and vehicle contents.
2. **Given** the prompt remains valid, **When** the player deposits an item into transport, **Then** the item moves from player inventory into the selected vehicle inventory.
3. **Given** the prompt remains valid, **When** the player retrieves an item from transport, **Then** the item moves from the selected vehicle inventory into player inventory.
4. **Given** the vehicle or selected item changed after prompt open, **When** the stale prompt is submitted, **Then** the browser blocks and writes nothing.

---

### User Story 4 - Browser discovery, coverage, and safety guards reflect #814 support (Priority: P2)

As a browser player, I can discover storage/transport item movement through help/action metadata, and default browser surfaces no longer treat #814 as an unresolved parity gap once implemented.

**Why this priority**: Browser parity is incomplete if the flow is write-capable but hidden from `/help`, command menus, game-screen action metadata, or command coverage fixtures.

**Independent Test**: Command/help/menu/coverage/API fixture tests show #814 storage and transport move actions as browser-supported guided forms while #817 remains open for remaining siblings.

**Acceptance Scenarios**:

1. **Given** browser command coverage is collected, **When** storage/transport action rows are inspected, **Then** #814 is no longer listed as an open browser parity gap while #817 remains tracked.
2. **Given** browser help/menu surfaces render storage/transport actions, **Then** labels use in-world Russian copy and default player UI does not expose audit/debug framing.
3. **Given** browser result/blocker copy is rendered for failed open/submit attempts, **Then** no `.json`, raw local paths, DTO/API/endpoint/debug/file-path/raw validation wording appears in default player-facing output.

### Edge Cases

- `items.json`, `current_location.json`, or `vehicles.json` is missing, malformed, or has an unexpected inventory shape.
- Location storage exists by `name` but has no `storageId`, or a stale prompt references a storage that has been removed or renamed.
- Vehicle entries use different known shapes (`vehicles[]`, active vehicle fields, optional `inventory` array); the browser must reuse existing C# selection helpers/patterns and not invent a new vehicle schema.
- Multiple items have the same display name; prompt values must use stable identities or indexes captured with a stale-submit re-check so the selected item is unambiguous.
- Active GM turn/local write/prompt-session blockers must prevent browser writes on both command-open and stale prompt-submit paths.
- Browser forms may use internal ids as values, but labels, summaries, blockers, and results must remain Russian/player-facing.
- The implementation must not add new runtime contract files, pending/control action types, capacity rules, item transformation rules, or React-side gameplay mutation handlers.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The browser command catalog MUST expose player-facing guided action(s) for storage deposit, storage retrieve, transport deposit, and transport retrieve.
- **FR-002**: Browser prompt builders MUST read available player inventory items, location storages, storage contents, vehicles, and vehicle contents from existing C# file-backed game-state authority.
- **FR-003**: Browser submit handlers MUST perform local writes through C# authority/service code, not React-side mutation, and must update only the existing inventory, current location, and vehicles state files used by the console flow.
- **FR-004**: Storage deposit MUST remove exactly the selected player inventory item and append the same JSON item to the selected `locationStorages[].contents` array.
- **FR-005**: Storage retrieve MUST remove exactly the selected storage item and append the same JSON item to the player inventory array, creating the inventory array only through the existing C# helper/pattern where necessary.
- **FR-006**: Transport deposit MUST remove exactly the selected player inventory item and append the same JSON item to the selected vehicle `inventory` array.
- **FR-007**: Transport retrieve MUST remove exactly the selected vehicle item and append the same JSON item to the player inventory array.
- **FR-008**: Direct command-open paths and stale prompt-submit paths MUST re-check active realm/session state, local write/GM-turn blockers, file parseability, selected storage/vehicle existence, selected item existence, and duplicate-name identity before writing.
- **FR-009**: Browser help, player command menu metadata, game-screen/action metadata, command coverage, and API contract fixtures MUST recognize #814 storage/transport moves as supported browser guided forms while leaving #817 open for remaining sibling issues.
- **FR-010**: Focused tests/source guards MUST be added before production implementation and must include successful move coverage plus stale/missing-state/player-facing blocker coverage.
- **FR-011**: This feature MUST keep existing runtime contract shapes unchanged. If implementation requires adding, renaming, or removing any pending/control/state field, the spec must be revised and the relevant GM-facing docs/examples/tests must be updated before completion.

### Key Entities

- **Player Inventory Item**: Existing item JSON node inside the player inventory data (`game_state/inventory/items.json`) with name, quantity/count, ids, and any item-specific properties preserved byte-for-byte as a moved JSON node.
- **Location Storage**: Existing `current_location.json.locationStorages[]` entry identified by `storageId` and/or name, with optional capacity/volume display data and a `contents[]` item array.
- **Vehicle Inventory**: Existing `game_state/misc/vehicles.json` vehicle entry selected by current C# vehicle authority, with an `inventory[]` item array.
- **Browser Prompt Session**: Existing local browser guided form flow that opens from command/action metadata, validates submit state, serializes local writes, and returns player-facing results.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused browser parity tests prove storage deposit and storage retrieve move the selected item between inventory and `locationStorages[].contents` without duplication or loss.
- **SC-002**: Focused browser parity tests prove transport deposit and transport retrieve move the selected item between inventory and vehicle `inventory` without duplication or loss.
- **SC-003**: Stale prompt, missing file, missing storage/vehicle, empty inventory/content, duplicate display name, and local write/GM-turn blockers are covered by command-open and/or submit tests with player-facing copy.
- **SC-004**: Command/help/menu/game-screen/coverage/API fixture tests prove #814 actions are browser-supported and no default browser surface leaks raw `.json`, local file path, DTO/API/endpoint/debug wording.
- **SC-005**: No afterlife/Mortal GM-authored contract, pending/control surface, validation schema, or example manifest shape changes are introduced; docs/examples remain unchanged unless implementation discovers a contract change.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserStorageTransport|BrowserInventoryManagement|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` if any runtime contract or GM-facing documentation surface changes; otherwise record that docs are not impacted because existing local file-backed console move semantics are reused unchanged.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/TypeScript files or generated browser fixtures change.
- **Manual/player-facing verification**: Inspect browser command/result labels in tests or generated fixtures for Russian in-world wording and absence of raw file/API/debug terms.

## Assumptions

- Existing console methods in `ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs` are the behavioral reference for direct local storage/transport item moves.
- Existing browser local-turn/prompt-session write coordination from #806, #812, and #813 remains the correct authority boundary for local browser mutation.
- #814 should expose existing direct local item movement semantics only; it should not add storage capacity enforcement, transport ownership checks, or stack split/merge mechanics beyond what current console code already enforces.
- If implementation discovers that the console flow currently mutates state through code that is too tightly coupled to Spectre prompts, the extraction should create C# service/helper authority shared by console and browser rather than duplicating mutation rules in React.
