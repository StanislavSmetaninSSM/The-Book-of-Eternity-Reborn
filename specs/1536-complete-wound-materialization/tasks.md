# Tasks: Complete Wound Materialization and Healing

**Input**: Design documents from `/specs/1536-complete-wound-materialization/`  
**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`  
**Method**: Test-driven development; every behavior test is written and observed RED before its implementation task.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel after prior dependencies are complete because it owns different files.
- **[Story]**: Maps the task to one user story from `spec.md`.
- Every implementation change remains tied to GitHub issue #1536.

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Establish reusable strict fixtures without changing production behavior.

- [X] T001 Create the reusable file-backed wound test context, canonical path inventory, exact before-image helpers, and deterministic identity factories in `BookOfEternityClient.IntegrationTests/WoundMaterializationTestContext.cs`
- [X] T002 [P] Add strict version-1 player/NPC/combatant/afterlife carrier, index, history, command, and pending JSON builders in `BookOfEternityClient.Tests/WoundContractTestData.cs`
- [X] T003 [P] Add post-apocalyptic, magical-world, spiritual-conflict, Elyara, and Shining faction seed builders in `BookOfEternityClient.IntegrationTests/WoundMaterializationTestFixtures.cs`
- [X] T004 [P] Create a source-ownership inventory test that records all current loose wound writers/readers and accepted-mechanics integration points in `BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs`

**Checkpoint**: Tests can create exact independent wound scenarios without production scaffolding or shared mutable roots.

---

## Phase 2: Foundational Common Wound Kernel (Blocking Prerequisites)

**Purpose**: Build the strict identity/lifecycle/transaction boundary required by every story. No player command is exposed in this phase.

**⚠️ CRITICAL**: Complete and verify this phase before any story-specific implementation.

### RED tests

- [X] T005 [P] Add RED closed-root/envelope, unknown/duplicate field, exact identifier, collection-limit, severity I-IV, location profile, and explicit legacy-shape rejection tests in `BookOfEternityClient.Tests/WoundMaterializationContractTests.cs`
- [X] T006 [P] Add RED global identity, confusable uniqueness, active/terminal status, owner/realm/carrier coordinate, and semantic fingerprint agreement tests in `BookOfEternityClient.Tests/WoundIdentityStateTests.cs`
- [ ] T007 [P] Add RED player, dedicated NPC, combatant/group-member, afterlife profile, duplicate occurrence, wrong-realm, and promotion carrier tests in `BookOfEternityClient.Tests/WoundCarrierCatalogTests.cs`
- [ ] T008 [P] Add RED append-only ordinal/fingerprint chain, operation-key replay, terminal continuity, healed-without-carrier, and no-reopen tests in `BookOfEternityClient.Tests/WoundHistoryStateTests.cs`
- [ ] T009 [P] Add RED create/worsen/complicate/diagnose/stabilize/treat/recover/heal/legacy/archive transition, independent mechanical/cosmetic legacy, and forbidden regression/domain-conversion tests in `BookOfEternityClient.Tests/WoundTransitionReducerTests.cs`
- [ ] T010 [P] Add RED exact version-1 Mortal/spiritual primitive registry, per-severity magnitude/cadence/expansion limits, Mortal non-display-impact minimum/maximum, spiritual exact slots, hidden component packing, safe-exit, and independent effect tests in `BookOfEternityClient.Tests/WoundConsequenceEnvelopeTests.cs`
- [ ] T011 Add RED staged prepare/source-export/effect/finalize, reciprocal link, provisional identity, invalid sibling, and complete-plan tests in `BookOfEternityClient.Tests/WoundAcceptedTurnPlannerTests.cs`
- [ ] T012 [P] Add RED wound command/input/fingerprint/before-image/cache equality, stale generation, take-once, and invalidation tests in `BookOfEternityClient.Tests/AcceptedMechanicsPlanCacheTests.Wounds.cs`
- [ ] T013 [P] Add RED wound carrier/index/history/command/pending/scheduler/output snapshot inclusion and missing-before-image rejection tests in `BookOfEternityClient.Tests/PendingTurnSnapshotAuthorityTests.Wounds.cs`
- [ ] T014 [P] Add RED indexed catalog/planner linear-work and version-1 bound tests in `BookOfEternityClient.IntegrationTests/WoundAcceptedMechanicsScaleTests.cs`

### GREEN implementation

- [X] T015 Implement strict schema-version-1 roots, common envelope records, parsers, exact identifiers, bounds, and canonical serialization in `BookOfEternityClient/Services/WoundMaterializationContract.cs`
- [X] T016 Implement client-owned identity index parsing, active/terminal agreement, exact fingerprints, and canonical serialization in `BookOfEternityClient/Services/WoundIdentityState.cs`
- [ ] T017 Implement owner-appropriate occurrence indexing and exact owner resolution for player/NPC/combatant/afterlife carriers in `BookOfEternityClient/Services/WoundCarrierCatalog.cs`
- [ ] T018 Implement append-only history, operation replay probes, terminal continuity, and the closed transition reducer in `BookOfEternityClient/Services/WoundHistoryState.cs` and `BookOfEternityClient/Services/WoundTransitionReducer.cs`
- [ ] T019 Implement the exact version-1 Mortal/spiritual registry, slot derivation, non-display-impact minimum, severity-specific magnitude/cadence/expansion/scope envelopes, and aggregate safe-exit checks in `BookOfEternityClient/Services/WoundConsequenceEnvelopeCatalog.cs`
- [ ] T020 Implement staged wound preparation/finalization models, provisional identity allocation, source export, reciprocal link validation, and immutable after-images in `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs` and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- [ ] T021 Extend generation-scoped accepted authority and cache invalidation with prepared/final wound plans in `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs`
- [ ] T022 Replace loose wound-source scans with accepted wound source exports while preserving existing static wound-source validation in `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs` and `BookOfEternityClient/Services/EffectSourceAuthority.cs`
- [ ] T023 Extend common input bindings, authority fingerprints, planning context, after-images, touched/consumed paths, and path coverage for wounds in `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs`
- [ ] T024 Compose prepare -> effect -> finalize -> resource ordering, typed same-root results, and bounded work statistics in `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- [ ] T025 Add the explicit accepted-turn wound completeness phase plus strict raw input/root composition and planning handoff in `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`, `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs`
- [ ] T026 Publish and read-back-validate wound carriers/index/history/pending after-images only through the common write lease in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Wounds.cs`
- [ ] T027 Extend snapshot collection/rollback and accepted post-validation with exact wound and output paths in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T028 Run the smallest focused common-kernel filters through `scripts/test-csharp.ps1`, record RED-to-GREEN evidence and durations in `specs/1536-complete-wound-materialization/tasks.md`, and resolve every foundational failure before US1

**Checkpoint**: Strict wound roots, identity/history, carrier agreement, staged effect composition, common publication, replay, and rollback are GREEN with no exposed player workflow.

---

## Phase 3: User Story 1 — Receive a Complete and Fair Wound (Priority: P1) 🎯

**Goal**: Accept an optional or guaranteed, GM-authored, client-bounded physical or spiritual wound with exact owner/source/severity/effects/history/narration in one transaction.

**Independent Test**: Resolve one wound-capable event, accept none/lower/legal/guaranteed cases, reject over-limit/contradictory cases, and prove exact wound/effect/history/output atomicity for every owner carrier.

### RED tests

- [ ] T029 [P] [US1] Add RED formal and narrative event opportunity, harmless-result conflict, none/lower/equal/over-maximum decision, and consumed-decline tests in `BookOfEternityClient.Tests/WoundOpportunityAuthorityTests.cs`
- [ ] T030 [P] [US1] Add RED pre-materialized guaranteed trigger, omitted result, source-state, hard-cap conflict, and exact retry tests in `BookOfEternityClient.Tests/WoundGuaranteedTriggerTests.cs`
- [ ] T031 [P] [US1] Add RED exact player/NPC/combatant/Guardian/resident/radiant/soul target, named ambiguity, wrong realm, and source-event binding tests in `BookOfEternityClient.Tests/WoundSourceTargetAuthorityTests.cs`
- [ ] T032 [P] [US1] Add RED acquisition narration completeness/contradiction, deterministic Russian notification, untrusted markup escaping, and no-ID projection tests in `BookOfEternityClient.Tests/WoundAcquisitionOutputTests.cs`
- [ ] T033 [US1] Add RED end-to-end optional/lower/guaranteed/over-limit, wound-owned effect atomicity, healed independent mechanical legacy, and cosmetic History legacy fixtures in `BookOfEternityClient.IntegrationTests/WoundMaterializationLifecycleTests.Creation.cs`
- [ ] T034 [P] [US1] Add RED all-owner carrier/index/history agreement, combatant persistence, Mortal-to-afterlife no-conversion, and accepted spiritual profile realm-preservation transition cases in `BookOfEternityClient.IntegrationTests/WoundMaterializationLifecycleTests.Owners.cs`

### GREEN implementation

- [ ] T035 [US1] Implement sealed Mortal/mechanical/narrative/spiritual wound opportunities, guarantees, safe GM context, and consumed decision authority in `BookOfEternityClient/Services/WoundOpportunityAuthority.cs`
- [ ] T036 [US1] Add the strict response proposal/decision fields and remove permanent-ID authority from GM input in `BookOfEternityClient/Models/GameResponse.cs` and `BookOfEternityClient/Services/WoundResponseInputComposer.cs`
- [ ] T037 [US1] Bind exact event/source/target/realm/profile and guaranteed evidence during wound preparation in `BookOfEternityClient/Services/WoundSourceAuthority.cs` and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- [ ] T038 [US1] Materialize complete wound-owned effects through accepted source exports and validate reciprocal source/target/slot ownership in `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`, `BookOfEternityClient/Services/EffectSourceAuthority.cs`, and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- [ ] T039 [US1] Compose create/worsen/decline after-images, client IDs, history, owner carriers, and combatant promotion transitions in `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs` and `BookOfEternityClient/Services/WoundTransitionReducer.cs`
- [ ] T040 [US1] Add escaped acquisition narration validation and deterministic player notification/output binding to the accepted turn in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` and `BookOfEternityClient/UI/WoundPlayerNotification.cs`
- [ ] T041 [US1] Replace direct `playerWoundChanges`/`NPCWoundChanges` distribution with strict accepted wound command consumption in `BookOfEternityClient/Configuration/FileMapping.cs`, `BookOfEternityClient/IO/StateDistributor.cs`, and `BookOfEternityClient/Models/GameResponse.cs`

