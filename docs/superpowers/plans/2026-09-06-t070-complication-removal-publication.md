# T070 Selective Complication Removal Publication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Execute one reviewed task and one bounded C# lane at a time.

**Goal:** Publish every already-legal ordered `remove_complication` result combined
with implemented nonterminal treatment operations, selectively retiring its actual
effect lineage and preserving unrelated wound consequences.

**Architecture:** Reuse the existing pure complication-removal projection, immutable
outcome preparation, single treatment effect batch and sole atomic publisher. A
removal without severity change has terminal operations but no new root applications;
a simultaneous reduction rematerializes the complete surviving graph in that same
batch. A required empty batch is authenticated work, not an omitted effect stage.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T070, FR-064;
`data-model.md` selective causal ownership and `contracts/mortal-wound-treatment.md`.
Source evidence is in Git metadata `sdd/t070-complication-removal-batch-design-audit.md`,
`sdd/t070-removal-gm-example-source-audit.md` and the lineage-fixture audit.

**Execution gate:** Recovery publication `48911240..189ba934` is independently reviewed
and parent-accepted. Record the subsequent scoped bookkeeping HEAD as the exact BASE.
The named shared interfaces and final owner inventory were checked at `189ba934`.
The recovery stage adds points
to the private intent seal and preparation fingerprint version3; this stage builds on it.
The lineage fixture audit is incorporated below and parent-checked against the actual
canonical graph/seed/snapshot source. No legacy-preparation choice or migration is
needed for this nonterminal operation.

## Global Constraints

- Use the existing `E:/Games/worktrees/boe-1536-wound-materialization` worktree and
  `1536-complete-wound-materialization` branch. No remote mutation or `.serena` access.
- No new command, raw GM identity, public batch field, parallel publisher, fabricated
  authority/receipt, test-installed treatment history or old-save compatibility path.
- Preserve the full approved operation union. This stage enables removal alongside
  `stabilize`, `add_recovery` and `reduce_severity`; `heal`, `add_complication` and
  `apply_deterioration` still require their actual producers. T070/T177/#1536 stay open.
- Ordered outcome length remains at most8; `no_improvement` remains sole-only, and
  empty results remain active-course-only. Aggregate non-heal reduction remains at most2.
- Remove the exact typed canonical complication ID. GM initial proposals still use
  a same-proposal `complicationRef`; the production composer alone allocates/rewrites it.
  A free-text name or public hash is not accepted-world authority.
- Removal without reduction must not alter severity, its last-change event, retained
  root IDs, retained effect payloads, unrelated identity history or natural-recovery
  anchors. Other explicitly selected operations retain their ordinary effects.
- Start closure traversal at declared roots even if already terminal. Use only the
  existing validated first-create generation/reaction lineage, not replacement edges.
  Keep terminal identities/history; do not delete all effects with the wound source.
- `CanonicalStateNormalizer` remains sole publisher. Wound, effects, identities,
  resource settlement, treatment history, output and rollback are one transaction.
- Preserve the existing eight-property `WoundEffectOperationBatch`, 13-field
  `MortalWoundTreatmentRematerializationAuthority`, and rematerialization planner
  type/method names/signatures. Existing name-only reflection forbids new `Prepare`
  or `Agrees` overloads. Private helpers with distinct names are allowed.
- Genuine accepted-state/filesystem/lease/restart tests belong in Integration; pure
  contracts/projectors/source guards stay Fast. No runner, lane, project or persistent
  timeout changes. One implementer and one C# lane; use the smallest relevant owners.
- Verify required TRX subsets when a filter also selects coherent passing controls;
  do not rerun unchanged code merely to obtain a preferred total. Preserve actual
  prior failures and count broad fail-fast results as incomplete, not full success.
- Synchronize the Mortal guide, complete ordinary GM example, manifest and executable
  guards. No new afterlife action/control/response contract is introduced.

### Task 1: Ordered selective removal through the existing single batch

**Files:**

- Modify `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`:
  exact typed removal seal, independent agreement, grammar, shared scalar removal,
  required-effect-batch predicate and separate terminal-only finalization.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentWorkingWoundSimulator.cs`
  only to expose/reuse its pure `TryRemoveComplication` projection internally. Do not
  copy its graph pruning/slot compaction into the accepted publisher or relax parsing.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentSeverityRematerializationPlanner.cs`:
  one removal-aware effect projection feeding the existing batch/authority builder.
  A cohesive private helper partial may be proposed with source-backed scope if needed;
  do not create a separate publisher or duplicate the existing construction pipeline.
- Modify `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`
  and `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs` only at required-batch
  presence/selection seams, exact treatment-event authority, and the source-confirmed
  independent final rematerialization-proof check below.
- Modify `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` only to separate
  accepted treatment causality from the unchanged create/worsen severity-event rule
  and validate retained-root lineage for authenticated removal-only even with no
  terminal operations, plus the source-confirmed private terminal-request selection
  seam and mixed removal/reduction predecessor-coordinate seam below. Do not relax
  the reaction-lineage analyzer.
- Additional source-confirmed shared prerequisite below may narrowly modify
  `AcceptedMechanicsPlanner.cs` at the existing lineage/identity authority owner and
  the existing generation helper in `WoundAcceptedTurnPlanner.cs`. Reuse that owner;
  do not copy an identity validator or introduce a second publisher.
