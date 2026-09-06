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
  T014's “direct-root empty” boundary applies to an original or genuinely new root.
  T070-B.6 extends the same persisted first-create field with one exact same-definition
  prior canonical root whenever a definition/ownership coordinate survives severity
  rematerialization; removed definitions retain terminal history and runtime authority
  distinguishes the retained generation edge from a reaction edge.

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
  T070-B.6 adds a shared origin/generation/reaction classifier: a same-definition
  first-create parent is legal only as sealed severity-generation provenance, while a
  reaction parent must remain an exact persisted `apply_definition` edge.
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
- [X] T047 [P] [US7] Add RED hidden owner/provider/route/resource seal and private NPC data non-disclosure tests in `BookOfEternityClient.Tests/WoundRepairPacketPrivacyTests.cs`
- [X] T048 [P] [US7] Add RED changed event/target/roll/snapshot/generation invalidation, exact packet receipt, and take-once tests in `BookOfEternityClient.Tests/WoundAcceptedTurnPlanCacheTests.cs`
- [X] T049 [US7] Add RED corrected repair roundtrips for every representative invalid category plus failure injection before/after wound/effect/resource/profile/scheduler/history/output writes with byte/existence assertions in `BookOfEternityClient.IntegrationTests/WoundMaterializationRollbackTests.cs`
- [X] T050 [P] [US7] Add RED 100-repeat loops for accepted event/treatment/course/cycle/payment/output identities after success, repair, crash recovery, and consumed commands, asserting zero duplicate state in `BookOfEternityClient.IntegrationTests/WoundMaterializationReplayTests.cs`

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

T047 RED evidence (2026-08-29): recursive privacy tests inject hidden owner/provider
identities, route/resource/source/provider seals, permanent wound/effect/resource IDs,
carrier paths, authority fingerprints, private NPC payloads, unrelated response data,
and GM-only notes through nested safe-context/proposal objects and arrays. The contract
requires their complete removal while retaining readable GM-authored wound content,
rejects canonical/private authority paths instead of echoing them, and detaches packet
snapshots and repeated serialization from later input/output-graph mutation. The
Focused RED build is warning-free and remains blocked only by the same intentionally
absent T051 repair DTO/builder boundary, with no tests executed, timeout, or duplicate
IDs (`20260829-005106-735-44732-50a5628e52d24198abb189b2a5f33a63-focused`).

T048 RED evidence (2026-08-29): the repair-wave cache contract now invalidates every
pending candidate when event, target, roll, snapshot, or generation authority changes;
requires exact session/request/snapshot/candidate/semantic receipt agreement; consumes
each exact candidate once without destroying valid unconsumed siblings; revokes repair
receipts together with prepared/final authority on `InvalidateAll`; and replaces prior
waves atomically when a new generation is registered. The Focused RED build is
warning-free and stops only at the intentionally absent T051 packet types plus the T053
cache surface, with no tests executed, timeout, or duplicate IDs
(`20260829-005454-253-39064-a529faa40242497a8a48233af84e24cf-focused`).

T049 RED evidence (2026-08-29): the integration contract now roundtrips corrected owner,
severity, slot, linked-effect, treatment, resource, and narration candidates through an
exact take-once repair receipt into one atomic wound/effect/index/history publication.
The rejected proposal is restored from the packet's preserved siblings by changing only
the named semantic leaf. A 24-case before/after failure matrix covers wound carrier and
identity, effect carrier and identity, resource, afterlife profile, scheduler/report,
wound history, and all three output surfaces; every row requires exact bytes and exact
existence restoration across the whole mixed present/absent set. The treatment fixture
was also aligned with the actual closed schema by replacing the nonexistent `aftercare`
leaf with required `displayName`. The Focused Integration RED build is warning-free and
stops only at the intentionally absent T051 builder/packet types and T053 repair-cache
surface, with no tests executed, timeout, or duplicate IDs
(`20260829-011751-823-21140-117e8f3898284e5d829f803a09e12933-focused`).

T050 RED evidence (2026-08-29): one hundred exact repeats of an accepted opportunity
receipt and one hundred repeats after a corrected severity repair must emit no
transition, notification, or command and leave the accepted wound/effect/resource roots
byte-identical. Separate file-backed 100-repeat contours cover ordinary success,
successful repair, an exact command retained across crash recovery, and an already
consumed command. Durable history replay now requires event, attempt, course,
course-milestone, cycle, payment, and output agreement and returns one stable
already-accepted receipt; changing any one of those seven coordinates remains a
conflict for all 100 attempts while wound/effect/resource/scheduler/output state retains
one result. The Focused Integration RED build is warning-free and stops only at the
intentionally absent T051 repair types, T053 repair-cache methods, and T055 extended
history/receipt/planner replay surface, with no tests executed, timeout, or duplicate IDs
(`20260829-013011-609-32928-64fdccecee2440f4ad30f3736b450586-focused`).

### GREEN implementation

