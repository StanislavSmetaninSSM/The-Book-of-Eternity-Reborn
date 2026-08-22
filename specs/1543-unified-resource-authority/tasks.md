---
description: "Dependency-ordered TDD tasks for the unified resource authority"
---

# Tasks: Unified Resource Authority

**Input**: Design documents from `specs/1543-unified-resource-authority/`

**Source issues**: [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543); blocked consumer [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

**Prerequisites**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md), and approved [Superpowers design](../../docs/superpowers/specs/2026-08-15-unified-resource-authority-design.md).

**Tests**: Every behavior task follows RED → GREEN → focused verification. No runtime migration, compatibility reader, dual write, raw fallback, or GitHub Actions may be added.

**Merge boundary**: TDD slices may exist only on the unmerged feature branch. #1543 is not complete and no integration PR is merge-ready until all included domain authorities, Effect Task 9, projections, prompts/examples/templates, legacy removals, and final gates are complete.

## Phase 1: Setup and Executable Inventory

**Purpose**: Lock scope, current evidence, exact writers/readers, and the breaking cutover boundary before behavior changes.

- [X] T001 Confirm issues #1543 and #1535 are open, #1535 records the #1543 blocker, branch/worktree are `1535-effect-materialization` and `E:/Games/worktrees/boe-1535-effect-materialization`, and record the status in `docs/superpowers/plans/2026-08-15-unified-resource-authority.md`
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, every `specs/1543-unified-resource-authority/*.md`, every `contracts/*.md`, `docs/testing.md`, and `docs/superpowers/specs/2026-08-15-unified-resource-authority-design.md`; record implementation conflicts and resolutions in `specs/1543-unified-resource-authority/research.md`
- [X] T003 Capture one current meaningful Fast baseline with `scripts/test-csharp.ps1` and record exact summary path/count/time/cleanup in `specs/1543-unified-resource-authority/quickstart.md`; do not invoke GitHub Actions
- [X] T004 [P] Inventory every active resource writer/mapping/validator/normalizer in `BookOfEternityClient/` and reconcile the exact production path table in `specs/1543-unified-resource-authority/plan.md`
- [X] T005 [P] Inventory every resource reader/projection/action gate in `BookOfEternityClient/` and reconcile console/browser/GM consumers in `specs/1543-unified-resource-authority/plan.md`
- [X] T006 [P] Inventory every active Mortal/afterlife prompt/example/manifest/template/fixture occurrence of removed authority fields in `Rules/`, `TaskGuides/`, `OtherGuides/`, `Examples/`, `FileSystemExample/`, `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, and `BookOfEternityClient/game_master_daemon.ps1`; record the cutover matrix in `specs/1543-unified-resource-authority/plan.md`
- [X] T007 Add shared exact fixture/root builders and byte/existence snapshot helpers in `BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.cs`, `ResourceMaterializationTestContext.Owners.cs`, and `ResourceMaterializationTestContext.Publication.cs`

---

## Phase 2: Foundational Contracts and Common Authority

**Purpose**: Establish strict schemas, exact scalar rules, built-ins, common owners, client identity, and no-write after-state records used by every story.

**⚠️ CRITICAL**: No domain cutover begins until this phase is GREEN.

- [X] T008 [P] Add RED wrong-root/null/empty/whitespace/duplicate/unknown/client-owned-field/limit tests in `BookOfEternityClient.Tests/ResourceMaterializationContractTests.cs`
- [X] T009 Implement strict duplicate-safe roots, exact identifiers/numbers, coefficient/scale quantum/arithmetic proofs, closed tokens, and technical limits in `BookOfEternityClient/Services/ResourceMaterializationContract.cs` until T008 is GREEN
- [X] T010 [P] Add RED built-in/setting definition, materialization seal, initialization, capacity variant, owner/operation, unit, case/confusable, and no-update tests in `BookOfEternityClient.Tests/ResourceDefinitionCatalogTests.cs`
- [X] T011 Implement definition parsing, built-in version-1 catalog, proposal validation/sealing, exact lookup, canonical sort, and no-revision rules in `BookOfEternityClient/Services/ResourceDefinitionCatalog.cs` until T010 is GREEN
- [X] T012 [P] Add RED registered/fixed/instance capacity and exact owner-input fingerprint tests for health, spirit focus, Guardian/Shining gacha, and blessing rerolls in `BookOfEternityClient.Tests/ResourceCapacityFormulaCatalogTests.cs`
- [X] T013 Implement the closed capacity/initialization formula registry with exact decimal results and no expression/path/method selection in `BookOfEternityClient/Services/ResourceCapacityFormulaCatalog.cs` until T012 is GREEN
- [X] T014 [P] Add RED state-root/coordinate/chronology/capacity-binding/duplicate/confusable/quantum/current-max/state/limit tests in `BookOfEternityClient.Tests/ResourceStateContractTests.cs`
- [X] T015 Implement immutable state entries, coordinate index, canonical ordering, capacity agreement, and state fingerprint in `BookOfEternityClient/Services/ResourceStateContract.cs` until T014 is GREEN
- [X] T016 [P] Add RED history-root/transition/source/continuity/immutable/replay/conflicting-replay/terminal/duplicate/confusable/execution-sequence/capacity-disposition/static-initialization/max-scale-exactness tests in `BookOfEternityClient.Tests/ResourceHistoryStateTests.cs`
- [X] T017 Implement immutable history parsing, replay index, chronology continuity, append helpers, sealed static initialization and explicit capacity-disposition validation, exact representability/cross-product arithmetic, client-owned phase/DAG execution ordering, canonical ordering, and history fingerprint in `BookOfEternityClient/Services/ResourceHistoryState.cs` until T016 is GREEN
- [X] T018 [P] Add RED owner catalog tests for player, NPC, anonymous combatant, group member, vehicle, item, afterlife actor/conflict side/scope, same-turn refs, capabilities, realm, lifecycle, duplicate/confusable/historical/name/index rejection in `BookOfEternityClient.Tests/ResourceOwnerAuthorityTests.cs`
- [X] T019 [P] Add RED common combatant/member identity tests for all refs consumed without effects/resources, named NPC binding, group reorder, forbidden submitted IDs, duplicate/confusable refs, and residual canonical refs in `BookOfEternityClient.Tests/CombatantIdentityStateTests.cs`
- [X] T020 Replace `BookOfEternityClient/Services/EffectCombatantIdentityState.cs` with common `BookOfEternityClient/Services/CombatantIdentityState.cs`, allocate/rewrite combatant and member IDs once, and update `EffectTargetAuthority.cs`, `EffectAcceptedTurnPlan.cs`, `EffectAcceptedTurnPlanner.cs`, and their tests until T019 is GREEN
- [X] T021 Implement composed exact owner/capability/ref/fingerprint authority in `BookOfEternityClient/Services/ResourceOwnerAuthority.cs` until T018 is GREEN
- [X] T022 [P] Add RED immutable plan record, exact before-image, touched/consumed path, full authority fingerprint, one random ID allocation, and failed-validation invalidation tests in `BookOfEternityClient.Tests/AcceptedMechanicsPlanCacheTests.cs`
- [X] T023 Define `AcceptedMechanicsInput`, `AcceptedMechanicsPlan`, `CanonicalBeforeImage`, authority fingerprint, after-image, event, and planning result records in `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs`
- [X] T024 Implement one-instance full-input fingerprint and validated/consumed cache in `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs` until T022 is GREEN
- [X] T025 Run the Phase 2 focused filters from `specs/1543-unified-resource-authority/quickstart.md`, record RED/GREEN summary paths in `docs/superpowers/plans/2026-08-15-unified-resource-authority.md`, review the Phase 2 diff, and commit the foundation slice

**Checkpoint**: Strict resource/owner/plan types exist, effect targets use common combat identity, and no domain value has yet been migrated.

---

## Phase 3: User Story 1 — One Trustworthy Resource Value (Priority: P1)

**Goal**: Materialize sealed definitions and one exact current state for every supported coordinate with no direct or legacy authority.

**Independent Test**: Bootstrap and setting-expand definitions/states for representative owners; direct/ambiguous/legacy/malformed state produces issues and zero canonical writes.

- [x] T026 [P] [US1] Add RED response/model/mapping contract tests for `resourceDefinitionCreations`, `resourceCapacityChanges`, and `resourceChanges` in `BookOfEternityClient.Tests/ResourceMaterializationContractTests.cs`; executable absence guards for player/item legacy mappings remain owned by T056/T063 at their atomic cutovers
- [X] T027 [P] [US1] Add RED raw/canonical validation tests for pristine bootstrap, strict roots, direct definition/state/history mutation, legacy fields, same-turn definition/owner refs, wrong realm, and command consumption in `BookOfEternityClient.IntegrationTests/ResourceMaterializationValidationTests.cs`
- [X] T028 [P] [US1] Add RED canonical publication tests for definitions/state/history/commands, untouched subtree preservation, literal-null/malformed late changes, full-fingerprint TOCTOU, and post-state agreement in `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Resources.cs`
- [x] T029 [US1] Add resource response properties in `BookOfEternityClient/Models/GameResponse.cs`; remove player/item legacy response properties only with their T057/T065 consumer cutovers
- [x] T030 [US1] Map the three resource fields to `game_state/resources/resource_commands.json`; remove legacy player/item resource mappings only with their T057/T065 consumer cutovers
- [x] T031 [US1] Implement duplicate-safe resource command parsing and definition/capacity/ordinary selector validation in `BookOfEternityClient/Services/ResourceAcceptedTurnInputComposer.cs`
- [X] T032 [US1] Implement raw, continuity, direct-mutation, legacy-authority, owner/definition/state/history/command, and canonical agreement phases in `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs` until T027 is GREEN
- [X] T033 [US1] Invoke resource raw validation before effect raw planning and invalidate the common plan on every early failure in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [X] T034 [US1] Add resource roots to canonical accumulated/backup/rollback contours and replace independent effect publication entry with common mechanics publication in `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- [X] T035 [US1] Implement lease-bound exact preflight and atomic definition/state/history/command publication in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs` until T028 is GREEN
- [X] T036 [US1] Add new-game/bootstrap creation of built-in definitions, player health/energy/poise state, and one immutable initialize-history row per created coordinate in `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` and validate exact state/history chronology agreement in `BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs`
- [X] T037 [US1] Add resource files to accepted pending-turn snapshots, rollback baselines, session cleanup, save/load preservation, and incompatible-legacy detection in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`, `BookOfEternityClient/Services/LiveTurnPreparationService.cs`, and existing save/load validation paths
- [X] T038 [US1] Run US1 focused unit/integration/bootstrap filters, record evidence and reviewed touched paths/rollback scope in `specs/1543-unified-resource-authority/quickstart.md`, and commit the US1 slice

