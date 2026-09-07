#1536 / T081-B2C — source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536

Parent-inspected architecture record; implementation and all acceptance scenarios remain open.

# Spiritual effect draft and journal: causal wound insertion ordering

Metadata architecture proposal for parent review, 2026-09-08. No repository/source/test edits, C# execution, git mutation or acceptance. The approved #1536 one-turn/one-publication contract is unchanged. Brainstorming, writing-plans and the Spec Kit bridge were used to separate the already approved mechanics from the implementation choice resolved here.

## Decision

Use **one owned materialized effect draft with an execution journal and dependency-local wound barriers**. Keep ordinary deferred operations in their existing global phases. Materialize an earlier accepted effect operation before a wound insertion only when the insertion needs its actual effect/identity/stack/lineage result. A decline, prefix capture or unrelated wound does not drain ordinary effects.

The journal records actual allocated identities and exact history edits at the time a reducer applies them to the owned draft. It is not a list of instructions to reapply at completion. Final completion validates these receipts and freezes the already materialized draft once. It runs still-deferred operations and the global non-bound lifetime and terminal folds once. There is no temporary completed plan, no second allocator pass and no prefix call to CompleteAcceptedBoundaryTranscript.

**Canonical ordering is resolved as follows:**

1. Initial accepted effect-plan history is an immutable prefix.
2. Within an uncut effect dependency domain, retain the current global phase order: non-consuming triggers; nonterminal reactions/applications; replacement agreement; consuming triggers; non-bound lifecycle; terminal winner fold.
3. A real authorized wound insertion creates a causal barrier only for its computed materialization dependency closure. Before that barrier, apply the selected already-accepted operations in the same non-consuming / reaction / replacement-agreement / consumption order. Do **not** run non-bound lifecycle or terminal winner fold.
4. Apply the wound's exact ordered terminal lineage operations, then its exact ordered new-generation root applications. Record actual allocated results and predecessor identities. These operations are after the selected prefix operations and before dependent suffix operations.
5. Subsequent operations on the resulting wound generation follow that barrier. A consumed/replaced/terminated prior instance is never used as the new generation's identity.
6. At completion, run all still-deferred ordinary operations in their original relative global phase order, then one global non-bound lifetime pass, then one global terminal winner fold ordered by the existing first-release mechanics ordinal.
7. Identity-history insertion retains explicit exact transition anchors: consumption before its accepted replacement; suspended evidence before the exact earlier terminal transition when the existing fold requires it. No sort by allocation ID, event-ref text or newly invented timestamp replaces those anchors.

For a turn with no materializing/worsening insertion, there is no barrier at all: this reduces to today's complete global-phased finalizer, with exact allocation order, history, diagnostics and final state. Existing ordinary effects outside an insertion's dependency closure retain their original phase schedule. Newly affected wound generations have a genuine causal ordering that did not previously exist in the ordinary finalizer; that ordering is explicitly defined above, not silently treated as an old-run equivalence claim.

This is preferable to either (a) finalizing every exchange, which changes unrelated lifecycle/allocation order, or (b) keeping only a shadow modifier view, which cannot supply a real replacement child or the current lineage for legitimate worsening.

## Source anchors and important corrections

Anchors are in BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs unless stated otherwise. Line numbers are the read baseline; method names are authoritative when other approved work shifts lines.

