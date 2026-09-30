# Tasks: Complete Wound Materialization and Healing

**Input**: Design documents from `/specs/1536-complete-wound-materialization/`  
**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`  
**Method**: Test-driven development; every behavior test is written and observed RED before its implementation task.

## Current execution boundary — 2026-09-30

Owner resumed development on 2026-09-30 and approved the completion strategy:
Astra owns implementation, one independent Astra XHigh reviewer checks each
completed coherent block, and verified blocks are committed/pushed to this
feature branch. This supersedes earlier gameplay/publication pauses; merge and
issue closure still require final acceptance. Testing-only T066–T071 in
[feature 1505](../1505-test-suite-performance/tasks.md) are complete.
CATEGORY-SELECTION-DECISION rev1 supersedes every historical Fast/PreMerge/full-
suite gate below. Select affected categories only; do not rerun unchanged passes.

- [x] T081-RESUME-RECONCILIATION (#1536 / #1552) Reconcile Mortal recovery
  R1/R2/R3 and dependent-frontier implementation, independent review manifests
  and actual verification artifacts. Separate accepted work, implemented work
  missing evidence and absent behavior in the current plan. Replace superseded
  broad-run gates with impact-selected coverage without weakening assertions.
  Close child tasks only on inspected source/evidence, then execute the already
  approved RESULT-CLOSURE-BINDING B0–B5. Maintain the compact current checkpoint;
  retain historical evidence below instead of duplicating it.
  Accepted 2026-09-30: recovery R1–R3/RUNTIME-UNBLOCK and full T069 are verified;
  FRONTIER's three missing cold cuts now pass and independent Astra XHigh review
  accepts it. B0 is complete; execution continues in the separate open B1–B5 tasks.

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
- [x] T069 [US2] Implement canonical-world-minute recovery/deterioration policies, independent recovery versus condition/deterioration anchors, cadence/blockers/overflow/elapsed-next-anchor semantics, exact strictly-worsening policy classification for treatment interruption references, and exact closed typed tick outcomes in `BookOfEternityClient/Services/MortalWoundRecoveryPlanner.cs`. Stabilization clears the satisfied condition anchor while rebasing only recovery; a later accepted condition re-entry allocates a fresh deterioration anchor from that exact transition and minute without reallocating recovery. `MortalWoundDeteriorationPolicyAuthority` must offer the resolver-bound `Create(acceptedState, coordinates, policyRef)` overload as well as the canonical recovery-planner factory; neither accepts injected JSON/fingerprint/authority.
  Current acceptance (2026-09-30): independent Astra XHigh compared the complete
  original T069 scope with current source and completed evidence. Phase C is now
  covered by accepted recovery R1–R3: 4b106de0 23/23, 9cadbcff 4/4 and 24983227
  47/47; 36f50fd0 35/35 covers current cadence/authority and genuine stabilization
  and condition re-entry. Both policy factories/strict-worsening tests are unchanged
  since accepted f9cd385c and passed df2c7d6d 34/34. Completed integration-base-37.trx
  inside the failed aggregate 19d243e4 additionally confirms all current policy and
  16 history-first cases; the aggregate itself is not relabelled as passing.
  T069 requires a typed death handoff, not its later consumer. No T069 requirement
  remains open; broader treatment/legacy/death publication remains T070. The
  phase histories below retain their original dates and superseded open states.
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
  Independent T069-C admission prerequisite (not complete Phase C):
  `docs/superpowers/plans/2026-09-07-mortal-recovery-current-history-guard.md`
  adds a write-free current-history guard after exact registry authority and before
  fresh numerical planning. It consumes an explicit-lease synchronous filesystem
  read, original parser issues and the accepted-state semantic history seal;
  malformed/missing/unreadable/changed history returns InvalidHistory with no
  receipt/resolution. Sixteen real Integration controls cover fresh/cold/no-ambient
  reads, equivalent JSON and sealed clock, plus authority precedence; one source
  guard synchronizes the Mortal worked continuation and manifest. No recovery
  receipt schema, producer, cadence consumption rule or durable replay is chosen.
  Bounded admission guard accepted (2026-09-07), source `b2bc29f8..f9cd385c`:
  Integration16/16, owners42/42 and parsers/docs51/51 PASS; one five-minute Fast
  timed out after7,282 PASS, followed only by exact missing347/347 PASS in Focused.
  Parent verified the complete diff and actual artifacts;7,629 combined passing
  cases equal7,575 discovery plus54 known theory expansions without cross-run
  method overlap. Independent review is Spec Compliant / Quality Approved,
  zero source defects or open Critical/Important findings under the documented
  development-checkpoint policy correction. Fast remains exit124, not GREEN;
  T177/performance/final PreMerge remain open. Historical overlapping RED runs
  caused two MSB3026 plus one MSB3101 warning, retained as a Minor process finding;
  final owner builds/cleanup are clean. The bounded plan records exact evidence
  and the Mortal-only no-FullValidation rationale.
  Full T069-C/T070/T074/T177/#1536 and top-level count77/177 remain open.
- [ ] T070 [US2] Compose Mortal accepted-occurrence creation, opportunity-decision receipt/pending-occurrence consumption, plus diagnosis/alternative-route/treatment/recovery wound/effect/item/resource/history/output after-images; re-export guaranteed authority from the final composed player/NPC skill after-image whenever its root is touched; revalidate and atomically consume/expire the already T067-resolved exact per-attempt Fate Shield intent or roll back; and prepare deterministic cosmetic IDs plus non-GM `wound_legacy` source exports/history through `MortalWoundHealLegacyPlanner` before publication. On a source-result transaction, accept only T064's complete harmful typed candidate batch, read both occurrence and receipt roots as signed before-images, combine pending and consumed replay by producer operation key plus batch ordinal/count, reject changed or incomplete batches, derive canonical occurrence/public identities inside the common plan, append only genuinely new candidates atomically in ordinal order, and expose opportunities only after the next active pending-turn snapshot has sealed exact bytes. On the later wound-decision transaction, extend the common signed-snapshot/before-image inventory with both occurrence and receipt roots. Every accepted `none` or `materialize` decision appends exactly one sealed receipt carrying the consumed occurrence's source replay authority and consumes exactly one occurrence; a decline publishes null wound/transition coordinates without inventing a wound-history row, while a materialized receipt carries exact wound/transition IDs and matches exactly one ordinary `create|worsen` history row on transition/wound/turn/event/operation/source authority. Exact cold replay emits no command/transition, changed decision conflicts, and failed publication/rollback leaves neither receipt nor consumed occurrence. Register the closed legacy source, feed its ordered per-mechanical-legacy typed batches through #1535 while leaving effect IDs solely to that planner, exclude it from active-wound cleanup, and preserve source/history after later effect removal in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/MortalWoundHealLegacyPlanner.cs`, `BookOfEternityClient/Services/EffectSourceDefinitionContract.cs`, `BookOfEternityClient/Services/EffectSourceAuthority.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`, `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`, and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Wounds.cs`
  Accepted source-catalog prerequisite (2026-09-07), commit `690288f4`:
  `docs/superpowers/plans/2026-09-07-wound-legacy-source-catalog.md` registers the
  exact non-public lower-level source while closing ordinary composer injection.
  It preserves the existing mandatory source test and adds 28 focused catalog /
  composer cases. Parent actual source/artifact inspection and independent Spec
  Compliant / Quality Approved found zero open defects. Semantic RED29 (23FAIL /
  six controls PASS) -> GREEN29, owners50/50; unchanged mandatory legacy source
  test now passes in Focused and Fast. One five-minute Fast completed6450 of7526
  discovery rows, 6447PASS / three separately diagnosed reflection-harness FAIL,
  arithmetic1076 uncompleted, not full GREEN. Clean build/cleanup, no timeout or
  duplicates; exact evidence is in the linked plan. The
  legacy preparation choice remains unanswered; canonical source/link contracts,
  real typed batches/finalization/history/reload/publication remain open T070.
  The bounded source/lifetime prerequisite is accepted through `642eb4e2`
  (BASE `d4a0a06c`), with exact evidence in
  `docs/superpowers/plans/2026-09-07-wound-legacy-canonical-vocabulary.md`:
  admit the exact kind in canonical SourceKinds/LinkKinds for the existing
  source-bound derivation, but preserve authored `links=[]` and the unchanged
  definition LinkKinds set. Its complete-code plan covers48 structural/catalog/
  documentation rows, existing non-public guards, six owned files, and shared
  guide/worked-continuation/manifest updates without a new GM selector. The
  broader private legacy adapter/history/reload publication and all unresolved
  architecture choices remain open. Parent source/artifact audit and independent
  Spec Compliant / Quality Approved found zero defects. RED35/48 -> owning
  GREEN371/371, including all48 new rows; one Fast7,628/7,628 in3:23.368 and
  FullValidation1,857/1,857 in10:46.920 passed with clean builds/cleanup and no
  timeout/skips/duplicate IDs. Final manifest consumers2/2 and source guard1/1
  passed after metadata cleanup. Only this bounded slice is accepted; full T070,
  T074/T177 and #1536 remain open, top-level count77/177 unchanged.
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
  Accepted follow-up prerequisite (2026-09-07), commit `ca29359c`:
  `docs/superpowers/plans/2026-09-07-mortal-follow-up-heal-staging.md` pins the
  missing FR-064 Mortal physical III/IV-to-active-I treatment staging allowance.
  It changes only the private treatment predicate and pure reducer tests; recovery,
  spiritual limits and the final terminal heal validator remain unchanged. This
  is not healing/legacy publication and does not decide the unanswered legacy
  preparation API. Parent source/artifact inspection and independent Spec
  Compliant / Quality Approved found zero open defects. Actual eight-row RED
  (four bound failures/four preservation PASS), eight GREEN and full reducer
  148/148 PASS are recorded in the linked plan. One Fast completed 6,093 of
  7,388 discovery rows with 6,092 PASS / one required unchanged legacy failure;
  arithmetic 1,295 uncompleted, not full GREEN. All runs stayed within five
  minutes with zero build warnings/errors, successful cleanup and no duplicates.
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
  Live implementation decomposition (2026-09-08), shared with T084-T092:
  `contracts/spiritual-wound-live-turn-boundary.md` owns one logical player turn,
  internal GM continuation and one final common publication. No top-level task
  is closed by the following dependency sequence:
  - [x] T081-A [US3] Extract detached complete conflict validation consumed by
    `ValidationService.AfterlifeSpiritualConflict.cs`, with frame acquisition in
    `ValidationService.AfterlifeSpiritualConflict.Frame.cs`; add a genuinely
    signed zero-error raw/publication/final-conflict fixture and captured-input
    equivalence controls in `AfterlifeSpiritualConflictValidationTests.Wounds.cs`.
    This does not export a wound source or change current arithmetic/offline rules.
    The test-only genuine resource/publication baseline is implemented through
    `AfterlifeResourceCutoverTests.ConflictFrame.cs` to reuse the existing validated
    ledger fixture without duplication. Its complete-code execution plan is
    `docs/superpowers/plans/2026-09-08-spiritual-conflict-frame-baseline.md`;
    detached-frame equivalence and source admission are not closed by that step.
    The fixture-only step is accepted at `a54fc3d5`: genuine zero-error signed
    publication, clean unauthorized-die sensitivity RED/GREEN, final 2/2 and
    unchanged-neighbor 47/47 evidence; independent Spec compliant / Quality
    Approved with parent diff/artifact verification. That fixture did not alone
    complete production-consumed detached validation or its equivalence controls.
    The complete production/test execution plan is
    `docs/superpowers/plans/2026-09-08-spiritual-conflict-frame-extraction.md`
    with its companion patch: executable pre-API RED, 28 new rows plus the
    retained two-row baseline, owner controls, independent review and one Fast.
    Production extraction accepted at `4cb7d774`: complete diff and nine-run
    artifact history inspected by parent, independent Spec compliant / Quality
    Approved with the sole report-counter finding corrected. Corrected 8/8,
    frame 30/30, resource owner 71/71, spiritual owner 449/449 (445 descriptors)
    and Fast 7753/7753 in 4:23.090 are verified; zero warnings/errors, no timeout,
    skips or duplicate executions, complete cleanup. Dynamic MemberData counts
    and rejected transient diagnostics are recorded in the extraction plan.
    Internal behavior-preserving refactor: no GM prompt/example/manifest change.
  - [ ] T081-B [US3] Extract a typed source-prefix resource/effect intermediate
    consumed by `AcceptedMechanicsPlanner.cs`; test retained identities, actual
    transition proof, dependent suffix, pending receipt waves and one final plan
    in `AfterlifeSpiritualWoundLifecycleTests.cs`. Do not retain a temporary
    publishable plan or rerun identity allocation to simulate a continuation.
    - [x] T081-B1 [US3] Separate completed ordinary resource/effect reduction
      from once-only final assembly in the existing production planner. Complete
      code and six behavior tests are specified in
      `docs/superpowers/plans/2026-09-08-accepted-mechanics-ordinary-reduction.md`
      and its companion patch. Preserve receipt, identity, replay and Mortal
      publication behavior; verify existing signed/pending/publication controls.
      This internal refactor creates no wound-source authority or GM contract.
      Accepted2026-09-08: core `c9230065`, required two-file fixture `05a35ba6`;
      independent aggregate `e3949168..05a35ba6` Spec compliant / Quality Approved,
      zero Critical/Important/Minor. Actual new6/6,owning423/423,signed2/2,Mortal4/4
      and corrected pending11/11 are green; semantic and fixture REDs retained.
      T177 browser/manifest follow-through `02aa0dbf..5b57534c` is independently
      review-clean with exact body preservation, focused guards1/1 and2/2, final
      inventories67/40. Full parent Fast7758/7758 (discovery7704),4:54.9235103,
      build0/0, no timeout/skips/duplicate executions/cross-descriptor IDs, complete
      cleanup; both reviews' Fast evidence gaps resolved. Exact artifacts and
      historical failed Fast are recorded in the three bounded plans. This checks
      only B1; top-level79/177 and full T081/T084/T085/T177 remain unchanged/open.
    - [ ] T081-B2 [US3] Retain and production-consume actual causal execution:
      prepared graph/identities, scheduler/frontier, resource ledger/history,
      accepted effect transcript state and incremental effect advancement.
      Stop at a closed source boundary, resume the same execution after an
      authorized decision, and recompose only the unaccepted dependent suffix.
      Prove exact unchanged-run equivalence, two wound-dependent exchanges,
      receipt waves, last-use/terminal reservations, retry/cold identity proof,
      failed suffix with no publication and one final common plan. B1 completion
      alone must not close B2 or T081-B; full runtime/GM synchronization applies.
      - [X] T081-B2A [US3] Retain the actual fixed-graph resource execution in
        one owned session, consumed by ordinary BuildResources/Drain. Complete
        three-file code and nine tests are in
        `docs/superpowers/plans/2026-09-08-retained-resource-execution-session.md`
        and its companion patch. Explicit closed-frontier stepping preserves
        prepared identities, scheduler, ledger/history, arbiter and transcript;
        no terminal history/effect seal or common plan is created at a checkpoint.
        This prerequisite does not update a wound-dependent suffix, admit a
        spiritual source or complete effects. Preserve real zero-mutation sources;
        full B2 remains required for live effect draft/journal, authorized wound
        insertion, next-exchange consumption, suffix replacement and cold/receipt
        continuation. Verify all prescribed owning/pending/signed/scale controls.
        Accepted2026-09-08: codef5e4de2f, full reviewfc6887e6..f5e4de2f Spec/Quality
        approved0C/I/M; exact production postimage and fixture-only corrections
        inspected. Actual Focused9/9,432/432,6/6,11/11,2/2 and parentFast7767/7767,
        wall5:00.6051031,default5m/TimedOut=false,build0/0,cleanupcomplete,zero
        skips/duplicate executions/cross-descriptor IDs. Exact artifacts and both
        historical REDs are recorded in the bounded plan. Full B2 remains open.
      - [X] T081-B2B [US3] Capture and production-consume a detached unsealed
        closed effect prefix, with exact authority-keyed use/terminal evidence
        and complete mechanics-frontier validation. Implement the five-file,
        nine-Fact complete plan/companion
        `docs/superpowers/plans/2026-09-08-nonterminal-effect-prefix.md`.
        Preserve ordinary Drain, terminal Freeze and all corrected B2A helpers;
        the view is not source authority or actual incremental effect state.
        Accepted2026-09-08: code02fafbdf, full review3e4518cb..02fafbdf approved0C/I/M.
        Parent reconstructed all five postimages and inspected actual RED0/1,
        GREEN45/45,109/109,5/5 andFast7776/7776,wall4:59.1412643,default5m,
        build0/0,cleanupcomplete,no skips/duplicates. All8new builder/10session
        Facts present; only one signature wrap differs from planned code.
        Exact artifacts and no-GM-update rationale are in the bounded plan.
      - [ ] T081-B2C [US3] Own actual effect materialization and identity/history
        edits in the same retained draft and consume them through the production
        finalizer and real wound insertion/next-exchange route. Follow the exact
        dependency-local barrier, allocation/history-anchor, routing-deferral and
        base-plus-insertion rules in
        `docs/superpowers/plans/2026-09-08-spiritual-effect-draft-journal-design.md`.
        Preserve the no-insertion global schedule; do not flush lifetime/terminal
        folds at a wound barrier or substitute a journal DTO/observer for writes.
        Prove all13 scenarios including consuming replacements, actual descendants
        before worsening, repeated re-trauma and composite-authority rejection.
        - [X] T081-B2C-J1 [US3] Own actual identity-history creation, append,
          anchored insertion and effect-factory allocation projection; both
          production initial/final plan builders consume the owner's image.
          Implement the complete four-file plan/companion
          `docs/superpowers/plans/2026-09-08-effect-identity-history-owner.md`.
          Prove13Fast rows plus a real Integration duplicate-child cascade,
          including OLD-production characterization and exact failure stage.
          Preserve consumption-before-replace and the original ordinary phases.
          This checks identity/history ownership only; J2 must immediately add
          actual carrier/source/phase ownership with real wound insertion and
          next-exchange consumption, not another observer-only prerequisite.
          Accepted2026-09-08 at80b09558: exact four-file companion, independent
          Spec compliant / Quality Approved review with zero findings;
          OLD RED0/1 plus OLD characterizations1/1 and3/3; GREEN13/13,131/131,
          Integration7/7; parent Fast7789/7789 in4:48.801/default5m, build0/0,
          no skips/cross-descriptor duplicates and complete cleanup. Parent
          read actual evidence; only this nested subtask is accepted.
        - [X] T081-B2C-J2-A [US3] Make ordinary production effect completion
          consume the real carrier/source/target/skill/phase owner and retain
          exact application edits, including non-create outcomes, plus J1
          history/allocation ranges. Follow the six-file complete-code plan
          and companion in
          `docs/superpowers/plans/2026-09-08-effect-draft-write-owner-unit-a.md`.
          Record seven OLD payload/allocation goldens and the real-production
          ownership-contract RED, then23 GREEN rows and the prescribed planner,
          scale, pending-wave and afterlife controls. Parent review and Fast
          remain required. These are ordinary-phase receipts, not spiritual
          source admission or a completed per-operation dependency journal.
          Real source acquisition, current-generation wound insertion and
          next-exchange consumption remain mandatory in the same J2 slice;
          this unit cannot close B2C, T081 or #1536.
          Accepted2026-09-08 at e720b03f: six exact companion postimages,
          independent Spec compliant / Quality Approved review (0C/I/M),
          actual OLD7/7 and ownershipRED0/1, GREEN23/23, planner145/145,
          Integration70/70+5/5+46/46 and final23/23. Parent Fast7812/7812
          in4:18.111/default5m; all28TRXs,23newrows and exact7OLD/GREEN
          payload/allocation outputs audited, build0/0/cleanup complete.
        - [ ] T081-B2C-J2-B [US3] Acquire the real original spiritual source
          under the canonical lease and make the production validator consume
          the shared strict checker. Apply the user-approved 2026-09-08 rule:
          ordinary sources and special sources without a wound declaration have
          neutral-IV source caps and no guarantee; special restrictions/guarantees
          require exact pre-materialized canonical declarations within harder
          bounds. Do not infer that authority from a caller cap,
          DTO, frame or hash; do not omit ordinary/special/guaranteed/start/
          terminal families. Source-only preparation is not admitted wound,
          successful resource spend or publishable receipt. Complete-code plan
          and actual signed positive/negative controls precede implementation.
          - [X] T081-B2C-J2-B0 [US3] Implement authenticated original-path
            presence via the parent-reviewed complete-code plan and companion
            `docs/superpowers/plans/2026-09-08-spiritual-snapshot-presence-b0.md`.
            Six nullable hash-aligned manifest DTOs, three real lease-bound
            producers, a closed sixteen-path observation set, and the opt-in
            reader distinguish signed absence from uncovered paths. Preserve
            old optional reads, the 64-path bound, and both restore mechanisms.
            Prove semantic RED/GREEN, 5 pure contracts, 19 Integration cases,
            2 GameEngine cases and existing/boundary controls, then independent
            review before B1. Parent owns the combined B0/B1 Fast checkpoint.
            This client-owned metadata adds no GM-authored surface; no GM
            prompt/example update is required for B0 alone. B0 is not wound
            admission and does not complete its parent or issue #1536.
            Accepted2026-09-08 at corrected source084146ea: full13-file
            companion replay, pure5/5, Integration19/19 plus GameEngine2/2,
            existing156/156 and independent re-review0C/I/M. The older
            boundary gate is now genuinely GREEN60/60 at ecffabdc after
            T177-BROAD-OWNERSHIP review0C/I/M; owner12/12 also passes.
            Earlier RED, fixture correction and timeout artifacts are retained.
          - [X] T081-B2C-J2-B1 [US3] After reviewed B0 named controls, acquire
            original source bytes or signed absence through its real reader
            under the canonical lease, then validate source declarations,
            targets and terminal exchanges without granting source admission.
            Execute the reviewed nine-file complete-code companion in
            `docs/superpowers/plans/2026-09-08-spiritual-source-owner-unit-b1.md`.
            Parent full-line replay confirms all nine source postimages and the
            reviewed tenth-file correction at source commit66d11b02. Current
            evidence: source60, pure20, corrected exact3 and unchanged frame26
            all pass, with exact retained TestId sets. The bounded fixture plan
            `docs/superpowers/plans/2026-09-08-spiritual-source-b1-frame-fixture-correction.md`
            and its exact companion preserve the diagnostic/historical boundary.
            No current/default source fallback was added and no final assertion
            was removed. Independent review at66d11b02 is Spec compliant / Quality
            Approved0C/I/M; parent read full source/evidence/report. The bounded
            B1 source-local task is accepted at the combined checkpoint below.
            Immediately synchronize source-action/terminal GM examples and guards through
            `docs/superpowers/plans/2026-09-08-spiritual-source-action-gm-sync.md`
            with this runtime change, not with B0. Apply that eight-file unit
            together with the old envelope parser-dependent Integration partial:
            their section headings delimit exactly one envelope and two action
            JSON fences. Parent owns one combined Fast and conditional
            FullValidation after named controls/review, not separate repeated runs.
            Accepted2026-09-09: GM9 sourcee8b1c71e has exact companion agreement,
            strict Integration4/4 and documentation129/129, independent0C/I/M.
            Reviewed process move01cae542 and narrow inventory correctionab2dd976
            resolve two preserved Fast failures without changing runtime/deadlines.
            Corrected Fast7870/7870 in4:07.056/default5m and conditional
            FullValidation1920/1920 in12:26.460/default15m pass; parent inspected
            actual logs/summaries/all40TRXs and exact discovery/theory membership.
            This does not close parent B, T081, C1/C2/D/E, T177 or full #1536.
        - [x] T081-B2C-J2-C1 [US3] After the B0/B1/GM checkpoint, implement the
          shared retained resource execution core and next-whole-exchange staging
          from `docs/superpowers/plans/2026-09-08-spiritual-journal-c1-implementation.md`
          and its complete nine-file companion. Preserve fixed-input goldens,
          accepted prefix identities, single arbiter and both-side causal closure;
          reject accepted exchange ID reuse even for zero work. OLD14 has exactly
          one semantic allocation RED; GREEN22, Integration20 and exact pinned
          three OLD/GREEN payloads precede independent review and one parent Fast.
          Internal client-only data: no new GM-authored/publication contract and
          no C1 FullValidation. This is not source admission or complete Unit C.
          Initial OLD artifact20260909-001823-083-38640-deddf28fd3004abd8a095ec81408dd03-focused
          is preserved as12/14 (expected allocator RED plus invalid golden
          precondition). Actual failure is AppliedTransitions NotEmpty at line355,
          not missing AcceptedPendingResolutions: the unchanged receipt-wait
          control passes. Correct only the new golden test and all literal plan/
          companion copies: pending requires empty applied transitions, nonempty
          accepted pending resolutions and incomplete transcript; other contours
          retain nonempty applied transitions. No runtime/fixture/input change.
          Preserve the initial OLD pin untouched; select a distinct explicitly
          pinned corrected OLD run before any production refactor and compare
          its three full goldens with GREEN. No completion claim yet.
          Resumed2026-09-13 after Windows reinstall: native git worktree repair
          restored this worktree at D:/Games/worktrees/boe-1536-wound-materialization.
          Durable current execution ledger: .superpowers/sdd/2026-09-08-spiritual-journal-c1-implementation/progress.md.
          Restore the missing .NET8 toolchain before the corrected OLD run;
          preserve original artifacts and translate only active execution paths.
          Corrected OLD20260913-005724-621-24132-7177304af20d4d5b9f92e1a109925d41-focused
          is13/14 with only the intended allocator2-versus4 RED, all three full
          goldens captured, no timeout/skips/duplicates and complete cleanup.
          Its distinct corrected-golden metadata receipt pins summary SHA256
          8B04F8E3096851B3869985C106D3CC64EE1BE07CBEEF5915216A2CE86E8682FA.
          Static correction review is Spec PASS/Quality PASS,0C/I/M. Cold rebuild
          exposes11 existing CS1998 warnings; all affected files equal HEAD.
          OLD Integration11 passed; C1 production and corrected GREEN22/Integration20
          now pass with exact OLD/GREEN goldens and independent static PASS/PASS.
          Parent Fast20260913-020621-837-26732-a5004cf3b74f43dca464405178624e3d-fast
          stopped at5667/5669 with two verification defects tracked below; this
          is not a complete Fast pass or C1 acceptance.
          Parent accepted bounded C1 on2026-09-13 after the corrected Fast
          20260913-022031-360-25788-7b45153e40764e18b9d0ef56d558d744-fast:
          7882/7882 in3:18.867/default5m, exit0, no timeout or skipped rows,
          complete cleanup, no cross-TRX duplicate IDs, zero build warnings/errors.
          Actual30TRXs contain7882 unique executions. Final focused22/22 and
          Integration20/20, unchanged three pinned OLD/GREEN goldens and both
          independent Spec/Quality PASS reviews were inspected by the parent.
          C2/D/E, parent Unit C and full #1536 remain open; no runtime cutover.
        - [x] T081-B2C-J2-C1-V1 [US3] Resolve the two observed C1 Fast gate
          failures without changing production or weakening runner boundaries:
          update the accepted-mechanics ownership inventory anchor for the actual
          partial class declaration, and make the invalid parameter-set test use
          PowerShell's stable structured error identity across Windows UI locales.
          Preserve the failing Fast artifact, exact rejected CLI combinations,
          nonzero exit/no-lane-start assertions, and the bounded runner. Verify
          both focused regressions and obtain independent review before one
          corrected Fast control. This verification-only follow-up belongs to
          #1536; no GM-authored/runtime contract or FullValidation change.
          Completed: actual exact partial anchor plus structured CLIXML error
          identity alongside the original plain CLI exit/no-lane assertions.
          Parent Focused20260913-021849-370-28256-7730cf11f9aa40dc936651f6a5259b99-focused
          passes2/2; independent Spec PASS/Quality PASS,0openC/I/M; corrected
          Fast above passes7882/7882. Runner behavior/deadlines remain unchanged.
        - [ ] T081-B2C-J2-C2 [US3] Bind C1 intervals to actual B1 source references
          and the current original-turn effect generation; register/retire new
          instances without resetting the shared arbiter. Implement owner-bound
          receipt and missing-side resume on that same retained session, including
          lawful start/terminal/passive/champion/dice-free contours. A pending guard
          or reconstructed DTO is not continuation. D actual wound insertion/eight
          consumers and E cold single publication remain mandatory before cutover.
          Parent boundary audit started2026-09-13 after accepted C1/V1:
          `docs/superpowers/plans/2026-09-13-spiritual-journal-c2-boundary-audit.md`.
          It records actual B1/C1/draft/arbiter/pending producer dependencies;
          it is not an executable plan or completed implementation. C2 remains open.
          Execution resumed2026-09-15 with complete companions:
          `docs/superpowers/plans/2026-09-15-spiritual-retained-resource-continuation.md`,
          `docs/superpowers/plans/2026-09-15-spiritual-source-continuation.md`, and
          `docs/superpowers/plans/2026-09-15-spiritual-original-capture.md`.
          Retained receipt implementation has semantic RED, Focused9/9, independent
          scoped review PASS and Fast7882/7882. Additional narrated/zero coverage
          exposed advertised `resource_delta: 0` projecting an illegal zero-request
          mutation; Z0 now fixes and verifies this mismatch with independent review.
          Signed source continuation is implemented with final focused families
          10+28+16 and independent bounded PASS. Missing-side continuation is
          applied: semantic RED, Focused13+9 PASS and independent XHigh PASS.
          Original capture is applied and independently accepted: read-set defect fixed
          (RED5/GREEN12, independent recheck PASS); chronology clarified by owner
          and verified14+9+12 with independent XHigh PASS; C2-PREFIX was still open at that checkpoint.
          Common original-capture continuation accepted: signed OLD controls, Focused9/9, independent XHigh PASS and Fast7872/7872 (4:48.949). These internal units do not complete source admission, generation/D or E.
        - [x] T081-B2C-J2-C2-ORDER [US3] Resolve original resource ordering before
          accepting capture or connecting common pending continuation. Owner
          chose chronological exchanges2026-09-16: ordinary/lifecycle prefix once,
          then complete each exchange and causal closure in journal order.
          Contract/spec/plan record the deliberate difference from fixed global
          phases. Experimental phase gate removed. Verified completed-parent
          recovery-to-cost graph append without relaxing in-flight dependency
          rules; fourteen capture, nine receipt and twelve graph cases passed with independent XHigh acceptance.
        - [x] T081-B2C-J2-C2-PREFIX [US3] Bind the first exchange audit to the
          actual accepted ordinary resource/effect prefix when its net resource
          state differs from the signed original ledger (for example6->5 ordinary
          spend then exchange5->2). Existing source/outcome acquisition currently
          rejects this before execution; do not weaken signed-source checks or
          claim general chronological runtime support from the capture helper.
          Feed genuine owned prefix evidence through source validation; cover
          direct mutations and triggered changes. Required before C2/D/E cutover.
        - [x] T081-B2C-J2-C2-Z0 [US3] Reconcile the advertised inclusive zero
          bound with the ordinary positive resource-mutation contract. Preserve
          an accepted `resource_delta` amount `0` as terminal receipt/use evidence
          while projecting no mutation, resource event, after-component work, or
          descendant activation; keep positive-request/clamped-zero transitions
          unchanged. Add pending-resolver, fixed-path and retained-session
          RED/GREEN coverage, then synchronize the Mortal and afterlife worked
          examples, afterlife matrix, manifest and source guards before integrated
          C2 acceptance. This does not complete C2, D, E, T177, or issue #1536.
        - [x] T081-B2C-J2-C2-V2 [US3] Apply the owner's 2026-09-15 clarification
          that test-lane budgets may evolve with measured workload. Document the
          policy in `docs/testing.md`; preserve honest group boundaries and keep
          Fast quick by optimizing or relocating measured heavy coverage without
          dropping assertions. Reconcile FullValidation discovery and runtime-expanded theory cases
          with completed and residual results, then adjust a lane budget only if
          timings and group composition justify it. Record the evidence and any
          runner/guard changes; an increased limit does not erase prior timeout
          evidence. This verification follow-up remains under GitHub issue #1536.
          Verified 2026-09-15: all 43 original methods/66 cases preserved as
          50 Fast + 16 Regression; focused 50/19 and independent review PASS.
          FullValidation default/cap 30 is documented from measured workload;
          guard RED/GREEN and six configuration bounds verified. Parent Fast
          20260915-190624-905-3964-1fe8a87bc8ce4235bddf9af2485c2779-fast
          passed 7868/7868 in 4:42.514, no timeout/duplicates, cleanup complete.
          Z0's separate Full coverage reconciles 1240 + 680 = 1920 passing executions
          with no cross-run overlap; the original 15m timeout remains unchanged.
        - [x] T081-B2C-J2-C2-V2-F [US3] Apply the owner's explicit follow-up
          to raise Fast's default and hard limit from 5 to 7 minutes after the
          verified 7868-case run took 4:42.514. Update runner, existing boundary
          guard and current documentation; preserve project/category boundaries,
          two-host ceiling, FullValidation 30 and Focused default 5/cap 15. Verify the
          affected guard and accepted/rejected configuration bounds without
          repeating the unchanged full Fast suite solely for the new deadline.
        - [ ] T081-B2C-J2-C2-V2-P [US3] At the next relevant FullValidation
          checkpoint, investigate measured fixture/manifest-check cost and the
          mixed-bin schedule based on discovery counts on one fixed checkout. Preserve
          collection isolation, exact coverage and Fast's quick feedback role;
          do not treat the 30-minute headroom as a performance fix. Evidence:
          `docs/testing.md` and
          `docs/superpowers/plans/2026-09-15-measured-fullvalidation-budget.md`.
          This follow-up does not block the next retained C2 implementation unit.
  - [ ] T081-C [US3] Implement strict source finalization, separate spiritual
    pending/receipt roots, exact cold reconstruction and narrow decision intake
    through `ValidationService.WoundMaterialization.cs`,
    `SpiritualWoundOpportunityAdapter.cs`, `GameEngine.ValidationAndRepair.cs`
    and `GameEngine.TurnLifecycle.cs`; first prove real explicit decline, not an
    empty/missing decision or test-authored command. Preserve the original turn,
    snapshot, dice, OD, progression and rollback identities.
  Bounded T080/T081-D/T087 projector prerequisite (2026-09-08): execute
  `docs/superpowers/plans/2026-09-08-spiritual-wound-contribution-projection.md`
  and its complete companion. Preserve exact source provenance and all legal
  nonterminal conflict states; ignore wounds of unambiguous nonparticipants,
  but reject malformed/unresolved/duplicate declared membership. This internal
  pure prerequisite does not complete wound consequences, live insertion,
  dependent suffix consumption, T080, T087 or US3. Parent owns the combined Fast.
  Pure prerequisite accepted2026-09-08 at30f0897c: semanticRED0/1, Focused35/35
  plus existing743/743, independent code review0C/I/M, parent Fast7849/7849
  in4:24.343/default5m. All four exact corrected postimages and28FastTRXs audited;
  no new GM-authored contract or live caller is exposed by this internal slice.
  - [ ] T081-D [US3] Complete lower/create/worsen/explicit older re-trauma,
    persistent actor effects and per-side seals in `WoundAcceptedTurnPlanner.cs`,
    `SpiritualWoundConflictContributionProjector.cs` and the lifecycle/seal tests.
    Include two current exchanges where the first wound changes applicable later
    mechanics, both simultaneous sides, exact replay and recent-history pruning.
    Approved FR-035A (2026-09-24): when the already-owned conflict wound is
    below a later guaranteed rank, worsen exactly to that rank within the hard
    maximum; when it already meets/exceeds the guarantee, derive client-owned
    `guarantee_satisfied` with exact `woundId`/`satisfiedSeverityRank`, null
    selected rank/transition ID, no GM decline and no duplicate transition.
    Extend the closed receipt parser, C2/C3 and cold replay with negative
    changed-source/wound/rank/instance controls and GM docs/examples/guards.
    Checkpoint 2026-09-25: bounded same-original-turn higher-cap `create` ->
    `worsen` path and owner duplicate-create guard are implemented and reviewed;
    signed C2/C3/common-plan, Focused, Fast and FullValidation evidence is in
    `plan.md`. Signed next-turn origin and exact worsening of the prior conflict
    wound are now implemented and independently reviewed (Focused 1/1;
    `plan.md` checkpoint 2026-09-25). T081-D remains open for negative
    signed-origin controls, FR-035A satisfaction and the
    remaining multi-source coverage.
    Optional capped `none` now has bounded code, signed C2/C3 integration,
    GM guidance/example/guard and independent XHigh review; see the
    2026-09-25 checkpoint in `plan.md`. Final post-correction controls are
    pending before this bounded slice is fully accepted.
    FR-035A has a bounded owner/C2/C3 implementation and worked GM example.
    Signed next-turn zero-calculated-maximum satisfaction, direct GM-decision
    rejection, mandatory C3 receipt with no insertion, positive common assembly
    and foreign-proof rejection passed Focused integration (6/6 C3 family,
    20260925-031036; details in `plan.md`). Five changed-source/wound/rank/side/
    instance negatives mutate saved original-draft images after a clean cold
    replay baseline; structurally valid changed checkpoints fail semantic replay
    (Focused 1/1, 20260925-034806). Current physical GM draft binding is C4's
    publication contract, not cold-replay authority. Documentation Focused
    132/132, Fast 8348/8348 and FullValidation 1923/1923 passed (details in
    `plan.md`). Keep T081-D open for final controls, historical rank
    across later worsening, dependent continuation and remaining source families.
    Both-side same-exchange checkpoint 2026-09-25: signed integration RED found
    the shared-coordinate uniqueness error in the live wound seal; the seal now
    keys by coordinate plus affected side. Focused GREEN 1/1, distinct per-side
    wound/transition IDs and common carrier agreement are recorded in `plan.md`.
    GM matrix/guide/worked example and guard were updated. Final owning Focused
    1/1, C3 family 3/3, docs 132/132, Fast 8348/8348, FullValidation
    1923/1923 and XML build passed. Independent Astra XHigh P2 corrected and
    confirmed closed: create/create example applies only before either side's
    first conflict wound. This bounded block is accepted; T081-D stays open.
    FR-035A stronger-wound checkpoint: same-turn automatic satisfaction now
    covers current II > guarantee I/source maximum I as well as equality;
    final witness assertions passed Focused 2/2 (20260925-043943), XML-enabled
    integration build exited 0 and independent Astra XHigh found no P1/P2.
    This test-only block is accepted. Historical rank after a subsequent
    worsening is covered by a signed three-exchange C2/C3 integration test:
    create I, automatic guaranteed-I satisfaction, worsen II. The middle
    receipt retains historical rank I, null transition/selection and the I/I
    source witness; the final plan seals. Focused Integration 1/1
    (20260925-044808), XML-enabled Integration build exit 0 with no changed-file
    warnings, and independent Astra XHigh review without P1/P2 or false-positive
    findings. This test-only block is accepted; T081-D remains open for
    dependent continuation and source families.
    Dependent-continuation C3 checkpoint: the existing signed private C2
    correction scenario now asserts the wound-induced guard cost of 3 on the
    later exchange, its exact resource transition, one same-ID wound receipt,
    and successful common plan. All seven variants passed Focused Integration
    (20260925-045231). Independent Astra XHigh review found that the common
    plan needed exact resource state/history assertions; these and receipt
    transition identity were added. Affected-row Focused 1/1 (20260925-045815),
    final XML-enabled Integration build exit 0 with no changed-file warning,
    and targeted reviewer confirmation closed the finding. This test-only
    block is accepted; T081-D remains open for mechanical consumers, negative
    origin and pruning controls; source-family breadth belongs to T081-E.
    Below-guarantee checkpoint 2026-09-26: signed create I then guaranteed II
    (maximum III) exposed and fixed original-source guarantee transport in the
    closed command parser. Only an exact, live owner-issued opportunity admits
    the serialized proof; unowned and altered claims reject. Final Focused 2/2
    (20260926-062036), companion owner controls 3/3 (20260926-061322), Fast
    8348/8348 (20260926-061527), XML-enabled build and independent Astra XHigh
    review passed. The test rejects none/I/III before valid II and verifies the
    same wound and final carrier/index/history. See `plan.md` for evidence and
    the internal-transport no-GM-documentation-update rationale. T081-D stays open.
    Foreign-origin controls 2026-09-26: the signed next-turn fixture rejects a
    structurally valid receipt naming a foreign wound or creation transition,
    with no offer or canonical carrier/index/history writes. Positive baseline
    plus both negatives passed Focused 3/3 (20260926-153538); XML build passed.
    Independent Astra XHigh review found no actionable issue. This bounded block
    is accepted; details and remaining origin controls are in `plan.md`.
    Missing registration/history now returns structured blocked results before
    private transport writes (Focused 5/5 split, C1/checkpoint 5/5, XML build,
    independent Astra XHigh). Fast was incomplete at 8216 passing results of
    8348 before its seven-minute bound; do not treat that run as green.
    The next bounded correction rejects lost/empty/relocated creation receipts
    using signed pre-turn wound evidence before C1 and normal/retrauma offers.
    RED/GREEN, remaining controls and independent review are recorded in
    `plan.md`. The shared Fast checkpoint passed 8348/8348; final terminal and
    malformed-UTF8 refinements passed their owning Focused selections, final
    XML build and FullValidation 1923/1923 (20260926-165803, 25:12 under 30m).
    Independent Astra XHigh has no remaining finding. These bounded corrections
    are accepted; the older incomplete Fast remains recorded as incomplete.
    T081-D is still open for the remaining mechanical, negative-origin and
    pruning controls; source-family breadth remains with T081-E.
  - [ ] T081-E [US3] Cover every required ordinary/special/guaranteed/start/terminal
    source family, danger/defeat/optional-dissipation behavior and truthful final
    preview through the existing T077-T092 owners and their worked GM examples.
    No lawful materialize/source may be excluded or left in permanent repair
    because only the decline milestone is implemented.
  Each unit includes its relevant GM prompt/docs/example/manifest/source guards
  with runtime changes and a complete-code execution plan before production edits.
  The architecture document remains planning evidence. The existing-mechanics
  signed publication/conflict fixture now proves zero errors, but no spiritual
  wound-source admission is implemented or accepted by it. Overflow remains an
  unconfirmed source risk, not a defect silently changed by this refactor.

### GREEN implementation

- [X] T082 [US3] Add `spiritual_resilience` tier 0-V to standard afterlife art/profile/bootstrap/progression authority without adding it to operation types in `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
  Accepted together with T100 on 2026-09-07 through `c5a8349d`; the complete
  ordinary-art scope, review and verification are recorded under T100 and in
  `docs/superpowers/plans/2026-09-07-standard-wound-arts.md`. This closes art
  registration/progression, not the spiritual wound source/decision workflow.
  Accepted 2026-09-07 clarification for T082/T093/T100: both new arts use the
  existing scalar integer tiers, ordinary player training/direct upgrades and
  persistent-entity progression. No per-art experience track or mixed schema.
  Fresh/current complete profiles explicitly contain both tier-zero entries;
  the persistent player profile mirrors soul state and never auto-upgrades itself.