- [X] T051 [US7] Implement bounded wound construction/repair/narration/alternative-treatment packets and recursive privacy sanitization in `BookOfEternityClient/Services/WoundRepairPacketBuilder.cs`
- [X] T052 [US7] Integrate wound repair obligations, safe harness packets, exact resubmission shape, and unrelated-response preservation in `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- [X] T053 [US7] Invalidate prepared/final wound, effect, and common handoffs together on repair/rejection/snapshot mismatch in `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs`
- [X] T054 [US7] Extend exact snapshot and post-publication agreement to scheduler, journals, quests, inventory/characteristics, debug/output, and pending wound roots in `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`
- [X] T055 [US7] Add operation/attempt/course/cycle/payment/output replay coordinates and already-accepted receipt projection in `BookOfEternityClient/Services/WoundHistoryState.cs` and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- [X] T056 [US7] Add invalid proposal, bounded repair, stale packet, rollback, and replay worked guidance in `OtherGuides/Wound_Materialization_Contract.md`, `Rules/Block_12.txt`, and `Examples/E_Block_12.txt`
- [X] T057A [US7] Resolve the inherited strict response/repair regressions exposed by the complete lifecycle filter, including duplicate-property fail-closed handling and preservation of the registered response issue codes
- [X] T057 [US7] Run focused repair/privacy/cache/rollback/replay filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

T051 GREEN evidence (2026-08-29): the new immutable repair packet boundary accepts a
stable wave of at most 64 exact/confusable-unique construction, repair, narration, or
alternative-treatment candidates; normalizes only registered wound-owned issue paths;
derives the sealed candidate-specific severity range; removes only offending proposal
leaves; and emits one closed corrected decision plus mandatory final-scene narration.
Recursive projection strips permanent wound/effect/owner/provider/resource identities,
canonical carrier paths, authority fingerprints, seals, private NPC/GM data, and
unrelated response content while preserving readable authored siblings. Input and
serialized packet graphs are detached, unsafe evidence is redacted, and protected
canonical paths or an invalid candidate fail the complete wave closed. The repair-wave
cache surface required to compile the frozen T048 contract was also introduced ahead
of its registry integration in T053. Fresh combined Focused verification is
warning-free: 41/41 packet, privacy, and cache tests pass with no timeout or duplicate
IDs (`20260829-014851-152-37584-42da496bb92e4a9992a3c80ac080ff8d-focused`). Two
RED-fixture defects found by the first execution were corrected without weakening the
contract: readable Cyrillic assertions now use the repository's relaxed JSON encoder,
and the target-authority mutation now supplies a genuinely different fingerprint.

T052 GREEN evidence (2026-08-29): real response-validation issues now retain detached
candidate authority and are projected into the closed T051 packet without exposing the
sealed opportunity fingerprint. Resource-bearing wound consequences are checked at the
raw resource-validation boundary against the exact accepted owner mapping, canonical
definition/operation policy, active owner capability, and active or same-turn capacity;
the first RED proved the former structural composer had no such authority
(`20260829-031840-993-6052-cf5cf8f5400b4bc894bc9b21ac08790d-focused`). Before
rollback, `GameEngine` captures one strict rejected wound command per packet. A retry
must resubmit the complete command root with the same session/request/snapshot, sealed
opportunity, command count/order, final-scene authority, and every non-offending sibling;
only the listed rejected semantic leaf (or the explicitly rejected narration response)
may differ. Missing, malformed, ambiguous, changed, or rollback-less retry authority now
fails closed instead of dispatching a broad GM repair. The repair request serializes the
closed wound packet directly, declares the exact `woundDecisions` obligation, and tells
the GM to repeat the coherent turn rather than patch restored canonical files.

Fresh verification is warning-free: packet/request unit contracts pass 29/29
(`20260829-035105-420-41476-818e5b31a23a4acaaeae1eb7a0bc1ae3-focused`), and the
complete rollback/repair integration filter passes 40/40, including all seven
representative invalid categories, exact unrelated-command preservation, fail-closed
retry capture, and 24 before/after publication failure boundaries
(`20260829-035205-812-9232-30d4d199ea1044ec8e89e946fc382bce-focused`). The required
Fast checkpoint executed 3867 tests with 3866 passing and only the deliberately frozen
next-task RED `WoundCache_HasNoIndependentTakeSurface` failing because T051 temporarily
introduced the local repair-wave take surface that T053 must move under common registry
authority (`20260829-035614-147-37444-a13a1e4f0cc34ad69ced4be42709a8d9-fast`). This is
present at the T051 `HEAD` boundary and T052 does not modify that cache. Durable
GM-facing worked repair/rollback guidance remains explicitly assigned to T056 after the
T053-T055 authority, snapshot, and replay contours stabilize.

T053 GREEN evidence (2026-08-29): repair waves now live under the common accepted-turn
cache and registry lock instead of exposing an independent take surface from the wound
cache. Registering a wave first invalidates prepared/final wound plans, effect plans,
the common handoff, wound/effect batches, and Mortal item authority. Changed event,
target, roll, snapshot, generation, or receipt authority closes the wave and invalidates
the aggregate state; an exact already-consumed receipt remains take-once without
destroying still-valid sibling packets. New common or wound preparation, explicit
invalidation, and filesystem session-generation rotation revoke the wave atomically.
The frozen RED failed only because the aggregate surface was absent
(`20260829-041101-286-43456-5d924ee30cf54d9e9e8a45c3d164028b-focused`). Fresh unit
authority verification passes 19/19
(`20260829-043127-023-2536-bd7b2d619d77475190aed1769a777004-focused`), and rollback plus
replay integration verification passes 42/42
(`20260829-042504-981-30120-95ef51800a2d4088b4c753bad8dcd17e-focused`). The bounded Fast
lane reached its five-minute limit only after all 5,850 executed tests had passed; the
two unfinished wound transition/consequence classes then passed 278/278 through a
Focused continuation (`20260829-043430-238-40016-d591395524764269bb33d127db5cd65a-fast`,
`20260829-044553-688-15916-d3ad105e70ca4a7e83562caab2e1b4ae-focused`). The test-runner
source guard also passes 1/1 after restoring the unchanged bounded runner
(`20260829-044747-936-30128-f1609f9c84b1410cb385b8b8d5ca0c3b-focused`). Review found no
critical defect. It did not justify coupling repair-wave generation to filesystem
session generation: those are separate epochs, while registry rotation already drops
the complete old state. It also did not justify adding a second GameEngine repair
authority beside T052's exact runtime retry obligation; T053 deliberately owns the
shared cache/registry handoff. This is an internal in-memory authority change with no
new player command, canonical state, pending/control file, response field, or GM-authored
surface, so Mortal/afterlife prompts, docs, examples, manifests, and source guards do not
require a T053 update; durable worked guidance remains assigned to T056.

T054 GREEN evidence (2026-08-29): the wound accepted-turn snapshot contract now seals
33 exact roots spanning wound carriers/identity/history, scheduler/report, every
participating journal, regular/soul/history quests, player and NPC inventory sidecars,
base and computed characteristics, the pending wound command/resolution roots, and all
three final output surfaces. `GameEngine` requires that complete publication agreement
when loading a wound baseline, including signed absence, while the normalizer's local
transaction inherits the same expanded rollback inventory. Immediately after generic
normalization and before common publication, the publisher partitions authority into
planned writes, planned deletes, and retained roots. It then proves every serialized
after-image byte-for-byte, every deletion by exact absence, and every retained root by
unchanged existence and bytes under the owning canonical write lease; any mismatch
throws and restores the whole local transaction.

The unit RED failed on the missing extended inventory
(`20260829-051549-667-33996-44efed5840314af1820dd47e324fc6c9-focused`), and all seven
representative scheduler, journal, quest, inventory, characteristic, output, and pending
drift rows RED-failed because no publication exception existed
(`20260829-051734-228-40772-e28da54b26a7475eb631f3b5181a2a01-focused`). Fresh GREEN
verification passes 39/39 snapshot-contract cases
(`20260829-052416-670-36264-57eb2b01cff24f2f8acaa94a454f8198-focused`), 10/10 common
normalizer wound cases (`20260829-052453-503-24892-c7f83a5df955460db1672e8a43a00960-focused`),
the complete 33-root GameEngine snapshot/rollback contour 1/1
(`20260829-052628-355-24520-4788e1e3166644e0982e6e91976995c4-focused`), and all 24
before/after publication failure boundaries
(`20260829-052826-831-44808-2401a36246e64774b18273ca90955114-focused`). The bounded Fast
lane reached exactly five minutes after 4,770/4,770 completed results passed; its two
unfinished shards then passed sequentially 64/64 and 1,193/1,193, completing all 6,027
Fast cases without a duplicate ID
(`20260829-053150-947-39720-1c262b5092fa4d2289a80158411ffd91-fast`,
`20260829-053825-928-29632-f107ff3b1a244640861be487dba371d1-focused`,
`20260829-053848-261-32560-a439f62c7d534623856f0d051c930ca1-focused`). The one
load-sensitive fencing row interrupted inside the timed-out shard also passes alone 1/1
in 0.73 seconds (`20260829-053757-453-32320-023d39c515bf4f7dba499d9af2bb887b-focused`),
confirming parallel saturation rather than a product regression. Independent read-only
review found 0 Critical, 0 Important, and 0 Minor issues. This is client-owned
transactional hardening of existing roots only: it adds no command, field, response,
pending action type, or GM-authored output, so Mortal/afterlife prompts, docs, examples,
manifests, and documentation guards require no T054 update.

T055 GREEN evidence (2026-08-29): strict wound history rows now persist the exact
accepted treatment attempt, course and milestone, recovery cycle, payment, and output
coordinates beside the existing operation/event authority. Parsing, typed-state
validation, canonical serialization, and replay comparison all use the same closed
version-1 shape; a milestone without its course identity fails closed. There is no
legacy compatibility branch or migration path. Exact replay projects one immutable
already-accepted receipt from the persisted transition, never from caller-supplied
values, while unknown operations return no receipt and any coordinate mismatch returns
`wound_history_conflicting_replay` without mutation. Current create/worsen publication
records its client-derived output fingerprint and leaves inapplicable coordinates
explicitly null for later treatment/recovery stages.

The frozen RED build failed only on the nine deliberately absent T055 types, members,
and replay fields
(`20260829-054701-274-29880-0bd78e42eed94b39a81e294920fcb2d2-focused`). Fresh GREEN
verification is warning-free: all 13 file-backed success/repair/crash-recovery/consumed
command replay contours pass their 100-repeat exact/conflict loops
(`20260829-055750-954-7200-9b10bc6b62a240ef9910f56788bbb8c8-focused`), strict history
passes 47/47 (`20260829-061327-918-35960-95174bfa943346e7a6ef43779a2de9eb-focused`),
transition/effect planning passes 316/316
(`20260829-060351-677-16156-1d0a377b573643fb9fbd934e3be65f4e-focused`), and the 30
relevant creation/worsening/owner/publication lifecycle rows pass 30/30
(`20260829-061414-906-34256-ddfe87b6e6f747a68df15a91c96c008d-focused`). The complete Fast
lane passes 6,091/6,091 in 4:31 with no timeout or duplicate IDs
(`20260829-061536-448-40368-4723f5255d9c46d6a0a3f9e6fa319407-fast`). Independent
read-only review found 0 Critical, 0 Important, and 0 Minor issues and declared T055
ready. The schema is client-owned, but GM-facing repair/replay workflow documentation
is intentionally synchronized in T056.

T056 GREEN evidence (2026-08-29): the shared GM wound contract, Block 12 rule, and
worked Block 12 example now describe the real 1-64 candidate repair wave, one-or-more
issues per packet, exact rollback-tracked before-image restoration, opaque binding
envelope versus sanitized semantic payload, all-packet atomic full-turn resubmission,
the exact `requiredResponseShape.woundDecisions[0].proposal.correctOnly` path, stale
authority rejection, and hidden-ID/seal privacy boundary. The complete worked
invalid-severity flow includes a parseable production-shaped packet with the offending
leaf removed from `preservedProposal`, followed by a full corrected wound response.
Replay guidance is deliberately qualified as the internal history resolver guarantee,
not a currently dispatched GM/player receipt, and distinguishes an unknown operation
key (`None`) from a matched-key coordinate conflict. The first documentation RED failed
on the absent workflow markers
(`20260829-063149-182-38628-cc7c87cc67d547e18dcd0db696d0fc12-focused`); strengthened
review-driven REDs then caught missing wave/path and operation-key guard text
(`20260829-064337-063-18124-73ff908b9fa34c1ebc4053885ef00329-focused`,
`20260829-064808-417-43604-a1fbe4bd65134aaf807ff44aa65acd0c-focused`). Final source,
prompt, and privacy guards pass 58/58
(`20260829-065146-299-44596-6f2c7ff9225146478dbf756a6d94b054-focused`); JSON/example
integration controls pass 3/3
(`20260829-065230-947-36232-5d8c8429622848e28d795578f65e08b9-focused`); both XML
documents parse and `git diff --check` is clean. Independent review finished at 0
Critical, 0 Important, and 0 Minor findings with Ready=yes after three remediation
passes. No afterlife matrix or example manifest update is needed: T056 changes no
runtime afterlife pending/control/action/response contract, and the Block 12 teaching
packet is parsed and guarded in place rather than registered as a production-valid
manifest scenario.

An exploratory complete wound lifecycle/validation selection also exposed eight
response/repair failures outside the T055 files (59/67 passed); a representative raw
duplicate-property exception reproduces unchanged on baseline commit `93b71676`.
T057A tracks that inherited US7 debt explicitly before the final T057 GREEN gate.

T057A GREEN evidence (2026-08-29): strict command parsing now retains its detailed
diagnostics and also reports the aggregate typed-adapter-unavailable boundary whenever
one command cannot be adapted. Raw duplicate JSON properties are rejected before DOM
projection, so ambiguous input remains fail-closed without throwing. Safe unknown
proposal leaves retain the registered `wound_response_unknown_field` code and require
omission on retry; repair authority is attached only after removing every listed
unknown leaf for that exact decision from a clone and proving the shared sanitized base
through strict parse, opportunity evaluation, and complete create/worsen composition.
Malformed known siblings therefore remain fail-closed, while two harmless unknown
leaves produce one atomic packet with two exact correction paths. Sensitive paths still
reject the entire wave in the packet builder.

The inherited 8-failure baseline is recorded in
`20260829-065429-059-33512-07c6ca6eabf8402f95589a6e32d07647-focused`.
The new multi-unknown regression first failed 0/1 as intended
(`20260829-072206-239-39924-a9e4bf04bc2f489c9ec7a924f5e95607-focused`)
and then passed 1/1 after grouped sanitization
(`20260829-072318-517-19700-293e7ff1b97045938fcb5a9ab32c9800-focused`).
Final lifecycle verification passes 62/62
(`20260829-072452-897-43828-7aba14dfadf749a299622ff1f076ec80-focused`),
including the malformed-sibling and grouped-unknown regressions. Independent final
read-only review found 0 Critical, 0 Important, and 0 Minor findings and declared the
change Ready. This restores the already documented strict repair contract and adds no
new command, response field, canonical state, pending action, or GM-authored capability;
Mortal/afterlife prompts, examples, manifests, and source guards therefore need no
additional T057A update beyond T056.

T057 GREEN evidence (2026-08-29): packet construction and recursive privacy pass 35/35
(`20260829-072718-577-23268-db102cc7b43c4d13a7a0b8dffdae2a0b-focused`),
the complete rollback/repair contour passes 40/40
(`20260829-072855-003-31940-8c56ce22558943939d160db3e5a32c26-focused`),
repair-wave cache invalidation/take-once authority passes 6/6
(`20260829-073041-603-32276-7c78bcadd4dd4e8ca8862f4b01396911-focused`),
and durable replay passes 13/13
(`20260829-073106-084-41876-3f9f62276372467f8c0c147057a89848-focused`).
The required Fast checkpoint passes all 6094/6094 tests in 4:34 with no timeout,
failure, duplicate ID, or incomplete owned-tree cleanup
(`20260829-073143-109-28696-7f0b3521b63146d58cafaa9b7ac1be86-fast`).

**Checkpoint**: Invalid or repeated wound work cannot leak authority, alter unrelated content, or leave partial state.

---

## Phase 5: User Story 2 — Diagnose and Treat a World-Specific Physical Wound (Priority: P1)

**Goal**: Materialize arbitrary setting-appropriate Mortal diagnosis, treatment, recovery, and deterioration without a ready-made wound/cure catalog.

**Independent Test**: Complete two unrelated post-apocalyptic and magical wound lifecycles using alternative routes and exact resources/providers/facilities; prove deterministic procedure/course/guaranteed outcomes and History.

### RED tests

- [X] T058 [P] [US2] Add RED complete Mortal route, required non-display impact, AND requirements/OR alternatives, closed mode/outcome, and no wound/symptom/medicine/cure-name lookup tests in `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs`
- [X] T059 [P] [US2] Add RED readable diagnosis path, bounded `requiresKnownFacts` least-fixed-point reachability, `gm_only` route rejection, known/completed-route agreement, exact staged success/failure command plus durable history/replay result and reveal boundaries, cyclic/impossible discovery, append-only evidence-backed `author_alternative_treatment`, kind-specific pending/repair response, and recursive repair privacy tests proving internal seals/operation keys stay omitted in `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs`
- [X] T060 [P] [US2] Add RED recursively strict immutable authority projections, generic self/player/NPC actor bindings, exact item/resource/skill/capability/provider/consent/facility/location/quest/effect/environment, and stale/confusable/cross-realm reference tests in `BookOfEternityClient.Tests/MortalWoundRequirementAuthorityTests.cs`
- [X] T061 [P] [US2] Add RED exact valid/invalid procedure/course/guaranteed route shapes; every procedure `success` band including natural 20 having an applicable positive state change; final-course positive/non-empty rules; closed typed outcomes; proposal `complicationRef` -> canonical `complicationId` rewrite; current GM-safe effectless/effectful complication sub-proposal reuse; closed cosmetic/mechanical-effect legacy drafts; non-public `wound_legacy` survival; structurally non-beneficial interruption; and exact/confusable refs/band IDs in `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs`. Freeze the completed three-argument T060 surface unchanged and add canonical player/NPC active/passive skill extension/provenance, extension-bearing `skillId` exact/confusable uniqueness against all current skill rows, idless/domain/severity/aggregate-limit failures, same-skill unchanged capability projection, and final-after-image publication proof tests in `BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityAuthorityTests.cs`. Add production-only accepted-state canonical lease/context/event/history/wound/skill/effect reads with no raw JSON/dice/modifier authority; normal/advantage/disadvantage reduction; restart-safe contiguous dice and Fate Shield reservations/collisions; exact typed `ResolvePreparedMortalWoundCriticalReaction` intent and legacy GM API compatibility; production coordinates/d20/world-minute and `SealProcedureRequest|SealCourseMilestoneRequest|SealGuaranteedRequest`; complete scoped requirement bundles with kind-complete typed success witnesses that independently recompute unchanged T060 row seals plus typed failure witnesses; one exact detached receipt; full command/pending/history request+bundle persistence and nested seal recomputation; restart `ProbeTreatmentAttempt` invalid-history dominance/exact detached request+receipt/no-intents; procedure numeric/category/Fate band boundaries and overflow; replay-before-fresh-state; course `Satisfied|Unsatisfied|InvalidAuthority` inclusive milestone/interruption/restart precedence; guaranteed sealed/current/final-composed proof plus sibling tier gate; ordered heal gate; immutable resolved-versus-replay payloads/consumption; derived route completion; attempt-terminal versus wound-terminal; and semantic coordinate conflict tests in `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs`
  Pending RED coverage MUST use a real non-empty `WoundRepairPacketBuilder` wave and the
  production parsed/recomposed command root. It freezes private top-level
  `submittedTreatmentRequests[]` with complete treat request and outer coordinates,
  absence outside a real repair wave, GM-packet privacy, byte-semantic command/pending
  coalescing, and fail-closed nested/outer disagreement; it MUST NOT invent a treatment
  repair candidate or a filesystem writer inside the pure composer.
  Command RED coverage MUST preserve the sole T059
  `ComposeAcceptedTransitionCommandRoot(binding, WoundTransitionRequest, text)` surface
  and freeze the distinct pure
  `ComposeMortalWoundTreatmentCommandRoot(binding, MortalWoundTreatmentResolution,
  text)` surface; the latter must parse/recompose exactly and persist only through the
  real `StateDistributor` flow. Persistence RED coverage MUST perform a true cold restart
  from copied durable bytes under a different filesystem root with fresh process-local
  registries; reconstruct the complete detached Request; pass it through the ordinary
  mode-specific T067 `Create*Attempt` reducer to reproduce the exact full Resolution,
  ordered outcome intents, and nullable critical-reaction intent; and pass that typed pair
  through the existing six-argument T070 publication. It MUST reject a post-seal production-
  foreign result-semantic replacement during command parsing or recomposition and MUST
  prove that failed persistence/rollback releases provisional
  die/Fate/resource claims, leaves no durable command/pending/history duplicate, and
  permits one exact retry without duplicate claims or intents.
  Every fresh procedure band MUST simulate as a currently legal complete transition before
  the attempt can consume its sealed die; every `success` band additionally MUST produce
  an actual monotone improvement, so an inapplicable partial/failed row cannot become a
  free reroll.
  T061 additionally freezes: exact six-argument high-level request factories and internal
  bundle factories; complete course-mode/start-wound authority, single-active-course
  lifecycle, milestone-bound bundle/resource coordinates, and current-state route
  applicability; full self-contained `RequestAuthority` inside Resolution; byte-semantic
  command/pending coalescing; exact combatant/member procedure/course targeting with
  canonical-player/NPC-only guarantee source; `woundDomain=physical`; same-turn typed-vs-
  legacy Fate duplicate rejection; one final heal, aggregate legacy/reduction limits and
  checked recovery/capability arithmetic; exact typed outcome-intent cardinality/payload/
  derived IDs/ref namespaces; two-phase legacy Prepare/Finalize plus unique heal/legacy
  child coordinates; deterministic selected-outcome and route-completion values; and
  rollback/replay without duplicate intents or claims.
  Legacy RED coverage MUST require an exact frozen ordered `EffectOperationBatches`
  array with one `WoundEffectOperationBatch` per mechanical legacy, none per cosmetic
  legacy, an ordered structural key of `localLegacyRef` plus ordered local root-
  application refs/grouping before request-derived namespaces and source seals, exact
  seed/source/ref agreement, and rejection of missing/extra/reordered/
  merged/split batches through `mortal_wound_legacy_topology_mismatch`, while a
  same-topology foreign request rejects without that topology code. Finalization MUST
  return one matching ordered fingerprinted
  `MortalWoundHealLegacyApplicationResultGroup` of complete existing
  `EffectAcceptedApplicationResult` rows per mechanical batch.
  Resolver RED coverage MUST also execute cold command-to-publication reconstruction for
  procedure, course, and guaranteed modes; procedure/course against exact combatant and
  combatant-member carriers; reusable-tool and release-only resource finalization; and
  post-heal #1535 terminal legacy-effect removal followed by cold provenance reload.
- [X] T062 [P] [US2] Add RED progressive/requires-stabilization/no-natural recovery, canonical `world_time.currentTimeInMinutes` cadence/grace authority, client-owned accepted-create recovery anchor plus separate condition/deterioration anchor, inclusive due-1/due/due+1 and grace-1/grace/grace+1 boundaries, checked elapsed/next-anchor time jumps, stabilization rebase/no catch-up, exact deterioration-policy semantics including interruption `apply_deterioration(policyRef)` strictly-worsening validation and neutral/beneficial rejection, replay-before-live-clock, overflow, closed typed transition intents, and Mortal death-lifecycle-handoff tests in `BookOfEternityClient.Tests/MortalWoundRecoveryTests.cs`. It MUST prove stabilization clears the condition anchor, accepted formal re-trauma re-enters through T064's manifest-bound source adapter plus real `StateDistributor`/`ValidationService`, the new anchor names the exact published `worsen` transition, and the recovery anchor remains rebased to the stabilization minute. The RED fixture must obtain carrier/identity/history only through T070 common accepted-plan composition and normal publication; it must never manufacture a binding/event/fingerprint or hand-write recovery receipt/history/after-images.
- [ ] T063 [US2] Add RED cross-setting create -> diagnose success/failure -> author alternative -> procedure/course/guaranteed -> recover -> heal -> History journeys and atomic resource/item use in `BookOfEternityClient.IntegrationTests/MortalWoundMaterializationLifecycleTests.cs`

T058 RED evidence (2026-08-29): 27 treatment-contract rows execute with a
warning-free build; seven existing/baseline controls pass and twenty intended
semantic assertions remain RED (`20260829-075246-452-37752-4e825b2038cb4f78b39a9137ad3f7403-focused`).
The frozen boundary covers complete procedure structure, required non-display
impact, conjunctive requirements versus alternative complete routes, closed
mode/mechanical/outcome vocabularies, exact cross-setting prose and semantic
reference preservation, and same-name wounds with independent routes. There
was no timeout or duplicate test ID. Production code is unchanged. Independent
review closed with 0 Critical, 0 Important, and 0 Minor findings; T059 owns the
next diagnosis/discoverability RED boundary.

T059 RED evidence (2026-08-29): 97 diagnosis/discovery/alternative-authoring rows
execute with a warning-free build; three baseline controls pass and 94 intended
semantic assertions remain RED
(`20260829-094250-461-28628-fdb0ef3e86e2411e93e216312771db1b-focused`).
The frozen boundary covers ordered route- and complication-fact fixed-point discovery,
success/failure terminal attempts and exact typed history replay, immutable prior
routes/paths/history, sealed visible/hidden alternative append and command
recomposition, kind-specific repair, and recursive key/value privacy. There was no
timeout or duplicate test ID. Production code is unchanged; the plan now names only
production-owned planner/history/command sealing seams so the tests contain no local
hash recipe or evidence-constructor topology. Independent final rereview closed with
0 Critical, 0 Important, and 0 Minor findings; T060 owns exact current-world reference
authority next.

T060 RED evidence (2026-08-29): 200 exact-authority rows execute with a
warning-free build; one independent requirement round-trip control passes and all 199
intended assertions remain uniformly RED on the absent production-owned T066 authority
type (`20260829-103452-285-2988-1b2622a16b424b75a27df0ef73bcfd66-focused`).
The frozen boundary covers recursively closed and externally immutable context/snapshot
projections, all eleven exact requirement kinds, generic player/NPC/self actor bindings,
ordinal/confusable/stale/cross-realm/ambiguous references, aggregate non-overbooking,
fresh ownership/availability/reservation/reachability/consent/co-presence/state checks,
detached exact result rows, and production-owned semantic fingerprints with no mutation
authority. There was no timeout or duplicate test ID. Production code is unchanged.
Independent final rereview closed with 0 Critical, 0 Important, and 0 Minor findings;
the later T061 design audit proved that transient requirement rows cannot safely grant a
guarantee. T061 therefore keeps the exact three-argument T060 parser/resolver/result
surface unchanged and owns a separate canonical player/NPC skill capability authority,
exact valid course/guaranteed route shapes, and procedure/course/guaranteed resolution.

T066 T060-core GREEN evidence (2026-08-29): the frozen three-argument parser/resolver
surface now resolves all eleven requirement kinds through strict immutable Mortal
context/snapshot authority. The review-fix RED run retained the prior 200/200 GREEN rows
and made all 35 new duplicate-property, closed-actor-kind, and exact-current-context
rows fail for their intended reasons
(`20260829-223130-969-43780-1501d13f66b44be2ae4a2af2b7cca42a-focused`).
The production correction then passed 235/235 with no warning, timeout, duplicate ID,
or incomplete cleanup
(`20260829-223405-748-27760-ae3b4c1be2414324b1ab157ac752b302-focused`).
Independent final rereview found no Critical, Important, or Minor issue. This completes
only T066's T060-compatible transient requirement core; T066 remains open for scoped
course bundles, canonical capability authority, accepted-state export, and final-
after-image proof.

T061 RED evidence (2026-08-29): the previously frozen treatment-contract matrix executes
153/153 rows with 29 controls PASS and 124 intended semantic RED failures
(`20260829-152557-988-23184-b592b07c9b3a42779666f8b91a2a57fb-focused`).
The final capability and resolver matrices execute 74 and 108 rows respectively; their
27 controls pass and all 155 intended assertions remain RED only on the absent T066-T070
production authorities. The combined capability/resolver/recovery control executed
229/229 rows with 44 PASS and 185 intended RED failures
(`20260829-213849-068-28908-99a3b95ecb894b348ea4c6bd493e24d4-focused`).
There was no timeout or duplicate test ID and the build was warning-free. Independent
final reviews of both capability and resolver matrices closed with no Critical or
Important findings.

T062 RED evidence (2026-08-29): 47 recovery, deterioration, re-trauma, replay, overflow,
and Mortal death-handoff rows execute with 17 controls PASS and 30 intended semantic RED
failures (`20260829-210623-959-44056-116d168eb1394aee9343b17e2a8be7c7-focused`).
The re-trauma fixture enters through T064's accepted-source adapter and the real
distribution/validation flow, snapshots the complete canonical tree to prove the adapter
is write-free, and obtains all lifecycle authority through the planned T070 publication
surface. There was no timeout or duplicate test ID and the build was warning-free.
Independent final rereview closed with no Critical or Important findings.

T064 scope-review evidence (2026-08-29): the first adapter pass was locally GREEN 12/12
with a warning-free build
(`20260829-214624-728-33604-931d589a92c24e9db8d6dd71922a8855-focused`),
but independent review correctly rejected closure. It proved that five declared kinds
were unreachable downstream, create was impossible, source/outcome/profile facts were
caller-trusted, only a singleton event was sealed, signed snapshot bytes/current request
were not revalidated, and decline receipts were not durable. The approved T064/T070
contract now requires one signed seven-kind occurrence authority, shared current-snapshot
reader, independently reconstructed complete event set, optional create versus explicit
worsen coordinate, and append-only decision receipt publication. The 12-row result is
retained only as superseded partial evidence and MUST NOT be used to mark T064 complete.

T064 pure-state checkpoint evidence (2026-08-30): the strict pending-occurrence and
append-only decision-receipt parsers/planners execute 48/48 rows with a warning-free
build (`20260830-011041-389-24040-5e9514878df94eb599f5adb9697f329c-focused`).
Independent rereview closed every prior Critical/Important occurrence/receipt finding
and returned Ready YES. The Fast control executed 1304 rows: 1271 PASS, the exact 30
already-recorded T062 intended RED recovery rows remained RED pending T070, and three
unrelated worker-process rows timed out under aggregate load
(`20260830-011250-782-35028-606a4d38f6784fd98c81438af1c83ddf-fast`); their focused
rerun passed 6/6 (`20260830-011942-122-30228-0272dd93f8cc44bfaa2ccd7c34a031b1-focused`).
This checkpoint freezes only T064's pure state/receipt foundation and does not mark the
adapter, snapshot, event-composer, validation, or common-publication work complete.

T064 current-snapshot reader checkpoint evidence (2026-08-30): the shared lease-bound
reader executes 16/16 rows with a warning-free build
(`20260830-020419-634-34064-a5b9278043f14252ae7863971d6c30e7-focused`). It requires
portable detached authority with exact byte hashing, rejects legacy text-hash authority
even when only BOM/encoding bytes differ, resolves one agreeing set of authoritative
lifecycle contexts, excludes diagnostic-only repair metadata, verifies requested signed
coverage and bytes, and returns detached immutable before-images without writing.
Independent rereview closed both prior Critical findings and returned Ready YES. This
checkpoint does not mark T064 complete; the typed producer reducer, complete accepted-
event projection, source-shaped ingress, independent validation reconstruction, and
the separately owned T070 publication remain outstanding.

T064 accepted-event primitive checkpoint evidence (2026-08-30): the closed typed
projection/ordinal-evidence composer executes 7/7 rows and the combined occurrence,
receipt, snapshot-reader, and event-composer selection executes 71/71 with warning-free
builds (`20260830-023233-383-25864-7c67ec73052f4c8ea70e78ebcfe48cf1-focused`,
`20260830-022908-199-32340-9472523ffc0e402f93b53c90287dc55c-focused`). It accepts no
raw JSON or supplied semantic fingerprint, derives selected seals, preserves the existing
generic production hash vector, returns the complete ordered-set fingerprint, rejects
ordinal and exact/confusable event/authority collisions, and detaches mutable input.
Independent rereview returned Ready YES with no Critical or Important findings and
explicitly reserves signed-occurrence/typed-producer provenance for the upcoming wiring.

T064 registered-producer reducer checkpoint evidence (2026-08-30): the closed seven-kind
candidate language, finalized resource/effect projection seam, harmless result, shared-
event agreement, create/worsen coordinates, guaranteed-trigger evidence, complete
source-result mutation vector, and production source guards execute 20/20 with a warning-
free build (`20260830-032414-454-29704-c5d5f3905a3d45b38b244e9fccb0ea60-focused`).
The semantic reducer and sealed-result constructor are private; the only reducer caller is
the common accepted-mechanics planner, which exposes no partial harmful batches after any
producer failure. The source guard deliberately freezes the current production implementor
list as empty: all seven kinds are the closed reducer language, not invented producer
results, and each future registration must arrive with a real finalized typed resolver.
Independent rereview returned Ready YES with no Critical or Important findings. This
checkpoint does not mark T064 complete; signed-occurrence correlation and independent
validation reconstruction remain outstanding. Concrete producer registrations are not
placeholder T064 work: each arrives only with its real finalized typed resolver, and
T070 owns the first reachable orchestration and publication call.

T064 ingress/validation checkpoint evidence (2026-08-30): the exact write-free
seven-kind source adapter, signed occurrence correlation, active receipt/pending
partition, historical event verification plus active-snapshot rebind, optional signed-
carrier worsening resolution, and cold replay are implemented. `ValidationService` now
reads occurrence and receipt roots from required signed snapshot paths, rejects changed
live bytes, validates their union, independently rebuilds and compares every command
opportunity, and uses all signed prior receipts for recomposition; it no longer derives
selected event semantics from command `inputEvidenceFingerprint` or raw effect-event
JSON. The focused adapter/occurrence/receipt/event/snapshot/source-guard contour passes
162/162 (`20260830-052902-490-37488-ab6fb730cfd240bbba90120bd085be22-focused`),
including all seven adapters, reordered sibling rejection, signed worsening lookup, and
prior-receipt projection. This checkpoint does not mark T064 complete until final
cross-contour verification and independent review are finished; T070 remains the sole
future publisher of occurrence/receipt after-images.

T064 replay/snapshot hardening checkpoint evidence (2026-08-30): consumed receipts now
retain a closed selected-event evidence projection whose semantic seal is recomputed;
mixed pending/consumed producer batches reconstruct every ordinal; persisted replay is
compared against the freshly rebuilt full opportunity, decision, conditional IDs, and
history before projection. Adapter and validation use one lease-bound byte-exact snapshot
over all wound carriers plus identity/history/occurrence/receipt roots, with recursive
duplicate-property rejection. An empty accepted wound stage remains sealed in the common
plan without replacing ordinary effect-event authority. The combined T064 and adjacent
effect-planner Focused selection passes 385/385 with a warning-free build
(`20260830-073940-020-39452-a287841111364c60ae27bc549fc32c57-focused`), and the full
file-backed wound-validation class passes 10/10
(`20260830-074040-808-27336-0f5d98e1c7bb47c699f80bee00d1f86d-focused`). The Fast
checkpoint's last-written TRX summary reported 1,338 rows, 1,270 PASS, and 68 RED
(`20260830-074142-125-12292-5c7df86f39a840249bc97f03cb276230-fast`). A later audit of
the complete parallel runner log found 99 failure lines: the expected 30 T062 and 68
T066/T070 rows plus one T064 afterlife-inventory guard for the Mortal-only occurrence
file. This checkpoint therefore remained partial even though it had no timeout,
duplicate ID, warning, or incomplete owned-process cleanup.

T064 final completion evidence (2026-08-30): review-remediation commits `66585fbf`,
`cbae6d4e`, and `39bb9465` close every final Critical, Important, and Minor finding.
Same-batch `adapterKind`, the exact signed history prefix, and the bidirectional
materialize-receipt/history-row agreement now fail closed. Dedicated player/NPC and
identity/history/occurrence/receipt roots remain byte-identical; shared combat enemy,
combat ally, and afterlife profile roots preserve the complete canonical wound
projection while their ordinary siblings may advance. The empty wound stage preserves
and publishes the ordinary effect event vector. The Mortal-only occurrence control file
is explicitly excluded from the afterlife gameplay/GM/status registry rather than being
misclassified as an afterlife contract.

The final combined unit contour passes 388/388
(`20260830-090615-246-42420-b056ca1d2ac14a1c9091d34b76ab28c7-focused`), the complete
file-backed integration class passes 17/17
(`20260830-091935-185-45140-09cdc319bd3f4b26bbe1c32974602d13-focused`), the afterlife
registry passes 6/6
(`20260830-092511-480-34192-51c5670e319b42e6b50ac7b3abe43cfb-focused`), and the
afterlife documentation source guard passes 118/118
(`20260830-092541-611-13848-d2eecbd4c4fb40ceb4c8532b38e7b5a6-focused`). The final
Fast control executes all 1,383 rows: 1,285 PASS and exactly 98 intentionally RED future
T062/T066/T070 rows, with no T064 failure, timeout, duplicate ID, warning, or incomplete
owned-process cleanup
(`20260830-092612-277-19572-2070e2e58ffc49969126397ee2072c93-fast`). Its normalized
failure signature exactly equals the prior signature after removing the fixed T064
registry guard (`delta=0`, `sha256:9cee871464450bada02aafa289bb5b644d9ec4c1c408bb5a71c676b39d8b09ee`).
Two independent final rereviews return Ready YES with no remaining Critical, Important,
or Minor findings. T070 still owns the first reachable producer orchestration and sole
publication of occurrence/receipt after-images; T064 adds no placeholder producer,
migration, player command, GM-authored output, or public afterlife contract, so no GM
prompt/example/manifest change is required beyond the explicit inventory exclusion.

### GREEN implementation

- [X] T064 [US2] Implement formal/QTE/combat/trap/check/hazard/narrative Mortal opportunity adapters and causal/profile validation in `BookOfEternityClient/Services/MortalWoundOccurrenceProducer.cs`, `BookOfEternityClient/Services/MortalWoundOpportunityAdapter.cs`, `BookOfEternityClient/Services/MortalWoundOccurrenceState.cs`, `BookOfEternityClient/Services/PendingTurnSnapshotReader.cs`, `BookOfEternityClient/Services/WoundAcceptedEventAuthorityComposer.cs`, `BookOfEternityClient/Services/MortalWoundOpportunityReceiptState.cs`, `BookOfEternityClient/Services/MortalWoundOpportunityValidationAuthority.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`. Each registered typed producer must reduce its harmful accepted result to one immutable complete occurrence-candidate batch in the same seven-kind language; harmless results yield no candidate and the later source-shaped ingress cannot manufacture one. The pure occurrence state owns a 32-row bound, exact source session/request/snapshot token/turn plus stable batch-scoped producer operation key and candidate ordinal/count, the original ordered 1-160 accepted-event authorities/set fingerprint/selected ordinal, strict Mortal owner/profile/source/outcome/hard-maximum/safe-context/create-or-worsen fields, candidate/source-result/occurrence fingerprints, exact/confusable uniqueness of `(producerOperationKey, producerCandidateOrdinal)` across pending and consumed rows with same-batch authority agreement, canonical ordinal ordering, and exact-replay-versus-changed-operation conflict. Add the exact write-free `ComposeAcceptedResponse(FileSystemManager, CanonicalWriteLease, JsonElement sourceEvent, GameResponse)` ingress: its closed source shape may select adapter/event ordinal and carry only public correlation, owner/domain/profile/source/outcome/safe-context plus an absent create or explicit worsening coordinate; hard maximum, minimum, and guarantee remain signed-occurrence-only authority, and it must reject caller session/request/snapshot binding, occurrence/event kind/ID/ref, fingerprints, transition IDs, receipts, anchors, and after-images. Add a shared current pending-snapshot reader that validates the manifest, detached reader authority, active request context, required coverage, and exact signed bytes before resolving the source-shaped correlation. Current source/combatant/hazard/die/prose presence alone MUST NOT prove a harmful occurrence. Derive owner/profile/source-state/outcome/legal locations/hard maximum only from the signed occurrence plus kind-specific canonical evidence. Build and seal the complete ordered accepted-event set from the immutable response plus occurrence, select one exact ordinal inside it, and make `ValidationService` independently reconstruct and compare that same set rather than replace it from the command. Add a strict 20,000-row append-only opportunity-decision receipt state/parser with contiguous ordinals, exact replay indexed by opportunity before operation, retained consumed-occurrence source coordinates/fingerprints, conditional null versus exact wound/transition coordinates, and full materialized history agreement; feed its exact prior receipts into initial composition, recomposition, and source-result replay. Return the ordinary `WoundResponseInputCompositionResult` and delegate command staging/admission to `StateDistributor` and `ValidationService`; both state types expose parse/serialize/fingerprint/pure after-image helpers only, and no adapter or distributor writes occurrence/receipt canonical roots directly.
  The occurrence state also preserves the existing opportunity contract's exact nullable
  `minimumSeverityRank`/pre-materialized guaranteed-trigger pair. Both are null for an
  ordinary opportunity; a complete guarantee is source/owner/domain bound, sealed into
  the candidate, and remains mandatory after cold restart. Correlation input cannot
  carry or alter it, and `none` is invalid for the resulting guaranteed opportunity.
  The pure decision helper MUST accept both current parsed occurrence and receipt before-
  states, resolve by opportunity ID, and return both after-states from one
  `PlanConsumeAndAppend` result. It MUST NOT accept a detached occurrence as proof of
  current pending membership: new acceptance moves exactly one row pending -> receipt,
  while durable-receipt-first exact replay changes neither state.
  `registered typed producer` is conditional: the seven names are the closed common
  reducer language. T064 MUST keep a missing real source resolver unregistered and MUST
  NOT add a placeholder producer; T070 wires the first real finalized source transaction
  to this reducer and remains the sole publisher of occurrence/receipt after-images.
- [X] T065 [US2] Implement strict readable diagnosis paths, typed known-fact prerequisites/reveals, least-fixed-point discovery validation, treatment routes/known/completed agreement, exact common route envelope, exact/confusable categorized gapless procedure bands with an applicable positive operation in every `success` band, contiguous canonical-minute course milestones with non-empty positive final course, closed no-improvement/effectless-positive-difficulty-complication/current-policy-reference interruption shape, guaranteed singleton source reference, canonical-versus-proposal complication selector dialects, client-owned canonical recovery/deterioration-anchor authoring rejection (proposal absent/null only), and closed ordered typed outcomes reusing the current complication/consequence sub-proposal plus proposal-safe cosmetic/mechanical-effect heal-legacy drafts in `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs` and `BookOfEternityClient/Services/WoundMaterializationContract.cs`
  T065 also owns structural positive-kind versus attempt-time applicability separation,
  positive-only non-empty ordinary course milestones, per-result at-most-one-final-heal
  and per-result aggregate legacy/severity bounds, checked numeric validation,
  selected-result complication-ref uniqueness, and closed local reference namespace rules.

T065 GREEN evidence (2026-08-30): the initial strict-contract baselines exposed 124/153
intended treatment failures
(`20260830-093538-110-23776-b46bd7e2cc864ebdaac7b30ab6205f63-focused`) and 94/97
diagnosis failures, of which only the parser contour belonged to T065
(`20260830-093655-118-7988-990b53efe05f4af69548bca11a4740d1-focused`). The completed
detached typed contract, canonical writer, fixed-point diagnosis graph, proposal/canonical
selector dialects, wound-owned complication/effect graph validation, bounded opaque
outcomes, client-owned recovery anchors, and cosmetic/mechanical legacy drafts pass all
223/223 T065-owned treatment rows
(`20260830-135231-591-43068-4f225d0a30a24c2cbf45574d6177e6b7-focused`) and all 33/33
parser-owned diagnosis rows
(`20260830-133438-649-43992-dcf87b19e8074d5e9eddee9d30418198-focused`). The neighboring
wound materialization, consequence-envelope, and transition-reducer control passes
378/378 (`20260830-133528-476-25916-191597281879462ab59291c580ad1842-focused`), while
recovery-authoring anchors plus T064 cold replay pass 2/2
(`20260830-133634-070-35580-418bcb47d74e43ba89069852c374f2a8-focused`).

Review hardening was performed RED -> GREEN for detached Mortal reaction-parameter
materialization, materialized-child envelope revalidation, exact/confusable legacy
definition/application namespaces, the 32-row opaque-outcome bound, client-bound wound
marker projection, end-to-end edge-parameter delivery, and derived owner-target plus
`wound_legacy` predicate validation. The final independent rereview returns Ready YES
with zero Critical and zero Important findings; its sole Minor plan-wording correction is
incorporated. The meaningful Fast checkpoint executes 2,568/2,568 rows in
`00:03:50.9483582`: 2,503 PASS and exactly 65 intentional T067 RED rows, all confined to
`MortalWoundDiagnosisTests` command parsing/recomposition, History, repair, and reducer
orchestration. It reports no T065 or unrelated failure, timeout, duplicate test ID,
warning, or incomplete owned-process cleanup
(`20260830-135651-447-38292-02a0bfea93d04eff8a82ba24ee962b00-fast`).

This slice is a strict authoring/validation contract only. It adds no reachable player
command, pending/control file, status projection, publication path, migration, or
compatibility reader. Full Mortal GM guidance, documentation guards, and worked examples
remain owned by T072-T074 once resolver/resource/recovery surfaces exist. Chaos Sea and
Shining Abode contracts are unchanged, so their matrix, examples, manifest, and daemon
entrypoints require no update for T065.
- [X] T066 [US2] Preserve the completed three-argument T060 surface and implement a separate complete scoped course-milestone requirement classifier with kind-complete typed success witnesses/shared internal T060 row-fingerprint recomputation and typed failure witnesses plus canonical player/NPC active/passive skill `mortalWoundTreatmentCapabilities[]` validation/normalization/export proof (linked for later adoption by #1533), including permanent exact/confusable-unique skill identity across all current active/passive rows, domain/severity/aggregate operation limits, provenance, idless/confusable/stale rejection, deterministic unchanged-shape T060 capability projection from the same skill, separate unchanged-T060 tier gates, current and final-composed publication proof exporters, and source/proof fingerprints in `BookOfEternityClient/Services/MortalWoundTreatmentAuthority.cs`, `BookOfEternityClient/Services/MortalWoundTreatmentCapabilityAuthority.cs`, `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs`, `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`, and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Skills.cs`
  T066 also owns `AcceptedTurnAuthorityRegistry.cs`,
  `MortalWoundTreatmentAcceptedStateAuthority.cs`, and
  `MortalWoundTreatmentRequirementAuthorityBundle.cs`, including the canonical lease-
  bound four-argument `MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(
  FileSystemManager, CanonicalWriteLease, MortalWoundTreatmentAuthority.Context, string woundId)`
  export, which revalidates the parsed source-shaped selection context and derives the
  live binding/events internally; combatant/member actor projection; production attempt-
  coordinate/game-time/course-mode/start-authority immutable types, first-course mode
  creation, exact `CreateForProcedure|CreateForGuaranteed` factories, shared pure bundle/
  witness writers and the `CreateForCourseMilestone` API shell, complete
  course coordinate binding, and version-1 `woundDomain=physical` enforcement. These
  prerequisite authority types MUST exist before T068 and T067 consume them.
  T066's accepted-state source adapter MUST use the canonical item identity/carrier,
  resource definition/state/history/recomposed owner agreement, exact current-location
  map/identity agreement, `NPCsInScene`, signed combat roots, canonical mastery fields,
  regular-quest `status`, and accepted effect mechanics. It also owns three closed
  version-1 registered current-location `customStates[]` kinds for arbitrary
  world-specific treatment facility, environment, and consent authority, their shared
  validator/export contract, focused GM documentation/example/source guard, and strict
  optional-snapshot case/duplicate coverage. Projection-shaped NPC/location shadow
  fields MUST grant no authority.
  The adapter MUST retain a known off-scene NPC as `reachable=false`; map player active,
  NPC active, and passive mastery only from their explicit canonical sources without an
  invented tier; keep terminal regular quests queryable by mapped status; derive item
  availability from identity/carrier quantity only, projecting only
  `player_inventory -> (player, player_current)` and `npc_inventory -> (npc, exact NPC ID)`
  while ignoring valid storage/vehicle/non-actor siblings; and filter the resource quartet to
  representable non-negative signed-32-bit Mortal actor integer rows. It MUST map
  `combat_group_member` to `combatant_member`, preserve suspended rows as inactive with
  zero availability, and ignore valid decimal/non-actor/foreign/out-of-range siblings
  rather than invalidating unrelated authority. `Context` admission MUST require exact
  version 1 plus recomputable parser-origin provenance.
  Registered treatment kinds are location-container-only: link `customStates[]` rejects
  them; identifier uniqueness excludes environment state; two environments may share one
  exact state. Existing-location authoring uses the complete replacement array in
  `worldMapUpdates.locationUpdates[]` and preserves unrelated siblings; new selected and
  remote locations use their complete ordinary creation envelopes. RED coverage MUST pin
  raw/canonical/new/update/link containers and world-map/current-location publication
  agreement.
  The documentation slice updates `OtherGuides/Wound_Materialization_Contract.md`, one
  compact worked case in a new `Examples/E_CLI_Wound_Materialization.txt`,
  `Examples/example_validation_manifest.json`, `BookOfEternityClient/game_master_daemon.ps1`,
  and `PromptDocumentationCoverageTests.Wounds.cs`. The daemon MUST copy the worked example
  and its always-read compact Mortal-location template/directive MUST name the copied wound
  guide/example whenever these scene rows are authored; context-pack copying alone is not
  sufficient guidance.
  T066 GREEN closure (2026-08-31): the accepted-state/source adapter, scoped requirement
  bundle, registered current-location treatment states, GM documentation/example guards,
  canonical skill capability contract, current/final exporters, and immutable proof
  authority are complete through `f107d1fd`. The final authority remediation independently
  rereviewed Ready with 0 Critical/Important/Minor findings. Fresh owning Focused control
  passed 296/296 with a warning-free build, no timeout/duplicates, and complete cleanup
  (`20260831-042446-783-43392-7bf8ca2438d04218b73b3fccb8b866cc-focused`). The 10
  publication rows blocked before the exporter remain explicit T067/T070 integration
  evidence rather than a T066 closure gap.