- Additional pure owners may extend `WoundEffectBatchPlannerTests.cs` and
  `WoundEffectLineagePlannerTests.cs`. Existing reaction/identity tests remain controls.
- The shared terminal-generation correction also updates the existing wound note in
  `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, and
  a source guard in `AfterlifeDocumentationCoverageTests.cs`; it grants no new
  spiritual-healing mechanic, pending/control field or GM-authored runtime identity.
- Create `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ComplicationRemovalPublication.cs`
  with `ComplicationRemovalPublication_` real-pipeline owners, reusing the existing
  partial family's accepted fixture and persistence/coordinator/rollback helpers.
- Modify `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs`
  only for the closed optional lineage seed, shared single-effect construction and
  live-only snapshot-count assertion described below. No raw override hook or changes
  to the protected stale-history tests.
- Modify `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` for that exact new source;
  synchronize `docs/testing.md` and `specs/1505-test-suite-performance/research.md`.
- Modify `OtherGuides/Wound_Materialization_Contract.md`,
  `Examples/E_CLI_Wound_Materialization.txt`, `Examples/example_validation_manifest.json`,
  `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`, and
  `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.Wounds.cs`.
- Preserve all existing tests, including both frozen stale-history bodies, the pure
  projection owner, existing reduction seal/DAG/empty-batch tests and selector guards.

**Existing interfaces:**

```csharp
MortalWoundTreatmentOutcomePublicationPlanner.Prepare(
    acceptedState, request, resolution, acceptedState.CurrentGameMinute);
MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
    preparation, resolution, effectBatch, applicationResults);
MortalWoundTreatmentSeverityRematerializationPlanner.Prepare(
    input, preparation, requestFingerprint, resolutionAuthorityFingerprint,
    resultFingerprint, attemptId, operationKey, expectedBeforeFingerprint);
WoundEffectTerminalOperationPlanner.Plan(
    beforeWound, preTurnEffectCarriers, parsedIdentityState, selectedRootEffectIds,
    acceptedTreatmentEventRef, mechanicsOrdinal: 1,
    operationOrdinalOffset: rootApplications.Count, operationKey);
```

`TryReadTreatmentContinuation` already exposes an internal validated view containing
the exact resolution and event coordinates. The shared effect source gate can use
that existing reader; there is no need for a second authority representation.

- [x] **Step 1: Establish real selective-removal RED**

Build routes before `AcceptedStateFixture.Create` exports any accepted authority.
Use the existing `ConfigureCourseConsequenceEnvelope(scenario, true)` fixture for
the initial singleton-course path. It gives severityIII, one unrelated base root,
two roots owned by one infection complication and a complete valid result. Replace
that route's milestone result with sole `remove_complication` using its exact
canonical complication ID. Keep the authored resource policy and real creation seed.

For the strict retained-effect equality cases, replace only this local scenario's
base periodic-damage definition/entry with a passive characteristic modifier of -1
and an empty triggers array, preserving its exact base root ID and definition key.
Do this before canonical history seeding/accepted preparation. The shared helper's
periodic owner_turn_end trigger legitimately appends effect history during publication;
its default characteristic value -2 is also illegal at a later severityI destination.
Neither is suitable for this unchanged-root oracle. Do not change the shared helper,
strip history fields from comparisons, or suppress ordinary production triggers.

Resolve, persist and rehydrate using the existing helpers; compose and publish via
the coordinated accepted pipeline. The initial failure must be the unsupported
publication slice, not malformed setup/history/ownership. No synthetic continuation.

Assert the intended final behavior before implementing: no complication, one retained
base root and slot, unchanged complete severity and recovery blocks, unchanged retained
effect/identity payloads, exactly the two selected roots terminal, no new effect
identities, one genuine current-dose settlement and one treat row with exact receipt.
Keep all terminal provenance. Add cold replay and byte-identical replay no-op checks.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.ComplicationRemovalPublication_RemovesOnlyOwnedRootsAndPreservesBaseConsequence"
```

- [x] **Step 2: Share the pure removal and retain every exact intent payload**

Make `MortalWoundTreatmentWorkingWoundSimulator.TryRemoveComplication` internal,
preserving its implementation. It removes the selected complication, its declared
bindings and entries, compacts slots, and retains only definitions reachable from
remaining roots. The simulator continues to canonical-parse every ordered intermediate
state; accepted-state selection continues to prove complete applicability before claims.

Append nullable `string? ComplicationId` to the existing private `IntentSeal`, after
`RecoveryPoints`. Populate and compare it from `MortalWoundRemoveComplicationOutcomeIntent`;
add the same exact ordinal comparison to `OrderedIntentsAgree`. Add an internal method
returning a fresh ordered array of the removal IDs from these private immutable seals.
Do not expose a mutable collection or recover IDs by re-reading raw route JSON.

Retain `resolution.Coordinates.EventRef` privately in the preparation, copy it during
detachment, and compare it independently in `AgreesWith`. Expose only a narrow internal
getter method for the effect projection. Increment preparation fingerprint3→4; append
the treatment event plus each ordered nullable removal ID to its versioned fields.
Keep the existing constructor signature; it already receives the complete resolution.