- CompleteAcceptedBoundaryTranscript 2205–2283 and FinalizeAfterResourceGraphCore 2285–2725: one complete transcript, five-field plan match, one completion proof; all-release preflight and global phases.
- PrepareReleasedReactionApplicationPlans 4853–5184: freezes one pre-mutation stack catalog; groups replacement applications by complete EffectStackCoordinate; requires one common frozen target/absence; permits multiple replacements only as a consuming self-cascade from one accepted boundary; assigns FrozenAbsent, FrozenExact or PriorSelfReplacementResult links. Do not remove these checks.
- TryResolveReactionApplicationSource after 5184: exact wound producer and downstream wound source, component-selected authorization and ownership-domain validation. A new source key alone cannot authorize a child.
- ApplyApplication 6326–6590: validates bound components/skill scope and stack occupancy; calls ResolveApplication; allocates effect ID then replacement transition ID (if replacing), then create transition ID; replaces/removes actual carrier entries and returns ApplicationExecutionFacts. Non-create stack/refresh/merge results also allocate a transition, but their returned CreatedEffect/CreatedIdentityEntry/ReactionResult are null.
- ApplicationExecutionFacts 8892 and ReactionApplicationResult 8950 are useful existing facts but **not a complete journal**: they omit the removed prior carrier image, replacement transition payload, non-create updated image, full identity edits and exact history-placement anchors. Merely wrapping ReactionApplicationResult's dictionary is not the next implementation unit.
- ValidateReleasedReplacementAuthority 7493–7587 checks an actual application result, exact create provenance and exact terminal replace transition. It currently runs *before* later consumption/lifecycle/terminal folds. Therefore its 'last transition is replace' check is a point-in-execution assertion, not a rule to rerun against every final identity after later lawful operations.
- TryApplyConsumingTriggerEvidenceBeforeReplacement 7350–7490 uses the pre-reaction effect image, updates its remaining budget through AdvanceLifetime, allocates one consume transition and inserts it immediately before the exact current replace transition. It never restores that replaced effect to the carrier.
- ApplyAcceptedTerminalReactionFold 6727–6811 chooses remove over suspend within the exact one-boundary group. AppendAcceptedTerminalAfterEarlierTerminal 6814–6885 either appends remove or inserts suspend before the earlier terminal transition. HasAcceptedTerminalProjection accepts expired/dispelled/removed/replaced only when the triggering activation event is already processed.
- ApplyWoundTerminalOperation 6069–6143 requires exact carrier/identity agreement, removes the actual occurrence and appends expire. It must operate on the current draft lineage selected after relevant prefix effects, not a fabricated pre-turn image.
- PrepareWoundBatchApplications 2949–3268 explicitly uses prepared.BaselineAuthority.PreTurnCarriers for worsen/treat. This existing signed-only route must remain intact. The new live route needs a distinct draft-before authority, not passing a current image as those pre-turn carriers.
- ValidateWoundTerminalRequests 4163–4247 and ValidateWoundApplicationExecution 4251 onward pin ordered lineage, ownership, exact source/target/carrier, component materialization fingerprint and created identity. New root creation is required, not a stack/refresh result disguised as a new generation.
- Initial wound materialization around 4692–4733 applies SeverityGeneration(parentEffectId) provenance when a prior root exists. The live route reuses that relationship.
- WoundRootOwnershipDomain in WoundAcceptedTurnPlan.cs 216 is only base_wound/complication identity; it is **not globally unique by itself**. Journal affinity must also include exact wound ID and source owner. Do not use the bare BaseWound singleton as a whole-turn domain key.
- EffectResourceTriggerIndex.Build/CreateResourceTriggerIndex (723/1051), routing handle ResolvePlanSourceBinding (952), and plan-bound resource-event resolution (1997/2012/2057) currently read a fixed plan. New live routing must use a draft-owned versioned source/target/skill registry; manufacturing another EffectAcceptedTurnPlan merely to get its index is not acceptable.
- Integration owner EffectResourceTriggerRoutingScaleTests.AcceptedBoundary_TwoConsumingSameStackReplacementsRecordBothUsesBeforeOriginalReplace at 676 proves the real two-use cascade: old history ends consume, consume, replace; the intermediate identity is replaced and the final result is real. This is a required exact-equivalence control, not a new invented effect profile.

## Owned representation and typed API contract

This section defines the chosen interface/representation, not a compilable giant patch. All new types below are internal and owned by the effect draft implementation. Public callers cannot construct authority receipts or mutate retained JSON. Exact constructor visibility and producers are part of the contract.

### Journal keys and immutable payloads