- [x] T067 [US2] Implement diagnosis/alternative-authoring orchestration and the complete
  production treatment resolver in two explicit phases around T068-A: deterministic request/attempt/
  course identities; strict response adaptation and repair; exact six-argument request
  factories; procedure dice/effect/Fate semantics with same-turn legacy-report guard;
  complete course mode/start/single-active-course/restart precedence; canonical
  guaranteed proof; full self-contained Resolution plus one receipt; typed outcome
  intents with derived complication/legacy IDs, ref namespaces, child coordinates, and
  exact declared-result agreement; command/pending/history coalescing and nested seal
  recomputation; invalid-history-first replay; deterministic selected index/route
  completion; derived attempt terminality; and operation/attempt/course/event collision
  rejection in `BookOfEternityClient/Models/GameResponse.cs`,
  `BookOfEternityClient/Configuration/FileMapping.cs`,
  `BookOfEternityClient/IO/StateDistributor.cs`,
  `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`,
  `BookOfEternityClient/Services/MortalWoundProcedureDiceReservationRegistry.cs`,
  `BookOfEternityClient/Services/MortalWoundCriticalReactionReservationRegistry.cs`,
  `BookOfEternityClient/Services/MortalWoundTreatmentReceipt.cs`,
  `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.cs`,
  `BookOfEternityClient/Services/MortalWoundTreatmentResolver.cs`,
  `BookOfEternityClient/Services/EffectAcceptedEventReportCatalog.cs`,
  `BookOfEternityClient/Services/FateShieldReactionArbiter.cs`,
  `BookOfEternityClient/Services/WoundResponseInputComposer.cs`,
  `BookOfEternityClient/Services/WoundResponseInputComposer.Parsing.cs`,
  `BookOfEternityClient/Services/WoundResponseInputComposer.CommandParsing.cs`,
  `BookOfEternityClient/Services/WoundRepairPacketBuilder.cs`,
  `BookOfEternityClient/Services/WoundTransitionReducer.cs`,
  `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs`, and
  `BookOfEternityClient/Services/WoundHistoryState.cs`
  Phase A (after T066, before T068-A/T069-A) owns only the production procedure-check authority,
  dice/Fate registries and shared arbiter/typed reaction seam, plus immutable request/
  resolution/outcome-intent type shells required by T068; it performs no request sealing
  or semantic resolution. Phase B (after T068-A/T069-A/T070-A/T069-B) owns high-level request sealing,
  persistence, resolver semantics, history/replay, course-continuation reconstruction,
  final `CreateForCourseMilestone` integration, cold restored-request reduction back to
  the exact full typed Resolution, and accepted-transition integration.
  Phase B also owns the distinct pure
  `WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(binding,
  resolution, finalSceneText)` seam; it must not overload or replace T059's sole
  `ComposeAcceptedTransitionCommandRoot(...WoundTransitionRequest...)` method.
  T067 Phase A closure (2026-08-31): complete through `a304e309`. Immutable authority
  shells, private procedure-die/Fate reservation lifecycles, the shared legacy/treatment
  Fate arbiter, typed reaction seam, and review hardening pass 37/37 owning plus 252/252
  adjacent Focused controls. Final independent rereview returned Ready YES with zero
  Critical/Important/Minor findings. The required Fast checkpoint exited non-zero only
  on downstream intentional RED boundaries: one completed TRX recorded 1,286/1,315 PASS
  with 29 failures, and a partial no-TRX shard emitted 13 more. All 42 observations were
  classified against the unchanged pre-Phase-A tests and retained earlier Fast artifact:
  13 require T067-B request sealing, one requires T069 deterioration authority, and 28
  require T070 common wound composition. There was no timeout, duplicate test ID,
  incomplete cleanup, or T067-A regression
  (`20260831-082119-955-39532-0f6db1d2d1cd49899d249d60cae2728f-fast`).
  T067 Phase B closure (2026-09-01): complete through `5e647387`. Production now seals
  the canonical detached route-source wound and complete requirement/resource/mode
  authority into procedure, course, and guaranteed requests; persists and revalidates
  command/pending/history evidence before current-state reads; reconstructs course
  continuation from globally ordered typed milestones; and reduces every fresh or cold
  restored request through the same mode-specific resolver to one complete typed
  Resolution and intent-free replay receipt. First accepted-state binding also restores
  exact dice/Fate claims once, in coalesced first-authoritative producer order, with
  roll-topology-compatible producer-reachable gaps and recovery-local historical claim
  mappings that never enter live state. The final coherent owning Focused control passed
  148/148 with a warning-free build, no timeout or duplicate IDs, and complete cleanup
  (`20260901-084738-234-31092-5e40f4d8ec0b441284e65640885850e4-focused`). Independent
  rereview returned Ready YES with zero Critical or Important findings. Two non-blocking
  Minors remain recorded in the SDD ledger: narrow the coordinator's internal dice-read
  capability seam in a future refactor, and add direct reordered-course/confusable-event
  regression rows when that history suite is next extended. The required Fast checkpoint
  completed its first descriptor 1,304/1,304 GREEN, then reached the lane's non-overridable
  five-minute limit while an incomplete no-TRX shard emitted 39 failures exclusively at
  the still-open T070-B treatment/recovery publication and durable-replay boundary. Owned
  process cleanup completed and the completed descriptor had no duplicate IDs
  (`20260901-085804-417-44772-7106623996524b37b081e9cd45db1365-fast`). T068-B resource
  finalization/restart and T070-B publication remain explicitly open. No GM prompt,
  example, manifest, Mortal guide, or afterlife matrix update is required for T067-B:
  this phase changes only client-owned internal Mortal request, resolver, persistence,
  and claim-recovery authority; T070-B owns the later player-visible accepted publication.