---

## Phase 4: User Story 2 — Deterministic Ordinary Resource Changes (Priority: P1)

**Goal**: Reduce every authorized mutation through exact ordered arithmetic, replay, capacity, events, and finite graph rules.

**Independent Test**: Apply representative operations and capacity changes across all numeric/order/replay boundaries without any file reads/writes in the reducer.

- [X] T039 [P] [US2] Add RED damage/restore/spend/gain, integer/decimal/quantum, min/max reject/clamp, overflow, non-commutative order, exact replay, conflicting replay, and one-seed/incremental-append/one-freeze history working-set tests in `BookOfEternityClient.Tests/ResourceMutationReducerTests.cs`
- [X] T040 [P] [US2] Add RED initialize/reconfigure preserve/clamp/scale, suspend/resume/retire, inexact ratio, stale formula binding, and terminal history tests in `BookOfEternityClient.Tests/ResourceMutationReducerTests.cs`
- [X] T041 [US2] Implement plan-local `ResourceHistoryWorkingSet` in `BookOfEternityClient/Services/ResourceHistoryWorkingSet.cs` and source-neutral one-mutation/capacity/lifecycle reducers returning immutable ledger/event/transition results without rebuilding canonical history per mutation in `BookOfEternityClient/Services/ResourceMutationReducer.cs` until T039–T040 are GREEN
- [X] T042 [P] [US2] Add RED closed source-route, source evidence, phase/priority/policy, same-turn source, unsupported operation, and GM override tests in `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs`
- [X] T043 [US2] Implement the closed ordinary/local/system/effect source-to-phase/priority/policy registry in `BookOfEternityClient/Services/ResourceMutationSourceCatalog.cs` until T042 is GREEN
- [X] T044 [P] [US2] Add RED four-phase ordering, dependency-constrained per-turn execution sequence, applied/depleted/filled event, exact replay suppression, invalid sibling, and complete no-after-image tests in `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs`
- [X] T045 [P] [US2] Add RED graph duplicate/missing-dependency/cycle/1,025-node/depth-33/stable-order/nested-resource-event tests in `BookOfEternityClient.Tests/ResourceTriggerGraphTests.cs`
- [X] T046 [US2] Implement the immutable working ledger, exactly one indexed history working set per plan, four-phase traversal, resource event propagation, finite DAG executor, and one final history freeze/sort/fingerprint in `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs` until T044–T045 are GREEN
- [X] T047 [US2] Compose accepted events by exact command ordinal and bind every GM/local/system mutation to one replay key in `BookOfEternityClient/Services/ResourceAcceptedTurnInputComposer.cs`
- [X] T048 [US2] Wire successful raw validation to `AcceptedMechanicsPlanCache.GetOrBuildValidated` and publication to exact retrieval/consume in `ValidationService.ResourceMaterialization.cs` and `CanonicalStateNormalizer.AcceptedMechanics.cs`
- [X] T049 [US2] Add state/history/event post-seal agreement and full canonical validation calls in `ValidationService.ResourceMaterialization.cs`
- [X] T050 [US2] Run US2 reducer/planner/graph filters, record evidence and purity/no-write review in `specs/1543-unified-resource-authority/quickstart.md`, and commit the US2 slice

