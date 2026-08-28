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
- [X] T007 [P] Add RED player, dedicated NPC, combatant/group-member, afterlife profile, duplicate occurrence, wrong-realm, and promotion carrier tests in `BookOfEternityClient.Tests/WoundCarrierCatalogTests.cs`
- [X] T008 [P] Add RED append-only ordinal/fingerprint chain, operation-key replay, terminal continuity, healed-without-carrier, and no-reopen tests in `BookOfEternityClient.Tests/WoundHistoryStateTests.cs`
- [X] T009 [P] Add RED create/worsen/complicate/diagnose/stabilize/treat/recover/heal/legacy/archive transition, independent mechanical/cosmetic legacy, and forbidden regression/domain-conversion tests in `BookOfEternityClient.Tests/WoundTransitionReducerTests.cs`
- [X] T010 [P] Add RED exact version-1 Mortal/spiritual primitive registry, per-severity magnitude/cadence/expansion limits, Mortal non-display-impact minimum/maximum, spiritual exact slots, hidden component packing, safe-exit, and independent effect tests in `BookOfEternityClient.Tests/WoundConsequenceEnvelopeTests.cs`
- [X] T011 Add RED direct-cutover `ownedEffectSources` definition-graph/root-binding parsing, mandatory detached exact-empty arrays, null object/array/element rejection, exact/confusable identity, graph completeness/reachability, legal zero-edge graph, optional single-leaf `apply_definition.maxExpansion=2`, five-definition/five-root bounds including the exact four-slot-plus-leaf rejection boundary, unique stack keys with exact `maxStacks=1` and legal #1535 policy combinations, root-bound reaction target exact same-domain `replace` with all other policies rejected, canonical definition/root/slot ordering and whole-wound fingerprint coverage, detachment, complication-root subset/pairwise-disjoint ownership plus exact removal/pruned-graph/recomputed-slot after-image and global terminal provenance, zero-slot direct/descendant marker ownership, exact empty root parameters, parameterized descendant-without-root-ID, full severity source-group teardown, and staged prepare/complete-source-graph/non-public-source/typed root-operation-batch/effect-owned opaque-ID/result-map/finalize tests, including a deterministic `book_of_eternity.wound.effect_materialization` version-1 writer over exact source/schema/parameters/ordered fully bound components, recursive object-key canonicalization with component-order sensitivity, internally derived expected and actual count/fingerprint values, source-export seal coverage, independent created-effect after-image recomputation, zero-slot marker and one-effect/two-slot cases, well-formed mismatch, malformed fingerprint, and atomic no-plan failure; prove every receiving boundary rejects payload tampering under an unchanged predecessor fingerprint (source export/wound preparation at composer, effect input at effect planner, effect plan at wound finalizer), a legal non-mechanical Mortal wound emits a sealed zero-operation batch and empty result with no effect ID/changed effect after-image, ordinary GM apply is rejected, a new same-turn wound uses `sourceRef`, every root receives a unique derived `createdEventRef`, the shared accepted wound event remains separate `causalEventRef`, every root receives a newly created ID, and missing/duplicate/confusable/non-creating/extra or field-mismatched results fail in `BookOfEternityClient.Tests/WoundMaterializationContractTests.cs`, `BookOfEternityClient.Tests/WoundConsequenceEnvelopeTests.cs`, `BookOfEternityClient.Tests/WoundTransitionReducerTests.cs`, and `BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs`; add `BookOfEternityClient.Tests/SpiritualWoundEffectProfileContractTests.cs` proving exact eight-profile common-registry parity, closed payloads, persistent-afterlife-actor/wound-link scope, rejection of Mortal/`spiritual_conflict_side`/missing-link definitions, architectural separation from `afterlife_combat_condition`, safe generic player projection, and lossless spiritual owned-source graph round trip

**T011 spiritual registry RED minimum**: exercise every exact target kind (`player`,
`guardian`, `resident`, `radiant_actor`, `afterlife_actor`), both legal current realms,
and every `allowedRealms` member; reject a Mortal current/allowed realm, every other
target including `spiritual_conflict_side`, missing/wrong-kind/wrong-role/duplicate or
confusable wound-source links while permitting independent legal context links, and
missing/extra/null/wrong-axis/wrong-or-ineligible-operation/wrong-type-or-value
`{operation,axis,magnitude}` members plus a confusable profile. Pin exact scope codes
`effect_source_definition_spiritual_wound_realm_invalid`,
`effect_source_definition_spiritual_wound_target_invalid`, and
`effect_source_definition_spiritual_wound_link_invalid`, retaining common malformed
component wrapper codes. Accept ordinary `source_bound(active, expire)` without the
finite condition adapter, and keep the corresponding persistent-actor
`afterlife_combat_condition` lifetime rejection as an already-GREEN control.
- [X] T012 [P] Add RED wound command/input/fingerprint/before-image/cache equality, internally computed common `PreparedPlanFingerprint`, independent cache recomputation, source/export/result/event/effect-after-image/final-binding cross-plan tamper rejection, stale generation, take-once, and invalidation tests in `BookOfEternityClient.Tests/AcceptedMechanicsPlanCacheTests.Wounds.cs`
- [X] T013 [P] Add RED wound carrier/index/history/command/pending/scheduler/output snapshot inclusion and missing-before-image rejection tests in `BookOfEternityClient.Tests/PendingTurnSnapshotAuthorityTests.Wounds.cs`
- [X] T014 [P] Add RED exact persisted/same-turn wound source export, `Materializable=false` raw-GM-apply rejection with typed-root and reaction-only authorization, mandatory `sourceRef` for new wounds, closed wound-owner/effect-target mapping, exact `SourceExportFingerprint` and reconstructed `base_wound | complicationId` root-lineage-authority coverage including existing canonical `EffectId` versus new `applicationRef` selectors, full-key/group and first-create causal-parent indexes, direct-root empty versus reaction-child exact singleton `sourceEffectIds`, legal mixed create-causality plus replacement-succession evidence, terminal-root descendant traversal, independent wound-owned effect terminalization that preserves byte-identical canonical wound state, semantic fingerprint, complete definition graph, root bindings, entries, severity, care, recovery, and history, root-bound target exact same-domain `replace` plus pre-mutation cross-domain stack/refresh/merge/replace rejection, cyclic/multiple-causal-create-parent/duplicate-same-kind/foreign-lineage rejection, 160 same-turn and 10,000 pre-turn definition/root bounds, at-most-five simultaneous source members and one-visit-per-parsed-source-member linear work, exact created-event/causal-event agreement, trusted common composition/finalization and cross-plan tamper rejection, and version-1 aggregate tests in `BookOfEternityClient.Tests/EffectIdentityStateTests.Wounds.cs`, `BookOfEternityClient.Tests/EffectSourceAuthorityTests.Wounds.cs`, `BookOfEternityClient.Tests/EffectAcceptedTurnInputComposerTests.Wounds.cs`, `BookOfEternityClient.Tests/WoundEffectLineagePlannerTests.cs`, `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.Wounds.cs`, and `BookOfEternityClient.IntegrationTests/WoundAcceptedMechanicsScaleTests.cs`