- [x] T068 [US2] Implement the prerequisite two-stage item/resource authority before T067 resolver integration: closed policy validation with scoped/milestone `consume_requirement` selectors; `PrepareProcedure|PrepareCourse|PrepareGuaranteed` production overloads; deterministic lease/generation-scoped reservation IDs and restart-safe command/pending claim coalescing/aggregate non-overbooking; immutable policy/claim authority sealing against complete success witnesses; exact retry/conflict/release; and post-resolution `Finalize(resolution)` that consumes only selected full-quantity claims, releases tools/non-consuming results, and integrates cancellation/validation/rollback/replay without consuming an unmet/current/future course step in `BookOfEternityClient/Services/MortalWoundTreatmentResourceComposer.cs`, `BookOfEternityClient/Services/MortalWoundTreatmentResourceReservationRegistry.cs`, and `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
  T068 MUST accept complete course-mode authority (not bare time), seal matching course
  ID/ordinal/coordinate evidence into bundle/reservation claims, and finalize solely from
  the resolution's full nested RequestAuthority without an unsealed live lookup.
  T068 Phase A, before T067-B, owns the exact three preparation overloads, immutable
  policy/claim/reservation authority, provisional generation-scoped registry, aggregate
  non-overbooking, exact retry/conflict, and opaque newly-created-only release ownership.
  It is proved directly from production T066/T067-A authority without a request shell.
  T068 Phase B, after T067-B creates sealed requests/resolutions and durable commands,
  owns `Finalize(resolution)`, confirmation/finalization, command/pending reconstruction,
  terminal cancellation/validation/rollback release, retryable publication-compensation
  distinction, and cold-restart evidence. Phase A does not
  invent a test resolution or a temporary finalization DTO to simulate Phase B.
  T068 Phase A closure (2026-08-31): complete through `be9faa2a`. The exact three
  production-only preparation overloads, immutable policy/claim/reservation authority,
  same-gate procedure die/Fate liveness, generation-scoped logical agreements,
  checked aggregate non-overbooking, exact retry/conflict, `not_required`, and opaque
  creator-only rollback are GREEN. Final resource preparation is 19/19
  (`20260831-101348-491-15528-309935302ea14e4eae7e8287c0111d62-focused`), the owning
  procedure authority remains 39/39, and adjacent accepted-state/requirement authority
  remains 238/238. Independent review found one Minor missing-null diagnostic; its
  exact 0/2 RED was fixed with typed requirement/mode-authority issues, and rereview
  returned Ready with zero remaining findings.
  T068 Phase B closure (2026-09-01): complete through `d6c02837`. The pure sealed
  `Finalize(resolution)` derives immutable full-quantity consume/release plans without
  writes; durable command/pending holds and history tombstones reconstruct atomically
  across cold restart; and reservations now follow the exact capacity-bearing
  `ProvisionalHeld -> ConfirmedHeld -> Finalized|Released` lifecycle with reason-bound
  release, stale-owner rejection, rollback ordering, current-state/capability gates, and
  idempotent terminal retries. Final bounded controls passed resource finalization 26/26
  (`20260901-123455-412-31688-28ecd13e49e64d73bec16f00f35544df-focused`), resource
  preparation 19/19 (`20260901-123855-025-44100-73a0cdc67ba64672a51f5e4a365fd5e5-focused`),
  persistence lifecycle 2/2 (`20260901-122637-846-10008-bff8f484cd704f6ca26d0c62ad964437-focused`),
  and complete cold recovery 19/19
  (`20260901-130052-777-36240-67fba474fb6d43b0972240c384aa9b34-focused`). Independent
  whole-range rereview returned spec PASS and code-quality PASS with no findings and
  Ready YES. The retained Fast artifact was not repeated: its completed descriptor was
  GREEN and its unfinished failures are the still-open T070-B publication/replay
  boundary, so another bounded Fast run would add no T068 evidence. No GM prompt,
  documentation, example, manifest, browser/console, or afterlife update is required:
  T068 is internal client-owned Mortal authority. Positive canonical publication and
  production commit invocation remain explicitly assigned to T070-B; no migration or
  premature publisher was added.
- [ ] T069 [US2] Implement canonical-world-minute recovery/deterioration policies, independent recovery versus condition/deterioration anchors, cadence/blockers/overflow/elapsed-next-anchor semantics, exact strictly-worsening policy classification for treatment interruption references, and exact closed typed tick outcomes in `BookOfEternityClient/Services/MortalWoundRecoveryPlanner.cs`. Stabilization clears the satisfied condition anchor while rebasing only recovery; a later accepted condition re-entry allocates a fresh deterioration anchor from that exact transition and minute without reallocating recovery. `MortalWoundDeteriorationPolicyAuthority` must offer the resolver-bound `Create(acceptedState, coordinates, policyRef)` overload as well as the canonical recovery-planner factory; neither accepts injected JSON/fingerprint/authority.
  T069 Phase A owns both closed deterioration-policy authority factories and exact
  strict-worsening classification. After T070-A has created canonical anchor state only
  through the accepted initial-wound path, T069 Phase B owns non-replay clock arithmetic,
  dispositions, and typed intents. T069 Phase C, after T070-B publishes the durable
  recovery receipt/history evidence, closes invalid-history-first and exact replay.
  Neither scheduler tests nor replay tests may hand-write an anchor, binding, receipt,
  history row, or after-image.
  T069 Phase A closure (2026-08-31): complete through `21696e44`. Both production-only
  deterioration-policy factories now derive one registry-current policy from the exact
  canonical wound or accepted treatment coordinates, classify only `increase_severity`,
  applicable `add_complication`, and `death_contour` as strictly worsening, and bind the
  complete policy plus exact wound occurrence path into immutable authority. Closed parsing
  fails without exceptions for malformed drafts or owner kinds; combined complication,
  slot, definition, and five-root boundaries are independently proved. The final owning
  structural/authority/GM source-guard Focused run is 32/32
  (`20260831-121844-716-47756-a4ea2f57c13a47eeba8ec7a372fbee24-focused`), warning-free,
  without timeout or duplicate IDs, and with complete cleanup. Independent final rereview
  reports zero Critical/Important/Minor findings and Ready YES. The required Fast checkpoint
  executed 1,333 tests with 1,305 PASS and 28 failures, all at the unchanged future T070-A
  common wound-composer overload before T069 scheduling executes
  (`20260831-122220-823-1644-c54f3bfbacca430cb71cd6f753bf511b-fast`); its one load-sensitive
  web fencing timeout independently passed 1/1
  (`20260831-122651-130-40164-c971ccaa748f4763840c321210badb83-focused`). No afterlife
  matrix/example/manifest update is required because this phase changes only the Mortal
  physical-wound policy contract and no afterlife runtime surface. Phases B and C remain
  explicitly open for canonical anchors, scheduling/intents, publication, and durable replay.
  T069 Phase B closure (2026-08-31): complete through `3c0b3b26`. The exact sole
  four-argument `MortalWoundRecoveryPlanner.Plan(fs, lease, binding, woundId)` now enters
  only through a private registry capability and the registry-current T066 accepted-state
  export; stale bindings, mismatched wound IDs, detached leases, and caller-supplied
  clocks/plans/ticks/policies cannot reach planning. Canonical world-minute arithmetic
  covers due/grace boundaries, checked elapsed jumps, next anchors strictly after the
  current minute, blocked/no-natural modes, simultaneous independent 10/7 recovery and
  deterioration cadences, and death as a typed lifecycle handoff. Strict-worsening policy
  authority is revalidated even while its condition is inactive. Planning is deterministic,
  registers no common plan, and leaves every governed root and the complete canonical tree
  byte-unchanged. The final owning policy/planner Focused control passes 25/25 with a
  warning-free build, no timeout or duplicate IDs, and complete cleanup
  (`20260831-192558-557-18844-a2e00bcd1b534598a5089740f76f9d4e-focused`). A complete
  recovery-class diagnostic executed 67 tests with 44 PASS and exactly 23 failures at the
  still-absent T067-B/T070-B stabilization, recovery-publication, and replay seams
  (`20260831-185756-527-43648-34d00da3c5634739b6403990ccaab10b-focused`). The required
  Fast checkpoint's first descriptor passed 1,304/1,304; the lane then hit its hard
  five-minute limit while the second process emitted only the already-owned 31 treatment-
  publication and 23 recovery-publication/replay REDs. It had complete owned-tree cleanup
  and no duplicate completed IDs
  (`20260831-190213-627-46268-ca6ce266b1114048999e29e32c68c01b-fast`); repeating the same
  bounded lane cannot add headroom because Fast currently rejects timeout overrides.
  Independent review found zero Critical/Important issues and one Minor missing test guard
  against a second `Plan` overload; the guard was added and review declared Ready YES.
  No GM prompt, example, manifest, Mortal guide, or afterlife matrix update is required:
  this phase adds only the client-owned evaluator for the already documented Mortal
  recovery policy/anchor contract and changes no GM-authored field, command, response,
  pending/control surface, or afterlife runtime contract. T069 Phase C remains open until
  T070-B publishes durable receipt/history evidence for invalid-history-first exact replay.
- [ ] T070 [US2] Compose Mortal accepted-occurrence creation, opportunity-decision receipt/pending-occurrence consumption, plus diagnosis/alternative-route/treatment/recovery wound/effect/item/resource/history/output after-images; re-export guaranteed authority from the final composed player/NPC skill after-image whenever its root is touched; revalidate and atomically consume/expire the already T067-resolved exact per-attempt Fate Shield intent or roll back; and prepare deterministic cosmetic IDs plus non-GM `wound_legacy` source exports/history through `MortalWoundHealLegacyPlanner` before publication. On a source-result transaction, accept only T064's complete harmful typed candidate batch, read both occurrence and receipt roots as signed before-images, combine pending and consumed replay by producer operation key plus batch ordinal/count, reject changed or incomplete batches, derive canonical occurrence/public identities inside the common plan, append only genuinely new candidates atomically in ordinal order, and expose opportunities only after the next active pending-turn snapshot has sealed exact bytes. On the later wound-decision transaction, extend the common signed-snapshot/before-image inventory with both occurrence and receipt roots. Every accepted `none` or `materialize` decision appends exactly one sealed receipt carrying the consumed occurrence's source replay authority and consumes exactly one occurrence; a decline publishes null wound/transition coordinates without inventing a wound-history row, while a materialized receipt carries exact wound/transition IDs and matches exactly one ordinary `create|worsen` history row on transition/wound/turn/event/operation/source authority. Exact cold replay emits no command/transition, changed decision conflicts, and failed publication/rollback leaves neither receipt nor consumed occurrence. Register the closed legacy source, feed its ordered per-mechanical-legacy typed batches through #1535 while leaving effect IDs solely to that planner, exclude it from active-wound cleanup, and preserve source/history after later effect removal in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/MortalWoundHealLegacyPlanner.cs`, `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs`, `BookOfEternityClient/Services/EffectSourceAuthority.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`, and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Wounds.cs`
  T070 MUST consume the exact T067 outcome-intent/DeclaredResult pair without raw-route
  reparsing or identity allocation; update the existing non-heal/follow-up-heal reducer
  gates for the sealed <=2 / staged-to-I contour; call
  `MortalWoundHealLegacyPlanner.Prepare(binding,resolution,workingWound)` before #1535 and
  `Finalize(preparation,acceptedEffectPlan)` afterward; emit durable legacy rows only from
  finalization; and validate all derived heal/legacy child operation/event coordinates.
  T070 additionally feeds every initial or stabilization sealed existing `WoundAcceptedTurnPlanner`
  `Prepare -> #1535 effect batch -> Finalize` bundle into the future common-authority overload
  `AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(fs, lease, bundle)`; it does
  not add an alternate create authority. Stabilization reads its canonical before-state
  and must produce a second complete sealed stage bundle; it cannot publish a generic
  reducer result or caller-authored after-image. It must be the selected sealed T067
  treatment request/resolution consumed by the existing six-argument
  `WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(...)` path; recovery
  reacquires its binding through T066 `ExportCurrent(fs, lease, context, woundId)`. That
  authority builds the full common input and
  after-images from the sealed bundle plus canonical roots and registers the ordinary
  `AcceptedMechanicsPlan`.
  `MortalWoundRecoveryAcceptedPlanComposer.Compose(...)` follows the same delegation
  for a typed recovery resolution. The common
  `CanonicalStateNormalizer` accepted-plan path is the sole publisher of the composed
  wound carrier, identity, history, receipt, and recovery anchors.
  Diagnosis/alternative continuation checkpoint (2026-09-06): the remaining 65
  `MortalWoundDiagnosisTests` failures are required unfinished T059/T070 behavior,
  not obsolete `accepted_transition` fixtures. The next bounded implementation is
  `docs/superpowers/plans/2026-09-06-t070-diagnosis-history.md`: closed immutable
  result/history foundation, followed by sealed factories/reducers, accepted commands,
  kind-specific GM authoring/repair, and real common publication/replay/rollback.
  T070 remains open; fixing only the unit baseline does not complete its capability.
  History foundation checkpoint: `16e08fca`, independent spec/quality review clean
  (0 Critical/Important/Minor). Final pure history/result/immutable-plan control is
  125/125 (`20260906-082612-854-26752-2a8526c1bd2d4d82a2e0c5a787bcd808-focused`);
  Integration persistence/duplicate-course control is 19/19
  (`20260906-082806-775-29640-9145f26551364a1f8f572a21e3a039af-focused`). The broader
  44/48 history run is not waived: its one newly obsolete diagnosis-result fixture
  was repaired, while the two unsupported course-publication cases and the missing
  treatment lineage-root replay case were independently reproduced on immutable
  BASE `1ca6e396` (0/3,
  `20260906-083805-938-19972-18a8564ed4ed4950b3449bdd5dcea689-focused`). These remain
  T070 publication work. Next bounded task:
  `docs/superpowers/plans/2026-09-06-t070-diagnosis-reducer.md`.
  Diagnosis factory/reducer checkpoint: `9859a0ab`, independently Approved with
  0 Critical/Important/Minor. Final owning selection is 299/299
  (`20260906-090901-961-29352-10b542de73d44338ace839e715d5b747-focused`), clean build,
  no timeout/duplicates/skips, complete cleanup. Both outcomes now have sealed exact
  path/result/attempt authority and durable nonterminal wound history without healing
  or undeclared changes. This is not a command/publication capability. Next pure slice:
  `docs/superpowers/plans/2026-09-06-t070-alternative-treatment-reducer.md`; first repair
  the two existing real replay tests' missing canonical wound-effect fixture seed,
  preserving all exact/conflicting/invalid-history assertions.
  Replay fixture correction checkpoint: `41583c0e..53850eda`, independently Approved
  with 0 Critical/Important/Minor. Both replay fixtures now use canonical effect roots,
  persisted-command resource confirmation, exact rehydration, and the coordinated
  publisher. Unchanged exact/conflict/invalid-history assertions and the comparison
  control pass 5/5 (`20260906-093335-089-30644-dfd6fcdc08cb457c9a1c0dcb9c1beb22-focused`),
  clean build and cleanup, no timeout/duplicates/skips. Parent inspected the actual
  summary/log and complete diff. The two baseline course-publication failures remain
  required T070 work; they are not covered or waived by this replay correction.
  Next: the sealed alternative-treatment reducer plan above, then closed standalone
  member validation and commands; fresh publication and GM authoring remain mandatory.
  Alternative factory/reducer checkpoint: `b45c5d2e`, independent spec/quality Approved
  with 0 Critical/Important/Minor. Final owning control is 484/484
  (`20260906-095307-279-16128-3dc013d240e742d39138a7d77dde5048-focused`), clean build,
  cleanup and no timeout/duplicates/skips. The full selected route/path/result and local
  images are sealed; only one append plus history is legal, never a treatment attempt.
  A new RED fixture exposed ambiguous diagnosis-path IDs; the approved narrow parser
  fix rejects case/Unicode-confusable duplicates. The same alternative plan's Task 2
  now owns its required GM guide/example/source-guard synchronization and is still open.
  Then execute `docs/superpowers/plans/2026-09-06-t070-treatment-member-shapes.md`:
  shared detached local envelope/slot checks followed by complete standalone route/path
  parsing. These must not invent missing severity/owner/policy or weaken full-wound
  checks. Accepted commands, GM authoring/repair and fresh publication remain later
  required contours; neither T070 nor T177 nor #1536 is complete.
  Diagnosis identity GM synchronization checkpoint: `a6ff984c`, independent
  spec/quality Approved, 0 Critical/Important/Minor. Guide, exact worked Mortal
  constructor and source guard are synchronized; owning documentation 3/3
  (`20260906-100529-701-16084-ebab65abdfae43298564bbf91d577f8f-focused`) and complete
  constructor/manifest 3/3 (`20260906-100547-999-28716-5ac981642eab490fbad70aa85e65aad6-focused`)
  pass with clean build/cleanup and no timeout/duplicates/skips. The alternative
  reducer plan's Tasks 1/2 are complete. Proceed to standalone member-shapes Task 1;
  afterlife schema/matrix/prompt routing are unchanged and do not need updates here.
  Detached envelope/slot prerequisite checkpoint: `ea7a9429..6b8603f2`, independent
  spec/quality Approved after fixing I1; zero open Critical/Important/Minor findings.
  Shared shape/full traversal now propagates existing oversized-expansion input faults;
  shape per-root budget also counts null-summary roots. Final exact owning control is
  289/289 (`20260906-103509-932-29420-e2db7b533fda46fda368d34abb99c3d4-focused`),
  clean build/cleanup, no timeout/duplicates/skips; parent inspected all run summaries,
  final build/output, complete diff and reviews. Full complication callers require
  non-null slot summaries and legacy graphs remain separate. Next is the narrow
  `docs/superpowers/plans/2026-09-06-t070-diagnosis-result-cardinality.md` regression
  alignment, then standalone member-shapes Task 2. No new GM/afterlife surface here;
  command/response/repair/fresh-publication work and T070/T177/#1536 remain open.
  Diagnosis cardinality alignment checkpoint: `7c4dc6c8`, independent spec/quality
  Approved, zero Critical/Important/Minor findings. Actual 2/3 RED proved that an
  empty success could emit an unpersistable history intent. It now rejects at the
  existing path/fact gate; empty failure and already-known-fact success both append
  and replay exactly. GREEN 198/198 (`20260906-104345-067-7584-e1ea35ecdea042a58e2a497c5ef4ec47-focused`)
  plus supplementary full owning class 73/73
  (`20260906-104759-343-30468-4d5a079a0bd34e8d9a40181b01f6791d-focused`) cover the
  requested controls; the initial narrower-filter deviation is recorded, not hidden.
  Parent inspected all three actual summaries, build/test evidence, diff and review;
  clean builds/cleanup, no timeout/duplicates/skips. No schema or GM/afterlife surface
  changed. Continue member-shapes Task 2, with T070/T177/#1536 still open.
  After member-shapes Task 2 review, execute
  `docs/superpowers/plans/2026-09-06-t070-accepted-diagnosis-alternative-commands.md`:
  one closed typed codec with shared complete-member/wire seals, unchanged legacy
  command families, and explicit unsupported fresh-adapter gates at distribution,
  validation, persisted catalog, pending capture, cold replay and repair retry ingress.
  The 20 frozen command rows plus new pure and exact Integration controls are required;
  this does not cover the three remaining GM response/repair rows or fresh publication.
  Next follow `docs/superpowers/plans/2026-09-06-t070-alternative-treatment-response-repair.md`:
  first strict immutable GM response drafts, pre-write raw-input gate and worked examples;
  then kind-specific safe correction/public persisted transport. Task 2 now has its
  executable detail in `docs/superpowers/plans/2026-09-06-t070-alternative-repair-projection.md`.
  Existing opportunity capture
  cannot stand in for transient rejected-authoring authority, and its hardcoded public
  resubmission route must not contradict the new packet. Full live retry/publication
  remains a subsequent mandatory T070 contour, not a public-checksum shortcut.
  Standalone-member baseline audit (2026-09-06): exact BASE `17dc4bb3` reproduces
  all three expanded-control failures (0/3,
  `20260906-111802-156-29536-fd698b82801d45c5b118fa3ae4b7a4b6-focused`). The two
  recovery-anchor fixtures must assert canonical null fields and absent/null proposal
  equivalence, retaining non-null GM-anchor rejection. The unchanged
  `WoundLegacySource_SurvivesWithoutActiveWoundButIsNeverPubliclyMaterializable`
  is genuinely unfinished T070 heal-legacy source registration/publication, currently
  `effect_source_authority_invalid_export`; it is not a shape-extraction regression.
  Keep it in the full selection as explicit RED until the actual closed derived-source
  planner is implemented. No skip, negative filter, permissive export or feature waiver.
  This prevents the required post-healing consequences from being lost at the codec
  checkpoint; neither its remaining failure nor the two course-publication REDs may
  survive final T070/feature/Fast acceptance.
  Standalone-member checkpoint: `17dc4bb3..63ece06f`, independent spec/quality
  Compliant/Approved, 0 Critical/Important. The exact combined selection remains
  744/745, exit 1 (`20260906-112755-916-9968-d8a71149663e433f8288f75b6fe6b2f9-focused`),
  solely the above unimplemented legacy-source row; standalone tests156/156 and
  corrected anchor rows4/4 pass. Parent inspected all ten actual summaries, baseline/
  final output, complete code/test diff and review; clean builds/cleanup, no timeout/
  duplicates/skips. Retain review Minor M1 for final-review triage: exact diagnostic
  code/path assertions in negative member tests (policy, owner, check, prerequisites).
  Shared full-context rules and canonical member writers are now the codec dependency;
  no fake wound/rank or gameplay authority is introduced. T070/T177/#1536 remain open.
  Accepted-command checkpoint (2026-09-06): `44a1c761..ddd38243`, independent
  Compliant/Approved, zero Critical/Important/Minor findings. Final pure253/253
  (`20260906-122041-379-27568-ae9d52294ecf44a0acc2da69759d835a-focused`, 1:03.639)
  and original combined Integration plus signed positive29/29
  (`20260906-122218-860-28956-09987906525d48e695c89762b9255f7a-focused`,57.323sec).
  Parent inspected the complete17-file change, all16 actual summaries/TRX counters,
  baseline/behavioral RED evidence, final build output and independent review. No
  timeout/duplicates/skips, complete cleanup and clean builds. Three required old
  fixtures were corrected narrowly after identical immutable BASE reproduction;
  signed event/owner negatives now reach their intended gates and preserve bytes,
  and a separate genuine signed positive prepares both plans. No original row was
  excluded. All20 frozen command rows now pass; three response/repair rows remain.
  Cold-replay/repair gates have inspected source coverage only at this checkpoint;
  owning executable lifecycle evidence is mandatory with the live adapters. The
  current codec is internal-only, so no GM/afterlife surface changed. Next local
  response work uses the distinct typed GM route model and explicit selector dialect
  specified in `docs/superpowers/plans/2026-09-06-t070-gm-treatment-draft-model.md`;
  all-kind public packet tags follow the stronger repair contract, not a compatibility
  exception for old construction packets. Fresh diagnosis check/cost and alternative
  private evidence/request/decline/discovery authority remain explicitly unfinished.
  No remote publication or issue closure; T070/T177/#1536 remain open.
  GM response checkpoint (2026-09-06): `da6f5c64..6b94bdac`, independent Spec
  compliant / Quality approved, all review findings resolved. Parent inspected the
  complete 24-file change, all16 actual summary/TRX/build artifacts and the complete
  reports/reviews. Final exact pure334/334
  (`20260906-134356-608-368-02cc27341f5f46768a7adc50ad2b606f-focused`) and
  Integration16/16 (`20260906-134420-991-10968-fcb0b50cfaba4dbbb4dfc901c5a61db2-focused`)
  pass; the final test-only punctuation correction passes its covering class43/43
  (`20260906-141113-452-13180-aa62da694a69410b9670d975f0c67f94-focused`). No warning,
  skip, duplicate, timeout or cleanup failure in these final controls. One original
  response RED is implemented; both frozen repair rows remain Task 2. Mortal GM
  guide/field documentation, three worked examples, manifest and production/source
  guards are synchronized; no afterlife surface changed in this local response task.
  Explicit raw-ingress rejection remains until actual fresh authority is wired.
  Task 2 must fix shared diagnostic coordinate compaction before deriving edit masks,
  preserve valid siblings through staged local corrections, and perform the all-kind
  tag cutover together with strict consumers and afterlife documentation controls.
  Closed heal-legacy source and course-publication REDs remain required; no T070,
  T177 or feature closure, merge or remote action is authorized by this checkpoint.
  Kind-specific repair checkpoint (2026-09-06): `af5e94a3..0baceaf5`, independently
  Spec compliant / Quality approved, including resolved M1 selector-test isolation.
  Parent inspected the complete change and all20 actual summary/TRX/build artifacts;
  all final filter terms select real tests. Production-tree pure456/456
  (`20260906-150744-445-19932-4454f01864cd4945a5407b4decf49fca-focused`),
  Integration60/60 (`20260906-145000-501-13720-1b253bb9f8c64ce99951abec15f7c240-focused`),
  afterlife120/120 (`20260906-145752-614-15748-8b6422b7ca164f35b30d1b1f09dba1cf-focused`),
  and FullValidation1855/1855 in 7:54.916
  (`20260906-145831-835-28680-28ac9cf503cb4714ad7e709c82d0baae-fullvalidation`).
  FullValidation predates the final route-root guard, separately covered by its
  actual RED/final pure control. Final test-only M1 correction class74/74
  (`20260906-152931-810-8704-f967723acf19470387a390673d7a391f-focused`). No final
  build warning/error, skipped row, duplicate, timeout or cleanup failure. The two
  original repair rows are unchanged and implemented; all four candidate kinds have
  strict current-format tags, and GM/runtime/afterlife guidance plus one complete
  production-validated three-part correction example are synchronized. Public/local
  correction remains separate from private and fresh accepted-world authority;
  alternative-only/mixed live waves remain explicitly closed. The subsequent scalar
  course plan is `docs/superpowers/plans/2026-09-06-t070-scalar-course-publication.md`.
  Scalar-course checkpoint (2026-09-06): `a393a4a0..3c0c77d1`, independently Spec
  compliant / Quality approved after I1/I2/M1 and the scoped T177 create-fixture repair.
  Parent inspected the complete production/test/GM change and all28 actual summary,
  TRX and build artifacts. Exact scalar milestones/interruption, retained non-course
  pointer, real current-dose settlement, history/receipt coordinates, cold replay,
  rollback and live/cold milestone exclusivity are implemented. Both frozen
  stale-history bodies and all four pure projection method bodies remain unchanged.
  Forty-five existing authority-dependent rows now use the genuine Integration seam,
  plus one new missing-authority rejection; five pure projection rows remain Fast.
  Final corrective pure42/42
  (`20260906-172056-979-18952-5de35c5de53349249864eae236ff3092-focused`) and
  Integration46/46 (`20260906-172436-375-24860-e6baee72f87846ddac48ffa174024b5d-focused`)
  pass with clean builds, no skip, duplicate descriptor, timeout or cleanup failure.
  Earlier complete owner evidence and its final narrow corrections are retained in
  the task report, not presented as an unperformed broad rerun. The meaningful Fast
  control (`20260906-165017-397-13632-74b2afc6f55a4ca5b032531116032dd0-fast`)
  stopped at2655/2656; its create-fixture failure is focused-GREEN now, but full Fast
  is not yet certified. The mandatory heal-legacy RED remains genuine unfinished work.
  Mortal guide, complete scalar course example inside the content wrapper, manifest
  and source/application guards are synchronized; no afterlife contract changed.
  Next bounded plan: `docs/superpowers/plans/2026-09-06-t070-treatment-recovery-publication.md`.
  T070/T177/#1536 remain open; no remote publication authorized.
  Recovery-publication checkpoint (2026-09-06): `48911240..189ba934`, independently
  Spec compliant / Quality approved and parent-accepted. Ordered checked recovery
  accumulation now preserves threshold/severity/anchors/effect history unless another
  explicitly selected operation changes them. Eight new owning methods/twelve rows
  cover actual publication, categories/capability, course/restart, overflow, detached
  payload mutations and post-write rollback. Parent inspected the complete nine-file
  diff and all19 actual summary/TRX/build sets. Final Focused pure24/24
  (`20260906-182431-529-13304-4991a3a8db944f98a523f697a0bb2e85-focused`),
  recovery Integration12/12 in5:40.061
  (`20260906-183958-416-468-5a5dac3995d0462c9cda0c0f7783ccea-focused`),
  and retained Integration62/62 in5:43.252
  (`20260906-184547-366-31900-04f419227f444160ab1b57e3648ab257-focused`) pass with
  clean builds/cleanup and no skipped/duplicate rows or timeout. The split preserves
  the original74-row union; the preceding ten-minute combined run remains incomplete.
  No full Fast success is implied. Review Minor M1, a redundant capability assertion,
  is retained for final whole-branch triage; no Critical/Important finding remains.
  Mortal guide/example/manifest/guards are synchronized; no afterlife contract changed.
  Next bounded plan: `docs/superpowers/plans/2026-09-06-t070-complication-removal-publication.md`.
  Natural recovery, terminal heal/legacies, other outcomes, fresh diagnosis/alternative
  adapters and T070/T177/#1536 closure remain required and open.
  Selective-removal checkpoint (2026-09-06): `51c97c24..58f29a5c`, independently
  Spec compliant / Task quality Approved and parent-accepted. Ordered selective
  teardown, exact retained0/0 lineage and mixed reduction now publish atomically;
  shared terminal generation retains the exact old canonical parent without old
  identity revival or terminal-history changes. Parent inspected all22 changed files
  and all24 C# artifact sets. Required Integration24/24
  (`20260906-204104-611-37200-03627fd7d31b49a4bdad56f499ed8380-focused`) plus52/52
  (`20260906-204805-284-36716-2599cef44a014f32940d34fd60bb03e6-focused`) is exactly
  the original76-row discovery union, with no missing/extra/overlapping descriptors.
  Relevant pure342 unchanged/shared rows plus full121 afterlife documentation rows
  pass; builds/cleanup are clean. Earlier bounded timeouts remain explicitly incomplete.
  Single Fast4110/4211 and FullValidation299/300 stop at two parent-confirmed baseline
  harness defects: valid snapshot lacks required skillId; named level3 JSON example
  incorrectly includes the next level2 scalar-course section. No full lane success
  or healed-legacy implementation is implied. Next task is the same-branch test-only
  `docs/superpowers/plans/2026-09-06-t177-verification-fixture-alignment.md` before
  direct-addition implementation. Minor M1 (unchanged-severity no-batch diagnostic)
  is retained for that future finalizer change/final branch triage. Mortal guide,
  complete example/manifest/guards and shared afterlife lifecycle guidance are synced.
  T070/T177/#1536 remain open; no remote mutation or issue closure occurred.
  T177 fixture checkpoint (2026-09-06): `b4f177c9..c7301340`, independently Spec
  compliant / Task quality Approved and parent-accepted after actual two-file diff,
  review and four artifact-set audit. Focused example1/1 and FullValidation1856/1856
  pass; clean builds/cleanup, no timeout/duplicate IDs. Focused requirement175/235
  and incomplete Fast4168/4211 retain60/43 exact-member failures: unchanged tests
  omit production SkillId. All failed rows and unchanged production source are
  parent-confirmed; no full Fast or legacy completion is implied. Task2 in
  `docs/superpowers/plans/2026-09-06-t177-verification-fixture-alignment.md` aligns
  the strict test view and exact skill identity before addition. Test-only scope
  changes no Mortal/afterlife authoring contract; no GM update or FV repeat needed.
  T070/T177/#1536 remain open.
  T177 test-view checkpoint (2026-09-06): `580f682e..9f111e7b`, independently Spec
  compliant / Task quality Approved, no findings, parent-accepted after actual diff,
  review and two artifact-set audit. All236 requirement rows now pass with exact
  nullable SkillId and capability/skill fingerprint assertions; both118-row Fast
  shards are also green. Fast5627/5628 remains incomplete on an unchanged console
  input registration timeout at1second. Parent single-owner diagnostic passes1/1
  in41ms with clean build/cleanup; no fix/full-Fast claim. Preserve load-sensitive
  console timing suspicion for final T177 triage. No production/GM contract changed.
  Both fixture tasks are accepted; next Task1 of
  `docs/superpowers/plans/2026-09-06-t070-complication-addition-publication.md` owns
  cumulative symbolic-graph/pre-roll admission before selected direct-add publication.
  Full policy publication, heal/legacy and T070/T177/#1536 remain open.
  Addition Task1 review checkpoint (2026-09-06): implementation `f3c3c0c..8057a262`
  is NOT accepted. Parent inspected its actual diff and all25 reported artifact
  sets: final pure127/127 and converter/GM60/60; earlier Integration18/18,26/26,
  destination3/3, afterlife121/121 and FullValidation1856/1856. The single Fast
  is incomplete5878/5879, stopped by the required unfinished legacy RED. Broad
  controls precede only the separately approved final malformed-creation diagnostic
  correction and are not full-feature completion evidence. Independent Spec+Quality
  review found I1: graph admission omits canonical retained treatment/policy checks
  at the resulting rank and diagnosis checks against remaining accepted complications.
  Execute `docs/superpowers/plans/2026-09-06-t070-retained-state-parity.md` using
  shared existing canonical owners, real pure/pre-roll RED-to-GREEN evidence and
  fresh semantic-change controls. Do not begin selected-addition Task2 or check
  Task1 complete before parent artifact audit and independent correction approval.
  This is validation parity restoration, not permission to rewrite retained catalogs,
  invent canonical IDs, weaken the authoring language or narrow unfinished outcomes.
  Addition Task1 parent acceptance (2026-09-07): `f3c3c0c..8057a262` plus correction
  `8057a262..ff5ddcb8` accepted after complete source/report review, all25 original
  and all10 correction artifact audits, and independent correction Spec compliant /
  quality Approved with I1 closed/no findings. Fresh owner463/464 retains only the
  required legacy RED; graph admission Integration20/20 and afterlife121/121 pass.
  Fresh FullValidation1856/1856 passes in9:04.8574487 with clean build/cleanup,
  no timeout, duplicates or skips. ONE Fast6006/6007 is incomplete due to that same
  legacy RED, not full-project success. New pure7/7 and real pre-roll2/2 prove the
  shared canonical retained-rank and remaining-diagnosis gates before die/resources.
  Original canonical writer exception and retained route SourcePath are preserved.
  Only bounded Task1/correction checklists are checked; top-level T070/T177/#1536
  remain open. Next is concrete Task2 in the addition plan: selected direct-add
  binding/single-batch publication, including existing legal mixed partial_success,
  exact new-versus-retained skill authority, one-pass scalar state and atomic retry.
  No spiritual healing art or full legacy/policy publication completion is implied.
  Addition Task2 source-confirmed addendum (2026-09-07):
  `docs/superpowers/plans/2026-09-07-t070-selected-add-reducer-continuity.md`
  defines the narrow Mortal/physical treatment mixed-root delta and the shared
  existing original/final same-rank source continuity for pre-roll admission.
  No per-operation semantic-key retirement ban: a later legal severity reduction
  retains its existing authority. Tagged ownership must distinguish a complication
  named `base_wound` from actual base roots. Real add/reduce and reduce/add rows
  pass, but same-rank publication, final regression and independent acceptance
  remain in progress. No additional top-level task is marked complete.
  Addition Task2 parent acceptance (2026-09-07): `109d4f92..9f9f6c86` including
  the reducer/continuity addendum is accepted after complete source/artifact audit
  and fresh independent Spec Compliance / Code Quality Approved, no findings.
  All26 new selected-addition rows have latest GREEN evidence. Pure239/239,
  applicability23 plus final-stamp2, removal24, severity/binding39, recovery8 and
  afterlife documentation121 pass; ordered binding2 and fresh-skill2 were separately
  reverified after final changes. ONE Fast6015/6016 retains the mandatory legacy
  RED and1287 discovered rows without completed results, not full-Fast success.
  ONE FullValidation1856/1856 passes in9:40.0472005,11 completed TRXs, clean build/
  cleanup and no timeout/skips/cross-TRX duplicates. Exact artifacts are recorded
  in the addition plan's acceptance. Its ten Task2 steps and eight addendum checks
  are complete; no top-level task is newly closed. Mortal worked example/guide/
  manifest/guards are synchronized; no new afterlife-authored surface. Next is
  actual selected non-death deterioration-policy publication through the same
  complete graph. Death handoff and heal/legacy remain explicit independent
  implementation work, not deleted mechanics. T070/T177/#1536 remain open.
  Selected-policy preparation plan (2026-09-07):
  `docs/superpowers/plans/2026-09-07-t070-selected-policy-preparation.md` defines
  the next independently tested T067-owned private policy/body/binding handoff
  before non-death policy publication. It preserves both T069 factories, the six
  public policy-intent fields and existing fingerprints. Explicit death is captured
  as typed evidence only, not published or conflated with healing. Current selected
  publisher rejection and the legacy RED remain. This client-owned private change
  adds no GM authoring/response field; current Mortal examples/prompt guards retain
  their unfinished-policy wording, with no new afterlife contract. Final-rank event,
  same-rank slot-budget, complete tagged reference, skill diagnostic-path and death
  lifecycle obligations for the following publisher are source-pinned in the plan.
  No additional top-level task is marked complete.
  Selected-policy private handoff functional acceptance (2026-09-07): source
  `177a734e` plus test-only copy-seal assertion `caa2ef17` are parent-accepted
  after independent functional Spec and Code Quality approval. Actual final
  Integration 34/34, preservation 23/23 and strengthened-copy/tamper 2/2 are
  GREEN with clean builds and cleanup. The detailed guards preceded their
  detailed tests, contrary to the plan's per-guard test-first requirement;
  that historical deviation remains explicit. Four later isolated counterfactual
  failures establish current regression sensitivity, not retroactive TDD.
  The single Fast completed 6,016 rows with 6,015 PASS and the mandatory legacy
  authority/registration RED; 1,287 discovered rows did not complete. No all-Fast
  success is claimed. The next bounded T070 work is actual non-death policy
  publication with original/final rank continuity, full tagged identities and
  synchronized Mortal GM examples. Death/heal/legacy remain separate open work;
  whole-feature top-level completion remains 77/177, T070/T177/#1536 open.
  Selected non-death policy publisher plan (2026-09-07):
  `docs/superpowers/plans/2026-09-07-t070-selected-nondeath-policy-publication.md`
  now defines one coherent T070 task for actual increase/add policy publication.
  It shares pure ordered preview/publication projection, rejects same-final-rank
  budget changes before die/resource claims, preserves original-wound T069 and
  private T067 authority, carries full policy/direct tagged identities and exact
  canonical selector paths, and rematerializes only for original/final rank change.
  Real mixed partial/course/critical, 0/0 batch, rollback/retry/cold replay and GM
  worked-example coverage belong to that task. Parent functional acceptance
  (2026-09-07): range `919fffba..e8864fba`, runtime896758b6 and test-only review
  fixes caa3c5d9/e8864fba, independent Spec Compliant / Quality Approved with zero
  open findings. Current selected34, private/applicability40, direct26, removal24,
  severity39, recovery8, scalar-course34 and afterlife documentation121 pass.
  Actual root/child canonical publication5/5 and final isolated body/private3/3
  controls pass. One FullValidation1857/1857 is GREEN; one Fast5908/5909 retains
  the required legacy RED and1405 uncompleted discovery rows, not full-Fast success.
  Exact artifacts and the historical per-guard test-order limitation are recorded
  in that plan's acceptance. Mortal GM guide/example/manifest/guards are synchronized;
  no new afterlife-authored surface. Do not reimplement this bounded publisher.
  Death, selected heal/legacy, scheduled recovery and spiritual healing remain open.
  No additional top-level task is closed; T070/T177/#1536 remain open.
  Prepared follow-up prerequisite (2026-09-07):
  `docs/superpowers/plans/2026-09-07-mortal-follow-up-heal-staging.md` pins the
  missing FR-064 Mortal physical III/IV-to-active-I treatment staging allowance.
  It changes only the private treatment predicate and pure reducer tests; recovery,
  spiritual limits and the final terminal heal validator remain unchanged. This
  is not healing/legacy publication and does not decide the unanswered legacy
  preparation API. Implementation and independent acceptance remain pending.
  For an accepted `worsen` re-entry, T070 derives `care.state=untreated`, clears
  `stabilizedAtTurn`, restores `not_stabilized`, clears/allocates the condition anchor
  against the exact published worsening transition, and preserves the independent
  stabilization-rebased recovery anchor.
  T070 Phase A, after T069-A and before T069-B, owns typed canonical anchor
  representation, the sealed `AcceptedMechanicsWoundStageBundle` common-authority
  overload, and accepted initial-create anchor allocation/publication. It adds no
  treatment or recovery shortcut. T070 Phase B, after T067-B/T068-B/T069-B, owns
  treatment, stabilization, recovery, re-entry, durable receipt/history, and remaining
  atomic publication integration required before T069-C replay closure.
  Any treatment plan carrying `ConfirmedHeld` resources MUST use a production-minted
  one-use take receipt and remain open through the complete accepted-turn pipeline,
  including helper readbacks/validators, runtime refresh, wound post-seal/output checks,
  critical/full-state validation, cleanup, and final runtime refresh. Resource
  finalization commits only after those boundaries pass. Before commit, retryable
  publication failure performs byte-exact transaction compensation, retains the restored
  durable command plus its exact confirmed hold, and re-arms only the same receipt-owned
  plan; terminal rejection removes durable authority before release.
  T070 Phase A closure (2026-08-31): complete through `56a0f341`. Initial accepted
  Mortal physical creates now allocate closed recovery and applicable condition anchors
  from the accepted canonical world minute, bind them to the exact sealed stage bundle,
  and atomically project the carrier, identity, and history fingerprints only through the
  ordinary common-plan normalizer. Production `ValidationService` uses the anchor-aware
  overload; mixed create/worsen phases, foreign turn/snapshot/realm authority, stale
  source/target roots, and malformed UTF-8/BOM contours fail closed. The final owning
  Focused control is 26/26
  (`20260831-165721-585-15076-d3ac909530244cdc921592198907b890-focused`), and the fresh
  production-path integration sentinel is 1/1
  (`20260831-170307-054-15684-e79b0d9d2c584d7b95c271f6da92907f-focused`), both with
  warning-free builds, no timeout or duplicate IDs, and complete cleanup. Independent
  final rereview reports zero Critical/Important/Minor findings and Ready YES. The Fast
  checkpoint's completed descriptor executed 1,355 tests with 1,323 PASS and 32 failures,
  all at the unchanged future T067-B/T070-B treatment-capability publication seam
  (`20260831-164643-837-43692-63f282ef4b9f4e8f87b223ba34b08963-fast`); the runner did not
  schedule its next descriptor after that intentional failing descriptor. The complete
  recovery class independently executed 47 tests with 22 PASS and exactly 25 future
  T069-B/T070-B scheduler/publication failures
  (`20260831-163811-483-25492-19059150dc45468da077ff833a3e97c1-focused`). No GM prompt,
  example, manifest, or afterlife matrix update is required: this phase changes only a
  client-owned Mortal canonical anchor/publication path, while existing GM guidance
  already forbids authoring clocks, anchors, history, fingerprints, or canonical carrier
  post-state. Phase B remains explicitly open for treatment, recovery, re-entry, durable
  evidence, and remaining publication integration.
  T070 Phase B.3 closure (2026-09-02): `7857df41` publishes selected
  `resource_quantity` finalization through one write-free registered outcome, common plan,
  sole normalizer, and full-pipeline confirmed-hold transaction. Final owning controls are
  25/25 unit (`20260902-124035-530-11756-2da0987683f74a3ebec0879a06da0684-focused`),
  28/28 integration (`20260902-124346-656-40908-4e79ed903f354238a83160ff41d1d54c-focused`),
  2/2 lifecycle/source guards (`20260902-125026-454-19140-c838ea5b0683479a9bb008aa57660749-focused`),
  36/36 B.1/B.2 controls (`20260902-125101-008-48304-dd449cdecf9f4b6585010a60ff04270c-focused`),
  and 26/26 T068 controls (`20260902-125306-555-48744-17bdc653820c4df1a8757995911bb371-focused`).
  The Fast checkpoint completed its first host 1,296/1,296 before the lane limit; the
  separately measured recovery diagnostic contained 16 exact B.4 selected-item failures,
  6 still-open recovery-composer failures, and 1 earlier request-preparation failure. B.3
  changes only client-owned publication machinery and adds no GM-authored surface.
  T070 Phase B.4 is the next mandatory bounded contour and executes test-first in this
  order:
  1. freeze the existing six skill fields plus only the seven ordinary Mortal item command
     properties named in the spec; extract deterministic write-free shared transforms for
     the item phase, including pure transfer classification/application from complete
     `JsonNode?` backup/current roots and outputs with null retained for absent files, deterministic
     snapshot-owned creation receipt/create-transition plus transfer-transition IDs, and
     production creation collector order `UpdateInventory` -> NPC core -> NPC commands ->
     current location -> offscreen storage. Forward the already validated route/transfer
     catalogs, effective post-location roots, and snapshots without reread or rebuild. Apply
     only later ordinary transforms touching the selected item graph in exact tail order:
     quest history -> NPC core -> conditional NPC trade -> inventory items journal -> item
     bonds -> item text updates -> NPC item journals. Seal their output, detached NPC-core
     authority, NPC-trade/training pending bytes, and authenticated fingerprinted
     `MortalItemNpcTradeTailDisposition` (`Apply|SkipUntouchedTreatmentContinuation`)
     together with exact dispatch-owned `AppliedTransformIds`: `quest_history:v1`,
     `npc_core:v1`, the disposition-matching
     `npc_trade:apply:v1|npc_trade:skip_untouched_treatment_continuation:v1`,
     `inventory_items_journal:v1`, `item_bonds:v1`, `item_text_updates:v1`, and
     `npc_item_journals:v1`, preserving the current treatment-continuation skip gate immediately
     before common publication. Snapshot proof owns detached clones/DTOs/fingerprints and
     validates the complete path set bidirectionally, including exact file absence/presence and
     top-level topology. Add same-turn creation/transfer/equipment parity, every tail sidecar,
     both legacy vehicle object/array forms, and unsupported-envelope tests; exclude the
     plan-owned B.2 skill projection from this live baseline;
     expose the base `TransformRegistry` with `npc_trade:v1`, dispatch it through one
     `ApplyRegisteredTransform` loop, and source-guard that the returned applied ID is
     recorded inside the loop. Behaviorally prove that Apply consumes/removes one shared
     `UpdateNpcTradeInventoryReceipts` stimulus and creates its receipt while Skip retains
     the command and creates none;
  2. add a pure shared `MortalItemConsumptionPlanner` for sequential partial/full stack
     identity transitions, clear only supported inline equipment, reject container/quest/
     bond/other companion references without separate atomic authority, and refactor the
     ordinary writer to consume that same result; freeze its sole entry point as
     `Plan(MortalItemConsumptionPlanningInput)`, where the input contains the exact
     baseline fingerprint, complete detached carrier/companion roots, parsed identity
     state, ordered commands, definitions/state, and attempt-derived capacity evidence,
     and where invalid planning exposes no actionable partial result;
  3. add exact `instance_fixed` proportional item-owned capacity projection, full terminal
     owner retirement, and private registered-capacity integration in the common reducer;
  4. compose item carrier/index and the B.2 skill projection over the supplied semantic
     final ordinary NPC baseline into one shared root set while retaining the distinct true
     live canonical before-image for transaction rollback; preserve NPC trade, every tail
     sidecar, mirror, and actor-catalog validation;
  5. integrate mixed selected item/resource finalization into the existing B.3 authority,
     one-use take receipt, transaction compensation, cold recovery, and exact replay;
  6. retain the six-argument publication API, single `Finalize(resolution)`, and all public
     request/resolution/finalization DTOs; no direct item writer, raw patch, caller ID, or
     alternate normalizer is permitted.
  B.4 owning RED/GREEN files are
  `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ResourcePublication.cs`,
  `BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs`,
  and `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`.
  Task 2's owning normalizer extraction/commit list includes
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`,
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs`,
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs`,
  and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs`.
  B.4 remains a closed-envelope, client-owned publication contour: no migration,
  compatibility path, public DTO widening, or GM-authored surface/documentation change.
  Procedure/course/Fate/heal/recovery and T069-C remain later T070 contours; B.4 uses new
  guaranteed-treatment cold/replay cases and does not require an existing procedure/course
  publication test to become GREEN.
  T070 Phase B.4 bounded closure (2026-09-03): complete through `fdf27a20`. The final
  complete controls passed the final vehicle-topology remediation 13/13
  (`20260903-165600-005-52272-92bd7d09dd054aed9eb95fcfcd8a6bfd-focused`), 166/166 unit
  (`20260903-170208-682-26064-ec892393511e419aba517c704686ce75-focused`), 25/25
  integration (`20260903-171229-681-544-4e82f8d5e4da41fe89fb3b55977238da-focused`),
  retained B.3 30/30
  (`20260903-171938-916-58752-f9f55aa7ac8c4f3292a532c6e877918b-focused`), retained
  B.1/B.2 accepted stale-oracle RED 28/36
  (`20260903-172451-949-45224-6ae414a8253e489ba2fb9eb6032491c3-focused`) and 36/36 after
  the independently reviewed test-only oracle correction
  (`20260903-173926-467-1364-9cb5a800955d42a9be052a4ff93e4a8e-focused`), and retained
  T068 26/26 (`20260903-175602-771-57288-cbec96e4bb244bacabfbe7be839298c3-focused`),
  all with warning/error-free builds, no timeout or duplicate IDs, and complete cleanup.
  Exactly one Fast checkpoint executed 1,313 tests with 1,312 PASS, no lane timeout,
  duplicate IDs, build warning/error, or cleanup leak
  (`20260903-175817-440-17720-05436190c30e49168b09a74f2613a130-fast`). Its sole
  official failure was an unrelated seven-second browser canonical-lease fencing timeout;
  the exact test passed 1/1 in isolation
  (`20260903-180606-032-29292-b79b33e83fd04206be5dc6c3bf840ed3-focused`). An unfinished
  no-TRX shard printed ten procedure/recovery rows, all at the explicit deferred
  `mortal_wound_treatment_publication_slice_unsupported@treatmentPublication.guaranteedStabilization`
  boundary. Independent cumulative review of `9aa64764^..fdf27a20` returned PASS with
  zero P0/P1/P2 findings. Structural inspection is clean except the pre-existing untracked
  `.serena/`; no migration, raw writer, public DTO/`GameResponse`, GM/browser/console, or
  afterlife contract surface changed. Selected legacy-array vehicle partial/full behavior
  is proven at the pure topology seam because the current accepted contract cannot select a
  vehicle-owned treatment item; untouched legacy-array treatment is proven end to end.
  T070, #1536, procedure/course/Fate/heal/recovery publication, and T069-C remain open.
  T070 Phase B.5 is the next bounded contour and follows
  `docs/superpowers/plans/2026-09-03-t070b5-procedure-publication.md`. It replaces the
  guaranteed-only successor gate with a typed scalar outcome-publication planner and admits
  exactly guaranteed/procedure singleton `stabilize` plus procedure singleton
  `no_improvement`. Natural-one Fate mitigation is published only from the sealed T067
  critical-reaction intent through #1535's accepted lifecycle-event seam; legacy
  `EffectEventReports` remains forbidden and a typed-plus-legacy duplicate receives the
  exact cross-surface diagnostic. The existing resource transaction becomes a coordinated
  treatment transaction for every procedure, including `not_required` resources: exact
  dice/Fate/resource claims and the persisted zero-claim resource agreement are checked
  before publication, retained across compensation, finalized without same-turn reuse, and
  released only after terminal durable-command quarantine. Cold recovery must reconstruct a
  finalized natural-one Fate claim from persisted reaction evidence without requiring the
  now-expired shield to remain an active candidate. B.5 does not admit `reduce_severity`, recovery,
  complication-effect batches, course, heal/legacy, or T069-C and does not widen the public
  response/request/resolution contract. It is a client-owned Mortal contour with no new GM,
  browser/console, migration, or afterlife documentation surface.
  T070 Phase B.5 bounded closure (2026-09-04): complete through `509554c2`.
  Implementation commits are `e2ecb79b`, `c7bdb05d`, `77616367`, `045bb5de`,
  `2e3defc0`, and `3dbf0572`; bounded-test corrections are `92831931`,
  `a3eea647`, and `667f86b0`; final-review remediation is `0cca886e` and
  `509554c2`. The final owning controls executed 39 tests: 38 passed and exactly
  the deliberately deferred `reduce_severity` row remained RED at
  `mortal_wound_treatment_publication_slice_unsupported`
  (`20260904-111802-251-35456-36e973d8b89b47398bca5ca496dbbec1-focused`,
  `20260904-112040-020-24580-584075d2043a4684b1c096225c62db82-focused`,
  `20260904-112207-664-46008-b61ec756d4f34de9ac9ab29166d157e0-focused`,
  `20260904-112426-554-35780-b945a21c74734e0aaa0ba290793774a5-focused`, and
  `20260904-112648-390-46848-663010660e4e46ca86fc0b89b99f2f0f-focused`). Retained
  B.1-B.4/T067/T068/#1535 controls passed 25/25
  (`20260904-112823-975-16220-db7d8ce960b34eb59c7dde6e5420e857-focused`,
  `20260904-112930-200-50480-f941f162fcc54513a6be33a5e41d9b8d-focused`,
  `20260904-113027-183-33000-f7d5315e649f48748b7cf0627e62e09d-focused`,
  `20260904-113122-794-38016-5f419a32d71d40b4ae0e8ea6b4821ea3-focused`, and
  `20260904-113158-467-58940-2b41d1fd09e245dabcbcbe7396ef30a4-focused`). The one
  required Fast checkpoint completed 4,759/4,759 in about 2:17 with 4,694 PASS
  and exactly 65 classified known diagnosis REDs, without timeout, duplicate
  IDs, warning/error, or cleanup debt
  (`20260904-082214-233-26816-a885b4a8285c4923a09768dc3ed2363f-fast`). After two
  remediation passes, fresh independent rereview reports zero
  Critical/Important/Minor findings and `Ready: YES`. `reduce_severity`,
  effect-bearing complications, course, recovery, heal/legacy, and T069-C remain
  open. B.5 changed only private client-owned Mortal publication enforcement;
  no public API/DTO, persisted schema, player command, GM-authored response
  shape, or afterlife runtime surface changed. The existing common-plan
  normalizer now applies already-specified client-owned after-images without a
  new GM-authored field or side-effect shape; existing wound guidance/examples
  already assign content to the GM and canonical identity/history/receipts/
  carrier post-state to the client. Therefore no GM prompt, worked example,
  manifest, browser/console, migration, or afterlife documentation update is
  required. T070 and #1536 remain open.
  T070 Phase B.6 is the next bounded contour and follows
  `docs/superpowers/plans/2026-09-04-t070b6-severity-rematerialization.md`. It
  extends the existing two-phase accepted-turn wound/effect pipeline rather than
  adding a scalar severity shortcut. The shared pure destination-rank projection
  preserves the complete authored consequence graph. It reconstructs the full
  persisted root/reaction envelope, invokes the severity-aware component-power
  catalog, requires exact derived-slot agreement, and separately canonical-
  reparses the projected wound; a lower rank whose unchanged graph no longer
  fits is rejected before an attempt can claim a die or resource. Accepted
  procedure/guaranteed results composed only from zero or one `stabilize` and one
  or two `reduce_severity` operations whose aggregate steps are one or two retain
  their sealed order,
  including every legal three-operation placement of stabilization among two
  one-step reductions, and produce one atomic final destination. The private treatment continuation uses a
  non-cyclic seed -> batch-topology -> final-seal authority DAG, expires the
  complete current active/suspended wound-source root/descendant group, and
  rematerializes every retained root through #1535; only that cached effect plan
  allocates fresh permanent effect identities. Every retained definition/
  ownership coordinate, through both existing `worsen` and new `treat`, records
  its exact prior root in first-create provenance; genuinely new roots remain
  parentless and removed definitions retain only terminal history. The lineage
  authority accepts only one acyclic, non-branching same-definition
  generation chain with fully terminal
  retired generations, so repeated legitimate treatment remains possible while
  forged disconnected terminal identities remain invalid. Final wound bindings,
  complication ownership, declared outcome, reducer/history evidence, and
  common-plan authority are derived after the exact result map is accepted. The
  contour includes the standard `[stabilize, reduce_severity]` success, retained
  selected-resource RED, failed/partial route-completion semantics, legal zero-
  root rematerialization, two distinct treatments, rollback/retry/cold-replay,
  and player/NPC/combatant carrier parity.
  It does not silently prune or weaken arbitrary GM-authored consequences and
  does not admit add-recovery, complication mutation, deterioration, course,
  recovery scheduling, heal/legacy, spiritual healing, or T069-C. Deterministic
  fixture-free projection/authority tests remain in Fast; the complete file-backed
  `MortalWoundTreatmentResolverTests*` family moves to RegressionIntegration under
  the approved #1551 lane correction, while full-engine filesystem/transaction/
  restart/replay cases extend the existing LifecycleIntegration fixture. The GM-
  facing Mortal wound guide/example/manifest gain a production-validated
  `stabilize + reduce_severity(1)` route; no afterlife runtime contract changes.
  B.6 remains open until its plan, implementation, retained controls, one Fast
  checkpoint, one bounded exhaustive run of the complete moved resolver family,
  lifecycle Integration evidence, documentation validation, and independent
  review are complete.
