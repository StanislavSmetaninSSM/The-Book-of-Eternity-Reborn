# Tasks: Complete Mortal Item Materialization

**Input**: Design documents from `/specs/1511-complete-item-materialization/`
**Source issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)

**Prerequisites**: `spec.md`, `plan.md`, `research.md`,
`data-model.md`, `contracts/`, and `quickstart.md`

**Tests**: Every behavior change follows red-green-refactor. Use PowerShell 7
and `scripts/test-csharp.ps1`; do not use an unbounded full-suite
`dotnet test`.

**Organization**: Tasks are grouped by the five approved user stories. All
current-schema work rejects receipt-less state; no compatibility/promotion task
is allowed.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can proceed independently in a different file after its phase
  prerequisites are complete.
- **[Story]**: Maps directly to the approved story in `spec.md`.
- Every task names its primary file path.

## Phase 1: Setup and Baseline

**Purpose**: Reconfirm the tracked branch, isolate user-owned artifacts, and
capture a reproducible pre-change baseline.

- [x] T001 Confirm issue #1511 is open/in-progress, branch `1511-complete-item-materialization` is based on `fa1e85276661717ae805c5ff6f0460c438892f25`, and only the known `.serena/` and `bin/obj` paths are untracked in `E:/Games/worktrees/boe-1510-design`
- [x] T002 Re-read `AGENTS.md`, `.specify/memory/constitution.md`, and all files under `specs/1511-complete-item-materialization/`; record any implementation-impacting drift in `specs/1511-complete-item-materialization/research.md`
- [x] T003 [P] Inventory active positive, negative, and fragment-only item fixtures with exact migration disposition in `specs/1511-complete-item-materialization/fixture-migration-inventory.md`
- [x] T004 Run clean baseline Focused controls for `CanonicalStateNormalizerTests.Inventory` and `BrowserInventoryManagementTests` through `scripts/test-csharp.ps1` and record result directories in `specs/1511-complete-item-materialization/quickstart.md`

---

## Phase 2: Foundational Test Infrastructure

**Purpose**: Provide one current-schema fixture vocabulary and snapshot harness
before any production behavior changes.

**Critical**: User-story implementation does not begin until these helpers
compile and existing focused tests remain green.

- [x] T005 Create a dependency-free complete item/envelope/receipt/index fixture builder with deterministic IDs in `BookOfEternityClient.TestSupport/MortalItemTestFixture.cs`
- [x] T006 [P] Create file-backed player/NPC/storage/vehicle carrier and validated pending-snapshot setup helpers in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.cs`
- [x] T007 Add fixture-builder shape tests and explicit fragment-only labeling tests in `BookOfEternityClient.Tests/MortalItemTestFixtureTests.cs`, then run their Focused filter green without production changes

**Checkpoint**: Current-schema tests can express complete roots, split-derived
items, retired entries, every carrier, and intentionally malformed receipt-less
objects without duplicating large JSON literals.

---

## Phase 3: User Story 1 — Complete First Creation (Priority: P1) 🎯 MVP

**Goal**: Every supported ordinary Mortal item creation route accepts only a
complete GM envelope/package and becomes one permanent, receipt-bearing,
indexed canonical item.

**Independent Test**: Run all eight route rows with one complete simple item,
then remove the same required section from each row. Complete rows seal exactly
one item; malformed rows leave carrier, companion, reward, and index state
unchanged. Empty Mortal bootstrap remains valid.

### Tests for User Story 1

- [x] T008 [US1] Add red exact-schema tests for envelope fields, section dispositions, physical empty shapes, duplicate properties, wrong realm, GM-authored client fields, and immutable receipt shape in `BookOfEternityClient.Tests/MortalItemMaterializationContractTests.cs`
- [x] T009 [US1] Add red exact-identity and one-pass catalog tests for player, NPC, location-storage, and vehicle carriers, including case/whitespace/Unicode ambiguity and the 2.5x work bound, in `BookOfEternityClient.Tests/MortalItemCarrierCatalogTests.cs`
- [x] T010 [US1] Add red raw-to-sealed player `UpdateInventory`, receipt-less canonical rejection, and empty-bootstrap integration tests in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.cs`
- [x] T011 [US1] Add red Theory rows for existing-NPC add, new-NPC inventory, loot template, craft output, trade output, quest reward, and existing-storage placement in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Routes.cs`
- [x] T012 [US1] Add red same-turn `creationRef` resolution tests for equipment, parent container path, item text, journal, bond, recipe, quest reward, and storage references in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Companions.cs`
- [x] T013 [P] [US1] Add red bootstrap index-shape assertions in `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs` and create `BookOfEternityClient.IntegrationTests/PendingTurnSnapshotTests.cs` with red snapshot-registration coverage for the item index and governed carriers
- [x] T014 [P] [US1] Add red validation-phase selection/order/equivalence coverage for `AcceptedTurnItemMaterializationCompleteness` in `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`