### GM contract synchronization

- [ ] T042 [P] [US1] Add RED source/documentation guards for wound constructor, optional GM choice, guarantee, severity bounds, acquisition narration, and effect separation in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`
- [ ] T043 [US1] Create the GM-facing common constructor/authority/effect contract in `OtherGuides/Wound_Materialization_Contract.md` and synchronize the wound-source section in `OtherGuides/Effect_Materialization_Contract.md`
- [ ] T044 [US1] Replace the loose constructor and add optional/lower/guaranteed/rejected worked examples in `Rules/Block_5.txt`, `Examples/E_Block_5.txt`, and `BookOfEternityClient/game_master_daemon.ps1`
- [ ] T045 [US1] Run focused US1 unit/integration/documentation filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Wound creation is complete, fair, atomic, narrated, and independently testable; no treatment or player command is exposed yet.

---

## Phase 4: User Story 7 — Recover Safely from Invalid GM Output or Retry (Priority: P1)

**Goal**: Reject/repair invalid wound work without leaking authority or leaving partial/duplicate state.

**Independent Test**: Inject invalid owner/severity/consequence/treatment/resource and failures at every publication stage; prove bounded repair, exact rollback, and zero duplicate effects/charges/progress/output on retry.

### RED tests

- [ ] T046 [P] [US7] Add RED table-driven owner/severity/slot/effect/treatment/resource/narration issue normalization, offending-path preservation, safe expected ranges, preserved siblings, required corrected response shape, and bounded candidate tests in `BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs`
- [ ] T047 [P] [US7] Add RED hidden owner/provider/route/resource seal and private NPC data non-disclosure tests in `BookOfEternityClient.Tests/WoundRepairPacketPrivacyTests.cs`
- [ ] T048 [P] [US7] Add RED changed event/target/roll/snapshot/generation invalidation, exact packet receipt, and take-once tests in `BookOfEternityClient.Tests/WoundAcceptedTurnPlanCacheTests.cs`
- [ ] T049 [US7] Add RED corrected repair roundtrips for every representative invalid category plus failure injection before/after wound/effect/resource/profile/scheduler/history/output writes with byte/existence assertions in `BookOfEternityClient.IntegrationTests/WoundMaterializationRollbackTests.cs`
- [ ] T050 [P] [US7] Add RED 100-repeat loops for accepted event/treatment/course/cycle/payment/output identities after success, repair, crash recovery, and consumed commands, asserting zero duplicate state in `BookOfEternityClient.IntegrationTests/WoundMaterializationReplayTests.cs`

### GREEN implementation

- [ ] T051 [US7] Implement bounded wound construction/repair/narration/alternative-treatment packets and recursive privacy sanitization in `BookOfEternityClient/Services/WoundRepairPacketBuilder.cs`
- [ ] T052 [US7] Integrate wound repair obligations, safe harness packets, exact resubmission shape, and unrelated-response preservation in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [ ] T053 [US7] Invalidate prepared/final wound, effect, and common handoffs together on repair/rejection/snapshot mismatch in `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs`
- [ ] T054 [US7] Extend exact snapshot and post-publication agreement to scheduler, journals, quests, inventory/characteristics, debug/output, and pending wound roots in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`
- [ ] T055 [US7] Add operation/attempt/course/cycle/payment/output replay coordinates and already-accepted receipt projection in `BookOfEternityClient/Services/WoundHistoryState.cs` and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- [ ] T056 [US7] Add invalid proposal, bounded repair, stale packet, rollback, and replay worked guidance in `OtherGuides/Wound_Materialization_Contract.md`, `Rules/Block_12.txt`, and `Examples/E_Block_12.txt`
- [ ] T057 [US7] Run focused repair/privacy/cache/rollback/replay filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Invalid or repeated wound work cannot leak authority, alter unrelated content, or leave partial state.