### GREEN implementation

- [X] T015 Implement strict schema-version-1 roots, common envelope records, parsers, exact identifiers, bounds, and canonical serialization in `BookOfEternityClient/Services/WoundMaterializationContract.cs`
- [X] T016 Implement client-owned identity index parsing, active/terminal agreement, exact fingerprints, and canonical serialization in `BookOfEternityClient/Services/WoundIdentityState.cs`
- [X] T017 Implement owner-appropriate occurrence indexing and exact owner resolution for player/NPC/combatant/afterlife carriers in `BookOfEternityClient/Services/WoundCarrierCatalog.cs`
- [X] T018 Implement append-only history, operation replay probes, terminal continuity, and the closed transition reducer in `BookOfEternityClient/Services/WoundHistoryState.cs` and `BookOfEternityClient/Services/WoundTransitionReducer.cs`
- [X] T019 Implement the exact version-1 Mortal/spiritual registry, slot derivation, non-display-impact minimum, severity-specific magnitude/cadence/expansion/scope envelopes, and aggregate safe-exit checks in `BookOfEternityClient/Services/WoundConsequenceEnvelopeCatalog.cs`
- [X] T020 Implement strict canonical `ownedEffectSources` with a complete immutable definition graph and separate root effect bindings, direct schema-version-1 cutover, canonical serialization, non-null mandatory empty-array handling, legal zero-edge and optional single-leaf `apply_definition.maxExpansion=2` graphs, exact five-definition/five-root and four-slot-plus-leaf bounds, zero-slot direct/descendant marker tracking, exact empty root parameters with bounded persisted descendant parameters, unique stack keys/exact `maxStacks=1`/legal #1535 policy combinations, root-bound reaction target exact same-domain `replace`, graph-preserving consequence validation, pairwise-disjoint complication-root ownership and exact root/slot removal plus remaining-root reachability pruning/recomputed-slot after-image, full source-group severity lifecycle reduction, staged wound preparation/finalization with a sealed internal reducer/history transition authority bound to the prepared input and local/permanent wound identities, detached non-public complete source graph plus immutable typed `WoundEffectOperationBatch`, stage-specific detached-payload fingerprint writers including shared `WoundEffectMaterializationFingerprint` version 1 over exact source/schema/parameters/ordered fully bound components, internally derived `ExpectedComponentCount`/`ExpectedMaterializationFingerprint` and result `ComponentCount`/`MaterializationFingerprint`, effect-batch recomputation before allocation, zero-operation/empty-result finalization without effect allocation or changed effect after-image, no wound-side effect-ID allocation, effect-plan-owned opaque root allocation, detached exact `applicationRef -> effectId` results with created/causal event and source/target/carrier/materialization agreement, effect-planner recomputation of `EffectInputFingerprint`, wound-finalizer recomputation of `EffectAcceptedTurnPlanFingerprint` plus each created effect's materialization from its detached after-image, exact `wound_plan_prepared_seal_mismatch`/`wound_plan_effect_handoff_invalid`/`wound_plan_effect_stage_failed`/`wound_plan_effect_result_agreement_mismatch` failure separation, descendant-no-application/no-preallocation enforcement, reciprocal link validation, and immutable after-images in `BookOfEternityClient/Services/WoundMaterializationContract.cs`, `BookOfEternityClient/Services/WoundConsequenceEnvelopeCatalog.cs`, `BookOfEternityClient/Services/WoundTransitionReducer.cs`, `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs`, `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs`, and `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`; register all eight spiritual wound profiles as deterministic first-class components with only `profile_specific` merge, shared profile-invariant validation, exact afterlife persistent-actor/wound-source scope, ordinary #1535 lifetime, registry parity in the wound envelope, and safe player projection in `BookOfEternityClient/Services/EffectComponentProfiles.cs`, `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs`, and `BookOfEternityClient/UI/EffectPlayerProjection.cs`; synchronize `BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs`, `BookOfEternityClient.Tests/SpiritualWoundEffectProfileContractTests.cs`, `BookOfEternityClient.Tests/WoundContractTestData.cs`, and `BookOfEternityClient.IntegrationTests/WoundMaterializationTestFixtures.cs`
  T020 internal handoff note: retain detached pre-turn wound carriers, identity, and
  history in a prepared baseline-authority record whose independent seal binds their
  fixed-order canonical bytes to the prepared input; Finalize must recompute it before
  producing collection seals or identity/history after-images.
  Prepared slot ordinals are correlations only; after opaque effect allocation, derive
  and independently verify canonical per-batch result ordinals by actual `effectId`
  order plus prepared within-root semantic order before persisting entries.