### Implementation for User Story 1

- [x] T015 [US1] Implement exact envelope, disposition, complete-field, receipt, seal, and identity validation primitives in `BookOfEternityClient/Services/MortalItemMaterializationContract.cs`
- [x] T016 [US1] Implement client-owned index parsing, deterministic serialization, receipt sealing, entry/transition invariants, and protected-field comparisons in `BookOfEternityClient/Services/MortalItemIdentityState.cs`
- [x] T017 [US1] Implement one-pass ordinal carrier/receipt/materialization/creation-ref indexes and scan metrics in `BookOfEternityClient/Services/MortalItemCarrierCatalog.cs`
- [x] T018 [US1] Implement exact route-authority derivation for turn, NPC add/new NPC, loot-template ordinal, craft request, NPC trade receipt, quest reward, and location storage in `BookOfEternityClient/Services/MortalItemRouteAuthorityCatalog.cs`
- [x] T019 [US1] Add raw pre-seal and canonical post-seal validation, receipt-less rejection, companion reconciliation, route binding, single-carrier checks, and bounded issue metadata in `BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs`; register `game_state/inventory/item_identity_index.json` as a client-owned surface in `BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs` and `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- [x] T020 [US1] Register the new validation phase and public/raw entrypoints in `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`, `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`, `BookOfEternityClient/Services/ValidationService.cs`, and `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [x] T021 [US1] Materialize player `UpdateInventory` into canonical `items[]`, assign exact permanent IDs, remove temporary aliases, resolve player companion references, seal receipts, and update the index in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- [x] T022 [US1] Materialize new/existing NPC item commands and rewrite NPC equipment/container references without changing unrelated actor semantics in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs`
- [x] T023 [US1] Reconcile item resources, texts, journals, bonds/Fate Cards, recipes, quest links, and exact permanent IDs in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs`
- [x] T024 [US1] Register `item_identity_index.json` and governed carrier inputs in normalizer backup/rollback/snapshot tracking, make `RefreshCanonicalStateAsync` acquire one `CanonicalWriteLease` and bind the normalizer to it, capture exact before-images for every touched carrier/companion/index path, restore them byte-for-byte on normalization or post-seal validation failure, preserve the same tracked contour for QTE/browser callers, and create the empty bootstrap index in `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`, `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`, and `BookOfEternityClient/Services/MortalBootstrapStateBuilder.cs`
- [x] T025 [US1] Tighten complete item validation to the current exact identity aliases, governed fields, `contentsPath` item-ID chains, and receipt-bearing canonical shape in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs` and `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- [x] T026 [US1] Bind quest item rewards and route outcomes to exact accepted item/index authority in `BookOfEternityClient/Services/QuestRewardAuthority.cs` and `BookOfEternityClient/Services/Validation/ValidationService.QuestRewardAuthority.cs`
- [x] T027 [US1] Run the US1 fast and integration Focused filters from `plan.md`; keep the red/green commands and result directories in `specs/1511-complete-item-materialization/quickstart.md`

**Checkpoint**: Each creation route independently produces one complete,
receipt-bearing item and index entry; malformed or receipt-less state fails
without legacy promotion.

---

## Phase 4: User Story 2 — Identity-Preserving Transfers (Priority: P1)

**Goal**: Existing physical items move between supported carriers without
recreation, duplication, receipt rewrite, companion loss, or name-based
ambiguity.

**Independent Test**: Move one item player → NPC → player → storage → player →
vehicle → player. Its permanent ID, envelope, and receipt never change; every
step has one active carrier and one appended client transition. Duplicate or
forged transitions fail atomically.

### Tests for User Story 2

