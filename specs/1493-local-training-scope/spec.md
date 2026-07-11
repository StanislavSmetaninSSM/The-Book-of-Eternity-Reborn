# Feature Specification: Local Training And Trade Scope

**Feature Branch**: `1493-local-training-scope`

**Created**: 2026-07-11

**Status**: Draft

**Input**: `/обучение` and `/торговля` must list only teachers or traders available at the player's current location in Mortal World and afterlife, without requiring internal identifiers.

## Source Issues & Scope

- **Source GitHub issue**: [#1493](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1493)
- **Related training specification**: `specs/1378-training-vitrines/`
- **Issue type**: player-facing contract bug and harness hardening.
- **Spec Kit justification**: The fix crosses Mortal World, Chaos Sea, Shining Abode, console/browser parity, client-owned purchase authority, pending GM requests, and GM-authored actor location contracts.
- **Contract scope**: player-facing, runtime-state, pending/control, GM-facing prompts, docs, examples, console, browser, validation/source guards.
- **Out of scope**:
  - Adding a new Shining Abode location model. Existing `currentHallId`, `halls[]`, and faction `hallId` are the location authority for this feature.
  - Removing internal ID-bearing actions. IDs may remain hidden machine values after the player chooses a named entity.
  - Redesigning training or trade prices, offers, cards, or purchase mechanics.
  - Making remote teachers available through teleportation, letters, or asynchronous lessons.
  - Changing the existing realm-wide, client-owned Shining return-cycle trade auto-refresh; it is world-lifecycle work, while player-triggered requests remain local.

## User Scenarios & Testing

### User Story 1 - Choose A Local Mortal Teacher (Priority: P1)

As a player in a mortal location, I open `/обучение` and see only teachers physically available where my character is now.

**Why this priority**: The current global list breaks spatial gameplay and can ask the GM to prepare a remote teacher's showcase.

**Independent Test**: Seed two valid teachers in different locations and open training while the player is co-located with only one.

**Acceptance Scenarios**:

1. **Given** two teachable NPCs in different locations, **When** the player opens `/обучение`, **Then** only the co-located teacher appears by name.
2. **Given** current location authority is missing, **When** the player opens `/обучение`, **Then** no global teacher list or pending showcase requests are created and a useful Russian explanation is shown.
3. **Given** an internal action targets a remote teacher, **When** purchase or showcase access is attempted, **Then** it is rejected before resources or pending state change.

---

### User Story 2 - Choose A Local Chaos Sea Mentor (Priority: P1)

As a soul in the Chaos Sea, I see training only from the active Guardian or other mentor demonstrably present in the current abode.

**Why this priority**: All afterlife profiles currently appear in training regardless of the soul's abode.

**Independent Test**: Seed two Guardian mentors in different abodes, set one as active/current, and open `/обучение`.

**Acceptance Scenarios**:

1. **Given** two Guardian mentors in different abodes, **When** `/обучение` opens, **Then** only the active Guardian bound to `chaosSeaNavigation.currentAbodeId` appears.
2. **Given** a non-Guardian mentor has an explicit location matching the current abode, **When** `/обучение` opens, **Then** the mentor may appear.
3. **Given** a mentor belongs to another realm or abode, **When** an internal purchase action names it, **Then** the action is rejected without spending Ink Feathers or creating a request.

---

### User Story 3 - Choose A Shining Abode Mentor (Priority: P1)

As a soul in the Shining Abode, I see mentors and faction trade available in my current hall and never see actors from another hall or realm.

**Why this priority**: Shining profiles share the same file with actors from other realms, and the current implementation does not filter them.

**Independent Test**: Seed two Shining mentors in different halls plus one Chaos Sea mentor, set `currentHallId`, and open training and trade.

**Acceptance Scenarios**:

1. **Given** mentors from two Shining halls and Chaos Sea, **When** `/обучение` opens, **Then** only the mentor linked to `currentHallId` appears.
2. **Given** two player-visible factions in different halls, **When** `/торговля` opens, **Then** only factions whose `hallId` equals `currentHallId` appear.
3. **Given** `currentHallId` is absent or does not resolve to `halls[]`, **When** a hall-local command opens, **Then** the system fails closed instead of showing all profiles/factions.

---

### User Story 4 - Keep Trade And Both Clients Consistent (Priority: P2)

As a player, I receive the same named local entity choices in console and browser, and `/торговля` obeys the same spatial rule.

**Why this priority**: UI-only filtering or separate realm matchers would let clients and commands drift again.

**Independent Test**: Execute console and browser command-result paths over the same Mortal, Chaos Sea, and Shining fixtures and compare the visible target sets.

**Acceptance Scenarios**:

1. **Given** the same session state, **When** console and browser open `/обучение`, **Then** both expose the same local teachers and no internal identifiers.
2. **Given** local and remote Mortal merchants, **When** `/торговля` opens, **Then** only local merchants appear.
3. **Given** multiple Chaos Sea Guardians, **When** `/торговля` opens, **Then** only the active Guardian in the current abode appears.
4. **Given** the active Shining Abode, **When** `/торговля` opens, **Then** only player-visible Shining factions from the active realm appear.

### Edge Cases

- A Mortal NPC has a matching location name but no ID, or matching ID but no name.
- A Mortal NPC supplies both a location ID and name, but they identify different locations; the actor is rejected rather than accepting the one matching alias.
- A same-turn Mortal NPC has only an `initialId` plus current location aliases.
- Current location JSON is malformed, absent, or has neither ID nor name.
- The active Guardian and `currentAbodeId` disagree; interaction fails closed rather than guessing.
- An afterlife profile omits `realm`; only an exact active-Guardian identity may establish Chaos Sea locality.
- A non-canonical generic `afterlife` realm value does not identify Chaos Sea or Shining Abode and fails closed.
- A Shining mentor profile has no direct hall, but its actor is a resident, faction leader, political actor, or faction member whose canonical faction resolves to the current hall.
- A Shining mentor has an indirect local faction association but an explicit direct hall in another hall; the explicit contradiction rejects the mentor.
- A stale pending training request exists for a now-remote teacher; it remains historical/pending state but is not dispatched from the local screen.
- A teacher moves after the screen was opened but before purchase; purchase rechecks location and blocks.
- Dynamic names and block reasons contain Spectre markup characters or HTML-sensitive text.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST resolve one canonical local interaction scope for the active realm and MUST fail closed when required location authority is missing or contradictory.
- **FR-002**: Mortal locality MUST derive from `game_state/world/current_location.json` and match NPC `currentLocationId`/`currentLocation` by canonical exact aliases. One supplied alias MAY establish locality, but when both sides provide ID and name every supplied alias MUST agree; contradictory aliases MUST fail closed.
- **FR-003**: Mortal `/обучение` MUST list only co-located NPCs with a usable teacher profile.
- **FR-004**: Chaos Sea locality MUST derive from the active Guardian and `chaosSeaNavigation.currentAbodeId`; other mentors require explicit matching abode location evidence.
- **FR-005**: Shining Abode locality MUST derive from `shining_abode_state.currentHallId`, require that hall in `halls[]`, and include only mentors whose direct hall or canonical resident/faction/leadership/political-actor association resolves to that hall.
- **FR-006**: Missing/stale showcase requests MUST be created or dispatched only for teachers in the resolved local scope.
- **FR-007**: Training purchase execution MUST re-resolve locality immediately before commit and reject remote sources before spending resources, updating skills/arts, writing receipts, or creating skill-evolution requests.
- **FR-008**: Player-facing selection MUST use entity names and summaries; internal IDs MAY be carried only in hidden command/action values after selection.
- **FR-009**: Console and browser command flows MUST consume the same service-level local target set.
- **FR-010**: Mortal and Chaos Sea trade target discovery and every direct inventory, purchase, sale, and buyback operation MUST re-resolve the actual current realm and location through the shared local scope immediately before commit. A stale specialized command alias MUST NOT bypass realm or location enforcement.
- **FR-011**: Shining trade MUST list only player-visible factions whose `hallId` equals the valid `currentHallId`.
- **FR-012**: Player-facing empty/error states MUST be localized, useful, and free from JSON paths, DTO names, IDs, and agent/validation terminology.
- **FR-013**: GM guidance and worked examples MUST explain that an actor is actionable through `/обучение` only when its canonical realm/location agrees with the player's local interaction scope.
- **FR-014**: Dynamic teacher, mentor, location, and error text MUST remain escaped/sanitized in console and browser rendering.
- **FR-015**: Training and trade detail/list reads MUST reconcile against fresh scope and actor state before returning local targets or details, and MUST NOT persist client refreshes for a source that is no longer local.

### Key Entities

- **LocalInteractionScope**: Resolved realm plus the location authority relevant to that realm: Mortal location ID/name, Chaos Sea active Guardian/abode, or Shining Abode current hall.
- **LocalInteractionTarget**: A teacher, mentor, merchant, Guardian, or Shining faction proven available in the resolved scope.
- **TrainingTeacherView**: Existing named teacher/mentor presentation filtered by locality before showcase evaluation or pending request creation.
- **PendingTrainingShowcaseRequest**: Existing GM request surface; local screens may create/dispatch it only for a local source actor.

## Success Criteria

### Measurable Outcomes

- **SC-001**: In fixtures with at least two teachers in different places, 100% of console and browser training selectors show only the co-located teacher.
- **SC-002**: A remote training purchase changes zero player resources, skills, arts, receipts, and pending requests.
- **SC-003**: Mortal World, Chaos Sea, and Shining Abode each have at least one automated positive-local and negative-remote training test.
- **SC-004**: Existing local trade flows in all three realm routes retain passing regression coverage.
- **SC-005**: Documentation coverage proves both Mortal and afterlife GM authoring guidance describes location-bound availability.

## Verification Plan

- **C# verification**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "TrainingServiceTests|ConsoleTraining|BrowserTraining|ConsoleNpcTradeCommandTests|BrowserTradeParityTests|GuardianTrade"
```

- **Documentation/contract verification**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|ExplorerModeSourceGuardTests"
```

- **Frontend verification**: No React/CSS change is expected; browser parity is verified through structured command-result C# tests. Run `npm run verify` only if frontend files change.
- **Manual/player-facing verification**: Open `/обучение` and `/торговля` in Mortal, Chaos Sea, and Shining fixture saves; verify named local selectors, useful empty states, and remote-action rejection.

## Assumptions

- Shining `currentHallId` is the current location for hall-bound training and trade; an absent hall means no hall-local targets rather than global access.
- Existing internal ID-bearing commands remain implementation details used after a named selection; the player is never instructed to type IDs.
- Existing pending requests for remote actors are not silently deleted; they simply cannot be created or dispatched by the current local screen.
- Training and trade keep their current price, showcase, receipt, and refresh semantics.