- EffectDraftOperationId: readonly record struct containing long Value; allocated once from the owning draft's monotonic operation counter. It is not a permanent effect ID and is never substituted into game JSON.
- EffectDraftVersion: readonly record struct containing long Value; increments only after successful stateful application/admission, not on a read.
- EffectDraftPhase: enum NonConsumingTrigger, NonterminalReaction, ConsumingTrigger, WoundTerminal, WoundRootApplication, FinalLifetime, FinalTerminal.
- EffectDraftIdentityEditKind: enum CreateEntry, AppendTransition, InsertBeforeTransition.
- EffectDraftEligibility: enum AcceptedBaseRouting, WoundProfileRouting, DeferredReactionRouting. These are client-selected provenance categories, not caller booleans and not universal timing rules.
- EffectDraftWoundDomain: immutable record of string WoundId, EffectSourceKey OwnerSource, WoundRootOwnershipDomain Ownership. Equality includes all fields.
- EffectDraftInstanceVersion: immutable record of EffectReplayIdentity Identity and EffectDraftOperationId? LastMutation. Null LastMutation means the exact captured base image, not a missing instance.
- EffectDraftImage: sealed class storing an immutable JSON string; constructor parses exactly one JsonObject and takes its own ToJsonString image; ReadObject() returns a new parsed object. No writable JsonObject property. Null is represented by nullable EffectDraftImage, not '{}'.
- EffectDraftIdentityEdit: immutable record containing EffectDraftIdentityEditKind Kind, string EffectId, string? AnchorTransitionId, string? ResultState, EffectDraftImage Payload. CreateEntry contains a whole actual identity entry; Append/Insert contain an actual already allocated transition. InsertBefore requires an exact unique anchor ID in the same entry.
- EffectDraftCarrierEdit: immutable record containing EffectReplayIdentity Subject, EffectCarrierCoordinate Carrier, EffectDraftImage? Before, EffectDraftImage? After. Before=null means actual creation; After=null means actual removal. The target/source/carrier agreement is validated when the operation executes, not inferred from this record.
- EffectDraftMaterializationReceipt: sealed, private constructor; read-only properties OperationId, Phase, VersionBefore, VersionAfter, AcceptedActivationOrdinal (long?), ReleasedMechanicsOrdinal (long?), ApplicationEventRef (string), DependencyOperationIds (ImmutableArray<EffectDraftOperationId>), AllocatedEffectIds/AllocatedTransitionIds (ImmutableArray<string>), CarrierEdits/IdentityEdits (ImmutableArray of the above), ApplicationFacts (owned detached representation of all ApplicationExecutionFacts), SourceBindings (immutable captured source entries), Targets (ImmutableArray<EffectTargetKey>), SkillScopeFingerprint (string), and Before/After fingerprints. Only successful owned reducer execution may mint it.

The receipt retains the allocation **sequence**, separately from identity-history placement. For replacement, allocate child ID / replace ID / create ID in today's order; later consume allocation remains later in the allocation sequence even though its history edit is InsertBeforeTransition(replaceId). There is no requirement that numeric or lexical IDs match canonical history order.

ApplicationFacts must be extended from the current private record to include actual non-create updated images and every affected identity/carrier edit. Do not reconstruct non-create results later by re-running ResolveApplication or by inspecting an unrelated final carrier.

### Wound admission and baseline authority

EffectDraftFrontier is a sealed private-constructed capability containing the owner session identity, original EffectAcceptedPlanAuthorityStamp, resource closed-prefix fingerprint and exact observed mechanics frontier, draft version, journal prefix fingerprint and exact selected exchange/side coordinates. It contains no IsValid flag. Capture requires the parent-accepted resource/effect/source prefix boundary; a generic checkpoint is insufficient.

EffectDraftWoundBeforeAuthority is a sealed private-constructed capability containing that frontier, exact WoundMaterializationEnvelope before image (nullable for new creation), exact retained effect/identity/lineage images and original signed-root provenance. It is minted only after the necessary dependency closure has actually advanced. It is distinct from WoundPreparedBaselineAuthority.

EffectDraftWoundInsertion is a sealed private-constructed capability containing the original source-admission receipt, decision batch identity, draft-before authority, complete prepared wound operation batch/source exports, source/target/skill binding images and exact generation predecessor mapping. Its constructor is accessible only to the existing wound preparation owner after it has verified the source/decision and current draft-before authority. The source-admission receipt type is the parent's loader contract; this journal does not mint it from a hash or from WoundAcceptedEventAuthority.

Required exact member signatures on the future owned draft:

```csharp
internal static EffectAcceptedDraft Begin(
    EffectAcceptedTurnPlan acceptedBase,
    EffectIdentityFactory identityFactory);

internal EffectDraftRoutingImage CaptureRoutingImage();

internal EffectDraftWoundBeforeResult AdvanceForWoundInsertion(
    EffectDraftFrontier frontier,
    EffectDraftWoundSelection selection);

internal EffectDraftInsertionResult ApplyWoundInsertion(
    EffectDraftWoundInsertion insertion);

internal EffectAcceptedTurnPlanningResult Complete(
    AcceptedEffectBoundaryTranscript completeTranscript);

public void Dispose();
```

Definitions for the result/selection/image types, to prevent an undefined 'validity token' seam:

