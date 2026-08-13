# Fixture Migration Inventory: Complete Mortal Location Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513)
**Inventory date**: 2026-08-12

## Policy

The game has not shipped. A positive repository fixture is migrated to the
receipt-bearing current schema; it is never made valid through a compatibility
reader. A negative fixture starts from the same complete current-schema shape
and mutates only its named defect. Backup fixtures are migrated because they are
pre-turn authority in validation tests. Fragment-only fixtures remain fragments
only where the test intentionally validates a raw carrier or a non-location
contract and cannot be mistaken for accepted canonical state.

Discovery command:

```powershell
rg -l '"(currentLocationData|worldMapUpdates|newLocations|newLinks|knownExits|adjacencyMap|locationId|initialLocationId)"' FileSystemExample BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests Examples -g '*.json' -g '*.cs' -g '*.txt'
```

The command returned 51 files. The table below assigns every result a category
and disposition; later migrations update this table if a fixture changes role.

## Active and file-backed JSON fixtures

| Path | Category | Existing role | Required disposition |
|---|---|---|---|
| `FileSystemExample/game_session/game_state/world/current_location.json` | Active positive | Playable current projection. | **Migrated** to the exact receipt-bearing `loc_valmont_private_chambers` projection; sibling `world_map.json` and `location_identity_index.json` are canonical and coherent. |
| `FileSystemExample/validator_fixtures/current_location_known_partial/broken/current_location_data.json` | Raw pre-seal negative | Existing selection resends one persistent semantic field. | **Migrated** to exact `locationId` plus current operational fields and one named `mortal_location_materialization_existing_full_resend` defect. It is never read as canonical state. |
| `FileSystemExample/validator_fixtures/current_location_known_partial/fixed/current_location_data.json` | Raw pre-seal positive | Exact existing current selection. | **Migrated** to exact `locationId` plus current-only fields and validated against the shared sealed pre-turn map/current/index snapshot. |
| `FileSystemExample/validator_fixtures/current_location_missing_coordinates/broken/current_location_data.json` | Retired legacy raw negative | Required coordinates on legacy known-location shorthand. | **Removed**: the current exact-selection route does not accept coordinate authority, and the obsolete validator scenario contradicted the closed `ExistingCurrentFields` contract. |
| `FileSystemExample/validator_fixtures/current_location_missing_coordinates/fixed/current_location_data.json` | Retired legacy raw positive | Legacy known-location shorthand with coordinates. | **Removed** with its obsolete counterpart; exact selection/full-resend coverage replaces it. |
| `FileSystemExample/validator_fixtures/world_map_existing_link/broken/world_map.json` | Raw pre-seal negative | Link update with unresolved permanent identity. | **Migrated** from source+coordinate authority to one unknown exact `linkId`; expected code is `mortal_location_link_update_target_unresolved`. |
| `FileSystemExample/validator_fixtures/world_map_existing_link/fixed/world_map.json` | Raw pre-seal positive | Exact existing-link update. | **Migrated** to the accepted permanent `lnk_test_ford_to_tower`; canonical pre-turn authority comes from the shared sealed snapshot. |
| `FileSystemExample/validator_fixtures/world_map_existing_link/shared/world_map_backup.json` | Retired duplicate backup | Legacy adjacency/coordinate authority. | **Removed and consolidated** into `_shared/mortal_location/world_map_backup.json` plus the exact identity index. |
| `FileSystemExample/validator_fixtures/world_map_existing_storage/broken/world_map.json` | Raw pre-seal negative | Unknown exact storage within an accepted location. | **Migrated** to exact `loc_valmont_private_chambers`; only `storage_unknown` remains defective. |
| `FileSystemExample/validator_fixtures/world_map_existing_storage/fixed/world_map.json` | Raw pre-seal positive | Existing exact storage update. | **Migrated** to accepted location/storage IDs backed by the shared sealed snapshot. |
| `FileSystemExample/validator_fixtures/world_map_existing_storage/shared/world_map_backup.json` | Retired duplicate backup | Legacy partial storage authority. | **Removed and consolidated** into the shared complete canonical map/current/index snapshot. |
| `FileSystemExample/validator_fixtures/world_map_threat_completion_active/broken/world_map.json` | Raw pre-seal negative | Completion against an idle exact threat. | **Migrated** to accepted location/threat IDs; only the named missing-active-activity precondition remains defective. |
| `FileSystemExample/validator_fixtures/world_map_threat_completion_active/fixed/world_map.json` | Raw pre-seal positive | Completion against an active exact threat. | **Migrated** to the accepted active threat in the shared sealed snapshot. |
| `FileSystemExample/validator_fixtures/world_map_threat_completion_active/shared/world_map_backup.json` | Retired duplicate backup | Legacy partial threat authority. | **Removed and consolidated** into the shared complete canonical map/current/index snapshot. |
| `FileSystemExample/validator_fixtures/_shared/mortal_location/world_map_backup.json` | Shared backup positive | Exact pre-turn authority for link/storage/threat fixture routes. | **Added** as two complete sealed locations plus one sealed directed link; storage/threat/topology dispositions match their payloads. |
| `FileSystemExample/validator_fixtures/_shared/mortal_location/current_location_backup.json` | Shared backup positive | Exact selected-current pre-turn projection. | **Added** as the canonical map location plus current-only weather/interactions/chronology. |
| `FileSystemExample/validator_fixtures/_shared/mortal_location/location_identity_index_backup.json` | Shared client-owned positive | Exact pre-turn identity authority. | **Added** with two active location entries and one active link entry matching all receipts and creation coordinates. |
| `FileSystemExample/validator_fixtures/_shared/mortal_location/pending_turn_snapshot.json` | Shared control positive | Validated pre-turn binding for raw fixture commands. | **Added** so raw commands are tested against a coherent map/current/index baseline rather than treated as canonical state. |