---

## Phase 5: User Story 2 — Diagnose and Treat a World-Specific Physical Wound (Priority: P1)

**Goal**: Materialize arbitrary setting-appropriate Mortal diagnosis, treatment, recovery, and deterioration without a ready-made wound/cure catalog.

**Independent Test**: Complete two unrelated post-apocalyptic and magical wound lifecycles using alternative routes and exact resources/providers/facilities; prove deterministic procedure/course/guaranteed outcomes and History.

### RED tests

- [ ] T058 [P] [US2] Add RED complete Mortal route, required non-display impact, AND requirements/OR alternatives, closed mode/outcome, and no wound/symptom/medicine/cure-name lookup tests in `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs`
- [ ] T059 [P] [US2] Add RED hidden route reachable diagnosis, reveal boundaries, cyclic/impossible discovery, and evidence-backed alternative route tests in `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs`
- [ ] T060 [P] [US2] Add RED exact item/resource/skill/capability/provider/consent/facility/location/quest/effect/environment and stale/confusable/cross-realm reference tests in `BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs`
- [ ] T061 [P] [US2] Add RED procedure result bands, course milestone/interruption, guaranteed capability, natural 1/20 policy, and terminal attempt tests in `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs`
- [ ] T062 [P] [US2] Add RED progressive/requires-stabilization/no-natural recovery, deterioration, clock replay, overflow, and Mortal death-boundary tests in `BookOfEternityClient.Tests/MortalWoundRecoveryTests.cs`
- [ ] T063 [US2] Add RED cross-setting create -> diagnose -> procedure/course/guaranteed -> recover -> heal -> History journeys and atomic resource/item use in `BookOfEternityClient.IntegrationTests/MortalWoundMaterializationLifecycleTests.cs`

### GREEN implementation