- [ ] T028 [US2] Add red transfer-continuity, duplicate-carrier, immutable-envelope/receipt, retired-ID reuse, and exact-name-collision tests in `BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.cs`
- [ ] T029 [US2] Add red player/NPC GM-command transfer and companion-retention integration tests in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Transfers.cs`
- [ ] T030 [P] [US2] Add red atomic player/storage/vehicle move, write-failure rollback, and index-carrier tests in `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs`
- [ ] T031 [P] [US2] Add red NPC buy/sell/buyback identity-preservation and receipt immutability tests in `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs` and `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`

### Implementation for User Story 2

- [ ] T032 [US2] Implement operation-intent-based coordinated carrier/index before-image validation, conditional writes, and exact rollback in `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- [ ] T033 [US2] Route player/location-storage and player/vehicle moves through the transition writer while preserving the same JSON item/receipt in `BookOfEternityClient/Services/StorageTransportMoveService.cs`
- [ ] T034 [US2] Preserve item identity across NPC buy, sell, buyback, and existing-item handoff while keeping merchant stock settlement atomic in `BookOfEternityClient/Services/NpcTradeService.cs` and `BookOfEternityClient/Services/NpcTradeRequestState.cs`
- [ ] T035 [US2] Classify accepted GM inventory remove/add/move surfaces as one exact transfer and reject remove-plus-recreate behavior in `BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- [ ] T036 [US2] Move or clear equipment, text/journal, bond, recipe, quest, ownership, and container-path companions according to their authority in `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- [ ] T037 [US2] Include `item_identity_index.json` in browser local-write tracked paths and preserve session-lock ordering in `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs`
- [ ] T038 [US2] Run the US2 Focused filters and verify exact pre/post bytes on injected write failure; record result directories in `specs/1511-complete-item-materialization/quickstart.md`

**Checkpoint**: Existing items have stable physical identity across every
supported transfer; storage/vehicle entity completeness remains outside #1511.

---

## Phase 5: User Story 3 — Stack Quantity and Provenance (Priority: P1)

**Goal**: Split and merge operations conserve quantity, use unique instance
receipts, and retain every origin through active and retired entries.

**Independent Test**: Split a ten-unit stack into seven/three, transfer the
child, return it, merge compatible stacks from two roots, and reject an
incompatible merge. Quantities, receipts, origins, survivor, and retired
contributors match the contract exactly.

### Tests for User Story 3

- [ ] T039 [US3] Add red split-derived receipt, parent/origin lineage, quantity conservation, and rollback tests in `BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.Stacks.cs`
- [ ] T040 [US3] Add red deterministic-survivor, origin-union, contributor-retirement, and complete semantic compatibility tests in `BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.Stacks.cs`
- [ ] T041 [P] [US3] Migrate browser split/merge fixtures to current schema and add receipt/index assertions plus incompatible readable/sentient/bonded/quest/equipped/container cases in `BookOfEternityClient.Tests/WebUi/BrowserInventoryManagementTests.cs`
- [ ] T042 [P] [US3] Add console/browser local discard tests proving `destroyed` retirement, cleared equipment, and no ground-loot carrier in `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs`

### Implementation for User Story 3