Extend the positive grammar with exact-identifier typed removal, keeping all existing
mode/category/empty/no-improvement and operation-count rules. Prepare's registered
intent switch admits removal. In `CreateScalarShell`, apply the existing pure removal
at its ordered position and reject a missing/duplicate/non-applicable target; retain
the recovery/stabilization logic and existing aggregate reduction. Upstream simulation
still validates each intermediate result, so aggregation cannot excuse an invalid order.

Define one deterministic `RequiresEffectBatch(preparation)` helper shared by all
consumers: severity projection exists OR an ordered removal ID exists. No resource
disposition, count of live effects or count of applications may replace this rule.

- [x] **Step 3: Generalize the existing effect projection and batch builder**

Factor the common `PrepareReduction` path rather than cloning it. Derive a private
projection from the authenticated preparation containing:

```text
HasReduction
EffectGraphAfterRemoval = preparation.SeverityReduction?.Before
                          ?? preparation.ProvisionalAfter
OrderedRemovedComplicationIds
OrderedSelectedRemovalRootIds
EffectProjectionFingerprint
```

Resolve each selected ID against exactly one before-image complication; take only its
declared root IDs, rejecting duplicate roots/IDs or inconsistent after-graph. Preserve
their deterministic declared ordering. Recompute the post-removal graph with the same
pure projection and compare its consequence/complication sections to the sealed
scalar/projection graph; care/course/recovery fields are independently checked by
preparation agreement. Do not trust a caller-authored fingerprint in place of these
comparisons. Keep canonical before-image,
accepted binding, identity index, owner/target and reduction recomputation checks.

For reduction-only, preserve the current severity projection fingerprint and authority
seed/seal DAG verbatim. For removal-aware results, use a private versioned domain
`book_of_eternity.mortal_wound_treatment.removal_effect_projection`, version1, covering
preparation fingerprint, optional severity projection fingerprint, ordered removal IDs,
ordered selected root IDs and canonical post-removal graph. Feed that fingerprint into
the existing `ProjectionFingerprint` slot; keep all13 authority fields and the current
rematerialization seed/topology/seal domains. Their complete revalidation stays intact.

Resolve exactly one accepted event by the preparation's treatment event, never by its
unchanged severity event. Use its actual semantic fingerprint in the source export.
Export the post-removal graph's complete definitions. Every fresh application and
terminal operation uses this exact treatment event and the original before-image owner.

| Shape | Fresh applications | Terminal roots | Final root identity behavior |
| --- | --- | --- | --- |
| remove effectless complication | none | empty | all retained IDs unchanged; authenticated0/0 batch |
| removal only | none | selected complication roots | retire only active/suspended validated closure |
| selected root already terminal | none | still select declared root | retire live descendants, never resurrect the root |
| remove plus reduction | surviving projection roots | all original wound roots | fresh surviving generations with exact prior roots |
| remove plus reduction, no survivors | none | all original wound roots | authenticated batch, no invented replacement root |

Always call the existing `WoundEffectTerminalOperationPlanner.Plan` on the original
before-image, even for empty selected roots. It validates the complete source-group
lineage and carrier/identity agreement and returns only the selected live closure.
Do not hand-traverse descendants or use the post-removal graph as teardown authority.
No still-live effect outside the terminal closure may depend on a pruned definition.
Use the existing full-lineage checks for this invariant, with a precise negative test;
add a narrow explicit postcondition only if actual source evidence exposes a gap.

Removal-only batches must include each retained **pre-turn** root whose definition
remains exported, ordered by `(EffectId, DefinitionKey)`, with null ApplicationRef
and its original ownership domain, EVEN WHEN THE TERMINAL LIST IS EMPTY. This is
pre-turn authority, not a request to keep a selected identity alive. Derive the small
row projection from already validated ownership; do not duplicate the lineage analyzer
or its validation pipeline. Mixed full-rematerialization keeps application-only lineage;
a zero-survivor mixed batch has no retained roots and therefore no lineage rows.

The old terminal-count-only gate needs a narrow extension for private-minted removal-only
treatment. The common effect builder always constructs `WoundReactionLineageAuthority`
after executing operations, even when there are no reactions. Its same-turn root set is
derived exclusively from the typed batch's lineage rows. Dropping those rows in a0/0
effectless removal would make every unrelated live effect unreachable. A zero-operation
batch therefore still needs exact retained-root authority; it is not an empty graph.

Build the same single eight-member batch and recompute source/topology/authority seals.
`Agrees` must still reproduce the exact batch/authority from original input, so omitted,
extra, reordered, foreign or changed terminal/lineage/source rows cannot borrow a seal.

- [x] **Step 4: Separate required effect work from fresh-root finalization**

Use `RequiresEffectBatch` at all three existing severity-only batch-presence seams:
`TreatmentContinuationPreparedAgrees`, `TreatmentContinuationFinalPlanAgrees`, and
`WoundAcceptedTurnPlanner`'s treatment finalization batch selection. Preserve exact
zero-or-one count, private continuation, full source/topology recomputation and all
resource/Fate/critical/lease checks. Reconstruction already reruns the same producer.
Derive required presence from the sealed operation/projection, not merely from whether
nullable `RematerializationAuthority` happened to be supplied. If the required proof
and batch are both missing, that must reject rather than become a no-effect case.