- [ ] T071 [US2] Remove loose Mortal aliases/wrappers, `generatedEffects`, `healingState.canBeImprovedBy`, `WoundReference`, `sourceWoundId`, `duration=999`, and NPC-effect-carrier fallbacks in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs`, `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`, and `BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs`

**T066/T067-A/T068-A+T069-A/T070-A/T069-B/T067-B/T068-B/T070-B/T069-C execution
gate**: implement T066's accepted-state, coordinate/time/course/start and
bundle/capability types plus first-course/non-course factories; implement T067-A's
procedure die/Fate authority and shared immutable shells; implement T068-A resource
preparation/registry and T069-A deterioration policy authority; use T070-A's sole common
accepted-create path to materialize canonical anchors; implement T069-B scheduling;
finish T067-B history-driven course authority, request factories, persistence, and
resolver; finish T068-B finalization/restart; finish T070-B treatment/recovery
publication; then close T069-C durable replay. T066 owns lease/base bundle/course types,
T067-A owns procedure authority, T068 owns resources, T069 owns deterioration/recovery
evaluation, T067-B owns history-driven course authority plus request/resolution
orchestration, and T070 owns all canonical anchor mutation and accepted publication.
None may use test-created authority/reservation/request/resolution/binding/anchor/receipt,
hand-written history/after-images, or a raw mutation fallback.

### GM contract synchronization

- [ ] T072 [P] [US2] Add RED Mortal diagnosis/hidden route/procedure/course/guarantee/resource/recovery documentation guards, including canonical skill extension fields, proposal-local complication removal, and typed client-owned Fate Shield treatment behavior with unchanged legacy GM report API, in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.MortalWounds.cs` and `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.MortalWounds.cs`
- [ ] T073 [US2] Replace legacy Mortal wound/treatment/recovery guidance and synchronize wound/skill/effect materialization compatibility in `Rules/Block_2.txt`, `Rules/Block_5.txt`, `Rules/Block_7.txt`, `Rules/Block_8.txt`, `Rules/Block_10.txt`, `Rules/Block_12.txt`, `CLI_API_Specification.md`, `OtherGuides/Wound_Materialization_Contract.md`, and `OtherGuides/Effect_Materialization_Contract.md`
- [ ] T074 [US2] Add post-apocalyptic and magical visible/hidden/alternative/partial/course/recovery/healing worked examples plus canonical skill guarantee, Fate Shield treatment compatibility, and the two-phase non-public `wound_legacy` source/result-map flow in `Examples/E_Block_5.txt`, `Examples/E_Block_7.txt`, `Examples/E_Block_8.txt`, `Examples/E_Block_10.txt`, `Examples/E_Block_12.txt`, `Examples/E_CLI_Ink_Feather_Actions.txt`, `Examples/E_CLI_Effect_Materialization.txt`, and `Examples/example_validation_manifest.json`
- [ ] T075 [US2] Run focused Mortal contract/authority/resolver/recovery/lifecycle/docs filters through `scripts/test-csharp.ps1` and record GREEN evidence in `specs/1536-complete-wound-materialization/tasks.md`