---

## Phase 5: User Story 3 — Complete Owner Lifecycle Across the Game (Priority: P1)

**Goal**: Cut player, NPC, vehicle, combat, group member, item, and afterlife owner creation/continuity/terminal cleanup to common resource authority.

**Independent Test**: Create, move/enter, reorder, suspend, retire, and remove every owner family; exact values follow identity and no orphan/duplicate/positional state exists.

### Owner-plan integration

- [X] T051 [P] [US3] Add RED player/NPC/same-turn NPC/named-NPC-in-combat/anonymous combatant/group member/vehicle lifecycle tests in `BookOfEternityClient.IntegrationTests/ResourceOwnerMaterializationTests.cs`, `ResourceCombatOwnerTests.cs`, and `ResourceVehicleOwnerTests.cs`
- [X] T052 [P] [US3] Add RED inventory/NPC/location/offscreen item movement, split/merge/destruction/nonempty removal, capacity, and same-turn item tests in `BookOfEternityClient.IntegrationTests/ResourceItemOwnerTests.cs`
- [X] T053 [P] [US3] Add RED persistent afterlife actor/conflict side/realm/scope/return-cycle creation and retirement tests in `BookOfEternityClient.IntegrationTests/ResourceAfterlifeOwnerTests.cs`
- [X] T054 [US3] Compose player/NPC/vehicle/combat/group/item/afterlife owner exports from validated owner after-images in `BookOfEternityClient/Services/ResourceAcceptedTurnInputComposer.cs` until T051–T053 owner-resolution cases are GREEN
- [X] T055 [US3] Publish all accepted combatant/member permanent IDs even without resource/effect commands and reject residual refs in `CombatantIdentityState.cs`, `ValidationService.QuestsRivalsFactionsAndWorld.cs`, and common publication