Outcome `Finalize` has three cases:

1. Reduction: require the one authentic batch and exact ordered application results,
   compare definitions against `SeverityReduction.Before` (post-removal graph), and
   use the existing `BuildFinalWound` fresh-root reconstruction.
2. Removal without reduction: require the one authentic terminal-only batch, zero
   applications/results, exact wound/source/event/definition agreement with the sealed
   after-image, and canonical equality of that after-image. Do NOT use `BuildFinalWound`,
   which reconstructs roots from applications and would delete unrelated retained roots.
3. No reduction/removal: preserve null batch plus empty application map and canonical
   provisional-after equality, including recovery-only and no-improvement behavior.

The full prepared/final continuation must reject an omitted required empty batch,
borrowed authority and changed terminal topology even if public checksums are recomputed.
The pure outcome finalizer is not a substitute for that private effect-stage agreement.

In `PrepareWoundBatchApplications`, identify removal-only from the validated private
treatment continuation and its outcome preparation, not from caller-authored transition
kind or empty operation counts alone. For that case, resolve the exact pre-turn wound
and expected-before fingerprint even with zero terminals; validate existing lineage
against its retained exported definitions. Keep the current exact ordering/domain checks
and original create/worsen/reduction-only behavior. Missing, extra or changed retained
rows must fail before any effect execution. Do not fall back to guessed roots in
`WoundReactionLineageAuthority` or substitute the final wound as pre-turn authority.

In `EffectAcceptedTurnPlanner.ValidateWoundSourceExport`, move the existing
source-event/severity-event equality into the `create` and `worsen` branches, preserving
their present restrictions. The `treat` branch must instead require the exact event
from `TryReadTreatmentContinuation`, together with the existing prepared-continuation
and expected-before checks. Keep accepted-event membership and semantic fingerprint
validation common. Never mutate severity merely to satisfy the old shared gate.

Source-backed implementation dependency discovered by actual focused RED191525:
`ValidateWoundTerminalRequests` independently selects ALL before-image root bindings,
so its exact live-closure equality rejects valid selective terminals while a base root
survives. Parent confirmed the validator, sole call and private request record. Extend
only that internal handoff: append a private immutable selected-root list to
`WoundTerminalRequest`, minted in `PrepareWoundBatchApplications` from the already
validated private removal continuation's ordered complication IDs and exact pre-turn
ownership. Reject missing/repeated/inconsistent selected complications; never derive
authority from the submitted terminal operations or fall back to full roots on failure.
All non-removal-only paths still select all original roots. The consumer reruns the
unchanged `WoundEffectLineagePlanner` for these selected roots, preserves exact ordered
active/suspended closure equality, and retains every carrier/identity/fingerprint/domain
check. This private packet is not a new public batch field or authority schema.
The initial real selected-removal owner proves this gate's RED/GREEN; retain the
planned omitted/extra/foreign terminal rejection and unrelated-base preservation.

Source-backed implementation dependency discovered by actual focused RED192609:
`ValidateGenerationPredecessors` builds its expected-coordinate map from ALL original
roots, so its exact treat bijection rejects a valid removal+reduction batch containing
only surviving coordinates. Parent confirmed the actual failure, validator and sole
call. For an already validated private treatment continuation with both sealed removal
IDs and a severity projection, pass its `SeverityReduction.Before` post-removal graph
as the expected coordinate authority. Keep the original pre-turn wound separately as
the owner/source authority and retain exact pre-turn carrier/identity predecessor
proof for every surviving coordinate. Never select expected coordinates from supplied
applications or terminals. Preserve the complete roots/predecessors bijection and
missing/extra/removed-or-foreign predecessor rejections; all create, worsen and
reduction-only paths retain their original coordinate map. This is a private validator
handoff, not a public batch/proof/schema/signature change. The real mixed-reduction
owner proves RED/GREEN; add corresponding targeted negatives to the tamper family.

Actual buffered RED193311 exposes the missing final proof check: the complete
`ValidatePreparedResult` rejects a cloned proof with changed `AttemptId` and unchanged
private `AuthoritySeal`, but `TreatmentContinuationFinalPlanAgrees` currently accepts
the forged bundle because its prepared agreement checks topology but does not rerun
the producer. Parent confirmed both validators and the real buffered failure. Whenever
`RequiresEffectBatch`, the existing final agreement seam must independently call the
existing rematerializer `Agrees` with `bundle.Input`, the exact private continuation
coordinates/preparation, selected batch and supplied proof before outcome finalization.
Preserve the cheap prepared check, null/missing requirements, all frozen shapes and
the existing negative assertions. This is an end-of-transaction proof check in the same
single-publisher architecture, not a second publication route or a generic refactor.
The effectless authenticated-batch tamper owner must prove its RED/GREEN.

### Required shared prerequisite: already-terminal retained generation

Actual195034 proves that removal+reduction with an already-terminal retained base root
is accepted by request/applicability but rejected by the common predecessor bijection.
This is an implementation gap, NOT an invalid gameplay combination: FR-027 and
`contracts/wound-effects-and-atomicity.md:264–271,534–550,557–568` require the exact
canonical old root as a historical parent and explicitly retain root bindings/slots
after independent effect removal. Parent fully read the separate source audit
`t070-terminal-generation-authority-audit.md` and independently confirmed the contract
and existing full-lineage/identity owner. No new gameplay decision is needed.

