# Implementation Plan: Unified Resource Authority

**Branch**: `1535-effect-materialization` | **Date**: 2026-08-15 | **Spec**: [spec.md](spec.md)

**Input**: Approved feature specification from `specs/1543-unified-resource-authority/spec.md` and approved architecture from `docs/superpowers/specs/2026-08-15-unified-resource-authority-design.md`.

## Summary

Replace every included persisted health/energy/poise/charge/ammunition/durability/action-point/current-max authority with one sealed resource definition catalog, one live state ledger, immutable transition history, and one transient command surface. Generalize combat/resource owner identity, reduce every authorized ordinary or effect-generated mutation through exact decimal arithmetic, and orchestrate resources plus Effect Task 9 through one cached `AcceptedMechanicsPlan` that publishes atomically under the existing canonical write lease/rollback contour. A selected Mortal QTE terminal penalty is a canonical direct resource producer with exact replay and shared quartet rollback; completed #1535 T042a/T047b resume it from immutable acceptance-time effect authority and publish the accepted trigger graph in the same terminal transaction. UI and GM context consume safe in-memory projections; active templates/examples switch directly and old technical saves are rejected without migration or fallback.

## Technical Context

**Language/Version**: C# 12 on .NET 8; PowerShell 7 for bounded local verification; existing React/Vite/TypeScript frontend changes only if current typed DTOs cannot consume the safe C# resource projection.

**Primary Dependencies**: Existing `FileSystemManager`, `PendingTurnSnapshotAuthority`, `ValidationService`, `CanonicalStateNormalizer`, accepted-turn write lease/repair/rollback loop, `StateDistributor`, `QteSceneService`, Mortal actor/item/location plans, effect source/target/carrier/index/lifecycle services from #1535, `StateManager`, console/browser result builders, Spectre.Console, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, `System.Text.Json`/`JsonNode`, and SHA-256 fingerprints. No new runtime package or cloud service.

**Storage**: File-backed JSON. The protected canonical resource quartet is `game_state/resources/resource_definitions.json`, `resource_state.json`, `resource_history.json`, and `resource_owner_authority.json`; transient input is `resource_commands.json`; bounded effect/resource pending work remains under `game_state/control/`. Final domain files contain client-published owner bindings and narrative/non-resource state only, never current/max mirrors.

**Testing**: Test-first xUnit unit/contract/integration coverage through `scripts/test-csharp.ps1`; documentation/source guards; scale guards; failure-injection lifecycle tests; one meaningful Fast checkpoint; FullValidation because shared Mortal/afterlife docs/examples/manifests change; LifecycleIntegration because accepted-turn planning, pending control, rollback, and stale output change; one final PreMerge after #1543 and resumed #1535 Task 9 are merge-ready.

**Target Platform**: Local Windows console/browser game client on .NET 8; production contracts remain portable offline .NET code.

**Project Type**: Local console/browser game client with a C# runtime, separate fast/integration test projects, file-backed GM contracts, and React/Vite browser frontend.

**Performance Goals**: Build definition, owner, state, history, effect source/target/carrier, pending, replay, and trigger indexes once per accepted plan. Doubling representative populations must remain at or below 2.5x measured planner/validation work; player status/detail views remain interactive and never replay full history.

**Constraints**: No migration, compatibility reader, legacy promotion, dual write, or raw fallback; no GitHub Actions; exact ordinal/confusable authority; exact decimal/quantum arithmetic; random client IDs allocated only by an explicit client-owned bootstrap contour and never by accepted GM/QTE materialization; no arbitrary paths or expressions; one complete plan; deterministic four-phase ordering; bounded graph; byte/existence rollback; console/browser parity; player-facing Russian copy and recursive privacy; currencies/accounting/progression remain outside. The direct QTE producer may not live-rebuild effects or become a permanent resource-only trigger bypass; completed #1535 T042a/T047b provide the mandatory immutable bindings.

**Scale/Scope**: Up to 256 definitions, 20,000 live entries, 512 pre-trigger mutations, 1,024 trigger nodes/depth 32, untruncated history, nine owner kinds, player/NPC/vehicle/combat/group/item/afterlife/effect cutovers, selected Mortal QTE terminal damage through the common quartet, and the active template plus shared Mortal/afterlife prompts/examples/manifests. Acceptance-time deferred QTE effect continuation is owned and completed by #1535 T042a/T047b rather than approximated in the direct producer.