### Mortal player cutover

- [X] T056 [P] [US3] Add RED bootstrap/ordinary player health-energy-poise and legacy percentage/delta rejection tests in `BookOfEternityClient.IntegrationTests/MortalResourceCutoverTests.cs`
- [X] T057 [US3] Remove persisted player health/energy/poise fields and delta validation/mapping/normalization from `ValidationService.PlayerAndInventory.cs`, `ValidationService.MathAssistant.cs`, `StateManager.cs`, `GameEngine.TurnLifecycle.cs`, and `GameResponse.cs`; route accepted operations through common mutations until T056 is GREEN
- [X] T058 [US3] Convert Shining survival/restoration bootstrap outcomes to registered player resource mutations in `BookOfEternityClient/Services/ShiningBlessingEffectState.cs` without touching currencies or boolean entitlements

### NPC and combat cutover

- [X] T059 [P] [US3] Add RED named NPC health continuity, vehicle health/create/update/move/destroy continuity, individual health/poise, group-member resources/reorder/defeat, no positional arrays, and legacy rejection tests in `BookOfEternityClient.IntegrationTests/ResourceCombatIntegrationTests.cs` and `ResourceVehicleIntegrationTests.cs`
- [X] T060 [US3] Remove NPC and vehicle current/max health authority and resolve their resource owners in `ValidationService.NpcWorldAndMeta.cs`, `ValidationService.MetaCodexAndAchievements.cs`, `ValidationService.InventoryNpcWorldCrossRefs.cs`, `StorageTransportMoveService.cs`, and NPC/vehicle normalizer/consumer paths until T059 named-NPC and vehicle cases are GREEN
- [X] T061 [US3] Replace individual `currentHealth/currentPoise` and group `healthStates[]` with owner/member bindings and resource mutations in `ValidationService.QuestsRivalsFactionsAndWorld.cs`, combat response handling, `EffectCarrierCatalog.cs`, and common publication until T059 combat cases are GREEN
- [X] T062 [US3] Add group member lifecycle/terminal companion after-images and exact rollback tracking in `AcceptedMechanicsPlanner.cs` and `CanonicalStateNormalizer.AcceptedMechanics.cs`

### Item cutover

- [X] T063 [P] [US3] Add RED item durability/charges/ammunition/generic reserve use/repair/reload/fire/move/destroy plus legacy command/field rejection tests in `BookOfEternityClient.IntegrationTests/ResourceItemIntegrationTests.cs`
- [X] T064 [US3] Replace item `durability/maxDurability` and item sidecar current/max validation with resource capabilities/capacity bindings in `BookOfEternityClient/Services/MortalItemMaterializationContract.cs` and `ValidationService.PlayerAndInventory.cs`
- [X] T065 [US3] Route player/NPC inventory resource operations through common commands/mutations and remove `inventoryItemsResources`/`NPCInventoryResourcesChanges` application in `FileMapping.cs`, `CanonicalStateNormalizer.InventorySidecars.cs`, `CanonicalStateNormalizer.FactionAndInventoryHelpers.cs`, and `ValidationService.NpcWorldAndMeta.cs`
- [X] T066 [US3] Bind item move/split/merge/destruction/consumption to resource continuity/retirement in `MortalItemTransitionWriter.cs`, `MortalItemTransitionWriter.Stacks.cs`, and `CanonicalStateNormalizer.MortalItems.cs` until T063 is GREEN
- [X] T067 [US3] After T087–T092 move every item detail/action consumer to `ResourceProjectionService`, delete the obsolete `game_state/inventory/item_resources.json` reader/template surface; retain only strict incompatible-save rejection and never add raw-ledger or legacy fallback

### Afterlife owner/capacity cutover