- [X] T021 Extend generation-scoped accepted authority and cache invalidation with prepared/final wound plans, internally computed common `PreparedPlanFingerprint`, independent detached-plan recomputation, and publication-time take/peek verification in `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs`
- [X] T022 Replace loose wound-source scans with canonical persisted definition graphs and accepted same-turn wound source exports, enforce checked 160 same-turn and 10,000 pre-turn wound definition/root aggregate bounds before composition, export wounds as `Materializable=false`, reject ordinary GM wound-definition apply, add an exact fingerprinted typed-root canonical-binding resolver without granting whole-source materialization, require `sourceRef` for new wound applications, implement the closed owner-to-target adapter, independently recompute `SourceExportFingerprint` and `WoundPreparationFingerprint` from detached payload in `EffectAcceptedTurnInputComposer`, reconstruct and seal root lineage authority as `base_wound | complicationId`, index exact `(realm, wound, woundId, definitionKey)` source coordinates plus `(realm, wound, woundId)` groups and first-create causal parent/child identity lineage, record exactly the producing effect in reaction-created first-create `sourceEffectIds`, ignore replacement succession for ownership traversal while preserving it as lifecycle evidence, reject cross-domain stack/refresh/merge/replace before mutation, admit every graph definition, and resolve later `apply_definition` descendants only through the sealed reaction executor when their definition and causal parent lineage belong to the active source graph while preserving existing static wound-source validation in `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`, `BookOfEternityClient/Services/EffectSourceAuthority.cs`, `BookOfEternityClient/Services/EffectIdentityState.cs`, and `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- [X] T023 Add the canonical common-plan detached-payload fingerprint writer and extend common input bindings, authority and non-interchangeable stage fingerprint fields, planning context, exact subordinate result maps and after-images, trusted common `PreparedPlanFingerprint`, touched/consumed paths, and path coverage for wounds in `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs`
- [X] T024 Compose prepare -> typed wound-effect batch -> effect-owned ID/result map -> finalize -> resource ordering, derive globally unique internal effect-operation events for every batch member while retaining the shared wound event as separate causal chronology, require exact result set/cardinality/new-identity/source/target/event/carrier outcomes, compose same-root results, reject cross-plan stage mixing, terminate pairwise-disjoint declared complication first-create causal-lineage closures (including from terminal roots), remove those roots/slots, prune only definitions unreachable from all remaining roots, recompute slot use, and retain terminal provenance in effect identity history; perform full old source-group teardown before severity rematerialization, terminate the complete active wound-source group on healing, preserve byte-identical canonical wound state and fingerprint when one wound-owned effect independently expires, is suppressed, or is dispelled, preserve no wound mutation for ordinary descendant reaction materialization, and expose linear work statistics proving visited identities do not exceed parsed exact source-group membership in `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- [X] T025 Add the explicit accepted-turn wound completeness phase plus strict raw input/root composition and planning handoff in `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`, `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs`
- [X] T026 Publish and read-back-validate wound carriers/index/history/pending after-images only through the common write lease in `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Wounds.cs`
- [X] T027 Extend snapshot collection/rollback and accepted post-validation with exact wound and output paths in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` and `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [X] T028 Run the smallest focused common-kernel filters through `scripts/test-csharp.ps1`, record RED-to-GREEN evidence and durations in `specs/1536-complete-wound-materialization/tasks.md`, and resolve every foundational failure before US1

T014/T022/T024 closure evidence: commits `2610916c` and `270193d7`; Focused
lineage `27/27` (`20260828-014902-256-5936-aa690e20e7ba4f55afb78076a31e80ee-focused`),
wound batch `186/186` (`20260828-015009-351-37380-2e5c1703c782408b878fed3a0275521d-focused`),
effect planner `71/71` (`20260828-015033-974-39460-0f411068bb8d4edeb6c3f442ac77e799-focused`),
common cache `181/181` (`20260828-015055-084-8980-840ecfb6e5d74e71b4b10bbbe4df0e3f-focused`),
and resource-routing integration `69/69` (`20260828-013931-489-18588-7f33d769e2c14ec093eeee7529463337-focused`).
The canonical source inventory is synchronized by `f86b33f1` with Focused
`2/2` (`20260828-020009-476-31152-24197f842b24404f95b05b77f461c903-focused`).
The earlier Fast checkpoint completed `5760` green tests with zero
failures/duplicates but reached the five-minute hard cap. T028 repeats and
records this control below; the runner-capacity limitation remains tracked
separately as #1550 rather than changing test infrastructure inside #1536.

T025 closure evidence (2026-08-28): the strict command/root and common-handoff
integration scenarios are GREEN `7/7`
(`20260828-145633-074-33716-6d4835f45c8c42579a1fe77e57f423fc-focused`),
the independently selectable wound completeness phase is GREEN `30/30`
(`20260828-144353-111-19864-666a201206f7490899fcec50b14b1f7c-focused`),
and the adjacent effect/common planner caches are GREEN `252/252`
(`20260828-145122-032-32608-cca5dde158c24c76ae81c7211b828bcc-focused`).
The broader effect/resource integration control completed `161/201` green
(`20260828-144439-136-34732-12d497fe45734918be90e37e069bcf22-focused`);
all 40 failures are legacy effect fixtures that still attempt ordinary GM
materialization from persisted `wound` sources. T022 intentionally made those
sources `Materializable=false`; migrating those fixtures to the sealed wound-batch
flow and resolving every resulting foundational failure remains owned by T028.

T026 closure evidence (2026-08-28): the publication/read-back RED failed `0/2`
for the expected missing wound writes and missing post-write drift check
(`20260828-150713-304-33416-b09e022b56ea4325a948e8a0281d87b4-focused`).
The completed wound validation/publication contour is GREEN `10/10`
(`20260828-151802-495-34348-3a30bec885524cbb9f4f9812a41a3a7a-focused`),
including all-carrier before-image protection and post-write identity/history
agreement. A production typed non-empty wound plan proves the carrier/index/history
write set `1/1` (`20260828-151542-113-24808-7dd7f784d84c47aba17c2651ad14a33e-focused`),
and the full common cache control is GREEN `182/182`
(`20260828-151905-961-36280-eb0a34850af94489bd44d49716e596e7-focused`).
This is an unexposed client-owned publication seam; it adds no GM-authored command,
response, pending file, or player workflow, so GM prompts/examples remain owned by
the later story tasks that expose wound authoring and treatment.

T013/T027 closure evidence (2026-08-28): the snapshot-authority API first failed
to compile as expected (`20260828-154127-963-36448-f8f0f419383b4ca1935bbf5020e18a5b-focused`),
the common plan then rejected only 4 of the required 14 missing-before-image cases
(`20260828-154547-904-38668-c37f044c7e6c4e3b975f45ac5fe1077f-focused`),
and dynamic touched-path coverage initially had no contract implementation
(`20260828-155843-246-30508-556c6218aec24dccbb9490c2fac545fc-focused`).
The completed exact present-or-signed-absent snapshot and common-plan guards are
GREEN `239/239`
(`20260828-161309-843-40640-a979df93ac05434ab10959305481c050-focused`).
Canonical publication/output-drift rollback is GREEN `11/11`
(`20260828-162030-463-36224-82ec9a39a350495baff9430a8d277a84-focused`),
and a real GameEngine lifecycle proves snapshot hashes, rollback backups, and
byte-exact restoration for all 14 foundational paths `1/1`
(`20260828-161649-472-28716-682280345f24486f8a716cdf0818d8b2-focused`).
The common plan also unions dynamically touched game/lore/output paths and rejects
arbitrary or non-canonical paths. These tasks strengthen only client-owned atomicity
and introduce no GM-authored or player-visible contract, so prompts, examples, and
afterlife documentation remain unchanged here.