- EffectDraftWoundSelection: private-constructed, parsed/authorized selection of exact new wound local reference or existing WoundId, proposed source-owned definition/target keys and selected re-trauma operation kind. Produced by the wound response owner from the authenticated opportunity plus structurally/source-valid decision; not from arbitrary target IDs.
- EffectDraftWoundBeforeResult: record with EffectDraftWoundBeforeAuthority? Before and IReadOnlyList<ValidationIssue> Issues. Failure has Before=null.
- EffectDraftInsertionResult: abstract record with two sealed derived records. EffectDraftInsertionApplied contains EffectDraftVersion Version, ImmutableArray<EffectDraftMaterializationReceipt> Receipts and ImmutableArray<WoundApplicationRootEffectBinding> RootBindings. EffectDraftInsertionRejected contains ImmutableArray<ValidationIssue> Issues only. Failure cannot export a new version or root binding; there is no Version=0 sentinel or caller validity flag.
- EffectDraftRoutingImage: private-constructed immutable image containing original base stamp, draft version, exact materialized-instance/source/target/skill versions, eligible carrier shapes, retired-instance set and creating-operation references. It is not a complete plan and has no completion proof. All readable JSON is cloned.
- EffectAcceptedDraft owns a private selection/frontier validation method that checks the exact owning object chain and fingerprints against its stored immutable origin; matching caller-provided hashes do not recreate the objects.

The interface intentionally does not define a transport/source loader implementation inside this artifact. Parent owns that boundary. Its concrete capability type must be imported, not replaced with bool accepted or a freely constructible record. The journal implementation has a precise required dependency: a real source-bound selection and admitted prepared insertion tied to its own draft-before version.

### Single ownership/lifetime

The resource execution session and effect draft belong to one common turn owner. The resource session remains the sole owner of the use arbiter, candidate duplicate set, released expansion state and transcript ordinals. The effect draft is the sole owner of effect materialization, actual allocated effect IDs, identity edits, processed effect events and source/target/skill versions.

Accepted activations/releases are imported by exact source-owned references/cursors, once. Duplicate import is detected by typed activation/release identity; it neither consumes uses again nor re-releases a reaction. No caller may replace the arbiter or rewrite accepted transcript entries.

Actual new wound instances require a new internal RegisterMaterializedInstance operation on the retained arbiter/routing owner: it derives the canonical use seed from the validated actual created effect, checks a never-before-registered typed identity and creating receipt, and adds only that new identity's budget. It never resets an existing effect ID's remaining use seed. The current Initialize-only API does not already supply this operation.

Abandonment disposes the unpublished draft. A failed materialization faults that operation/session according to the parent repair contract; it cannot be marked successfully applied. Already successful journal entries are not reapplied during a retry. Cold recovery must reconstruct and verify the operation chain from original signed and retained candidate evidence under the parent's retained-identity contract; deserializing these records alone is not authentication.

## Dependency-local cut algorithm

This is the chosen executable algorithm for AdvanceForWoundInsertion, not 'flush whatever seems related'.

1. Validate the owning frontier and structurally/source-valid wound selection without allocating identities. Freeze the selected existing wound occurrence or the exact new-wound source/target descriptor.
2. Build the initial read/write footprint from the selected roots and first-create descendants, the source-owned stack coordinates of all proposed root applications, and all current occupants of those exact coordinates. Include the full exact wound ID/domain in lineage keys.
3. Find already accepted, not yet materialized operations whose write set intersects that footprint: target instance/carrier, complete stack coordinate, lineage producer/result, source-binding generation or identity entry. Add their producer/application prerequisites, their actual accepted consumption/non-consuming evidence and any earlier operation whose output their preflight reads. Repeat until fixed point.
4. A replacement-coordinate batch is indivisible: include all already accepted applications in its current frozen coordinate epoch, preserving the same-boundary consuming self-cascade restriction. Never take only the first of two same-boundary replacements. Include all accepted uses of an instance that the cut will replace/retire, so no earlier consumption is left to run against a missing instance.
5. Prepare those reaction application batches against their stored immutable epoch-start catalogs. Do not change a frozen target merely because another current image happens to be convenient. Validate every dependency before its allocator call; prepared candidates retain exact source definition, ownership domain, target, parameters and skill scope.
6. Execute the selected closure in today's phase order N → R → replacement agreement → U. Within N/U use increasing accepted activation ordinal; within R use increasing released mechanics ordinal and existing deterministic tie-breaking. Apply each operation once and journal its actual result. Leave unrelated operations queued.
7. Capture current wound/identity/carrier lineage from this real materialized draft, including new reaction descendants and retired ancestors. Mint EffectDraftWoundBeforeAuthority from it and the unchanged original source frontier.
8. Wound preparation uses that distinct authority to build/validate its terminal/root operations. Apply terminal operations in their existing OperationOrdinal order, then root applications in the prepared root order. Atomically publish the successful insertion's new draft version/routing image to the common turn owner, not to the filesystem.
9. Dependent suffix preparation sees that routing image. No already accepted operation or original source/dice/resource coordinate is recomposed. Decline bypasses steps 2–8 entirely; it records only its explicit source decision under the parent owner.