- [ ] T043 [US3] Replace clone-and-ID split with a transition-writer split that seals one derived child receipt and records exact lineage in `BookOfEternityClient/Services/InventoryManagementService.cs`
- [ ] T044 [US3] Replace raw-JSON merge signatures with governed semantic compatibility, selected-ID survivor, origin union, and contributor retirement in `BookOfEternityClient/Services/InventoryManagementService.cs`
- [ ] T045 [US3] Route full local discard through a `destroy` transition and preserve partial/full consumption distinction in `BookOfEternityClient/Services/InventoryManagementService.cs`
- [ ] T046 [US3] Remove duplicate UI-side split/merge signature assumptions and use the shared service outcome/identity contract in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs` and `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs`
- [ ] T047 [US3] Run the US3 Focused filters for fast service/browser tests and integration console parity; record result directories in `specs/1511-complete-item-materialization/quickstart.md`

**Checkpoint**: No tested stack operation creates/erases quantity or loses
origin lineage; incompatible stacks remain unchanged.

---

## Phase 6: User Story 4 — Narrow Atomic Repair (Priority: P2)

**Goal**: Malformed item creation produces one exact bounded repair packet and
retains rollback/idempotence authority without replaying rewards or exposing
internal state.

**Independent Test**: Fail one route at a time for missing section, orphan
companion, route mismatch, duplicate identity, and forged client field. Each
packet targets only the exact item and GM-owned files; repeated repair creates
one item; failed repair restores exact pre-turn bytes.

### Tests for User Story 4

- [ ] T048 [US4] Add red packet grouping, target allowlist, exact corrections, companion targets, expected/actual authority, and protected-file exclusion tests proving `item_identity_index.json` never appears in GM repair targets or mapped GM-authorable surfaces in `BookOfEternityClient.Tests/MortalItemRepairPacketBuilderTests.cs` and `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- [ ] T049 [US4] Add red accepted-turn repair-loop, snapshot retention, exact rollback, and Theory-matrix same-request idempotence tests for craft, trade, quest reward, loot acquisition, transfer, split, and merge in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationLifecycleTests.cs`; assert one settlement, conserved quantities, and no duplicate item/receipt/index transition on replay
- [ ] T050 [P] [US4] Add red stale player-facing output tests proving no acquisition confirmation survives canonical repair/rollback in `BookOfEternityClient.IntegrationTests/MortalItemMaterializationLifecycleTests.Output.cs`

### Implementation for User Story 4

- [ ] T051 [US4] Implement item-coordinate grouping, minimal GM-owned targets, exact corrections, companion lists, protected-field instructions, and an explicit exclusion for `game_state/inventory/item_identity_index.json` from GM targets/mappings in `BookOfEternityClient/Services/MortalItemRepairPacketBuilder.cs`, `BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- [ ] T052 [US4] Integrate item packets into canonical repair selection/freshness handling without broad faction/actor fallback behavior in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T053 [US4] Keep one bound `CanonicalWriteLease` plus exact snapshot/index/carrier/companion/route/output before-images through post-seal success, restore every tracked path byte-for-byte on normalizer or validation failure, and clean item command surfaces only after acceptance in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T054 [US4] Run the US4 Focused fast packet and integration lifecycle filters, including injected repair repetition and write failure, and record evidence in `specs/1511-complete-item-materialization/quickstart.md`

**Checkpoint**: Item repair is exact, replay-safe, rollback-backed, and cannot
delegate client-owned identity authoring to the GM.

---

## Phase 7: User Story 5 — In-World Console and Browser Views (Priority: P2)

**Goal**: Accepted semantic item detail remains visible in both clients while
envelope, receipt, index, path, and repair internals remain hidden.

**Independent Test**: Open the simple and mechanic-bearing accepted fixtures in
console and browser details. Both show equivalent Russian semantic information
and zero internal authority tokens; rejected items never appear.

### Tests for User Story 5

- [ ] T055 [US5] Add red console detail/privacy tests for `materialization`, receipt, seal, lineage, carrier, path, and repair tokens in `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs`
- [ ] T056 [P] [US5] Add red browser result/payload/privacy tests for the same accepted/rejected items in `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`
- [ ] T057 [P] [US5] Add red browser local-action prompt parity tests using receipt-bearing fixtures in `BookOfEternityClient.Tests/WebUi/BrowserInventoryManagementTests.cs` and `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs`

### Implementation for User Story 5