Implement one shared internal proof at the existing owner/helper:

1. Resolve and fingerprint-check the ORIGINAL pre-turn wound for every `treat|worsen`
   batch, independent of terminal-operation count. Removal's coordinate subset still
   comes only from the validated private continuation, never submitted batch roots.
2. Build/reuse `WoundEffectLineagePlanner.Plan(originalBefore, parsedIdentities)` once
   per wound per independent generation-validation stage (not once per root). Retain
   the existing separate authoritative teardown revalidation; do not remove or unify
   it merely to save a bounded traversal. The exact current canonical root/domain
   lookup plus this full validated
   lineage is historical authority; an arbitrary matching terminal source member is not.
3. Keep active/suspended `OccurrenceAndIdentityAgree` behavior. For closed terminal
   states `expired|dispelled|removed|replaced`, require successful complete lineage,
   exact bound root/domain, valid catalog and ZERO catalog occurrences. Reuse/narrowly
   expose the existing `IdentityAuthorityAgrees` for definition-derived coordinate
   agreement rather than duplicating owner/source/target/stack predicates. A compact
   immutable per-wound context may live in the existing owner; no public proof field.
4. Generic preparation and independent effect-stage validation consume the same proof.
   Invalid purported retained terminal authority is a failure, never a parentless-new
   fallback. Preserve established changed-live-stack/carrier tests as controls; do not
   redefine that separate behavior. Fresh effect identity creation records the exact
   old root only as first-create parent; the old terminal row remains immutable.
5. Retain all fork/cycle/disconnected-sibling/foreign/retired-active/reaction-lineage
   checks, exact predecessor bijection, and final rematerializer recomputation.

Keep the real four-case mixed-removal owner: live/terminal old roots crossed with
survivor/no-survivor. Terminal survivor is expected GREEN with the exact old parent,
never null; terminal no-survivor is an authenticated0/0 batch. Add a plain all-terminal
reduction owner to this Integration partial, plus terminal-survivor post-write rollback,
same-plan retry and cold replay through the real normalizer. Existing static fixture
seeding is initial history only, not evidence of a live reaction spawn.

Pure shared controls cover all closed terminal states and existing Player/Npc/
Combatant/AfterlifeGuardian owner adapters, terminal current root with live reaction
child, retired generations, mixed live/terminal roots and zero teardown. Negatives
exercise foreign source/definition/target/owner/stack/domain, disconnected terminal
sibling, fork/cycle/multiple first-create parents, later successor while stale root
is bound, active retired member, terminal carrier presence, missing/duplicate identity,
and missing/extra/borrowed predecessor. Reuse exhaustive existing analyzer owners;
add representative new generation-seam rejection tests before effect-ID allocation.

Synchronize the Mortal guide/worked example and the existing afterlife wound note:
independently suppressing an effect does not heal the wound; only a currently authorized
wound transition rematerializes a legal effect through a new identity with exact old
provenance and unchanged terminal history. Include a short worked client-lifecycle
example, not a new GM output schema. GM never authors those runtime IDs or uses a
missing-carrier fallback; this does not enable Mortal treatments or the still-pending
new healing art in afterlife.

- [x] **Step 5: Complete the actual-pipeline behavior and tamper matrix**

All new owners use `ComplicationRemovalPublication_`. Reuse the real accepted snapshot
of the canonical initial wound/effects, resource persistence, fresh rehydration, common
effect execution and coordinated final publication; compare actual canonical effect/
identity/history state, not proposed JSON. Initial effect-lineage seed evidence must
not be described as a live reaction-spawn test; these owners prove accepted treatment.

The canonical graph allows only one `apply_definition` edge globally, at severityIII
orIV, without a nested edge. Therefore use distinct valid fixtures:

- selected complication reaction root and `wound_consequence` marker child, plus one
  unrelated direct base root: two roots, two charged entries, one zero-slot child;
- effectless complication, with one unrelated base reaction root and its marker child.

For the seeded lineage fixtures, keep a reaction root's genuine owner_damaged trigger
but use a treatment event that does not damage its owner. Static direct roots and
marker children have explicitly empty triggers arrays. This isolates the operation's
lineage preservation without creating unrelated turn-end history or new reaction
descendants. Full carrier/index equality and exact retained historical prefixes remain
required; these fixtures do not claim to prove live child spawning.

Use `WoundContractTestData.CreateApplyDefinitionRoot`, `CreateOwnedEffectDefinition`
and `CreateRootBinding`; those helpers are linked through TestSupport. Do not reference
the unlinked unit class `WoundEffectLineagePlannerTests` as an Integration fixture.
Its `CreateLineageWound` is the source oracle for the permitted graph, not a new dependency.
Use complication kind `infection` for guaranteed cases: the real provider capability
already permits that kind. Do not grant a capability by changing only the authored route.