- [ ] T064 [US2] Implement formal/QTE/combat/trap/check/hazard/narrative Mortal opportunity adapters and causal/profile validation in `BookOfEternityClient/Services/MortalWoundOpportunityAdapter.cs`
- [ ] T065 [US2] Implement strict diagnosis paths, treatment routes, requirements, resolution modes, outcomes, and discoverability graph validation in `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs`
- [ ] T066 [US2] Implement exact materialized item/resource/skill/capability/provider/facility/location/quest/effect resolution in `BookOfEternityClient/Services/MortalWoundTreatmentAuthority.cs`
- [ ] T067 [US2] Implement procedure/course/guaranteed attempt resolution, terminal attempt identity, route addition, and bounded transition intents in `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.cs`
- [ ] T068 [US2] Route item reservations/consumption and common resource mutations through accepted typed authorities with rollback/replay in `BookOfEternityClient/Services/MortalWoundTreatmentResourceComposer.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- [ ] T069 [US2] Implement game-time recovery/deterioration policies, cadence/blockers/overflow, and exact tick outcomes in `BookOfEternityClient/Services/MortalWoundRecoveryPlanner.cs`
- [ ] T070 [US2] Compose Mortal diagnosis/treatment/recovery wound/effect/item/resource/history/output after-images in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Wounds.cs`
- [ ] T071 [US2] Remove loose Mortal aliases/wrappers, `generatedEffects`, `healingState.canBeImprovedBy`, `WoundReference`, `sourceWoundId`, `duration=999`, and NPC-effect-carrier fallbacks in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs`, `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs`

### GM contract synchronization

- [ ] T072 [P] [US2] Add RED Mortal diagnosis/hidden route/procedure/course/guarantee/resource/recovery documentation guards in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.MortalWounds.cs` and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.MortalWounds.cs`
- [ ] T073 [US2] Replace legacy Mortal wound/treatment/recovery guidance in `Rules/Block_2.txt`, `Rules/Block_5.txt`, `Rules/Block_10.txt`, `Rules/Block_12.txt`, and `CLI_API_Specification.md`
- [ ] T074 [US2] Add post-apocalyptic and magical visible/hidden/alternative/partial/course/recovery/healing worked examples in `Examples/E_Block_5.txt`, `Examples/E_Block_10.txt`, `Examples/E_Block_12.txt`, and `Examples/example_validation_manifest.json`
- [ ] T075 [US2] Run focused Mortal contract/authority/resolver/recovery/lifecycle/docs filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Arbitrary Mortal wounds have complete setting-specific treatment and recovery lifecycles with exact resource authority and no catalog.

---

## Phase 6: User Story 3 — Suffer or Avoid a Spiritual Wound in Conflict (Priority: P1)

**Goal**: Make spiritual wounds optional GM-authored consequences of bounded strain transitions while preserving durable defeat and always-optional dissipation.

**Independent Test**: Run training/controlled/hostile/annihilation conflicts across every formula boundary and prove caps, none/lower choices, one wound per side, explicit re-trauma, bounded defeat, and optional dissipation.

### RED tests

- [ ] T076 [P] [US3] Add RED trauma-pressure term/threshold, harmful-margin source, resilience-tier delta with zero OD spend, strain rank/jump, natural 1/20, and exact `clear/strained/fractured/overwhelmed/broken` destination-cap boundary tests in `BookOfEternityClient.Tests/SpiritualWoundOpportunityTests.cs`
- [ ] T077 [P] [US3] Add RED training escalation, controlled II cap, hostile/annihilation cap, mode visibility, and post-roll mode-change rejection tests in `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.Wounds.cs`
- [ ] T078 [P] [US3] Add RED one-new-wound-per-side, later worsening, declined-earlier opportunity, and explicit older-wound re-trauma tests in `BookOfEternityClient.Tests/SpiritualConflictWoundSealTests.cs`
- [ ] T079 [P] [US3] Add RED non-training bounded anti-repeat defeat outcome and annihilation winner softer/optional dissipation tests in `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.DefeatWounds.cs`
- [ ] T080 [P] [US3] Add RED exact spiritual I-IV consequence count, legal axes, counterplay/safe-exit, and effect-link tests in `BookOfEternityClient.Tests/SpiritualWoundConsequenceTests.cs`
- [ ] T081 [US3] Add RED conflict start -> exchange -> opportunity -> GM decline/lower/create/worsen -> resolve lifecycle and rollback fixtures in `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualWoundLifecycleTests.cs`

### GREEN implementation

- [ ] T082 [US3] Add `spiritual_resilience` tier 0-V to standard afterlife art/profile/bootstrap/progression authority without adding it to operation types in `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- [ ] T083 [US3] Extend spiritual conflict start/state with declared danger mode, escalation evidence, per-side wound seals, and bounded defeat outcome in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
- [ ] T084 [US3] Implement trauma-pressure calculation, destination/mode/source caps, harmful-margin audit, one-per-side/re-trauma rules, and opportunity export in `BookOfEternityClient/Services/SpiritualWoundOpportunityAdapter.cs`
- [ ] T085 [US3] Validate danger/opportunity/strain/art audit and reject GM-authored computed fields in `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs` and `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`
- [ ] T086 [US3] Add danger, wound maximum, accepted wound/decline, and defeat consequence to player-safe conflict preview/audit in `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs`
- [ ] T087 [US3] Compose persistent profile wound transitions and conflict-only evidence without duplicating wounds in combat conditions in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeSpiritualConflict.cs`
- [ ] T088 [US3] Implement mandatory bounded non-training defeat outcomes while preserving the existing separate optional soul-dissipation proof in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`

### GM contract synchronization

- [ ] T089 [P] [US3] Add RED danger/formula/GM-decline/lower/one-per-side/defeat/dissipation afterlife documentation guards in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualWounds.cs`
- [ ] T090 [US3] Update spiritual conflict rules, terminology, GM context, and afterlife matrix in `Rules/Block_21.txt`, `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, `OtherGuides/Afterlife_Contract_Matrix.md`, and `TaskGuides/CLI_Step_Main.txt`
- [ ] T091 [US3] Add no-wound, lower-than-maximum, guaranteed, over-limit repair, one-per-side worsening, bounded defeat, and optional dissipation examples in `Examples/E_CLI_Afterlife_Turns.txt` and `Examples/example_validation_manifest.json`
- [ ] T092 [US3] Run focused spiritual opportunity/conflict/consequence/lifecycle/docs filters through `scripts/test-csharp.ps1`, using a measured timeout override only if needed, and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Spiritual conflict has durable, bounded injury/defeat semantics without HP, automatic wounds, or compulsory dissipation.

---

## Phase 7: User Story 4 — Heal Spiritual Wounds and Advance the World (Priority: P1)

**Goal**: Let players and persistent entities diagnose, actively heal, self-heal, or naturally recover using standard arts and exact action/world-cycle costs.

**Independent Test**: Cover insufficient tier, every roll band/natural result, combat cost/counter, safe self/provider sessions, tier-0/V natural recovery, entity recovery/progression, and player treatment of another entity.

### RED tests

