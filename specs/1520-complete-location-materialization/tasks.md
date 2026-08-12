# Tasks: Complete Mortal Location Materialization

**Input**: Design documents from `/specs/1520-complete-location-materialization/`
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)

**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`,
`contracts/`, and `quickstart.md`

**Tests**: Every behavior change follows red-green-refactor. Use PowerShell 7
and `scripts/test-csharp.ps1`; never use an unbounded full-solution test command.

**Compatibility**: The game is unreleased. Migrate active repository state and
remove obsolete aliases/fallbacks; do not add receipt-less promotion.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May proceed in a different file after phase prerequisites complete.
- **[Story]**: Maps to one independently testable story in `spec.md`.
- Every task names its primary file path and, for behavior changes, starts with
  a failing test or explicitly follows a preceding red-test task.

## Phase 1: Setup and Baseline

**Purpose**: Reconfirm tracked scope and capture a clean implementation baseline.

- [x] T001 Confirm issue #1513 remains open, branch `1520-complete-location-materialization` is isolated in `E:/Games/worktrees/boe-1513-location-materialization`, and preserve unrelated `bin/obj` artifacts using `git status --short`
- [x] T002 Re-read `AGENTS.md`, `.specify/memory/constitution.md`, and every artifact under `specs/1520-complete-location-materialization/`; record any issue drift in `specs/1520-complete-location-materialization/research.md`
- [x] T003 Record the existing successful 2937-test Fast baseline and its result directory in `specs/1520-complete-location-materialization/quickstart.md`
- [x] T004 [P] Inventory every active, positive, negative, backup-only, and fragment-only location/map fixture in `specs/1520-complete-location-materialization/fixture-migration-inventory.md`
- [x] T005 [P] Inventory every production reader/writer that consumes `current_location.json`, `world_map.json`, `knownExits`, `adjacencyMap`, location names, or coordinate endpoints in `specs/1520-complete-location-materialization/research.md`

---

## Phase 2: Foundational Test Infrastructure

**Purpose**: Establish one deterministic current-schema fixture vocabulary and
transaction harness before production behavior changes.

**Critical**: No user-story implementation begins until this phase compiles and
existing focused controls remain green.

- [x] T006 Create deterministic complete raw/canonical Mortal location, link, envelope, receipt, current projection, and identity-index builders in `BookOfEternityClient.TestSupport/MortalLocationTestFixture.cs`
- [x] T007 [P] Create file-backed pre-turn/raw/current/composed-state and injected-write-failure helpers in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationTestContext.cs`
- [x] T008 Add fixture shape, unique identity, and explicitly-malformed labeling tests in `BookOfEternityClient.Tests/MortalLocationTestFixtureTests.cs`, run them red then green without production changes
- [x] T009 Add shared assertion helpers for byte-exact rollback and forbidden player authority tokens in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationAssertions.cs`
- [x] T010 Run the smallest existing map/bootstrap focused controls from `specs/1520-complete-location-materialization/plan.md` and record the result directories in `specs/1520-complete-location-materialization/quickstart.md`

**Checkpoint**: Tests can express complete/empty-by-design locations, links,
hidden/rumored state, same-turn references, neutral bootstrap, and every
intentional malformed case without large duplicated JSON literals.

---

## Phase 3: User Story 1 — Complete First Creation (Priority: P1) 🎯 MVP

**Goal**: A current or remote Mortal location is complete, exact, receipt-bearing,
indexed, and canonical on its first accepted turn.

**Independent Test**: Submit one complete current creation and one complete
remote creation, then omit each governed section in turn. Valid packages create
one canonical map object and receipt; invalid packages leave map/current/index
unchanged. An isolated complete location with an explicit empty topology reason
remains valid.

### Tests for User Story 1

- [x] T011 [US1] Add red exact-envelope, route/source agreement, section disposition, physical empty-shape, duplicate-property, wrong-realm, and GM-owned-client-field tests in `BookOfEternityClient.Tests/MortalLocationMaterializationContractTests.cs`
- [x] T012 [US1] Add red canonical receipt/seal/index agreement, receipt-less rejection, immutable envelope, exact identity, and historical replay tests in `BookOfEternityClient.Tests/MortalLocationIdentityStateTests.cs`
- [x] T013 [US1] Add red complete semantic tests for outdoor/indoor shape, placement, discovery, two difficulty profiles, chronicle, every governed collection, and topology disposition in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.cs`
- [x] T014 [US1] Add red route-ownership tests for one current creation, one remote creation, duplicate cross-route creation, multiple remote candidates, and existing full resend in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Routes.cs`
- [ ] T015 [US1] Add red raw-to-canonical normalizer tests for permanent IDs, receipt sealing, wrapper removal, map/current coherence, and identity-index updates in `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.MortalLocations.cs`
- [x] T016 [P] [US1] Add red validation phase selection/order/equivalence coverage for `AcceptedTurnLocationMaterializationCompleteness` in `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`
- [x] T017 [P] [US1] Add a deterministic one-pass indexing/scaling test with the 2.5x doubling bound in `BookOfEternityClient.Tests/MortalLocationIdentityStateTests.cs`

### Implementation for User Story 1

- [x] T018 [US1] Implement exact identity, envelope, section-disposition, complete location/link, receipt, and seal primitives in `BookOfEternityClient/Services/MortalLocationMaterializationContract.cs`
- [x] T019 [US1] Implement client-owned index parsing, exact/confusable history, deterministic serialization, receipt sealing, and location/link lifecycle records in `BookOfEternityClient/Services/MortalLocationIdentityState.cs`
- [x] T020 [US1] Implement one-pass pre-turn/raw location, link, coordinate, parent, route, and cross-reference indexes plus immutable `MortalLocationAcceptedTurnPlan` models in `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlan.cs`
- [x] T021 [US1] Implement raw current/remote route classification, complete composed objects, permanent ID assignment, receipts, index transitions, and exact supported reference plans in `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs`
- [x] T022 [US1] Add raw pre-seal and canonical post-seal validation, receipt-less rejection, cross-route uniqueness, coordinate/parent/discovery/section checks, and structured repair context in `BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs`
- [x] T023 [US1] Register phase bit 30, selectable profiles, raw response validation, and accepted-turn entrypoints in `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`, `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`, `BookOfEternityClient/Services/ValidationService.cs`, and `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T024 [US1] Normalize raw wrappers into exact `world_map.locations[]`/`links[]`, seal accepted creations, write the identity index, and build selected current projection in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs`
- [ ] T025 [US1] Run Mortal location normalization before Mortal items and register world map/current/index in canonical input, backup, snapshot, and rollback path lists in `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- [ ] T026 [US1] Replace legacy recursive/alias-aware location validation with exact canonical root and current-projection coherence checks in `BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs` and `BookOfEternityClient/Services/Validation/ValidationService.InventoryNpcWorldCrossRefs.cs`
- [ ] T027 [US1] Update current-schema state-file allowlists and client-owned index protection in `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`, `BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- [ ] T028 [US1] Run all US1 Focused filters and record red/green command/result evidence in `specs/1520-complete-location-materialization/quickstart.md`

**Checkpoint**: Both creation routes independently produce one complete,
receipt-bearing location and exact index entry; invalid or receipt-less data has
no compatibility path.

---

## Phase 4: User Story 2 — Materialized Bootstrap World (Priority: P1)

**Goal**: A fresh Mortal life remains pending until the ordinary contract
accepts a complete visited start plus either a complete neighbor/link or a
narrative-only unresolved exit.

**Independent Test**: Start a new life from a neutral scaffold. Accept the
reserved start/neighbor/link through ordinary routes; repeat with a narrative-only
exit; reject missing start, alias reservations, fake neighbor identity, and
bootstrap-only reduced semantics without committing playable state.

### Tests for User Story 2

- [ ] T029 [US2] Add red fresh-root assertions for neutral map/current/index and exact reserved scaffold references/coordinates/IDs in `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs`
- [ ] T030 [US2] Add red golden-path start+neighbor+directed-link and narrative-only unresolved-exit acceptance tests in `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs`
- [ ] T031 [US2] Add red missing-start, partial-start, reservation-alias, duplicate-route, fake-neighbor, and settled-reservation replay tests in `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs`
- [ ] T032 [P] [US2] Add red snapshot/tracked-path assertions for world map, current location, scaffold, and location index in `BookOfEternityClient.IntegrationTests/PendingTurnSnapshotTests.cs`

### Implementation for User Story 2

- [ ] T033 [US2] Replace pseudo-ready start/neighbor state with neutral canonical roots and empty location index in `BookOfEternityClient/Services/MortalBootstrapStateBuilder.cs`
- [ ] T034 [US2] Emit exact start/neighbor/link reservations, coordinate constraints, materialization requests, and narrative-only alternative in `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- [ ] T035 [US2] Validate scaffold source authority, reservation consumption, pending-state retention, and ordinary contract equivalence in `BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs`
- [ ] T036 [US2] Remove bootstrap `knownExits`/`adjacencyMap` and pseudo-location guidance from the runtime bootstrap reminder in `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- [ ] T037 [US2] Run the bootstrap and snapshot Focused filters and record evidence in `specs/1520-complete-location-materialization/quickstart.md`

**Checkpoint**: A new session cannot become playable through a receipt-less
placeholder, while ordinary first materialization handles the entire golden path.

---

## Phase 5: User Story 3 — Exact Topology and Existing Lifecycle (Priority: P1)

**Goal**: Directed topology and later movement/updates use exact permanent
identity, preserve original receipts, and never infer aliases or reverse edges.

**Independent Test**: Create ordinary, one-way, portal, hidden, sealed, and
isolated topology; move to and narrowly update an existing location; update and
remove one exact link. Assert exact endpoints, unchanged materialization evidence,
derived exits, and rejection of full/name/coordinate-based operations.

### Tests for User Story 3

- [ ] T038 [US3] Add red link envelope/receipt, dual-endpoint, self/dangling/confusable endpoint, duplicate coordinate, parent-cycle, and no-inferred-reverse tests in `BookOfEternityClient.Tests/MortalLocationMaterializationContractTests.cs`
- [ ] T039 [US3] Add red same-turn two-location endpoint rewrite and exact permanent link identity tests in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Topology.cs`
- [ ] T040 [US3] Add red movement-only, narrow-update allowlist, receipt/envelope immutability, discovery transition, link update/removal, and replay tests in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Lifecycle.cs`
- [ ] T041 [P] [US3] Replace legacy wrapper/name/coordinate map tests with receipt-bearing exact directed topology cases in `BookOfEternityClient.Tests/LocalMapViewerServiceTests.cs`
- [ ] T042 [P] [US3] Add red exact current-location locality tests for interactions, training, and trade in `BookOfEternityClient.Tests/LocalInteractionScopeServiceTests.cs`, `BookOfEternityClient.Tests/TrainingServiceTests.cs`, and `BookOfEternityClient.Tests/ConsoleTrainingCommandTests.cs`

### Implementation for User Story 3

- [ ] T043 [US3] Implement exact directed link creation/update/removal and retained retired history in `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs` and `BookOfEternityClient/Services/MortalLocationIdentityState.cs`
- [ ] T044 [US3] Implement existing movement selection, narrow semantic updates, discovery transitions, immutable field checks, and current projection rebuild in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs`
- [ ] T045 [US3] Derive map nodes/edges and current exits only from accepted canonical locations/links with no name/slug/fallback layout authority in `BookOfEternityClient/Services/LocalMapViewService.cs`
- [ ] T046 [US3] Require exact canonical Mortal current location in `BookOfEternityClient/Services/LocalInteractionScopeService.cs`, `BookOfEternityClient/Services/ActorMemoryService.cs`, `BookOfEternityClient/Services/NpcTradeService.cs`, and training service callers without changing afterlife name semantics
- [ ] T047 [US3] Remove durable trust in `knownExits`, `adjacencyMap`, `knownLocations`, `newLocations`, `newLinks`, `locationUpdates`, name aliases, coordinate endpoints, and missing-z compatibility from `BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs`
- [ ] T048 [US3] Run topology/lifecycle/map/locality Focused filters and record evidence in `specs/1520-complete-location-materialization/quickstart.md`