Add a closed optional `WoundLineageSeed` to `ResolverScenario`. Its test-only entries
contain exactly effect ID, definition key, nullable first-create parent effect ID and
state `active|suspended|removed`; optional defects are `None`, `MultipleFirstCreateParents`
or `TerminalForeignChild`. It must accept no raw carriers, index, authority or history.
Extract the existing single-effect construction body from `CreateCanonicalWoundEffects`
and reuse it for each seeded root/child. Require every current root exactly once and
every child's actual definition to exist. Build identities using the existing fixture
factory, then align initial chronology and first-create parent evidence from the seed.

For suspension, append a real-shaped suspend history transition and update both carrier
and identity state/last chronology. For removal, append matching remove history and
retain the terminal identity without its carrier. Before real turn preparation, install
these canonical initial effect facts through the one existing fixture seeding seam.
Do not hand-install accepted treatment history, receipts, after-images or snapshots.
Finish the scenario with the existing `CreateCurrentWoundHistory(before)` and
`SeedCanonicalWoundEffects=true` exactly like the procedure-publication setup.

Correct only the shared accepted-snapshot count assertion: compare its Effects.Count
to identity rows whose state is `active|suspended`, not all historical identities.
Terminal rows are intentionally carrierless under the production agreement contract.
Preserve snapshot acceptance and every other fixture assertion.

Apply named negative defects only to these otherwise valid initial facts. For the
multiple-parent case append one extra first-create parent. For the foreign-child case
use a removed/carrierless child and change its identity source/stack source to a foreign
wound, retaining its causal parent. A live foreign carrier could fail an earlier unrelated
source-binding gate and would not prove the intended causal-lineage rejection.

| Required owner | Required scenarios and evidence |
| --- | --- |
| `RemovesOnlyOwnedRootsAndPreservesBaseConsequence` | Initial real course RED/GREEN, exact owned-root terminals, retained base, one dose, course completion, cold replay no-op. |
| `EffectlessComplicationStillRequiresAuthenticatedEmptyBatch` | One non-effect-owning active complication plus legal base root; nonnull batch/authority,0/0 operations but exact retained-root lineage, original effects/identities unchanged. Missing batch, missing/changed retained lineage and foreign proof reject before publication, even with resealed public checksums. |
| `SelectedLineageRetiresOnlyLiveDescendants` | Two rows: selected active/suspended child under one selected reaction root plus unrelated direct base root. Exactly the root/child expire, unrelated bytes and prior history prefixes remain unchanged. |
| `TerminalOwnedRootRetiresLiveDescendantWithoutDuplicatingHistory` | Selected root removed/carrierless, child active, unrelated direct base root. Only the child receives a terminal result; the root's complete historical row remains unchanged. |
| `EffectlessRemovalPreservesUnrelatedReactionLineage` | Effectless complication plus unrelated base reaction root/child. Required batch0/0; entire effect carrier/index unchanged after real publication. This proves unrelated descendant preservation without an illegal second graph edge. |
| `MultipleComplicationsAndOrderingRemainExact` | Two distinct complications; remove either alone and both in declared order. Retain other complication and its roots when not selected. Missing/duplicate target and an invalid intermediate order reject before claims/writes; legal remove-before-reduce succeeds where reduce-first exceeds the intermediate envelope. |
| `MixedReductionRematerializesOnlySurvivingCoordinates` | Existing complete III→I remove+reduce2 course fixture, plus no-survivor legal active wound. One batch, complete old-group teardown, fresh retained base root with exact generation parent, no new removed-complication root/definition. Retain legitimate no-effect lifecycle evidence; never add a fake permanent blocker. |
| `AllSupportedModesKeepResourceAndCompletionRules` | Procedure success/partial/failed selection, guaranteed authority-backed removal, active/final course and non-course removal during an active course. Preserve real provider capability, category/completion/pointer/Fate/critical behavior and exact resource policy. Include ordered removal+recovery, removal+stabilize and a legal recovery/reduction/removal combination. |
| `LineageDisagreementRejectsBeforeWrites` | Two real accepted-treatment rows: multiple first-create parents -> `accepted_mechanics_wound_lineage_create_invalid`; terminal foreign child -> `accepted_mechanics_wound_lineage_foreign_child`. Persist/rehydrate, capture the complete tree, call `ComposeResourcePublicationResult`, require invalid/null plan/exact issue and unchanged tree. Existing pure lineage tests retain cycle/graph/owner/carrier and pruned-definition rejection coverage; do not reproduce the entire pure matrix with expensive runtime fixtures. |
| `ChangedProjectionOrBatchCannotBorrowAcceptedAuthority` | Changed removal ID/ordinal/kind/order, provisional graph, source definitions, accepted causal event, missing/extra/reordered terminal, retained ownership lineage, unexpected application, borrowed preparation/authority. Change each axis independently; include same-input detached-copy positive control and resealed public-hash attempts. |
| `PostWriteFailureRestoresWoundEffectsResourcesAndHistoryThenRetriesOnce` | Existing fault hook after a real target write and before wound-history completion. Observe hook/earlier write, exact full-tree rollback, same-plan retry, one charge/transition/terminal per selected effect, cold replay inert. |

Mixed no-survivor setup may keep actual untreated care or an independently meaningful
active complication; do not weaken the canonical zero-mechanics lifecycle rule. Tests
must demonstrate valid prerequisites and reach the intended gate, not merely fail early.

- [x] **Step 6: Extend a complete live-compatible GM example and its actual guard**