## C# fixture and test sources

| Path | Category | Existing role | Required disposition |
|---|---|---|---|
| `BookOfEternityClient.IntegrationTests/ActorMaterializationValidationTests.cs` | Positive/negative generated | Actor-location authority scenarios. | Build canonical locations through `MortalLocationTestFixture`; mutate one exact actor reference for negatives. |
| `BookOfEternityClient.IntegrationTests/AfterlifeEntityProfileValidationTests.cs` | Fragment-only realm isolation | Uses location-shaped afterlife fragments. | Keep afterlife behavior unchanged; replace only any Mortal canonical positive embedded in the test. |
| `BookOfEternityClient.IntegrationTests/BrowserCommandPresentationAuditTests.cs` | Positive player projection | Browser location/map DTO audit. | Seed receipt-bearing canonical map/current/index and assert no raw/internal carriers. |
| `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs` | Documentation fixture runner | Executes JSON examples containing location routes. | Update expectations to the new examples/manifest; no compatibility branch. |
| `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GeneralPanels.cs` | Positive/negative console projection | Console location/map/detail panels. | Use accepted discovery-aware fixtures; add rejected/hidden negative variants. |
| `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs` | Positive/negative browser projection | Browser location/map/detail/actions. | Use accepted discovery-aware fixtures and exact IDs; raw/rejected objects remain negative only. |
| `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs` | Companion positive/negative | Faction territory references to locations. | Replace canonical location setup with exact receipt-bearing fixtures; retain faction defects only. |
| `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs` | Lifecycle positive/negative | Accepted-turn state distribution/normalization. | Use shared file-backed context and assert atomic map/current/index writes and rollback. |
| `BookOfEternityClient.IntegrationTests/GmTurnHelperContractTests.cs` | Raw fragment-only | Checks generated GM route contracts. | Keep raw fragments, but update required complete location/link request shapes. |
| `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs` | Bootstrap positive/negative | Fresh Mortal start and current legacy scaffold. | Replace with neutral scaffold, reserved refs, complete start/neighbor/link, and narrative-only exit cases. |
| `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Companions.cs` | Companion positive/negative | Location storage/item carrier setup. | Use receipt-bearing location/storage carriers and exact current/map IDs. |
| `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Routes.cs` | Raw/canonical route fixtures | Item routes through current/map locations. | Build locations through shared fixture; raw item route remains raw only. |
| `BookOfEternityClient.IntegrationTests/NpcCoreChangesTests.cs` | Companion positive/negative | NPC location references during canonical changes. | Seed exact active locations and preserve only NPC-change defects. |
| `BookOfEternityClient.Tests/ActorMaterializationContractTests.cs` | Contract fragments | Actor contract location fields. | Use exact canonical IDs; fragment status remains explicit. |
| `BookOfEternityClient.Tests/ActorMemoryServiceTests.cs` | Reader positive/negative | Current location and memory locality. | Migrate to canonical current projection and remove name/case fallback positives. |
| `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` | Fragment-only afterlife docs | Afterlife location words in documentation guards. | No change unless a shared contract surface is edited; #1514 owns afterlife location materialization. |
| `BookOfEternityClient.Tests/ConsoleNpcTradeCommandTests.cs` | Action positive/negative | Trade locality setup. | Seed exact accepted current/NPC location IDs; names are display-only. |
| `BookOfEternityClient.Tests/ConsoleTrainingCommandTests.cs` | Action positive/negative | Training locality setup. | Seed exact accepted current/NPC location IDs. |
| `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs` | Source/document guard | Guards legacy/current panel vocabulary. | Replace expected legacy reader tokens with canonical projection/authority guards. |
| `BookOfEternityClient.Tests/LiveTurnPreparationServiceTests.cs` | Lifecycle fragments | Raw turn preparation location carriers. | Update complete raw route shape and keep it explicitly pre-seal. |
| `BookOfEternityClient.Tests/LocalMapViewerServiceTests.cs` | Reader positive/negative | Local map layout/navigation fixtures. | Migrate to receipt-bearing locations/links, exact IDs, and discovery visibility. |
| `BookOfEternityClient.Tests/MortalItemCarrierCatalogTests.cs` | Companion route fragments | Location storage carrier discovery. | Use canonical location/storage fixtures; raw item carriers remain explicit. |
| `BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.cs` | Companion lifecycle | Item transfer across location carriers. | Use exact receipt-bearing location/storage carrier coordinates. |
| `BookOfEternityClient.Tests/MortalItemTestFixtureTests.cs` | Fixture self-test | Existing item fixture with location references. | Point location carriers at deterministic `MortalLocationTestFixture` identities. |
| `BookOfEternityClient.Tests/NpcTradeServiceRequestFlowTests.cs` | Action positive/negative | NPC trade location setup. | Use exact current/NPC location IDs and canonical projection. |
| `BookOfEternityClient.Tests/RivalSoulArcServiceTests.cs` | Story locality | Rival events tied to current location. | Seed canonical current projection and exact location references. |
| `BookOfEternityClient.Tests/TrainingServiceTests.cs` | Action positive/negative | Training locality checks. | Use exact active location IDs; remove name fallback positives. |
| `BookOfEternityClient.Tests/TrainingWebCommandServiceTests.cs` | Browser action | Training browser locality. | Use canonical current projection and exact location IDs. |
| `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs` | Companion action/projection | Location storage movement. | Use accepted canonical storages and assert rejected location/items do not appear. |
| `BookOfEternityClient.Tests/WebUi/BrowserTradeParityTests.cs` | Companion action/projection | Trade locality and item flow. | Use exact accepted location IDs and canonical current projection. |