**Checkpoint**: Existing locations and links evolve only through closed exact
operations, and every map/current exit is a derived directed relation.

---

## Phase 6: User Story 4 — Atomic Acceptance and Bounded Repair (Priority: P1)

**Goal**: Location, link, current projection, identity authority, and governed
references commit once or return byte-for-byte to validated pre-turn state; only
one exact GM-owned candidate receives a repair packet.

**Independent Test**: Inject raw and post-write failures for missing sections,
dangling endpoints, coordinate conflict, parent cycle, cross-entity mismatch,
write failure, and receipt/index mismatch. Assert exact rollback, no stale player
output, one bounded repair when safe, and pre-dispatch stop for protected or
ambiguous identity.

### Tests for User Story 4

- [ ] T049 [US4] Add red packet grouping, deterministic correction lists, raw target allowlist, cross-contract ownership, and protected-file exclusion tests in `BookOfEternityClient.Tests/MortalLocationRepairPacketBuilderTests.cs`
- [ ] T050 [US4] Add red fail-closed unknown/duplicate/replay/confusable/client-field/index/seal tests proving no GM request dispatch in `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs`
- [ ] T051 [US4] Add red before-image rollback tests for map/current/index/NPC/faction/item-storage/link writes and injected post-normalization failure in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationLifecycleTests.cs`
- [ ] T052 [US4] Add red repair-retry idempotence and stale player-output suppression tests in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationLifecycleTests.Output.cs`
- [ ] T053 [P] [US4] Add red validation-repair request serialization tests for exact location/link context and zero client-owned targets in `BookOfEternityClient.Tests/ValidationRepairRequestTests.cs`