- [X] T068 [P] [US3] Add RED spirit-focus player/opposition action point, cost/recovery, conflict start/close, stale formula, and no legacy pool tests in `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.cs`
- [X] T069 [P] [US3] Add RED Guardian/Shining gacha spend/reset/capacity and blessing reroll entitlement resource tests in `BookOfEternityClient.Tests/ResourceCapacityFormulaCatalogTests.cs` and `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.cs`
- [X] T070 [US3] Replace `activeConflict.actionEconomy` current/max arithmetic with scoped resource owners/mutations while preserving action-cost audit and spiritual axes in `AfterlifeSpiritualConflictState.cs`, `AfterlifeSpiritualConflictTurnPreviewService.cs`, `ValidationService.AfterlifeSpiritualConflict.cs`, and its normalizer
- [X] T071 [US3] Replace Guardian/Shining `chargesUsedThisReturn/chargesPerReturn` arithmetic with resource capacity/mutations while preserving return-cycle/gacha history in `GuardianGachaChargeRules.cs`, `ShiningAbodeState.cs`, `ShiningAbodeState.Gacha.cs`, `GameEngine.IncarnationAndAfterlife.cs`, `GameEngine.MainMenu.cs`, and `CanonicalStateNormalizer.SharedAndSoulHelpers.cs`
- [X] T072 [US3] Replace numeric blessing reroll mirrors with persistent actor resource entries while retaining boolean/free-shape/free-retune entitlements in `ShiningBlessingEffectState.cs` and its consumers
- [X] T073 [US3] Prove currencies, progression, relationships, spiritual axes, and faction accounting remain outside by adding negative admission/source-guard cases in `BookOfEternityClient.Tests/ResourceContractSourceGuardTests.cs`
- [X] T074 [US3] Run all US3 owner/Mortal/combat/item/afterlife focused filters, record evidence and removed-authority inventory review in `specs/1543-unified-resource-authority/quickstart.md`, and commit the US3 slice

---

## Phase 6: User Story 4 — Effects Use the Same Resource Model (Priority: P1)

**Goal**: Resume #1535 Task 9 with periodic operations, resource-event triggers, bounded story receipts, and lifetime cleanup inside the common plan.

**Independent Test**: Ordinary and effect-equivalent operations yield the same resource/history semantics; nested triggers and receipts remain deterministic, bounded, and atomic.

- [X] T075 [P] [US4] Add RED periodic damage/restore component-to-coordinate, unsupported resource/owner, quantum/bounds, replay, and 100-run ordinary-versus-effect byte-equivalent state/history determinism tests in `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs`
- [X] T076 [P] [US4] Add RED resource event trigger ordering, depletion/fill, nested chain, cycle/expansion, effect uses/lifetime, and terminal cleanup tests in `BookOfEternityClient.Tests/ResourceTriggerGraphTests.cs` and `EffectLifecycleSchedulerTests.Lifetime.cs`
- [X] T077 [P] [US4] Add RED full player/NPC/vehicle/combat/afterlife periodic publication where the effect target catalog permits the owner, resource/effect atomicity, and source/target disappearance tests in `BookOfEternityClient.IntegrationTests/EffectResourceMaterializationTests.cs`
- [X] T078 [US4] Resolve periodic component payloads to exact `ResourceCoordinate` and internal mutations in `EffectAcceptedTurnPlanner.cs`, `EffectComponentProfiles.cs`, and `AcceptedMechanicsPlanner.cs` until T075 is GREEN
- [X] T079 [US4] Feed actual resource events into effect trigger selection/execution and return downstream mutations to the same reducer in `AcceptedMechanicsPlanner.cs` until T076 is GREEN
- [X] T080 [US4] Advance effect uses/time/scene/source/condition lifetime and terminal identity/index cleanup after graph execution in `EffectLifecycleScheduler.cs`, `EffectAcceptedTurnPlanner.cs`, and common plan publication
- [X] T081 [P] [US4] Add RED bounded request/receipt schema, privacy, no-state-change, in-bound delta, missing/stale/partial/extra/replay/cross-target/wrong-operation/out-of-bound tests plus proof that deterministic ordinary operations can never create pending work in `BookOfEternityClient.Tests/ResourcePendingResolutionTests.cs`
- [X] T082 [P] [US4] Add RED pending/full-turn resubmission, zero pre-receipt mechanics, receipt-to-mutation, consumption/replay, rollback, and output suppression tests in `BookOfEternityClient.IntegrationTests/ResourcePendingResolutionIntegrationTests.cs`
- [X] T083 [US4] Implement bounded technical pending state, safe GM packet, receipt parsing, replay evidence, and terminal consumption in `BookOfEternityClient/Services/ResourcePendingResolutionState.cs` until T081 is GREEN
- [X] T084 [US4] Integrate pending requests/receipts into `AcceptedMechanicsPlanner.cs`, `ValidationService.ResourceMaterialization.cs`, `CanonicalStateNormalizer.AcceptedMechanics.cs`, and effect input composition until T082 is GREEN
- [X] T085 [US4] Reconcile #1535 Task 9 completion/evidence and interfaces in `specs/1535-complete-effect-materialization/tasks.md`, `plan.md`, `data-model.md`, `quickstart.md`, and effect contracts; migrate the remaining test-only `NormalizeEffectsAsync` callers, retire the independent effect cache/publication handoff after every caller uses the common accepted-mechanics plan, and do not mark later unimplemented lifecycle work complete
- [X] T086 [US4] Run US4 effect/reducer/graph/pending integration filters, record exact RED/GREEN, rollback, and no-effect-only-adapter evidence in `specs/1543-unified-resource-authority/quickstart.md`, and commit the US4 slice