- [ ] T093 [P] [US4] Add RED `spiritual_healing` tier 0-V bootstrap/profile/progression/training, visible name, diagnosis-at-zero, and insufficient-tier tests in `BookOfEternityClient.Tests/SpiritualHealingArtTests.cs`
- [ ] T094 [P] [US4] Add RED `d20 + 2H` vs `10 + 2W + complications`, all margin bands, bounded two-step result, tier-before-natural-20, and natural-1 override tests in `BookOfEternityClient.Tests/SpiritualHealingResolverTests.cs`
- [ ] T095 [P] [US4] Add RED combat base-5/reduction/floor-2 spend, explicit operation-to-art mapping, counter/matchup legality, and rollback tests in `BookOfEternityClient.Tests/SpiritualHealingCombatActionTests.cs`
- [ ] T096 [P] [US4] Add RED one safe-cycle session, no OD, self no currency/item, one attempt per wound/cycle, active-treatment-then-natural-recovery ordering, failed-attempt world advance, and duplicate suppression tests in `BookOfEternityClient.Tests/SpiritualHealingSafeCycleTests.cs`
- [ ] T097 [P] [US4] Add RED `1+tier`, I=2/II=4/III=6/IV=8 thresholds, overflow, worsening reset, unsafe suppression, tier-0 20-cycle, and tier-V 4-cycle tests in `BookOfEternityClient.Tests/SpiritualWoundNaturalRecoveryTests.cs`
- [ ] T098 [P] [US4] Add RED player/Guardian/resident/leader/radiant recovery, normal art progression, consent/reachability, and player-heals-entity tests in `BookOfEternityClient.IntegrationTests/AfterlifeWoundEntityRecoveryTests.cs`
- [ ] T099 [US4] Add RED combat heal and safe self/helper/entity heal accepted-plan/resource/scheduler/effect/history/output lifecycle tests in `BookOfEternityClient.IntegrationTests/AfterlifeWoundHealingLifecycleTests.cs`

### GREEN implementation

- [ ] T100 [US4] Add `spiritual_healing` tier 0-V to player-soul and persistent actor standard art bootstrap, validation, progression, training offers, and Russian display in `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`, `BookOfEternityClient/Services/AfterlifeTrainingCostPolicy.cs`, and `BookOfEternityClient/Services/TrainingService.cs`
- [ ] T101 [US4] Implement the single tier gate, roll authority, result bands, natural 1/20 precedence, complication modifier, and bounded transitions in `BookOfEternityClient/Services/SpiritualHealingResolver.cs`
- [ ] T102 [US4] Add a counterable healing operation and art mapping without making resilience active in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- [ ] T103 [US4] Resolve combat healing cost through `AfterlifeActionCostRules` and typed spiritual-action-point outcomes in `BookOfEternityClient/Services/AfterlifeActionCostRules.cs` and `BookOfEternityClient/Services/AfterlifeSpiritualConflictResourceOutcome.cs`
- [ ] T104 [US4] Implement exact safe-cycle session/attempt identity, no-OD self/helper treatment, and world-cycle outcome composition in `BookOfEternityClient/Services/AfterlifeWoundHealingPlanner.cs`
- [ ] T105 [US4] Implement universal natural recovery reducer, overflow/reset, per-wound cycle seals, and player/entity eligibility in `BookOfEternityClient/Services/SpiritualWoundRecoveryPlanner.cs`
- [ ] T106 [US4] Integrate accepted recovery and art progression with `ProgressionScheduleService` for player soul and persistent actors without wall-clock or effect-scheduler fallback in `BookOfEternityClient/Services/ProgressionScheduleService.cs` and `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- [ ] T107 [US4] Compose healing/recovery wound/effect/resource/profile/scheduler/history/output after-images and exact replay results in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- [ ] T108 [US4] Add player-to-entity and entity-to-player treatment authority using exact profile, visibility, reachability, consent/conflict, and current wound bindings in `BookOfEternityClient/Services/AfterlifeWoundTreatmentAuthority.cs`

### GM contract synchronization

- [ ] T109 [P] [US4] Add RED spiritual arts/combat heal/safe-cycle/natural entity recovery documentation and manifest guards in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualHealing.cs`
- [ ] T110 [US4] Update arts, healing check, action cost, safe-cycle self/helper treatment, and natural entity recovery guidance in `Rules/Block_21.txt`, `OtherGuides/Afterlife_Contract_Matrix.md`, and `TaskGuides/CLI_Step_Main.txt`
- [ ] T111 [US4] Add combat healing, failed self-healing world advance, tier-0/V natural recovery, wounded Guardian recovery, and player-heals-Guardian examples in `Examples/E_CLI_Afterlife_Turns.txt` and `Examples/example_validation_manifest.json`
- [ ] T112 [US4] Run focused art/resolver/combat/safe-cycle/recovery/entity/lifecycle/docs filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Every player and persistent afterlife entity has a bounded treatment and natural recovery path; healing advances combat resources or world life exactly once.

---

## Phase 8: User Story 5 — Inspect Active Wounds and Choose a Target Safely (Priority: P2)

**Goal**: Provide private, active-only wound inspection and guided treatment targeting with console/browser parity and separate healed History.

**Independent Test**: Compare console/browser active/detail/History, self-first nearby targets, duplicate names, hidden routes/private NPC wounds, stale reachability/consent, quotes/costs/results/errors, and renderer escaping.

### RED tests

- [ ] T113 [P] [US5] Add RED active-current-realm list, healed History separation, visible detail/effects/routes, empty state, and internal metadata omission tests in `BookOfEternityClient.Tests/WoundPlayerProjectionTests.cs`
- [ ] T114 [P] [US5] Add RED Self-first nearby target list, exact hidden binding, duplicate-name disambiguation, wound discovery, consent, reachability, and stale confirmation tests in `BookOfEternityClient.Tests/WoundTargetSelectionTests.cs`
- [ ] T115 [P] [US5] Add RED recursive hidden symptom/route/NPC/provider/effect privacy and Spectre/browser unsafe-text tests in `BookOfEternityClient.Tests/WoundProjectionPrivacyTests.cs`
- [ ] T116 [P] [US5] Add RED `/раны`, `/wounds`, `/лечить`, `/treat`, `/исцелить`, `/heal` catalog/alias/result-flow tests proving full detail or treatment-flow start within at most two selections in `BookOfEternityClient.IntegrationTests/ExplorerWoundCommandTests.cs`
- [ ] T117 [US5] Add RED console/browser semantic parity for list/detail/History/target/method/confirmation/result/stale/error flows, including the same two-selection navigation bound, in `BookOfEternityClient.IntegrationTests/WoundConsoleBrowserParityTests.cs`
- [ ] T118 [P] [US5] Add RED browser command menu, card hierarchy, responsive/keyboard flow, empty state, copy, and safe-render tests in `BookOfEternityClient.WebFrontend/test/woundCommands.test.tsx`