### Implementation for User Story 4

- [ ] T054 [US4] Implement exact actor grouping, raw coordinates, missing/invalid/conflict lists, safe rules, companion ownership, and fail-closed classification in `BookOfEternityClient/Services/MortalLocationRepairPacketBuilder.cs`
- [ ] T055 [US4] Route location issue codes through the dedicated packet builder, remove legacy adjacency/name/coordinate packets, and stop protected/ambiguous cases before GM dispatch in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T056 [US4] Extend `AcceptedTurnCanonicalStateRefresh` to share one lease/before-image contour and combined Mortal item/location post-check, restoring every tracked path byte-for-byte on any failure in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- [ ] T057 [US4] Ensure transient location/link commands and bootstrap reservations settle only after post-validation and survive correctly across retry/rollback in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- [ ] T058 [US4] Map console/browser lifecycle failures to player-safe Russian text while retaining operator diagnostics in `BookOfEternityClient/UI/` and `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs`
- [ ] T059 [US4] Run packet/lifecycle/rollback Focused filters, including injected failures and repeated repair, and record exact result directories in `specs/1520-complete-location-materialization/quickstart.md`

**Checkpoint**: No malformed or failed retry can leave a partial map, link,
current projection, identity entry, companion binding, or player confirmation.

---

## Phase 7: User Story 5 — Discovery-Safe Player Views (Priority: P2)