- [ ] T058 [US5] Add `materialization`, `materializationReceipt`, `creationRef`, and all client authority fields to the console internal-field denylist while preserving semantic catch-all behavior in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs` and NPC inventory rendering partials
- [ ] T059 [US5] Project browser inventory data through an explicit semantic allowlist and omit identity-index state from player DTOs in the applicable files under `BookOfEternityClient/WebUi/`
- [ ] T060 [US5] If and only if C# projection tests prove frontend source receives internal fields, add a typed semantic projection/denylist and tests under `BookOfEternityClient.WebFrontend/src/`; otherwise record the no-frontend-change rationale in `specs/1511-complete-item-materialization/quickstart.md`
- [ ] T061 [US5] Run the US5 console/browser Focused filters and inspect one simple plus one mechanic-bearing result payload; record absence of internal terms in `specs/1511-complete-item-materialization/quickstart.md`

**Checkpoint**: Harness evidence is invisible to ordinary players while
accepted item semantics remain inspectable and console/browser behavior agrees.

---

## Phase 8: Current-Schema Migration and GM Contract Synchronization

**Purpose**: Migrate repository-owned positive state and teach the GM the exact
workflow. This phase crosses all stories and cannot be deferred.

- [ ] T062 Migrate the active sword and companion journals, add the matching identity index, and preserve fixture integrity in `FileSystemExample/game_session/game_state/inventory/items.json`, `FileSystemExample/game_session/game_state/npcs/item_journals.json`, and `FileSystemExample/game_session/game_state/inventory/item_identity_index.json`
- [ ] T063 Migrate shared positive item builders/fixtures identified by T003, keep receipt-less objects only in explicitly named negative inputs, and enforce the classification in `BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs` and `BookOfEternityClient.Tests/MortalItemTestFixtureTests.cs`
- [ ] T064 Update new-item, exact-ID, container-path, loot-template, transfer, split/merge, craft, storage, and no-legacy rules in `Rules/Block_2.txt`, `Rules/Block_5.txt`, `Rules/Block_9.txt`, `Rules/Block_10.txt`, `Rules/Block_11.txt`, `Rules/Block_19.A.txt`, and `Rules/Block_20.txt`
- [ ] T065 [P] Update canonical file mapping guidance, client-owned index ownership, repair restrictions, and current-schema lifecycle in `Rules/Block_CLI_Operations.txt`, `CLI_API_Specification.md`, and `CLI_Agent_Daemon_Specification.md`
- [ ] T066 [P] Update GM authoring/repair steps and daemon-read requirements in `TaskGuides/CLI_Step_Main.txt`, `Examples/E_CLI_Step_Main.txt`, and the repository daemon PowerShell entrypoint
- [ ] T067 Add complete mundane player-acquisition and mechanic-bearing craft/trade worked examples, transfer/storage/lineage notes, and explicit receipt-less rejection in `Examples/E_Block_9.txt`, `Examples/E_Block_10.txt`, `Examples/E_Block_11.txt`, `Examples/E_Block_19.A.txt`, `Examples/E_Block_20.txt`, and a new `Examples/E_CLI_Mortal_Item_Materialization.txt`
- [ ] T068 Register every new/changed example and expected diagnostic in `Examples/example_validation_manifest.json`
- [ ] T069 Add red-then-green GM contract/source-guard assertions for required fields, routes, client ownership, no legacy promotion, and worked examples in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- [ ] T070 Add red-then-green executable example/manifest validation for valid routes and receipt-less rejection in `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [ ] T071 Check `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, afterlife source guards, and realm-segregation tests; record in `specs/1511-complete-item-materialization/quickstart.md` that no afterlife contract changed, or update them only if an actual shared boundary changed

---

## Phase 9: Verification, Hygiene, and Integration

**Purpose**: Prove the complete candidate, reconcile durable task state, and
integrate only the tested commit.

- [ ] T072 Run `git diff --check`, audit tracked/untracked artifacts, and remove only feature-owned generated outputs while preserving the pre-existing `.serena/` and `bin/obj` directories
- [ ] T073 Run all four Focused command groups from `specs/1511-complete-item-materialization/plan.md`; diagnose failures with the smallest relevant filter and record exact result directories in `specs/1511-complete-item-materialization/quickstart.md`
- [ ] T074 Run one Fast checkpoint through `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` and record total/pass/fail/timeout/cleanup evidence in `specs/1511-complete-item-materialization/quickstart.md`
- [ ] T075 Run documentation-sensitive `FullValidation` and accepted-turn `LifecycleIntegration` once, diagnose only related failures, and record result evidence in `specs/1511-complete-item-materialization/quickstart.md`
- [ ] T076 Perform the manual console/browser semantic/privacy check from `quickstart.md`; if frontend source changed, also run its repository-defined verification and record the result in `specs/1511-complete-item-materialization/quickstart.md`
- [ ] T077 Reconcile every FR/SC and every task checkbox against inspected diffs and fresh evidence in `specs/1511-complete-item-materialization/tasks.md`; do not mark report-only work complete
- [ ] T078 Request and apply code review for contract authority, exact identity, atomicity, linear scans, repair targets, route coverage, docs synchronization, and player privacy; record resolved findings in `specs/1511-complete-item-materialization/quickstart.md`
- [ ] T079 Create a clean-checkout candidate from the exact final commit and run one `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge`; require zero failures, duplicate IDs, timeout, and cleanup errors
- [ ] T080 Push `1511-complete-item-materialization`, open the #1511-linked PR with Mortal/afterlife docs rationale and exact verification evidence, merge only the reviewed PreMerge-tested commit to `main`, then close #1511 and update its roadmap status

---

## Dependencies and Execution Order

### Phase dependencies

1. Setup (Phase 1) has no dependency.
2. Foundational test infrastructure (Phase 2) depends on Setup and blocks all
   production work.
3. US1 (Phase 3) depends on Foundation and establishes the shared
   contract/index/normalizer.
4. US2 (Phase 4) depends on US1 permanent identity and index authority.
5. US3 (Phase 5) depends on US1 identity authority and the US2 coordinated
   transition writer.
6. US4 (Phase 6) depends on canonical US1–US3 issue metadata and transitions.
7. US5 (Phase 7) depends on accepted semantic fixtures from US1 and local
   operations from US2/US3.
8. Contract migration/docs (Phase 8) depends on stable runtime field names and
   behavior from US1–US5.
9. Verification/integration (Phase 9) depends on every selected story and
   documentation task.

### User-story dependency graph

```text
Foundation
    |
    v