Update only the `wound_mortal_roll_scope_skill_v1` response fence in
`Examples/E_CLI_Wound_Materialization.txt`. Preserve its existing exact-skill base root
`ashglass_lockpicking_hindrance_local` and component `ashglass_lockpicking_roll` unchanged.
Add an active `impairment` complication using local ref `ashglass_fragment_local`,
display name `Осколок золостекла в порезе`, difficulty modifier1 and known visibility.
Add one complete complication-owned direct `action_control` root using the existing
definition dialect: definition ref `ashglass_fragment_grip_limit_local`, definition
key `ashglass-fragment-grip-limit`, component ID `ashglass_fragment_grip_control`,
payload `{ "action": "use_item", "operation": "restrict" }`; its one slot describes
the shard restricting grip. Reuse the existing documented definition shape and frozen
empty bounds/source-bound lifetime/independent stack/removal fields. No new profile.

Change the route's success result to:

```json
[
  { "kind": "remove_complication", "complicationRef": "ashglass_fragment_local" },
  { "kind": "stabilize" }
]
```

The two roots exactly fit severityII. Add clear prose: extraction retires the shard's
restriction, stabilization permits recovery, and the base cut's exact-skill hindrance
remains. This is not full healing. Never convert the sole old root to complication
ownership: that leaves no legal lifecycle evidence after removal+stabilization.

Extend `MortalWoundRollScopeWorkedExamples_ComposeAndBindExactSkill` without weakening
its broad/skill selector assertions. Select the original wrapper/component by the exact
refs above; the broad case remains `bell_contusion_hindrance_local` and
`bell_contusion_roll`. Preserve exact skill ID, operations, empty links, original slot,
actual skill binding, source coordinates, player-text and identity-privacy assertions.
For the skill case, additionally prove:

- the production composer rewrites both removal and ownership to the same derived
  complication ID, while raw GM JSON contains no `complicationId` or effect IDs;
- exactly one base root and one complication root are requested with correct ownership;
- the production composer intentionally clears temporary consequence bindings from
  `ProposedAfter` after parsing; obtain canonical roots through the ordinary detached
  `Prepare -> WoundEffectBatchPlanner.Build -> Finalize` path, using existing baseline/
  input helpers and independent skill authority. Do not invent accepted effect IDs or
  treat these in-memory planner results as filesystem publication;
- typed `ParseProjection` plus `WorkingWoundSimulator.Simulate` on that actual canonical
  planner after-image and the actual success
  result is applicable, removes only the complication/root, preserves the exact base
  root, compacts to one slot, stabilizes and clears `not_stabilized`;
- no test-only changes are made to the documented payload before parsing it.

Preserve all three existing frozen proposal-rewrite/confusable/canonical-ID rejection
tests. Update the existing `wound_mortal_roll_scope_skill_v1` manifest entry to describe
actual conversion, ownership and simulator applicability, not claimed live publication.
Add the selective ownership/causal closure explanation and worked-example pointer to
the Mortal guide. Update its pending producer list and scalar documentation guard to
remove only `remove_complication` from the post-recovery list; retain the three remaining
unsupported producers. The alternative-treatment draft example stays explicitly local
until its separate fresh live adapter exists.

- [x] **Step 7: Verify bounded owners, inspect evidence, commit and independent review**

Add the new Integration partial to both exact source arrays; after recovery the
reviewed-heavy source count changes61→62. Preserve class categories and historical
results when synchronizing the testing documentation/research inventory.

Use focused RED/GREEN while implementing. Once cohesive, run one combined Integration
selection with a bounded ten-minute override: all new removal owners, the existing
46-row severity preparation/finalization/rematerialization/skill-scope owners, both
roll-scope worked-example rows, the complete manifest guard, existing recovery mixed
ordering owner and scalar-course active-positive/three-stage lifecycle owners.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.ComplicationRemovalPublication_|FullyQualifiedName~MortalWoundTreatmentResolverTests.Prepare_|FullyQualifiedName~MortalWoundTreatmentResolverTests.FinalizeReduction_|FullyQualifiedName~MortalWoundTreatmentResolverTests.PrepareReductionBatch_|FullyQualifiedName~MortalWoundTreatmentResolverTests.ReductionEffectHandoff_|FullyQualifiedName~MortalWoundTreatmentResolverTests.SkillScope_AcceptedTreatmentRematerializationDoesNotRebindOrRequireProposalCoordinates|FullyQualifiedName~ExampleDocumentationValidationTests.MortalWoundRollScopeWorkedExamples_ComposeAndBindExactSkill|FullyQualifiedName~ExampleDocumentationValidationTests.CompleteEffectMaterializationManifest_CoversEveryRequiredWorkedFamily|FullyQualifiedName~MortalWoundTreatmentResolverTests.RecoveryPublication_OrderedRecoverySupportsEveryImplementedCombination|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseScalarPublication_ActivePositiveMilestonePreservesCourseAndRetainedEffectGraph|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseScalarPublication_ThreeMilestonesRestartAndCompleteWithoutHealing"
```

The existing owner names are source-checked; verify them once against the accepted
recovery HEAD before dispatch. Record the selected TRX rows, including each new method.
The override is for this combined real-publication group, based on the prior course
control's7:15.323 measurement; it is not a persistent lane change or a new Fast budget.

Run the pure shared-generation controls together with the changed documentation
guards and exact Fast inventories. This includes the complete required afterlife
documentation class because the additional correction affects shared validation:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundEffectLineagePlannerTests|FullyQualifiedName~WoundEffectBatchPlannerTests|FullyQualifiedName~WoundReactionLineageAuthorityTests|FullyQualifiedName~EffectIdentityStateTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatmentScalarCourseDocumentation_|FullyQualifiedName~FastTestBoundaryTests"
```