### GREEN implementation

- [ ] T119 [US5] Implement typed visibility-safe active/detail/History/result projection and renderer-neutral blocks in `BookOfEternityClient/Services/WoundPlayerProjection.cs`
- [ ] T120 [US5] Implement Self-first nearby entity discovery, hidden exact bindings, visible disambiguation, and fresh reachability/consent/wound revalidation in `BookOfEternityClient/Services/WoundTargetSelectionService.cs`
- [ ] T121 [US5] Implement the shared target -> wound/diagnosis -> route/helper -> requirements/quote -> confirmation -> result application workflow in `BookOfEternityClient/Services/WoundApplicationService.cs`
- [ ] T122 [US5] Implement one-shot exact wound command staging/consumption and stale/replay rejection in `BookOfEternityClient/Services/WoundCommandState.cs`
- [ ] T123 [US5] Add escaped Russian console list/detail/History/prompts/results using shared blocks in `BookOfEternityClient/UI/ExplorerWoundCommandResultBuilder.cs` and `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Wounds.cs`
- [ ] T124 [US5] Register all command aliases and route both Mortal/afterlife console actions to the shared handler in `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs` and `BookOfEternityClient/UI/ExplorerMode.cs`
- [ ] T125 [US5] Route browser reads/writes, prompt sessions, command coverage, and menu entries through the shared handler in `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`, `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs`, `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`, and `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs`
- [ ] T126 [US5] Implement dedicated wound list/detail/History and guided treatment browser cards/steps over the shared semantic blocks, preserving the project design system and accessibility in `BookOfEternityClient.WebFrontend/src/components/WoundCommandView.tsx` and `BookOfEternityClient.WebFrontend/src/styles/wounds.css`
- [ ] T127 [US5] Remove raw legacy wound parsing/previews and activeConditions duplication from `BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs`, `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs`, and `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs`

### Player/GM documentation and verification

- [ ] T128 [P] [US5] Add RED CLI alias, active-only, History, hidden-ID target, parity, privacy, and stale-selection documentation/source guards in `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.Wounds.cs` and `BookOfEternityClient.Tests/AfterlifePlayerFacingSourceGuardTests.Wounds.cs`
- [ ] T129 [US5] Document `/раны` and `/лечить` guided flows, active-vs-History, entity selection, privacy, and equivalent aliases in `Rules/Block_CLI_Operations.txt`, `CLI_API_Specification.md`, `OtherGuides/Wound_Materialization_Contract.md`, and `BookOfEternityClient/game_master_daemon.ps1`
- [ ] T130 [US5] Run focused projection/target/privacy/console/browser parity tests plus `npm run verify`; use the preferred browser skill for rendered desktop/mobile/keyboard/privacy checks and record evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Players can discover and act on wounds without IDs or leaks; healed records never clutter the active list; console/browser semantics match.

---

## Phase 9: User Story 6 — Find Afterlife Healing Help (Priority: P2)

**Goal**: Provide Elyara, visible Shining faction healers, paid quotes, negotiated compensation, and access-aware services through the same healing resolver.

**Independent Test**: Treat through Elyara command payment and roleplay compensation, verify retry/rollback charge rules, and inspect factions with visible healers of different tiers/access without assuming public service.

### RED tests

- [ ] T131 [P] [US6] Add RED 25/50/100/200 base price, 50%-200% multiplier, upward whole-Feather rounding, sealed quote, and exact attempt charge tests in `BookOfEternityClient.Tests/AfterlifeHealingServiceQuoteTests.cs`
- [ ] T132 [P] [US6] Add RED capability-vs-service, visibility, realm/location, access condition, compensation kind, stale provider, and service-profile bounds tests in `BookOfEternityClient.Tests/AfterlifeHealingServiceContractTests.cs`
- [ ] T133 [P] [US6] Add RED accepted success/partial/failure charge-once, cancel/validation/rollback refund, and favor/debt/quest/allegiance/free-aid same-handler tests in `BookOfEternityClient.IntegrationTests/AfterlifeHealingPaymentLifecycleTests.cs`
- [ ] T134 [P] [US6] Add RED first-entry discoverability, tier-V downgrade protection, Lazaret/public-100%, non-active-Guardian availability, and negotiated Elyara tests in `BookOfEternityClient.Tests/ElyaraHealingServiceTests.cs`
- [ ] T135 [P] [US6] Add RED every-faction visible primary `healing_support`, exact actor profile tier I-V, multiple healer, non-public visibility, and access requirement tests in `BookOfEternityClient.IntegrationTests/ShiningFactionHealingSupportTests.cs`
- [ ] T136 [US6] Add RED paid/negotiated Elyara and accessible/inaccessible Shining provider end-to-end command/world-cycle/rollback journeys in `BookOfEternityClient.IntegrationTests/AfterlifeHealingProviderLifecycleTests.cs`

### GREEN implementation