The closure is computed by the client from typed operation read/write sets and exact lineage, not an arbitrary caller callback. Effects on the same actor but different source/stack/lineage do not automatically enter the closure. Conversely an actual cross-identity replacement dependency cannot be left queued merely because its producer has another convenient label.

A new wound normally has an empty prior footprint, so its application can proceed without forcing unrelated old reactions. Worsening is the important nonempty case: pending accepted descendants must be materialized before selecting teardown. If the closure cannot be closed because an actual required resource receipt is unresolved, retain that real pending prerequisite; do not misclassify the wound source as ineligible.

## Production route through the existing effect methods

### PrepareReleasedReactionApplicationPlans

Retain the old whole-turn call unchanged for the no-insertion route. In the draft route, extract its pure preflight into a batch preparation result that owns its exact frozen catalog/source/lineage/skill images and complete ordered releases. Each coordinate has an epoch, closed only by an actual stateful wound barrier affecting that coordinate.

Append admission to an open epoch must re-check the full coordinate batch constraints against its original snapshot. It must not silently split a conflicting ordinary batch into several legal-looking one-reaction batches. A real wound barrier terminates that epoch only after its accepted prefix operations execute; subsequent wound-generation operations bind to the new actual identities and a new epoch snapshot. This is how legal re-trauma crosses versions without weakening the old batch rules.

Do not claim the old immutable pre-turn catalog can resolve a newly created wound producer. The next epoch's source authority is an authenticated base-plus-insertion chain; TryResolveReactionApplicationSource receives the actual materialized producer and the precise downstream definition version selected by its component.

### ApplyApplication / ApplyReactionExecution

Extract each successful mutation result as a receipt while preserving execution. The initial ordinary adapter keeps calling in the same order with the same factory. The draft owns workspace/identityRoot/list state; there is no second ApplyApplication at Complete for a journaled operation.

For ApplyDefinition, retain ApplicationExecutionFacts plus complete actual edits. PriorSelfReplacementResult resolves the exact earlier successful receipt's ResultIdentity. The runtime occupancy check still examines the actual current workspace immediately before ApplyApplication. An exact frozen target, absence or prior result must agree; a caller cannot provide a target-valid flag.

PeriodicComponent remains graph-owned resource work, not a second effect application. EventOutcome stays a chronology/identity trigger operation and may remain deferred if no insertion reads its identity. Remove/suspend continue to reserve live unavailability immediately, but their canonical winner operation stays in the one global terminal fold.

### ValidateReleasedReplacementAuthority

Call the existing checks at the same logical point: after the selected reaction batch has materialized and before its consuming/retirement/lifecycle edits. Return an internal owned replacement-agreement receipt tied to the exact application results and actual create/replace transition IDs.

At final completion, validate that those exact journal entries and their dependency links remain present once. Do not rerun 'replace must be the final transition' against an identity that a later legal wound teardown or terminal remove has changed. This is not a relaxed authority rule: the original runtime assertion is retained at its original point, and final validation checks the exact anchored history plus the later authorized edits.

### TryApplyConsumingTriggerEvidenceBeforeReplacement

Extract its result into a typed consume operation whose materialized history edit is InsertBeforeTransition(originalReplaceTransitionId). Keep the immutable pre-reaction effect image and sequential budget update for multiple consuming activations. The complete replacement batch is available before this call, so the exact original replacement anchor exists.

The finalizer's existing no-insertion adapter follows the same method and allocation order. A selected pre-wound closure uses the same result once. It must not leave consumption queued until after wound teardown, and must never reinsert the same consume transition at Complete.

### Lifecycle and terminal completion

ApplyDueLifecycleEvents(false) remains one global final pass with the existing event order, current-time/scene/realm predicates and same-causal-event exemption. Initial bound continuation processing stays initial. A barrier does not invent an end-turn event.