T028 closure evidence (2026-08-28): the inherited integration baseline was
`161/201` with 40 legacy fixtures attempting ordinary GM application from
non-materializable persisted wound sources
(`20260828-144439-136-34732-12d497fe45734918be90e37e069bcf22-focused`).
Separating ordinary materializable skill fixtures from explicit wound guards first
reduced this to `189/201`
(`20260828-163307-021-33160-e7eebdd10e064099bf7cfbe84b84fa77-focused`),
then the direct-cutover wound guard set reached `8/8`
(`20260828-165106-473-32824-3cf3dfd2a26a496bb877cc1b2c438023-focused`).
The exact original effect/resource selection is now GREEN `201/201` in 3m27s
(`20260828-165209-803-9280-e194438275b54e278eff96f84ca872e5-focused`).
A Fast control exposed one real source guard: the reserved
`pending_wound_resolutions.json` path was absent from the afterlife inventory
(`2313/2314`, `20260828-165635-559-39216-8e227544282a4125b1699ba737be1a6e-fast`).
The inventory now explicitly excludes that future path because this foundational
slice has no runtime writer, resolver, receipt consumer, status surface, or
GM-facing pending contract; the registry guard is GREEN `6/6`
(`20260828-170027-003-32848-2d2d77437b6846dea1308f5295673522-focused`).
The repeated Fast control executed `5575/5575` green tests with zero failures or
duplicates before the known five-minute hard cap #1550 stopped remaining shards
(`20260828-170122-115-7968-1d262fc23d5e4b09a035a342d3870a4c-fast`).
Using the documented explicit Focused headroom instead of optimizing seconds, the
complete wound/common-cache/snapshot domain is GREEN `1652/1652` in 29s
(`20260828-170644-433-32604-09a45b93f2564ac2bf32410d7fdd4a6a-focused`).
No foundational functional failure remains. No matrix/example update is warranted
until a later story task adds the actual pending wound lifecycle and GM surface.

**T011/T020 execution gate**: complete these task IDs in two test-first tranches. First,
add and observe the canonical persistence/reducer RED tests, then make only that
`WoundMaterializationContract`/consequence/reducer slice GREEN. Only after that API is
stable may the staged planner test file enter the repository; observe its RED result and
complete the planner slice. Mark T011 and T020 complete only when both tranches and their
adjacent controls are GREEN.

**T011/T020 completion evidence (2026-08-27)**: both test-first tranches are GREEN and
independently reviewed with 0 Critical/Important/Minor findings. Final planner guard
SHA-256 is `98911D8B93633DC1225793015956D4DB75A765DDFF6BAE6FA4E2AE8901C54654`
(230,131 bytes, byte-identical staging/promoted). Focused artifacts:
`20260827-142757-035-44292-68349581ad9f493d99eecd0f63f687e6-focused`
(planner 176/176),
`20260827-142854-384-37124-11ee7c9106e94fae9bf7449d3494db49-focused`
(planner/adapter/carrier/source guard 251/251), and
`20260827-142945-685-36352-e0a81f92302c4142be0909a9013bb7fd-focused`
(adjacent contracts/reducer/effect authority 1101/1101). Fast control
`20260827-143140-391-22092-80c17060e5254bf7825e775630233a7c-fast`
passed 5679/5679 in 00:03:59.456 with a warning-free build.
GM synchronization rationale: this tranche is an unexposed client-owned planning and
validation seam; it adds no command, pending/control file, normalizer publication,
player projection, or GM-authored response surface. Runtime integration and its required
Mortal/afterlife prompts, worked examples, manifests, and guards remain explicitly
tracked by T022-T027 and the story-specific documentation tasks.

**Checkpoint**: Strict wound roots, identity/history, carrier agreement, staged effect composition, common publication, replay, and rollback are GREEN with no exposed player workflow.

---

## Phase 3: User Story 1 — Receive a Complete and Fair Wound (Priority: P1) 🎯

**Goal**: Accept an optional or guaranteed, GM-authored, client-bounded physical or spiritual wound with exact owner/source/severity/effects/history/narration in one transaction.

**Independent Test**: Resolve one wound-capable event, accept none/lower/legal/guaranteed cases, reject over-limit/contradictory cases, and prove exact wound/effect/history/output atomicity for every owner carrier.

### RED tests

- [X] T029 [P] [US1] Add RED formal and narrative event opportunity, harmless-result conflict, none/lower/equal/over-maximum decision, and consumed-decline tests in `BookOfEternityClient.Tests/WoundOpportunityAuthorityTests.cs`
- [X] T030 [P] [US1] Add RED pre-materialized guaranteed trigger, omitted result, source-state, hard-cap conflict, and exact retry tests in `BookOfEternityClient.Tests/WoundGuaranteedTriggerTests.cs`
- [X] T031 [P] [US1] Add RED exact player/NPC/combatant/Guardian/resident/radiant/soul target, named ambiguity, wrong realm, and source-event binding tests in `BookOfEternityClient.Tests/WoundSourceTargetAuthorityTests.cs`
- [X] T032 [P] [US1] Add RED acquisition narration completeness/contradiction, deterministic Russian notification, untrusted markup escaping, and no-ID projection tests in `BookOfEternityClient.Tests/WoundAcquisitionOutputTests.cs`
- [X] T033 [US1] Add RED end-to-end optional/lower/guaranteed/over-limit, wound-owned effect atomicity, healed independent mechanical legacy, and cosmetic History legacy fixtures in `BookOfEternityClient.IntegrationTests/WoundMaterializationLifecycleTests.Creation.cs`
- [X] T034 [P] [US1] Add RED all-owner carrier/index/history agreement, combatant persistence, Mortal-to-afterlife no-conversion, and accepted spiritual profile realm-preservation transition cases in `BookOfEternityClient.IntegrationTests/WoundMaterializationLifecycleTests.Owners.cs`

T029 RED evidence (2026-08-28): the new formal/narrative opportunity,
harmless-conflict, legal severity range, over-maximum, and consumed-decline replay
contract reaches the production assembly and fails compilation only on the first
intentionally absent US1 surface, `WoundOpportunityBuildRequest` (0 warnings,
1 expected error, no tests executed, no timeout/duplicates;
`20260828-171614-101-21144-606fe2f437e94fb4a7b271eb9b8421d0-focused`).
No production file is changed by this RED checkpoint.

T030 RED evidence (2026-08-28): the pre-materialized guarantee, same-turn
rejection, inactive/consumed/suspended source-state, omitted/exact result,
hard-cap conflict, and exact retry contract reaches the same intentionally absent
US1 input boundary (0 warnings, 2 expected compiler errors across T029/T030, no
tests executed, no timeout/duplicates;
`20260828-171916-306-34132-d084bbe8f687450da9178a3ed187d916-focused`).
Production remains untouched.

T032 RED evidence (2026-08-28): acquisition narration presence and exact structured
agreement, deterministic physical/spiritual Russian notifications, console/browser
escaping, and a player-visible no-ID/no-authority projection are specified. Focused
stops on eight intentionally absent T029-T032 types with 0 warnings, no timeout or
duplicates (`20260828-172813-800-36780-6e5028e3411940b899b272550e66a08f-focused`).
Production remains untouched.