**Checkpoint**: Arbitrary Mortal wounds have complete setting-specific treatment and recovery lifecycles with exact resource authority and no catalog.

---

## Phase 6: User Story 3 — Suffer or Avoid a Spiritual Wound in Conflict (Priority: P1)

**Goal**: Make spiritual wounds optional GM-authored consequences of bounded strain transitions while preserving durable defeat and always-optional dissipation.

**Independent Test**: Run training/controlled/hostile/annihilation conflicts across every formula boundary and prove caps, none/lower choices, one wound per side, explicit re-trauma, bounded defeat, and optional dissipation.

### RED tests

- [ ] T076 [P] [US3] Add RED trauma-pressure term/threshold, harmful-margin source, resilience-tier delta with zero OD spend, strain rank/jump, natural 1/20, and exact `clear/strained/fractured/overwhelmed/broken` destination-cap boundary tests in `BookOfEternityClient.Tests/SpiritualWoundOpportunityTests.cs`
  Accepted arithmetic prerequisite (2026-09-07), shared with T084:
  `docs/superpowers/plans/2026-09-07-spiritual-wound-opportunity-math.md` records
  commit `1969a695`, parent source/artifact inspection and independent Spec
  Compliant / Quality Approved, zero open findings. Actual 42 semantic RED ->
  42 GREEN -> 24 invalid-domain RED -> 66 GREEN proves the unused internal pure
  calculator. One bounded Fast completed 6,208 of 7,380 discovery rows with
  6,207 PASS / one required unchanged legacy-source failure; arithmetic 1,172
  uncompleted is not an exact identity-set claim or full GREEN. All runs have
  zero build warnings/errors, successful cleanup and no timeout/duplicate artifacts.
  These rows do not prove accepted exchange provenance,
  zero-OD passive registration, natural-die independence in the live adapter or
  opportunity publication. T076 remains open for those integrated requirements.