ApplyAcceptedTerminalReactionFold remains one global pass over the complete accepted release collection with the current winner/group order. A target already expired by an authorized wound terminal operation still uses the existing AppendAcceptedTerminalAfterEarlierTerminal behavior. Suspend is inserted before that exact earlier terminal; remove appends after it. This preserves the established distinction between live unavailability and canonical terminal evidence.

After these passes: FinalizeAcceptedSpiritualConflict, ValidateAfterImages, complete transcript/base-plus-insertion authority agreement, wound root/generation/source agreement, one completed effect plan and one common plan. Keep the old CompleteAcceptedBoundaryTranscript strict entry point for old callers; add an internal owned-draft completion route that verifies the composite chain, not a changed arbitrary base plan.

## Worked execution A: ordinary consuming replacement

Use the actual existing two-consuming same-boundary fixture cited above: original O has 2 uses; reactions replace O with A, then A with B.

There is no wound barrier, so the old complete schedule runs unchanged.

- Preflight: one common frozen typed target O, one boundary, one consuming owner; first expectation FrozenExact(O), second PriorSelfReplacementResult(first event).
- Reaction execution 1 allocates A, replace(O→A), create(A). Execution 2 allocates B, replace(A→B), create(B). Both real result identities are journaled.
- Replacement agreement validates exact creates, source producer and both retired targets before later history edits.
- Consume execution 1 uses immutable O budget 2, allocates consume C1 and inserts before replace(O→A); update retained pre-reaction O budget to 1.
- Consume execution 2 uses budget 1, allocates C2 and inserts before the same original replace anchor; remove exhausted pre-reaction budget image.
- Canonical O tail is [consume C1, consume C2, replace O→A]. A tail is [create A, replace A→B]. B has its actual create. O/A stay replaced; no zero-use O is reintroduced.
- Allocation order is still A / replace OA / create A / B / replace AB / create B / C1 / C2 according to the existing factory call stream. Canonical history order is not allocation order.
- Capturing/rereading a journal and completing again performs zero extra calls; a second completion is rejected by the owning lifecycle.

No new winding of the preflight catalog, allocator or use arbiter is allowed in this case.

## Worked execution B: new wound, then suffix replacement and legitimate expiry

After exchange 1 both side branches and causal effects close. The source owner admits one wound opportunity; GM's valid materialize decision chooses an existing supported source-owned wound definition and profile. This is not a new injury roll.

- The new wound's exact source/stack coordinate has no prior occupant. Its dependency closure is empty unless the proposed coordinate genuinely reads an earlier accepted operation.
- Prepare/apply new root R with real wound ID, real effect ID/create transition, direct provenance, exact source/target/skill/materialization evidence. Journal WoundRootApplication at barrier B1.
- R's modifier enters exchange 2 only where the approved profile evaluator says it is eligible. The resource trigger registry adds R's exact use seed once if applicable. No initial-bound lifecycle pass is rerun.
- Exchange 2 legitimately accepts a consuming reaction from R with replace source policy. Its frozen target is typed R, not an original pre-turn placeholder. Preflight uses the new epoch's actual R source/lineage image.
- Reaction materialization creates S, records replace R→S and S's create with reaction-parent provenance. Consumption is inserted before the exact R replacement, using R's accepted uses-before.
- R canonical history: create R; consume; replace R→S. S's create retains R as producer. No wound-binding pointer is silently rewritten to treat S as the original root: first-create lineage follows the real descendant.
- S is not automatically admitted as a same-turn trigger. Preserve current reaction-created routing deferral. Its real state nevertheless exists for lineage and future authorized teardown.
- At final lifetime pass, if S's actual supported lifetime and an existing due lifecycle event legitimately expire it (including the same-causal-event exclusion), append S expiry once. If not due, it remains; this case does not manufacture an expiry merely to exercise the journal.
- One final wound/effect/resource/common image contains the exact R/S identities and resulting lineage. Neither exchange is rerolled and no early carrier is published.

The acceptance fixture must use an existing source definition/profile and an explicit legal due event; arbitrary caller 'eligible' or 'expired' fields are not proof.

## Worked execution C: lawful worsening/re-trauma between exchanges

Let existing wound W own generation root O (and possibly already materialized descendants). Exchange 1 accepts consumption and a lawful downstream reaction that may create/replace a descendant D. The GM selects valid worsening of W after its admitted source.

