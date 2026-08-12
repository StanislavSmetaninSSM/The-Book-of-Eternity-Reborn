# Mortal Item Fixture Migration Inventory

**Issue**: [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511)
**Recorded**: 2026-08-11

This inventory separates durable ordinary Mortal item carriers from command,
sidecar, historical, and display-only fragments. The game has not shipped, so
no positive receipt-less carrier remains after #1511. A receipt-less object is
allowed only inside a test whose name explicitly states that receipt-less state
is rejected.

Generated `bin/`, `obj/`, `TestResults/`, and `.serena/` content is excluded.

## Repository-owned JSON state

| Path | Fixture/member | Classification | #1511 action | Receipt-less allowed after #1511 |
| --- | --- | --- | --- | --- |
| `FileSystemExample/game_session/game_state/inventory/items.json` | `item_ancient_sword` | positive-current canonical carrier | remove `id`; add all governed fields, complete envelope, immutable receipt, and matching index entry | no |
| `FileSystemExample/game_session/game_state/inventory/item_identity_index.json` | new index root | positive-current client authority | create schema v1 index containing the sword create transition | n/a |
| `FileSystemExample/game_session/game_state/npcs/item_journals.json` | `item_ancient_sword` journal | positive companion | retain permanent `itemId`; reconcile readable/sentient disposition and journal authority | n/a |
| `FileSystemExample/game_session/game_state/quests/quest_history.json` | historical-only reward IDs | historical fragment | retain as historical-only; prove it does not claim an active carrier/index entry | n/a |
| `FileSystemExample/game_session/saves/manual_saves/mortal_world_command_display_fixture.zip` | 10 player items, 3 NPC items, 2 location-storage items | positive reusable command-display save | migrate every durable carrier to a complete receipt-bearing item; normalize `equippedItems`; move accepted NPC adds into `npc_core.json`; clear stale NPC item commands; add the 15-entry identity index | no |
| `FileSystemExample/validator_fixtures/item_bond_fate_card_contract/fixed/items.json` | `traveler_lantern_01` | positive-current validator carrier | add equal IDs, complete envelope/receipt/index fixture; retain bond/Fate Card semantics | no |
| `FileSystemExample/validator_fixtures/item_bond_fate_card_contract/broken/items.json` | `traveler_lantern_01` | intentional semantic-negative carrier | add otherwise-valid envelope/receipt/index so only the intended bond/Fate Card defects remain | no |
| `FileSystemExample/validator_fixtures/item_bond_fate_card_contract/fixed/item_bonds.json` | lantern bond entries | positive companion | rewrite to exact permanent ID and keep companion/evidence agreement | n/a |
| `FileSystemExample/validator_fixtures/item_bond_fate_card_contract/broken/item_bonds.json` | invalid lantern bond entries | intentional semantic-negative companion | preserve the named bond defect while resolving exact item identity | n/a |
| `FileSystemExample/validator_fixtures/npc_inventory_add_payload/fixed/npc_inventory.json` | empty `NPCInventoryAdds` | empty command fragment | retain; no durable item exists | n/a |
| `FileSystemExample/validator_fixtures/npc_inventory_add_payload/broken/npc_inventory.json` | add without nested item | intentional command-negative fragment | retain; expected failure occurs before item materialization | n/a |
| `FileSystemExample/validator_fixtures/item_journals/fixed/item_journals.json` | exact journal refs | positive companion fragment | resolve against a current-schema carrier supplied by its test harness | n/a |
| `FileSystemExample/validator_fixtures/item_journals/broken/item_journals.json` | malformed journal refs/content | intentional companion-negative fragment | preserve the named journal defect; supply valid carrier authority separately | n/a |
| `FileSystemExample/validator_fixtures/item_text_updates_shape/fixed/item_text_updates.json` | text update | positive companion fragment | retain exact permanent ID reference; do not embed receipt/index fields | n/a |
| `FileSystemExample/validator_fixtures/equipment_changes_slots/fixed/equipment_changes.json` | equipment references | positive command fragment | retain exact permanent ID reference; carrier fixture supplies receipt/index | n/a |
| `FileSystemExample/validator_fixtures/equipment_changes_slots/broken/equipment_changes.json` | invalid slot/reference | intentional command-negative fragment | preserve the named equipment defect | n/a |