- [ ] T137 [US6] Implement strict healing service profiles, capability separation, quote/rounding, access, and compensation contracts in `BookOfEternityClient/Services/AfterlifeHealingServiceContract.cs`
- [ ] T138 [US6] Compose Ink Feather reservation/charge/receipt through its existing specialized currency/accounting authority, and accepted non-currency agreements through the same wound attempt, without admitting Ink Feathers to the unified resource ledger, in `BookOfEternityClient/Services/AfterlifeHealingServicePlanner.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- [ ] T139 [US6] Add fixed tier-V healing capability, Lazaret location, public 100% service, and negotiated compensation metadata to `BookOfEternityClient/system_guardians/built_in/elyara/manifest.json` and `BookOfEternityClient/system_guardians/built_in/elyara/dossier.md`
- [ ] T140 [US6] Enforce Elyara discoverability/profile/service invariants during fresh game and subsequent validation/normalization in `BookOfEternityClient/Services/SystemGuardianLibraryService.cs` and `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs`
- [ ] T141 [US6] Add visible primary `healing_support` to Shining resident role materialization and Russian display in `BookOfEternityClient/Services/ShiningAbodeState.cs`, `BookOfEternityClient/Services/GuardianAbodeResidentState.cs`, and `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs`
- [ ] T142 [US6] Validate every faction has at least one exact roster healer whose afterlife profile proves tier I-V while public access requires a separate service profile in `BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- [ ] T143 [US6] Add access-aware self/helper/Elyara/Shining provider offers and sealed confirmation to the common player flow in `BookOfEternityClient/Services/WoundApplicationService.cs` and `BookOfEternityClient/Services/WoundPlayerProjection.cs`

### GM contract synchronization

- [ ] T144 [P] [US6] Add RED Elyara/service price/compensation/Shining role/access contract, registry, manifest, and Russian terminology guards in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.HealingProviders.cs` and `BookOfEternityClient.Tests/AfterlifeContractRegistryTests.cs`
- [ ] T145 [US6] Register/document the pending/service/profile/role authority in `BookOfEternityClient/Services/AfterlifeContractRegistry.cs`, `OtherGuides/Afterlife_Contract_Matrix.md`, `Rules/Block_32_Guardians.txt`, and `TaskGuides/CLI_Step_Main.txt`
- [ ] T146 [US6] Add paid Elyara, failed charged attempt, rolled-back no-charge, negotiated compensation, visible inaccessible Shining healer, and accessible faction healer examples in `Examples/E_CLI_Afterlife_Turns.txt` and `Examples/example_validation_manifest.json`
- [ ] T147 [US6] Run focused quote/service/payment/Elyara/Shining/provider/docs filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Afterlife help is discoverable and mechanically complete without conflating ability, visible role, public service, or one mandatory Shining clinic.

---

## Phase 10: Polish, Cross-Cutting Guards, Review, and Integration

**Purpose**: Remove obsolete authority, prove complete cross-story behavior, synchronize all GM surfaces, and merge only from fresh evidence.

- [ ] T148 [P] Audit `memory_suppression` runtime source/identity/carrier/lifecycle in `BookOfEternityClient/Services/EffectSourceAuthority.cs` and Saref tests; add an independent-survival test in `BookOfEternityClient.IntegrationTests/SarefMemorySuppressionWoundIsolationTests.cs`, and create a separate linked GitHub issue before #1536 closes if the audit proves metadata-only behavior
- [ ] T149 [P] Harden source guards so no loose `PlayerWoundChanges`/`NPCWoundChanges`, direct wound file mapping/distribution, legacy wrappers/aliases/generated effects/healing progress, NPC effect-carrier wounds, raw UI parsing, or standalone wound writer remains in `BookOfEternityClient.Tests/WoundMaterializationSourceGuardTests.cs`
- [ ] T150 [P] Add complete strict example manifest coverage for every Mortal/afterlife constructor, repair, treatment, recovery, provider, command, History, and independent-legacy scenario in `Examples/example_validation_manifest.json` and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.Wounds.cs`
- [ ] T151 [P] Add Russian terminology and no-debug/API/ID leakage scans for all wound/provider/player surfaces in `BookOfEternityClient.Tests/AfterlifeRussianTerminologyScannerTests.Wounds.cs` and `BookOfEternityClient.Tests/WoundPlayerFacingSourceGuardTests.cs`
- [ ] T152 [P] Add adversarial Spectre markup, browser HTML/script, bidi/control/confusable, oversized payload, and recursive hidden-field cases in `BookOfEternityClient.Tests/WoundUntrustedTextTests.cs`
- [ ] T153 Run indexed scale/bounds tests and eliminate accidental wound×effect/route/history quadratic work in `BookOfEternityClient.IntegrationTests/WoundAcceptedMechanicsScaleTests.cs` and owning production indexes
- [ ] T154 Run the complete wound/effect/resource/item/profile/scheduler/journal/quest/output failure-injection matrix and reconcile exact byte/existence rollback in `BookOfEternityClient.IntegrationTests/WoundMaterializationRollbackTests.cs`
- [ ] T155 Run one meaningful `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast` checkpoint after integrated implementation and record result directory/count/duration in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T156 Run focused `AfterlifeDocumentationCoverageTests|PromptDocumentationCoverageTests` and conditional `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation`, diagnose only related failures, and record artifacts in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T157 Run `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane RegressionIntegration` only if the complete spiritual-conflict matrix changed or focused evidence requires it, and record the measured rationale/result in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T158 Run `npm run verify` and preferred rendered browser desktop/mobile/keyboard/privacy checks when browser source changed; record commands/screenshots/findings in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T159 Execute every applicable scenario in `specs/1536-complete-wound-materialization/quickstart.md` and reconcile all 88 FRs, 44 acceptance scenarios, edge cases, contracts, current bootstrap roots, docs, examples, and manifests against implementation
- [ ] T160 Request independent code review of #1536 focused on client/GM authority, staged wound/effect composition, same-root atomicity, replay/rollback, privacy, console/browser parity, and Mortal/afterlife documentation synchronization, and record findings in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T161 Verify every review finding against code/spec, add a focused RED test for accepted defects, implement only substantiated changes, and rerun the smallest affected filters
- [ ] T162 Inspect `git diff --check`, all changed files, generated state/examples, and `tasks.md`; remove placeholders, obsolete fallbacks, dead paths, and untracked artifacts while preserving unrelated user work
- [ ] T163 Run one final `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` without an adjacent redundant Fast run and record exact frontend/build/TRX/count/timeout/cleanup evidence
- [ ] T164 Commit all verified #1536 code/tests/docs/spec task evidence, push `1536-complete-wound-materialization`, open a PR linked to #1536 with architecture/no-migration/GM-sync/verification/residual-risk summary, and record the PR URL in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T165 Inspect the GitHub PR diff/checks and remote branch, address only verified issues with focused tests, and confirm every required file is present before approval
- [ ] T166 Merge the approved PR into `main`, verify the merge commit on `origin/main`, close #1536 only when all accepted scope is present, and leave the linked Saref follow-up open if T148 required it

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependency.
- **Foundational (Phase 2)**: depends on Setup and blocks every story.
- **US1 (Phase 3)**: depends on Foundational and establishes accepted creation.
- **US7 (Phase 4)**: depends on US1 so repair/replay can exercise a complete accepted wound; its safety boundary blocks setting adapters.
- **US2 (Phase 5)**: depends on US1 and US7.
- **US3 (Phase 6)**: depends on US1 and US7; can proceed in parallel with US2 after those prerequisites.
- **US4 (Phase 7)**: depends on US3 and the common US7 safety boundary.
- **US5 (Phase 8)**: depends on complete Mortal US2 and spiritual US4 application semantics; commands are not exposed earlier.
- **US6 (Phase 9)**: depends on US4 healing and US5 guided flow.
- **Polish (Phase 10)**: depends on all desired stories; #1536 requires all seven.