T031 RED evidence (2026-08-28): exact player/NPC/combatant/Guardian/
resident/radiant/player-soul owner-to-effect-target mappings, duplicate display-name
ambiguity, cross-realm rejection, and exact source/event/fingerprint binding are now
specified. Focused stops on seven intentionally absent T029-T031 authority types with
0 warnings, no timeout/duplicates, and no executed tests
(`20260828-172341-053-27028-eb6c5f5031794e72bc2b17dd8390421b-focused`).
Production remains untouched.

T033 RED evidence (2026-08-28): the accepted response now has end-to-end fixtures for
an optional decline, a lower-than-cap materialization, an omitted guaranteed result,
an over-cap proposal, atomic wound-owned effect/carrier/index/history publication,
and separation of healed cosmetic versus independent mechanical legacies. Focused
reaches the production assembly with 0 warnings and stops only on the nine expected
uses of the intentionally absent strict `WoundDecisions` / `WoundResponseInputComposer`
surface; no tests execute, no timeout or duplicate IDs occur
(`20260828-183829-283-25676-96135e584bca44b1b84be6c0fe064ef7-focused`).
Production remains untouched.

### GREEN implementation

- [X] T035 [US1] Implement sealed Mortal/mechanical/narrative/spiritual wound opportunities, guarantees, safe GM context, and consumed decision authority in `BookOfEternityClient/Services/WoundOpportunityAuthority.cs`
- [X] T036 [US1] Add the strict response proposal/decision fields and remove permanent-ID authority from GM input in `BookOfEternityClient/Models/GameResponse.cs` and `BookOfEternityClient/Services/WoundResponseInputComposer.cs`
- [X] T037 [US1] Bind exact event/source/target/realm/profile and guaranteed evidence during wound preparation in `BookOfEternityClient/Services/WoundSourceAuthority.cs` and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- [X] T038 [US1] Wire optional, lower-than-cap, guaranteed, declined, and worsening US1 response proposals plus exact opportunity/source authority into the completed T020/T022/T024 typed wound-batch/finalization API without adding a second effect path in `BookOfEternityClient/Services/WoundResponseInputComposer.cs` and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- [X] T039 [US1] Compose create/worsen/decline after-images, client-owned wound IDs, effect-plan-owned root IDs, full old source-group teardown before worsen rematerialization, history, owner carriers, and combatant promotion transitions in `BookOfEternityClient/Services/WoundAcceptedOwnerCarrierAuthority.cs`, `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`, and `BookOfEternityClient/Services/WoundTransitionReducer.cs`
- [X] T040 [US1] Add escaped acquisition narration validation and deterministic player notification/output binding to the accepted turn in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` and `BookOfEternityClient/UI/WoundPlayerNotification.cs`
- [X] T041 [US1] Replace direct `playerWoundChanges`/`NPCWoundChanges` distribution with strict accepted wound command consumption in `BookOfEternityClient/Configuration/FileMapping.cs`, `BookOfEternityClient/IO/StateDistributor.cs`, and `BookOfEternityClient/Models/GameResponse.cs`

T035 GREEN evidence (2026-08-28): the opportunity authority now recomputes the
accepted event-evidence seal, applies ordinary hard caps, fails closed on harmless
conflicts, seals pre-materialized active guarantees without inventing a compromise,
projects bounded safe context, and derives replay-stable consumed decision/operation
authority. The first semantic run reached all 44 new tests (43 pass/1 over-broad
escaping assertion); after correcting that assertion, the full new authority/output
set plus the existing wound effect planner is GREEN `230/230`, warning-free
(`20260828-174543-410-27288-84c076e306aa4efa86768f114a4d3dce-focused`).
The existing wound source guard remains GREEN `2/2`
(`20260828-174701-133-7656-d64c6e40fcd842b8a791584a4a8c1a4e-focused`).
`WoundSourceAuthority` and `WoundPlayerNotification` are implemented as tested pure
boundaries in the same tranche, but T037 and T040 remain open until the accepted-turn
planner and GameEngine consume them. No GM-authored/runtime response surface exists
yet, so T043/T044 documentation and examples are intentionally not advanced here.

T035 review hardening (2026-08-28): malformed event/source rows now fail closed
without throwing, and a separately valid guaranteed-trigger seal cannot be transplanted
onto another owner under a recomputed opportunity seal. The new authority regressions
are GREEN `21/21` and `15/15`
(`20260828-175501-281-33012-ceef5ff3bcea4ff0835182cee1f691f7-focused`,
`20260828-175741-134-9332-a1e363a762d24a5ba349091bf82a3359-focused`).

T036 GREEN evidence (2026-08-28): `GameResponse.woundDecisions` now accepts only
strict `none` or ID-free `materialize` proposals. The pure composer binds one decision
to one sealed opportunity, derives complication/transition/root-application local
coordinates, validates a complete ephemeral canonical wound/effect graph, produces
detached typed drafts and notifications, and emits a sealed client command without
granting the GM `woundId`, `effectId`, `complicationId`, or transition authority.
Malformed nested scalars fail closed rather than throwing. The owning response/decision/
guarantee/legacy contour is GREEN `13/13`, warning-free
(`20260828-190346-049-19220-89e7b4c3dba743f388c5e9992a62d6e4-focused`),
the explicit malformed-scalar regression is GREEN `1/1`
(`20260828-185947-824-29544-c429eb338bf54823a29db9245571414b-focused`),
and the wound source guard remains GREEN `2/2`
(`20260828-190216-860-2028-415de2c7a56c463dbfc5983be64c9fdf-focused`).
The two publication fixtures intentionally remain RED at the existing nonempty command
adapter until T037/T038; T040-T044 still own runtime output and GM documentation wiring.
T037/T038 accepted-command RED expansion (2026-08-28): the integration contour now
also requires an exact accepted event, an accepted owner/effect-target binding even for
a declined opportunity, and duplicate-preserving strict parsing of nested sealed
opportunity fields. The expected pre-adapter state is `13/18` GREEN with exactly those
three authority regressions plus the existing decline/create publication fixtures RED,
warning-free and without timeout
(`20260828-191957-736-31776-8eeccdf68b0b43e58a8a2590728fff92-focused`).
The expanded wound/cache/snapshot checkpoint initially exposed one stale cache-test
builder, not 62 independent runtime failures; after resealing its intentionally changed
request coordinates, the exact same control is GREEN `1700/1700`, warning-free and
without duplicates or timeout
(`20260828-180057-724-33360-eb419aa63f9b4900be317353c3ad18b3-focused`).

T037 GREEN evidence (2026-08-28): accepted wound commands are now reparsed from
duplicate-preserving raw JSON, typed-recomposed byte-semantically, and bound to the
exact runtime event kind/authority/evidence seal plus the current effect target. The
planner rejects foreign events, unresolved owners even on a decline, changed source/
realm/profile/guarantee seals, and malformed nested command authority before allocation.
The same common effect planner remains the only root-ID allocator, and publication
independently rebuilds its canonical source catalog with the validated same-turn wound
stage rather than bypassing stale-plan checks. The owning integration contour is GREEN
`18/18` and warning-free
(`20260828-194143-970-30396-78849bd3926a44a9a1176e142f7c05d9-focused`);
the opportunity/guarantee/source-target/effect-stage/source-guard contour is GREEN
`224/224`
(`20260828-194431-494-6488-d1a4d1f30137497881929e1b1b7b54ab-focused`).
The explicit post-seal source/profile/owner/event-kind/guarantee mutation matrix is
GREEN `5/5`
(`20260828-195246-611-3428-974376736bdc46d5bdf04904cf16f58f-focused`).
The meaningful Fast checkpoint reached `3818/3819` before exposing one stale test-only
event-kind builder; that exact regression is GREEN after resealing its matching
opportunity (`20260828-195052-313-17096-ae1d4c79e1fd41d2b8355567dc5a4e7a-focused`).
T038 remains open for successful guaranteed publication and the worsen path; T039 still
owns their complete canonical after-images and owner transitions.

T034 RED evidence (2026-08-28): the integration matrix now covers every closed wound
owner kind with exact carrier/index/history agreement, same-turn combatant reference
allocation before wound creation, Mortal physical-wound non-conversion on entry to the
afterlife, and same-realm accepted afterlife profile updates that preserve spiritual
wounds while accepting unrelated owner fields. Focused compilation is warning-free and
stops only on the three expected uses of the intentionally absent
`WoundAcceptedOwnerCarrierAuthority`; no tests execute and no timeout or duplicate IDs
occur (`20260828-202335-226-44312-b9a17c0035314e7a8607301dd9301508-focused`).

T034 GREEN / T039 owner-baseline checkpoint (2026-08-28): accepted combat and
afterlife owner roots are now composed with client-owned pre-turn `activeWounds`
collections before wound preparation. Existing wounds remain byte-equivalent, GM
injections are discarded, new permanent combatants/NPCs/profiles receive exact empty
collections, and disappearance of an active wound fails closed pending a separate typed
owner transition. The same contour exposed and fixed the existing normalized
`chaos_sea|shining_abode` player-soul realm projector bug by resolving afterlife tokens
before the generic Mortal fallback. The complete lifecycle class is GREEN `35/35`,
warning-free
(`20260828-203502-794-16132-3232389c3419479b8c09f8324a90d494-focused`);
adjacent carrier/cache/root-assembly/effect-input/source-guard controls are GREEN
`261/261`
(`20260828-203729-965-20712-0dbbf85b708747fc89637aa671736f62-focused`).
T039 remains open for guaranteed/worsen after-images, old source-group teardown, and
the complete common-plan owner transition contour. This checkpoint adds no GM-authored
surface or afterlife pending/control/response field, so prompts, examples, manifests,
the Afterlife Contract Matrix, and documentation guards require no update here.

T038/T039 completion evidence (2026-08-28): guaranteed creation and exact worsening now
use the same sealed wound-batch/effect-plan/finalization path as ordinary creation. A
worsen opportunity seals the complete active before-wound and cause; preparation
resolves it against carrier and identity baselines, reuses the stable `woundId`, plans
full terminal teardown of the old root/descendant effect lineage, and lets the ordinary
effect planner allocate a fresh replacement root set. Finalization emits an `update`
carrier mutation, replaces the identity row, preserves creation facts and
`maximumAtCreation`, and appends the next `worsen` history row. Stale before authority
fails before mutation. The exact worsening lifecycle and stale-target checks are GREEN
(`20260828-213500-112-35696-fff4b4c7894f46d690964c6bba51abd1-focused`,
`20260828-214006-517-44160-9106f5216f214f488ab8e9957af4ad86-focused`).
Independent seal/fingerprint coverage is GREEN `189/189`
(`20260828-214525-451-41664-4d1142b4b3f4467391e851a78f354e1e-focused`),
the adjacent cache/reducer/opportunity contour is GREEN `332/332`
(`20260828-214655-479-34328-5e515aa592c04b93a36cf49da339d91e-focused`),
and the complete lifecycle class is GREEN `38/38`, warning-free
(`20260828-214746-595-45676-444a697ec7d243da82a5517c060dd303-focused`).
T040 still owns accepted-turn output/notification publication. No GM-facing contract or
afterlife pending/control field changes in this checkpoint, so documentation remains
owned by T042-T044.

T040 completion evidence (2026-08-28): accepted output is now recomputed from the
detached finalized wound-stage bundle, correlates every input transition, prepared
batch, and final owner-carrier mutation by exact permanent wound identity, rereads the
strict published narrative response, and publishes notifications only when the exact
acquisition narration remains present; every player projection escapes untrusted
markup. Narrative errors target only
`output/narrative_response.json`; an internal stage mismatch is client-owned and fails
closed. The take-once notification handoff survives read-only refreshes without entering
the serialized GM response, and console rendering escapes untrusted markup while
showing no wound/effect IDs. The final unit contour is GREEN `27/27`, warning-free
(`20260828-224642-879-43440-2ced4139b2b34baeb4b180efa097bff9-focused`),
the complete creation/owner lifecycle plus take-once response contour is GREEN `40/40`,
warning-free
(`20260828-224416-720-33836-248d56b4f4eb4f0a93222665bbd9c626-focused`),
and adjacent GameEngine/wound source guards are GREEN `118/118`
(`20260828-224155-205-37584-4cfb16ab5d0f42a3ace1b2b2e7d292de-focused`).
GM prompt, worked-example, manifest, and documentation-guard synchronization remains
explicitly owned by T042-T044 before the US1 checkpoint can close.

T041 completion evidence (2026-08-28): the legacy `playerWoundChanges` and
`NPCWoundChanges` response properties and field-to-file mappings are removed. Raw
`woundDecisions` are client-consumed and cannot enter generic distribution; a nonempty
wound command is written only from a successful typed composition result after an
independent strict parse and exact rebinding to the current normalized narrative and
one-to-one response decision set. The command participates in the same backup,
mutation-hook, rollback, and output-publication transaction, including byte-exact
restoration of a preexisting command on injected failure. Unit/source/refresh controls
are GREEN `16/16`, warning-free
(`20260828-231503-257-26160-7d040482fa694d50bd7443bbfe4be752-focused`);
the complete wound lifecycle is GREEN `44/44`
(`20260828-231527-257-30912-264ab76dd1f74b7cadda4a7599389684-focused`);
and the adjacent QTE distribution/rollback contour is GREEN `117/117`
(`20260828-231746-113-33628-a9c372a469c84fd9b9ac65f9a25ff76e-focused`).
Remaining legacy wound validation/fallback surfaces are deliberately owned by T149;
T042-T044 still own the GM-facing contract, examples, manifest, and prompt updates.

### GM contract synchronization

- [X] T042 [P] [US1] Add RED source/documentation guards for wound constructor, optional GM choice, guarantee, severity bounds, acquisition narration, effect separation, and the exact registered spiritual wound profile set in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs` and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [X] T043 [US1] Create the GM-facing common constructor/authority/effect contract in `OtherGuides/Wound_Materialization_Contract.md`, synchronize the wound-source and exact spiritual-profile registry sections in `OtherGuides/Effect_Materialization_Contract.md`, and update `Examples/E_CLI_Effect_Materialization.txt` plus `Examples/example_validation_manifest.json` so all exact eight registered spiritual wound profiles have closed fragments and at least one complete worked GM spiritual-wound source graph
- [X] T044 [US1] Replace the loose constructor and add optional/lower/guaranteed/rejected worked examples in `Rules/Block_5.txt`, `Examples/E_Block_5.txt`, and `BookOfEternityClient/game_master_daemon.ps1`; synchronize the remaining active wound-routing references in `Rules/Block_2.txt`, `Rules/Block_12.txt`, `Rules/Block_15.txt`, `Rules/Block_CLI_Operations.txt`, and `Examples/E_Block_16.txt`
- [X] T045 [US1] Run focused US1 unit/integration/documentation filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