---

## Phase 7: User Story 5 — Safe and Consistent Player/GM Projections (Priority: P2)

**Goal**: Derive all ordinary resource displays/actions from accepted state with console/browser parity and recursive privacy.

**Independent Test**: Visible resources render equivalently; hidden/internal/malformed resources produce no leak or raw fallback.

- [X] T087 [P] [US5] Add RED visible/owner-visible/hidden/GM-only/localization/unit/percentage/recent-delta/action/unavailable tests in `BookOfEternityClient.Tests/ResourceProjectionServiceTests.cs`
- [X] T088 [P] [US5] Add RED recursive internal ID/event/history/path/pending/receipt/validation/repair/agent privacy tests in `BookOfEternityClient.Tests/ResourcePlayerPrivacyTests.cs`
- [X] T089 [US5] Implement immutable accepted snapshot parsing and safe player/GM projections in `BookOfEternityClient/Services/ResourceProjectionService.cs` until T087–T088 are GREEN
- [X] T090 [US5] Populate in-memory player status resources from `ResourceProjectionService` while preserving narrative condition/money in `BookOfEternityClient/Core/StateManager.cs` and `Models/GameState/AggregatedGameState.cs`
- [X] T091 [US5] Cut status bar, console status/stats/meta, agent console, and shared Mortal result builder to the projection in `UI/GameInterface.cs`, `ExplorerMode.WorldAndStatus.cs`, `ExplorerMode.MetaStoryAndStatus.cs`, `ExplorerUniversalMetaCommandResultBuilder.cs`, `ExplorerMortalWorldCommandResultBuilder.cs`, and `GameEngine.AgentConsole.cs`
- [X] T092 [US5] Cut NPC/vehicle/combat/item detail and action eligibility to exact projections in `ExplorerMode.Npcs.ListAndDetails.cs`, `ExplorerMode.Npcs.Rendering.cs`, `ExplorerMode.MetaLoreAndTravel.cs`, `ExplorerMode.Inventory.cs`, `ExplorerMortalWorldCommandResultBuilder.cs`, and `ExplorerLifecycleLocalTurnCommandResultBuilder.cs`
- [X] T093 [US5] Cut afterlife spiritual conflict/gacha/status/previews to projections in `ExplorerMode.Afterlife.SpiritualConflict.cs`, `ExplorerMode.Afterlife.GuardiansProjectsTrade.cs`, `ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs`, `ExplorerMode.Afterlife.ShiningAbode.Actions.cs`, `ExplorerMode.Afterlife.StatusAudit.cs`, and `ExplorerAfterlifeCombatCommandResultBuilder.cs`
- [X] T094 [US5] Cut browser game-screen/status DTO construction to projection-only values and no raw fallback in `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- [X] T095 [P] [US5] Add console/browser/GM-context semantic parity and missing/wrong-type/hidden projection integration tests in `BookOfEternityClient.IntegrationTests/ResourceConsoleBrowserParityTests.cs`
- [X] T096 [US5] Add fixed Russian generic failure copy and bounded omission-only repair projection in `ResourcePlayerFailureMessages.cs` and `ResourceRepairPacketBuilder.cs`; integrate operator-only diagnostics in `GameEngine.ValidationAndRepair.cs`
- [X] T097 [US5] Run US5 projection/privacy/parity filters and manual status/NPC/combat/item/afterlife spot-check, record evidence and serialized DTO review in `specs/1543-unified-resource-authority/quickstart.md`, and commit the US5 slice

---

## Phase 8: User Story 6 — Atomic Failure, Repair, and Breaking Cutover (Priority: P2)

**Goal**: Prove byte/existence atomicity, remove all legacy authorities/fallbacks, and synchronize active GM/player contracts/templates.

**Independent Test**: Failure injection restores every touched path/output; old saves and legacy commands fail; active templates/examples validate with only the ledger.

- [X] T098 [P] [US6] Add RED failure injection after each owner/resource/effect/pending/command/output write and post-validation in `BookOfEternityClient.IntegrationTests/ResourceMaterializationLifecycleTests.cs`
- [X] T099 [P] [US6] Add RED stale definition/owner/state/history/source/target/carrier/index/event/command/pending late-mutation and zero-write cases in `CanonicalStateNormalizerTests.Resources.cs`
- [X] T100 [US6] Extend accepted-turn rollback/touched-path/output suppression to every common plan path in `GameEngine.SessionAndSnapshots.cs`, `GameEngine.ValidationAndRepair.cs`, and `CanonicalStateNormalizer.AcceptedMechanics.cs` until T098–T099 are GREEN
- [X] T101 [US6] Add protected-versus-one-bounded-omission repair classification and exact full-turn resubmission tests in `ResourceMaterializationLifecycleTests.cs` and implement them in `ResourceRepairPacketBuilder.cs`
- [X] T102 [P] [US6] Add RED source guard for every removed active response field, persisted state field, validator, writer, reader, fallback, prompt/example/template occurrence, and unauthorized out-of-scope admission in `BookOfEternityClient.Tests/ResourceContractSourceGuardTests.cs`
- [X] T103 [US6] Remove remaining legacy production fields/readers/writers/validators identified by T004–T006 from `BookOfEternityClient/` until the T102 production contour is GREEN
- [X] T104 [US6] Add RED ordinary-source replay/ordinal/target-binding tests in `BookOfEternityClient.Tests/ResourceAcceptedTurnInputComposerTests.cs` and `BookOfEternityClient.IntegrationTests/ResourceMaterializationValidationTests.cs`, then implement client-derived accepted-event source authority for `action_cost`, `combat_outcome`, and `narrative_outcome` in `BookOfEternityClient/Services/ResourceAcceptedTurnInputComposer.cs`, `ResourceMutationSourceCatalog.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs`
- [X] T105 [US6] Add RED group-to-detached-row continuity, ambiguity, resource-owner, effect-target, and carrier tests in `BookOfEternityClient.IntegrationTests/ResourceCombatIntegrationTests.cs` and `EffectMaterializationValidationTests.Sources.cs`, then implement exact `memberId` identity across `MortalResourceOwnerComposer.cs`, `EffectAcceptedTurnInputComposer.cs`, `EffectCarrierCatalog.cs`, and the owning combat validators
- [X] T106 [US6] Add RED recipient-scope, wrong-player, replay, malformed-envelope, and no-local-mutation tests for FullParty resource packets in `BookOfEternityClient.IntegrationTests/ResourceFullPartyInteractionTests.cs`, then implement `contracts/resource-full-party-interaction.md` and the closed `otherPlayersInteractions[playerId][]` resource-command packet validation/accepted staging in `ValidationService.MetaCodexAndAchievements.cs` without granting local ledger authority
- [X] T107 [US6] Update Mortal API/daemon/launcher/task/rule contracts in `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, `BookOfEternityClient/Launcher/CLI_Launch_Script.md`, `TaskGuides/CLI_Step_Main.txt`, and the active `Rules/Block_*.txt` inventory with the new command/owner/no-direct-write rules
- [X] T108 [US6] Add/update worked Mortal examples including `Examples/E_CLI_Mortal_Resources.txt`, active combat/item/status examples, and `Examples/example_validation_manifest.json`; remove all legacy authority from active examples
- [X] T109 [US6] Update afterlife action-point/gacha/reroll contracts and exact no-currency boundary in `OtherGuides/Afterlife_Contract_Matrix.md`, `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, `Examples/E_CLI_Afterlife_Turns.txt`, relevant `Rules/Block_21.txt`/`Block_32_Guardians.txt`, task/daemon guidance, and `example_validation_manifest.json`
- [X] T110 [US6] Replace active template roots and removed fields in `FileSystemExample/game_session/game_state/`, add `FileSystemExample/validator_fixtures/resource_materialization/`, and update fixture manifests/readmes
- [X] T111 [US6] Update documentation/source-guard tests in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`, `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`, and fixture integrity tests until production examples validate
- [X] T112 [US6] Add old technical save incompatibility tests for each removed player/NPC/vehicle/combat/item/afterlife authority in `BookOfEternityClient.IntegrationTests/ResourceMaterializationValidationTests.cs`
- [X] T113 [US6] Run US6 lifecycle, source-guard, docs/example, FullValidation, and template filters; record exact rollback bytes/existence, stale-output suppression, and test evidence in `specs/1543-unified-resource-authority/quickstart.md`, then commit the US6 slice