Then one meaningful unchanged-budget `Fast` checkpoint because the shared effect-event
gate and outcome finalizer have changed. No duplicate Fast after tiny fixes; correct a
demonstrated regression with its narrow owner. Keep the required legacy-source RED
visible if reached; a fail-fast partial Fast run is never a full success. Do not add a
fake legacy producer, skip the test or rewrite its contract to turn this stage green.
Run one `FullValidation` after the shared terminal-generation and afterlife
documentation correction, as required by AGENTS (default bounded15minutes):

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

This supersedes the original Mortal-only/no-FullValidation rationale. There are no
new afterlife fields/commands or spiritual healing rules, but the common generation
validator now implements the already approved terminal-parent rule in all realms.
No PreMerge, unrelated DeepValidation or exhaustive lifecycle lane is required here.

Report every actual RED/intermediate/GREEN artifact, exact command/filter membership,
counts/wall/exit/build/cleanup/skip/duplicate/timeout evidence, changed files, self-review
and GM/afterlife rationale. Commit only scoped changes. Parent inspects actual diffs and
artifacts and obtains independent Spec and Quality approval. No feature or issue closure.

## Parent pre-flight review — complete at `189ba934`

- Lineage fixture audit incorporated; parent verified one-edge/slot rules, canonical
  effect factory, terminal carrierlessness and the existing snapshot-count mismatch.
- Concrete final filters select unchanged actual owners at the accepted recovery HEAD.
- The outcome seal includes recovery points, preparation version3, and the exact current
  reviewed-heavy inventory is61; the next stage's62 count is verified.
- Parent source pre-check during recovery's final controls confirms every existing filter
  owner, current Prepare/Agrees signatures, the13-field rematerialization authority,
  eight-property batch and unchanged pure removal helper. The final recovery diff leaves
  these shared interfaces and all retained Integration owner files unchanged.
- The plan reuses one shared batch construction path and pure removal implementation,
  with independent finalization; implementation must preserve these boundaries and
  every existing frozen interface and assertion.
- Parent traced the mandatory post-operation reaction-lineage build and corrected the
  earlier audit's0/0-lineage omission. Independent focused source confirmation agrees;
  parent retains the stronger operation-derived required-batch predicate rather than
  using nullable proof presence as the source of whether work is required.
- No gameplay contradiction has been found: all removal behavior is already specified.
  The accepted treatment-event gate is an implementation decoupling from severity,
  not new authority or a change to the wound-severity mechanic.
- Parent checked the shared definition factory's real triggers and magnitude defaults:
  strict equality fixtures now explicitly separate passive retained consequences from
  owner_damaged reaction roots and passive marker children. No test-history filtering
  or production trigger suppression is authorized.

## Parent acceptance — 2026-09-06

Bounded task accepted at `58f29a5cd4da25a7ee3641babf7fe96b578d42c0` from
BASE `51c97c24b5cef764a47c24c9fc9d97491d2820c0`. Parent inspected the complete
22-file implementation and all24 actual C# artifact sets; fresh independent review
is Spec compliant / Task quality Approved, with no Critical or Important finding.
The report, review and exact diff package are retained in Git metadata `sdd/`.

Required Integration evidence is the exact76-row union through disjoint24/24
(`20260906-204104-611-37200-03627fd7d31b49a4bdad56f499ed8380-focused`,6:48.0597581)
and52/52 (`20260906-204805-284-36716-2599cef44a014f32940d34fd60bb03e6-focused`,4:20.0070667).
Parent independently compared both TRX sets against original discovery with ordinal
equality: missing0,extra0,overlap0. The preceding ten-minute combined timeout remains
incomplete. Relevant pure owners passed in the463-row control after its sole prose-
wrapping failure was corrected and the exact full121-row afterlife class passed.
Builds/cleanup are clean; no passing result is inferred from an unfinished lane.

Single Fast `20260906-205232-674-38928-bc049757df254d988b738319956748e1-fast` stopped
at4110/4211 due to101 failures from one unchanged snapshot fixture missing required
`skillId`. FullValidation `20260906-205503-004-40236-b759f856cd2d4d7bb0ae319354773d70-fullvalidation`
stopped at299/300 because the unchanged named-example reader included the next
level2 section in a level3 example. Parent source/BASE reproduction confirms both;
neither broad lane is certified green. Their explicit next same-branch task is
`2026-09-06-t177-verification-fixture-alignment.md`, before complication addition.

Review Minor M1 is retained: outcome finalizer line543 still describes a null batch
for unchanged severity even when removal requires a nonnull batch. Correct this
diagnostic when direct addition extends the same required-batch branch; retain it
for final whole-branch triage until verified. It does not alter acceptance behavior.
T070, T177 and #1536 remain open; no remote publication/merge/closure is authorized.
