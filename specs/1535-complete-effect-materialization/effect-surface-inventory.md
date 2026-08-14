# Effect Surface Inventory

**Feature**: [Complete Effect Materialization](spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
**Captured from**: branch `1535-effect-materialization`, base `385979a01c08863cf3018b399aad7799b58b8929`

## Classification Rules

- `legacy-command`: old GM route to remove, not alias.
- `active-carrier`: durable runtime state governed by the shared effect contract.
- `mechanics-reader`: runtime consumer to migrate to `EffectMechanicsSnapshot`.
- `player-reader`: console/browser surface to migrate to `EffectPlayerProjection`.
- `afterlife-adapter`: specialized spiritual/profile state that keeps domain semantics while adopting common identity/lifecycle.
- `static-source`: template or passive/static mechanic; existence alone never activates it.
- `separate-entitlement`: related state that must not become a generic carrier.
- `repair-privacy`: internal validation/repair state excluded from player output.
- `positive-fixture`: active example/template to rewrite to current schema.
- `negative-fixture`: deliberately invalid evidence retained only under an explicit rejecting test.

The targeted scan found 94 non-generated files. Ten adjacent executable or
contract paths were added because their indirection does not contain the
searched field names directly. Generated `bin/` output and the bundled
map-viewer JavaScript are excluded.

## Executable Runtime and UI Surfaces

| Path | Current role | Classification | #1535 action | Task |
| --- | --- | --- | --- | --- |
| `BookOfEternityClient/Configuration/FileMapping.cs` | Maps legacy effect fields into durable files | `legacy-command` | Replace with common transient command root | T028–T029 |
| `BookOfEternityClient/Models/GameResponse.cs` | Declares legacy player/NPC response fields | `legacy-command` | Replace with `effectChanges[]` and receipts | T028 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` | Repair request/retry flow | `repair-privacy` | Bounded packet, coherent replay, safe output | T081–T087 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs` | Preserves passive bonuses | `static-source` | Keep static; validate definitions at owner boundary | T033–T034 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` | Snapshot/rollback evidence | `repair-privacy` | Track all effect-owned paths | T084 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` | Player failure output | `repair-privacy` | Fixed in-world copy; operator-only detail | T087 |
| `BookOfEternityClient/game_master_daemon.ps1` | Forces GM reading | `static-source` | Require current effect contracts | T106 |
| `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs` | Profile combat-effect source semantics | `afterlife-adapter` | Add profile carrier; keep arts static | T057–T059 |
| `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` | Specialized active conditions | `afterlife-adapter` | Common identity/lifecycle, no duplication | T059–T062 |
| `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs` | Conflict mechanics preview | `mechanics-reader` | Consume legal snapshot components | T063 |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs` | Normalizes static bonuses | `static-source` | Preserve; never promote automatically | T033–T034 |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs` | Normalizes passive knowledge bonuses | `static-source` | Preserve; never promote automatically | T033–T034 |
| `BookOfEternityClient/Services/CharacteristicsService.cs` | Applies raw temporary effects | `mechanics-reader` | Use all-or-nothing snapshot | T027, T037–T038 |
| `BookOfEternityClient/Services/FactionCoreChangesContract.cs` | Static faction bonus contract | `static-source` | Optional closed definitions only | T034 |
| `BookOfEternityClient/Services/GuardianPolicyContracts.cs` | Passive memory/afterlife mechanics | `static-source` | Keep passive; no automatic activation | T034, T057 |
| `BookOfEternityClient/Services/MortalItemMaterializationContract.cs` | Item bonuses/combat templates | `static-source` | Add closed definitions separately | T033 |
| `BookOfEternityClient/Services/NpcCoreChangesContract.cs` | NPC skill/action templates | `static-source` | Definitions only when complete | T034 |
| `BookOfEternityClient/Services/ShiningBlessingEffectState.cs` | Blessing entitlement | `separate-entitlement` | Explicitly exclude from generic carrier | T053, T098, T105 |
| `BookOfEternityClient/Services/TrainingService.cs` | Skill template metadata | `static-source` | Preserve definition/application split | T033 |
| `BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs` | Reward/memory semantics | `static-source` | Keep static; owner contract may define effects | T033–T034 |
| `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeArchiveTradeAndLifecycle.cs` | Afterlife reward semantics | `static-source` | Route active state through common command | T057–T062 |
| `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeCombatConditions.cs` | Direct condition validation | `afterlife-adapter` | Reject direct lifecycle authoring | T054, T059–T060 |
| `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs` | Accepted profiles | `afterlife-adapter` | Validate profile carrier and definitions | T034, T057–T058 |
| `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs` | Conflict lifecycle | `afterlife-adapter` | Compose common plan and specialized rules | T054–T062 |
| `BookOfEternityClient/Services/Validation/ValidationService.FactionsAndProjects.cs` | Faction/project bonuses | `static-source` | Preserve static semantics | T034 |
| `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs` | Pending/control protection | `repair-privacy` | Protect pending effect root | T050, T084 |
| `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs` | Faction materialization | `static-source` | Export accepted definitions/identity | T025, T034 |
| `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs` | Allows `NPCEffectChanges` | `legacy-command` | Remove route; delegate final carrier | T024, T031, T034 |
| `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs` | Player legacy route, templates, combat arrays | mixed | Remove route; validate definitions/final envelope | T024, T031, T033 |
| `BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs` | Combat arrays and quest/world templates | mixed | Shared final contract plus source exports | T024–T025, T033 |
| `BookOfEternityClient/UI/AfterlifeCombatConditionPlayerAuditSanitizer.cs` | Filters visible conditions | `player-reader` | Use shared adapter projection/privacy | T068, T073–T074 |
| `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs` | Art templates and active conditions | mixed | Keep templates; project accepted active state | T068, T073 |
| `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs` | Lifecycle condition view | `player-reader` | Use accepted projection | T068, T073 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.EntityProfiles.cs` | Special-art templates | `static-source` | Add active profile section separately | T068, T073 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` | Static bonuses | `static-source` | Preserve static display | T070, T073 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs` | Raw condition reader | `player-reader` | Use adapter projection | T068, T073 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs` | Static faction/news bonuses | `static-source` | Preserve semantics; suppress internal DTOs | T069, T074 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs` | Item templates | `static-source` | Preserve template inspection | T070 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs` | Raw player/combat arrays | `player-reader` | Use accepted projection | T066, T070 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs` | Indirect NPC effect reader | `player-reader` | Use accepted NPC projection | T066, T070 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Trade.cs` | Item bonuses | `static-source` | No active authority | T070 |
| `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs` | Skill templates and status | mixed | Separate static and active views | T066, T070 |
| `BookOfEternityClient/UI/ExplorerMortalEffectDetailActions.cs` | Raw action snapshots | `player-reader` | Opaque selector and revalidation | T066–T071, T095 |
| `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` | Browser effects/templates | mixed | Shared accepted projection | T067, T070–T074 |
| `BookOfEternityClient/UI/MortalStatusEffectFallback.cs` | Indirect fallback reader | `player-reader` | All-or-nothing accepted fallback | T066–T070 |
| `BookOfEternityClient/UI/MortalItemPlayerProjection.cs` | Recursive DTO sanitizer | `repair-privacy` | Add effect DTO shape signatures | T065, T074 |
| `BookOfEternityClient/UI/NpcDetailSectionProjection.cs` | Indirect NPC projection | `player-reader` | Shared accepted effect projection | T066–T070 |
| `BookOfEternityClient/UI/StructuredBonusDisplay.cs` | Static bonus formatter | `static-source` | Preserve semantic catch-all | T070 |
| `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs` | Browser action submit | `player-reader` | Re-resolve current accepted selector | T067, T071 |

## GM Rules and Guides

| Path | Classification | #1535 action | Task |
| --- | --- | --- | --- |
| `Rules/Block_2.txt` | `static-source` | Teach definitions, profiles, active boundary | T102 |
| `Rules/Block_5.txt` | `static-source` | Complete skill definition and apply | T102 |
| `Rules/Block_6.txt` | `static-source` | Item definition/application boundary | T102 |
| `Rules/Block_7.txt` | mixed | Replace direct active-array authoring | T102 |
| `Rules/Block_8.txt` | mixed | Combat refs, triggers, stacks, lifetime | T102 |
| `Rules/Block_9.txt` | `static-source` | Wound/effect independence | T102–T103 |
| `Rules/Block_10.txt` | `static-source` | Fate Card template versus active instance | T102 |
| `Rules/Block_12.txt` | `legacy-command` | Remove NPC legacy command | T102 |
| `Rules/Block_13.txt` | reviewed source | Synchronize related terminology | T102–T103 |
| `Rules/Block_14.txt` | `static-source` | Quest definition/application | T103 |
| `Rules/Block_15.txt` | `static-source` | Location/hazard definition/application | T103 |
| `Rules/Block_17.txt` | `static-source` | World-event definition/application | T103 |
| `Rules/Block_19.C.txt` | `static-source` | Same-turn faction source | T103 |
| `Rules/Block_19.txt` | `static-source` | Preserve passive faction bonuses | T103 |
| `Rules/Block_21.txt` | `afterlife-adapter` | Align profile/condition routes | T105 |
| `Rules/Block_24.txt` | `afterlife-adapter` | Synchronize identity/lifecycle | T105 |
| `Rules/Block_25.txt` | `static-source` | Hazard periodic/triggered lifecycle | T103 |
| `Rules/Block_25.A.txt` | `static-source` | Trigger/bounded resolution | T103 |
| `Rules/Block_CLI_Operations.txt` | mixed | Commands, pending, repair privacy, no aliases | T104 |
| `OtherGuides/Afterlife_Combat_Terminology_Glossary.md` | `afterlife-adapter` | Common identity/lifecycle terms | T105 |
| `OtherGuides/Afterlife_Contract_Matrix.md` | `afterlife-adapter` | Register all new paths/contracts | T105 |

## Worked Examples and Manifest

| Path | Classification | #1535 action | Task |
| --- | --- | --- | --- |
| `Examples/E_Block_5.txt` | `static-source` | Skill definition plus apply | T107 |
| `Examples/E_Block_6.txt` | `static-source` | Item definition plus apply | T107 |
| `Examples/E_Block_7.txt` | positive application | Common command and combat anchor | T107 |
| `Examples/E_Block_8.txt` | positive application | Stack/refresh/expiry/trigger | T107 |
| `Examples/E_Block_10.txt` | `static-source` | Fate Card definition/application | T107 |
| `Examples/E_Block_10.V.txt` | `static-source` | Preserve template semantics | T107 |
| `Examples/E_Block_12.txt` | `legacy-command` | Replace NPC legacy route | T107 |
| `Examples/E_Block_13.txt` | reviewed source | Synchronize terminology | T107 |
| `Examples/E_Block_14.txt` | `static-source` | Quest definition/application | T107 |
| `Examples/E_Block_16.txt` | reviewed source | Synchronize exact source behavior | T107 |
| `Examples/E_Block_19.C.txt` | `static-source` | Same-turn faction source | T107 |
| `Examples/E_Block_19.txt` | `static-source` | Preserve faction bonus semantics | T107 |
| `Examples/E_Block_21.txt` | `afterlife-adapter` | Profile/condition routes | T108 |
| `Examples/E_Block_23.txt` | `afterlife-adapter` | Common identity/lifecycle | T108 |
| `Examples/E_Block_25.txt` | `static-source` | Environmental periodic/triggered effect | T107 |
| `Examples/E_CLI_Afterlife_Turns.txt` | `afterlife-adapter` | Profile effect and five condition kinds | T108 |
| `Examples/E_CLI_Mortal_Item_Materialization.txt` | `static-source` | Definition without automatic promotion | T107 |
| `Examples/E_CLI_NPC_Trade.txt` | `static-source` | Preserve item semantics | T107 |
| `Examples/E_CLI_Step_Main.txt` | `legacy-command` | New response fields | T104, T107 |
| `Examples/E_CLI_Training_Showcases.txt` | `static-source` | Definition/application separation | T107 |
| `Examples/E_Soul_Relic_Integration.txt` | `static-source` | Keep relic template semantics separate | T108 |
| `Examples/example_validation_manifest.json` | manifest | Register required text/validation relations | T099, T109 |

## Active and Validator Fixtures

| Path | Classification | #1535 action | Task |
| --- | --- | --- | --- |
| `FileSystemExample/game_session/game_state/inventory/items.json` | `positive-fixture` | Add definitions where materializable; keep static bonuses | T101 |
| `FileSystemExample/validator_fixtures/combat_group_health_states/broken/combatants.json` | `negative-fixture` | Retain explicit invalid active-array case | T100, T110 |
| `FileSystemExample/validator_fixtures/combat_group_health_states/fixed/combatants.json` | `positive-fixture` | Complete instances plus client anchors/index | T100–T101 |
| `FileSystemExample/validator_fixtures/faction_bonus_existing_id/fixture.json` | `static-source` | Preserve static behavior | T101 |
| `FileSystemExample/validator_fixtures/faction_bonus_existing_id/shared/faction_core_backup.json` | `static-source` | Preserve static behavior | T101 |
| `FileSystemExample/validator_fixtures/faction_custom_state_existing_id/shared/faction_core_backup.json` | `static-source` | Preserve static behavior | T101 |
| `FileSystemExample/validator_fixtures/faction_project_existing_id/shared/faction_core_backup.json` | `static-source` | Preserve static behavior | T101 |
| `FileSystemExample/validator_fixtures/item_bond_fate_card_contract/broken/items.json` | `negative-fixture` | Retain invalid definition/legacy evidence | T100, T110 |
| `FileSystemExample/validator_fixtures/item_bond_fate_card_contract/fixed/items.json` | `positive-fixture` | Add complete definition where applicable | T100–T101 |
| `FileSystemExample/validator_fixtures/pending_memory_legacy_root_fields/broken/pending_memory_legacy.json` | `negative-fixture` | Keep separate from active effects | T100 |
| `FileSystemExample/validator_fixtures/pending_memory_legacy_root_fields/fixed/pending_memory_legacy.json` | `static-source` | Preserve passive authority | T100 |
| `FileSystemExample/validator_fixtures/skill_mastery_semantics/broken/skills_active.json` | `negative-fixture` | Retain rejecting static/definition case | T100, T110 |
| `FileSystemExample/validator_fixtures/skill_mastery_semantics/fixed/skills_active.json` | `positive-fixture` | Preserve static skill; definitions only if complete | T100–T101 |

## Conclusions

1. The two legacy response fields are the only existing general application routes; neither proves complete authority.
2. Static bonuses, combat templates, Fate Cards, special-art descriptions, and Shining blessing entitlement must not be mass-promoted.
3. The five owner families and three identity/command/pending roots in `EffectMaterializationTestContext.OwnedPaths` cover active authority.
4. Every current mechanics/UI direct read has an owning migration task; static readers remain separate.
5. Every matched GM rule, example, manifest, and active fixture has an explicit T097–T110 disposition.