---

## Phase 9: Performance, Review, and Final Integration

**Purpose**: Prove near-linear behavior, full requirement coverage, clean diff, local gate health, and merge readiness without weakening tests.

- [ ] T114 [P] Add RED/guard cases for definition/owner/state/history catalog scaling, one history working-set seed/freeze per plan, no whole-history rebuild per mutation, and exact 2.5x doubling threshold in `BookOfEternityClient.Tests/ResourceAuthorityScaleTests.cs`
- [ ] T115 [P] Add RED/guard cases for mutation/trigger/plan scaling, 512/1,024/32 boundaries, and exact descriptor counts in `BookOfEternityClient.IntegrationTests/AcceptedMechanicsPlannerScaleTests.cs`
- [ ] T116 Optimize only measured repeated parsing/index construction in `BookOfEternityClient/Services/ResourceHistoryWorkingSet.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs` until T114–T115 are GREEN without changing coverage, limits, caps, filters, or timeout
- [ ] T117 Run one meaningful Fast checkpoint, record exact summary/count/time/duplicates/cleanup in `specs/1543-unified-resource-authority/quickstart.md` and `docs/superpowers/plans/2026-08-15-unified-resource-authority.md`, and do not rerun it immediately before PreMerge
- [ ] T118 Reconcile every FR/SC/contract/data-model item to an implemented task and evidence row in `specs/1543-unified-resource-authority/tasks.md` and `quickstart.md`; leave incomplete boxes unchecked
- [ ] T119 Run `speckit-analyze` on `specs/1543-unified-resource-authority/spec.md`, `plan.md`, and `tasks.md`; resolve every Critical/Important inconsistency in the owned artifacts
- [ ] T120 Perform a fresh read-only code review of the complete #1543/#1535 resource range against owner, arithmetic, replay, graph, pending, TOCTOU, rollback, privacy, GM docs, examples, and no-legacy contracts; record findings/fixes/evidence in `docs/superpowers/plans/2026-08-15-unified-resource-authority.md`
- [ ] T121 Run `git diff --check`, confirm only #1543/#1535 expected paths are changed, confirm no GitHub Actions/settings changes, and inspect all new/untracked files before staging
- [ ] T122 Run the required LifecycleIntegration/FullValidation controls only if not already fresh for the final reviewed tree, then start exactly one final `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge`
- [ ] T123 Record final PreMerge path, exit, timeout, counts, duplicates, cleanup, Fast/conditional-lane evidence, changed files, prompt/docs/example rationale, residual risks, and no-migration/no-Actions status in `quickstart.md`, the Superpowers plan, and the eventual PR summary
- [ ] T124 Commit only reviewed #1543/#1535 paths, record the final clean branch/merge handoff in `specs/1543-unified-resource-authority/quickstart.md`, and present PR options under the repository's owner-approval rules without changing repository visibility/settings or merging without the user's direction