- [ ] T077 [P] [US3] Add RED training escalation, controlled II cap, hostile/annihilation cap, mode visibility, and post-roll mode-change rejection tests in `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.Wounds.cs`
- [ ] T078 [P] [US3] Add RED one-new-wound-per-side, later worsening, declined-earlier opportunity, and explicit older-wound re-trauma tests in `BookOfEternityClient.Tests/SpiritualConflictWoundSealTests.cs`
- [ ] T079 [P] [US3] Add RED non-training bounded anti-repeat defeat outcome and annihilation winner softer/optional dissipation tests in `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualConflictValidationTests.DefeatWounds.cs`
- [ ] T080 [P] [US3] Add RED exact spiritual I-IV consequence count, legal axes, counterplay/safe-exit, effect-link, exact actor-to-current-side contribution, side-relative axis, absent/duplicate/wrong-realm/ambiguous participant rejection, conflict-close persistence, and byte-identical `combatConditions[]` tests in `BookOfEternityClient.Tests/SpiritualWoundConsequenceTests.cs`; map the same actor once to each side and prove exact `actionCostAudit.player|opposition` plus `playerSideStrain|oppositionSideStrain` with byte-identical canonical payload, preserve a non-empty real `afterlife_combat_condition` sibling independently, and require a separate typed contribution collection with exact effect/component/wound-source, target actor, resolved side, operation, source/resolved axis, magnitude, and priority provenance
- [ ] T081 [US3] Add RED conflict start -> exchange -> opportunity -> GM decline/lower/create/worsen -> resolve lifecycle and rollback fixtures in `BookOfEternityClient.IntegrationTests/AfterlifeSpiritualWoundLifecycleTests.cs`