- Worsening selection names exact W and the admitted source. It does not claim that the mutable current carrier is signed pre-turn authority.
- The dependency closure includes O's relevant accepted trigger/reaction batch, complete replacement-coordinate siblings, accepted uses and lineage-producing results. Execute N/R/agreement/U once. If O was replaced, retain its exact retired identity and include the actual descendant D in the current first-create lineage.
- Build draft-before authority after this real advancement. Validate ordered active/suspended lineage against that image. Existing retired ancestors stay historical; active/suspended descendants selected by the real lineage planner are terminalized in exact operation order.
- Apply W's expire terminal operations, then create stronger generation root N using SeverityGeneration(exact prior root). Validate new root disposition is created_new_identity and its severity-bound component materialization matches the accepted prepared wound. It is not a refresh of O with its ID retained.
- W's before/after wound fingerprints and root bindings become a new authenticated insertion link. Root O's canonical history contains its accepted prefix use/replacement, then a wound expiry only if it was still an active/suspended selected occurrence. D's history includes real reaction create followed by its actual teardown expiry where selected.
- Exchange 2 sees N's actual profile-bound contribution and exact new instance budget. Retired O/D do not remain eligible. A lawful later reaction can replace N with Q in the new epoch; preflight expects typed N. Consumption appears before N→Q replacement. Q may expire at the one final due-lifetime pass if its real contract requires.
- If exchange 2 produces another lawful worsening source for W, the next insertion uses the current N/Q lineage and a new draft-before version. It does not require W to have existed unchanged in the original snapshot, does not use stale O as the predecessor, and does not reject re-trauma because the old signed-only planner cannot yet express this chain.

Queued terminal remove/suspend on an earlier O activation stays in the global fold. Its activation event was already processed by the pre-wound closure. If O is already expired/replaced, the exact existing earlier-terminal branch applies once: remove appended, or suspend inserted before its earlier terminal. That is why this design does not flush terminal winners at a wound barrier.

## Live routing is not the same as physical materialization

A real draft instance has two separate questions: does it physically exist with valid source/identity/lineage, and is a component eligible in the next event?

- AcceptedBaseRouting retains the original accepted carrier shape/source/target/skill authority used by today's turn routing, modified only by actual accepted use/terminal availability and explicit wound retirement.
- DeferredReactionRouting records real reaction-created/updated state but preserves the existing current-turn routing deferral. A forced early materialization for lineage must not accidentally grant the child new current-turn triggers or reset refreshed use budgets.
- WoundProfileRouting uses the real admitted root and the existing wound/component/lifetime/event scope rules for its first eligible later exchange. No universal immediate-trigger or next-turn delay is invented.
- A retired instance is removed from eligible routing by exact typed identity, even if a legacy accepted-base image remains as immutable evidence. A newly created generation is registered as a new instance, never overlaid on that old image.
- Source/target/skill registry entries are versioned by creating operation and semantic fingerprints. A later worsening must not overwrite the earlier generation's source binding in a dictionary keyed only by EffectSourceKey. Earlier accepted evidence retains its old version; new suffix candidates bind to the new one.

This lets ordinary reaction state stay deferred where correct while providing actual state for the cases that cannot be solved by a view. It also prevents a forced lineage cut from changing unrelated ordinary modifier behavior.

## Implementation boundary and proposed executable acceptance work

Do not implement a tiny 'journal DTO' or a Dictionary wrapper as another accepted prerequisite. The next coherent production unit must own actual application/history writes and be consumed by the existing finalizer; its subsequent live insertion consumer belongs in the same planned feature slice.

### First code boundary worth implementing

Extract an owned EffectAcceptedDraft operation runner plus immutable materialization receipts from the existing N/R/U helpers, with the existing finalizer as its production Drain/Complete adapter. Capture all carrier edits, identity creates/appends/inserts and allocation order; retain exact replacement-preflight catalogs/results. The adapter runs the unchanged whole ordinary schedule. This is a real write-owning journal, not an observer; the original mutation helpers must not also write an independent state.

A complete small patch is not supplied here because current ApplicationExecutionFacts lacks the necessary edit payloads and the helpers mutate workspace, identityRoot, processedEventRefs, allocation lists and activeEffects together. Changing only the returned record would falsely claim ownership. The smallest honest code change spans ApplyApplication, both trigger-evidence helpers, ApplyReactionExecution, replacement agreement and the finalizer's ownership. It should be drafted/executed as one bounded owner refactor, not hidden in a new DTO file.