### T082 generated-fixture reconciliation (2026-08-13)

- `BookOfEternityClient.TestSupport/MortalLocationTestFixture.cs` is the only
  generator of accepted Mortal location/link test state. Its raw builders are
  explicitly pre-seal, its canonical builders emit complete envelope,
  receipt/seal, map/current/index shapes, and
  `CreateReceiptlessNegative()` is visibly labelled
  `[INVALID FIXTURE: receiptless]`. Its seven fixture self-tests are green.
- Direct canonical/generated migrations are complete in
  `ActorMaterializationValidationTests.cs`,
  `ExplorerModeCommandTests.GeneralPanels.cs`,
  `ExplorerWebCommandServiceTests.cs`,
  `FactionMaterializationValidationTests.cs`,
  `GameEngineTurnLifecycleTests.cs`,
  `MortalBootstrapValidationTests.cs`,
  `MortalItemMaterializationTestContext.Companions.cs`,
  `MortalItemMaterializationTestContext.Routes.cs`,
  `NpcCoreChangesTests.cs`, `ExplorerModeSourceGuardTests.cs`, and
  `LocalMapViewerServiceTests.cs`. Positive state comes from the shared
  receipt-bearing fixture or a file-backed canonical context; negatives mutate
  one named field/reference.
- `BrowserCommandPresentationAuditTests.cs` consumes the active
  `FileSystemExample` and the tracked
  `mortal_world_command_display_fixture.zip`. T081 migrated both positive
  map/current/index inputs: the reusable archive now carries five accepted
  locations, four accepted links, one matching location identity index, and a
  current projection whose storage metadata agrees with the map while its two
  receipt-bearing items remain in current-only `contents`. A permanent archive
  integrity test validates every location/link receipt, current/map agreement,
  and index reconciliation. The four file-backed location validator
  definitions now run through the dedicated focused
  `MortalLocationMigratedFixture_BrokenAndFixedVariantsRespectCurrentContract`
  boundary; `ValidatorFixtureHarness` invokes the raw Mortal-location phase for
  `AcceptedTurn` fixtures.