- [ ] T083 [US3] Extend spiritual conflict start/state with declared danger mode, escalation evidence, per-side wound seals, and bounded defeat outcome in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
  Accepted declaration/persistence prerequisite, shared with T077/T085/T089-T092:
  `docs/superpowers/plans/2026-09-07-spiritual-danger-declaration.md` supplies
  complete code for strict start/canonical danger tokens, immutable ordinary
  exchange/replacement carry, terminal-proof retention and signed same-ID
  active/recent continuity across every occurrence. It includes 66 Fast reducer
  rows, 50 Integration response/file/snapshot rows, one GM guard, exact current
  fixture cutover and a manifest-backed persisted start example. Runtime947832f0
  and archive correction1576aa57 are independently final-state Spec compliant /
  Quality Approved. Parent checked actual diffs, archive bytes, Fast7698PASS in
  4:11.077/5m and exact reconciled Full coverage1859rows/1075methods. The original
  official FullValidation remains FAILED; its corrected reruns are separate
  evidence. Historical Integration RED50 was skipped, retained as a Minor process
  deviation, with post-implementation mutation31FAIL/19PASS and restored50PASS.
  The four traced Mortal source-fixture failures were subsequently repaired by
  the accepted T177 rollback-fixture checkpoint below. Full T083 stays open for
  accepted escalation, per-side wound seals and bounded defeat. No implicit mode, migration, art
  schema or healing authority is introduced by this prerequisite.
- [ ] T084 [US3] Implement trauma-pressure calculation, destination/mode/source caps, harmful-margin audit, one-per-side/re-trauma rules, and opportunity export in `BookOfEternityClient/Services/SpiritualWoundOpportunityAdapter.cs`
  The accepted T076-linked pure arithmetic prerequisite (`1969a695`) adds
  `BookOfEternityClient/Services/SpiritualWoundOpportunityMath.cs` without a runtime
  caller. A value calculation is not accepted source evidence. T084 remains open
  for the actual adapter, harmful-side provenance and all per-side/export rules.
- [ ] T085 [US3] Validate danger/opportunity/strain/art audit and reject GM-authored computed fields in `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs` and `BookOfEternityClient/Services/Validation/ValidationService.WoundMaterialization.cs`
  Bounded historical-classifier prerequisite:
  `docs/superpowers/plans/2026-09-07-spiritual-exchange-history-authority.md`
  replaces marker-alone exemption with same-conflict one-use snapshot evidence.
  Exact payload matching is first; old-marker optional readable top-level summary
  drift remains compatible only when all other members are exact. The existing
  resource publisher still demands the exact pre-turn prefix. Sixteen real
  Integration cases plus a source guard/worked example/manifest establish only
  this exchange-audit boundary. T089-T092 own the linked bounded GM synchronization
  and Focused/oneFast/conditionalFullValidation evidence. Source of Light's separate
  marker path, cross-exchange die ownership, full strain/actor/resilience authority,
  wound producer/seals and all broader T085 requirements remain open.
  Accepted classifier prerequisite (2026-09-07): commits `921feff0` plus
  `27f6804c`, original BASE `a8e551c7`, parent actual source/artifact checks and
  independent Spec Compliant / Quality Approved with zero remaining findings.
  Behavior16/16, neighboring37/37, final documentation122/122 PASS; one
  Fast7,630/7,630 in4:35.585/5m and conditional FullValidation1,857/1,857
  in10:09.537/15m, clean build/cleanup and exact discovery/theory reconciliation.
  The bounded plan records every RED/GREEN and correction artifact, including
  an initial pre-edit GREEN that is not counted as RED. T085/T089-T092/T177
  and #1536 remain open; the top-level77/177 count does not change.
  Accepted adjacent prerequisite:
  `docs/superpowers/plans/2026-09-07-light-incarnate-history-authority.md`
  removes Light Incarnate's independent marker-only/pooled-no-marker exemption.
  Active exchanges pass their existing accepted membership; recent resolutions
  consume their own exact pre-turn occurrences once. Validated no-dice baselines
  still require current authority. Twenty-three Integration cases plus GM
  API/daemon/turn/matrix/example/manifest/source-guard synchronization leave bonus,
  closure, offline compatibility and the resource publisher unchanged.
  Acceptance (2026-09-07): `cbbfa98f` plus `d9547c52`, independent Spec compliant /
  Task quality Approved, no Critical/Important findings. The final exact-source
  correction includes a mutation-sensitive recent-summary negative. Parent
  verified final owners54/54, docs123/123, oneFast7631/7631 in3:50.280/5m and
  oneFull1857/1857 in10:50.932/15m against actual artifacts and complete discovery.
  The bounded plan records RED/GREEN chronology, the temporary CS1026 build-only
  failure, command-form deviation, and two non-blocking Minor review items for
  final T177 review. This acceptance does not close T085/T089-T092/T177/#1536
  or change the top-level77/177 count.
- [ ] T086 [US3] Add danger, wound maximum, accepted wound/decline, and defeat consequence to player-safe conflict preview/audit in `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs`
- [ ] T087 [US3] Compose persistent profile wound transitions and typed owner-to-current-side `SpiritualWoundConflictContribution` evidence without duplicating wounds/effects or mutating `combatConditions[]`; fail closed on absent/duplicate/wrong-realm/ambiguous participant membership and clear only derived evidence on conflict close in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`, `BookOfEternityClient/Services/SpiritualWoundConflictContributionProjector.cs`, `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs`, and `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeSpiritualConflict.cs`
- [ ] T088 [US3] Implement mandatory bounded non-training defeat outcomes while preserving the existing separate optional soul-dissipation proof in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`

### GM contract synchronization

Bounded J2-B source-envelope documentation prerequisite (2026-09-08):
`docs/superpowers/plans/2026-09-08-spiritual-source-envelope-gm-sync.md`
and its complete companion own T089-T092's canonical source declaration guide,
entrypoint routing, worked special-art fragment, exact manifest coverage and
Fast/Integration documentation guards. The source rule is user-approved at
1cb6afd1; runtime parsing/acquisition belongs to T081-B2C-J2-B. This does not
close full source-action/terminal/pending contracts or any top-level story task.

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
  Accepted numerical prerequisite (2026-09-07), shared with T101, commit `4711d6a8`:
  `docs/superpowers/plans/2026-09-07-spiritual-healing-outcome-math.md` defines
  44 valid-value and 14 invalid-domain/overflow rows for an unused pure calculator.
  Parent source/artifact inspection and independent Spec Compliant / Quality
  Approved found zero open defects. Actual 44 semantic RED -> 44 GREEN ->
  14 invalid RED -> 58 GREEN; one Fast completed 6,266 of 7,446 discovery rows,
  with 6,265 PASS / one required unchanged legacy failure and arithmetic 1,180
  uncompleted, not full GREEN. All runs have clean build/cleanup, no timeout or
  duplicate artifacts within five minutes; exact evidence is in the linked plan. Tests
  must distinguish active I from healed I without creating severity 0, and valid
  insufficient tier from malformed data. No actual sealed-die/modifier/actor or
  accepted treatment authority is claimed; T094 remains open for integration.
- [ ] T095 [P] [US4] Add RED combat base-5/reduction/floor-2 spend, explicit operation-to-art mapping, counter/matchup legality, and rollback tests in `BookOfEternityClient.Tests/SpiritualHealingCombatActionTests.cs`
- [ ] T096 [P] [US4] Add RED one safe-cycle session, no OD, self no currency/item, one attempt per wound/cycle, active-treatment-then-natural-recovery ordering, failed-attempt world advance, and duplicate suppression tests in `BookOfEternityClient.Tests/SpiritualHealingSafeCycleTests.cs`
- [ ] T097 [P] [US4] Add RED `1+tier`, I=2/II=4/III=6/IV=8 thresholds, overflow, worsening reset, unsafe suppression, tier-0 20-cycle, and tier-V 4-cycle tests in `BookOfEternityClient.Tests/SpiritualWoundNaturalRecoveryTests.cs`
  Accepted numerical prerequisite (2026-09-07), shared with T105, commits
  `45beccd1` plus comment-only review fix `8aff3325`:
  `docs/superpowers/plans/2026-09-07-spiritual-natural-recovery-math.md` defines
  43 valid-value and nine invalid/overflow cases for one unused bounded calculator.
  Parent source/artifact inspection and independent Spec Compliant / Quality
  Approved found no remaining defects. Actual 43 semantic RED -> 43 GREEN ->
  nine invalid RED -> 52 GREEN. One five-minute Fast completed 6203 of 7498
  discovery rows: 6202 PASS / one unchanged mandatory legacy-source FAIL;
  arithmetic uncompleted difference 1295, not full GREEN. All new rows pass;
  build/cleanup clean, no timeout/duplicates. The helper preserves nonnegative-long
  progress, carries through every following step, and distinguishes healed I from
  active I. Actual safe/unsafe cycle authority, worsening reset, entity eligibility
  and the generic recovery follow-up heal boundary remain required integration.
- [ ] T098 [P] [US4] Add RED player/Guardian/resident/leader/radiant recovery, normal art progression, consent/reachability, and player-heals-entity tests in `BookOfEternityClient.IntegrationTests/AfterlifeWoundEntityRecoveryTests.cs`
- [ ] T099 [US4] Add RED combat heal and safe self/helper/entity heal accepted-plan/resource/scheduler/effect/history/output lifecycle tests in `BookOfEternityClient.IntegrationTests/AfterlifeWoundHealingLifecycleTests.cs`

### GREEN implementation

- [X] T100 [US4] Add `spiritual_healing` tier 0-V to player-soul and persistent actor standard art bootstrap, validation, progression, training offers, and Russian display in `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`, `BookOfEternityClient/Services/AfterlifeTrainingCostPolicy.cs`, and `BookOfEternityClient/Services/TrainingService.cs`
  Accepted together with T082 on 2026-09-07, complete task range
  `072d0cd7..c5a8349d` (registration `f94aeafb`, rank-plan correction `db6bcd92`,
  entity/presentation correction `fd502e8e`, player-writer correction `db8da630`,
  Training-read correction `c5a8349d`). Parent inspected source, archive deltas
  and raw verification evidence. Independent whole-task review plus correction
  re-review returned Spec Compliant / Quality Approved, zero remaining findings
  or evidence uncertainties. Both arts use scalar 0..5 tiers and existing costs,
  rank gates, player training/direct upgrades and entity progression; no art XP,
  migration or malformed-authority backfill. Final owning controls: Integration
  205/205, Fast 7753/7753 in 3:48.325/5m, FullValidation 1909/1909 in 11:19.817/15m,
  final documentation 125/125, Training follow-through 80/80 and post-copy 3/3.
  These final controls have clean builds/cleanup and no timeout or duplicate IDs.
  The linked concrete plan preserves the historical failed controls, setup-only
  bootstrap RED and direct-build deviation; later GREEN does not relabel them.
  Afterlife matrix, TaskGuide, glossary, worked examples, manifest and source
  guards are synchronized. Mortal mechanics and GM contracts did not change.
  T093 remains open for actual diagnosis-at-zero and insufficient-tier workflow;
  T101-T112 and #1536 remain open. Top-level accepted count is now 79/177;
  that unweighted task count is not a full-feature completion percentage.
- [ ] T101 [US4] Implement the single tier gate, roll authority, result bands, natural 1/20 precedence, complication modifier, and bounded transitions in `BookOfEternityClient/Services/SpiritualHealingResolver.cs`
  The accepted T094-linked prerequisite (`4711d6a8`) adds unused
  `BookOfEternityClient/Services/SpiritualHealingOutcomeMath.cs`. It returns
  recomputable arithmetic, not accepted healing authority. T101 remains open for
  the single actual resolver, exact target/art/modifier/die proofs, transitions
  and shared self/NPC/command/roleplay/service integration.
  Registration follow-through for T101/T102/T104 and command integration:
  `ExplorerAfterlifeCombatCommandResultBuilder` and
  `ExplorerMode.Afterlife.SpiritualConflict` currently say
  `Диагностика и лечение духовных ран пока недоступны; искусство уже можно развивать`
  and label the cost reference `Планируемое правило лечения`. These are
  provisional registration-only descriptions, not the finished healing UX.
  When the corresponding diagnosis/treatment paths become executable, update
  those descriptions and their assertions in `ExplorerModeCommandTests.Afterlife.cs`
  and `ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns.cs`, plus
  `ExplorerModeCommandTests.StandardWoundArtAuthority.cs` and
  `ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns.StandardWoundArtHelp.cs`,
  together with real handler/command coverage. Preserve the tier-zero diagnosis rule,
  mastered-tier treatment gate and combat/out-of-combat cost distinction;
  do not leave the unavailable wording in the completed feature.
- [ ] T102 [US4] Add a counterable healing operation and art mapping without making resilience active in `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- [ ] T103 [US4] Resolve combat healing cost through `AfterlifeActionCostRules` and typed spiritual-action-point outcomes in `BookOfEternityClient/Services/AfterlifeActionCostRules.cs` and `BookOfEternityClient/Services/AfterlifeSpiritualConflictResourceOutcome.cs`
- [ ] T104 [US4] Implement exact safe-cycle session/attempt identity, no-OD self/helper treatment, and world-cycle outcome composition in `BookOfEternityClient/Services/AfterlifeWoundHealingPlanner.cs`
- [ ] T105 [US4] Implement universal natural recovery reducer, overflow/reset, per-wound cycle seals, and player/entity eligibility in `BookOfEternityClient/Services/SpiritualWoundRecoveryPlanner.cs`
  The accepted T097-linked prerequisite (`45beccd1`, `8aff3325`) adds unused
  `BookOfEternityClient/Services/SpiritualWoundRecoveryMath.cs`, not an accepted
  cycle or transition producer. T105/T107 must preserve all carried progress and
  represent multi-step/follow-up healing with exact tick authority; the existing
  generic recovery predicate allows original I/II only. Do not cap/discard a valid
  III/IV result or reuse the unrelated Mortal-only treatment staging exception.
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
  Include the universal command after accompanying the player to the Shining Abode,
  death removing its availability, stale death/relocation offers rejecting before payment,
  travel or treatment, and bootstrap/normalization preserving accepted life/location state.
- [ ] T135 [P] [US6] Add RED every-faction visible primary `healing_support`, exact actor profile tier I-V, multiple healer, non-public visibility, and access requirement tests in `BookOfEternityClient.IntegrationTests/ShiningFactionHealingSupportTests.cs`
- [ ] T136 [US6] Add RED paid/negotiated Elyara and accessible/inaccessible Shining provider end-to-end command/world-cycle/rollback journeys in `BookOfEternityClient.IntegrationTests/AfterlifeHealingProviderLifecycleTests.cs`

### GREEN implementation

- [ ] T137 [US6] Implement strict healing service profiles, capability separation, quote/rounding, access, and compensation contracts in `BookOfEternityClient/Services/AfterlifeHealingServiceContract.cs`
- [ ] T138 [US6] Compose Ink Feather reservation/charge/receipt through its existing specialized currency/accounting authority, and accepted non-currency agreements through the same wound attempt, without admitting Ink Feathers to the unified resource ledger, in `BookOfEternityClient/Services/AfterlifeHealingServicePlanner.cs` and `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- [ ] T139 [US6] Add fixed tier-V healing capability, Lazaret location, public 100% service, and negotiated compensation metadata to `BookOfEternityClient/system_guardians/built_in/elyara/manifest.json` and `BookOfEternityClient/system_guardians/built_in/elyara/dossier.md`
  Treat the Lazaret as the initial location; metadata must not imply permanent presence or immortality.
- [ ] T140 [US6] Enforce Elyara discoverability/profile/service invariants during fresh game and subsequent validation/normalization in `BookOfEternityClient/Services/SystemGuardianLibraryService.cs` and `BookOfEternityClient/Services/Validation/ValidationService.GuardiansAndAfterlife.cs`
  Preserve canonical relocation and death; never respawn or duplicate her to satisfy service invariants.