---

## Dependencies and Execution Order

```text
Phase 1 inventory
  -> Phase 2 strict contracts/common identity/cache
    -> US1 canonical definitions/state/publication
      -> US2 reducer/order/replay/graph
        -> US3 all owners and ordinary domain cutover
          -> US4 Effect Task 9 common integration
            -> US5 projection/privacy/parity
              -> US6 rollback/no-legacy/docs/templates
                -> performance/analyze/review/final gates
```

- US1 requires Phase 2.
- US2 requires strict definition/state/history and plan records from Phase 2; it does not require domain cutover.
- US3 requires US1 and US2 so owner lifecycles can initialize/reduce atomically.
- US4 requires US3 because effects must target the same ordinary owner/value authority; no effect-only adapter is permitted.
- US5 requires accepted ordinary/effect state but its isolated projection unit tests can begin after US1.
- US6 requires every domain/projection writer and reader to be known; final source guard cannot pass before US3–US5.
- Final PreMerge requires all stories, docs/examples/templates, Effect Task 9 reconciliation, and fresh review.

## Parallel Opportunities

- Phase 1 inventories T004–T006 are read-only and edit separate plan sections.
- Phase 2 RED tests T008/T010/T012/T014/T016/T018/T019/T022 use separate test files before shared implementation.
- US3 owner RED tests T051–T053 and domain RED tests T056/T059/T063/T068/T069 are independent fixtures before production cutover.
- US4 planner/graph/integration/pending RED tests T075–T077/T081–T082 are separate test contours.
- US5 projection/privacy/parity RED tests T087/T088/T095 are separate files.
- US6 rollback/TOCTOU/source-guard RED tests T098/T099/T102 are separate files; ordinary-source, detached-member, and FullParty packet slices T104–T106 execute sequentially because they share accepted-turn authority surfaces.
- Performance guards T114/T115 are independent before measured optimization.

No parallel task may mutate shared production files concurrently without an explicit execution coordinator. Parallel labels identify review-independent work, not permission to race the shared worktree.

## Independent Test Criteria

- **US1**: Complete built-in/setting definitions and representative owner state accept; direct/legacy/malformed/ambiguous state yields zero writes.
- **US2**: Pure reducer/planner produces exact deterministic values/events/history under boundary/order/replay tests and no file mutation.
- **US3**: Every supported owner, including vehicles, survives creation/movement/entry/reorder/exit or retires correctly with no duplicate/orphan/positional authority.
- **US4**: Ordinary and effect-equivalent operations match; periodic/event/pending flows use one plan and one reducer exactly once.
- **US5**: Console/browser/GM projections are semantically aligned, usable, safe, and have no raw fallback or internal/hidden leak.
- **US6**: Failure injection restores all bytes/prior absence, legacy saves/fields reject, and active prompts/examples/templates validate only the new authority.

## Implementation Strategy

The independently testable technical MVP is US1 after Phase 2, but it is branch-local only and not mergeable as the completed feature. Build in the dependency order above with one reviewer gate and commit per coherent slice. Resume Effect Task 9 only after US3. Treat the entire feature as complete only after US6 and Phase 9 prove there is one persisted authority.