- `ActorMaterializationContractTests.cs`, `ActorMemoryServiceTests.cs`,
  `ConsoleNpcTradeCommandTests.cs`, `ConsoleTrainingCommandTests.cs`,
  `MortalItemCarrierCatalogTests.cs`,
  `MortalItemIdentityTransitionTests.cs`, `MortalItemTestFixtureTests.cs`,
  `NpcTradeServiceRequestFlowTests.cs`, `RivalSoulArcServiceTests.cs`,
  `TrainingServiceTests.cs`, `TrainingWebCommandServiceTests.cs`,
  `WebUi/BrowserStorageTransportParityTests.cs`, and
  `WebUi/BrowserTradeParityTests.cs` do not own a second accepted canonical
  location builder. They now consume exact accepted IDs/projections or retain
  explicitly raw item/actor/action fragments; names and case-folded aliases are
  display-only negatives.
- `GmTurnHelperContractTests.cs` and `LiveTurnPreparationServiceTests.cs`
  intentionally retain raw pre-seal carriers. They are not canonical positive
  state and will receive their final authoring vocabulary with T083–T091.
  `ExampleDocumentationValidationTests.cs` remains the executable runner for
  those examples and is likewise finalized in T091.
- `AfterlifeEntityProfileValidationTests.cs` and
  `AfterlifeDocumentationCoverageTests.cs` remain unchanged realm-isolation and
  afterlife-document fragments. They contain no accepted Mortal canonical
  location and stay owned by #1514 where applicable.

## GM examples and documentation fixtures

| Path | Category | Existing role | Required disposition |
|---|---|---|---|
| `Examples/E_Block_11.B.txt` | Fragment-only cross-reference | NPC/location authoring example. | Audit; update any persistent location authority to exact IDs without broadening the example. |
| `Examples/E_Block_19.D.txt` | Fragment-only cross-reference | Rival/story location example. | Audit; keep narrative names, replace governed references with exact IDs where present. |
| `Examples/E_Block_20.txt` | Primary worked example | Mortal location/map authoring. | Rewrite for complete start+neighbor+link, hidden remote+reveal, and bounded repair. |
| `Examples/E_Block_21.txt` | Fragment-only cross-reference | Faction territory example. | Update territory references to exact accepted location IDs only. |
| `Examples/E_CLI_Afterlife_Turns.txt` | Afterlife fragment-only | Shining/Chaos Sea turn examples. | No change for #1513 unless a shared contract is actually modified. |
| `Examples/E_CLI_GM_Worker_Guardian_Abode_Content.txt` | Afterlife fragment-only | Guardian Abode worker example. | No change; #1514 owns afterlife location materialization. |
| `Examples/E_CLI_Step_Main.txt` | Primary worked CLI example | Mortal turn response with current/map wrappers. | Rewrite to complete raw creation routes and client-owned post-seal outcome. |

## Exit criteria

- Every positive or backup canonical fixture carries a valid envelope/receipt
  and has coherent map/current/index companions.
- Every negative canonical fixture documents and mutates exactly one intended
  defect unless its test explicitly covers an atomic multi-defect case.
- Every raw fragment is labeled pre-seal and is never accepted by a canonical
  reader.
- Afterlife-only fixtures remain unchanged unless a shared implementation
  boundary demonstrably changes.