US1 Complete creation
    |
    v
US2 Transfers -----> US3 Stack lineage
    |                    |
    +---------+----------+
              v
         US4 Repair
              |
              v
         US5 Projection
              |
              v
       Docs / Final gates
```

The stories remain independently testable at their checkpoints, but the
implementation is intentionally sequential because later stories consume the
same protected identity contract.

## Parallel Opportunities

- T003 and the baseline preparation around T004 can proceed independently.
- T005 and T006 touch different test projects; T007 joins them.
- In US1, T013 and T014 can be authored independently after core test fixtures;
  the main contract/route tests remain sequential to avoid competing test files.
- In US2, storage/vehicle tests T030 and NPC trade tests T031 are independent.
- In US3, browser stack tests T041 and console discard tests T042 are
  independent.
- In US4, stale-output coverage T050 is independent from packet-shape tests.
- In US5, browser payload T056 and local prompt parity T057 are independent.
- In Phase 8, CLI documentation T065 and task-guide/daemon updates T066 are
  independent after final field names are stable.

No subagent execution is assumed by this plan; `[P]` records file/dependency
independence for future staffing only.

## Parallel Example: User Story 2

```text
Task A: T030 — storage/vehicle transition rollback tests
Task B: T031 — NPC trade/buyback identity tests

Join: T032 transition writer, followed by T033/T034 implementation.
```

## Independent test criteria by story

- **US1**: Eight complete route rows seal one item; the same missing section
  rejects all rows without partial state; empty bootstrap remains valid.
- **US2**: One identity crosses player/NPC/storage/vehicle carriers with one
  active coordinate and unchanged receipt at every step.
- **US3**: Split/merge/discard conserve quantity and preserve or retire exact
  identities/origins as specified.
- **US4**: Each malformed package yields one exact GM-owned repair packet and
  replay-safe rollback.
- **US5**: Console/browser show the same semantic item and zero internal
  materialization authority.

## Implementation Strategy

### MVP

Complete Setup, Foundation, and US1, then run its independent focused controls.
This is the smallest coherent materialization boundary; it is not sufficient
to close #1511 because transfer, stack, repair, projection, and docs acceptance
criteria remain.

### Incremental delivery

1. Seal complete creations and index identity (US1).
2. Preserve identity through physical moves (US2).
3. Add conservative stack lineage and discard retirement (US3).
4. Add bounded repair/rollback (US4).
5. Prove player-facing privacy/parity (US5).
6. Migrate repository state and synchronize GM contracts.
7. Run final controls and integrate.

## Notes

- Mark a task complete only after inspecting its diff and fresh verification.
- Tests must be observed red before the corresponding production change.
- Use one logical commit per small task group; never stage unrelated untracked
  workspace content.
- Exact identity authority uses ordinal comparison even where display selection
  remains case-insensitive.
- There is no old-save compatibility, receipt-less promotion, or fallback
  reader task.