**Goal**: Console and browser show equivalent accepted location semantics and
actions for each discovery tier while hiding rejected state and every internal
authority surface.

**Independent Test**: Populate hidden, rumored, discovered, visited, current,
receipt-less, and malformed candidates plus nested internal DTOs. Compare map,
list, detail, current, news, locality, and actions across both clients.

### Tests for User Story 5

- [ ] T060 [US5] Add red canonical acceptance, discovery-tier, recursive DTO-shape sanitization, exact action binding, and semantic preservation tests in `BookOfEternityClient.Tests/MortalLocationPlayerProjectionTests.cs`
- [ ] T061 [US5] Add red console map/list/detail/current/ASCII/storage privacy and semantic parity tests in `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GeneralPanels.cs`
- [ ] T062 [P] [US5] Add red browser map/list/detail/current/storage DTO and action privacy tests in `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`
- [ ] T063 [P] [US5] Add red Mortal news/threat hidden/rejected/exact-location projection tests in `BookOfEternityClient.Tests/ExplorerMortalWorldNewsCommandResultBuilderTests.cs`

### Implementation for User Story 5

- [ ] T064 [US5] Implement canonical-only acceptance, discovery projection, recursive item/location authority filtering, full internal DTO suppression, and player-safe action records in `BookOfEternityClient/Services/MortalLocationPlayerProjection.cs`
- [ ] T065 [US5] Rebuild console locations, current detail, ASCII map, visible exits, and storage counts from the shared projection in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaLoreAndTravel.cs` and `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs`
- [ ] T066 [US5] Rebuild browser current/location/map/detail/storage results from the shared projection and exact non-rendered selectors in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`
- [ ] T067 [US5] Make Mortal news/threat location joins canonical, exact, and discovery-safe in `BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs`
- [ ] T068 [US5] If C# DTO tests prove the React client needs a new field or guard, update and test the applicable files under `BookOfEternityClient.WebFrontend/src/`; otherwise record the no-frontend-source-change rationale in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T069 [US5] Run all player projection/console/browser/news Focused filters and inspect one hidden, rumored, and visited payload; record evidence in `specs/1520-complete-location-materialization/quickstart.md`