### User story independence

- **US1** independently proves bounded atomic wound creation for every owner.
- **US7** independently proves repair/retry/rollback for the common creation contour.
- **US2** independently proves two complete Mortal lifecycles without the afterlife UI.
- **US3** independently proves conflict occurrence/defeat without active healing.
- **US4** independently proves afterlife treatment/recovery through service-neutral helpers.
- **US5** independently proves player inspection/targeting against seeded complete wounds.
- **US6** independently proves provider/access/payment behavior using the shared healing flow.

### Within each story

1. Complete all listed RED tests and observe the intended failure.
2. Implement the minimum strict domain behavior to make them GREEN.
3. Update GM prompts/docs/examples and their RED guards in the same story phase.
4. Run the smallest owning Focused filters and record evidence before the checkpoint.
5. Do not mark a task complete from an agent report alone; inspect diffs and test output.

## Parallel Opportunities

- Phase 1 test builders T002-T004 own separate files after T001's path vocabulary is agreed.
- Foundational parser/identity/carrier/history/transition/consequence tests T005-T010 can be authored in parallel; cache/snapshot/scale tests T012-T014 can follow the staged contract in parallel.
- US1 opportunity, guarantee, target, output, and owner tests T029-T034 own separate files.
- US7 repair privacy/cache tests T046-T048 and replay test T050 are independent test-authoring tracks before shared implementation.
- US2 contract/diagnosis/authority/resolver/recovery tests T058-T062 can be authored in parallel.
- US3 formula/mode/seal/defeat/consequence tests T076-T080 can be authored in parallel.
- US4 art/resolver/combat/safe-cycle/recovery/entity tests T093-T098 can be authored in parallel.
- US5 projection/target/privacy/console/frontend tests T113-T118 can be authored in parallel after application semantics are fixed.
- US6 quote/service/payment/Elyara/Shining tests T131-T135 can be authored in parallel.
- Cross-cutting Saref/source/docs/terminology/untrusted-text audits T148-T152 own separate files.

## Parallel Examples by Story

```text
US1: T029 opportunity tests || T030 guarantee tests || T031 target tests || T032 output tests
US7: T046 repair shape || T047 privacy || T048 cache invalidation || T050 replay
US2: T058 routes || T059 diagnosis || T060 exact refs || T061 resolver || T062 recovery
US3: T076 formula || T077 modes || T078 conflict seal || T079 defeat || T080 consequences
US4: T093 arts || T094 resolver || T095 combat || T096 safe cycle || T097 recovery || T098 entities
US5: T113 projection || T114 targets || T115 privacy || T116 console || T118 frontend
US6: T131 quotes || T132 service contract || T133 payment || T134 Elyara || T135 Shining
```

Parallel test authoring does not authorize parallel edits to the same accepted-mechanics,
conflict, profile, or application production files. Shared implementation is integrated
sequentially through the story checkpoint.

## Implementation Strategy

### Engineering MVP

The smallest safe engineering increment is Setup + Foundational + US1 + US7. It proves
strict atomic creation and repair but intentionally exposes no incomplete player command.

### First player-complete increment

All P1 stories are required together: US1, US7, US2, US3, and US4. This yields complete
Mortal and spiritual wound lifecycles but still keeps guided commands disabled until US5.

### Complete #1536 delivery

1. Finish common foundation and safe creation/repair.
2. Finish Mortal and spiritual domain lifecycles.
3. Expose shared console/browser inspection and treatment only after both are complete.
4. Add provider/faction access and synchronize every GM surface.
5. Run cross-cutting audit/review/final controls, merge, then close #1536.

No slice may ship a GM output path or player command whose accepted lifecycle is
incomplete. Commits may follow checkpoints on the same branch; one final PR may contain
the complete feature unless a later explicit decision splits reviewable PRs while
retaining this task/spec authority.

## Notes

- Test lane limits are bounded defaults. Use measured explicit headroom for a coherent
  selection that legitimately grew; do not discard coverage to win seconds.
- `memory_suppression` remains an independent Saref effect. T148 creates a follow-up
  issue only if current runtime authority is incomplete.
- No migration, compatibility parser, dual write, or old-save fallback is permitted.
- Every dynamic wound/provider text surface is untrusted and must be escaped/sanitized.
- Update task checkboxes only after inspecting implementation and verification evidence.