## Shared test builders and full carrier literals

All rows below must construct durable positive carriers through
`BookOfEternityClient.TestSupport/MortalItemTestFixture.cs`. Tests that need a
specific semantic defect first create a valid current-schema item, then mutate
only the field named in the test.

| Path | Fixture/member | Classification | #1511 action | Receipt-less allowed after #1511 |
| --- | --- | --- | --- | --- |
| `BookOfEternityClient.TestSupport/MortalActorTestFixtures.cs` | `CreateInventoryItem` | shared positive new-NPC item | compose the complete raw item builder with the actor-specific identity/name | no |
| `BookOfEternityClient.IntegrationTests/ActorMaterializationValidationTests.cs` | new actor inventory cases | raw creation carriers | use complete `new_npc_inventory` items and actor/item creation refs | no |
| `BookOfEternityClient.IntegrationTests/AfterlifeRealmSegregationValidationTests.cs` | Mortal inventory snapshots | positive-current realm boundary carriers | use receipt-bearing Mortal items plus matching index; keep afterlife intrusion as the sole defect | no |
| `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Inventory.cs` | `letter_black_wax` | positive-current canonical carrier | use a complete readable item and matching index while retaining journal-anchor assertions | no |
| `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.cs` | player/NPC trade inventory seeds | positive-current local/trade carriers | use complete carrier/index fixtures; preserve trade-specific data | no |
| `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GeneralPanels.cs` | inventory panel seeds | positive-current display carriers | use complete current-schema items; assertions remain player-semantic only | no |
| `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs` | drop/split/merge/trade seeds | positive-current lifecycle carriers | use complete roots/index; assert transfer, derived receipt, retirement, and privacy | no |
| `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs` | `SeedInventoryEquipmentItemsAsync`, `SeedInventoryItemDetailStateAsync`, document/local-action seeds | positive-current display/lifecycle carriers | use complete roots/index and exact companion IDs | no |
| `BookOfEternityClient.IntegrationTests/MechanicalBonusAuthorityValidationTests.cs` | `CreateItem`/`WriteInventoryAsync` | positive plus single-field semantic-negative carriers | build complete current-schema items, then mutate only mechanical authority under test | no |
| `BookOfEternityClient.IntegrationTests/QuestRewardAuthorityValidationTests.cs` | active reward inventory cases | positive-current route carriers | use complete `quest_reward` item/index transition; historical-only cases remain fragments | no |
| `BookOfEternityClient.IntegrationTests/ReadableDocumentAuthorityValidationTests.cs` | `CreateDocumentItem`, `CreateNewDocumentItem` | canonical and raw readable carriers | use complete current-schema readable items; same-turn raw cases use `creationRef` only | no |
| `BookOfEternityClient.Tests/ConsoleNpcTradeCommandTests.cs` | player trade inventory seeds | positive-current trade carriers | use complete roots/index and preserve settlement assertions | no |
| `BookOfEternityClient.Tests/NpcTradeServiceRequestFlowTests.cs` | player inventory seeds | positive-current trade carriers | use complete roots/index; failed settlement must preserve exact bytes | no |
| `BookOfEternityClient.Tests/ShiningBlessingEffectStateTests.cs` | ordinary Mortal recipient items | positive-current shared Mortal carriers | use complete roots/index while keeping Shining effect authority separate | no |
| `BookOfEternityClient.Tests/WebUi/BrowserInventoryManagementTests.cs` | `SeedInventoryAsync` | positive-current local-action carriers | use roots/index; replace cloned identity behavior with split/merge/destroy expectations | no |
| `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs` | player/storage/vehicle inventory seeds | positive-current transfer carriers | use roots/index for every active occurrence; malformed JSON tests remain malformed-file tests | no |
| `BookOfEternityClient.Tests/WebUi/BrowserTradeParityTests.cs` | player/NPC trade inventory seeds | positive-current trade carriers | use roots/index and preserve exact physical identity through buy/sell/buyback | no |