**Checkpoint**: Rejected/hidden locations have zero player footprint; rumors are
safe; discovered/visited/current semantics and actions agree across clients.

---

## Phase 8: User Story 6 — Coherent Cross-Entity References (Priority: P2)

**Goal**: Actor, faction, lore, threat, storage, item, movement, and location
surfaces resolve one exact accepted Mortal place without names, aliases, realm
leaks, or contradictory physical authority.

**Independent Test**: Create a current location with same-turn actor/faction,
exact lore/threat references, storage metadata, and one #1511 item. Accept the
coherent turn, then individually introduce name-only, wrong-case, Unicode,
dangling, cross-realm, duplicate, and physical-placement conflicts.

### Tests for User Story 6

- [ ] T070 [US6] Add red actor binding/current physical location and same-turn temporary-reference tests in `BookOfEternityClient.IntegrationTests/ActorMaterializationValidationTests.cs` and `BookOfEternityClient.IntegrationTests/NpcCoreChangesTests.cs`
- [ ] T071 [P] [US6] Add red faction control, effective same-turn faction identity, and contradictory location-control tests in `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
- [ ] T072 [P] [US6] Add red exact codex/quest/world-event/threat/storage reference and cross-realm tests in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Companions.cs`
- [ ] T073 [US6] Add red same-turn current-location storage item route, remote-map-content rejection, and independent item-repair ownership tests in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Routes.cs`
- [ ] T074 [P] [US6] Add red exact canonical authority tests for NPC/faction readers and current-location scope in `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Authority.cs`

### Implementation for User Story 6

- [ ] T075 [US6] Replace recursive current/map actor location authority with exact canonical map plus accepted same-turn plan in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs` and `BookOfEternityClient/Services/NpcCoreChangesContract.cs`
- [ ] T076 [US6] Replace recursive/case-insensitive faction location authority with exact canonical map plus accepted same-turn plan in `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs`
- [ ] T077 [US6] Implement field-aware parent/actor/faction/lore/threat/storage/current-selection rewrites and composed physical-authority checks in `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs` and `BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs`
- [ ] T078 [US6] Preserve current storage contents during location projection, expose exact accepted same-turn storage authority, and keep item identity/repair ownership in `BookOfEternityClient/Services/MortalItemRouteAuthorityCatalog.cs`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`, and `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- [ ] T079 [US6] Replace broad location enumeration/case-insensitive dictionaries in cross-reference validation with receipt-bearing canonical exact indexes in `BookOfEternityClient/Services/Validation/ValidationService.InventoryNpcWorldCrossRefs.cs` and `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- [ ] T080 [US6] Run actor/faction/lore/threat/storage/item authority Focused filters and record evidence in `specs/1520-complete-location-materialization/quickstart.md`

**Checkpoint**: Every governed companion points to one coherent accepted Mortal
location, and same-turn current storage composes with #1511 without sharing
identity or repair ownership.

---

## Phase 9: Current-Schema Migration and GM Contract Synchronization

**Purpose**: Migrate repository-owned state and teach the GM the enforceable
Mortal location workflow. This phase crosses all six stories and is mandatory.

- [ ] T081 Migrate active `FileSystemExample/game_session/game_state/world/current_location.json`, add canonical `world_map.json` and `location_identity_index.json`, and update integrity assertions in `BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs`
- [ ] T082 Migrate every positive validator fixture and generated test helper from T004, isolate receipt-less objects as explicitly named negative inputs, and record each disposition in `specs/1520-complete-location-materialization/fixture-migration-inventory.md`
- [ ] T083 Update complete location/link envelopes, exact routes, derived topology, existing movement/update rules, bootstrap, no-legacy policy, and repair boundaries in `Rules/Block_20.txt`
- [ ] T084 Add the three complete worked GM examples—bootstrap start/neighbor/link, hidden remote/reveal, and invalid bounded repair—to `Examples/E_Block_20.txt`
- [ ] T085 [P] Update canonical map/current/index ownership, raw file mapping, exact existing movement, and client-field restrictions in `CLI_API_Specification.md` and `CLI_Agent_Daemon_Specification.md`
- [ ] T086 [P] Update main GM authoring/repair workflow in `TaskGuides/CLI_Step_Main.txt`, `Examples/E_CLI_Step_Main.txt`, and `Rules/Block_CLI_Operations.txt`
- [ ] T087 Update the daemon/launcher reminder so the GM reads and follows the current location contract in `BookOfEternityClient/game_master_daemon.ps1`
- [ ] T088 Audit `Rules/Block_18.A.txt`, `Rules/Block_19.txt`, `Rules/Block_21.txt`, `Examples/E_Block_11.B.txt`, launcher guidance, and translation guidance; edit only genuine Mortal location contract duplicates and record untouched rationale in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T089 Register all changed/new valid and invalid examples with exact expected diagnostics in `Examples/example_validation_manifest.json`
- [ ] T090 Add red-then-green contract/source-guard assertions for routes, required fields, derived links, client ownership, no name/legacy fallback, bootstrap, and worked examples in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- [ ] T091 Add red-then-green executable example/manifest validation for both creation routes, bootstrap, topology, discovery, repair, and receipt-less rejection in `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [ ] T092 Check `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, afterlife manifests/guards, and runtime callers; record the no-update rationale in `specs/1520-complete-location-materialization/quickstart.md` unless implementation actually changed an afterlife contract owned by #1514

---

## Phase 10: Verification, Hygiene, and Integration Readiness

**Purpose**: Prove the complete candidate and reconcile durable task state before
requesting review or merge.

- [ ] T093 Run `git diff --check`, audit tracked/untracked artifacts, and remove only feature-owned generated outputs while preserving unrelated `bin/obj` directories
- [ ] T094 Run every bounded Focused command group from `specs/1520-complete-location-materialization/plan.md`, diagnose with the smallest related filter, and record exact result directories in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T095 Run one `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` checkpoint after runtime/projection integration and record the result directory in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T096 Run `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` for the changed rules/examples/manifest and record the result directory in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T097 Run `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration` for bootstrap/normalization/repair/rollback and record the result directory in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T098 Apply the `requesting-code-review` checklist to the complete diff, resolve every Critical/Important finding with red-green evidence, and record disposition in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T099 Reconcile all 60 FRs, 12 SCs, task checkboxes, fixture migration rows, GM docs/examples, prompt/source guards, and the explicit afterlife no-update rationale in `specs/1520-complete-location-materialization/tasks.md`
- [ ] T100 Run exactly one final `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` on the candidate commit, without a duplicate Fast immediately before it, and record the result directory in `specs/1520-complete-location-materialization/quickstart.md`
- [ ] T101 Inspect the final diff and status, commit only #1513-owned files, create the PR with issue/spec/test evidence, merge only the tested commit after approval, and close #1513 only after merge

---

## Dependencies & Execution Order

### Phase dependencies

```text
Setup
  -> Foundational test infrastructure
     -> US1 complete creation (core authority)
        -> US2 bootstrap
        -> US3 topology/lifecycle
           -> US4 atomic repair (tracks the complete declared companion contour)
              -> US5 player projection (requires stable accepted authority)
                 -> US6 cross-entity references (uses the settled transaction boundary)
                    -> migration/docs
                       -> final verification/integration