T042 RED evidence (2026-08-28): seven documentation/source guards now specify the
strict common constructor, ordinary optional/lower and guaranteed decisions, legal
severity/narration/effect boundaries, mandatory daemon routing, exact runtime-derived
eight-profile spiritual catalog, manifest coverage, closed profile fragments, and one
production-valid GM spiritual-wound source graph without client identities. Unit RED
is the expected missing contract/rule/profile surface (`0/3`,
`20260828-232552-262-36208-c50750536b0747138a94eb46d4c4b3fe-focused`) plus the
expected missing daemon entrypoint (`0/1`,
`20260828-232717-038-18916-6b195d3cf3ba4b9fad0ca13db90d3ba4-focused`). Integration
RED is exactly the two absent named examples and two absent manifest entries (`0/3`,
`20260828-232732-653-41640-b16623c093e14c9e8771fa07babcb0c8-focused`). Both projects
compile warning-free; T043-T044 own the corresponding GREEN documentation.

T043 completion evidence (2026-08-28): the new GM-facing common contract documents
the strict ID-free constructor, ordinary none-through-maximum choice, guaranteed
result, exact acquisition narration, separate wound/effect lifecycles, direct schema-v1
cutover, and exact eight-profile spiritual registry. The common effect contract now
distinguishes its nine general profiles from the eight wound-specific profiles and
documents non-materializable wound-source/typed-root authority. The example corpus and
manifest contain closed fragments for every runtime-derived spiritual profile plus one
complete severity-III GM source graph; every fragment and the graph execute through
`EffectSourceDefinitionContract` in both afterlife realms. Contract/registry guards are
GREEN `2/2` (`20260828-234009-456-35664-e0d5af41e67b4af99aa1120683773499-focused`),
and manifest/parser/GameResponse-shape/production-example controls are GREEN `6/6`
(`20260828-234032-563-29748-823a643b14be42d5b30e84ca5607a7f6-focused`), all
warning-free. T044 retains the Mortal rule, four constructor examples, and mandatory
daemon entrypoint.