## Intentional negative inputs

| Path | Fixture/member | Classification | #1511 action | Receipt-less allowed after #1511 |
| --- | --- | --- | --- | --- |
| `BookOfEternityClient.Tests/MortalItemTestFixtureTests.cs` | `CreateReceiptlessNegative` | explicit receipt-less negative | test name must include `RejectsReceiptless`; never write it as accepted canonical state | yes, negative input only |
| `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.cs` | canonical receipt-less row | explicit receipt-less negative | assert `mortal_item_materialization_receiptless_current_item` and exact rollback | yes, negative input only |
| `BookOfEternityClient.IntegrationTests/ReadableDocumentAuthorityValidationTests.cs` | raw missing/readable mismatch rows | intentional raw semantic negatives | start from a complete raw item and remove only the tested evidence | no canonical receipt-less state |
| `BookOfEternityClient.IntegrationTests/MechanicalBonusAuthorityValidationTests.cs` | mechanical mismatch rows | intentional canonical semantic negatives | retain valid receipt/index and mutate only targeted mechanic field | no |
| `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs` | malformed JSON cases | intentional unreadable-file negatives | retain raw malformed file; no item object is accepted | n/a |

## Fragment-only and out-of-carrier references

These surfaces do not own a durable item and therefore must not receive an
embedded envelope or receipt. They use `creationRef` for a same-turn new item
or exact permanent `itemId` for an existing item, according to their route.

| Path | Fragment | #1511 action |
| --- | --- | --- |
| `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` | isolated sentinel inventory archive | keep fragment-only unless the test invokes canonical item validation; do not treat `id` as current identity |
| `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs` | repair diagnostics/target paths | update expected packet kind/targets; no durable carrier literal |
| `BookOfEternityClient.IntegrationTests/GmTurnHelperContractTests.cs` | trajectory diagnostics containing item paths | update expected issue/packet codes only |
| `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs` | empty inventory bootstrap | add empty index; no placeholder item |
| `Examples/E_Block_5.txt` | generic first acquisition | replace raw item with complete `player_acquisition` package |
| `Examples/E_Block_9.txt` | craft output | use complete `craft_output` package bound to craft request |
| `Examples/E_Block_10.txt` | player inventory commands | use `creationRef` for new and exact `itemId` for existing items |
| `Examples/E_Block_11.txt` | inventory moves | preserve exact existing item identity; do not author receipt/index |
| `Examples/E_Block_19.A.txt` | NPC inventory/trade | distinguish new NPC item creation, NPC acquisition, and existing transfer |
| `Examples/E_Block_20.txt` | location storage | bind new placement to existing location/storage authority and existing moves to exact item ID |
| `Examples/E_CLI_NPC_Trade.txt` | trade request/receipt refs | preserve exact item/request identity and never recreate transferred stock |
| `Examples/E_CLI_Quest_Reward_Authority.txt` | reward item refs | bind reward detail to the accepted permanent item transition |
| `Examples/E_CLI_Step_Main.txt` | main GM workflow | require complete raw package, client sealing, and exact repair workflow |
| `Examples/E_CLI_Afterlife_Turns.txt` | afterlife items/relics | out of scope; verify no shared contract change under #1511 |

## Completion rule

The migration is complete only when a source guard proves that every positive
repository carrier has equal `itemId`/`existedId`, a complete envelope, a valid
receipt, and one matching index entry; every remaining receipt-less literal is
reachable only from an explicitly named negative test or a fragment-only raw
command that has not yet become canonical state.