```

US2 and US3 may proceed independently after the US1 foundation. US4 declares
and tests the complete companion write contour before US5 consumes canonical
state. US6 then fills that already-atomic contour with exact cross-entity
resolution without changing repair ownership. Red tests for US5 and US6 may be
authored earlier in separate files, but production phases complete in the order
shown above.

### Within each story

1. Write the named tests and confirm the intended failure.
2. Implement the minimum current-schema behavior.
3. Run the smallest focused filter green.
4. Refactor only under green coverage.
5. Run the story's integration filter before its checkpoint.

## Parallel Opportunities

- T004 and T005 can inventory fixtures and readers independently.
- T007 and T009 touch separate test-support files after T006 establishes shapes.
- T016 and T017 are independent fast-test files after the contract vocabulary exists.
- US2 snapshot tests (T032) can run alongside bootstrap contract tests.
- US3 map-service tests (T041) and locality tests (T042) can be authored in parallel.
- US5 console, browser, and news tests (T061–T063) use separate files.
- US6 actor, faction, companion, and authority tests (T070–T074) split across independent fixtures/files.
- Documentation tasks T085–T088 can proceed in separate files after runtime schema and Rule 20 are stable.

## Parallel Example: User Story 6

```text
Task T070: actor binding/current physical location regressions
Task T071: faction control/effective identity regressions
Task T072: lore/threat/storage exact-reference regressions
Task T074: canonical authority reader regressions