Before accepting that refactor as more than an internal substep, require the actual AdvanceForWoundInsertion → distinct draft-before preparation → ApplyWoundInsertion → next-exchange routing path. Graph suffix replacement and parent transport remain separate integrations, but the effect-side test must already consume the resulting new-generation routing image.

### Exact acceptance scenarios and assertions

The following are concrete executable test requirements, not claims of completed test code/runs.

1. Existing ordinary two-consuming replacement fixture: compare legacy golden full resource/effect images, identity entries/transition order, source/target/skill authority, allocated IDs and factory call order against the journal adapter. Assert O tail exactly consume, consume, replace and intermediate A really replaced. Use EffectResourceTriggerRoutingScaleTests' actual helper/definition; do not substitute a mock receipt.
2. Ordinary event-outcome, after-component release, remove-over-suspend, replacement-target drift and replacement self-cascade conflict: same finalizer diagnostics and allocation point as current owner tests. No journal entry exported for failed application. Exact zero new allocation on preflight-invalid batches.
3. Ordinary no-insertion with many prefix captures/declines: one global schedule, no forced materialization, identical final result and allocation stream. Decline is not a semantic barrier.
4. New wound after exchange 1: real signed source/decision fixture, real root ID/source/target/skill provenance, next-exchange modifier/resource evaluator observes its allowed contribution. Assert same original turn/dice/OD and retained accepted resource prefix.
5. New wound root consuming replacement: source-owned real profile, exact R→S result identity, consume-before-replace anchor, one create per child and one consumption allocation. Complete twice/attempt duplicate activation must not reapply or allocate.
6. New reaction child due expiry: legal existing lifecycle event distinct where required; verify created/expired identity history once, no same-event trigger, no rerun bound-continuation phase.
7. Worsening with accepted prefix reaction descendant: prove the descendant is genuinely materialized before teardown selection, then retired; new root uses exact severity-generation parent and changed bound components. Confirm the old signed-only entry still rejects an unsigned mutable before image.
8. Re-trauma twice in one logical turn: second draft-before links first insertion and current lineage; before fingerprint/selected root tampering fails; exact new generations rather than resetting one ID. A lawful source is not converted to none/repair because it is the second wound transition.
9. Previously accepted terminal suspend/remove plus later worsening: full terminal fold still occurs once at final completion; suspend-before-earlier-terminal or remove-after-earlier-terminal follows current owner behavior and keeps source transition evidence.
10. Unrelated reaction lifetime/refresh effects: wound insertion does not force their materialization or make newly materialized reaction shapes eligible during this turn. Exact per-effect history remains the ordinary global phase history.
11. Dependency closure contains two same-boundary replacements: cannot cut between them; all accepted uses are recorded before selected lineage retirement. Frozen target/provenance remains exact across cascade.
12. Composite authority negatives: wrong original stamp, foreign draft owner/version, changed candidate source definition/target/skill scope, reused prior-generation source binding, reordered insertions or omitted journal operation all fail before a completed plan. Caller hash equality cannot mint ownership.
13. Aliases/lifetime: mutating returned JSON cannot change draft receipts/source images; dispose/failed operation/repeated Complete are rejected; reading/capturing does not allocate. Final full validator still requires one complete matching transcript plus the owned insertion chain.

Place deterministic journal/reducer tests in Fast. Real accepted-state/source/turn fixtures and the existing EffectResourceTriggerRoutingScaleTests/EffectPendingWaveIntegrationTests owners are Integration. Use separate Focused project selections; do not mix their filters. The source owner must inspect docs/testing.md and use default bounded timeouts unless measurement justifies a change.

## Genuine remaining dependencies, not ordering deferrals

The journal schedule and history anchor rules are selected above. No product choice or source exclusion is left open here.

Implementation still requires the parent-owned real source-admission capability, draft-before authority extension to the wound preparation owner, incremental new-instance registration on the retained arbiter/routing registry, and graph suffix rebinding that consumes the new routing image. None exists merely because these interface names are written down. The no-insertion equivalence baseline and new real signed insertion fixtures must be executed before acceptance.

The highest-risk code boundary is separating point-in-time replacement agreement from final anchored-history verification without weakening either. The second is preserving routing deferral while an application is physically materialized early for lineage. Both have explicit adversarial cases above; neither is a reason to disable lawful worsening, replacement, zero-cost sources or effect profiles.

No new GM-facing behavior is authorized by the internal refactor alone. The actual live spiritual insertion path retains the approved #1536 prompt/docs/examples/source-guard work and one common publication. Parent retains final specification/tracking/implementation acceptance.