T044 completion evidence (2026-08-28): Rule 5.20 and four named worked responses now
use only the strict `woundDecisions[]` constructor, including optional decline,
lower-than-maximum severity, guaranteed creation, and rejected over-maximum severity.
The GM daemon copies `Wound_Materialization_Contract.md` into the session context pack,
publishes its authoritative path, and injects a mandatory opportunity-aware directive
into ordinary, repair, and terminal prompts. Active combat/schema/CLI guidance no longer
mentions the retired player/NPC wound wrappers, no longer derives wounds from percentage
thresholds, and keeps `npc_wounds.json` independent from `npc_effects.json`. The daemon
parses successfully under the PowerShell AST parser. Unit prompt/effect controls are
GREEN `5/5` (`20260828-235655-578-4108-9e1b5df0247b4b209f3c042848103cab-focused`);
manifest, JSON syntax, GameResponse shape, exact spiritual-profile, and complete-source
controls are GREEN `5/5`
(`20260828-235723-278-29848-f15bfc3be91a45548294e1b68e89d10f-focused`), with
warning-free builds. The first parser rerun correctly exposed two line-bound legacy
syntax exemptions shifted by the expanded examples; their manifest coordinates were
updated and the fresh control is GREEN. After aligning the daemon wording with the
actual `decision=none|materialize` enum, its AST parse remained clean and the exact
rule/daemon guards reran GREEN `2/2`
(`20260828-235935-513-39900-28a66e601ac54fff9e5c2c473c99c523-focused`).

T045 completion evidence (2026-08-29): the focused US1 opportunity, guarantee,
source/target, acquisition output, source-guard, UI/refresh, and GM-documentation
selection is GREEN `61/61`
(`20260829-000256-625-32552-b63f520a920c490d91c61b8e998a8dfb-focused`); the complete
creation/owner/command/response lifecycle plus manifest/parser/source examples is GREEN
`50/50` (`20260829-000423-299-46036-578336cfb5c84749ac2f71244671aa80-focused`);
and the required afterlife documentation guard is GREEN `118/118`
(`20260829-000621-443-27040-a25b8d82d0fd47be92d3578a70164da1-focused`). The first
conditional FullValidation correctly exposed that its accepted-turn scenario baseline
still omitted the mandatory empty schema-v1 wound identity/history roots and the empty
Azalia `activeWounds` carrier; after adding those exact current-schema fixtures, the
runtime manifest scenario is GREEN `1/1` with measured eight-minute headroom
(`20260829-001935-192-44600-db8a4e4072fc4c11b95cfb556e3097f5-focused`). The same run
also exposed a pre-existing 38-byte stale exact-size expectation for the unchanged
tracked Mortal save; the corrected integrity sentinel is GREEN `1/1`
(`20260829-002157-811-35304-b39948aa5b4d4cef91b06616ff2e8628-focused`). Final
FullValidation is GREEN `1848/1848` in `00:10:11`, warning-free, with no timeout or
duplicate IDs
(`20260829-002249-557-18320-77e1e13cc9c1496ba0e51716c3edd2fb-fullvalidation`).