Join at T077/T079 after all red tests establish the composed-state contract.
```

## Implementation Strategy

### MVP first

1. Complete setup and foundational helpers.
2. Complete US1 with empty-by-design companions and isolated or one simple link.
3. Stop and run the US1 independent test matrix.
4. Do not merge the MVP alone: #1513 acceptance still requires bootstrap,
   topology, atomic repair, projection, cross-entity composition, docs, and final
   controls.

### Incremental delivery inside the feature branch

1. US1 establishes current-schema location/link identity.
2. US2 moves fresh game bootstrap onto the ordinary route.
3. US3 establishes exact later lifecycle and directed topology.
4. US4 closes the declared transaction/repair boundary for every governed path.
5. US5 moves every player reader to the stable accepted projection.
6. US6 composes existing actor/faction/item/lore/threat/storage authority inside that boundary.
7. Migrate active state and synchronize GM documentation.
8. Review and run the bounded final controls.

## Notes

- Do not implement #1514 afterlife locations, #1515 transport/storage entity
  materialization, living-world execution, GM workers, network multiplayer, or a
  visual map redesign in this branch.
- Do not add runtime compatibility, promotion classifiers, name identity,
  coordinate identity, or inferred reverse links to satisfy old fixtures.
- Do not mark a task complete from an agent report; inspect the diff and fresh
  verification evidence first.