- [ ] T141 [US6] Add visible primary `healing_support` to Shining resident role materialization and Russian display in `BookOfEternityClient/Services/ShiningAbodeState.cs`, `BookOfEternityClient/Services/GuardianAbodeResidentState.cs`, and `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs`
- [ ] T142 [US6] Validate every faction has at least one exact roster healer whose afterlife profile proves tier I-V while public access requires a separate service profile in `BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs` and `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- [ ] T143 [US6] Add access-aware self/helper/Elyara/Shining provider offers and sealed confirmation to the common player flow in `BookOfEternityClient/Services/WoundApplicationService.cs` and `BookOfEternityClient/Services/WoundPlayerProjection.cs`
  Resolve «Пойти к Элиаре за лечением» universally from her current canonical location and
  life state; omit it after death and revalidate stale offers before side effects in both UIs.

### GM contract synchronization

- [ ] T144 [P] [US6] Add RED Elyara/service price/compensation/Shining role/access contract, registry, manifest, and Russian terminology guards in `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.HealingProviders.cs` and `BookOfEternityClient.Tests/AfterlifeContractRegistryTests.cs`
- [ ] T145 [US6] Register/document the pending/service/profile/role authority in `BookOfEternityClient/Services/AfterlifeContractRegistry.cs`, `OtherGuides/Afterlife_Contract_Matrix.md`, `Rules/Block_32_Guardians.txt`, and `TaskGuides/CLI_Step_Main.txt`
- [ ] T146 [US6] Add paid Elyara, failed charged attempt, rolled-back no-charge, negotiated compensation, visible inaccessible Shining healer, and accessible faction healer examples in `Examples/E_CLI_Afterlife_Turns.txt` and `Examples/example_validation_manifest.json`
  Include relocated Elyara in the Shining Abode and unavailable treatment after her death.
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

- [X] T177-BROAD-OWNERSHIP [US8] Repair the actual 58/60 Integration boundary
  failure exposed during B0 via the complete-code plan and companion
  `docs/superpowers/plans/2026-09-08-wound-broad-validation-boundary.md`.
  Scope the catalog setup and failure-only severity diagnostic with exact
  named profiles; preserve the genuinely complete-state publication oracle.
  Only its four callers/eight rows gain method-level FullValidation, guarded
  by an exact caller/category manifest. Review eight calls rather than blindly
  accepting all ten; keep a ninth-call negative control. Preserve behavioral
  assertions and all existing test rows. Prove boundary60/60 and owner12/12,
  then independent review; parent owns the next combined Fast and conditional
  FullValidation. Test-only ownership maintenance has no GM/afterlife contract
  changes. This prerequisite does not complete T177, US3 or issue #1536.
  Accepted2026-09-08 at ecffabdc: actual boundary60/60 in1:11.219/default5m,
  exact owner12/12 in5:17.092/approved10m after identical5m timeout,
  source diff and all5 companion postimages inspected, independent review
  Spec compliant / Quality Approved0C/I/M, build0/0 and cleanup complete.
  No Fast/FullValidation result is claimed by this bounded acceptance.

- [X] T177-GM-PROCESS-OWNERSHIP [US8] Correct the real PowerShell proposal-dispatch
  suite's Fast ownership exposed by the combined B0/B1/GM checkpoint. Execute
  `docs/superpowers/plans/2026-09-08-gm-proposal-dispatch-process-boundary.md`
  and its literal companion: move all six unchanged Facts to Integration with
  ProcessIntegration, add both ownership entries, synchronize inventory68, and
  preserve worker/lane deadlines. Prove genuine path-ownership RED/GREEN1 and
  Integration7, exact moved source equality, independent review, then corrected
  combined Fast and the already-required GM-sensitive FullValidation. Test-only
  placement changes no Mortal/afterlife GM contract. Full T177 remains open.
  Accepted2026-09-09 at01cae542 plus combined checkpointab2dd976: exact source
  equality, ownershipRED0/1->GREEN1/1, Integration7/7, independent0C/I/M,
  Fast7870/7870 and existing GM FullValidation1920/1920. No timeout expansion.

- [X] T177-SNAPSHOT-WOUND-INVENTORY [US8] Reconcile the B0 closed original-path
  declaration with the exact wound source inventory after combined Fast
  `20260908-233632-202-19176-e7f4558435bf46fcba7c3cf466d7d5d2-fast`
  failed the real production discovery guard (6015/6016, no lane timeout).
  In `WoundMaterializationSourceGuardTests.cs`, add one accepted-mechanics owner
  for `PendingTurnSnapshotPathPresenceV1` and allow the player wound path only
  within its `ClosedLogicalPaths` initializer, before the `LogicalPaths` accessor.
  Keep every discovery token, scanner, assertion and existing allowance unchanged;
  no whole-file exclusion or runtime edit. Prove the existing complete guard class
  green, inspect the same failed/passing TestId, and obtain independent review
  before the corrected combined Fast and already-required GM FullValidation.
  This test-inventory correction changes no Mortal/afterlife GM contract.
  Accepted2026-09-09 atab2dd976: exact8-line inventory-only correction,
  unchanged complete guard body by inverse audit, owning5/5 with the same
  original failed TestId, independent0C/I/M, corrected Fast7870/7870 and
  conditional FullValidation1920/1920. Prior partial Fast remains FAILED.

T177 bounded fixture checkpoints (2026-09-07):

Accepted generic effect rollback correction:
`docs/superpowers/plans/2026-09-07-mortal-effect-rollback-fixtures.md` restores
the generic effect rollback cohort's canonical source fixture. Legacy wound
arrays could not supply the typed canonical source, so eleven Mortal cases
failed before their intended write probes. The core changes only
EffectMaterializationLifecycleTests.cs to the existing registered skill source,
preserving all4methods/14rows and assertions. An adjacent one-line predicate
assertion fixes xUnit2031 in DangerMode.cs without changing semantics.
Commits5235d413 and14d2dfd6 are independently Spec compliant / Quality Approved,
zero findings. Parent checked actual diffs, exact RED3/14 then GREEN14/14,
Fast7698/7698 in4:38.665/5m, and final clean12method/50row Integration control.
The plan records build-warning provenance, runtime theory expansion and all
artifacts. No production, GM surface, source fallback or actual wound-owned
binding is changed; full T177/T070/#1536 and top-level77/177 remain open.

Previously accepted bounded reflection correction:
`docs/superpowers/plans/2026-09-07-wound-cache-reflection-fixture.md` repairs only
the two ordinary-stage reflection calls that still pass two arguments to current
four-parameter registry methods. The catalog Fast's three failing rows stop at
method selection, not at their behavioral assertions. Preserve all assertions,
strict Invoke and production authority checks; reproduce the existing seven-fact
cohort, then run its owner class and one bounded Fast. Full T177 remains open.
Intermediate fixture commit `4170e01d` restores
five of seven affected behaviors; the other two now expose the same exact-arity
drift in RegisterEmptyMortalItems (six supplied versus ten current parameters).
The parent source-verified extension supplies empty routes/transfers and complete
null-valued projection-root maps in that same test file; production cache clones
both maps independently. No assertion or production authority change is allowed.
Accepted exact range `9f3e1455..7460800d`, fresh Spec Compliant / Quality Approved,
zero findings, with parent source/diff/actual artifact inspection. Fixture RED
1/7 then 5/7 is followed by behavioral GREEN 7/7 and owner GREEN 208/208. One Fast
`20260907-093816-867-32324-46e815b089614fbe80b9fb7d063d4cc0-fast` passes 7,580/7,580
in 3:13.691 within five minutes, zero build diagnostics/timeouts/duplicates, clean
cleanup. Its 7,526 discovery entries expand by 54 cases in seven runtime-enumerated
theories; parent method-level reconciliation leaves no unmatched cases. No gameplay
or GM surface changed; no prompt/example/manifest/source-guard update or
FullValidation is needed. Detailed five-run evidence is in the linked bounded plan.
This does not close T177, T070 or #1536; top-level completion stays 77/177.

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

### C2-PREFIX implementation slices — approved spec 2026-09-19 (#1536)

- [ ] T081-B2C-J2-C2-GEN [US3] Continue original C2/D against plan.md current-generation execution section: bind real retained sources/intervals to the owned effect draft, execute dependency-local wound barriers, prepare against distinct owned draft-before authority, apply real roots/lineage, register/retire instances on the same routing/use owner and consume the new generation in the next exchange. Source/frontier guards are internal steps only; completion requires real spiritual insertion/worsening/re-trauma and the architecture's thirteen domain-qualified scenarios plus eight-profile consumers, relevant focused controls, independent XHigh review and Fast. Under owner-approved option A (2026-09-19), reaction descendants/replacements/anchored consumption use supported physical wounds or general effects in the same engine; spiritual reaction graphs remain rejected. New wound root/source-epoch/descendant/retirement assertions require actual physical wounds inserted through the same draft path; generic effects cannot substitute. E cold reconstruction/common publication remains separate and mandatory before cutover (#1536).

- [x] T081-B2C-J2-C2-GEN-R-CUT [US3] Materialize the accepted physical-wound
  `apply_definition` replacement closure at the local worsening barrier in exact
  N/R/U order. Authenticate the current routing epoch and actual writer receipts,
  support consuming replacement targets through anchored insertion, retain unrelated
  reactions for the ordinary finalizer, route reaction-created descendants into a
  later worsening, and keep spiritual reaction graphs rejected. Accepted 2026-09-21:
  RED `002051` and `004315`; final Focused `010328` 4/4 plus regression `005953`
  10/10; Fast `070950` 7902/7902 in 3:47.236; independent XHigh review found no
  P1-P3. This closes only the bounded R-cut checkpoint; lifecycle frontier, remaining
  thirteen-scenario coverage, common completion/publication and E remain open.

- [x] T081-B2C-J2-C2-GEN-R-CASCADE [US3] Reconcile acceptance scenario 11 with
  the legal domain split approved under option A. The existing general-effect
  same-boundary golden proves two consuming activations, uses 2→1→0 and both
  replacements. The physical wound theory separately proves the permitted one-edge
  reaction, anchored consumption, teardown, retry and unrelated-work behavior. Do
  not construct the rejected physical same-boundary two-trigger replacement cascade:
  the wound contract requires at most one owned `apply_definition` edge and exactly
  one owning trigger per wound reaction component. Evidence: combined Focused
  `20260921-072414-304-13676-ab06d53e14064541bc73e1ac7a2e5b5f`
  passed 4/4; contract guard Focused `20260921-072618-572-22984-fe9b4b2903584ad0bcf88c18854df1d3`
  passed 1/1; independent XHigh review found no P1-P3 in this domain-qualified
  reconciliation.

- [x] T081-B2C-J2-C2-GEN-R-ANCHOR-RECEIPT [US3] Prove that the retained physical
  replacement and anchored-consumption receipts reject a detached identity image whose
  exact replace anchor changed. The real owner remains healthy: exact public retry,
  writer/allocation counts and all seven canonical roots stay unchanged. Focused
  `20260921-073751-298-24064-d0437d75d3db4b0b91a0aafc5f8f9fc3` passed 3/3 with
  a clean build; independent XHigh review found no P1-P3. This is receipt validation,
  not an outer-preflight corruption test.

- [x] T081-B2C-J2-C2-GEN-R-ANCHOR-PREFLIGHT [US3] During common completion/cold
  reconstruction, prove outer preflight rejects a changed owned replacement anchor
  atomically before new writes or allocations. Preserve unrelated work and do not
  broaden the one-edge wound graph or spiritual reactions. Accepted 2026-09-21:
  changed live replacement history is rejected before completion writes/allocations,
  revokes the unpublished capture and leaves all seven canonical roots unchanged.
  Focused `20260921-080922-029-19480-9a38f2238a8e4bdd92bb6404140c8da4`
  passed 3/3 with a clean build; the final common-completion controls and XHigh
  re-review below also cover the strengthened insertion-history preflight.

- [x] T081-B2C-J2-C2-GEN-COMPLETE-EFFECT [US3] Reconcile the complete accepted
  transcript with the retained trigger/reaction/insertion journal, execute only deferred
  ordinary operations, and run the global due-lifecycle and terminal folds exactly once.
  Produce one completed effect plan with current carrier, identity, source/root epoch and
  insertion-chain agreement; do not replay a journaled event or publish canonical files.
  Accepted 2026-09-21: completion revalidates the live insertion writer history and
  current carrier image, rebuilds current lineage, preserves canonical source-fingerprint
  semantics, skips journaled N/R work, executes deferred foreign terminal work and one
  due-lifecycle phase, and returns an exact-retry result without canonical publication.
  TDD REDs: missing completion `20260921-074344-114-20040-e047475928524c6bafebf2f905d7baf8`,
  wrong canonical fingerprint plus changed retirement `20260921-082252-846-29884-3ec170dd77f54204bb219d34a9e5fd00`,
  and changed create/carrier receipts `20260921-084308-418-32552-4d251d9559c94131becb2d3c4eb59f77`.
  Final Focused `20260921-085021-518-27820-7fc7d9ee6600478c85817958e20a990d`
  passed 11/11; ordinary draft regression `20260921-083719-394-22564-207d2e52f62b4a51bd9bbcb04a4cfdd1`
  passed 23/23; Fast `20260921-085419-103-33512-9c1af8dee9bb48d0805ab14246fc3190`
  passed 7902/7902 in 4:19.527 under the seven-minute limit. Independent XHigh review
  found four P2 and one P3 across two correction rounds; final re-review found no P1-P3.
  This is an unpublished internal completion surface, so no GM prompt, example, manifest
  or public contract update applies. Common canonical publication and E remain open.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-9 [US3] Close the remaining terminal-lifecycle
  half of architecture acceptance scenario 9 without inventing a spiritual reaction:
  prove an unrelated legal general-effect `suspend` survives the physical-wound local
  cut, is folded exactly once only at common completion, and keeps its accepted consume
  and terminal source events while the wound generation is worsened and the old wound
  generation is retired. Exercise both a retained two-use observer and a last-use observer;
  for the latter prove the exact `suspend -> expire` and `expire -> remove` identity order
  with accepted event references. Reconcile these integration rows with the existing
  ordinary terminal-sibling matrix; preserve exact retry, zero canonical publication,
  and unchanged allocation/write counts on retry. Run the smallest owning Focused
  selection and independent XHigh review before acceptance.
  Accepted 2026-09-21: the retained two-use rows preserve ordinary consume plus
  remove/suspend reaction evidence, while the last-use rows prove exact
  `suspend -> expire` and `expire -> remove` transition order with the activation
  event on expiry and reaction event on the terminal operation. Completion retry
  returns the same result without new identity writes or allocations. Initial
  coverage passed 4/4 in `20260921-091213-634-30368-67484477c52e427aaade147ad92ba660-focused`;
  the ordinary sibling matrix passed 3/3 in
  `20260921-091443-394-25480-abfd2e2b77e24f728cfd080269a403e0-focused`.
  XHigh review found the missing earlier-terminal branch and two assertion/doc gaps;
  the strengthened rows first failed 4/6 in
  `20260921-091919-498-34752-8fb4b12ec69e401b91892445e901ddbf-focused`,
  then passed 6/6 with a clean build in
  `20260921-092216-818-8656-7bd714aa63f345fda22464036f9cd909-focused`.
  Independent XHigh re-review found no remaining P1-P3. This adds only integration
  coverage for existing unpublished behavior; parent GEN/publication/E remain open.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-3 [US3] Close architecture acceptance scenario 3
  through the real spiritual original-turn owner: capture and execute multiple signed
  harmful exchange prefixes, explicitly decline every offered wound, and prove each
  unchanged decline is idempotent without initializing the effect draft or allocating
  identity state. Drain the one retained resource schedule and compare common draft
  completion with the unchanged ordinary finalizer for the exact final effect result
  and allocation stream. Preserve claimed-die/source chronology, zero forced wound
  materialization, zero canonical publication and unchanged canonical files. Run the
  smallest owning Focused selection and independent XHigh review before acceptance.
  Accepted 2026-09-21: two signed harmful exchanges produce two real opportunities;
  both declines are exact-retry idempotent and leave the draft uninitialized until
  the one common completion. A signed one-use general effect activates before the
  first decline and expires only in the global schedule. Paired deterministic
  factories prove the retained and ordinary finalizers have the same nonempty
  allocation order, IDs and final fingerprint; the owned history records exact
  `create -> expire` with the accepted activation event. The first empty-effect
  control passed 1/1 in
  `20260921-093242-833-35180-fb4c59f17e754b61bf876dc3c3aed2a3-focused`;
  XHigh review required a nonempty stream and XML documentation. The strengthened
  control passed 1/1 in
  `20260921-093902-840-7480-93cbd58ebe0d4e8fa5a7d1a63b36741a-focused`,
  and the related source/frontier/prefix family passed 8/8 in
  `20260921-094015-521-15544-4e8edd4617c1491db93fae04c4da499e-focused`.
  Independent XHigh re-review found no remaining P1-P3. No production or public
  contract changed; parent GEN/publication/E remain open.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-10 [US3] Reconcile architecture acceptance
  scenario 10 across its legal domains. In the physical-wound insertion fixture, retain
  the existing unrelated two-use lifetime control and add a real skill-owned reaction
  that releases both an incumbent refresh and a new reaction definition at the first
  resource event. Insert the wound before a later matching resource event in the same
  turn; prove the wound root reacts while the not-yet-materialized reaction shape does
  not enter current routing. Before common completion, preserve the producer lifetime,
  incumbent lifetime/history and absence of the new shape. During the one common global
  schedule, require the ordinary exact producer, refresh and newly-created child histories,
  including lifecycle consumption and event references. Reconcile with the ordinary draft
  refresh golden rather than inventing a spiritual reaction. Verify exact retry, zero
  canonical publication and no extra allocations or writes. Run the smallest owning
  Focused selections and independent XHigh review.
  Accepted 2026-09-21: a real skill-owned producer releases two deferred
  `apply_definition` reactions on the first spend. A physical wound is then inserted,
  and a later gain in the same turn activates the wound root without activating the
  newly defined reaction shape. Before completion, the producer and incumbent retain
  their original lifetimes and histories and the child remains absent. The common
  schedule records exact `create -> consume`, `create -> refresh -> consume` and
  `create -> consume` histories for the producer, incumbent and child, with the
  accepted reaction and lifecycle event references. Exact retry adds no allocations
  or writes and all canonical files remain byte-identical. The completed method matrix
  passed 6/6 with a clean XML build after the wording correction in
  `20260921-102256-605-4768-2d6a9c9a0bb243d19844e753493e792d-focused`;
  the ordinary refresh golden passed 4/4 in
  `20260921-101842-772-27224-a4a7ebdda14e431eb8eaa1c994ab7b63-focused`.
  Independent XHigh review found one causal P2 and two wording P3 across the review
  rounds; final re-review found no remaining P1-P3. This changes only integration
  coverage for existing unpublished behavior; parent GEN/publication/E remain open.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-6 [US3] Close architecture acceptance scenario 6
  with a real reaction-created child that becomes due in the ordinary global lifecycle
  phase. Extend the lawful `foreign_refresh` chain so the first spend releases the child
  definition, a physical wound is inserted, and the later gain activates the wound root
  without activating the absent child. Materialize that child only during common completion
  with one remaining owner turn, then require exact `create -> expire` history with distinct
  reaction-create and deterministic lifecycle-expiry event references, no surviving carrier,
  no same-event activation and no repeated bound-continuation or allocation/write on retry.
  Preserve the existing three-turn refresh row, zero canonical publication and byte-identical
  canonical files. Run the smallest owning Focused selection and independent XHigh review.
  This is integration evidence for existing unpublished behavior; it does not change a GM or
  public contract and does not close parent GEN, common publication, E or the cost decision.
  Accepted 2026-09-21 in the legal general-effect domain: the real deferred reaction child
  is absent through the later gain, is created only by common completion with one remaining
  owner turn, and expires in the single final-lifetime phase. Its exact identity history is
  `create -> expire`; the create transition uses the released reaction event and the expiry
  uses the distinct deterministic lifecycle event. It leaves no carrier, receives no
  same-turn activation, and exact retry preserves phase, allocation and write counts. The
  original three-turn row remains nonterminal. Final Focused
  `20260921-103802-170-30800-f649f006857f4828ac41cd1909708a7f-focused`
  passed 7/7 with a clean XML build, no timeout or duplicate IDs and complete cleanup.
  Independent XHigh review found one P3 in optional-parameter documentation; the corrected
  XML was re-reviewed clean. No production, GM-authored or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-5 [US3] Close the remaining causal gap in
  architecture acceptance scenario 5. In one original Mortal draft, insert a physical
  generation whose legal `apply_definition` reaction owns a consuming replace target,
  route both through a later accepted resource event, and materialize the replacement at
  the following worsening barrier. Prove the current wound source/producer/target binding,
  exact replaced and result identities, consume-before-replace anchor, one result create and
  one consumption allocation. Require successful common completion, exact materialization
  and completion retries without new allocations or writes, and byte-identical canonical
  roots. Reconcile with the already accepted published-prior-generation consuming control;
  do not broaden the one-edge physical reaction contract. Run the smallest owning Focused
  selection and independent XHigh review. This internal test slice does not change a GM or
  public contract and does not close parent GEN, common publication, E or the cost decision.
  Accepted 2026-09-21: a rank-III generation inserted in the active draft owns the legal
  reaction root and its two-use replacement target. The later gain accepts both activations,
  consumes the target from 2 to 1, and the rank-IV cut records the exact current wound source,
  producer, replaced and result identities. The trigger receipt inserts one consume before
  the retained replace anchor; the reaction receipt creates one result identity. Histories
  are exactly `create -> consume -> replace` and `create -> expire`. First common completion
  preserves both journaled images and their unique IDs, while materialization and completion
  retries add no writes or allocations. Final Focused
  `20260921-105358-464-36584-e9b909168ee742559fc33cb4269810d6-focused`
  passed 1/1 with a clean XML build; the published-prior-generation matrix
  `20260921-104937-655-35104-adc3bfe1d0944064be8d6759c81ccfc9-focused`
  passed 6/6. Three intermediate REDs corrected test assumptions about receipt ownership,
  replacement anchors and created-event identity. Independent XHigh review then found one
  P2 missing first-completion replay evidence; the strengthened re-review found no P1-P3.
  No production, GM-authored or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-OMITTED-JOURNAL [US3] Close the omitted-
  journal subcase of architecture acceptance scenario 12 on the accepted scenario-5
  contour. Run the same real rank-I publication, rank-II/rank-III insertion, later
  gain and consuming replacement at the rank-IV cut, then remove only the retained
  target-consumption receipt before common completion. Require deterministic rejection
  without a completed plan, no additional identity allocation or write, revoked capture,
  byte-identical canonical roots and a non-resumable repeated call. Preserve the positive
  row unchanged. Run the smallest owning Focused selection and independent XHigh review.
  This negative integration slice does not close the remaining scenario-12 tamper cases,
  parent GEN, common publication, E or the cost decision, and changes no GM/public contract.
  Accepted 2026-09-21: completion now distinguishes a genuinely deferred trigger from an
  already applied event whose retained cut receipt is missing, and rejects the latter during
  journal reconciliation before `RunCore`. The real consuming-replacement theory removes
  exactly the target receipt, receives `spiritual_wound_cut_agreement_mismatch`, produces no
  plan or new identity allocation/write, revokes the capture, rejects a repeated call and
  preserves all seven canonical roots byte-for-byte. The positive row remains successful.
  Test-only enumeration RED `20260921-110035-037-28864-a30d746491864b63a2a4fdeddeaa5bbc-focused`
  was corrected; behavior RED `20260921-110223-706-28328-c0863661a2994ef4a7e2b941cfea073f-focused`
  proved the old late after-image diagnostic but did not measure mutation counters. Final
  Focused `20260921-110518-955-31220-5c21db7f7d544055897a7fa7149981b4-focused`
  passed 2/2 with a clean XML build. The combined 23-row filter exceeded its five-minute
  boundary, so the unchanged scenario groups were rerun separately and passed 8/8, 6/6 and
  7/7 in `20260921-111254-028-14868-08d84103b0864e569619a46b0d6fcff7-focused`,
  `20260921-111556-017-31220-9931b8260d164d6496c717d27db8b226-focused` and
  `20260921-111804-210-6040-f71000aed32b4e849a04c623fbeecc4b-focused`.
  Fast `20260921-112002-022-25796-64b1dbab6cda42f5bd6b1739f467f23c-fast`
  passed 7902/7902 in 3:45.716 under the seven-minute limit. Independent XHigh review found
  no P1-P3. No prompt, example, manifest or public contract update applies to this internal
  unpublished guard; the other scenario-12 negatives remain open.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-7-SIGNED-ONLY [US3] Close the remaining
  negative half of architecture acceptance scenario 7. On both the real spiritual
  current-generation worsening contour and the physical current-generation consuming-
  replacement contour, pass the exact recomposed live selection back to the old ordinary
  `WoundAcceptedTurnPlanner.Prepare` entrypoint. Require rejection because its signed
  original wound/effect baselines cannot authenticate the newly inserted mutable current
  generation, while the owned draft-before path succeeds unchanged. Prove the probe does
  not mutate shared draft allocation/write counts or canonical roots. Run the smallest
  owning Focused selections and independent XHigh review. This is internal integration
  coverage; it changes no GM/public contract and does not close parent GEN, publication,
  E or the separate cost decision.
  Accepted 2026-09-21: the actual spiritual rank-I-to-II selection and the physical
  current-generation consuming-replacement rank-III-to-IV selection are each replayed
  through the old ordinary preparation entrypoint. Their signed original baselines cannot
  authenticate the recomposed current target, so both return no plan with
  `wound_plan_worsening_target_stale`; the shared draft allocation/write counts do not move.
  The owned draft-before route still materializes and routes each generation, and the
  owning contours preserve their existing canonical no-publication assertions. Physical
  Focused `20260921-112809-069-34824-2db2397dc2b74699a159f4b1b2b42740-focused`
  passed 2/2. Final spiritual Focused
  `20260921-113539-977-37004-f712b4d72b5942e09f2f4cc07eddae08-focused`
  passed 5/5 with a clean XML build. Independent XHigh review found no P1/P2 and two
  rounds of P3 XML precision issues; final re-review found no remaining P1-P3. Production
  did not change, so the immediately preceding 7902/7902 Fast checkpoint was not duplicated.
  No GM-facing prompt, example, manifest or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-8-TAMPER [US3] Close the two remaining
  adversarial assertions in architecture acceptance scenario 8 on the real spiritual
  same-turn re-trauma contour. After the rank-I insertion, retain the rank-II owned
  draft-before preparation and separately tamper its bound before fingerprint and its
  selected severity-generation predecessor root. Require both variants to fail before
  any shared carrier, identity, allocation or write mutation, while the existing lawful
  rank-II and rank-III generations keep distinct effect identities and current lineage.
  Preserve the existing positive rows and canonical no-publication assertions. Run the
  smallest owning Focused selection and independent XHigh review. This is internal
  integration coverage for the existing unpublished authority contract; it changes no
  GM/public contract and does not close parent GEN, common publication, E, the remaining
  scenario-12 negatives or the separate cost decision.
  Accepted 2026-09-21: two additional rows retain the actual rank-II draft-before and
  its successful preparation. One changes the registered before's fingerprint after
  preparation; the other changes the one non-null selected severity-generation
  `PriorRootEffectId` in a detached preparation while retaining the original claimed
  seal. Both reach owner application and reject with `wound_plan_prepared_seal_mismatch`
  before any shared allocation or identity write. The complete current wound carriers,
  wound identity/history, effect carriers and effect identity images remain unchanged,
  as do the canonical profile root and absent accepted publication. The five lawful
  rows remain green, including the existing rank-II/rank-III lineage controls. The first
  command selected the wrong Focused project and discovered no tests; the next compile
  RED corrected carrier comparison types. Final Focused
  `20260921-114647-614-23404-8f46b9af032a4767b4e9012a679bbe69-focused`
  passed 7/7 with a clean build, no timeout, duplicate IDs or cleanup failure.
  Independent XHigh review found no P1-P3. Production did not change, so the prior
  7902/7902 Fast checkpoint was not duplicated. No GM-facing artifact changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-1-LEGACY-GOLDEN [US3] Close the remaining
  equivalence evidence in architecture acceptance scenario 1 with the existing real
  `EffectResourceTriggerRoutingScaleTests` two-consuming same-boundary replacement
  fixture. Capture an independent OLD golden from historical commit
  `6837eab670d785019812481d289f8ff7d694c817`, before the draft-owner finalizer,
  using deterministic resource and effect identity factories. Bind complete canonical
  resource state/history, the complete accepted effect payload fingerprint, exact
  resource/effect allocation streams and factory call order. Compare the current
  ordinary adapter against that frozen golden while retaining the existing explicit
  O `consume -> consume -> replace`, replaced A and active B assertions. Preserve the
  fixture's exact null skill authority; do not invent another capability contour or
  compare two current adapter paths. Run the smallest owning Focused selection and
  independent XHigh review. Production and GM/public contracts must remain unchanged.
  Accepted 2026-09-21: the real historical fixture at commit
  `6837eab670d785019812481d289f8ff7d694c817` produced an independently captured
  4,447-byte OLD golden with SHA-256
  `0FCC418149BEEAD94B03EBF3C26213BD686CC8321C2A5ED9F15B386F816232D4`.
  Its complete canonical resource state/history, accepted effect payload and final-plan
  fingerprints, allocated effect/transition IDs, resource/effect factory call order,
  source/target authorities and exact null skill authority are frozen in the current
  ordinary-adapter test. The original O `consume -> consume -> replace`, replaced A and
  active B assertions remain explicit. Historical Focused
  `20260921-120257-033-32992-ce598f5a4dfc499280ebc4cb3dbd6682-focused`
  and final current Focused
  `20260921-121034-485-18448-e80ca73ee4644ddcbd16b3fc348657d1-focused`
  each passed 1/1; the current build was clean with no timeout, duplicate IDs or cleanup
  failure. Independent XHigh review verified the historical instrumentation and every
  frozen value, found one P3 unsupported `inheritdoc` use, and passed the corrected
  multiline XML with no remaining P1-P3. Production did not change, so the preceding
  7902/7902 Fast checkpoint was not duplicated. No GM-facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-WRONG-STAMP [US3] Close the wrong-original-
  stamp subcase of architecture acceptance scenario 12 on the real Mortal sequential
  same-turn worsening contour. After two accepted signed insertions, retain the complete
  resource transcript and replace only its private plan-authority stamp with an otherwise
  valid stamp carrying a different input fingerprint. Invoke the public retained capture
  completion path and require `effect_boundary_transcript_plan_mismatch`, no completed
  plan, unchanged effect allocations/writes/carrier edits/phases and current wound/effect
  images, revoked non-resumable capture, byte-identical seven canonical roots and no
  accepted publication. Preserve the positive row unchanged. Run the smallest owning
  Focused selection and independent XHigh review. This is internal coverage for existing
  authority behavior; it does not change a GM/public contract or close the other scenario-
  12 negatives, parent GEN, common publication, E or the separate cost decision.
  Accepted 2026-09-21: the additional mode executes the real original I->II->III
  signed contour, drains the capture-owned resource transcript and changes only the
  retained private authority stamp's valid-shaped input fingerprint. The other four
  authority fields and transcript fingerprint remain exact. Public capture completion
  rejects with `effect_boundary_transcript_plan_mismatch`, exports no plan, performs no
  allocation, identity write, carrier edit or phase, and preserves complete detached
  wound/effect images. It revokes the capture, rejects retry, preserves all seven canonical
  roots byte-for-byte and publishes no accepted plan. The positive rows remain green.
  Focused `20260921-122059-806-24328-1f66fb10d0ef44fd997cd44ea95aae4f-focused`
  passed 9/9 with a clean build, no timeout, duplicate IDs or cleanup failure. Independent
  XHigh review found no P1-P3. Production did not change, so the prior 7902/7902 Fast
  checkpoint was not duplicated. No GM-facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-CANDIDATE-TARGET [US3] Close the changed-
  candidate-target subcase of architecture acceptance scenario 12 on the accepted
  scenario-5 physical-reaction contour. After real I->II->III insertion and the third
  resource boundary, change only the target ID of the retained released reaction inside
  the actual closed-prefix image before the rank-IV cut. Preserve its activation, source,
  replacement target, event refs, original authority stamp and claimed prefix fingerprint.
  Require public rank-IV materialization to reject with the exact lineage diagnostic before
  allocations, identity writes, carrier edits, phases, wound/cut versions, journals or full
  current wound/effect images change; revoke capture, reject retry/completion, preserve all
  seven canonical roots and publish no plan. Keep positive and omitted-receipt rows. Run the
  smallest owning Focused selection and independent XHigh review. Production changes only
  if the test reproduces a defect. This does not close source-definition or skill-scope
  tampering, other scenario-12 negatives, parent GEN, common publication, E or cost rules.
  Accepted 2026-09-21: the three-row scenario-5 theory retains both existing positive and
  omitted-receipt contours and adds an actual closed-prefix target-only mutation. Public
  rank-IV materialization rejects with both required diagnostics before any mutable draft
  counter, version, journal or complete wound/effect image changes; the capture is revoked,
  retries and completion throw, all canonical roots remain byte-identical and no plan is
  published. Focused
  `20260921-124243-320-17556-49091e7b83b74a2897a3c2cc0e1179eb-focused` passed 3/3
  with a clean XML build and independent XHigh re-review PASS after its sole P3 documentation
  wording correction. Production, public and GM-facing contracts did not change; Fast was
  not repeated after the accepted 7902/7902 production checkpoint.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-CANDIDATE-SOURCE [US3] Close the changed-
  candidate-source-definition subcase of architecture acceptance scenario 12 on the same
  accepted scenario-5 physical-reaction contour. After real I->II->III insertion and the
  third resource boundary, change only the valid `resistance_modifier` value in the actual
  current source catalog entry for the released wound child, while preserving its source
  key, status, target, sealed wound-group definition, cached source fingerprint, original
  transcript authority and claimed prefix fingerprint. Require public rank-IV materialization
  to reject with exact wound-lineage source authority before allocations, writes, carrier
  edits, phases, wound/cut versions, journals or complete current images change; revoke the
  capture, reject retry/completion, preserve all canonical roots and publish no plan. Keep
  the positive, omitted-receipt and changed-target rows. Add a production integrity check
  only if the RED proves the current catalog can drift from its sealed group. Run the smallest
  owning Focused selection, Fast after any production correction, and independent XHigh
  review. This does not close skill-scope tampering, other scenario-12 negatives, parent GEN,
  common publication, E or cost rules.
  Accepted 2026-09-21: RED
  `20260921-125059-341-6784-ef904c76f99d4a32baea5611b45426b9-focused` proved that
  the changed actual current catalog entry was accepted and rank IV materialized. The
  lineage resolver now requires that resolved definition to equal its sealed group snapshot.
  Integration Focused
  `20260921-125331-908-33588-4ef644640a334928b0cbe9daec8d8290-focused` passed 4/4;
  final direct authority Focused
  `20260921-130118-329-10440-09137422960a42f1afcf90374e5feda1-focused` passed 13/13;
  Fast `20260921-125716-969-30572-15b5d899dc814a8f9df68e0a9e206637-fast` passed
  7903/7903 in 3:39.320. Independent XHigh review found one P3 missing XML contract;
  after the multiline API documentation correction re-review passed with no P1-P3. No GM-
  facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-PRIOR-SOURCE-BINDING [US3] Close the
  reused-prior-generation-source-binding subcase of architecture acceptance scenario 12
  on the accepted scenario-5 physical-reaction contour. In a dedicated positive/negative
  pair, give generations II and III the same legal `wound_reaction_child` canonical key
  and definition while retaining different real root identities and source-group epochs.
  After the accepted generation-III gain, inject only the genuine generation-II source
  catalog into the current lineage resolver. Require public rank-IV materialization to
  reject with `effect_reaction_wound_lineage_source_invalid` and
  `spiritual_wound_generation_required` before allocations, writes, carrier edits,
  phases, wound/cut versions, journals or complete current images change; revoke capture,
  reject retry/completion, preserve all seven canonical roots and publish no plan. Preserve
  the current catalog, routing preparation, insertion, candidate, definition JSON, stamps
  and fingerprints. Write the integration RED first, add only an exact source-group epoch
  agreement guard if the defect reproduces, run the smallest owning Focused selection,
  Fast after production correction, and independent XHigh review. This internal authority
  check changes no GM-facing or public contract and does not close the remaining skill-
  scope, ordering/composite epoch, broader omitted-journal, parent GEN, publication, E or
  cost work.
  Accepted 2026-09-21: the shared-key pair installs genuine generations II and III with
  deeply equal child definitions but distinct effect roots, application refs, source
  catalogs and source-group fingerprints. RED
  `20260921-131145-149-29428-ec1c3bbb69af416f899545b84e89446c-focused` proved the
  old resolver accepted the genuine generation-II catalog and materialized rank IV; its
  other failure was only the new positive fixture's expected dependency-cut count. The
  resolver now requires the selected catalog's source-group fingerprint to equal the group
  epoch sealed by the current producer lineage before resolving the downstream entry.
  Direct authority Focused
  `20260921-131925-782-29516-94c271863d6f450c91dbc42ab5a1a480-focused` passed 14/14;
  final integration Focused
  `20260921-132003-372-20424-3e8bf445749b437baaa555ccada86f72-focused` passed 6/6;
  Fast `20260921-132321-174-27000-7694bab838ff401db60db43522c5e967-fast` passed
  7904/7904 in 3:46.228 under the seven-minute limit. All builds were clean, with no
  timeout, duplicate IDs or cleanup failure. Independent XHigh review passed with no
  P1-P3 and confirmed the fingerprint is only an agreement check inside owner-held
  authority. No GM-facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-SKILL-SCOPE [US3] Close the changed-
  candidate-skill-scope subcase of architecture acceptance scenario 12 on the accepted
  scenario-5 physical-reaction contour. Add a real player skill and a legal generation-III
  skill-scoped `roll_modifier` replacement child, then prove its actual accepted release and
  consuming target before the rank-IV cut. In the negative row, change only that exact
  skill's current availability inside the actual retained plan authority while preserving
  authority identity, offered catalog, target, selector, source definition, candidate,
  cached fingerprint, original plan stamp and claimed prefix fingerprint. Require public
  rank-IV materialization to reject with `effect_roll_skill_scope_unavailable` at the exact
  reaction selector path plus `spiritual_wound_generation_required`, before allocations,
  writes, carrier edits, phases, wound/cut versions, journals or complete current images
  change; revoke capture, reject retry/completion, preserve all canonical roots including
  skills and publish no plan. Keep a paired scoped positive completion/retry row. Write the
  integration test first, change production only if the defect reproduces, run the owning
  Focused theory and independent XHigh review, and repeat Fast only if production changes or
  focused evidence warrants it. This internal test changes no GM-facing or public contract
  and does not close ordering/composite epoch, broader omitted-journal, parent GEN,
  publication, E or cost work.
  Accepted 2026-09-21: a paired real generation-III physical-wound reaction uses a full
  canonical player skill and a consuming focused `roll_modifier` child. The negative row
  changes only the exact retained current skill row from active to inactive after the actual
  release, preserving the authority object, offered catalog, source/target/selector,
  candidate, cached fingerprint and transcript stamp. Rank IV rejects with the exact scope
  and enclosing generation diagnostics before any mutable draft evidence changes, revokes
  the capture, rejects retries, preserves canonical files including skills and publishes no
  plan; the positive row completes and retries. An initial fixture-only failure in
  `20260921-133740-355-33404-37fcdc86aa7d41bdb85825078040c596-focused` corrected source
  inspection to the real key-resolved owner catalog. Focused
  `20260921-134207-539-27324-f0d4910d422a4384a62d59e300bd1919-focused` passed 8/8; XHigh
  review then identified the shortened skill fixture as noncanonical. After installing the
  complete lawful Active Skill Object without an observer/effect source, final Focused
  `20260921-134800-289-23932-c8a57ff4b89e4eecb0484dcc0aeb23da-focused` passed 8/8 in
  2:31 with a clean build and no timeout, duplicate IDs or cleanup failure. Independent
  XHigh re-review passed with no P1-P3. Production did not change, so the accepted 7904/7904
  Fast checkpoint was not duplicated. No GM-facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-ROUTING-EPOCH [US3] Close the mixed-head
  composite-routing-epoch subcase of architecture acceptance scenario 12 on the real
  sequential Mortal I->II->III worsening contour. Retain the genuine generation-II and
  generation-III insertion receipts, then inject the older receipt only into the draft and
  routing current-insertion pointers while preserving the genuine generation-III routing
  preparation, insertion dictionary, preparation cache, resource registrations, images,
  identities, journals, transcript and original plan authority. Require common completion
  to reject with `spiritual_wound_routing_required` before any plan, allocation, write,
  carrier edit, phase, journal or canonical-file change; revoke capture and reject retry.
  Bind a retained routing preparation to its exact insertion if the integration RED proves
  the mixed epoch passes the routing check, including when a later generic guard still
  rejects publication. Run the owning Focused theory, Fast after production correction, and
  independent XHigh review. This internal check changes no GM-facing or public contract and
  does not close arbitrary insertion-chain reordering, foreign-owner/version, broader
  omitted-journal, parent GEN, publication, E or cost work.
  Accepted 2026-09-21: the injected older draft/routing head preserved the genuine newer
  routing preparation. RED
  `20260921-135806-864-36324-9d5ed05f6c2d4e3cb1c98bc5a9f4d0f2-focused` passed 9/10:
  it returned no plan, but the diagnostic assertion stopped that run before the mutation
  checks; the mixed epoch passed routing ownership and failed only at the later generic
  after-image agreement. Routing preparations now retain and compare their exact insertion
  identity. Final Focused
  `20260921-140312-904-21992-6deef1c6544342258b403096b4d4ad3c-focused` passed 10/10
  in 4:37 and Fast `20260921-140805-629-5256-ceb4fc27ea7b405188715e04ce1ed968-fast`
  passed 7904/7904 in 3:52.118, with clean builds, no timeout, duplicate IDs or cleanup
  failure. The negative now fails early with `spiritual_wound_routing_required`, leaves all
  draft/canonical evidence unchanged, revokes capture and rejects retry. Independent XHigh
  review passed code/test with no P1-P3; its sole P3 documentation correction is reflected
  here. No GM-facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-INSERTION-ORDER [US3] Close the arbitrary
  insertion-chain reordering subcase of architecture acceptance scenario 12 on the real
  sequential Mortal I->II->III worsening contour. Preserve the genuine generation-III head
  and routing preparation, then change only generation II's predecessor from null to the
  generation-III insertion, forming a reversed cycle while every receipt, version, registry,
  preparation, image, journal, transcript and plan authority remains unchanged. Require
  common completion to reject with `spiritual_wound_insertion_agreement_mismatch` before any
  plan, allocation, write, carrier edit, phase, journal or canonical-file change; revoke
  capture and reject retry. Add an exact retained predecessor/version agreement only if RED
  proves the reordered chain completes. Use a seven-minute Focused override if the coherent
  eleven-row selection needs headroom beyond its measured 4:37 ten-row runtime, then run
  Fast after production correction and independent XHigh review. This internal check changes
  no GM-facing or public contract and does not close foreign-owner/version, broader omitted-
  journal, parent GEN, publication, E or cost work.
  Accepted 2026-09-21: the negative changes only the first real insertion's null predecessor
  to the later real insertion while preserving the genuine latest head/routing, creating a
  III->II->III cycle. RED
  `20260921-141740-150-35660-87e95c0393554db29e5e35b9dbfd7e69-focused` passed 10/11
  because common completion returned success. Completion agreement now requires adjacent
  before/insertion versions and the exact same-owner retained predecessor object, with only
  version zero allowed to omit it. Final Focused
  `20260921-142236-794-37144-eeec34e15caa4262a8a317cd35ddbbdf-focused` passed 11/11
  in 5:01.987 under the measured seven-minute override; Fast
  `20260921-142744-617-25412-c0236a03a96e43aa9e83b87b1ffeae30-fast` passed 7904/7904
  in 3:56.061. Builds were clean, with no timeout, duplicate IDs or cleanup failure. The
  negative now rejects with exact insertion agreement before any draft/canonical mutation,
  revokes capture and rejects retry. Independent XHigh review passed with no P1-P3. No GM-
  facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-FOREIGN-DRAFT [US3] Close the foreign draft
  owner/version subcase of architecture acceptance scenario 12 on the real sequential
  Mortal I->II->III contour. Create a foreign draft over the exact same accepted plan object,
  set only its wound-version scalar equal to generation III and prove its insertion registry
  is empty; then change only the genuine latest insertion receipt's owner to that foreign
  draft while preserving its before proof, predecessor, versions, original registry,
  routing/preparations, images, identities, journals and transcript. Require completion to
  reject with `spiritual_wound_insertion_agreement_mismatch` before any plan, draft mutation,
  canonical-file change or publication; revoke capture and reject retry. Change production
  only if exact plan/version equality bypasses current registry ownership. Run the owning
  Focused theory under the measured seven-minute budget, repeat Fast only after production
  changes, and obtain independent XHigh review. This internal check changes no GM-facing or
  public contract and does not close broader omitted-journal, parent GEN, publication, E or
  cost work.
  Accepted 2026-09-21: a second draft receives the exact same plan object and generation-III
  version but keeps an empty insertion registry; only the genuine latest insertion's owner
  is changed to it. Existing completion agreement rejects with exact insertion mismatch and
  preserves every draft/canonical counter and image, revokes capture and rejects retry.
  Focused `20260921-143541-957-27280-d36433ea2ed847babfab5985298f29eb-focused` passed
  12/12 in 4:49.518 under the measured seven-minute budget, with a clean build, no timeout,
  duplicate IDs or cleanup failure. Independent XHigh review passed with no P1-P3 and
  confirmed plan/version equality cannot mint registry ownership. No production changed;
  the immediately preceding Fast `20260921-142744-617-25412-c0236a03a96e43aa9e83b87b1ffeae30-fast`
  remains current at 7904/7904. No GM-facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIO-12-OMITTED-REACTION-JOURNAL [US3] Complete the
  remaining omitted-journal audit on the real scenario-5 consuming-replacement contour.
  Remove only the retained reaction-application receipt after the rank-IV local cut while
  preserving the transcript release, target-consumption trigger receipt, replacement
  anchors, images, insertion chain, routing and plan authority. Require completion to reject
  with `spiritual_wound_cut_agreement_mismatch` before a plan or any additional draft/
  canonical mutation, revoke capture and reject retry. Preserve the existing positive and
  omitted-trigger rows. Change production only if the already applied release is mistaken
  for pending work; run the owning Focused theory, independent XHigh review and Fast only
  after production changes. This internal check changes no GM-facing or public contract and
  does not close parent GEN, publication, E or cost work.
  Accepted 2026-09-21: the ninth scenario-5 row removes only the retained reaction-
  application receipt after the real generation-III release and rank-IV local cut while
  preserving the transcript release, target-consumption trigger receipt and its replacement
  anchors. Completion rejects with `spiritual_wound_cut_agreement_mismatch`, returns no plan,
  changes no allocation, write, workspace edit, phase, wound/cut version, remaining journal,
  complete draft image or canonical file, revokes capture and rejects retry. Focused
  `20260921-144446-880-35912-92a6f4490bed4a06825c3aa76b9c4efc-focused` passed 9/9
  in 3:41.645 with a clean build, no timeout, duplicate IDs or cleanup failure. Independent
  XHigh review passed with no P1-P3. Production did not change, so Fast was not repeated
  after the current 7904/7904 checkpoint
  `20260921-142744-617-25412-c0236a03a96e43aa9e83b87b1ffeae30-fast`. Together with
  the accepted omitted target-trigger receipt row, this closes the remaining scenario-12
  journal-omission audit. No GM-facing or public contract changed.

- [x] T081-B2C-J2-C2-GEN-SCENARIOS-2-4-13-AUDIT [US3] Audit the three remaining
  architecture acceptance scenarios that do not yet have an explicit GEN checkpoint.
  For scenario 2, map the ordinary event-outcome, after-component, remove-over-suspend,
  replacement-target drift and self-cascade failures to the draft owner, prove preflight-
  invalid batches allocate nothing, and prove an allocator exception leaves only an empty
  phase shell with no exported operation journal entry. For scenario 4, bind
  the real signed first spiritual insertion to its root/source/target provenance and actual
  next-exchange contribution while preserving the original turn, dice, ordinary-decision
  authority and accepted resource prefix. For scenario 13, prove detached receipt/source
  reads, no allocation from read/capture, once-only completion, dispose/failure rejection
  and final transcript plus insertion-chain agreement. Reuse existing executable tests where
  they already prove the exact claim; add only missing assertions or a narrow test row. Run
  separate owning Focused selections, obtain independent XHigh review, and repeat Fast only
  after production changes. This internal audit changes no GM-facing or public contract and
  does not resolve the four ambiguous profile consumers, cost decision, common publication
  or E cold reconstruction.
  Accepted 2026-09-21: the existing ordinary owner matrix passed 29/29 and retains the five
  required scenario-2 diagnostics; the draft failure controls prove zero allocations for
  preflight rejection and an empty exported operation journal after an allocator exception.
  The signed first-insertion controls passed 10/10, and the strengthened next-exchange
  hindrance provenance rows passed 2/2. The final draft alias/lifetime selection passed 3/3
  in `20260921-150749-485-1780-4ee6ce4e9dd44b52bc08d479690b4cf6-focused`,
  including allocation-free reads, detached receipt images and once-only completion.
  Independent XHigh re-review found no P1-P3. Together with the earlier accepted checkpoints,
  all thirteen domain-qualified architecture scenarios now have explicit executable evidence.
  Production did not change, so Fast `20260921-142744-617-25412-c0236a03a96e43aa9e83b87b1ffeae30-fast`
  remains the current 7904/7904 control. Parent GEN, profile consumers, cost decision, common
  publication and E remain open.

- [x] T081-C0-SPIRITUAL-EFFECT-COMPLETE [US3] Add the original spiritual capture's
  once-only unpublished completion boundary before durable finalization. Under the retained
  canonical lease, revalidate every original input; require a begun resource/effect owner,
  no unresolved resource or missing-side wait, all checked exchanges advanced, no pending
  source requirement or wound-routing integration, and one exact decline or materialization
  decision for every positive-ceiling source. Prepare one fresh uncommitted source continuation
  before first completion and cached retry; require the complete candidate to end at the exact
  closed exchange frontier with no prospective requirement or incomplete audit. Drain the owned
  resource schedule once and complete the same effect draft ordinarily when no insertion exists,
  or through its installed base-plus-insertion routing when one does. Exact
  retry returns the retained result; failed mutation revokes the capture. Prove the existing
  two-decline ordinary-equivalence row through this production entrypoint, add incomplete-
  decision and real inserted-root completion controls, and preserve zero canonical writes and
  absent accepted-plan authority. This internal prerequisite defines no pending/receipt schema,
  common publication or GM-facing surface; T081-C cold reconstruction/finalization remains open.
  Run separate owning Focused selections, one Fast because production changes, and independent
  XHigh review before acceptance.
  Accepted 2026-09-21: `CompleteEffectsAsync` now prepares a fresh owner/lease/revision-bound
  source ticket before both first completion and cached retry, rejects an unbound prefix,
  unexecuted full-candidate suffix, prospective requirement or incomplete audit, and drains only
  after every positive-ceiling source has its retained decision. No-insertion turns use ordinary
  draft completion; inserted wounds require the exact retained routing owner. RED
  `20260921-153440-170-25644-e9bcc2ea4827464e9daedf3ea74cba14-focused` proved prefix,
  suffix and cached-freshness bypasses, and RED
  `20260921-154746-235-34944-3d3bc4a09cf949fe922f325317e0f03e-focused` proved the first
  nullable-limit correction rejected the supported legacy capture. Final Integration Focused
  `20260921-155101-758-32304-885e00f5f49949ee8f3f94b75ce9f688-focused` passed 25/25;
  draft Focused `20260921-154515-022-26356-32909fc0a0274e46b59ddc5ded9daf1c-focused`
  passed 3/3; Fast `20260921-155354-934-34144-e4f3915f6ed44269bf0cf2d02e1b5f78-fast`
  passed 7904/7904 in 3:38.027 under the seven-minute limit. Builds were clean, with no timeout,
  duplicate IDs or cleanup failure. Independent XHigh re-review passed with no P1-P3 after the
  legacy and XML corrections. This is an internal unpublished boundary; no Mortal World or
  afterlife prompt, documentation, example, manifest or public contract changed. Durable pending
  state, cold reconstruction and common publication remain open under T081-C.

- [x] T081-C0-SPIRITUAL-ORDINARY-REDUCTION [US3] Seal the completed original
  spiritual resource/effect execution as the same non-publishable ordinary reduction
  consumed by common accepted-mechanics assembly. The retained resource owner must prove
  the exact captured input, successful terminal resource result, completed effect plan and
  final live pending-resource state; project every registered system outcome against that
  exact resource result, preserve owner companion after-images/transitions and same-turn
  definitions, and cache one immutable reduction after a fresh source-completion check.
  Do not call final plan assembly, register accepted-plan authority, define spiritual
  pending/receipt JSON, or write canonical state. Prove real explicit decline and one real
  materialized insertion both reach the reduction with exact resource/effect/conflict
  projections while canonical files and accepted-plan authority remain unchanged. Run the
  smallest owning Focused selection, one Fast because production changes, and independent
  XHigh review before acceptance. T081-C still owns durable decision evidence, live wound
  carrier assembly, cold reconstruction and the sole common publication.
  Accepted 2026-09-21: the resource session binds the exact original effect draft and source
  owner before execution; successful effect completion retains the exact final resource
  transcript. A fresh owner/lease/revision/frontier/resource/effect projection must be consumed
  once against that exact completed plan before sealing, so neither a foreign same-base draft and
  plan nor a stale genuine source projection can authorize the reduction. The reduction preserves
  final resource/effect/pending/definition/system-outcome and source-conflict projections, caches
  exact retry, and publishes no plan or canonical file. RED
  `20260921-164427-425-27268-cae1a351957748a5b154aacffa3dc804-focused` and
  `20260921-170119-806-37348-3830b851ddc04d8faed42c57f03bdccc-focused` proved the reviewed
  ownership and freshness holes. Final Focused passed 2/2
  (`20260921-171037-378-32648-042851d496a5470388bc8fd3bdb5a943-focused`), 3/3
  (`20260921-171243-653-36496-2b7113bd28c34122bab01c0be8fce879-focused`), unit 6/6
  (`20260921-171432-228-36844-97296c65c68c4f03aae618932ca3c825-focused`), completion 7/7
  (`20260921-171513-502-20476-b5887359e4d04ea08fe420c0d2015d61-focused`) and neighboring
  integration 26/26
  (`20260921-171619-083-31248-7959ae04abc644078d88dd0baa2d1cb6-focused`). Fast
  `20260921-172444-062-30508-15c4a12bc5db4728993f061169e741fb-fast` passed 7904/7904 in
  4:30.756 under the seven-minute limit, with clean build, no timeout, duplicate IDs or cleanup
  failure. Independent XHigh re-review passed with no P1-P3 after the two substantive corrections
  and XML correction. No GM-facing/public contract changed; durable decision evidence, cold
  reconstruction, final assembly and common publication remain open under T081-C.

- [x] T081-C1-DURABLE-STATE-KERNELS [US3] Implement the approved strict version-1
  `SpiritualWoundDecisionPendingState` and `SpiritualWoundOpportunityReceiptState` pure state
  kernels from `data-model.md` section 22. Require closed shapes, duplicate-property rejection,
  detached values, checked limits, canonical serialization, domain-separated recomputable
  fingerprints, monotonic pending cursor/staged decisions and append-only contiguous
  instance/closure/source/decision rows; terminal closure appends instead of mutating an instance.
  A receipt append must contain one source and one agreeing explicit `none|materialize` decision;
  decline has no wound/transition identity and materialize requires both. Register both paths in
  `AfterlifeContractRegistry` and
  `WoundAcceptedTurnSnapshotContract`; update bootstrap/path guards only where the existing
  topology owns client roots. In the same unit, register both client-owned paths in the afterlife
  contract matrix, `OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json`, validation
  manifest and documentation/source guards. Write failing contract
  tests first, then owning Focused tests, the required documentation Focused control,
  FullValidation, Fast, `git diff --check` and independent XHigh review. This unit writes no files
  and creates no GM continuation, wound command or accepted plan.
  Accepted 2026-09-22: strict kernels and private-root topology passed independent XHigh
  review (`t081c_plan_review`); final XML-enabled Fast8140/8140 in3:43.925
  (`20260922-175230-872-5748-c4520a8b1e3c4c0fad8da40a4613616f-fast`) and
  FullValidation1923/1923 in17:23.542, with owning Focused controls and clean diff check.
  Exact run evidence and resolved guard findings are recorded in plan.md. C2-C5 remain open.

- [ ] T081-C2-PENDING-RECONSTRUCTION [US3] Bind the live retained original-turn capture to one
  immutable pending spiritual packet, visibility-safe GM decision projection and bounded decision
  intake. Persist the original snapshot identity, retained source prefix, current cursor, exact
  staged decisions, dice/resource/effect/source evidence, registered before/candidate images and
  preserved draft without creating an accepted receipt. Under the same active canonical lease,
  this retained capture is the only unfinished-packet writer and may atomically replace only its
  own packet to advance generation/cursor, append decisions and validated sources, and update
  owner-validated candidate images, prefix fingerprints and permitted dependent draft fields.
  Preserve original before-images/dice and frozen sources/prior decisions. Only the common plan
  may consume the packet. Reconstruct from JSON in a fresh service
  instance by reopening the original pending snapshot and rerunning production origin validation;
  hashes remain comparisons only. Reject changed source, image, decision, snapshot, cursor or
  caller alias before any spend/write/notification/accepted-plan authority. Prove restart before a
  decision and after a staged decision, originally absent roots, exact replay and zero accepted
  side effects with TDD, owning Focused plus targeted RegressionIntegration, Fast and independent
  XHigh review. Update the afterlife turn guidance, daemon/launcher dispatch, a worked bounded
  continuation example, manifest and guards in this same unit without exposing private packet
  authority. Run the required focused documentation control and FullValidation before acceptance.
  Planning finding 2026-09-22: spec.md proposal C2-R1 addresses missing immutable original-draft
  retention and cold replay of ordinary random allocations/request timestamps. The proposed
  private capture checkpoint was approved by the user on 2026-09-22 and is not yet implemented.
  Synchronize its schema and persistence plan before dependent implementation. Current C1 schema
  remains unchanged.

- [x] T081-C2-R1-CHECKPOINT-SCHEMA [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Specify the closed client-owned capture-checkpoint schema and its checkpoint-first write,
  read-back, ambiguous-result and cold-recovery protocol in data-model.md, the live-turn
  contract and plan.md before implementing persistence. Define exact original draft images,
  separate excluded physical witnesses, ordered continuation input deltas, causal allocation
  journal boundaries, derived pending comparison and original before-image ownership. Keep C1
  pending schema and the approved C2-R1 game contract unchanged. Run a scoped Spec Kit
  consistency analysis and independent Astra XHigh design review; accept the documentation
  block only after findings and diff verification. Bound immutable physical witnesses to
  original snapshot manifest/authority/request; route signed service absence through observed
  optional selection and discard speculative owners after an uncommitted write. This task
  grants no runtime persistence.
  Accepted 2026-09-23: scoped Spec Kit consistency pass against the approved C2-R1 spec,
  C1 pending model, constitution and parent C2 task found no unresolved contract gap;
  scoped diff check clean. Independent Astra XHigh design review PASS after concrete
  recovery, mutable-control, per-step comparison and first-write findings were resolved.
  No runtime behavior or GM-authored contract changed; parent C2 remains open.

- [x] T081-C2-R1-CHECKPOINT-TOPOLOGY [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Register the private checkpoint path in afterlife contract ownership, snapshot presence
  observation and common wound before-image/publication agreement inventory. Keep exact signed
  present/absent optional selection under the existing reader limit; never expose this path in
  player status or GM repair. Update afterlife contract matrix, pending/control inventory,
  validation manifest and source/documentation guards with an honest reserved-path boundary.
  TDD for privacy, original signed absence/presence and rollback path coverage; owning Focused,
  required afterlife documentation Focused and FullValidation, XML build, scoped diff, then
  independent Astra XHigh review of the completed topology block. This registration alone
  creates no checkpoint file, parser, GM decision flow or accepted publication.
  Accepted 2026-09-23: path ownership, 18-row signed presence with 16-row legacy reads,
  observed optional service selection, accepted-plan path agreement, repair/status privacy,
  afterlife matrix/inventory/example/manifest and source guards are registered. TDD RED/GREEN
  covers exact signed absence/presence, legacy-map ordinary read, private status, rollback
  inventory and repair exclusions. Owning Focused 142/142 and integration Focused 20/20;
  additional focused presence and source-guard corrections 5/5 each. Required documentation
  Focused 131/131 (`20260923-201919-196-2692-e4a3cdec7d3245b987d2f4365d273d75-focused`),
  FullValidation 1923/1923 (`20260923-200057-632-2680-c44037e5bd17403d9e042768c44524db-fullvalidation`),
  XML-enabled integration build exit 0 with three pre-existing unrelated CS1587 warnings,
  and final Fast 8295/8295 in 4:01 under the seven-minute limit
  (`20260923-202805-100-33368-95bc9e31ee834e0e995dc6e5b58013d9-fast`).
  Scoped diff check clean. Independent Astra XHigh topology review PASS after findings,
  including a narrow follow-up on the two corrected guard tests. This task does not claim
  checkpoint persistence or completed parent C2 reconstruction.

- [x] T081-C2-R1-CHECKPOINT-CODEC [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Implement the closed version-1 private capture-checkpoint parser and canonical serializer
  from data-model.md section 23.2 as detached shape/comparison evidence only. Validate exact
  root/row fields, sorted complete original draft and immutable witness inventories, exact
  existed/absent byte images, safe canonical paths without case aliases, ordered advances,
  allocation boundaries and all domain fingerprints. Add a pure monotonic prefix-advance
  comparison that rejects changed frozen origin, prior decisions or journal prefix, rather
  than granting candidate authority. TDD malformed/duplicate/extra/aliased fields,
  missing and present-empty byte images, broken counts, reordered advances, changed suffix,
  alias mutation and digest mismatch. Use owning Focused, one Fast checkpoint, XML-enabled
  build, scoped diff check and independent Astra XHigh review. This task must not write the
  checkpoint file, reconstruct game owners or publish a pending/accepted packet.
  Accepted 2026-09-23 for the structural phase: exact closed wrapper/rows, complete
  registered image inventory, three immutable witness rows, byte/absence fingerprints,
  journal structure and internal step/count/digest agreement are checked. The detached
  serializer and one-step prefix comparison retain no caller alias. Owning Focused 26/26
  (`20260923-205103-209-2448-8c9d6907e6374aeb9d12f4ed676f6d53-focused`), XML-enabled
  integration build exit 0 with only three pre-existing unrelated CS1587 warnings, and
  Fast 8321/8321 in 4:33 under the seven-minute limit
  (`20260923-205216-475-27220-64d671d29b4a4640a127758950f9c850-fast`).
  Independent Astra XHigh review found one phase-boundary gap: the signed pre-turn snapshot
  cannot itself supply later GM draft exchange/source or owner-allocation bounds. The
  approved contract is unchanged; data-model section 23.2 and plan step 3 explicitly
  require real-owner bounds and exact per-step journal replay before any recovery, repair
  or advancement. T081-C2-R1-COLD-OWNER-BOUNDS tracks that deferred acceptance gate.
  Independent Astra XHigh follow-up PASS for this explicit phase split; scoped diff check
  clean, including manual trailing-whitespace inspection of the two untracked new files.
  No checkpoint persistence, cold recovery or accepted publication is claimed.

- [x] T081-C2-R1-COLD-OWNER-BOUNDS [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  In the C2 cold replayer, after structural checkpoint parsing but before recovery,
  repair or advancement, rederive original accepted exchange/source and ordinary input
  bounds from the real origin owners. Reject excess committed advances and decision
  suffixes, replay every real allocation request without a new fixed cap, and require
  the exact saved allocation cursor and rederived C1 packet at every step. Prove a
  self-consistently rehashed oversized checkpoint cannot mint authority. This is an
  explicit dependency of parent T081-C2-PENDING-RECONSTRUCTION, not a standalone
  authorization to recover or publish.
  Completed 2026-09-24 by auditing the already implemented owner replay and
  adding three structurally valid, self-rehashed adversarial controls. The
  first extra advance is rejected specifically at the real source frontier
  (`spiritual_c2_next_source_missing`) before repair writes; an extra decision
  fingerprint and an extra saved allocation tail also reject after full
  owner/prefix/cursor comparison. Focused passed 3/3
  (`20260924-081605-681-5484-2969f668880842ac8a284883aa2574a3-focused`);
  XML-enabled integration build exited 0 with three unrelated existing CS1587
  warnings; Fast passed 8337/8337 in 4:14
  (`20260924-081828-444-23544-7ce45cc5145c4ee7873be052812c2d64-fast`).
  Independent Astra XHigh audit found no missing production boundary: the real
  exchange inventory, one owner-produced decision per step, exact derived
  packet prefix and allocation cursor already enforce this limit without an
  arbitrary fixed cap. This test-only block changes no GM contract or example.

- [x] T081-C2-R1-JOURNAL-CURSOR [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to owner-bounded replay: expose the exact committed allocation cursor
  from the retained journal and current capture even while the full saved journal
  has unconsumed future rows. A read rejects an active speculation, faulted journal,
  stale capture or lease, and grants no checkpoint or gameplay authority. TDD a
  partial cold replay, failed speculative attempt and ordinary completed export;
  owning Focused, XML-enabled build, scoped diff and independent Astra XHigh review.
  Integrate this cursor into per-step checkpoint comparison under COLD-OWNER-BOUNDS;
  a cursor read alone does not authorize reconstruction, persistence or publication.
  Accepted 2026-09-23: healthy journal cursor rejects active speculation and faults,
  reports only committed replay requests with retained future rows, and leaves the
  full-prefix `Export()` gate intact. Capture exposes it through its current-owner,
  lease and gate checks; integration covers partial cold replay, busy and disposed
  capture. TDD RED missing API (`20260923-224553-607-19844-81edd7cf97b44b2b84f5bbe677924f17-focused`),
  unit GREEN 1/1 (`20260923-224622-241-29188-744a25d900ff48ecae4614743da46c43-focused`),
  final integration Focused 1/1 (`20260923-224826-513-32540-adb969b14da647089706e279a08344bb-focused`),
  XML-enabled integration build exit 0 with three pre-existing unrelated CS1587
  warnings, and Fast 8327/8327 in 4:03 under the seven-minute limit
  (`20260923-225001-501-30040-ab8fde0faa5043508227009a99e39004-fast`). Scoped
  diff and untracked trailing-whitespace checks clean; independent Astra XHigh review
  PASS. This private comparison API changes no GM-authored contract or example.

- [x] T081-C2-R1-C1-ORIGIN-INVENTORY [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to C1 first-offer production: expose from the exact named original
  capture a detached, immutable inventory of all originally admitted new exchange
  coordinates and the signed D20 pool, including unexecuted, harmless and zero-ceiling
  exchanges. First support the current same-active-conflict resource contour with an
  exact signed historical prefix; return an explicit unsupported/pending result for
  start, terminal and replacement contours rather than a guessed zero. Retain the
  full original A projection across source continuations and cold replay, without
  reading changed physical B or changing ordinary warm source append behavior.
  TDD original future bound, warm/cold parity, alias isolation and unsupported
  contours; owning Focused, XML build, one Fast checkpoint, scoped diff and independent
  Astra XHigh review. This evidence alone neither freezes warm C1 decisions nor
  produces a packet, checkpoint, recovery or accepted publication.
  Accepted 2026-09-23: the current source owner retains the initial full conflict
  projection across warm/cold continuation; its exact same-active historical prefix
  yields detached original new exchange IDs and signed D20 values under the current
  capture gate. Start, terminal and replacement contours return explicit pending,
  and malformed/duplicate rows fail closed. TDD RED missing projection API
  (`20260923-232601-431-36836-49cddc1763d747158308cf5c0cca14e8-focused`),
  final owning Focused 7/7
  (`20260923-232920-589-7472-8a3df07e38d14334962848bc06ce77c8-focused`),
  XML-enabled integration build exit 0 with three pre-existing unrelated CS1587
  warnings, and Fast 8327/8327 in 4:11 under the seven-minute limit
  (`20260923-233145-512-35624-fdffab8fb5324db48e64b6fd21f1d095-fast`).
  Scoped diff and trailing-whitespace checks clean; independent Astra XHigh
  review PASS after malformed-row and historical-ID findings were corrected.
  This private evidence API changes no GM-authored contract or example.

- [x] T081-C2-R1-C1-CLOSED-EXCHANGE-EVIDENCE [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to first-offer packet production: retain or export under the exact
  named capture a contiguous immutable ledger of every successfully closed
  source-owner accepted exchange. Join each owned resource/effect interval to its same-index
  validated source exchange JSON and all same-exchange source objects, including
  zero-ceiling sources and source-free exchanges. Derive exact original signed
  D20 claim coordinates from every executed exchange, not the undifferentiated
  claimed-die set. A wait creates no ledger row; both direct and resumed close
  paths append once. Reject stale owner, mismatched interval/order/ID, malformed
  claims and duplicate original die indices without guessing empty evidence.
  Preserve the latest-frontier requirement on actionable wound admissions.
  Keep ordinary warm append and legacy full-suffix capture valid; enforce the
  frozen original exchange bound only in the dependent C1 packet producer.
  TDD source-free/zero/eligible sequence, direct/resumed parity, one-use dice,
  alias isolation and owner rejection; owning Focused, one Fast, XML build,
  scoped diff and independent Astra XHigh review. This ledger neither produces
  a C1 packet nor publishes or writes any state.
  Evidence: source-owner indexed checked exchange and owned resource interval
  are retained after direct or resumed acceptance; read returns detached sources,
  signed per-exchange claims, and effect fingerprints. Warm append and legacy
  prevalidated suffix remain valid. Owning Focused 8/8
  (`20260924-001659-038-34060-e30e594c31944846a9389e5501fbe16d-focused`),
  warm/receipt regressions 9/9 plus corrected legacy 1/1; XML-enabled integration
  build exit 0 (three pre-existing CS1587 warnings), Fast 8327/8327 in 4:08
  (`20260924-001839-086-36708-1f37cc651566438087ecd004fafee26a-fast`),
  scoped whitespace checks clean; independent Astra XHigh correction review PASS.
  This private evidence surface changes no GM-authored prompt or example.

- [x] T081-C2-R1-C1-IMAGE-INVENTORY [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to first-offer production: retain an exact capture-owned registered
  path inventory and detached publication rollback images. Combine the frozen
  distributed draft inventory, real resource/effect/publication owner paths and
  required private C1/C2 roots; preserve owner-provided `_input.BeforeImages`
  rather than reconstructing them from disk or assuming they are signed pre-turn
  bytes. Fill missing required draft rollback paths from the frozen original draft and
  bind both private C1/C2 service roots to selected signed snapshot bytes or
  absence, including cold replay. Other missing physical private paths remain
  for the first-offer producer's full coverage check. Keep physical
  freshness witnesses separate from candidate output. Reject unregistered,
  case-confusable or unsupported paths and changed ownership. TDD with present,
  absent, dynamic, owner-override and alias cases; owning Focused, one Fast,
  XML build, scoped diff and independent Astra XHigh review. No packet or write.
  Evidence: named capture exports frozen draft/required paths with owner-provided
  publication images, required draft fallbacks and signed C1/C2 bytes or absence.
  Legacy capture lacks signed C1 service authority and fails only at this export.
  A cold checkpoint already present on disk does not become original rollback
  evidence. Focused 3/3 (`20260924-004353-424-18516-3953eb12f88a43038e735d47af7f3aa3-focused`),
  XML-enabled integration build exit 0 (three existing unrelated CS1587 warnings),
  Fast 8327/8327 in 4:05 (`20260924-004534-143-14024-69453a90c59a439984ee41b9fad443c2-fast`),
  scoped whitespace checks clean and independent Astra XHigh review PASS. The
  private evidence API changes no GM-authored prompt or example.

- [x] T081-C2-R1-C1-RESOURCE-PREFIX-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to first-offer candidate images: expose a read-only snapshot of
  the exact latest accepted closed spiritual resource prefix's current state
  and history from its owning live executor. Never call the destructive final
  `ResourceHistoryWorkingSet.Freeze` on an unfinished turn; use a pure validated
  snapshot over baseline plus pending transitions. Require exact current
  interval identity, no pending resource/missing-side wait, current capture and
  lease; return detached canonical images without spending, advancing, closing
  effect phases or changing work counters. TDD one accepted prefix and later
  accepted continuation, stale interval, wait, alias isolation and unchanged
  ability to continue; owning Focused, one Fast, XML build, scoped diff and
  independent Astra XHigh review. No packet, pending write or GM contract change.
  Evidence: current capture and exact latest interval export detached canonical
  resource state/history; an in-progress missing-side wait, prior interval and
  disposed capture are rejected. Repeated reads leave final-freeze and rebuild
  counters unchanged and a second exchange still executes. Owning Focused 2/2
  (`20260924-005549-315-7844-6f0854733b8f42579112926cdbb0f92b-focused`),
  XML-enabled integration build exit 0, Fast 8327/8327 in 4:23
  (`20260924-005846-824-34832-996de1e4681f4d2aac958fcf18d46f96-fast`),
  scoped whitespace checks clean and independent Astra XHigh review PASS.
  This private read API changes no GM-authored prompt or example.

- [x] T081-C2-R1-C1-CANDIDATE-OWNER-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to the first offer: compose a read-only detached candidate view
  from the exact latest closed source/resource/effect owners under one capture
  gate. The conflict must contain only executed exchanges; merge only the
  effect-owned combat conditions into its active conflict. Export effective
  resource definitions, state/history and transition evidence so registered
  companion outcomes and typed owner transitions can project against the same
  prefix. Compose shared wound/effect carriers using their established ownership
  rules. Reject duplicate or incompatible path writers and any incomplete
  projection without advancing or completing an owner. TDD executed-prefix,
  shared-carrier, companion/transition and rejection cases; owning Focused,
  one Fast, XML build, scoped diff and independent Astra XHigh review. This is
  a private producer dependency, not a GM-facing behavior change.
  Evidence: one current capture gate reads the latest executed source conflict,
  closed resource definitions/state/history/authority and transition evidence,
  ordinary companion outcomes, typed owner transitions, current effect/wound
  carriers and original output projections. Future exchange suffixes remain
  excluded; stale intervals and incompatible shared writers reject. A real
  wrapper companion regression failed before correction and passed after
  comparing the source-owned full original projection. Owning Focused executed
  prefix 1/1 (`20260924-011415-088-24448-02423a09f8454cb9b5bc27f3fe97774d-focused`),
  candidate integration 3/3 (`20260924-014744-652-15468-287dfec346b4413988338ccee95d51ca-focused`)
  and composer unit 5/5 (`20260924-014659-153-21896-935f69c324a84af3811d867fd32d7297-focused`)
  passed. XML-enabled integration build passed with three pre-existing CS1587
  warnings outside this block; Fast passed 8332/8332 in 4:03
  (`20260924-014948-716-6096-b297b1901be8401c954599abe2d9fef8-fast`).
  Scoped whitespace checks clean; independent Astra XHigh review PASS. This
  private view does not change GM prompts, examples or gameplay contracts.

- [x] T081-C2-R1-C1-SIGNED-RECEIPT-ORIGIN [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to first-offer instance binding: extend closed pending-snapshot
  presence to observe the spiritual opportunity receipt root, preserving exact
  legacy 16/18-key manifest reads and emitting a 19-key presence map for new
  snapshots. A named capture must retain exact signed receipt A bytes or signed
  absence separately from candidate/rollback B and reject unproved absence.
  Before offering, re-read the selected signed snapshot and compare retained A
  identity and bytes; never infer an empty ledger from uncovered B. TDD
  old/new manifest compatibility, covered and signed-absent receipt, A/B
  distinction, alias/tamper rejection and stale capture. Synchronize afterlife
  matrix, worked example/manifest and source guards for the signed presence
  contract. Owning Focused, required afterlife documentation Focused and
  conditional FullValidation, XML build, scoped diff and independent Astra
  XHigh review. This prerequisite grants no receipt publication authority.
  Accepted 2026-09-24: new signed snapshots emit 19 exact presence keys;
  strict reader retains 16/18-key compatibility and rejects unproved receipt
  absence. Named C1 capture retains and rechecks signed A receipt/conflict bytes
  and identity independently of physical B candidate/rollback bytes. Focused
  presence unit 5/5 (`20260924-020241-826-34788-99d0a1dca4ca4a60990945c81525a75c-focused`),
  presence integration 9/9 (`20260924-020328-897-36412-5862913c7e6a4e7e9ead62672670ffd7-focused`),
  signed-origin integration 2/2 (`20260924-021002-665-35872-521fd6c40f5745e9b339c15210d7d720-focused`),
  afterlife documentation guard 132/132 (`20260924-021155-510-21444-4ce001991d0d42dfbb56385745847419-focused`)
  and worked-example parse 1/1 (`20260924-021225-747-27728-3e1ab3538e484553a1897a438c3d43df-focused`) passed.
  Fast 8333/8333 in 4:02 (`20260924-021302-259-30356-956c7fc461fc442f8b2d17d39012c18d-fast`),
  required FullValidation 1923/1923 in 17:51
  (`20260924-021755-022-31240-8fe77afa523644a5bc03a8af51885bf3-fullvalidation`),
  XML-enabled integration build exit 0 and scoped whitespace/JSON checks passed.
  Independent Astra XHigh review PASS with no actionable defects. This private
  origin read does not publish receipts or alter GM commands; matrix, worked
  example, manifest, inventory and guard were synchronized.

- [x] T081-C2-R1-C1-FIRST-OFFER-PRODUCER [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Compose a detached, strictly parseable generation-1 C1 pending packet from the
  current named capture after an eligible executed same-active exchange. Freeze the
  original exchange/die/path inventory at the first offer; derive source and die
  witnesses, source/resource/effect fingerprints, exact registered rollback and
  candidate images, preserved response bytes, and the current source cursor from
  actual capture owners. Retain zero-ceiling evidence without a decision slot;
  return no packet when no eligible source exists. Reject stale/foreign source,
  changed A/B, duplicate claim, out-of-original exchange, alias mutation and
  unsupported start/terminal/replacement contour before any write or spend.
  Leave staged decision intake, checkpoint/pending persistence, cold replay and
  common accepted publication for later tasks. TDD a real eligible first offer,
  zero-ceiling/no-offer and negative ownership paths; owning Focused, one Fast,
  XML build, scoped diff and independent Astra XHigh review. This private
  producer changes no GM-facing contract until it is connected to the live wave.
  Accepted 2026-09-24: the named capture builds a detached strict generation-1 packet
  from source-owned closed exchange/die facts, signed receipt/conflict A, complete
  registered rollback/candidate B images and exact original response bytes. A
  zero-ceiling exchange yields no packet. Duplicate accepted request, confusable or
  invalid proposed instance history, already selected/declined source, changed
  signed manifest, changed registered warm B input and stale source frontier fail
  before publication. Focused unit 3/3
  (`20260924-032210-568-14784-21cae02863d94684bf8319eb2e6f480d-focused`),
  focused integration 4/4
  (`20260924-032426-759-37120-7b63b959b67c4e029280019e9ce6710a-focused`),
  neighboring integration 13/13
  (`20260924-031033-353-18348-bd40358944724c669ba4fff28283ccca-focused`),
  final Fast 8336/8336 in 4:36 under the seven-minute limit
  (`20260924-032723-377-26836-3571038c28e3493ba1c34aeeeaef2786-fast`).
  XML-enabled unit and integration builds passed; integration reported only three
  unrelated existing CS1587 warnings. Scoped whitespace checks passed. Independent
  Astra XHigh review found two substantive defects, both corrected; narrow rereview
  PASS. This private packet producer does not yet change a GM-authored command or
  afterlife runtime contract, so no prompt/example update is required in this block.

- [x] T081-C2-R1-CHECKPOINT-FIRST-DRAFT [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  From one current named capture and its exact first strict C1 packet, build a
  detached, strictly parseable initial C2-R1 checkpoint (`committedAdvance=0`).
  Retain every original draft image with exact absence/bytes, the three immutable
  physical authority witnesses, complete healthy original allocation journal,
  original request identity and derived packet fingerprint from actual owners.
  Reject a foreign/stale or changed packet, incomplete replay, changed A/B,
  unsafe/aliased inventory and a no-offer exchange. This is write-free comparison
  evidence only: no checkpoint/pending file, decision, recovery or accepted
  publication. TDD owning Focused and negative owner paths, XML build, one Fast
  checkpoint, scoped diff and independent Astra XHigh review. The private draft
  changes no GM-facing command or example.
  Evidence: real named-capture checkpoint and C1 packet round-trip, exact dynamic
  origin bytes and absence, zero advances, physical witness and journal counts;
  rejection of changed B, changed conflict bytes, zero-ceiling offer and a
  previously consumed missing-side continuation. The continuation defect was
  reproduced RED (`20260924-034634-998-36256-06cc037fe77d46b88f858f5b8b422487-focused`)
  and corrected GREEN (`20260924-034827-956-41596-bfceb026285946528a784ff616f421f7-focused`).
  Owning C1/C2 Focused passed 6/6 (`20260924-034945-368-40388-a4f19511dca34f46ae86ae0b6bbf4b26-focused`);
  Fast passed 8336/8336 in 4:21 under its seven-minute limit
  (`20260924-035205-852-33708-c43c3aa188f748008b63d4d13add46f2-fast`).
  XML-enabled integration build passed with 0 errors; it reported existing
  undocumented-public-member warnings outside this block. Independent Astra
  XHigh review found the prior-continuation defect; narrow rereview PASS after
  its correction. This detached draft does not yet alter a GM-facing command
  or active afterlife runtime contract, so no prompt/example update is needed.

- [x] T081-C2-R1-COLD-FIRST-PACKET [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Begin the existing COLD-OWNER-BOUNDS parent with write-free initial-packet
  replay. On a fresh validator and active lease, reopen a strictly parsed
  `committedAdvance=0` checkpoint through actual signed-origin and original
  location/item, resource, effect and source owners; advance only its frozen
  original exchanges to the first positive offer. Derive a new strict C1 packet
  from those owners and compare its packet fingerprint and exact initial journal
  cursor/count with checkpoint markers. Reject mismatched origin, extra or
  missing allocation, no offer, changed exchange/source bounds and a forged
  self-consistent packet marker. Do not read physical pending as input or write
  either private file. Cover real warm-to-cold positive and negative cases with
  owning Focused, XML build, one Fast control and independent Astra XHigh review.
  Later committed advances, pending repair and publication remain parent C2 work.
  Evidence: fresh named cold capture replays actual initial owners from checkpoint
  A, derives byte-identical strict C1 packet with changed physical conflict,
  matches initial allocation cursor/count, and writes neither private root.
  Self-consistently rehashed forged packet marker, extra saved allocation and
  missing saved allocation reject. The last case reproduced an uncaught journal
  exhaustion exception RED (`20260924-040741-500-36928-aae52cc127a94e4d85809c49fbfcc16e-focused`),
  then returned a fail-closed issue GREEN (`20260924-040852-078-30924-38c3becb84a941c88cefc79b14738659-focused`).
  Owning C1/C2 Focused passed 10/10 (`20260924-040950-640-25596-dbdb692a3c204f369db11ff30c51b068-focused`);
  final Fast passed 8336/8336 in 4:05 under seven minutes
  (`20260924-041255-493-44512-011c3707aca1491bb0c85f605a185421-fast`).
  XML-enabled integration build passed with only three unrelated existing
  CS1587 warnings; scoped whitespace checks passed. Independent Astra XHigh
  review found the exception gap and accepted the correction on narrow rereview.
  This private, write-free replay changes no GM-authored command or active
  afterlife contract, so no prompt/example update is needed in this block.

- [x] T081-C2-R1-INITIAL-TRANSPORT [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Give the current named capture the only initial unfinished transport writer.
  Under one gate and canonical lease, derive the same strict first C1 packet and
  initial checkpoint from real owners, require exact signed before-images for
  both private service paths (absence or empty root), and refuse a foreign,
  stale or already-active root. Atomically write checkpoint first; classify
  read-back as exact old, exact new or third image even if the write throws.
  Only exact new permits a derived pending write and exact read-back. Exact old
  discards this attempt; third image blocks it. If pending fails after checkpoint
  commit, retain the checkpoint and require replay repair before new decisions.
  This private writer is not yet bound to GM dispatch or accepted publication.
  Cover success, initial absence/present-empty, prewrite failure, ambiguous
  write outcomes, pending failure, mismatched baselines and no duplicate owner
  allocations with injected transport failures. Use owning Focused, one Fast,
  XML build, scoped diff and independent Astra XHigh review. Full cold repair,
  committed advances and GM-facing docs/examples remain parent C2 work.
  Evidence: current named capture writes exact owner-derived initial checkpoint
  then pending under one gate/lease, with signed absent or present-empty
  before-images and byte-exact read-back. Fault injection proves old/new/third
  checkpoint outcomes, pending failure, write-then-throw and revocation after
  checkpoint commit; repair-required cases replay the same packet and allocation
  cursor from committed checkpoint. Changed private baseline and zero-ceiling
  offer write nothing. Independent Astra XHigh review found two P2 gaps:
  revocation during the final awaited baseline read could still commit both
  roots (RED `20260924-043358-690-17004-05a03b730164418ea947b8d4ecedd76d-focused`)
  and ordinary BOM-bearing signed empty roots were rejected (RED
  `20260924-043507-888-29008-30d440ea99214a58924d9b7d1222a2a8-focused`).
  Both corrected and narrow rereview PASS; focused regressions 2/2 GREEN
  (`20260924-043630-461-45048-c0ddd61764b64d3b8156a74d1c923b94-focused`).
  Owning C1/C2 Focused passed 21/21
  (`20260924-044130-379-44388-a3b229103af147269227d1cea766416f-focused`);
  Fast passed 8336/8336 in 4:43 under seven minutes
  (`20260924-044552-626-42780-457ed7cf7d2846f9a6dbacf1f7ef937c-fast`).
  XML-enabled integration build passed with three unrelated existing CS1587
  warnings; scoped whitespace checks passed. The reserved private path and
  example were already documented; this unbound writer adds no GM command or
  visible afterlife lifecycle, so live GM guidance awaits runtime integration.

- [x] T081-C2-R1-INITIAL-PENDING-CLASSIFIER [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Under one canonical lease, read the physical first checkpoint and pending.
  The checkpoint supplies replay inputs but no independent authority; pending
  is comparison-only. Parse the checkpoint with its
  duplicate-safe frozen draft inventory, reopen signed A and immutable
  witnesses, replay actual original owners to the first C1 packet and exact
  allocation cursor, then classify physical pending as structural match,
  missing/stale projection requiring repair, or blocked corruption. A pending
  without a checkpoint cannot create an origin. Expose no GM decision or
  accepted publication authority; this is read-only recovery evidence.
  Cover real restart after both writes and the checkpoint-only gap, forged
  self-consistent pending, missing checkpoint, changed signed origin and
  malformed/aliased inventory with owning Focused, one Fast checkpoint,
  XML build, scoped diff and independent Astra XHigh review. Actual pending
  repair write and saved decision advances remain separate parent C2 work.
  RED build failed for the missing classifier API (`20260924-045422`); final owning
  Focused passed 7/7 (`20260924-050518-248-36224-4718ff9fc2fc475ba87c6ec3bb96664b-focused`).
  Independent Astra XHigh review caught present-empty pending and replay I/O
  failure handling; both were corrected and the reviewer rechecked PASS.
  Final Fast passed 8336/8336 in 4:17
  (`20260924-050832-573-33832-087204447bf44eab84b2d75090c9d7a3-fast`).
  XML-enabled integration build exited 0. The private read-only classifier
  changes no GM-visible contract, command, example or accepted publication.

- [x] T081-C2-R1-INITIAL-PENDING-REPAIR [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Under one canonical lease, classify the physical first checkpoint and pending
  through the cold original owners. Only a repair_required classification may
  atomically replace pending with the exact rederived packet. Recheck the exact
  checkpoint and pending before write, read back the pending after every write
  outcome, and never rewrite the checkpoint or replay allocations as fresh
  issuance. Preserve present-empty and absent baseline distinctions. A changed
  authority root, revoked owner, unknown write outcome, or readback mismatch
  blocks further decisions. Exact replay of an already matched pair is a
  no-write success. Cover missing, malformed and stale pending, old/new/third
  atomic write outcomes, concurrent physical change and revocation with RED/GREEN
  integration tests, Focused/Fast/XML controls and independent Astra XHigh review.
  Runtime GM dispatch and saved decision advances remain separate C2 tasks.
  RED missing API (`20260924-051400`), signed request mutation
  (`20260924-052020`), unusable stale returned owner (`20260924-052202`),
  and post-readback changed replay baseline (`20260924-053646`) were reproduced.
  Final combined C2 pending Focused passed 17/17
  (`20260924-053858-618-14908-f059ceecfdb34e61ad83060ab1b36da6-focused`);
  Fast passed 8336/8336 in 4:14
  (`20260924-054358-455-40428-b097d4990f9b4325a13bfa5d9a328a68-fast`).
  XML-enabled integration build exited 0 with three unrelated existing CS1587
  warnings. Independent Astra XHigh code review rechecked all corrections PASS.
  Private repair changes no GM-visible command or accepted contract, so the
  already documented private path needs no further GM-facing update here.

- [x] T081-C2-R1-SAVED-DECISION-REPLAY [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  From an exact committed C2 checkpoint/pending pair, accept one actual next
  positive-source `none` or `materialize` decision through the retained source,
  wound, resource and effect owners. Freeze and validate only permitted dependent
  decision/prose and conflict input changes against immutable original actions,
  actors, dice, sibling draft paths, earlier sources/decisions and before-images.
  Derive the successor C1 packet and one appended checkpoint advance in memory,
  including exact new decision fingerprints and allocation count. Reopen the
  original signed snapshot on a fresh service, strictly replay the initial and
  saved step through actual owners and prove the same packet, images, decisions,
  ID/time journal cursor and bounded pending fingerprint. Keep replay-only
  allocations closed until every retained boundary is proved. Cover both
  decision kinds, same-exchange second source, dependent next exchange, changed
  original action/actor/die, altered saved input/decision/allocation rows and
  later uncommitted physical GM edits with RED/GREEN focused integration tests,
  one Fast control, XML build and independent Astra XHigh review. No filesystem
  writes, GM dispatch or accepted publication in this block; those follow only
  after the owner-derived and replayed transition is established.
  Completed 2026-09-24: an exact matched pair stages owner-derived `none` and
  `materialize` decisions, retains the second positive source of the same
  exchange, and validates dependent next-exchange resource corrections after
  wound routing. A fresh service replays two saved decisions in order against
  the signed origin even when physical GM inputs changed later or pending is
  missing. Rehashed input/decision/allocation evidence, invalid UTF-8/prose,
  original action/actor/die/sibling edits, and changed classifier-origin bytes
  reject. The owner-composed conflict candidate remains separate from raw
  correction input. Focused controls passed for saved decisions/cold replay
  (13/13), independent input mutations (4/4), dependent exchange (1/1),
  two-source staging (1/1), two-step replay (1/1), advanced classification
  (3/3), initial classification (7/7), and ABA-review corrections (5/5).
  XML-enabled integration build exited 0 with three pre-existing unrelated
  CS1587 warnings. Fast passed 8337/8337 in 4:13 under its 7-minute limit
  (`20260924-073104-338-28028-b259a1caf62543e189c4a9d3f784ed65-fast`).
  Independent Astra XHigh reviewers found and rechecked corrections for saved
  input/encoding validation, two candidate-image dataflow defects, and a
  classifier-origin race. Generalized classification is read-only here;
  transport, repair, GM dispatch and common publication remain separate work.
  No GM-visible command or accepted contract changed in this write-free block,
  so no prompt/example/manifest update is required yet.

- [x] T081-C2-R1-SAVED-TRANSPORT-REPAIR [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Persist one owner-derived saved decision under the canonical lease and capture
  gate. Freeze the exact matched checkpoint/pending generation and proposed GM
  inputs; derive and validate the successor in memory, then atomically replace
  checkpoint and verify exact read-back before writing the derived pending.
  Reconcile ambiguous first-write outcomes only as exact old/new bytes, reject
  any other state, and repair missing/stale pending solely from strict cold
  replay of the latest committed checkpoint. Do not issue another decision or
  publish canonical state while projection repair is required. Cover write
  failures, changed roots and restart classification with focused RED/GREEN
  tests, XML build, one Fast control and independent Astra XHigh review.
  Completed 2026-09-24: a retained matched owner derives and saves the next
  decision under one gate and lease. Exact old/new/third checkpoint outcomes
  determine whether the step committed; pending replacement follows only a
  confirmed checkpoint and can be repaired from full signed-origin replay.
  The postflight probe checks all immutable witnesses, the owner-backed source
  continuation and the exact new pair. The prior first-pending repair API
  remains initial-only; the generalized API also handles saved checkpoints.
  RED reproduced missing APIs and an independent-review P1 where a changed
  turn request still yielded `committed`; the overbroad first correction also
  failed the happy path because it expected the old private-root bytes. Both
  were corrected with exact successor overrides for only the two service roots.
  Focused saved transport/repair passed 9/9
  (`20260924-080312-951-22724-128b3b16077241e5a97a817112a2e571-focused`);
  actual dependent materialization/next-exchange transport and second-source
  saved transport/cold replay passed 1/1 each (`20260924-080056-010-44028-2c8cef5c7b58490691873e5ecacfff54-focused`,
  `20260924-080207-432-35488-1c0a630791de4d1bbab772defa5e2ba2-focused`).
  Combined regression passed 17/17 with new generalized code (10 initial
  repair and 7 saved transport/repair controls)
  (`20260924-074748-302-42972-7c5cc309ff2040df89826b9e60f90a46-focused`).
  XML-enabled integration build exited 0 with three pre-existing unrelated
  CS1587 warnings; Fast passed 8337/8337 in 4:26 under its 7-minute limit
  (`20260924-080708-079-27996-0605a8ead46e4b78bbac3fb91550b70a-fast`).
  Independent Astra XHigh review rechecked the P1 and affected dependencies
  PASS. This is private transport/recovery only; no GM command, visible flow,
  example or accepted contract changed. Parent C2 dispatch, GM synchronization,
  cold-owner bounds and common publication remain open.

- [x] T081-C2-R1-DECISION-CONTINUATION [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Before saving the final positive decision of a closed exchange, use the retained
  owners to advance the original exchange suffix until the next positive offer or
  verified complete frontier. Apply at most the current bounded dependent conflict
  correction; traverse zero-ceiling exchanges without committing a stranded cursor.
  Reject missing or invalid dependent work before checkpoint transport so the same
  decision can be retried from the prior matching pair. Cover unchanged, corrected,
  zero-ceiling and terminal continuations, cold replay and transport recovery with
  signed Focused tests, one Fast checkpoint, XML build and independent Astra XHigh
  review. This remains an unpublished private C2 operation.
  Accepted 2026-09-24: the final current-exchange decision now executes the
  owner-checked original suffix to the next positive source or a checked complete
  frontier before saving. One optional dependent correction applies to the first
  continued exchange; zero-ceiling middle exchanges are drained without requiring
  an impossible correction-only checkpoint advance. A missing correction rejects
  the in-memory attempt while retaining the old physical pair for fresh-owner
  retry. Signed dependent Focused cases passed 2/2, the zero-ceiling/positive
  cold-classifier case passed 1/1, and neighboring same-exchange/replay controls
  passed 4/4. XML-enabled integration build exited 0 with three pre-existing
  unrelated CS1587 warnings; Fast passed 8337/8337 in 4:21 under seven minutes
  (`20260924-090256-366-18348-b82c597ebd394fa4a3619312e9ab5b8d-fast`).
  Independent Astra XHigh review: PASS, no actionable findings. This private
  path is not yet wired to live GM dispatch, so GM-facing synchronization stays
  with C4; parent C2 and C3/C4 remain open.

- [x] T081-C2-R1-PRIVATE-GM-ADAPTER [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Build the private owner-bound offer and decision adapter without activating
  GameEngine or daemon dispatch. After checkpoint-first classification/repair,
  project only the current visibility-safe opportunity and allowed response
  shape from a matched replay owner; never expose checkpoint, pending packet,
  source witnesses, allocation journal, before/candidate images or authority
  fingerprints. Bind one GM response to that exact current offer and physical
  pair, reuse the ordinary wound command/composition owner and saved transport,
  and distinguish next offer, dependent exchange continuation, completed but
  unpublished capture, repair required and blocked. Exhausting a local source
  list must not imply the whole original turn is complete. Cover forged/stale,
  unoffered and duplicate responses, missing pending repair, same-exchange
  second source, zero-ceiling source skip, dependent next exchange and completed
  capture through signed Focused integration tests, one Fast checkpoint, XML
  build and independent Astra XHigh review. This adapter remains client-owned
  and uninvoked by live GM entrypoints; GM guidance and a worked example are
  synchronized when C4 activates the full publishable workflow.
  Accepted 2026-09-24: checkpoint-first repair and owner-bound safe offer are
  internal only. The adapter serializes a session's submissions, binds the
  submitted command bytes to the saved input layer, reports correctable
  dependent exchange failures separately, and exposes exact guarantee and
  worsening bounds without private provenance. Signed Focused adapter 6/6,
  dependent exchange 7/7, guarantee/retrauma 10/10; XML-enabled rebuild exited
  0 with three unrelated existing CS1587 warnings. Fast passed 8337/8337 in
  4:24 under the seven-minute limit
  (`20260924-095550-435-38228-11b66af5f3f045a5bf746951b17f46b5-fast`).
  Independent Astra XHigh review found and verified fixes for submission,
  command substitution, disposition and offer bounds, then passed the final
  correction with no remaining findings. GM-facing prompts/examples remain
  unchanged because no live GM entrypoint invokes this private seam; C4 owns
  their synchronization on activation. Parent C2 and C3/C4/C5 stay open.

- [x] T081-C2-R1-COLD-CONTINUATION-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Prerequisite to owner-bounded replay: let the exact cold capture prepare one detached
  dependent conflict-draft correction against immutable A, using the existing source
  permitted-edit validator and a capture-owned resource/source transaction. Bind any
  prepared ticket to the selected view revision; commit the new layer only when the
  actual source/resource step succeeds. Freeze the original exchange/action/dice
  inventory, reject foreign, stale, non-conflict and overlong changes, and preserve
  failed-attempt source, allocation and physical-file state. TDD against the signed
  cold-origin fixture and existing future-exchange contour; Focused, Fast checkpoint,
  XML build, scoped diff and independent Astra XHigh review. This unit does not claim
  general saved inputChanges, C1 packet production, recovery, persistence or publication.
  Accepted 2026-09-23: exact capture-owned cold layer and revision are selected inside
  the source/resource transaction; the immutable original A, physical later draft and
  unrelated path inventory remain unchanged. Ordinary source continuation cannot read
  or commit a speculative layer. Early calls, changed action/die, overlong exchange,
  appended recent conflict, malformed/foreign changes, concurrent ordinary continuation
  and stale prepared tickets reject without advancing the accepted owner state. Focused
  integration 1/1 (`20260923-223505-063-7196-c652eae9f0a449a999b42d6b6536da5c-focused`),
  neighboring integration 12/12 (`20260923-224210-483-29192-fa5d1ee1cbd746ce9e303cb793bed8c2-focused`),
  unit 6/6 (`20260923-222413-803-19996-60505331c93c4805a63e2f95cc5db3c1-focused`),
  XML-enabled integration build exit 0 with only three pre-existing unrelated CS1587
  warnings, and Fast 8326/8326 in 4:08 under the seven-minute limit
  (`20260923-223754-768-23228-7273ac766f1044b594940f0093fef349-fast`). Scoped diff
  and untracked-file trailing-whitespace checks clean; independent Astra XHigh review PASS
  after three substantive boundary corrections. This private view changes no GM-authored
  command or contract, so prompts/examples do not change here. COLD-OWNER-BOUNDS remains open.

- [x] T081-C2-R1-COLD-ORIGIN-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Expose only detached original draft images and immutable physical witness fingerprints
  from the structurally parsed checkpoint, then let a fresh named original capture under
  one canonical lease reopen the signed snapshot, compare original identity, declared
  paths and the three real immutable witnesses, and rerun existing location/item,
  resource/effect and spiritual source/wound owners against checkpoint A rather than current
  physical GM draft B. Retain current physical mutable controls under their transport
  owner; reject any origin disagreement before allocation, write or accepted authority.
  TDD a fresh service with changed current dependent draft and with a changed immutable
  witness, including originally absent versus present-empty paths. Use owning Focused
  integration, one Fast checkpoint, XML-enabled build, scoped diff and independent Astra
  XHigh review. This unit stops before applying saved advances, pending repair or writes.
  Evidence 2026-09-23: cold-origin Focused integration 4/4
  (`20260923-214645-799-20092-aa484b67867a4b2f84b00555bb7ccd17-focused`),
  warm-owner control 4/4 (`20260923-214809-415-16744-7a11c70ead1843c5b97721e7822f5943-focused`),
  codec Focused 30/30 (`20260923-214952-818-36408-621f6123777244a18c7f4f2237cf94c1-focused`),
  XML-enabled integration build exit 0 with zero warnings/errors (`NoWarn=1591` for
  existing unrelated missing-comment warnings), scoped diff/trailing-whitespace check
  clean, Fast 8325/8325 in 4:06 (`20260923-215217-637-33452-85e5a2d2bec5433f8eef1fead48330c4-fast`),
  and independent Astra XHigh review PASS after corrections. This establishes only
  write-free original A owner replay with changed physical B and immutable witness
  rejection; COLD-OWNER-BOUNDS, saved advances, pending repair and publication remain open.

- [x] T081-C2-R1-JOURNAL [US3] Implement the approved C2-R1 closed allocation/time replay journal
  as a pure prerequisite to capture persistence. Retain typed owner/causal coordinates and exact
  ordinary generated values; replay rejects reordered, extra, missing or differently typed calls
  without invoking allocation callbacks. Appending replays the retained prefix before generating
  new values. Faulted attempts cannot export a commit candidate. Prove detached input/output,
  strict shapes, ID/time validation, exact retry and prefix mismatch with TDD and Focused controls;
  independent XHigh review before acceptance. This prerequisite grants no persistence or gameplay
  authority and leaves parent C2 open for production owner hooks, checkpoint and cold recovery.
  Accepted 2026-09-22: XML Focused26/26, Fast8166/8166 in4:44.872 (182900), clean diff check,
  independent XHigh review PASS after the XML correction. Exact run evidence is in plan.md.

- [x] T081-C2-R1-RESOURCE-IDENTITIES [US3] Bind the journal to existing typed resource mutation
  and capacity allocation callbacks through an opt-in factory. Derive comparison coordinates from
  the complete typed operation key, preserving distinct mutation/capacity domains and ordinary
  random allocation. Reject unbound calls without an unjournaled fallback. Prove exact real-owner
  cold replay and causal mismatch rejection; XML Focused and independent XHigh review. This unit
  is not the full definition/pending/time or effect hook integration and does not create a capture
  checkpoint or authorize publication. Keep parent C2 open until all those paths are covered.
  Accepted 2026-09-22: XML Focused34/34 (183732), including actual resource execution on all
  three contours and causal mismatch controls; independent XHigh actual-code review PASS.
  Combined owner-hook Fast remains the next checkpoint, not a claim of parent C2 completion.

- [x] T081-C2-R1-EFFECT-IDENTITIES [US3] Thread immutable causal allocation keys through actual
  effect and combatant/member allocation sites, retaining parameterless compatibility for ordinary
  factories. Forward typed overloads through the real identity-history owner without losing its
  receipts. Add an opt-in journal adapter that forbids unbound allocation. Prove real base creation
  and consuming/non-consuming replacement cold replay, distinct semantic replacement slots,
  wrapper forwarding and stable identity history; owning XML Focused, combined Fast and independent
  XHigh review. This prerequisite does not install live capture persistence or authorize publication.
  Accepted 2026-09-22: XML Focused203/203 and Fast8186/8186 in3:39.980 (191020), independent
  XHigh final PASS, clean targeted diff check. Ordinary factory overrides remain compatible.

- [x] T081-C2-R1-RESOURCE-CREATION [US3] Route both actual definition/seal allocation sites and
  bounded pending request/time creation through typed ordinary factory hooks and the journal.
  Preserve legacy callback callers; freeze pending draft JSON before validation and detach each
  allocator callback argument. Prove actual catalog/pending replay and adversarial callback alias
  isolation, plus owning planner/source integration controls. Run XML Focused, appropriate
  integration selection, combined Fast and independent XHigh review. No checkpoint/publication
  authority or new GM capability is granted by these internal allocation hooks.
  Accepted2026-09-22: XML Focused281/281, signed integration3/3, Fast8194/8194 in4:24.328
  (193404), independent XHigh review PASS after generic XML and baseline-fixture corrections.

- [x] T081-C2-R1-CAPTURE-ALLOCATION-AUDIT [US3] Complete the actual original-capture allocation
  and clock inventory before live binding. Trace initial owner composition, cached effect base,
  registered outcomes and continuation factories. Record cache lifetime requirements and every
  additional reachable random/time producer in plan/data-model. Preserve ordinary behavior and
  registered authority invalidation; distinguish verified reachability from pending hypotheses.
  Independent XHigh review and Spec Kit consistency check precede dependent implementation.

- [x] T081-C2-R1-ORIGINAL-DRAFT-INPUTS [US3] Retain exact complete distributed input bytes before
  original capture composition. Add a detached original-input view with an owner-derived frozen
  inventory, explicit absence and original session/request/turn/snapshot binding. Preserve dynamic
  draft paths and independent siblings beyond the existing mechanics _observed subset. Reuse the
  physical snapshot reader's validated inventory without treating unselected snapshot files as read
  authority. Keep snapshot/authority/rollback artifacts and active checkpoint/pending content out of
  recursive input payloads; their original before-images remain separately required. Bind the view
  only to a successful original capture with the same original identity and unchanged observed
  inputs. Unknown reads fail closed. Prove real capture exact bytes, empty versus absent files,
  dynamic/unexecuted siblings, detached aliases, stale original identity and zero file writes.
  Use pure Focused tests plus signed integration, one Fast checkpoint and independent Astra XHigh
  review. This unit retains data only; candidate-read routing, pure narrative/interface admission,
  checkpoint persistence, continuation replay and GM dispatch remain explicit parent C2 work.
  Accepted2026-09-23: signed owning21/21 (081017), post-review pure3/3 (082112) and signed2/2
  (082227), XML Fast8240/8240 in4:52.415 of7min (082353). Independent Astra XHigh final review
  PASS after rollback-subtree exclusion and explicit prefilter root-case rejection. Exact run IDs,
  baseline XML warnings and the unnecessary earlier Focused timeout override are recorded in plan.md.

- [x] T081-C2-R1-COLD-INTAKE-ALLOCATIONS [US3] Before fresh candidate-input reconstruction,
  bind actual upstream raw item/location identity producers to the retained journal and isolate
  their caches by capture attempt/factory. Prove actual admitted new-item/location/link/transition
  and governed threat creation; preserve ordinary random ID forms and default callers. Item
  validation invalidation alone retains the old same-fingerprint allocation cache, and location
  cache hits likewise skip factory calls: neither may bypass the replay stream. Preserve owner
  fences, original snapshot identity, current canonical-root lifetime and missing-governed-item
  checks. Record the concrete typed hook/causal coordinate design before implementation; no
  normalization-only producer is added without actual raw-intake reachability. Run TDD, owning
  Focused, signed cold-intake integration, combined Fast and independent Astra XHigh review.
  This extends the producer inventory only when upstream intake is actually re-executed; the
  completed resource-capture allocation audit remains valid for its original narrower contour.
  Accepted2026-09-23: XML pure45/45, signed3/3, ordinary/rejection8/8 and compatibility2/2;
  independent Astra XHigh code and correction review PASS. Fast8258/8258 in3:59.667 of7min
  (094005) passed after fixing a reflection helper exposed by failed Fast093409; targeted2/2
  (093900) preserved both planner-exception authority-cleanup checks. Exact artifacts and process
  assessment are in plan.md. This remains private intake, not full cold reconstruction.

- [x] T081-C2-R1-OUTPUT-PROJECTION [US3] Before write-free candidate admission, extract shared
  in-memory narrative/interface normalization while preserving ordinary validation and its current
  write-on-change wrapper. Retain exact original raw bytes separately. Preserve malformed/no-change
  inputs, escaped line-break and hidden-control-tag behavior, exact serializer, timestamp fallback
  conditions and existing validation severity. Supply actual fallback time through a typed attempt
  clock when replay reaches this projection; existing timestamps must not allocate. Keep projection
  data separate from authority and do not use the side-effecting GameEngine response builder.
  Record the concrete API/reader boundary and run consistency review before implementation.
  TDD, pure normalization controls, relevant signed write-free/ordinary wrapper integration and
  independent Astra XHigh review are required; combine Fast with the candidate-admission checkpoint.
  This prerequisite does not complete dependent achievement/prose checks, full candidate-reader
  routing, original continuation replay or checkpoint persistence. Source issue remains #1536/C2-R1.
  Accepted2026-09-23: XML Focused pure/ordinary39/39 (101255), signed/ordinary Integration22/22
  (101431), and final absence/present-empty5/5 (101729), all exit0/no timeout/no duplicates/clean
  owned-tree cleanup. Hook-controlled revocation test was RED0/1 (101044) and GREEN in22/22 after
  a post-read owner/lease check. Parent inspected the baseline-three source diff/new files and
  evidence; independent actual Astra XHigh final code review PASS after the race and XML fixes.
  Fast remains combined with candidate admission by plan. This private projection changes no
  GM-authored output schema/command; full GM documentation will accompany later admission.

- [x] T081-C2-R1-CAPTURE-ALLOCATION-BINDING [US3] After the allocation audit, bind one journal
  and typed factories to the original capture from initial owner/base construction through all
  accepted continuations. A fresh capture must not reuse a cached result from a different factory
  scope. Preserve history receipts, registry invalidation and exact retained before/current images.
  Rejection/revocation invalidates the attempt. Prove fresh-owner replay, cache scope separation,
  changed causal inputs and zero accepted publication through owning Focused and signed integration
  controls, combined Fast and independent XHigh review. Full checkpoint schema, durable commit,
  draft restoration and GM dispatch remain parent C2 work.
  Accepted2026-09-23: XML capture/completion integration25/25 (025035), prefix/receipt28/28
  (024459), Fast8237/8237 in3:57.083 (074019), independent Astra XHigh binding/inventory review
  PASS after source-ticket and consumed-completion revocation fixes. The interrupted Fast073248
  is not acceptance evidence; the successful replacement is recorded in plan.md.

- [x] T081-WORKFLOW-SOL-PRIMARY [#1552](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1552)
  Apply user-approved 2026-09-23 routing revision to global AGENTS.md and this worktree's existing
  development-workflow.md: Sol High owns routine end-to-end development; independent Astra XHigh
  reviews completed coherent blocks; Astra High is reserved for concrete unresolved problems.
  Remove duplicated parent/implementer analysis and automatic intermediate-document reviews while
  preserving Spec Kit approvals, verification and mandatory independent review of final changes.
  Verify exact documentation diff and obtain bounded independent review; no C# rerun for policy alone.
  Accepted2026-09-23: parent inspected exact two-file baseline diff and whitespace; independent
  actual Astra XHigh review PASS. Global changes are confined to routing/workflow policy; approvals,
  verification and independent review remain intact. No C# tests needed for this documentation edit.

- [x] T081-C2-R1-CANDIDATE-READ-VIEW-INTAKE [US3] After output projection, introduce an explicit
  immutable current-input view for location/item owner intake, including route/transfer catalogs
  and nested location calls. Before implementation, record exact APIs, original identity checks,
  every read's candidate/pre-turn/physical role and a bounded private intake entry with Spec Kit
  consistency and independent Astra XHigh design review. Preserve each existing consumer decoder,
  ordinary callers, signed snapshot authority, real lease/request/freshness checks and attempt-owned
  allocation/fence semantics. Unknown registered draft reads fail closed; never fall back to disk,
  redirect physical signing authority, use an ambient override or create a scratch filesystem.
  Test actual owner plans against retained input while physical draft roots differ; keep the
  existing full capture's mismatching original-image rejection intact. That private intake test
  cannot authorize attaching a complete capture without later full reader/checkpoint validation.
  Read-only preflight must preserve existing treatment receipts/holds even after common validation
  was consumed, and reject foreign validated item factories before any cache/fence mutation. It
  must not create a missing session-generation file. Follow the exact root guard in plan.md;
  never restore old authority as valid or clear reservations to admit a staged original view.
  TDD, owning Focused, signed intake/ordinary compatibility controls and independent Astra XHigh
  review; combine Fast with the stable candidate-admission checkpoint. Resource/effect/source,
  same-turn actor/faction/wound validation, durable checkpoint and GM dispatch remain parent C2 work.
  Progress2026-09-23: source routing and noncreating guard implemented. XML Focused pure guard4/4
  (114952), signed/ordinary intake11/11 (115103) and Fast8290/8290 (105809) passed without timeout,
  duplicate IDs or cleanup failure. A/B tests inspect actual location/item plans, quest reward route,
  same-turn source export, missing governed item, player-to-NPC transfer, text companion, projection
  roots and foreign-factory invisibility while physical current files differ. Live resource-only,
  open held-treatment and finalized claims reject named intake without losing publication authority;
  downstream failure revokes a raw private item registration. The noncreating/stale-generation,
  identity, scope, override, factory, lease, foreign-fence and mid-capture-change controls pass.
  Independent actual Astra XHigh final re-review PASS after inspecting source, diffs and executed
  Focused/Fast artifacts. This accepts only the private prerequisite; complete C2 remains open.

- [x] T081-C2-R1-SOURCE-CURRENT-READ-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Bind the initial live spiritual source session's
  candidate dictionary to the exact retained current draft when the named original capture supplies
  it. Keep `PendingTurnSnapshotReader` and the physical request read authoritative; compare all four
  original identity coordinates before consuming candidate images. Preserve the source owner's
  strict UTF8 decoder, signed-before paths, ordinary source entry and existing continuation rules.
  Reject an unregistered selected path and invalid UTF8 without falling back to physical files.
  Prove signed A versus changed physical B through the actual source session's candidate dictionary,
  exact absent/present and malformed-input cases, and ordinary compatibility. This is an initial
  source acquisition prerequisite; effect/resource readers, source continuation and cold recovery
  remain parent C2 work. TDD, owning XML Focused, relevant signed controls, scoped diff and one
  independent Astra XHigh review; combine Fast at the next stable reader checkpoint.
  Accepted 2026-09-23: named original intake passes its detached current images to initial source
  acquisition only; signed before-images and physical request remain authoritative, and the
  ordinary source path stays physical. Genuine signed A/B integration plus two existing controls
  passed XML Focused 3/3 (171817), zero warnings/timeouts/duplicate IDs, clean owned-tree cleanup.
  The A/B test checks all selected paths, both absence directions, four tuple mismatches,
  unregistered selected path, malformed strict UTF8 and unchanged request. A later XML-only
  documentation correction compiled without new warnings; three unrelated CS1587 warnings remain
  in untouched files. Independent actual Astra XHigh review PASS after that correction. No
  GM-authored contract changed; initial source continuation and full C2 recovery remain open.

- [x] T081-C2-R1-EFFECT-CURRENT-READ-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Route the initial effect owner's current command, carrier, identity, source-authority and world-
  time reads through the named original draft, including its nested location plan; preserve
  physical signed before-images, manifest and request, and ordinary/mortal entrypoints. Route the
  corresponding effect command and identity reads in the surrounding resource input composer.
  Check the exact four-coordinate manifest identity before consuming a view; reject unregistered
  paths without physical fallback. Prove a real named A/B capture reaches final stale-input
  rejection instead of adopting physical B, and an ordinary read still sees B. Track remaining
  wound-draft and changed same-turn owner validators separately. TDD, owning XML Focused,
  relevant signed controls, scoped diff and independent Astra XHigh review; combine Fast with the
  next stable reader checkpoint.
  Accepted 2026-09-23: initial named effect current command/carrier/index/source/world-time and
  nested location reads use the retained original draft after exact manifest identity checking;
  physical signed pre-turn and request reads, ordinary/Mortal paths and later continuation stay
  unchanged. Real A/B signed tests cover both presence directions and final stale-input rejection;
  direct controls cover four tuple mismatches, missing selected path and revocation of a real prior
  effect handoff. XML Focused new/related controls 7/7 (174410), final XML build zero warnings.
  Independent actual Astra XHigh PASS after XML and handoff-revocation corrections. Fast first
  exposed a missing explicit family-inventory entry for the earlier treatment guard test; one
  line in FastTestBoundaryTests was independently reviewed PASS and exact Focused 1/1 (174912).
  Repeat Fast 8291/8291 in 4:02 (174945), no timeout/duplicates and clean cleanup. Full resource,
  wound-draft, changed same-turn owner, source continuation and C2 recovery remain open; no
  GM-authored contract changed.

- [x] T081-C2-R1-RESOURCE-CURRENT-READ-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Bind the initial resource owner's selected current command and canonical roots, continuity
  checks, resource-owner roots, system-outcome inputs and draft before-images to the exact retained
  original view in named spiritual intake. Keep validated manifest, signed pre-turn roots, physical
  turn request and excluded control-file witnesses physical; ordinary and Mortal paths remain
  unchanged. Reject identity mismatch and unregistered draft inputs before plan authority can be
  retained, without treating a current B image as the original A. Prove signed mid-capture A/B
  changes across an unchanged canonical root and an afterlife owner root reach the final freshness
  guard, while ordinary validation still observes B. Wound-draft and changed same-turn owner
  validators remain separate. TDD, owning XML Focused, relevant signed controls, scoped diff and
  independent Astra XHigh review; run Fast at the next meaningful checkpoint.
  Accepted2026-09-23: genuine signed A/B RED then owning Focused4/4 (181623), related
  source/effect/resource Focused7/7 (180816), XML-enabled build exit0 with three pre-existing
  unrelated CS1587 warnings; scoped whitespace clean. Independent actual Astra XHigh PASS.
  Subsequent Fast8291/8291 in4:20 (181930), under the7-minute lane cap, with no
  timeout/duplicates and clean cleanup.
  No GM-authored contract changed; wound-draft, changed same-turn owners, source continuation
  and durable recovery remain open.

- [x] T081-C2-R1-WOUND-DRAFT-APPLICABILITY [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Investigate whether the raw `wound_commands.json` loader needs a retained-current reader in
  initial named spiritual intake under the existing accepted-command contract. A genuine signed
  Chaos Sea fixture with an empty bound wound command reached the loader but failed in the
  downstream Mortal-only opportunity authority. Independent Astra XHigh audit checked every
  command family: empty/opportunity commands require `mortal_world`; treatment detached coordinates
  require Mortal realm and binding equality; accepted transitions have no adapter. Thus no
  presently valid spiritual original capture can pass with a present wound command. The separate
  spiritual source-bound wound selection remains in scope for C2/C3/C4. Trial production routing,
  fixture extension and failing test were removed; no production behavior was changed by this
  investigation. Revisit this seam only if a tracked task later adds a valid spiritual wound-command
  adapter; preserve existing realm authority meanwhile. Evidence and ruling in plan.md.

- [x] T081-C2-R1-SAME-TURN-OWNER-READ-VIEW [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Close the changed effect-source owner validator's transitive current-read boundary under named
  original intake. It currently calls generic selected state validation, NPC actor completeness
  and both Mortal/Shining raw faction families against physical B even though accepted source
  roots came from retained A. Reuse the real validators through a private A-bound validation
  context with the original physical filesystem for signed pre-turn/controls; keep ordinary and
  Mortal calls physical and avoid a mutable process-wide or leaked override. Fail closed on every
  unregistered transitive current dependency. Prove signed valid-A/invalid-B and invalid-A/valid-B
  cases for changed skill, NPC and faction owners, absent/present distinctions, non-leaking
  ordinary control, and physical signed authority. Split generic/NPC and full faction closures
  into separately verified subblocks if needed; both are required before acceptance. TDD,
  owning Focused, relevant signed controls, XML comments, scoped diff, Fast at a meaningful
  checkpoint and independent Astra XHigh review of the completed coherent block.
  Accepted 2026-09-23: signed A/B and current-read controls Focused20/20
  (`20260923-191556-128-21640-6bcf9d32c1fd4cd19de8afe1497daf1b-focused`),
  XML-enabled integration build exit 0 with three pre-existing CS1587 warnings outside this
  block, Fast8291/8291 in4:14.109 with the 7-minute limit
  (`20260923-192005-965-25740-5d045849b8f049f3a1b89e518d3a4304-fast`),
  scoped diff check clean, and independent Astra XHigh review PASS after its NPC-scene
  synchronous-read finding was reproduced RED and corrected GREEN. Parent C2 remains open.

- [x] T081-C2-R1-EFFECT-CACHE-SCOPE [US3] Prerequisite to capture binding: allow an explicit
  effect factory on ordinary and wound-prepared cache builds. Reuse requires the same factory
  reference and existing complete input fingerprint; factory identity never enters serialized
  fingerprints. Preserve constructor defaults, validated handoff and invalidation. Test real
  ordinary/wound cache paths and fresh strict-replay factories, with Focused and XHigh review;
  combined Fast at the live-binding checkpoint. This unit alone does not bind the registry/capture.
  Accepted2026-09-22: owning XML Focused277/277, Fast8200/8200 in3:42.899 (195201),
  independent XHigh actual-code review PASS. Registry/capture forwarding remains open.

- [x] T081-C2-R1-VEHICLE-IDENTITY [US3] Preserve ordinary vehicle creation during original
  spiritual replay, whose actual reachability is proven by the signed allocation inventory test.
  Add the typed admitted-ref allocation hook, retain existing override compatibility, route the
  actual composer and implement an opt-in journal adapter. Test real owner exact replay, changed
  ref rejection and unbound-call failure; owning Focused, XHigh review and combined Fast.
  Accepted2026-09-22: XML Focused277/277, legacy/signed integration12/12, Fast8200/8200
  in3:42.899 (195201), independent XHigh actual-code review PASS.

- [x] T081-C2-R1-PROJECTION-CLOCKS [US3] Retain actual times for repeated pure projections:
  memory grant, archive receipt, spiritual conflict closure and survival consumption. Use one
  attempt-owned memoizing journal clock with detached causal keys, never an ambient global clock.
  Preserve owner admission, optional timestamp fallback semantics and output formatting. Prove
  real projection equality, no extra clock calls, invalidation and mismatch rejection with TDD,
  owning Focused, XHigh review and combined Fast. Initial/live capture routing remains a separate
  binding step; no memory ID generator is reachable through admitted legacy grants.
  Accepted2026-09-22: XML Focused126/126 plus unrelated-root1/1, integration3/3,
  Fast8212/8212 in3:45.270 (201110), independent XHigh owner/survival review PASS.

- [x] T081-C2-R1-SPECULATIVE-ALLOCATIONS [US3] Before live allocation binding, preserve rejected
  continuation retries through a private serialized speculation boundary over journal cursor,
  appended rows, key/identity indexes and projection-clock memo together. Commit only for accepted
  owner transitions; ordinary rejection restores both, while callback/mismatch failures remain
  permanently faulted. No public/durable rollback authority; no rollback of already advanced
  resource/effect state. Prove rejected A then accepted B cold replay, same-key retry, retained
  prefix rollback, identity index restoration, permanent faults and scope ownership/lifetime.
  TDD, owning Focused, independent XHigh and combined Fast before live binding acceptance.
  Accepted2026-09-23: XML47/47, independent XHigh fix review PASS, combined Fast8222/8222
  in5:15.806 (014122). Validation-only probes during strict replay remain a live-binding design
  dependency: rolling back a speculative scope does not undo a replay mismatch fault.

- [x] T081-C2-R1-CLOSURE-PROBES [US3] Preserve closure time when the admitted missing
  terminalExchange witness is completed; exclude only that mutable witness from its clock key,
  retaining all other effective resolution fields and ordinary source validation. Add a private
  non-committable conflict validation probe for previously unseen clock keys during strict replay:
  reuse accepted memo times, otherwise use a valid UTC value absent from retained source timestamp
  evidence, with no journal request or underlying clock read. Restore memo on every exit; no probe
  ticket/projection may escape into accepted consumption. Cover retained-time collision, witness
  completion, rejected altered immutable evidence, strict replay ordering and scope lifetime.
  TDD, owning Focused and independent XHigh before live binding; combined Fast at its checkpoint.

- [x] T081-C2-R1-ALLOCATION-SCOPE-GUARD [US3] Before capture binding, require an active
  allocation scope for opt-in capture journals, including strict replay cursor consumption.
  Preserve ordinary standalone journal behavior and memo-only reads of accepted projection time.
  Reject nested requests and scope closure during allocation callbacks; recheck journal usability
  and exact scope after callbacks. Faults remain permanent. Cover append/replay outside scopes,
  callback reentry/closure, combined-clock rollback and accepted memo reads with Focused and XHigh.
  Capture gate, exact source authority and fresh factory ownership remain separate obligations.

- [ ] T081-C3-SPIRITUAL-RECEIPT-REDUCTION [US3] Reduce a completed exact ordinary-mechanics
  capture plus every explicit staged decision into append-only spiritual instance/closure/source/decision
  after-images and common wound-command inputs. First prove a production-valid signed explicit
  decline appends a durable `none` receipt as real common-plan work without a fabricated wound
  transition. Then prove materialize agrees with exactly one existing common wound command,
  carrier/index update and history transition. Preserve the full independently reconstructable
  source witness, causal source order, receipt-side consumption state and exact replay/conflict
  semantics. Cover decline, one production-created materialize result and terminal/reused display
  IDs with RED/GREEN unit and signed integration tests, owning Focused, the required documentation
  Focused control, FullValidation, Fast and independent XHigh review of
  source/receipt/history/carrier/fingerprint agreement. T081-D continues to own the full
  lower/create/worsen/explicit older re-trauma, simultaneous-side and dependent-exchange behavior;
  T081-E continues to own complete source families and danger/defeat/dissipation behavior, and both
  must use this boundary before parent T081 can complete. Update the afterlife matrix, wound/output
  guidance, worked example, manifest and guards with the accepted receipt semantics in this same
  unit; leave the closed Mortal receipt contract unchanged.

- [x] T081-C3-R1-EXPLICIT-DECLINE [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  First bounded C3 reduction block: from a cold-replayed, completed C2 owner
  and its exact signed receipt/conflict A, append each positive source's full
  witness and explicit `none` decision to the spiritual instance ledger.
  Derive instance identity through the existing signed-origin resolver and
  preserve lifetime/global ordinals and causal decision bindings; reject a
  guarantee, stale/forged pair, duplicate accepted request or changed signed
  history. Attach the validated receipt after-image to the completed ordinary
  mechanics reduction so `CompleteAcceptedReduction` exposes real touched
  common-plan work, while producing no wound materialization stage or carrier/index/history
  transition or accepted publication. Test RED then GREEN with one signed
  single-source decline and an existing-history/second-source case; inspect
  packet-to-receipt witness agreement, replay/conflict, untouched wound roots
  and zero physical writes. Update the afterlife receipt matrix, guide,
  worked example, manifest and guards for this reducer contract; do not
  activate GameEngine/daemon dispatch. Run owning Focused, required
  documentation Focused, FullValidation for the affected afterlife docs,
  one Fast control, XML build and independent Astra XHigh review. Parent C3
  remains open for materialize, closure and final C4 handoff.

- [x] T081-C3-R2A-LIVE-WOUND-PROOF [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Retain each production-created spiritual wound insertion's actual prepared
  authority, operation-before state, reduced wound state, typed effect results
  and writer receipts. At successful completion of the exact resource/effect/source
  owner tuple, export one immutable ordered proof of every registered insertion;
  reject foreign, missing, duplicated or reordered selections and a mismatch
  with the completed effect plan/transcript. Prove the final wound carriers,
  identity and history are the real reducer outputs, including a second
  sequential insertion. This proof is private, detached and write-free; it
  grants no accepted-plan or publication authority. Use RED/GREEN owning
  tests, Focused and one meaningful Fast checkpoint, XML build and independent
  Astra XHigh review. C3-R2B will bind the proof to receipt decisions and the
  common assembler; C4 retains live publication and transaction controls.

  Accepted 2026-09-24: the actual owner-created single insertion and sequential
  create/worsen chain retain exact prepared/reduced state, typed effect results,
  writer receipts, source selections and completed resource/effect identity.
  Missing, foreign, reordered, mispaired and divergent-list evidence fails
  before common assembly; a raw live-wound reduction without the proof also
  fails closed. RED failed at the absent proof API. Final owning Focused passed
  8/8 in `20260924-212157-247-33380-623eca7b14aa4346a44d2bc539dc24e7-focused`;
  Fast passed 8338/8338 in 5:15 under its seven-minute limit in
  `20260924-212444-180-35080-b0aa1f9c02284a58a732097e3674d143-fast`.
  XML-enabled solution build exited 0 with no changed-file warnings, and
  `git diff --check` exited 0. Independent Astra XHigh review found three
  authority-boundary issues, all corrected, and its targeted final review
  reported PASS. This private proof adds no GM-authored or published afterlife
  contract; C3-R2B and C4 remain open.

- [ ] T081-C3-R2B-RECEIPT-COMMON-ASSEMBLY [US3] [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
  Reduce the completed C2 packet's ordered explicit `none` and `materialize`
  decisions against the exact R2A owner-sealed insertion proof. Preserve full
  source witnesses and append-only receipt order; derive materialized wound and
  transition IDs only from real reducer outputs. Require a bijection between
  staged materializations, selections, insertions and wound history transitions,
  including same-wound create/worsen and mixed declines; reject missing,
  stale, foreign, duplicated or reordered evidence before accepted assembly.
  Extend the existing common accepted reducer and carrier assembler with a
  typed live-wound path that folds each actual insertion against its immediate
  predecessor and publishes only the last validated carrier/index/history
  image alongside the completed effect/resource/receipt images. Preserve the
  ordinary `WoundStageBundle` contract, original before-images, touched and
  consumed paths, exact fingerprints and one common-plan publication proof.
  Keep this block write-free: no GameEngine or daemon activation. Add RED/GREEN
  unit and signed integration controls for single materialize, create/worsen,
  mixed decisions, negative pairings and no physical writes; update the
  afterlife receipt matrix/guide, worked example, manifest and guards for the
  newly representable materialized receipt. Run owning Focused, documentation
  Focused, FullValidation, one Fast checkpoint, XML build, `git diff --check`
  and independent Astra XHigh review. Parent C3 still owns closure/reused
  display identity and final replay checks; C4 owns live publication.
  Investigation checkpoint 2026-09-24: C2 freezes action target evidence
  before the first inserted wound ID exists, so full C2→C3 create→worsen
  proof remains open; predicted IDs are not acceptable evidence. Current
  packet duplicate-create rejection covers only the local FR-034 boundary.
  C4 must bind the present C2 command candidate separately from the original
  draft B before-image retained by this reduction; signed A is separate
  receipt/conflict authority.
  Partial implementation checkpoint 2026-09-24: full-packet capture binding,
  exact owner receipt proof, live common carrier/index/history composition,
  initial-stage contribution overlay, duplicate-create rejection and C4
  fail-closed writer guard are present but unpublished. Independent targeted
  review found authority bypasses; corrected the proof issuer, full-witness
  binding, exact effect instance checks and completed-reduction constructor,
  then review reported no remaining P1/P2 in this bounded code scope.
  Owning Focused integration 4/4, documentation Focused 132/132,
  FullValidation 1923/1923 in 19:16 of 30:00, Fast 8338/8338 in 4:28
  of 7:00, XML-enabled solution build exit 0 (4016 documentation warnings
  across the solution; none reported in the changed R2B API files), and
  `git diff --check` exit 0. Do not mark this
  task complete: full C2→C3 same-wound create→worsen, initial-stage plus
  live-insertion integration, and cross-packet side seal remain open.
  Reachability investigation 2026-09-26: genuine spiritual intake currently
  cannot produce an ordinary initial WoundStageBundle. Its only upstream
  handoff uses Mortal occurrence or Mortal treatment authority, both requiring
  mortal_world; the signed spiritual source requires chaos_sea/shining_abode.
  The positive initial-stage-plus-live criterion stays open and depends on a
  lawful afterlife healing/recovery producer from T099/T101-T107, especially
  T107. Do not substitute a pre-existing wound or fabricated stage, or weaken
  realm checks to close it. The older same-wound/cross-packet gap is now covered
  by the accepted T081-D controls: NextSameSideOfferTargetsRegisteredConflictWound
  proves actual create→worsen insertions, same ID, distinct transitions and
  final common carrier/index/history; NextTurnTargetsSignedConflictWound(null)
  authenticates the prior receipt from the next signed snapshot and worsens the
  same conflict wound through C3/common assembly. Negative origin controls are
  recorded under T081-D. These satisfy those bounded R2B integration checks;
  initial-stage coexistence and parent C3 terminal/reused-display-ID work stay open.
  Next bounded verification extends the actual both-side materialization case
  with cold common-plan equality, genuine owner rejoin, foreign receipt-proof
  and reordered receipt rejection, and unchanged canonical bytes. This adds
  evidence for current C3 behavior, not a new game capability or C4 publisher.
  Accepted2026-09-26: actual both-side cold owner/common-plan replay and negative
  foreign/reordered receipt controls passed Focused1/1 (191355,1:04), XML build
  succeeded with only known unrelated warnings, and independent Astra XHigh
  found no actionable issue. Parent inspected the test and evidence. R2B remains
  open only for its lawful initial-stage coexistence integration dependency;
  parent C3 retains terminal/reused-display-ID work.

- [x] T081-C3-R3-TERMINAL-PRODUCER [US3] Implement the approved terminal receipt
  prerequisite for C3: begin with one genuine terminal exchange directly after
  signed A under existing resolve/reward rules. First settle actual retained
  resource-owner ordering so both-side costs precede opposition retirement;
  preserve one executor/transcript and derive an owner-bound closure proof.
  Extend the precise C1/C2 terminal inventory and C3 immutable closure append,
  then prove cold replay/no duplicate cost or closure, no physical publication,
  changed terminal witness/missing prefix rejection, and later same-display
  distinct-instance admission from an actual accepted closure. Keep broader
  source families under T081-E and initial-stage producer under T107. Follow
  plan.md's terminal block, TDD, GM docs/examples/guards, owning Focused, XML,
  Fast, conditional FullValidation and independent Astra XHigh before acceptance.
  Preserve full effect-identity cold equality by completing the retained ordinary
  reduction at the exhausted final C2 step before its allocation-journal export;
  C3 consumes that cached result without new allocations. Cover both continuing
  and terminal completion routes and final-write recovery as detailed in plan.md.
  Accepted2026-09-26: owning terminal/pruning6/6, full-plan positive5 cases and
  final completion/dependency/failure16/16, XML, docs134/134, example2/2,
  Fast8356/8356 and FullValidation1925/1925 passed. Independent Astra XHigh found
  no actionable issue; parent inspected actual diff/evidence and rechecked28
  frozen hashes. The completion-boundary replay defect is fixed. Exact evidence
  is in plan.md; C3-R2B/T107, broader T081-E, C4 and #1536 remain open.

- [x] T081-C4-COMMON-PUBLICATION [US3] Consume C3's exact reduction once through the existing
  `CompletedOrdinaryMechanicsReduction` and `AcceptedMechanicsPlanner.CompleteAcceptedReduction`.
  Include pending consumption and spiritual receipt, command, carrier, effect, conflict, history,
  narrative and notification images in one publishable accepted plan; decline must bypass no-work
  early returns. Use the existing GameEngine write/read-back/rollback transaction as the sole
  publisher, with no temporary source-only accepted plan or post-publication seal writer. Prove
  live decline and materialize, unchanged dice/resource/progression identities, exact replay,
  initially absent roots, write/read-back failure, cold start after success and unchanged nonempty
  combat-condition siblings. Run owning Focused, targeted RegressionIntegration and
  LifecycleIntegration controls, the required documentation Focused control, FullValidation, Fast
  and independent XHigh review covering writer receipts, live identity/history, current carrier
  image and canonical/full fingerprint semantics. Update the
  afterlife lifecycle guidance, daemon/launcher prompts, worked example, manifest and guards with
  sole publication/read-back/rollback/final notification behavior in this same unit, and record why
  no new console/browser command or Mortal contract change is required.

- [x] T081-C4-A-COMMON-HANDOFF [US3] First bounded C4 milestone: bind a genuine
  completed C3 plan to the registry/cache and existing common publication
  transaction without replanning. Preserve separate signed rollback, original
  provenance and current committed publication images; reject uncommitted drift
  and foreign/missing handoffs. Consume both spiritual private roots and wound
  commands even for none, and verify live wound/effect/receipt/history read-back,
  write failures, original absence restoration and unchanged combat conditions.
  Follow plan.md's concrete C4 integration map, required GM synchronization,
  owning/conditional broad controls and independent Astra XHigh review. Actual
  GM continuation dispatch, final output/notifications and cold-after-success
  lifecycle remain mandatory under parent C4; this milestone cannot close C4.
  Accepted2026-09-26: genuine publication4/4, drift/ownership4/4, write/read-back
  rollback2 and strengthened owner1, chronicle/ordinary compatibility3/3,
  docs/source guards141/141 and example/manifest/C3 regression3/3 passed; XML
  build exit0 with only3known unrelated warnings, Fast8357/8357 in3:52 and
  FullValidation1926/1926 in14:57 passed. Exact failed-versus-passing run history
  remains in plan.md; the invalid zero-slot fixture was removed, not waived.
  Independent Astra XHigh found no actionable C4-A defects; parent checked the
  actual evidence and frozen27-file hashes. Parent C4 and #1536 stay open.

- [x] T081-C4-B-GM-TRANSPORT [US3] Connect the production original spiritual
  turn to C2 and the common C4 publisher through a correlated bounded GM reply.
  The precise C4-GM-TRANSPORT revision2 at spec.md:1017 was explicitly approved
  by the user on2026-09-26. Expand the existing
  C4 plan with the actual file/helper and worker/proposal/ready routes and run
  Spec Kit consistency analysis. Preserve source/snapshot/dice/progression and
  already selected decisions, reject stale/foreign/overbroad changes before
  advancement, and prove both decision and dependent-draft phases. Keep final
  narrative, notifications and cold-after-success under parent C4, including
  their actual GameEngine lifecycle integration. No new HTTP/player command.

- [x] T081-C4-B1-PENDING-SUBMISSION [US3] Under approved C4-GM-TRANSPORT revision2,
  extend SpiritualWoundCaptureCheckpointState and the existing C2 saved-decision/
  replay/transport/private adapter partials with the exact outstanding choice and
  selection-only allocation prefix. Preserve committed frontier/pending, rederive
  owners on restart, atomically advance+clear, and cover guarantee_satisfied and
  tampered/failed-write cases in SpiritualC2PendingSubmission integration tests.
  Follow the B1 plan/data-model, TDD, relevant docs and independent Astra XHigh.

- [x] T081-C4-B2-STRICT-ENVELOPE [P] [US3] Implement the approved comparison-only
  request/response protocol in Services/SpiritualWoundContinuationProtocol.cs,
  typed optional GmWorkerModels envelopes, strict GmWorkerJson parsing and narrow
  GmWorkerContractValidator checks. First prove real roundtrip currently drops
  the envelope; test closed/case/duplicate/cardinality/correlation rules in
  SpiritualWoundContinuationProtocolTests and ordinary worker compatibility.
  No protocol DTO grants C2 or filesystem authority; production apply remains B3.

- [x] T081-C4-B3-LIVE-DISPATCH [US3] After B1+B2, wire a focused GameEngine
  spiritual continuation partial, existing validation repair/helper/worker and
  apply/ready boundaries to real signed original intake and C2 methods. Preserve
  snapshot/dice/progression/frozen fields, release lease during GM wait, and
  recheck exact continuation before apply and ready. Prove both file/worker
  none/materialize, sequential and dependent/restart flows, stale/race negatives
  and ordinary repair regressions; synchronize GM docs/examples/guards and run
  the B3 plan controls plus independent Astra XHigh before marking parent B done.

- [x] T081-C4-B3-SOURCE-ONLY [US3] Resolve the preexisting completion gap found
  by the bounded 2026-09-27 entry-routing consultation: an original spiritual
  turn with no positive wound offer cannot produce its first C2 checkpoint.
  Prove ordinary same-active zero-offer compatibility and an owner-authenticated
  zero-offer terminal completion through the single publisher. Track investigation
  and the minimal implementation in the existing C4 plan; never fabricate a none
  decision, treat no_offer as completed_unpublished, or silently bypass terminal
  source/closure authority. Keep the unsupported route blocked until proven.

- [x] T081-C4-B3-DEPENDENT-CLOSURE [US3] Refine LIVE-DISPATCH's comparison-only
  dependent context through disposable genuine cold replay of the saved choice.
  Derive the complete uniquely determined cost correction closure across later
  zero-offer exchanges, executing real resource/effect owners in diagnostic memory;
  stop at a positive offer or the original inventory boundary. Separately walk
  actual corrections to report the first remaining issue. Preserve stable fields
  and correlation, all physical/private bytes, and the approved checkpoint schema.
  Prove two correction rounds for force-payment addition followed by expired-audit
  removal, sibling/prefix rejection, cold reopening and one actual saved resume.
  Never infer ambiguous recovery outcomes or parse diagnostic prose as authority.
  Migrate adjacent recovery/force tests only after observed compatibility RED,
  preserving their gameplay assertions. Relevant Focused controls and independent
  Astra XHigh review are required; this does not complete LIVE-DISPATCH.

- [x] T081-C4-LIVE-OUTPUT [US3] Complete the already approved parent C4 player-output
  boundary for live spiritual insertions. Bind each ordered create/worsen to its
  own immutable insertion and exact final narrative under the existing common
  publication transaction; return detached presentation to the existing GameEngine
  take-once notification path. Preserve same-wound chronological transitions,
  escaping, guarantee_satisfied silence, signed rollback and disposed-owner
  boundaries. Do not fabricate a batch stage bundle or add durable authority.
  Prove the actual dependent file/worker and automatic cold lifecycle assertions,
  ordered create-to-worsen output and missing-narration refusal; run relevant
  controls and independent Astra XHigh review. Follow the bounded map in plan.md.

- [x] T081-C4-INTERRUPTION-PROBE [US3] Verify the existing all-restored-or-all-committed
  acceptance at actual post-publication/pre-story and post-story/pre-cleanup cuts.
  Use genuine GameEngine publication, deterministic interruption with no implicit
  rollback, new filesystem/engine and real startup/late handling. Prove either
  complete signed-original restoration or complete accepted state/history/cleanup,
  with no duplicate charge, wound transition, decision, story or ordinary GM repair.
  Do not require seamless rollforward or invent persistent delivery authority.
  Record actual boundaries and failures; production finalization changes that
  introduce a new durable contract remain subject to written specification approval.

- [x] T081-C4-REJECTED-TERMINAL-CLEANUP [US3] Fix the observed retained original
  input after a completed late-response rollback (probe run3de12828). Remove only
  the still-correlated session/request/turn input under one canonical lease,
  after proven successful rollback and before snapshot cleanup. Cover both late
  rejection branches; preserve treatment-publication retry and foreign/newer input.
  Retain evidence if rollback fails. This repairs existing terminal cleanup without
  introducing durable publication authority, replay or a new GM contract. Run the
  owning interruption and correlation controls, XML build and independent review.
  Accepted2026-09-27: actual pre-story cold rollback1/1 (f469c3bd,3:27.645),
  exact input-correlation11/11 (d5e483,1:52.939), failed rollback evidence2/2
  (9548a16c,34.061s), XML build15.33s0warnings/errors and independent Astra XHigh
  source/evidence approval. Four-file as-built manifest37EB1FF1 is in plan.md;
  integrated docs139/Fast8438 and separate orphan cleanup have since passed; FullValidation and startup compatibility remain pending.

- [x] T081-C4-ORPHAN-SNAPSHOT-EVIDENCE [US3] Finish the already required terminal
  cleanup after the actual post-story interruption (run7f3b951a). Preserve the exact
  inactive original snapshot evidence in client-only diagnostic storage before
  removing its active copies. Do not infer commit, replay authority or recovery
  from story/C3/unusable status. Leave active, foreign, malformed or ambiguous
  correlation untouched; validate complete copies and unchanged originals under
  canonical lease/session fencing before narrowly scoped removal. Never call the
  broad rollback-file cleanup for this case. Prove evidence preservation, failure
  refusal, idempotence and unchanged world/output/story plus the existing owning
  cold all-committed scenario. No new gameplay/GM or durable publication contract.
  Accepted2026-09-27: copy/failure6/6, refusal18/18, actual post-story cold1/1
  (9115043e,4:20.221), clean XML build and independent Astra XHigh source review;
  exact layered71-file hashes reconciled. Evidence in plan.md. This acceptance
  does not establish recovery from a crash inside the publisher; the separate probe below owns that remaining verification.

- [x] T081-C4-MID-PUBLICATION-COLD-PROBE [US3] Verify spec.md US7 acceptance4
  at a genuine partial common-publisher cut, before all after-images/deletions finish.
  Capture the exact durable physical tree under the current writer lease, then
  recover that unchanged cut with a fresh filesystem/engine at a different test
  root through ordinary startup/late handling. Warm exception compensation is not
  cold recovery evidence. Prove a real mixed publication cut and either complete
  signed-original restoration or complete accepted state/history/cleanup, without
  new GM decisions, charges, wounds, effects or story duplicates. Use the existing
  signed rollback contract; initially test-only, with no new durable authority.
  Record deterministic copied-cut versus OS-kill scope, bounded owning controls,
  XML build and independent Astra XHigh review. Follow the concrete plan below the
  inactive-evidence block; do not weaken the oracle after an observed failure.
  Accepted2026-09-27: genuine mixed-cut cold probe1/1 in5:06.732 (d41a8b56),
  XML1:10.49 with only3known unrelated warnings, unchanged all-restored OR
  all-committed oracle, independent Astra XHigh source/evidence approval and
  parent diff/TRX checks. No production change was necessary. The deterministic
  copied cut transfers no live owner and does not claim an OS-process kill.
- [x] T081-C5-COLD-RECOVERY-DOCS [US3] Reconcile the C4 runtime acceptance with
  GM guidance: replace obsolete blanket cold-finalization-open claims by the
  existing signed-original-or-complete-accepted recovery contract and current-request
  workflow. Synchronize matrix, wound guide, CLI/daemon/step entrypoints, worked
  example, manifest and documentation guards; describe the three actual deterministic
  cut boundaries in the internal contract without claiming OS-kill coverage, forced
  rollforward or physical exactly-once notification delivery. Preserve authoring
  fields/IDs, source-only receipt limitations and C3-R2B/T107 plus T081-D/E gaps.
  Follow the bounded plan, source/manifest Focused, XML, Fast, conditional
  FullValidation and independent Astra XHigh review. No new gameplay contract.
  Accepted2026-09-27: XML19.02s clean; docs140/140, examples/manifest5/5,
  Fast8439/8439 in3:39.718 (8a506079), FullValidation1929/1929 in15:09.760
  (f5f1f945), clean runners/cleanup. Parent and independent Astra XHigh checked
  exact9-file application494E6440 and all final TRX; no open findings.
- [x] T081-D-POSITION-DECISION [US3] Resolve the approved position profile's
  starting/allowed-position ambiguity against actual dice, binding and maneuver
  consumers. Record a precise proposal and gameplay examples in spec.md, present
  that exact version for owner approval, then align plan/contracts/tasks before
  dependent implementation. Preserve canonical frontier, actor/operation scope,
  current-generation authority, closed exchanges and existing maneuver restrictions.
  Accepted2026-09-27: exact spec revision1 at1189 independently reviewed by Astra
  XHigh; owner explicitly replied «Утвердить редакцию 1 (рекомендуется)».
  Implementation and its verification remain separate open work.
- [x] T081-D-POSITION-CONSUMER [US3] Implement approved POSITION-DECISION revision1
  through current exact-actor/operation mechanics, position dice and binding
  prerequisites, preserving canonical maneuver and all unrelated operation rules.
  Cover both sides, aggregation/saturation, insertion/expiry, no repeated erosion,
  immutable completed comparison and original signed historical/retained evidence.
  Follow the approved-position plan; RED, XML, owning Focused and independent review.
- [ ] T081-C4-NEXT-TURN-ITEM-LOCATION-COMPATIBILITY [US3] Investigate the next-turn
  strict-validation incompatibility exposed by the position publication fixture:
  `SpiritualIntakeAllocations.WriteOriginalIntakeDraftAsync` authors unrelated
  item/location allocation drafts; `MortalItemTestFixture.cs:39,70` supplies a
  Common item with empty `fateCards`, required by
  `MortalItemMaterializationContract.cs:824` but rejected by
  `ValidationService.PlayerAndInventory.cs:5113–5122`;
  `MortalLocationAcceptedTurnPlanner.cs:457,3115–3116` rebuilds `knownExits` and
  `adjacencyMap` on the linked current location, rejected as unknown properties
  by the strict current-location schema. Reproduce through actual publication
  and the next pre-send check, then resolve the existing contract mismatch under
  #1536. The position fixture omits only these unrelated drafts before capture;
  no production fix or acceptance of the item/location path is claimed.
- [x] T081-D-POSITION-TERMINAL-COMPARISON [US3] Resolve the real C4 terminal
  publication failure exposed by the prior-conflict aggregation fixture. Trace
  the exact failure before changing production. Preserve ordinary resolved
  `terminalExchange` envelopes and permitted reuse of display conflict IDs;
  do not fabricate full canonical `exchangeLog` or participant rosters. Bind the
  comparison-only completed packet to the genuine planned active/terminal output,
  rejecting removed/changed/forged current evidence and stale packet reuse without
  creating execution authority. Prove actual terminal publication, later-conflict
  wound aggregation, and adversarial completed-packet comparisons; re-review the
  affected C4 boundary independently. This restores existing #1536 lifecycle
  requirements and does not add a new GM-authored field or game rule.
- [x] T081-D-POSITION-PREVIEW [US3] Keep the existing pre-turn dice preview truthful
  when accepted wounds make position operation-dependent. The current preview has
  no selected-operation input and labels canonical-position rows as mandatory.
  Add a regression through the accepted effect snapshot, then avoid publishing an
  unconditional result that conflicts with the approved effective-position rule.
  Reuse existing preview/reminder surfaces; do not invent new gameplay fields or
  grant execution authority. Preserve unaffected previews, exact participant scope,
  independent modifiers and signed dice. This is required consumer integration,
  not a new game capability; verify and independently review with the position block.
- [ ] T081-D-POSITION-DEPENDENCY [US3] Add narrowly owner-derived position modifier,
  total/margin/band correction, preserving independent modifier rows, all dice and
  closed source identity. Compose only proved cost permissions. Cover real C2 saved
  choice, cold replay, forbidden sibling changes and GameEngine final publication;
  synchronize GM docs/example/manifest and run required Fast/FullValidation/review.
  Independent review P2 is reproduced by genuine cold critical-narration RED
  e7a54dce (0/1). Approved staged issued-frontier protocol is recorded in spec.md
  DEPENDENT-FRONTIER-DECISION revision1; owner explicitly approved2026-09-28.
  Execute the following bounded subtask and plan's staged-frontier section;
  parent dependency remains incomplete until all verification/review passes.
- [x] T081-D-POSITION-DEPENDENCY-FRONTIER [US3] Implement approved
  DEPENDENT-FRONTIER-DECISION revisions1+2 (#1536) through plan.md's six ordered
  steps. Preserve critical RED; cover public strict issued A before successor B,
  actual valid critical narration, forbidden future edits, exact stale/cold/pair
  handling, worker rollback and real file/worker final publication. Update the
  named validation/walk/policy, GameEngine and worker files, GM guides/example/
  manifest/guards in the same block. Require XML build, affected documented
  categories and independent Astra XHigh correction/dependency review.
  No new JSON authority, changed correlation formula, invented consequences,
  or RESULT-CLOSURE implementation is authorized by this subtask.
  Current acceptance (2026-09-30): the reviewed 17 production and six lifecycle/
  cache files remain unchanged. Independent Astra XHigh verified the three missing
  cold cuts: 73981a04 1/1 (12:35.837), 5a4b0e55 1/1 (12:40.472), 9f6af212 1/1
  (14:14.899), each complete without timeout/duplicates/cleanup debt. Together
  with retained worker A/B, stale-A, orphan Ready, accepted-A recovery and private
  journal negatives, these satisfy the approved FRONTIER scope. Historical failed
  broad controls below remain failed and are not renewed acceptance gates.
  Revision2 explicitly APPROVED2026-09-28: revision1 cannot prove historical A acceptance or freeze its
  texts during cold B recovery without durable private evidence. Approved spec
  revision2 permits a replay-checked dependentDraftProgress inside the existing
  pendingSubmission. Include exact schema/chain, owner-controlled checkpoint commit
  activating B, cold obsolete-A cleanup, full prior-image retention, final normal
  advancement and all added negative cases from the updated six-step plan.
  New public preflight RED26fd0499 confirmed blocked issuance. Revision2 runtime
  and tests are now applied; typed/private/public, journal/history/fault and
  worker-before-Ready controls passed as recorded in plan.md. Full current cold/worker, compatibility,
  Fast/FullValidation and final independent acceptance remain open. Existing
  arithmetic/cost controls remain recorded.
  Warm file control65def75e reached confirmed A and exact transport cleanup but
  exhausted its internal540-second bound before B. Remove only the redundant
  outer-loop B projection after commit: keep detached committed request/issues,
  release the lease and freshly prove B at ordinary request publication. Preserve
  the unchanged cold route and all owner/transport checks. Measure full file and
  stale-A controls separately in LifecycleIntegration rather than expanding Fast.
  Independent review found that this shortcut also skipped the caller's orphan
  Ready rejection. Prove that real warm A commit+cleanup followed by an injected
  orphan Ready blocks B and preserves all files; then restore this guard at the
  first successor publication lease. Preserve current-request retry semantics.
  Reuse the whole existing reconciliation block, including exact obsolete A
  cleanup and existing B whole-draft validation, with no extra replay when both
  transport files are absent. Normalize physical test-hook paths and interrupt B
  only after publication confirmation; a rolled-back callback is not a cold cut.
- [ ] T081-D-POSITION-DEPENDENCY-FRONTIER-RUNNER [US3] After measured worker
  da50f085 internal660 failure with A accepted/B not yet published, capture real
  PlanOnly runner RED for a selected ProcessIntegration30 control. Add a caller
  filter intersected with the Process category and an explicit selected maximum30;
  keep unfiltered/default Process15, Focused max15, Fast7 and other lanes unchanged.
  Prove group intersection/default budgets and rejection of unfiltered Process30,
  selected Process31, Focused16 and unsupported lane filters. Document measured
  justification and provisional worker warm1500/selected Process30; retain its
  ProcessIntegration trait and all assertions. Build, targeted runner GREEN,
  owning worker evidence and independent review precede acceptance. Record its
  measured duration and explicitly resolve aggregate Process/PreMerge viability
  through C5 before final integration; selected success proves neither aggregate.
- [ ] T081-D-POSITION-DEPENDENCY-FRONTIER-CAPACITY [US3] Preserve aggregate test
  **WITHDRAWN2026-09-29 at owner direction**: the proposed60/90-minute limits
  are rejected; no aggregate control was run or accepted. Restore the prior
  bounded configuration and follow FRONTIER-PERFORMANCE below. Historical
  proposal/evidence remain for traceability, not execution authorization.
  coverage after the actual staged worker67fbcd35 passed21:20.645 wall. Retained
  same-class worker controls7:51.902 and7:56.348 make a planning floor37:08.895
  before three simpler rows/other process classes; Process30 is insufficient.
  Capture real runner-options RED, then provisionally raise Process default and
  ceiling to60 and PreMerge to90. Preserve category filters, phase order,
  concurrency, assertions, cleanup, Focused15/Fast7 and the selected owning
  worker's explicit30. Update behavioral/source guards and docs/testing.md.
  Require XML build, runner GREEN, affected Astra XHigh review and one complete
  unchanged-selection PreMerge diagnostic. Inspect exact expected Process cases
  across process/E2E phases, duration/headroom, total results, duplicates, timeout
  and owned cleanup; do not infer a standalone Process pass. Keep the new limits
  provisional until that evidence; no silent recategorization or exclusion.
- [ ] T081-D-POSITION-DEPENDENCY-FRONTIER-PERFORMANCE [US3] Enforce the owner's
  **ONLY TEST WORK2026-09-29**: all wound-feature implementation and production
  gameplay changes are suspended. Deliver cached prepared fixtures reused by
  other tests, consolidated important scenarios and removal of verified low-value
  duplicates with measured acceptable complete-run times. After verification,
  report actual times and stop until the owner's inspection/resume; do not
  automatically continue materialization. Thirty minutes is an upper bound,
  not the desired ordinary feedback duration.
  2026-09-29 upper bound30 minutes for every complete verification control;
  Fast remains7. Restore rejected Process60/PreMerge90 amendments before work.
  Diagnose the21-minute three-exchange worker with a bounded profile and phase
  timings, then remove verified redundant test preparation and low-value duplicate
  heavyweight tests. Map every removed case to preserved important coverage;
  combine related assertions around one expensive preparation and cache immutable
  fixture/catalog inputs where useful, as explicitly authorized by the owner.
  Copy/reset mutable state and retain fresh-engine cold recovery and owner proof.
  retain original-owner authority, saved choice/history, separate GM responses,
  cold recovery and single final publication. Do not substitute hidden suites
  or weaker acceptance criteria for the runtime problem. Use one relevant owning
  case per verified change, require measured improvement, Fast7 and one final
  PreMerge≤30 when the coherent block is ready. Independent Astra XHigh reviews
  correctness and coverage loss; document actual results and remaining risks.
  Full PreMerge exposed stale test authorities beyond the initial caches. Correct
  only invalid fixture inputs, exact governed-root expectations and central roll
  fixture writers against preserved current contracts; keep important checks,
  record failed controls honestly and use relevant bounded Focused gates before
  retrying the aggregate. A faster failed run is not performance acceptance.
  Preserve production game behavior; any runtime bottleneck found is evidence,
  not authorization for unrelated gameplay implementation.
- [ ] T081-D-PERFORMANCE-SEEDED-FRAME-CACHE [US3] Under ONLY TEST WORK,
  cache only the exact named default OriginalIntake frame, retaining custom hooks,
  dice and genuine GameEngine signers. Each copy has a fresh runtime generation,
  lease and authenticated C2 owner; no live authority is cached. Repair the stale
  test-context omission of genuine session generation, evidenced by Focused
  b16a9d62 failing both original output projection cases. Prove one preparation,
  independent roots/generations, tamper isolation, cross-owner rejection and
  disposal with a later clean copy. Measure relevant retained owning cases,
  review the coherent block independently, then include it in complete control.
  No production gameplay or admission guard changes; parent acceptance stays open.
- [ ] T081-D-PERFORMANCE-RECOVERY-SOURCE-CACHE [US2] Optimize only the genuine
  upstream recovery fixture preparation shared across evaluation clocks. Keep
  the existing complete signed-tree cache key, including evaluationMinute,
  unchanged. Cache an earlier detached source tree after real creation and
  optional stabilization, before writing the evaluation clock and signing turn44.
  Preserve complete initial-world-time, wound, creation/stabilization-minute,
  source-profile/root-count keys and every source-history/anchor assertion.
  Fresh roots, generations, final signed clock snapshots and ExportRecoveryBinding
  remain genuine. Prove source preparation reuse across clocks without mutable
  owner reuse, exact signed clocks/anchors, local tamper isolation and cold replay;
  measure phase costs, perform independent review and retain parent acceptance open.
- [ ] T081-D-PERFORMANCE-TRUSTED-FRAME-VARIANTS [US3] Extend only known,
  hookless test-frame preparations with complete immutable profile keys: exact
  named seed/version, ordered signed dice, both decimal balances and the fixed
  signing tuple. Canonicalize the explicit default pool to its existing template.
  Cover repeated force/special-cost, position and named intake+tier-two original
  baselines. Hooks, initializers, arbitrary callbacks and custom signers retain
  their genuine uncached path. Cache session bytes only, retaining fresh roots,
  actual generations, leases and C2 execution/cold replay. Preserve per-key
  preparation counters and original custom-input/isolation guards. Never share
  a private staged checkpoint as a foreign fresh fixture. Prove reuse, key
  separation, tamper isolation and owner rejection, measure actual preparation
  and owning controls, review independently and retain parent acceptance open.
- [ ] T081-D-PERFORMANCE-STAGED-TEMPLATE-AFFINITY [US3] Keep the selected
  prepared-staged-fixture consumers in one process-local balancing item so their
  genuine accepted-A preparation is not repeated across test hosts. Replace the
  GM/worker private-journal test's redundant source creation and A commit with
  the existing detached accepted-A template, while retaining its lawful B
  positive control, forbidden GM proposal, reserved worker rejection, zero
  applied files and exact unchanged transport tree. Preserve all selected cases,
  assertion axes, category filters, phase ordering, deadlines and concurrency
  ceilings. The foreign-owner negative must still create a distinct genuine
  source. Guard the five named template consumers' single-descriptor affinity,
  four cutover descriptors and704 selected cases; compare the entire actual
  discovery set against the emitted method selectors during review. The observed six cutover descriptors for704 cases exceed
  the four concurrent slots and left one descriptor unstarted in fad1db75;
  coalesce this class only into four balanced descriptors while retaining all704
  selected cases and the common lane bound. Verify owning tests, measure process preparation and review
  the coherent test/runner/documentation block independently before acceptance.
Recovery acceptance reconciliation, 2026-09-30: RUNTIME-UNBLOCK and R1/R2/R3 are
accepted within their bounded original scope after actual source/artifact
inspection and separate Astra XHigh evidence review. Current 43-file agreement,
23/23 + 4/4 + 47/47, later 35/35 cache and 27/27 activity/guidance evidence are
recorded in plan.md's current checkpoint. Obsolete aggregate verification and
publication-pause clauses in the historical descriptions below are superseded;
they are not additional required work. Full T069/T070 and FRONTIER remain open.

- [x] T081-D-PERFORMANCE-RUNTIME-UNBLOCK [US2] Owner-approved2026-09-29 exception
  to ONLY TEST WORK, limited to the three presented PreMerge blockers (#1536).
  Correct signed/live treatment-history equality by actual semantic value while
  preserving every prefix coordinate/result and rejecting changed history.
  The now-unblocked genuine reentry also exposes the already approved FR-065
  reset/allocation gap: derive untreated care and the exact not_stabilized reentry
  from the signed before-image; preserve the recovery anchor and unrelated state,
  and allocate only the new deterioration anchor from the accepted worsen ID/minute.
  Correct that same existing recovery boundary without weakening the retained test.
  Preserve the actual skill active flag through accepted treatment projection so
  procedure admission rejects the existing exact inactive-skill negative without
  claims, plans or writes. Implement only the already specified T069-C/T070 Mortal
  recovery composer and durable replay boundary required by the retained recovery
  tests: genuine registry binding/resolution, common accepted-plan authority,
  sole coordinated normalizer publication, complete immutable sealed history/
  receipts/anchors/effect lifecycle and invalid-history-first replay. Do not
  exceed MORTAL-RECOVERY-PUBLICATION revision1, whose exact written version the owner
  approved2026-09-29 after scoped analysis identified the contract gap. Do not
  complete all T070 or resume RESULT-CLOSURE. Use existing actual RED evidence,
  smallest owning Focused controls, XML-enabled build, relevant GM guidance/example
  guards when their contract is affected, and one independent Astra XHigh review
  of the coherent completed block. Then run the complete PreMerge within30minutes;
  retain Fast7, original test selection and important assertions. Record actual
  timing and stop for owner inspection. Acceptance of the parent performance task
  remains open until successful complete verification; no commit/push/merge/closure.
- [x] T081-D-PERFORMANCE-RECOVERY-R1 [US2] Implement approved revision1 durable
  recovery codec, exact epoch consumption and invalid-history-first replay in
  Services/MortalWoundRecoveryPersistence.cs, MortalWoundRecoveryPlanner.cs and
  WoundHistoryState.RecoveryReplay.cs; preserve closed source/receipt grammar and
  prove pure tamper/arithmetic plus retained owning replay cases. Plan R1.
- [x] T081-D-PERFORMANCE-RECOVERY-R2 [US2] After R1 interfaces are fixed and FR-065
  shared hooks are handed off, implement private recovery continuation and genuine
  common publication in Services/MortalWoundRecoveryAcceptedPlanComposer.cs and
  WoundAcceptedTurnPlanner.MortalRecoveryPublication.cs with targeted existing
  registry/cache/common hooks. Preserve actual effects, bounded threshold/full
  healing, no-op/death handoff, owner/binding and atomic rejection. Plan R2.
- [x] T081-D-PERFORMANCE-RECOVERY-R3 [US2] After R1/R2, strengthen owning
  IntegrationTests/MortalWoundRecoveryTests*.cs without dropping important cases,
  synchronize the existing Mortal GM guide/example/guards and inspect XML/Focused
  results; complete independent Astra XHigh review, successful full PreMerge≤30
  and STOP for owner inspection. Plan R3; parent performance task remains open.
  Initial actual independent Astra XHigh review2026-09-29 identified a genuine
  same-minute treatment/replay defect and missing one-turn intermediate-generation
  owning coverage. Original owning control12/20 in5:25.247 also exposed a blocked
  due-minute regression and two test defects. Preserve the original blocked110
  deadline; do not redefine expectations to match skipped unconsumed intervals.
  Same-minute real-treatment RED and subsequent positive/cold-replay/tamper paths
  are recorded in plan.md. Targeted review corrections and complete successful
  timing control remain open. Owner's later timing clarification allows a modest
  evidence-backed bound adjustment after optimization, while rejecting hour-long
  runs; runner30/Fast7 currently remain unchanged.
- [ ] T081-D-POSITION-RESULT-CLOSURE [US3] Design and implement exact operation-specific
  result dependencies when an approved position correction changes legal consequences.
  Do not guess an after-image in diagnostic replay or expose unrestricted after/dice.
  Recompute frontier permissions/correlation only after actual predecessor validation;
  retain original choice, dice, actors and closed prefix. Remains required for full
  POSITION-DECISION acceptance; arithmetic-only closure does not complete this task.
- [x] T081-D-POSITION-RESULT-CLOSURE-B0 [US3] Align research/data-model, live-turn
  contract and quickstart with approved RESULT-CLOSURE-BINDING revision1 (#1536,
  reviewed spec65BDA9AB), then run scoped Spec Kit analysis. Follow plan.md B0–B5;
  approved documentation alignment may proceed during evidence reconciliation;
  preserve the frozen FRONTIER runtime/tests until its acceptance, before B1 execution.
  No new journal schema, correlation formula, exchange or authority.
- [x] T081-D-POSITION-RESULT-CLOSURE-B1 [US3] After B0, prepare an isolated new-file
  fixture while unchanged FRONTIER binaries finish; execute only after its acceptance.
  capture genuine saved-choice RED: lawful binding13/10 after pressure5/15 loses
  leverage but remains in player_success dice band. Add force_binding +2→+1 with
  only setup:true, sufficient strong setup/decisive and nonmatching controls.
  Preserve signed dice, actors, prefix, choice, costs/resources and unpublished state.
  Evidence 2026-09-30: 30bf287c and 6d1f80df reproduce the genuine missing
  behavior after ordinary original admission. Six-case ae83e528 passes all four
  legal alternatives/nonmatching controls and fails only both intended lost cases.
  Independent Astra XHigh reviewed the exact fixtures and bounded permissions;
  its saved-command byte-comparison correction subsequently passes with all six
  cases in 84efd5c4 (2m56.721s command). This closes the test-first prerequisite,
  not RESULT-CLOSURE gameplay or its remaining B2–B5 acceptance.
- [ ] T081-D-POSITION-RESULT-CLOSURE-B2 [US3] After B1, derive only owner-proved
  current arithmetic plus outcome/after.controlState; admit ordinary-valid
  no_effect/blocked with exact before control/value/presence. Retain independently
  proved cost permissions and mixed-cost regression. Reject setup/operation/dice/
  actor/prefix changes, successful control, unrelated after fields and premature
  final echo. Legal alternatives expose no result fields; strong binding remains distinct.
- [ ] T081-D-POSITION-RESULT-CLOSURE-B3 [US3] After B2, validate actual A with only
  an independently proved exact final-control mismatch projected in a detached
  view; never suppress another ordinary error. Derive separate B for the unique
  effective raw activeConflict.controlState carrier/value/presence, keeping the
  existing dependent_draft phase and conflict lifecycle. Reject ignored wrappers and whole
  containers. Require full ordinary raw validation without temporary replacement after B.
- [ ] T081-D-POSITION-RESULT-CLOSURE-B4 [US3] After B3, persist/replay genuinely
  closed last-exchange A and derive B with zero exchanges remaining. Preserve
  journal shape/hashes; invent no B row/source/opportunity. Prove real warm replies,
  cold accepted-A-before-B recovery, distinct Ready/correlation, cumulative A+B,
  exact cleanup and once-only publication; reject forged B and altered accepted A.
- [ ] T081-D-POSITION-RESULT-CLOSURE-B5 [US3] After B4, synchronize afterlife GM
  guides/live contract/CLI-worker instructions/example/manifest/guards and record
  prompt/Mortal decisions. Inspect XML build and the selected, documented categories
  covering changed policy, owning lifecycle/transport and GM docs/examples. Complete
  independent Astra XHigh review and corrections before child acceptance. Parent
  RESULT-CLOSURE/#1536 remain open with all other operation/result chains unchanged.
- [x] T081-E-SOURCE-CEILINGS-LIFECYCLE [US3] Prove approved acceptance8–9 and
  FR-032 through three genuine file-transport GameEngine facts: ordinary and
  unrestricted special sources allow IV, originally capped special source allows
  only II despite formula/destination/danger IV. Sign original art before admission;
  decline the first opportunity and materialize legal rankII at the second. Check
  exact source/decision rows, full effect group, resources/history/output and cleanup.
  Use the bounded plan, new test partials only while C5 inputs remain frozen;
  parent serial XML build, separately selected bounded owning tests, Fast and
  independent Astra XHigh review. Accepted2026-09-27: three owning Facts1/1 each (93634b77, 098bc3b9, 565b64b8); XML build40.88s with0warnings/errors; Fast8439/8439 in3:40.593 (77d2db3b), all30TRX verified. Parent and independent Astra XHigh confirmed exact manifest20AC2E61 and final evidence with no findings. Test-only source-ceiling coverage; no GM gameplay contract changed and no FullValidation repeat needed. T081-E stays open.
- [ ] T081-C5-DOCS-AND-FINAL-CONTROLS [US3] Reconcile the C1-C4 documentation updates across the
  afterlife contract matrix, turn/rules/glossary and wound/output guides, daemon/launcher prompts,
  worked CLI example, validation manifest, documentation coverage, example validation and source
  guards. The complete example must show same-turn continuation, explicit decline receipt and
  materialized decision without exposing private authority or advertising a player command. Record
  the no-update rationale for inspected Mortal World surfaces. Run the required documentation
  Focused control, FullValidation for the afterlife contract boundary, signed runtime selections, Fast and final
  PreMerge when integration-ready; complete an independent XHigh final review and reconcile every
  approved T081-C durable-boundary scenario before marking T081-C complete. T081-D and T081-E still
  block parent T081 and overall US3 completion. Do not commit, stage, push, merge, close #1536 or
  implement the separately approved GEN-COST-DECISION work in this C5 task.

- [x] T081-B2C-J2-C2-PREFIX-P1 [US3] Add opt-in resource-owned original prefix boundary in AcceptedMechanicsPlanner.OriginalPrefix.cs and existing live iterator/session; verify no replay, real effect closure, waits, exact ownership and fixed/legacy behavior in ResourceExecutionSession tests. Prerequisite for acceptance1-6, not signed source admission.
- [x] T081-B2C-J2-C2-PREFIX-P2 [US3] After P1, finish source check/API design in plan.md and bind the actual retained prefix through SpiritualOriginalTurnCapture and signed source preparation/continuation. Preserve original snapshots and all immutable checks. Implements acceptance1-5; no caller-provided ledger authority. Accepted2026-09-19: consolidated signed Focused51/51 (075808), independent prefix_source_review XHigh PASS; P3/EFFECT controls were still open at that checkpoint and subsequently passed as recorded in plan.md.
- [x] T081-B2C-J2-C2-PREFIX-P3 [US3] Add real signed integration controls in AfterlifeResourceCutoverTests.SpiritualOriginalPrefix.cs for acceptance1-6, targeted source rejection controls and Fast; independent XHigh review and actual evidence before parent C2-PREFIX completion.
- [x] T081-B2C-J2-C2-PREFIX-EFFECT [US3] Implement owner-approved periodic_spend/periodic_gain profiles through existing generic effect parsing, deterministic and bounded receipt dispatch, reaction routing and allowed-operation authority. Preserve Damage/Restore semantics, wound-specific allowlists and use budgets. Prove actual AP prefix spending/recovery in signed integration; synchronize GM contract, worked examples, manifest and guards; Focused, conditional FullValidation and independent XHigh review before completion (#1536, approval2026-09-19).

- [x] T081-B2C-J2-C2-GEN-COST-DECISION [US3] Owner approved the exact two cost rules in spec.md on 2026-09-26 (#1536). Recovery/force-incarnation consumers, retained evidence, dependent correction and GM documentation are implemented and accepted. Owning Focused, Fast8355/8355, FullValidation1924/1924, final existing compatibility controls, XML builds and independent Astra XHigh review passed; exact evidence and resolved expiry finding are in plan.md's current special-cost checkpoint. C3/C4/T081-D and other unresolved profiles remain open.
  Execution order: follow plan.md "Approved action-cost execution plan" and
  contracts/wound-effects-and-atomicity.md special-cost section. First TDD the
  owned both-side recovery payment and conditional force-incarnation audits in
  `AfterlifeResourceCutoverTests.SpiritualWoundSpecialCosts.cs`, then update
  `ValidationService.AfterlifeSpiritualConflict.cs`, retained witness checks,
  `AfterlifeSpiritualConflictResourceOutcome.cs` and the current-generation
  consumer gate. Require actual Spend-before-Gain history, original no-burden
  compatibility, insufficient-funds rejection, failed/opposed/capped recovery,
  trigger closure, closed-prefix preservation and replay without duplicate cost.
  GM matrix/guide/worked examples/manifest/guards, owning Focused, XML build,
  Fast, relevant FullValidation and independent Astra XHigh are part of this
  same coherent block. This work does not close C4 or other unresolved profiles.
  Include the bounded absent-audit addition for an unchanged future force action
  in the live-turn contract: structural eligibility first, actual owner-derived
  positive burden on reconstruction; preserve sibling and completed-prefix
  evidence. Cover effective opposition operation precedence, whole audit-root
  creation, rejection without burden and cold replay. Do not globally classify
  force-incarnation as a mandatory-cost operation or suppress its required
  dependent correction into the unrelated missing-audit leaf flow.
  Resolve reviewed P2 finite-use expiry: a prescribed future force audit may be
  removed (including its otherwise empty root) only for the unchanged unexecuted
  action with actual owner reconstruction proving zero burden. A positive burden
  still requires payment. Cover expiry/removal, rejection while burden remains,
  unrelated sibling preservation and cold replay; classify obsolete valid audits
  as dependent repair without accepting a fabricated payment or zero audit.

- [x] T081-D-PERFORMANCE-LANE-SPLIT-SPEC [US3] Reconcile owner-approved #1536
  TEST-LANE-BOUNDARY-DECISION revision1 with #1505 FR-014/SC-013 in
  `specs/1536-complete-wound-materialization/spec.md`,
  `specs/1505-test-suite-performance/spec.md`, this plan and tasks. Run a
  non-destructive Spec Kit consistency check before runner implementation;
  preserve the existing30-minute PreMerge bound and Process/E2E membership.
- [x] T081-D-PERFORMANCE-LANE-SPLIT-RED [US3] In
  `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.ProcessSelection.cs`,
  add a failing PlanOnly guard that demands the new `SpiritualCutoverIntegration`
  lane's exact704 ordinary cases, four descriptors, five existing process cases,
  eight exact PreMerge sentinels, disjoint PreMerge filters and visible planned
  versus completed counts. Keep the guard itself in ProcessIntegration.
- [x] T081-D-PERFORMANCE-LANE-SPLIT-RUNNER [US3] In `scripts/test-csharp.ps1`,
  preserve the old category boundary and add the separate 30-minute cutover lane
  plus the exact eight-method PreMerge selection from plan.md. Keep the four
  balanced cutover shards, five prepared-staged consumers in one shard, full
  Fast, ExplorerWeb startup wave, four-host/two-Fast caps and exclusive
  ProcessIntegration/E2E. Reject missing planned TRX/cases as incomplete;
  produce explicit planned/completed counts in summary.json. Make RED guard green.
- [ ] T081-D-PERFORMANCE-LANE-SPLIT-VERIFY [US3]
  **SUSPENDED by owner on 2026-09-30; do not run the broad controls below.**
  Category-strategy replacement is tracked in #1505 tasks.md under
  `T065-CATEGORY-STRATEGY-SPEC`; unfinished old controls are not passes.
  Historical requirement: update `docs/testing.md` with the two groups,
  trigger policy and actual evidence. Confirm the
  compatibility-corrected owner/reflection tests remain green, XML-doc build
  has zero warnings/errors, exact PlanOnly discovery has no lost/duplicated
  ordinary or process cutover cases, and the new full cutover lane passes
  <=30 minutes. Run one complete PreMerge <=30 including frontend, Fast,
  Integration, ProcessIntegration, E2E and cleanup; report true planned and
  executed counts. Optimize or further split on an actual timeout, without
  hiding a partial run as completion.
- [ ] T081-D-PERFORMANCE-LANE-SPLIT-REVIEW [US3]
  **SUSPENDED with the old lane strategy on 2026-09-30.** Review the completed
  category migration under its new tracked tasks instead; independent review
  remains mandatory and no old verification task is marked complete.
  Historical requirement: have independent Astra XHigh inspect the exact
  runner, guard, documentation and test-fix diff against the
  approved #1536/#1505 requirements and verification evidence. Address valid
  findings, rerun affected checks, update the existing checkpoint once, then
  report measured times and stop for owner inspection. Do not resume gameplay
  implementation, stage, commit, push, merge or close either issue.