**Checkpoint**: Wound creation is complete, fair, atomic, narrated, and independently testable; no treatment or player command is exposed yet.

---

## Phase 4: User Story 7 — Recover Safely from Invalid GM Output or Retry (Priority: P1)

**Goal**: Reject/repair invalid wound work without leaking authority or leaving partial/duplicate state.

**Independent Test**: Inject invalid owner/severity/consequence/treatment/resource and failures at every publication stage; prove bounded repair, exact rollback, and zero duplicate effects/charges/progress/output on retry.

### RED tests

- [X] T046 [P] [US7] Add RED table-driven owner/severity/slot/effect/treatment/resource/narration issue normalization, offending-path preservation, safe expected ranges, preserved siblings, required corrected response shape, and bounded candidate tests in `BookOfEternityClient.Tests/WoundRepairPacketBuilderTests.cs`
- [ ] T047 [P] [US7] Add RED hidden owner/provider/route/resource seal and private NPC data non-disclosure tests in `BookOfEternityClient.Tests/WoundRepairPacketPrivacyTests.cs`
- [ ] T048 [P] [US7] Add RED changed event/target/roll/snapshot/generation invalidation, exact packet receipt, and take-once tests in `BookOfEternityClient.Tests/WoundAcceptedTurnPlanCacheTests.cs`
- [ ] T049 [US7] Add RED corrected repair roundtrips for every representative invalid category plus failure injection before/after wound/effect/resource/profile/scheduler/history/output writes with byte/existence assertions in `BookOfEternityClient.IntegrationTests/WoundMaterializationRollbackTests.cs`
- [ ] T050 [P] [US7] Add RED 100-repeat loops for accepted event/treatment/course/cycle/payment/output identities after success, repair, crash recovery, and consumed commands, asserting zero duplicate state in `BookOfEternityClient.IntegrationTests/WoundMaterializationReplayTests.cs`

T046 RED evidence (2026-08-29): the new table-driven repair contract covers exact
owner, severity, consequence-slot, linked-effect, treatment, resource, and acquisition-
narration issue normalization; strips file/index prefixes to safe semantic paths; uses
client-bounded expectations instead of validator-internal authority prose; removes only
the offending proposal leaf while preserving every valid sibling; requires one exact
candidate decision plus the complete narration response; serializes only the closed
repair packet; accepts exactly 64 unique candidates in stable order; and rejects the
whole wave for a 65th or exact/confusable candidate reference. The Focused RED build is
warning-free and stops only at the intentionally absent `WoundRepairBuildRequest` and
`WoundRepairCandidateInput` production boundary, with no tests executed, timeout, or
duplicate IDs
(`20260829-004616-241-34108-ed55326470544e94a989feb0387a0cf6-focused`).

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
- [ ] T080 [P] [US3] Add RED exact spiritual I-IV consequence count, legal axes, counterplay/safe-exit, effect-link, exact actor-to-current-side contribution, side-relative axis, absent/duplicate/wrong-realm/ambiguous participant rejection, conflict-close persistence, and byte-identical `combatConditions[]` tests in `BookOfEternityClient.Tests/SpiritualWoundConsequenceTests.cs`; map the same actor once to each side and prove exact `actionCostAudit.player|opposition` plus `playerSideStrain|oppositionSideStrain` with byte-identical canonical payload, preserve a non-empty real `afterlife_combat_condition` sibling independently, and require a separate typed contribution collection with exact effect/component/wound-source, target actor, resolved side, operation, source/resolved axis, magnitude, and priority provenance
- [ ] T081 [US3] Add RED conflict start -> exchange -> opportunity -> GM decline/lower/create/worsen -> resolve lifecycle and rollback fixtures in `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualWoundLifecycleTests.cs`

### GREEN implementation

- [ ] T082 [US3] Add `spiritual_resilience` tier 0-V to standard afterlife art/profile/bootstrap/progression authority without adding it to operation types in `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- [ ] T083 [US3] Extend spiritual conflict start/state with declared danger mode, escalation evidence, per-side wound seals, and bounded defeat outcome in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
- [ ] T084 [US3] Implement trauma-pressure calculation, destination/mode/source caps, harmful-margin audit, one-per-side/re-trauma rules, and opportunity export in `BookOfEternityClient/Services/SpiritualWoundOpportunityAdapter.cs`
- [ ] T085 [US3] Validate danger/opportunity/strain/art audit and reject GM-authored computed fields in `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs` and `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`
- [ ] T086 [US3] Add danger, wound maximum, accepted wound/decline, and defeat consequence to player-safe conflict preview/audit in `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs`
- [ ] T087 [US3] Compose persistent profile wound transitions and typed owner-to-current-side `SpiritualWoundConflictContribution` evidence without duplicating wounds/effects or mutating `combatConditions[]`; fail closed on absent/duplicate/wrong-realm/ambiguous participant membership and clear only derived evidence on conflict close in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/SpiritualWoundConflictContributionProjector.cs`, `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs`, and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeSpiritualConflict.cs`
- [ ] T088 [US3] Implement mandatory bounded non-training defeat outcomes while preserving the existing separate optional soul-dissipation proof in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`

### GM contract synchronization

- [ ] T089 [P] [US3] Add RED danger/formula/GM-decline/lower/one-per-side/defeat/dissipation afterlife documentation guards plus exact eight-profile payload, persistent actor carrier/lifetime, forbidden `spiritual_conflict_side`, typed current-side contribution, no-`combatConditions[]` duplication, and conflict-close persistence assertions in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.SpiritualWounds.cs` and corresponding afterlife example/manifest assertions in `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- [ ] T090 [US3] Update spiritual conflict rules, terminology, GM context, and afterlife matrix with the persistent actor-effect versus transient combat-condition distinction and typed no-duplication contribution flow in `Rules/Block_21.txt`, `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, `OtherGuides/Afterlife_Contract_Matrix.md`, and `TaskGuides/CLI_Step_Main.txt`
- [ ] T091 [US3] Add no-wound, lower-than-maximum, guaranteed, over-limit repair, one-per-side worsening, persistent wound/effect survival after conflict with unchanged `combatConditions[]`, bounded defeat, and optional dissipation examples in `Examples/E_CLI_Afterlife_Turns.txt` and `Examples/example_validation_manifest.json`
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