**Source Issue(s)**: [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543); blocked consumer [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

**Contract Scope**: Player-facing console/browser; GM-facing Mortal/afterlife prompts and examples; runtime state; validation; normalization/publication; owner lifecycle; effect lifecycle; bounded pending/receipt; repair/rollback; docs/manifests/templates/source guards; no unrelated visual redesign.

**Verification Commands**:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceMaterializationContractTests|FullyQualifiedName~ResourceDefinitionCatalogTests|FullyQualifiedName~ResourceStateContractTests|FullyQualifiedName~ResourceHistoryStateTests|FullyQualifiedName~ResourceOwnerAuthorityTests|FullyQualifiedName~ResourceMutationReducerTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests|FullyQualifiedName~AcceptedMechanicsPlanCacheTests|FullyQualifiedName~ResourceTriggerGraphTests|FullyQualifiedName~ResourcePendingResolutionTests|FullyQualifiedName~ResourceProjectionServiceTests|FullyQualifiedName~ResourcePlayerPrivacyTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceMaterializationValidationTests|FullyQualifiedName~ResourceOwnerMaterializationTests|FullyQualifiedName~ResourceCombatOwnerTests|FullyQualifiedName~ResourceVehicleOwnerTests|FullyQualifiedName~ResourceItemOwnerTests|FullyQualifiedName~ResourceAfterlifeOwnerTests|FullyQualifiedName~CanonicalStateNormalizerResourceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalResourceCutoverTests|FullyQualifiedName~ResourceCombatIntegrationTests|FullyQualifiedName~ResourceVehicleIntegrationTests|FullyQualifiedName~ResourceItemIntegrationTests|FullyQualifiedName~AfterlifeResourceCutoverTests|FullyQualifiedName~EffectResourceMaterializationTests|FullyQualifiedName~ResourcePendingResolutionIntegrationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceConsoleBrowserParityTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~MortalBootstrapValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ResourceContractSourceGuardTests|FullyQualifiedName~ResourceAuthorityScaleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResolveActiveActionAsync_AppliesOnlySelectedTerminalResourceDamageThroughCanonicalQuartet|FullyQualifiedName~ResolveActiveActionAsync_ExactTerminalResourceReplayDoesNotApplyDamageTwice|FullyQualifiedName~ResolveActiveActionAsync_DirectLatePersistenceFailureRollsBackResourceQuartetAndRuntime|FullyQualifiedName~ResolveActiveActionAsync_RejectsMalformedResourceOwnerAuthorityBeforeAnyOutcomeMutation|FullyQualifiedName~QteTerminalResourceOutcome_WorkCountersRemainLinearForMultiCommandSelection"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ValidateAcceptedTurnQteOfferAsync_RejectsResourceAuthorityOutsideClosedQteV1Contract|FullyQualifiedName~ValidateAcceptedTurnQteOfferAsync_AcceptsClosedQteV1ResourceDamage|FullyQualifiedName~CompleteAction_ResourcePenaltyLateFailureRollsBackCanonicalQuartet"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedMechanicsPlannerScaleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectResourceTriggerRoutingScaleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~FileSystemExampleFixtureIntegrityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Do not run a duplicate Fast immediately before PreMerge. Do not use GitHub Actions.

## Constitution Check

*GATE before research: PASS. Re-check after Phase 1 design: PASS.*

- **GitHub traceability**: Issue #1543 owns the resource feature and #1535 records it as the blocker for Effect Task 9. Both are linked from spec, plan, tasks, contracts, data model, quickstart, Superpowers design, and final implementation plan.
- **Spec Kit fit**: This is a cross-domain epic changing canonical state, validation, normalization, pending/rollback, console/browser projections, afterlife, GM contracts, examples, and multiple sessions.
- **Player-facing integrity**: One projection feeds console/browser; in-world Russian copy remains; hidden/GM-only resources and internal owner/history/pending/repair data are recursively absent.
- **Contract/state authority**: Definitions and owners authorize mechanics; the client owns state/history/ordering/IDs/pending/publication; Mortal and afterlife prompts/docs/examples/manifests/tests change with executable contracts.
- **Test-first path**: Closed contract/reducer/owner RED tests precede production types; domain cutover RED integration precedes field removal; privacy/rollback/source-guard RED tests precede final removal.
- **Verification evidence**: Focused controls, one Fast checkpoint, conditional FullValidation and LifecycleIntegration, manual console/browser parity spot-check, scale guard, and one final PreMerge are specified.
- **Agent orchestration**: Work remains in the existing isolated worktree. No subagent is required by this plan; any later explicit delegation must include #1543/#1535, this Spec Kit set, Superpowers TDD/review/verification, and bounded commands.
- **Pre-release save policy**: Active bootstrap/templates/examples/tests switch directly. Old legacy resource state is incompatible; no migration or compatibility branch is planned.

## Project Structure

### Documentation (this feature)

```text
specs/1543-unified-resource-authority/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/
│   └── requirements.md
├── contracts/
│   ├── accepted-mechanics-publication.md
│   ├── resource-definition-and-command.md
│   ├── resource-full-party-interaction.md
│   ├── resource-owner-authority.md
│   ├── resource-pending-resolution.md
│   ├── resource-projection-and-cutover.md
│   └── resource-transition-and-history.md
└── tasks.md

docs/superpowers/
├── specs/2026-08-15-unified-resource-authority-design.md
└── plans/2026-08-15-unified-resource-authority.md
```

### New resource/mechanics source units

```text
BookOfEternityClient/Services/
├── ResourceMaterializationContract.cs       # strict roots/records/scalars/shared constants
├── ResourceDefinitionCatalog.cs             # sealed definitions, built-ins, setting proposals
├── ResourceCapacityFormulaCatalog.cs        # closed formula registry and exact capacity results
├── ResourceStateContract.cs                 # live ledger parsing, indexing, state agreement
├── ResourceHistoryState.cs                  # immutable transitions, replay index, continuity
├── ResourceHistoryWorkingSet.cs             # one plan-local incremental history builder/freeze
├── ResourceOwnerAuthority.cs                # composed owners/capabilities/exact refs/fingerprint
├── CombatantIdentityState.cs                # common combatant/group member client identity
├── ResourceMutationReducer.cs               # pure one-mutation/capacity arithmetic and events
├── ResourceMutationSourceCatalog.cs         # closed source routes -> phase/priority/policies
├── ResourceAcceptedTurnInputComposer.cs      # bounded resource/owner/effect input composition
├── AcceptedMechanicsPlan.cs                  # immutable complete after-image records
├── AcceptedMechanicsPlanCache.cs             # one validated full-fingerprint handoff
├── AcceptedMechanicsPlanner.cs               # cross-domain ordering and trigger DAG
├── ResourceRegisteredSystemOutcomeAdapter.cs # no-I/O registered sources/mutations/companion projection
├── ResourcePendingResolutionState.cs         # bounded requests, receipts, replay evidence
├── ResourceProjectionService.cs              # safe typed player/GM in-memory projection
├── ResourceRepairPacketBuilder.cs            # bounded omission-only repair projection
├── ResourcePlayerFailureMessages.cs          # fixed Russian player copy
├── CanonicalStateNormalizer/
│   └── CanonicalStateNormalizer.AcceptedMechanics.cs
└── Validation/
    └── ValidationService.ResourceMaterialization.cs
```

`EffectCombatantIdentityState.cs` is replaced by the common `CombatantIdentityState.cs`; effect target/planner callers are updated in the same identity task. `CanonicalStateNormalizer.Effects.cs` remains an effect after-image helper but no longer owns an independent publication transaction.

### Existing production files modified by the authority and publication foundation

```text
BookOfEternityClient/
├── Configuration/FileMapping.cs
├── Models/GameResponse.cs
├── Models/GameState/AggregatedGameState.cs
├── Core/StateManager.cs
├── Core/GameEngine/
│   ├── GameEngine.AgentConsole.cs
│   ├── GameEngine.IncarnationAndAfterlife.cs
│   ├── GameEngine.MainMenu.cs
│   ├── GameEngine.SessionAndSnapshots.cs
│   ├── GameEngine.TurnLifecycle.cs
│   └── GameEngine.ValidationAndRepair.cs
├── Services/
│   ├── CanonicalStateNormalizer.cs
│   ├── LiveTurnPreparationService.cs
│   ├── EffectAcceptedTurnInputComposer.cs
│   ├── EffectAcceptedTurnPlan.cs
│   ├── EffectAcceptedTurnPlanCache.cs
│   ├── EffectAcceptedTurnPlanner.cs
│   ├── EffectCarrierCatalog.cs
│   ├── EffectLifecycleScheduler.cs
│   ├── EffectMechanicsSnapshot.cs
│   ├── EffectTargetAuthority.cs
│   ├── MortalItemMaterializationContract.cs
│   ├── MortalItemTransitionWriter.cs
│   ├── MortalItemTransitionWriter.Stacks.cs
│   ├── ShiningBlessingEffectState.cs
│   ├── AfterlifeSpiritualConflictState.cs
│   ├── AfterlifeSpiritualConflictTurnPreviewService.cs
│   ├── GuardianGachaChargeRules.cs
│   ├── ShiningAbodeState.cs
│   ├── ShiningAbodeState.Gacha.cs
│   ├── CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs
│   ├── CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs
│   ├── CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs
│   ├── CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs
│   ├── Validation/ValidationService.AfterlifeSpiritualConflict.cs
│   ├── Validation/ValidationService.BootstrapAndProtocol.cs
│   ├── Validation/ValidationService.EffectMaterialization.cs
│   ├── Validation/ValidationService.InventoryNpcWorldCrossRefs.cs
│   ├── Validation/ValidationService.MathAssistant.cs
│   ├── Validation/ValidationService.MetaCodexAndAchievements.cs
│   ├── Validation/ValidationService.NpcWorldAndMeta.cs
│   ├── Validation/ValidationService.PlayerAndInventory.cs
│   ├── Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs
│   ├── MortalItemCarrierCatalog.cs
│   ├── MortalItemRouteAuthorityCatalog.cs
│   ├── PlayerGuardianFoundationState.cs
│   └── StorageTransportMoveService.cs
└── game_master_daemon.ps1
```

The task inventory must add any newly discovered active writer/reader before its cutover task is marked complete; the final legacy source guard is the executable completeness check.

### Phase-1 executable cutover inventory (2026-08-15)

The inventory is path-based, not token-only. A listed file may own several contours; every listed active writer/reader must be cut over or retained only under the explicit exclusion table. `StateDistributor` is included because it stages mapped GM response fragments even when it does not name each domain field directly.

#### Writers, stagers, normalizers, validators, and lifecycle owners

| Authority contour | Active production paths to cut over |
| --- | --- |
| Shared response/staging/bootstrap/snapshot/publication | `Models/GameResponse.cs`; `Configuration/FileMapping.cs`; `IO/StateDistributor.cs`; `Services/MortalBootstrapStateBuilder.cs`; `Core/GameEngine/GameEngine.TurnLifecycle.cs`; `Core/GameEngine/GameEngine.SessionAndSnapshots.cs`; `Services/LiveTurnPreparationService.cs`; `Services/CanonicalStateNormalizer.cs` |
| Mortal player health/energy/poise | `Services/Validation/ValidationService.PlayerAndInventory.cs`; `ValidationService.PrivateImplementation.cs`; `ValidationService.MathAssistant.cs`; `ValidationService.BootstrapAndProtocol.cs`; `Services/ShiningBlessingEffectState.cs`; the shared response/mapping/bootstrap paths above |
| Named NPC health | `Services/Validation/ValidationService.NpcWorldAndMeta.cs`; `ValidationService.InventoryNpcWorldCrossRefs.cs`; `ValidationService.MetaCodexAndAchievements.cs`; shared response/staging/publication paths |
| Vehicle health and lifecycle | `Models/GameResponse.cs`; `Configuration/FileMapping.cs`; `Services/Validation/ValidationService.MetaCodexAndAchievements.cs`; `ValidationService.InventoryNpcWorldCrossRefs.cs`; `ValidationService.LifecycleControlAndStateFiles.cs`; `ValidationService.MortalItemMaterialization.cs`; `Services/StorageTransportMoveService.cs`; `MortalItemCarrierCatalog.cs`; `MortalItemRouteAuthorityCatalog.cs`; `MortalItemTransitionWriter.cs`; `CanonicalStateNormalizer.MortalItems.cs` |
| Individual/group combat health and poise | `Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs`; `Services/EffectCarrierCatalog.cs`; shared response/staging/publication paths |
| Item durability/charges/ammunition/generic reserve | `Models/GameResponse.cs`; `Configuration/FileMapping.cs`; `Services/MortalItemMaterializationContract.cs`; `MortalItemTransitionWriter.cs`; `MortalItemTransitionWriter.Stacks.cs`; `CanonicalStateNormalizer.InventorySidecars.cs`; `CanonicalStateNormalizer.FactionAndInventoryHelpers.cs`; `CanonicalStateNormalizer.MortalItems.cs`; `ValidationService.PlayerAndInventory.cs`; `ValidationService.NpcWorldAndMeta.cs`; `ValidationService.InventoryNpcWorldCrossRefs.cs`; `ValidationService.MortalItemMaterialization.cs`; `ValidationService.LifecycleControlAndStateFiles.cs`; `MortalBootstrapStateBuilder.cs` |
| Afterlife spiritual action points | `Services/AfterlifeSpiritualConflictState.cs`; `AfterlifeSpiritualConflictTurnPreviewService.cs`; `ValidationService.AfterlifeSpiritualConflict.cs`; `ValidationService.AfterlifeCombatConditions.cs`; `Core/GameEngine/GameEngine.ValidationAndRepair.cs`; `CanonicalStateNormalizer.SharedAndSoulHelpers.cs` |
| Guardian/Shining per-return attempts and formula inputs | `Services/GuardianGachaChargeRules.cs`; `PlayerGuardianFoundationState.cs`; `SystemGuardianLibraryService.cs`; `ShiningAbodeState.cs`; `ShiningAbodeState.Gacha.cs`; `Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs`; `GameEngine.MainMenu.cs`; `GameEngine.TurnLifecycle.cs`; `CanonicalStateNormalizer.SharedAndSoulHelpers.cs`; `ValidationService.GuardianPolicyKernel.cs`; `ValidationService.GuardianProjectsAndGacha.cs`; `ValidationService.GuardiansAndAfterlife.cs`; `ValidationService.LifecycleControlAndStateFiles.cs`; `ValidationService.ShiningAbode.cs` |
| Numeric blessing/memory/relic rerolls | `Services/ShiningBlessingEffectState.cs`; `ShiningAbodeState.cs`; `ShiningAbodeState.Gameplay.cs`; `Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs`; `ValidationService.AfterlifeArchiveTradeAndLifecycle.cs`; `ValidationService.LifecycleControlAndStateFiles.cs`; `ValidationService.ShiningAbode.cs` |
| Effect-generated resource operations and common publication | `Services/EffectAcceptedTurnInputComposer.cs`; `EffectAcceptedTurnPlan.cs`; `EffectAcceptedTurnPlanCache.cs`; `EffectAcceptedTurnPlanner.cs`; `EffectLifecycleScheduler.cs`; `CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`; `Validation/ValidationService.EffectMaterialization.cs` |

#### Readers, projections, and action gates

| Consumer contour | Active production paths to cut over |
| --- | --- |
| Player status/stats/browser | `Core/StateManager.cs`; `Models/GameState/AggregatedGameState.cs`; `UI/GameInterface.cs`; `UI/ExplorerUniversalMetaCommandResultBuilder.cs`; `UI/ExplorerMortalWorldCommandResultBuilder.cs`; `UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs`; `ExplorerMode.MetaStoryAndStatus.cs`; `WebUi/BrowserGameScreenService.cs` |
| NPC, vehicle, and combat detail/action views | `UI/ExplorerMortalWorldCommandResultBuilder.cs`; `UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs`; `ExplorerMode.MetaLoreAndTravel.cs`; `ExplorerMode.Npcs.ListAndDetails.cs`; `ExplorerMode.Npcs.Rendering.cs`; `Services/StorageTransportMoveService.cs` |
| Item detail/use/trade/transport | `Services/InventoryEquipmentService.cs`; `NpcTradeService.cs`; `StorageTransportMoveService.cs`; `UI/ExplorerMode/ExplorerMode.Inventory.cs`; `ExplorerMode.Npcs.Rendering.cs`; `UI/ExplorerMortalWorldCommandResultBuilder.cs`; `ExplorerLifecycleLocalTurnCommandResultBuilder.cs` |
| Spiritual conflict action economy | `Services/AfterlifeSpiritualConflictTurnPreviewService.cs`; `UI/ExplorerAfterlifeCombatCommandResultBuilder.cs`; `ExplorerLifecycleLocalTurnCommandResultBuilder.cs`; `UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs` |
| Guardian/Shining attempts and rerolls | `UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs`; `ExplorerMode.Afterlife.PlayerGuardianFoundation.cs`; `ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs`; `ExplorerMode.Afterlife.ShiningAbode.Actions.cs`; `ExplorerMode.Afterlife.ShiningAbode.Gates.cs`; `ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs`; `ExplorerMode.Afterlife.StatusAudit.cs`; `ExplorerMode.Npcs.ListAndDetails.cs`; `UI/ExplorerShiningAbodeCommandResultBuilder.cs` |
| GM/agent context | `Core/GameEngine/GameEngine.AgentConsole.cs`; `Core/GameEngine/GameEngine.TurnLifecycle.cs`; `game_master_daemon.ps1`; the CLI/rule/example inventory below |

#### GM prompt, example, manifest, and active-template cutover matrix

| Scope | Exact active paths inspected and owned by the final cutover |
| --- | --- |
| Shared API/daemon/launcher/task | `CLI_API_Specification.md`; `CLI_Agent_Daemon_Specification.md`; `BookOfEternityClient/Launcher/CLI_Launch_Script.md`; `TaskGuides/CLI_Step_Main.txt`; `BookOfEternityClient/game_master_daemon.ps1` |
| Mortal rules | `Rules/Block_0.txt`; `Block_2.txt`; `Block_2.5.txt`; `Block_2.6.txt`; `Block_5.txt`; `Block_6.txt`; `Block_9_Universal_Tool_Functions.txt`; `Block_10.txt`; `Block_11.txt`; `Block_12.txt`; `Block_13.txt`; `Block_15.txt`; `Block_15.A.txt`; `Block_17.txt`; `Block_19.txt`; `Block_19.A.txt`; `Block_CLI_Operations.txt`; `Block_FINAL.txt` |
| Afterlife rules/guides | `Rules/Block_21.txt`; `Rules/Block_32_Guardians.txt`; `OtherGuides/Afterlife_Contract_Matrix.md`; `OtherGuides/Afterlife_Combat_Terminology_Glossary.md` |
| Worked examples and manifest | `Examples/CLI_Translation_Guide.md`; `E_Block_2.5.txt`; `E_Block_5.txt`; `E_Block_6.txt`; `E_Block_9_Updated.txt`; `E_Block_10.txt`; `E_Block_10.V.txt`; `E_Block_13.txt`; `E_Block_15.A.txt`; `E_Block_16.txt`; `E_Block_17.txt`; `E_Block_19.txt`; `E_Block_19.A.txt`; `E_Block_21.txt`; `E_Block_32.txt`; `E_CLI_Afterlife_Turns.txt`; `E_CLI_Mortal_Item_Materialization.txt`; `E_CLI_NPC_Trade.txt`; `E_CLI_Step_Main.txt`; `E_Soul_Relic_Integration.txt`; new `E_CLI_Mortal_Resources.txt`; `example_validation_manifest.json` |
| Active template and fixtures | `FileSystemExample/game_session/game_state/core/player_status.json`; `game_state/player/player_status.json`; `game_state/inventory/items.json`; `game_state/misc/vehicles.json` when present; Guardian/Shining state containing attempts/rerolls; `validator_fixtures/combat_group_health_states/**`; item/vehicle/Guardian/Shining fixtures found by the source guard; new `validator_fixtures/resource_materialization/**` |

#### Reviewed same-token exclusions

| Token family | Retained authority and rationale |
| --- | --- |
| `ownerBondLevelCurrent/Max`, mastery, experience, levels, reputation/relationship values | Relationship/progression authority; not damage/restore/spend/gain state |
| Ink Feathers, Light Sparks, treasury/faction `resourceType`, prices, trade balances | Currency/accounting authority explicitly outside FR-060 |
| Effect stacks, uses, duration, `turnsRemaining` | Effect identity/lifetime authority, not a general owner resource |
| QTE progress, mistake/noise caps, lock-pin `durability` | Ephemeral QTE-local state; must not be admitted by the resource source guard |
| Spiritual power/strain/shield and offensive-project power/shield audit values | Specialized afterlife axes or immutable calculation audit, not a spendable current/max ledger value |
| Item stack `count` | Item identity/split/merge lifecycle; resource companions follow the surviving item identity but count is not admitted as a resource |
| Archive `project_fuel` request names | Archive entry reservation/transfer workflow, not a scalar fuel pool |

The executable inventory corrected one planning omission: vehicle health qualifies and therefore adds the ninth owner kind. No other reviewed token family may enter implicitly; any future admission requires an explicit tracked contract update.

### Existing player/GM projection files modified

```text
BookOfEternityClient/
├── UI/GameInterface.cs
├── UI/ExplorerUniversalMetaCommandResultBuilder.cs
├── UI/ExplorerMortalWorldCommandResultBuilder.cs
├── UI/ExplorerAfterlifeCombatCommandResultBuilder.cs
├── UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs
├── UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs
├── UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs
├── UI/ExplorerMode/ExplorerMode.MetaLoreAndTravel.cs
├── UI/ExplorerMode/ExplorerMode.Inventory.cs
├── UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs
├── UI/ExplorerMode/ExplorerMode.Npcs.Rendering.cs
├── UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs
├── UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs
├── UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs
├── UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs
├── UI/ExplorerMode/ExplorerMode.Afterlife.StatusAudit.cs
└── WebUi/BrowserGameScreenService.cs
```

React files change only if browser DTO tests prove the current generic/status rendering cannot consume the projected shape.

### New tests

```text
BookOfEternityClient.Tests/
├── ResourceMaterializationContractTests.cs
├── ResourceDefinitionCatalogTests.cs
├── ResourceCapacityFormulaCatalogTests.cs
├── ResourceStateContractTests.cs
├── ResourceHistoryStateTests.cs
├── ResourceOwnerAuthorityTests.cs
├── CombatantIdentityStateTests.cs
├── ResourceMutationReducerTests.cs
├── AcceptedMechanicsPlannerTests.cs
├── AcceptedMechanicsPlanCacheTests.cs
├── ResourceTriggerGraphTests.cs
├── ResourcePendingResolutionTests.cs
├── ResourceProjectionServiceTests.cs
├── ResourcePlayerPrivacyTests.cs
├── ResourceAuthorityScaleTests.cs
└── ResourceContractSourceGuardTests.cs

BookOfEternityClient.IntegrationTests/
├── ResourceMaterializationTestContext.cs
├── ResourceMaterializationTestContext.Owners.cs
├── ResourceMaterializationTestContext.Publication.cs
├── ResourceMaterializationValidationTests.cs
├── ResourceOwnerMaterializationTests.cs
├── ResourceCombatOwnerTests.cs
├── ResourceVehicleOwnerTests.cs
├── ResourceItemOwnerTests.cs
├── ResourceAfterlifeOwnerTests.cs
├── CanonicalStateNormalizerTests.Resources.cs
├── MortalResourceCutoverTests.cs
├── ResourceCombatIntegrationTests.cs
├── ResourceVehicleIntegrationTests.cs
├── ResourceItemIntegrationTests.cs
├── AfterlifeResourceCutoverTests.cs
├── EffectResourceMaterializationTests.cs
├── ResourcePendingResolutionIntegrationTests.cs
├── ResourceConsoleBrowserParityTests.cs
├── ResourceMaterializationLifecycleTests.cs
├── AcceptedMechanicsPlannerScaleTests.cs
└── EffectResourceTriggerRoutingScaleTests.cs
```

Existing effect, Mortal bootstrap/item/combat, afterlife spiritual-conflict/gacha/blessing, Explorer, browser, documentation, example, and fixture tests are updated rather than duplicated where they already own the behavior.

### GM contracts, examples, templates, and source guards

```text
CLI_API_Specification.md
CLI_Agent_Daemon_Specification.md
BookOfEternityClient/Launcher/CLI_Launch_Script.md
TaskGuides/CLI_Step_Main.txt
Rules/Block_0.txt
Rules/Block_2.txt
Rules/Block_5.txt
Rules/Block_6.txt
Rules/Block_10.txt
Rules/Block_12.txt
Rules/Block_13.txt
Rules/Block_15.txt
Rules/Block_15.A.txt
Rules/Block_17.txt
Rules/Block_19.txt
Rules/Block_19.A.txt
Rules/Block_32_Guardians.txt
Rules/Block_CLI_Operations.txt
Rules/Block_FINAL.txt
Examples/E_Block_5.txt
Examples/E_Block_6.txt
Examples/E_Block_10.txt
Examples/E_Block_10.V.txt
Examples/E_Block_13.txt
Examples/E_Block_15.A.txt
Examples/E_Block_16.txt
Examples/E_Block_17.txt
Examples/E_Block_21.txt
Examples/E_Block_32.txt
Examples/E_CLI_Afterlife_Turns.txt
Examples/E_CLI_Mortal_Resources.txt
Examples/example_validation_manifest.json
OtherGuides/Afterlife_Contract_Matrix.md
OtherGuides/Afterlife_Combat_Terminology_Glossary.md
FileSystemExample/game_session/game_state/resources/resource_definitions.json
FileSystemExample/game_session/game_state/resources/resource_state.json
FileSystemExample/game_session/game_state/resources/resource_history.json
FileSystemExample/game_session/game_state/core/player_status.json
FileSystemExample/game_session/game_state/inventory/items.json
FileSystemExample/validator_fixtures/resource_materialization/**
BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs
BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs
```

The source guard maintains a narrow explicit allow-list for historical design/audit documents; active prompts, examples, templates, mechanical consumers, and mappings must contain zero removed authority.

**Structure Decision**: Keep the existing single C# runtime and partial-class validation/normalizer organization. Add focused resource/mechanics services under `BookOfEternityClient/Services`, focused test files in the existing unit/integration projects, and contract artifacts under `specs/1543-unified-resource-authority`. Root-shared accepted-turn handoffs use `CanonicalRootIdentity` plus a weak `AcceptedTurnAuthorityRegistry`; the registry exposes only lease-bound typed operations and rotates common/effect/item authority together by persisted generation plus monotonic in-process revision. Generalize only the combat identity and accepted mechanics transaction boundaries that must be shared; do not introduce a new project, database, package, or frontend redesign.

## Implementation Phases

### Phase A — Contract and pure authority foundation

Create strict definition/state/history/command contracts, exact coefficient/scale scalar helpers, built-in catalog, capacity formula registry, common owner authority, and generalized combat/group identity. Static initialization is checked locally; registered-formula initialization remains bound to the later composed typed owner authority. Nothing reads/writes legacy values yet; unit RED/GREEN proves all invariants.

### Phase B — Pure reducer and accepted mechanics plan

Create mutation/capacity reducers, one plan-local indexed `ResourceHistoryWorkingSet` with incremental admission and one final freeze, replay/event indexes, source route catalog, trigger DAG, bounded pending receipt model, immutable full plan, and full-fingerprint cache. The reducer must not rebuild, resort, or refingerprint the untruncated canonical history per mutation. Refactor Effect Task 6/8 plans to be a subplan of `AcceptedMechanicsPlan` without executing periodic resource components yet.

### Phase C — Canonical validation/publication/bootstrap

Add response mapping, resource raw/canonical validation, snapshot/rollback tracking, bootstrap roots, lease preflight, atomic publication, post-validation, bounded repair, and failure copy. Commands can initialize/test the ledger, but final merge remains blocked until all old authorities are removed.

### Phase D — Owner and ordinary Mortal cutover

Cut player, named NPC, vehicle, anonymous/group combat, item resource/durability, ordinary damage/heal/recovery/cost/use/repair/fire/reload, GM context, and associated UI reads to common owners/mutations/projections. Remove legacy mappings/validators/writers in the same domain slice.

### Phase E — Afterlife cutover

Cut spiritual action points, Guardian/Shining per-return gacha attempts, and numeric blessing rerolls to registered formulas and common mutations. Preserve currencies, progression, relations, faction ledgers, and spiritual axes. Update matrix/examples/manifests and run conditional afterlife docs/full validation.

### Phase F — Resume Effect Task 9

Add periodic damage/restore, resource-event triggers, finite graph execution, bounded story requests/receipts, downstream mutations, effect lifetime advancement, terminal cleanup, and complete resource/effect atomic publication. Reuse one shared owner-capacity materialization planner across Mortal and afterlife owners; a first afterlife profile preserves the exact Actor-Materialized `actorId`, uses `materializationId` only as the same-turn resource handoff, and never introduces `actorRef` or a second identity. Mark #1535 T042–T043/T047/T049–T050 only after exact evidence.

### Phase G — Projection/privacy and complete breaking cleanup

Finish every console/browser/GM projection, delete all persisted mirrors/fallbacks/legacy fields and obsolete sidecars, migrate active templates/examples/fixtures, add source guard/performance/rollback matrix, run Fast, FullValidation, LifecycleIntegration, final review, and one PreMerge.

## Complexity Tracking

No constitution violation requires an exception. The common owner and accepted mechanics abstractions replace existing duplicated authorities and remain inside the existing runtime/test projects.

The T058 Shining survival cutover uses one immutable registered-outcome draft and the existing common plan. Direct health/energy/poise losses run first; exact 20% recovery runs in `registered_system_outcome`; soul consumption and world-event downgrade are projected from those exact transitions and published under the same lease/before-image/rollback boundary. The old runtime percentage restorer and its resource-state/history write path are removed. This is client-owned composition over the existing GM resource command/event contract, so no new GM-authored Shining or afterlife field exists; active prompt/example migration remains deliberately centralized in T107–T111 rather than introducing an interim contract.

T058 evidence: planner derived-loss/replay `TestResults/test-lanes/20260816-020531-411-8556-71b000b3ecd84df8b1b70c7c9c63b2cc-focused/summary.json` (`23/23`); full Shining runtime/draft control `20260816-020230-594-29016-5b5913f1c21145448b61f98940e94062-focused` (`21/21`); Mortal cutover publication, late soul/world mutation rollback, legacy-runtime no-op, and ordinary/legacy checks `20260816-020444-336-55044-fb5cc40a22514ddf8a5fb4e43e7e5f56-focused` (`7/7`). All exited `0`, timed out `false`, reported duplicate IDs `0`, and completed owned-tree cleanup.