### GREEN implementation

- [ ] T082 [US3] Add `spiritual_resilience` tier 0-V to standard afterlife art/profile/bootstrap/progression authority without adding it to operation types in `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- [ ] T083 [US3] Extend spiritual conflict start/state with declared danger mode, escalation evidence, per-side wound seals, and bounded defeat outcome in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
- [ ] T084 [US3] Implement trauma-pressure calculation, destination/mode/source caps, harmful-margin audit, one-per-side/re-trauma rules, and opportunity export in `BookOfEternityClient/Services/SpiritualWoundOpportunityAdapter.cs`
  The accepted T076-linked pure arithmetic prerequisite (`1969a695`) adds
  `BookOfEternityClient/Services/SpiritualWoundOpportunityMath.cs` without a runtime
  caller. A value calculation is not accepted source evidence. T084 remains open
  for the actual adapter, harmful-side provenance and all per-side/export rules.
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
- [ ] T159 Execute every applicable scenario in `specs/1536-complete-wound-materialization/quickstart.md` and reconcile all 100 FRs, 53 acceptance scenarios, edge cases, contracts, current bootstrap roots, docs, examples, and manifests against implementation
- [ ] T160 Request independent code review of #1536 focused on client/GM authority, staged wound/effect composition, same-root atomicity, replay/rollback, privacy, console/browser parity, and Mortal/afterlife documentation synchronization, and record findings in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T161 Verify every review finding against code/spec, add a focused RED test for accepted defects, implement only substantiated changes, and rerun the smallest affected filters
- [ ] T162 Inspect `git diff --check`, all changed files, generated state/examples, and `tasks.md`; remove placeholders, obsolete fallbacks, dead paths, and untracked artifacts while preserving unrelated user work
- [ ] T163 Run one final `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge` without an adjacent redundant Fast run and record exact frontend/build/TRX/count/timeout/cleanup evidence
- [ ] T164 Commit all verified #1536 code/tests/docs/spec task evidence, push `1536-complete-wound-materialization`, open a PR linked to #1536 with architecture/no-migration/GM-sync/verification/residual-risk summary, and record the PR URL in `specs/1536-complete-wound-materialization/tasks.md`
- [ ] T165 Inspect the GitHub PR diff/checks and remote branch, address only verified issues with focused tests, and confirm every required file is present before approval
- [ ] T166 Merge the approved PR into `main`, verify the merge commit on `origin/main`, close #1536 only when all accepted scope is present, and leave the linked Saref follow-up open if T148 required it

### Exact skill scope insertion (appended IDs; execute before remaining Phase 10 work)

**Purpose**: Extend the completed #1535 common `roll_modifier` with mandatory explicit
scope, then resume paused wound documentation and final controls against one final schema.

- [X] T167 [US8] **Enforce the closed structural scope union**: add RED closed-union rows and require every `roll_modifier` payload to contain exactly `operations`, `contribution`, and `scope`; accept only closed `all` or exact `skill` scope, require focused scope to use exactly `operations=["skill_check"]`, and cut shared fixtures to explicit broad scope in `BookOfEternityClient/Services/EffectComponentProfiles.cs`, `BookOfEternityClient.Tests/EffectMaterializationContractTests.cs`, `BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs`, and `BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs`
- [X] T168 [US8] **Build canonical offered/current skill-scope authority**: add RED/GREEN `EffectRollSkillScopeAuthority` coverage and implementation for detached bounded offered/current player and NPC active/passive catalogs, exact/confusable authority, new binding, runtime usability, fingerprints, and GM catalog projection; expose canonical NPC enumeration and accepted-root composition in `BookOfEternityClient/Services/EffectRollSkillScopeAuthority.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`, and `BookOfEternityClient.Tests/EffectRollSkillScopeAuthorityTests.cs`
- [X] T169 [US8] **Seal scope authority into accepted effect and wound planning**: add RED/GREEN planner, cache, reaction, wound-batch, repair, detached-candidate, and transcript-tamper tests; seal the skill-scope authority fingerprint into ordinary/reaction/wound accepted input, wound candidate, accepted-boundary transcript, and staged/final authority checks; structurally revalidate every component after scalar parameter binding and forbid `parameterBounds.scope`; revalidate composed final roots, preserve exact wound proposal repair coordinates, treat only authenticated treatment rematerialization as an unchanged accepted-selector continuation, and prove zero partial allocation/publication across `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/AcceptedEffectBoundaryTranscript.cs`, `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`, `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs`, `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs`, `BookOfEternityClient/Services/Validation/ValidationService.EffectMaterialization.cs`, `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`, `BookOfEternityClient/Services/WoundResponseInputComposer.cs`, `BookOfEternityClient/Services/WoundRepairPacketBuilder.cs`, and their owning tests
- [X] T170 [US8] **Introduce the single scope-aware roll reducer**: add RED/GREEN pure reducer coverage and implement the sole `EffectRollContributionResolver.Resolve(EffectMechanicsSnapshot, EffectRollContext)` in `BookOfEternityClient/Services/EffectRollContributionResolver.cs` so exact actor/realm/operation and `all|skill` scope filter before unchanged same-direction collapse and opposite-direction cancellation; carry detached current authority in `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs`
- [X] T171 [US2] [US8] **Bind Mortal treatment rolls to the exact selected skill ID and one detached source authority**: add RED/GREEN pure and Integration rows; remove the compatibility `Skill` constructor that conflates `CapabilityRef` with `SkillId`; seal nullable `RollSkillId` plus a bounded/versioned normalized `EffectDetachedRollSourceAuthority` through `MortalWoundTreatmentAuthority`, accepted canonical projection/state, requirement bundle, procedure authority/fresh validation, detached seal validation, restore clones, typed serialization, and `WoundAcceptedTurnPlanner.MortalTreatmentPublication`; capture all active common roll rows before filtering and use one `EffectRollContributionResolver` core for live, fresh, and detached resolution; make `resolved_skill_tier` derive its exact usable skill proof from the recursively valid requirement binding while `fixed_zero` supplies null/no proof; reject malformed/duplicate/confusable normalized rows and compact-result tampering; require fresh canonical source/result agreement; and prove jointly resealed source/result recovery restores prior die, Fate, and resource registries without leaking display/owner/carrier/full-payload data in `BookOfEternityClient/Services/EffectDetachedRollSourceAuthority.cs`, `BookOfEternityClient/Services/EffectRollContributionResolver.cs`, `BookOfEternityClient/Services/MortalWoundProcedureCheckAuthority*.cs`, `BookOfEternityClient/Services/MortalWoundTreatmentDetachedSealValidator.cs`, `BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs`, and `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests*.cs`
- [X] T172 [US5] [US8] **Project broad, focused, and dormant scopes safely**: add RED/GREEN projection rows and render broad, focused, unavailable, missing, and invalid current scopes as safe in-world Russian text without technical `skillId` leakage, similar-name selection, or hidden-effect visibility changes in `BookOfEternityClient/UI/EffectPlayerProjection.cs` and `BookOfEternityClient.Tests/EffectPlayerProjectionTests.cs`
- [X] T173 [US8] **Supply the bounded selectable catalog to the GM**: add RED/GREEN `EffectSkillScopeLifecycleTests` request-staging coverage and attach detached bounded `turn_request.json.effectSkillScopeCatalog` through `BookOfEternityClient/Models/TurnRequest.cs`, an isolated strict skill-root loader in `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs`, `BookOfEternityClient/Services/LiveTurnPreparationService.cs`, and `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`, with an explicit empty default for other routes and accepted-state recomputation proving catalog edits are non-authoritative
- [X] T174 [US1] [US8] **Complete direct-cutover fixtures and wound slot accounting**: reconcile the superseded per-operation wording to the normative one-whole-component/one-slot rule; add RED/GREEN broad/focused slot, exact `(operation, scope.kind, focused skillId)` selector-coordinate, same-selector duplicate, rematerialization, and fingerprint rows; keep scope semantic rather than a power or slot expansion in `WoundConsequenceEnvelopeCatalog`, `WoundMaterializationContract`, and `WoundPersistedConsequenceEnvelopeAdapter`; convert every executable `BookOfEternityClient.Tests`, `BookOfEternityClient.IntegrationTests`, `BookOfEternityClient.TestSupport`, `Examples/E_CLI_Effect_Materialization.txt`, and `Examples/E_CLI_Afterlife_Turns.txt` payload to explicit broad scope unless it is an intentional focused case; and add semantic source guards proving no active payload lacks scope
- [X] T175 [US2] [US7] [US8] **Prove lifecycle, dormancy, replay, and rollback in Integration**: add file-backed `RegressionIntegration` lifecycle evidence for ordinary/wound materialization, final binding rejection, derived dormancy/exact restoration/non-inheritance, restart, exact and changed-selector replay, cache invalidation, rollback, and focused treatment behavior in `BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs`, `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`, and `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests*.cs`; repair the raw-to-canonical definition-ref mapping in `WoundResponseInputComposer.cs` with deterministic `WoundRepairPacketBuilderTests` coverage
- [X] T176 [US8] **Synchronize GM contracts, examples, manifests, and guards**: turn scope documentation/source guards RED, then synchronize `OtherGuides/Effect_Materialization_Contract.md`, `OtherGuides/Wound_Materialization_Contract.md`, `Examples/E_CLI_Effect_Materialization.txt`, `Examples/E_CLI_Wound_Materialization.txt`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, and owning prompt/effect/wound/afterlife/example guards; preserve existing Task 7 wound documentation edits, prove broad wound, focused wound, and non-wound focused authoring, and record the no-daemon-change rationale when existing mandatory context-pack paths already load the updated files
- [ ] T177 [US8] **Complete verification and review**: run semantic legacy scans, one meaningful Fast checkpoint, focused afterlife/prompt/example documentation controls, required FullValidation, and RegressionIntegration only when focused lifecycle evidence leaves a related boundary uncovered; request independent review of offered/current trust, `RollSkillId`, repair, projection, detachment, replay/rollback, and lane placement, apply only verified corrections with focused tests, run `git diff --check`/status/log safety checks, and reserve PreMerge for a later explicit push/PR/merge request

**Checkpoint**: Every active payload has explicit closed scope; exact target-skill
binding, runtime dormancy, treatment, projection, slot accounting, replay/rollback, and
GM authoring agree before paused T148–T166 final work resumes.

T175 completion evidence (2026-09-06): lifecycle 16/16
(`20260905-185327-418-29756-57f34ed39be740db8aad93040c34acc2-focused`),
treatment SkillScopedRoll 14/14
(`20260905-185635-191-21872-60a477632d4d4f1fb44d50e7a70130f7-focused`),
repair packets 31/31
(`20260905-185751-161-26656-ed7e0a39f84b4fac98f2311381441c5c-focused`),
and Integration manifest 1/1
(`20260905-185837-769-57600-cd9b3a9c81864fb79e8fc4bd6a61b570-focused`)
passed with clean builds and cleanup, no timeout or duplicate IDs.
Independent final review: 0 Critical, 0 Important, 0 Minor; Ready for T175.
Known 65 `MortalWoundDiagnosisTests` failures remain mandatory to fix in their
owning T070 contour before #1536 completion and PreMerge; T177 remains open.

T176 completion evidence (2026-09-06): production-parsed broad/focused Mortal wound,
ordinary focused effect, and afterlife broad source examples pass 4/4
(`20260906-075339-943-2656-8c0f567a35154780ba815b419d014bcc-focused`);
afterlife and wound-prompt controls pass 127/127
(`20260906-075511-954-13600-06f26c97083246b9af455d302cda8ba4-focused`).
The prior Task 7 complete severity-reduction example and guard are incorporated.
Clean builds/cleanup, no timeout/duplicate IDs; independent review has zero findings.
No daemon path change: existing mandatory context-pack entries load the updated
effect/wound guides and examples. No matrix edit: it already delegates the common
payload. FullValidation passed 1,852/1,852 in 8:23.236
(`20260906-075652-842-8748-572c5ddf92a648c2844956c6ccf453bd-fullvalidation`),
with clean builds/cleanup, no timeout and no duplicate IDs. T177's meaningful
Fast checkpoint still requires the unfinished T070 diagnosis/alternative contour.

T177 review correction (2026-09-06): cumulative scope review found only I1, two physical
snapshot/lease Facts still in Fast. `71a4f016` moves exactly those Facts to Integration,
adds syntax-aware ownership coverage, and synchronizes the exact 38-source manifest.
Final owning Integration control is 6/6
(`20260906-084634-463-13104-5b288293013a49e9b9e286fba2846663-focused`); pure resolver is
44/44 (`20260906-084733-313-8724-b53c9cb7d9ac4d358adf02652b427210-focused`), clean builds
and cleanup. Independent correction review has 0 Critical/Important/Minor and no
unresolved checks. Fast's limit/category selection is unchanged. I1 is closed, but
T177 remains open for its meaningful Fast checkpoint after the required T070 behavior.

T177 narrow fixture correction within T070 addition Task1 (2026-09-06): the retained
`DeteriorationPolicyAuthority_RejectsSlotOverflowWithRootsAndDefinitionsAvailable`
fails its initial SlotsUsed2 assertion, before invoking policy authority, because
shared CreateScenario was already narrowed to one consequence at accepted f3c3c0c.
Parent checked the unchanged source/base diff. Restore the complete two-slot
CreateActiveWound consequences locally in that test only, before accepted-state
creation; keep every count assertion and the actual slot-overflow rejection unchanged.
RED225229-237-41592-a3a3894a34fe4185a81a35ab1ca1a5aa (25/26) is retained; final
coherent verification/review remains pending. No gameplay/GM contract change and
no completion of T070/T177/#1536 is implied.

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
- **Exact skill scope insertion (T167–T177)**: depends on completed #1535 and the current #1536 wound/effect foundation; despite appended sequential IDs, it executes before remaining T148–T166 work and blocks their final documentation, review, and verification conclusions.
- **Polish (Phase 10)**: depends on all desired stories plus T167–T177; #1536 requires all eight stories.

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
- Scope structural, authority, reducer, projection, GM-catalog, and lifecycle RED tests in T167–T175 own distinct first-failure surfaces; shared accepted-planner and documentation files remain sequential, and T176 incorporates rather than overwrites current Task 7 documentation work.

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
4. Add provider/faction access, complete T167–T176 exact skill scope, and synchronize every GM surface against the final explicit-scope schema.
5. Run T177 and the remaining cross-cutting audit/review/final controls, merge, then close #1536.

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
- Historical #1535 tasks remain complete; the dependent global-contract extension and every new unchecked implementation task are owned by #1536 T167–T177.
- Every dynamic wound/provider text surface is untrusted and must be escaped/sanitized.
- Update task checkboxes only after inspecting implementation and verification evidence.
