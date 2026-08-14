---

description: "Dependency-ordered TDD tasks for complete effect materialization"
---

# Tasks: Complete Effect Materialization

**Input**: Design documents from `specs/1535-complete-effect-materialization/`
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
**Prerequisites**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md), approved [Superpowers design](../../docs/superpowers/specs/2026-08-14-effect-materialization-design.md)

**Tests**: Every behavior task follows RED → GREEN → focused verification. No runtime migration or compatibility reader may be added. All verification is local; GitHub Actions remain disabled.

**Organization**: Tasks are grouped by the six independently testable user stories in [spec.md](spec.md). Foundational contract and authority work blocks all stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it edits different files and has no dependency on another incomplete task in the same phase.
- **[Story]**: Maps to one user story from `spec.md`.
- Every task names exact repository paths.

## Phase 1: Setup and Evidence Baseline

**Purpose**: Lock task scope, clean isolation, current repository evidence, and exact verification surfaces before behavior changes.

- [x] T001 Confirm issue #1535 is open/triaged, branch `1535-effect-materialization` is based on current `origin/main`, and `git status --short --branch` contains only #1535 work in `E:/Games/worktrees/boe-1535-effect-materialization`
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/1535-complete-effect-materialization/spec.md`, `plan.md`, `data-model.md`, all `contracts/*.md`, `quickstart.md`, and `docs/superpowers/specs/2026-08-14-effect-materialization-design.md`; record any implementation conflict in `specs/1535-complete-effect-materialization/research.md` before coding
- [x] T003 Record the current local Fast baseline result and dependency-restore prerequisite in `specs/1535-complete-effect-materialization/quickstart.md`; do not run or enable GitHub Actions
- [x] T004 [P] Inventory every active-effect command/carrier/reader/mechanics consumer in `BookOfEternityClient/`, `Rules/`, `OtherGuides/`, `Examples/`, and `FileSystemExample/` and reconcile the concrete path matrix in `specs/1535-complete-effect-materialization/plan.md`
- [x] T005 [P] Add an implementation fixture/path ownership outline for player, NPC, combatant, afterlife profile, spiritual conflict, identity index, command staging, and pending resolution in `BookOfEternityClient.IntegrationTests/EffectMaterializationTestContext.cs`

---

## Phase 2: Foundational Contracts and Authority (Blocking)

**Purpose**: Establish closed source definitions, active envelope, component registry, identity authority, carrier catalog, and one-plan boundary before any owner-specific behavior.

**⚠️ CRITICAL**: No user-story implementation begins until this phase is green.

### Contract and source-definition RED/GREEN

- [x] T006 [P] Add RED closed-envelope tests for required sections, duplicate/unknown fields, wrong types, client-owned fields, and missing pristine versus non-empty legacy carriers in `BookOfEternityClient.Tests/EffectMaterializationContractTests.cs`
- [x] T007 [P] Add RED registered-profile payload tests for all nine initial component profiles, non-finite/out-of-bound values, unknown profiles, and prose-only mechanics in `BookOfEternityClient.Tests/EffectMaterializationContractTests.cs`
- [x] T008 [P] Add RED static `activeEffectDefinitions[]` tests for exact/confusable `definitionKey`, target/realm/component/parameter/stack/lifetime/trigger/removal completeness, and forbidden active identity fields in `BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs`
- [x] T009 Implement the closed canonical instance parser/validator and mode-specific lifetime/stack/trigger/removal/link validation in `BookOfEternityClient/Services/EffectMaterializationContract.cs` until T006 is GREEN
- [x] T010 Implement the discriminated component registry, payload validators, deterministic component metadata, merge rules, and projection descriptors in `BookOfEternityClient/Services/EffectComponentProfiles.cs` until T007 is GREEN
- [x] T011 Implement the embeddable static source-definition contract and parameter-bound validation in `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs` until T008 is GREEN

### Identity and carrier RED/GREEN

- [x] T012 [P] Add RED identity-index tests for closed root/entry/owner/stack/transition shapes, active/terminal agreement, exact/confusable global uniqueness, immutable terminal evidence, and GM-authored index rejection in `BookOfEternityClient.Tests/EffectIdentityStateTests.cs`
- [x] T013 Implement random opaque effect/transition identity allocation, exact/confusable identity rules, index parse/create/validate/update helpers, and terminal history in `BookOfEternityClient/Services/EffectIdentityState.cs` until T012 is GREEN
- [x] T014 [P] Add RED carrier-catalog tests for one logical occurrence, player/NPC/combat buff-debuff/profile/condition coordinates, duplicate cross-carrier occurrence, adjacent wound preservation, and unsupported legacy carrier shape in `BookOfEternityClient.Tests/EffectCarrierCatalogTests.cs`
- [x] T015 Implement allowlisted owner coordinates, canonical carrier parsing, direct-mutation comparison, and one-occurrence reconciliation in `BookOfEternityClient/Services/EffectCarrierCatalog.cs` until T014 is GREEN

### Source/target and plan-cache RED/GREEN

- [x] T016 [P] Add RED exact source authority tests for skill/art/item/wound/quest/location/hazard/faction/event/Fate Card/combat adapters, same-turn effective identity, passive/instantaneous non-promotion, case/confusable/historical/cross-realm rejection, and parameter bounds in `BookOfEternityClient.Tests/EffectSourceAuthorityTests.cs`
- [x] T017 [P] Add RED exact target authority tests for player/NPC/combatant/Guardian/resident/radiant/afterlife actor/spiritual side, same-turn `combatantRef` to client-owned `combatantId`, forbidden GM permanent combatant ID, same-turn actor target, name/index/case/confusable/historical ambiguity, and realm mismatch in `BookOfEternityClient.Tests/EffectTargetAuthorityTests.cs`
- [x] T018 Implement one-pass source catalog composition and closed source-kind adapters in `BookOfEternityClient/Services/EffectSourceAuthority.cs` until T016 is GREEN
- [x] T019 Implement one-pass target catalog composition, actor/profile binding, exact temporary-to-permanent combatant mapping in `BookOfEternityClient/Services/EffectCombatantIdentityState.cs`, stable combat-local anchor support, and realm matching in `BookOfEternityClient/Services/EffectTargetAuthority.cs` until T017 is GREEN
- [x] T020 [P] Add RED plan-cache tests proving one random allocation, same plan instance across raw/companion/commit callers, invalidation on session/snapshot/input/source/target change, and no deterministic GM-derivable ID in `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs`
- [x] T021 Implement immutable plan/result/input records and random identity factory in `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs`
- [x] T022 Implement accepted-input fingerprinting and one-instance cache authority in `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs` until T020 is GREEN

**Checkpoint**: Focused contract, definition, profile, identity, carrier, source, target, and plan-cache filters are GREEN; no gameplay consumer has been migrated yet.

---

## Phase 3: User Story 1 — Trustworthy Active Effect Creation (Priority: P1) 🎯 MVP

**Goal**: Accept only complete source-authorized runtime instances for Mortal player, NPC, and combatants and apply no mechanic from partial state.

**Independent Test**: Representative complete applications create one client-owned instance; every missing/forged/ambiguous section and passive/instantaneous source rejects the whole transition with zero mechanics or writes.

### Tests for User Story 1 (RED first)

- [ ] T023 [P] [US1] Add RED planner tests for `apply`, forbidden submitted post-state, valid player/NPC/combatant final carriers, source parameters, and exact final index/touched paths in `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs`
- [ ] T024 [P] [US1] Add RED raw/composed validation tests for missing source/target/display/component/lifetime/stack/removal, unknown/duplicate fields, direct carrier/index mutation, and pristine missing-carrier initialization in `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.cs`
- [ ] T025 [P] [US1] Add RED same-turn composition tests for accepted item, actor, faction, location, quest/event, wound, and `combatantRef` source/target exports without trusting raw sibling JSON in `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Sources.cs`
- [ ] T026 [P] [US1] Add RED normalizer tests for player, NPC with adjacent wound state, enemy/ally buff-debuff category, transient command consumption, identity-index creation, and post-state agreement in `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Effects.cs`
- [ ] T027 [P] [US1] Add RED mechanics-snapshot tests proving one malformed sibling invalidates all active mechanics and static passive definitions are not double-counted in `BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.cs`

### Implementation for User Story 1

- [ ] T028 [US1] Add `EffectChanges` and `EffectResolutionReceipts` response properties and remove legacy player/NPC effect application properties in `BookOfEternityClient/Models/GameResponse.cs`
- [ ] T029 [US1] Map the two common response fields to transient `game_state/effects/effect_commands.json` and remove legacy application mappings in `BookOfEternityClient/Configuration/FileMapping.cs`
- [ ] T030 [US1] Implement apply parsing, exact source/target resolution including one-time `combatantRef` mapping, source-bound parameter resolution, final component/display/link creation, client identity allocation, and complete final carrier/index planning in `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` until T023 is GREEN
- [ ] T031 [US1] Add raw, composed, continuity, client-owned-field, source/target, carrier/index, and post-seal validation integration in `BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs` until T024 is GREEN
- [ ] T032 [US1] Invoke effect raw/composed phases after applicable source/target materialization plans and before accepted publication in `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`
- [ ] T033 [US1] Add closed `activeEffectDefinitions[]` validation calls to source-owning player/item/quest/wound/combat contracts in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs`, `BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs`, and `BookOfEternityClient/Services/MortalItemMaterializationContract.cs`
- [ ] T034 [US1] Add closed `activeEffectDefinitions[]` validation calls to NPC/faction/location/event/actor source validators in `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`, `ValidationService.MortalFactionMaterialization.cs`, `ValidationService.MortalLocationMaterialization.cs`, and `ValidationService.AfterlifeEntityProfiles.cs` until T025 is GREEN
- [ ] T035 [US1] Implement effect canonical writes after source-owning normalizers, preserve unrelated carrier subtrees, consume/delete transient commands, and write the identity index under the bound lease in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs` until T026 is GREEN
- [ ] T036 [US1] Call `NormalizeEffectsAsync` after all source-owning normalizers in `BookOfEternityClient/Services/CanonicalStateNormalizer.cs` and extend effect state paths in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- [ ] T037 [US1] Implement the immutable accepted all-or-nothing component/read model in `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs` until T027 is GREEN
- [ ] T038 [US1] Replace raw player effect alias reads with `EffectMechanicsSnapshot` in `BookOfEternityClient/Services/CharacteristicsService.cs` and keep static source bonuses separate
- [ ] T039 [US1] Run the US1 focused unit and integration filters from `specs/1535-complete-effect-materialization/quickstart.md`, capture RED→GREEN result paths in the implementation log section of `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`, and commit the US1 slice

**Checkpoint**: Complete Mortal active-effect creation is independently usable; malformed state contributes zero mechanics.

---

## Phase 4: User Story 2 — Deterministic Stacking and Lifetime (Priority: P1)

**Goal**: Resolve every repeated application, tick, trigger, use, lifetime, dispel, removal, suspension, and terminal transition in a stable exactly-once order.

**Independent Test**: All five stack policies and eight lifetime modes pass boundary/replay tests; cycles and duplicate events reject before mutation.

### Tests for User Story 2 (RED first)

- [ ] T040 [P] [US2] Add RED stack-policy tests for independent bounds, stack max behavior, reset/extend refresh, replace terminal/new identity, merge rules, conflicting policy, overflow, and event replay in `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Stacking.cs`
- [ ] T041 [P] [US2] Add RED lifetime tests for turns, uses, until-time equality, scene close, source/condition suspend-expire, authorized permanent/manual, forbidden sentinels, and realm transition in `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Lifetime.cs`
- [ ] T042 [P] [US2] Add RED ordering/trigger tests for phase order, priority/effect/component ordering, periodic damage/restore, event reaction, duplicate event, cycle, and expansion bound in `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Triggers.cs`
- [ ] T043 [P] [US2] Add RED pending-resolution tests for exact request/receipt, stale/partial/cross-target/out-of-bound receipt, one-time consumption, and deterministic components requiring no GM work in `BookOfEternityClient.Tests/EffectLifecycleSchedulerTests.Resolutions.cs`
- [ ] T044 [P] [US2] Add RED composed lifecycle integration tests for apply+remove, apply+expiry, refresh+advance, source loss+reapply, replay, session replacement, and command consumption in `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Lifecycle.cs`

### Implementation for User Story 2

- [ ] T045 [US2] Implement source-authorized stack coordinate resolution and all five policy reducers in `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` until T040 is GREEN
- [ ] T046 [US2] Implement all eight mode-specific continuation/advancement/terminal reducers and exact realm-transition policy in `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` until T041 is GREEN
- [ ] T047 [US2] Implement deterministic scheduler phases, trigger selection/order, periodic operations, event reactions, duplicate-event protection, graph cycle detection, and expansion limits in `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` until T042 is GREEN
- [ ] T048 [US2] Implement `dispel`/`remove` command parsing, exact allowed authority, terminal transitions, replace cleanup, and effect-owned companion disposition in `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- [ ] T049 [US2] Implement client-owned pending resolution root/request construction and exact receipt validation/consumption in `BookOfEternityClient/Services/EffectPendingResolutionState.cs` and `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` until T043 is GREEN
- [ ] T050 [US2] Add `pending_effect_resolutions.json` to protected control validation, snapshot authority, session replacement, and cleanup in `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- [ ] T051 [US2] Integrate due lifecycle events and pending receipts into the cached accepted plan and final carrier/index state in `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` until T044 is GREEN
- [ ] T052 [US2] Run the US2 focused filters and the periodic/trigger quickstart scenarios, record RED→GREEN evidence in `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`, and commit the US2 slice

**Checkpoint**: Reapplication and lifecycle are deterministic and retry-safe across accepted turns.

---

## Phase 5: User Story 3 — One Authority Across Owners and Realms (Priority: P1)

**Goal**: Apply common identity/lifecycle guarantees to persistent afterlife actor effects and specialized spiritual combat conditions without flattening their mechanics.

**Independent Test**: Every supported owner family creates/advances one valid instance; cross-realm/direct-mutation/duplicate carrier cases reject uniformly.

### Tests for User Story 3 (RED first)

- [ ] T053 [P] [US3] Add RED persistent afterlife profile tests for player soul, Guardian, resident, Shining faction head, radiant actor, exact profile binding, one logical carrier, and Shining blessing entitlement exclusion in `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Profiles.cs`
- [ ] T054 [P] [US3] Add RED spiritual condition tests for all five kinds, legal axes, exact conflict side/source, stacking, finite uses/exchanges/scene, counterplay, direct `combatConditions[]` mutation, replay, and terminal history in `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Conditions.cs`
- [ ] T055 [P] [US3] Add RED realm-transition and cross-realm source/target tests for suspend, expire, forbidden carry, stale conflict, and same-turn afterlife actor identity in `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Realms.cs`
- [ ] T056 [P] [US3] Add RED afterlife mechanics snapshot tests proving only legal spiritual axes apply and hidden/GM-only conditions cannot enter player audit sources in `BookOfEternityClient.Tests/EffectMechanicsSnapshotTests.Afterlife.cs`

### Implementation for User Story 3

- [ ] T057 [US3] Add canonical `activeEffects[]` preservation/validation to accepted profiles in `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- [ ] T058 [US3] Add persistent afterlife profile carrier projection and exact actor/profile target/source binding in `BookOfEternityClient/Services/EffectCarrierCatalog.cs`, `EffectTargetAuthority.cs`, and `EffectSourceAuthority.cs` until T053 is GREEN
- [ ] T059 [US3] Implement the specialized `afterlife_combat_condition` adapter between common plan instances and current condition fields in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` and `BookOfEternityClient/Services/EffectComponentProfiles.cs`
- [ ] T060 [US3] Replace direct condition lifecycle authoring with common effect plan input while preserving conflict-specific validation in `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeCombatConditions.cs` and `ValidationService.AfterlifeSpiritualConflict.cs` until T054 is GREEN
- [ ] T061 [US3] Compose profile and spiritual-condition final state in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`, preserving existing profile/conflict normalizers and one logical occurrence
- [ ] T062 [US3] Implement afterlife realm transition and stale conflict/session guards in `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` and `EffectTargetAuthority.cs` until T055 is GREEN
- [ ] T063 [US3] Route legal condition contributions through `EffectMechanicsSnapshot` and existing spiritual-conflict audits without adding generic Mortal stat stacking in `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs` until T056 is GREEN
- [ ] T064 [US3] Run US3 focused afterlife filters, record RED→GREEN evidence in `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`, and commit the US3 slice

**Checkpoint**: Mortal and afterlife owners share identity/lifecycle guarantees; spiritual mechanics remain specialized.

---

## Phase 6: User Story 4 — Complete Mechanics and Safe Player Inspection (Priority: P2)

**Goal**: Expose complete visible mechanics and actions with console/browser parity while suppressing hidden effects and internal DTOs recursively.

**Independent Test**: The same accepted state yields equivalent console/browser facts/actions and zero internal or hidden tokens.

### Tests for User Story 4 (RED first)

- [ ] T065 [P] [US4] Add RED projection tests for visible profile facts, setting-specific component catch-all, lifetime/stacks/source links, hidden/GM-only omission, whole technical DTO signatures, annotated supersets, and adjacent legitimate semantic objects in `BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs`
- [ ] T066 [P] [US4] Add RED Mortal console tests for player/NPC/combatant rows/details, all-or-nothing fallback, in-world Russian copy, action eligibility, stale selector, and effect-only versus wound treatment wording in `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Effects.cs`
- [ ] T067 [P] [US4] Add RED browser tests for equivalent status/NPC/combatant cards, actions, serialized game-screen privacy, malformed authority, and stale/forged selector in `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.Effects.cs`
- [ ] T068 [P] [US4] Add RED afterlife console/browser condition/profile parity and hidden-token audit tests in `BookOfEternityClient.IntegrationTests/EffectAfterlifeAdapterTests.Projection.cs`

### Implementation for User Story 4

- [ ] T069 [US4] Implement accepted-set projection, registered component projection, lifetime/stack/source/link/action facts, visibility rules, and whole internal DTO shape suppression in `BookOfEternityClient/UI/EffectPlayerProjection.cs` until T065 is GREEN
- [ ] T070 [US4] Replace raw player/NPC/combatant effect parsing and fallback authority in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs`, `ExplorerMode.WorldAndStatus.cs`, `ExplorerMode.Npcs.ListAndDetails.cs`, `MortalStatusEffectFallback.cs`, and `ExplorerMortalWorldCommandResultBuilder.cs` until T066 is GREEN
- [ ] T071 [US4] Implement opaque short-lived effect action selectors and current accepted re-resolution in `BookOfEternityClient/UI/ExplorerMortalEffectDetailActions.cs` and `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs` until T067 is GREEN
- [ ] T072 [US4] Update browser result DTO construction for player/NPC/combatant effect status and action facts in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` without exposing permanent IDs
- [ ] T073 [US4] Route spiritual-condition/profile views and audits through the shared adapter in `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs`, `AfterlifeCombatConditionPlayerAuditSanitizer.cs`, and `ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs` until T068 is GREEN
- [ ] T074 [US4] Extend context-aware recursive technical DTO suppression to active-effect identity/pending/repair wrappers without hiding non-effect `route/kind/title/steps/turn/source` semantics in `BookOfEternityClient/UI/MortalItemPlayerProjection.cs`
- [ ] T075 [US4] If typed frontend contracts change, update and verify affected files under `BookOfEternityClient.WebFrontend/src/`; otherwise record the no-frontend-change rationale in `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`
- [ ] T076 [US4] Run US4 focused console/browser/projection filters, perform the manual privacy/parity scenario from `quickstart.md`, record evidence, and commit the US4 slice

**Checkpoint**: Both clients expose the same complete visible semantics and no internal authority.

---

## Phase 7: User Story 5 — Atomic Failure and Bounded Repair (Priority: P2)

**Goal**: Roll back every partial effect transition and offer only exact bounded semantic repair after baseline restoration.

**Independent Test**: Every publication/post-check failure restores full baseline and output; protected failures dispatch nothing; one repairable omission requires coherent complete resubmission.

### Tests for User Story 5 (RED first)

- [ ] T077 [P] [US5] Add RED repair-builder tests for one bounded semantic omission, protected identity/source/target/realm/stack/receipt/history/cycle/direct-mutation errors, protected target files, exact packet serialization, and full-resubmission obligation in `BookOfEternityClient.Tests/EffectRepairPacketBuilderTests.cs`
- [ ] T078 [P] [US5] Add RED real validation-loop tests for baseline-before-dispatch, unavailable snapshot no-dispatch, ready-only no-op rejection, partial-effect-only retry rejection, complete response replay, semantic freshness, worker parity, and session replacement in `BookOfEternityClient.IntegrationTests/EffectMaterializationRepairLifecycleTests.cs`
- [ ] T079 [P] [US5] Add RED failure-injection matrix after every actual effect/carrier/index/pending/companion/output publication and post-check in `BookOfEternityClient.IntegrationTests/EffectMaterializationLifecycleTests.cs`
- [ ] T080 [P] [US5] Add RED privacy tests for helper/report/cleanup/rollback exceptions, persistent operator diagnostics, and zero technical vocabulary/exception text in full caller output in `BookOfEternityClient.IntegrationTests/EffectMaterializationRepairLifecycleTests.Privacy.cs`

### Implementation for User Story 5

- [ ] T081 [US5] Implement exact candidate binding, repairable field catalog, protected code/path/target rules, packet serialization, and coherent resubmission obligations in `BookOfEternityClient/Services/EffectRepairPacketBuilder.cs` until T077 is GREEN
- [ ] T082 [US5] Route effect issues into the existing validation repair request/worker packet without broadening source/target/client authority in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T083 [US5] Restore validated baseline before actionable dispatch, preserve one in-memory full-response obligation, require canonical semantic freshness, and prevent ready-only/partial no-op acceptance in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` until T078 is GREEN
- [ ] T084 [US5] Add every effect command/carrier/index/pending/companion/output path to accepted snapshot and rollback tracking while excluding durable operator diagnostics in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- [ ] T085 [US5] Make effect diagnostic/report/transient cleanup best-effort for ordinary exceptions while preserving session-replacement propagation and one caller-owned rollback in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T086 [US5] Publish all planned effect paths through the bound canonical write lease and post-validate the complete carrier/index/mechanics set in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs` until T079 is GREEN
- [ ] T087 [US5] Replace every reachable outer player failure/cancellation/stall/retry exception path with fixed in-world Russian copy and operator-only exact logging in `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` until T080 is GREEN
- [ ] T088 [US5] Run US5 focused repair filters and `LifecycleIntegration -Filter "FullyQualifiedName~EffectMaterializationLifecycleTests"`, record exact result artifacts, and commit the US5 slice

**Checkpoint**: Effect failures cannot partially publish, leak internals, retarget authority, or accept an empty retry.

---

## Phase 8: User Story 6 — Wound and Effect Independence (Priority: P2)

**Goal**: Preserve exact wound links while making effect removal incapable of healing or deleting the wound.

**Independent Test**: Dispel/remove/expire effect-only transitions leave wound bytes/semantics unchanged; an accepted wound transition may cause only declared linked effect changes.

### Tests for User Story 6 (RED first)

- [ ] T089 [P] [US6] Add RED contract/source tests for exact wound link, source-bound definition, case/confusable/missing wound rejection, and no repair retarget in `BookOfEternityClient.Tests/EffectSourceAuthorityTests.Wounds.cs`
- [ ] T090 [P] [US6] Add RED composed integration tests for apply, dispel, remove, expire, source loss, simultaneous wound treatment, direct effect-side wound mutation, and byte/semantic wound preservation in `BookOfEternityClient.IntegrationTests/EffectMaterializationValidationTests.Wounds.cs`
- [ ] T091 [P] [US6] Add RED console/browser wording and action tests that distinguish effect suppression from wound treatment and never claim wound healing in `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Effects.cs` and `ExplorerWebCommandServiceTests.Effects.cs`

### Implementation for User Story 6

- [ ] T092 [US6] Implement exact wound source adapter/link authority and source-loss export without wound-write capability in `BookOfEternityClient/Services/EffectSourceAuthority.cs` until T089 is GREEN
- [ ] T093 [US6] Enforce effect-plan field ownership so effect operations cannot mutate wound carrier sections and only accepted wound-plan events may change linked effect state in `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` until T090 is GREEN
- [ ] T094 [US6] Preserve adjacent player/NPC wound state byte-semantically during effect carrier normalization and rollback in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`
- [ ] T095 [US6] Update effect/wound player projection copy and action eligibility in `BookOfEternityClient/UI/EffectPlayerProjection.cs` and `BookOfEternityClient/UI/ExplorerMortalEffectDetailActions.cs` until T091 is GREEN
- [ ] T096 [US6] Run US6 focused wound/effect filters and manual effect-only removal scenario, record RED→GREEN evidence in `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`, and commit the US6 slice

**Checkpoint**: Wounds and effects are linked but independently governed; effect deletion is never healing.

---

## Phase 9: GM Contracts, Active Fixtures, Performance, and Final Gates

**Purpose**: Synchronize every GM-authored surface, remove current-schema ambiguity, validate repository fixtures, and complete local release evidence.

### Documentation/source-guard RED first

- [ ] T097 [P] Add RED source guards requiring `effectChanges[]`, `effectResolutionReceipts[]`, closed profiles/lifetimes/stacks, no legacy positive routes/sentinels, wound independence, and shared Mortal/afterlife guide references in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- [ ] T098 [P] Add RED afterlife coverage guards for common identity/lifecycle adapter, profile `activeEffects[]`, no direct condition authoring, hidden privacy, Shining entitlement exclusion, and worked condition examples in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- [ ] T099 [P] Add RED example/manifest validation for all required source/profile/lifecycle operations and malformed bounded repair in `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [ ] T100 [P] Add RED active fixture integrity tests for missing-empty versus valid non-empty current carriers/index/pending roots and zero legacy positive state in `BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs`

### Active state and GM documentation migration

- [ ] T101 Migrate active new-game/template player, NPC, combat, afterlife profile/conflict, effect index, and pending-control fixtures under `FileSystemExample/`, `FileSystemExample/validator_fixtures/_shared/`, and test fixture builders to the current schema; add no runtime migration code
- [ ] T102 Update common effect construction, skill/item source definitions, application, stacking, lifetime, trigger, dispel/removal, wound boundary, and negative guidance in `Rules/Block_2.txt`, `Rules/Block_5.txt`, `Rules/Block_6.txt`, `Rules/Block_7.txt`, `Rules/Block_8.txt`, `Rules/Block_10.txt`, and `Rules/Block_12.txt` until T097 is GREEN
- [ ] T103 Update quest/location/faction/event/hazard and related Mortal effect source guidance in `Rules/Block_14.txt`, `Rules/Block_15.txt`, `Rules/Block_17.txt`, `Rules/Block_19.C.txt`, `Rules/Block_25.txt`, and `Rules/Block_25.A.txt`
- [ ] T104 Update command schema, transient lifecycle, operator-only pending/repair context, and no-legacy routes in `Rules/Block_CLI_Operations.txt`, `CLI_API_Specification.md`, and `CLI_Agent_Daemon_Specification.md`
- [ ] T105 Update afterlife common-profile and spiritual-condition adapter rules, privacy, lifecycle, and Shining entitlement boundary in `OtherGuides/Afterlife_Contract_Matrix.md` and `OtherGuides/Afterlife_Combat_Terminology_Glossary.md` until T098 is GREEN
- [ ] T106 Update GM prompt entrypoints and forced-reading reminders for the current effect contract in `BookOfEternityClient/game_master_daemon.ps1` and applicable launcher/task guides under `TaskGuides/`
- [ ] T107 Add/update Mortal worked examples for buff, debuff, periodic, triggered, environmental, item/skill/art, quest, wound, stack, refresh, replace/merge, expiry, dispel, and bounded repair in `Examples/E_Block_2.txt`, `E_Block_5.txt`, `E_Block_6.txt`, `E_Block_7.txt`, `E_Block_8.txt`, `E_Block_10.txt`, `E_Block_12.txt`, and the corresponding quest/world/faction example files
- [ ] T108 Add/update afterlife actor/profile and all five spiritual-condition lifecycle examples in `Examples/E_CLI_Afterlife_Turns.txt` until T098 is GREEN
- [ ] T109 Update every changed example entry, required text, response surface, and validation relationship in `Examples/example_validation_manifest.json` until T099 is GREEN
- [ ] T110 Remove or rewrite active positive legacy `playerActiveEffectsChanges`, `NPCEffectChanges`, direct non-empty `activeBuffs`/`activeDebuffs`/`combatConditions`, duration sentinel, and prose-mechanics fixtures across `Rules/`, `Examples/`, `FileSystemExample/`, and tests; retain only explicitly labeled negative inputs

### Performance and verification

- [ ] T111 [P] Add a scaling regression that compares N and 2N effects/components/triggers, proves one-pass catalogs, and enforces the 2.5x bound in `BookOfEternityClient.Tests/EffectMaterializationPerformanceTests.cs`
- [ ] T112 Run all focused filters listed in `specs/1535-complete-effect-materialization/plan.md` and `quickstart.md`, confirm zero failures/duplicates and cleanup complete, and record result directories in `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`
- [ ] T113 Run one meaningful local `Fast` checkpoint through `scripts/test-csharp.ps1`, record total/passed/failed/duplicates/timeout/cleanup/wall time, and fix any regression through `systematic-debugging` before proceeding
- [ ] T114 Run required local `FullValidation` and effect-filtered `LifecycleIntegration`; if frontend source or typed contracts changed, run the repository frontend verify command and record the result in `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`
- [ ] T115 Perform manual Mortal and afterlife console/browser validation from `quickstart.md`, including visible/hidden/private, stacks, lifetime, actions, malformed rejection, stale output, recursive DTO privacy, and unchanged wound state; record evidence/rationale in `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`
- [ ] T116 Run `requesting-code-review` against issue #1535, `spec.md`, `plan.md`, `tasks.md`, contracts, all production/test/docs diffs, dirty/untracked state, and verification artifacts; resolve every Critical/Important finding with new RED→GREEN evidence
- [ ] T117 Re-run `speckit-analyze` read-only, remediate approved artifact drift, and ensure all implemented `tasks.md` checkboxes have code/docs/test evidence before marking them complete
- [ ] T118 Run exactly one final local `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` without an immediately preceding duplicate Fast; require exit 0, zero failures/duplicates, timeout false, cleanup complete, and record the exact summary path
- [ ] T119 Inspect `git status --short`, `git diff --check`, full #1535 diff, commits, and verification artifacts; update the GitHub issue/PR summary with Mortal/afterlife prompt-doc-example synchronization and no-migration rationale, then integrate only after user approval

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 — Setup**: Starts from approved design and clean isolated worktree.
- **Phase 2 — Foundations**: Depends on Phase 1 and blocks every user story.
- **US1 (Phase 3)**: Depends on all foundational contracts/authority; establishes usable Mortal creation and mechanics boundary.
- **US2 (Phase 4)**: Depends on US1 canonical instances/planner; adds stack/lifecycle.
- **US3 (Phase 5)**: Depends on US1/US2 common instance and lifecycle; adds cross-realm adapters.
- **US4 (Phase 6)**: Depends on accepted mechanics/projection authority from US1–US3.
- **US5 (Phase 7)**: Depends on all planned publication paths from US1–US3 so failure injection is complete; may begin repair-builder unit work after Phase 2.
- **US6 (Phase 8)**: Depends on US1/US2 source-bound lifecycle and may proceed before US4/US5 production integration if file conflicts are controlled.
- **Phase 9 — Docs/final gates**: Begins RED guards early but completes only after all desired user stories are implemented.

### User Story Dependencies

```text
Foundations
    └── US1 Trustworthy creation
          ├── US2 Stacking/lifetime
          │     ├── US3 Cross-owner/realm
          │     └── US6 Wound independence
          ├── US4 Projection (after US2/US3 visible semantics settle)
          └── US5 Atomic repair (after publication paths settle)
All stories ──> Docs/fixtures/performance/review/final gates
```

### Within Each User Story

1. Add the smallest exact RED test and run it to confirm the intended failure.
2. Implement only enough production behavior to turn that test GREEN.
3. Run the complete story-focused filter and relevant neighboring controls.
4. Refactor while green and inspect the diff.
5. Record result artifact and commit one logical slice.

## Parallel Opportunities

- Phase 2 contract, identity, source, target, and carrier RED test files are independent before production convergence.
- US1 planner, validation, normalizer, and mechanics RED tests can be authored in parallel because they live in separate test files.
- US2 stacking, lifetime, trigger, and receipt RED suites are independent before scheduler implementation.
- US3 profile, condition, realm, and mechanics RED suites are independent.
- US4 projection unit, Mortal console, browser, and afterlife parity RED suites are independent.
- US5 repair-builder, retry-loop, failure-injection, and privacy RED suites are independent.
- US6 contract, composed lifecycle, and UI wording RED suites are independent.
- Documentation RED guards and example inventory can begin while final production behavior is stabilizing, but GREEN docs must reflect the final executable contract.

## Parallel Example: User Story 2

```text
Task: T040 — stack-policy RED tests in EffectLifecycleSchedulerTests.Stacking.cs
Task: T041 — lifetime RED tests in EffectLifecycleSchedulerTests.Lifetime.cs
Task: T042 — trigger/order RED tests in EffectLifecycleSchedulerTests.Triggers.cs
Task: T043 — pending receipt RED tests in EffectLifecycleSchedulerTests.Resolutions.cs
```

These tests do not edit the same file. Production scheduler work begins only after the expected failures are captured.

## Implementation Strategy

### MVP First

1. Complete Setup and Foundations.
2. Complete US1 for trustworthy Mortal player/NPC/combatant creation.
3. Validate that complete instances apply and malformed sets contribute zero mechanics.
4. Do not ship/integrate this partial state because legacy routes are removed only with the complete #1535 cross-realm contract; the MVP is an engineering checkpoint, not a public compatibility release.

### Incremental Delivery

1. Foundations establish reusable contracts and authority.
2. US1 creates safe instances.
3. US2 makes them deterministic and retry-safe.
4. US3 brings every required realm/owner onto the shared boundary.
5. US4 exposes safe player semantics/actions.
6. US5 proves atomic failure and bounded repair.
7. US6 proves wound independence.
8. Phase 9 synchronizes GM knowledge and produces integration evidence.

## Requirement Coverage

| Requirement group | Primary task coverage |
| --- | --- |
| FR-001–FR-006 scope/identity/direct mutation | T006–T015, T020–T022, T024, T031 |
| FR-007–FR-014 source/target/components | T007–T011, T016–T019, T023–T035 |
| FR-015–FR-022 stacking/lifetime | T040–T051 |
| FR-023–FR-028 lifecycle/triggers/receipts | T042–T051 |
| FR-029–FR-035 owners/afterlife/mechanics | T026–T038, T053–T064 |
| FR-036–FR-040 wound/companions | T048–T051, T089–T096 |
| FR-041–FR-047 atomicity/repair/privacy | T077–T088 |
| FR-048–FR-049 player parity/projection | T065–T076 |
| FR-050–FR-052 docs/examples/no migration | T097–T110, T119 |
| SC-001–SC-011 measurable completion | T023–T119 focused, performance, docs, lifecycle, manual, review, and PreMerge gates |

## Notes

- `[P]` means the task is structurally parallelizable, not permission to spawn subagents; current session policy still requires explicit user authorization for delegation.
- Mark a task complete only after inspecting its diff and recording required RED/GREEN or verification evidence.
- If implementation reveals a requirement conflict, stop the affected slice and update the Spec Kit artifact through the appropriate phase instead of silently drifting.
- Do not add runtime migration, compatibility aliases, GitHub workflows, or cloud dependencies.
