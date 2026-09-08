# Effect identity-history owner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make production initial/final effect planning publish identity history and allocated IDs from one actual write owner, preserving the current game rules and validation order.

**Architecture:** An owned identity root records real create/append/anchored-insert writes and the delegated allocation stream; the existing planner helpers mutate it exclusively. The current finalizer still controls phase order and carrier/source state. This is J1 of T081-B2C, not the complete effect draft or live wound insertion.

**Tech Stack:** C#/.NET8, System.Text.Json.Nodes, existing effect/resource contracts, xUnit, bounded PowerShell7 lanes.

## Global Constraints

- Source issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), nested T081-B2C-J1. Follow constitution, specs/1536-complete-wound-materialization/{spec,plan,tasks}.md and contracts/spiritual-wound-live-turn-boundary.md.
- Scope exactly four files: new BookOfEternityClient/Services/EffectIdentityHistoryOwner.cs; BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs; new BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.IdentityHistoryOwner.cs; BookOfEternityClient.IntegrationTests/EffectResourceTriggerRoutingScaleTests.cs (one new Fact and deterministic factory only).
- Actual identity creates, appends and anchored inserts must flow through the owner; both initial and final production plan constructors consume its identity image and allocated-ID projections. No independent legacy writer, observer-only journal, copied whole-workspace diff per operation, second application or allocation pass.
- Preserve the exact current N/R/replacement-agreement/U/lifecycle/terminal phase order, replacement preflight, source/target/skill/lineage checks, initial wound processing and every existing failure stage. Allocation order is distinct from canonical history order; consumption remains inserted before the exact replacement anchor.
- Combatant target preparation remains before writer construction on the original factory. The retained factory forwards each effect/transition/resolution call exactly once; original baseline allocation lists retain their order. No disposable owner/factory may escape as a later callable plan dependency.
- Malformed/duplicate factory outputs keep their actual existing diagnostic stage. Duplicate child IDs in a two-consuming same-boundary cascade return effect_reaction_replacement_authority_invalid for the second release before consumption; malformed/duplicate transition IDs reach the existing final validator when applicable. Do not strengthen recording into an earlier uniqueness exception or swallow exceptions.
- CaptureAnchor follows the existing authority dictionary's first-entry selection, exact-single create predicate and last replacement predicate. Publish still checks unique unchanged exact anchors after full ValidateAfterImages. This agreement is identity-history evidence, not source admission.
- State-image reads and mutable operations reject a disposed owner; detached immutable receipt/allocation/ID projections remain inspectable. Publish is once-only, capture/read does not allocate, and a delegated allocator fault prevents later writes/publication while preserving the original exception.
- The complete amended companion includes13Fast rows and1Integration row. First record the genuine old-production positive/missing-type RED; also run the duplicate-child and three malformed-transition characterization rows against OLD production, then repeat them after the change. A setup/build failure is not semantic RED.
- Same E:/Games/worktrees/boe-1536-wound-materialization and branch1536-complete-wound-materialization. Apply patches with absolute worktree paths. Preserve unrelated .serena metadata without manual edits or staging. No new branch/worktree, remote mutation, issue closure or session cleanup.
- The implementer alone owns scoped C# edits and prescribed C# execution until explicit handoff. Use PowerShell7 and scripts/test-csharp.ps1 with exact separate-project Focused commands/default5m; no child Fast/PreMerge/FullValidation/full-solution run. Parent inspects actual artifacts, conducts independent review and owns the meaningful Fast checkpoint.
- No new gameplay/GM contract, schema, pending transport, source capability, spiritual-art XP object, migration, extra player turn/OD/dice/progression or safe-cycle change. Scalar spiritual arts remain0..5.
- J2 remains mandatory in the same feature slice: actual carrier/source/target/skill/processed-event/phase ownership plus real wound insertion/current-generation before authority and next-exchange routing. J1 cannot close T081-B2C, B2 or #1536. Do not add another observer-only prerequisite.
- No Mortal/afterlife prompt/example/matrix/manifest update is required for this internal behavior-preserving writer. Report an actual external behavior change instead of absorbing it; live wound insertion still requires the tracked GM synchronization.

### Task 1: Own production effect identity-history writes and prove exact ordinary behavior

**Files:** The four paths in Global Constraints. Parent owns plan/spec/task acceptance.

**Interfaces:** Consume existing EffectIdentityFactory, identity JSON, typed replacement identities and unchanged planner helper phases. Produce EffectIdentityHistoryOwner with the complete receipt/factory/image APIs implemented in the companion below; all14 typed helper changes are listed in the full design appendix.

- [ ] **Step1: Verify original contexts, stage the exact old-API subset and record the semantic reflection RED.**
- [ ] **Step2: Stage and execute the exact OLD-production collision/malformed-ID characterization rows before production changes.**
- [ ] **Step3: Apply the complete remaining tests and production companion; preserve the old checks and order.**
- [ ] **Step4: Execute all three prescribed GREEN Focused selections; inspect summaries/logs/TRX and retain failed artifacts.**
- [ ] **Step5: Self-review full scope/companion agreement, commit only four scoped files and write the report.**

The complete executable companion is `docs/superpowers/plans/2026-09-08-effect-identity-history-owner.patch`, reproduced below. It was converted mechanically from the inspected ordinary diff into apply_patch format, with blank context whitespace normalized; production/test postimages are unchanged. Do not use git apply. For RED staging extract only the named complete members from this companion. Reconcile already-staged postimages rather than duplicating declarations.

Parent preflight resolved the phrase 'disposed data reads' to the precise state-image/mutable-operation rule above, matching the complete code; immutable receipt and allocated-ID projections remain inspectable. No code behavior was changed by that wording.

**Report contract:** Status/base/finalcommit, exact files, all commands/artifacts/test counts/failures/skips/duplicate executions, warnings/errors, wall-time/bound/cleanup. Explain every code/fixture deviation and assertion change. Explicitly distinguish OLD characterization from missing-type RED and GREEN. Confirm real production owner consumption, original diagnostics/ID order, no GM update needed for J1, and all J2/source/continuation requirements still open.

## Complete design and executable test staging appendix

# Effect identity-history write-owner implementation plan

> For the implementing owner after parent inspection/tracking: use the repository's execution/TDD/review method. This is metadata plan input only; this author made no repository edits, C# runs, commits or acceptance decisions.

**Goal:** Move actual effect identity-history creation, append and anchored insertion into one owned writer, and make production plan publication consume its identity image and allocation projection.

**Architecture:** EffectIdentityHistoryOwner holds one private baseline clone, exact mutation receipts and the delegated effect-factory call stream. Shared initial-plan and ordinary-finalizer helpers receive that owner instead of writable identity JSON. The ordinary finalizer retains its existing N/R/agreement/U/lifecycle/terminal order and records replacement agreement at its existing point; publication checks the retained exact anchors without reapplying history.

**Tech stack:** Existing C# / System.Text.Json.Nodes / xUnit / repository bounded PowerShell test runner. No new package or service.

## Scope and required follow-on

This is **J1: identity-history write ownership**, not full EffectAcceptedDraft, not a materialization session, not source approval, and not T081-B2 completion. Parent explicitly accepted this smaller boundary after reading the larger journal design.

J2 remains mandatory in the same feature slice: carrier/source/target/skill/processed-event/phase-runner ownership, complete actual application carrier edits, dependency-local real wound insertion, distinct draft-before authority and an actual next-exchange routing consumer. Do not insert further observer-only prerequisites between J1 and that integrated work. The selected journal ordering in spiritual-effect-draft-journal-design.md remains binding.

The parent's source-loader note establishes real PendingTurnSnapshotReader.ReadCurrent as the later original-source capture route. This patch adds no source ingestion, transport token, signature service, signed-baseline substitution or diagnostic-frame changes.

## Complete executable companion

Apply only after parent review and after the implementing owner confirms the exact source contexts still match:

- Companion: docs/superpowers/plans/2026-09-08-effect-identity-history-owner.patch
- Add BookOfEternityClient/Services/EffectIdentityHistoryOwner.cs — complete 294-line implementation.
- Modify BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs — 14 typed helper parameters and exact mutation/read/publication routing; 61 original lines removed, 46 added.
- Add BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.IdentityHistoryOwner.cs — complete 323-line test partial, seven Facts plus six Theory rows: **13 rows**.
- Modify BookOfEternityClient.IntegrationTests/EffectResourceTriggerRoutingScaleTests.cs — one real two-consuming duplicate-child-ID regression and its deterministic finalization factory: **one additional row, 14 total**.
- EffectAcceptedTurnPlannerTests is already partial; no declaration change is required.

The companion is a complete ordinary unified patch, with real old/new contexts and all new source/test bodies. It does not contain ellipsis hunks, omitted methods, a full-file replacement or test-only production switches. It changes no resource-session file, transcript file, tracked plan, specification or project declaration. SDK file inclusion supplies the new files.

The source patch was mechanically reconstructed in memory from its original/changed line sequence: original and proposed bodies matched exactly. This is a read-only patch-shape check, **not** compilation or behavioral verification. No C#/build/test or git apply was run. Parent must inspect and execute it before acceptance.

## Production data flow and ownership

The initial plan builder and FinalizeAfterResourceGraphCore each create an owned writer for their own actual invocation. Both immediately route identityFactory through the owner's delegating factory. The finalizer supplies the previously allocated effect/transition lists as immutable baseline prefixes; the initial builder starts empty after its existing combatant-target preparation.

Both EffectAcceptedTurnPlan constructor calls consume:

- identityRoot.AllocatedEffectIds
- identityRoot.AllocatedTransitionIds
- identityRoot.Publish()

They no longer publish the old caller-mutable identity root or independently assembled allocation lists. Existing helper-local lists remain for their current mechanics bookkeeping, but the published allocation identity projection is now derived from the owner’s captured factory calls and retained baseline lists. Delegation calls the original factory exactly once at each existing allocation site; no IDs are preallocated, sorted, deduplicated or regenerated.

This necessarily routes shared initial-plan application/wound/terminal helpers too; otherwise ApplyApplication would need a nullable writer or two independent history mutation routes. Initial raw/signed/source/target/skill/bound-lifecycle validation and ordering are unchanged. There is no alternate legacy JSON-writing path behind a flag.

### Actual writes

- New identity entry: ApplyApplication calls CreateEntry, which owns the append and captures the complete created entry.
- Ordinary append: AppendIdentityTransition delegates to TryAppendTransition. The existing effect_lifecycle_identity_unresolved diagnostic stays in the same helper with the same arguments and early return.
- Consumption before replacement: TryApplyConsumingTriggerEvidenceBeforeReplacement supplies the exact old identity, last-transition index and exact replacement JSON it just checked. The owner verifies that anchor against its private current entry, then performs the insertion and captures its exact payload/index/ID.
- Suspend before an earlier terminal: AppendAcceptedTerminalAfterEarlierTerminal uses the same anchored writer. It does not turn that insert into an append or move the winner fold.
- Allocation: the owner retains effect/transition/resolution factory results in actual call order, independently from canonical history order. The current helper sites allocate effect/transition IDs only; combatant target preparation still precedes writer construction.
- Point-in-time agreement: ValidateReleasedReplacementAuthority calls RetainReplacementAgreement only in its existing successful validExpectation && validCreate && validReplacement branch. The owner captures the same exact create/replace source/result/event material. Later valid consume/suspend insertion or expiry does not invalidate an agreement merely because 'replace is last' is no longer true.

No mutation receipt samples a whole workspace before/after an operation. Create receipts retain the created identity entry; append/insert receipts retain the exact transition, state change and anchor. FindEntries clones only matching identity entries for private helper inspection. ReadEntries is used once at the existing replacement-validation phase. Whole identity ReadSnapshot copies occur only at the existing ParseIdentity/final-validation phase calls; they are not per-application generic diffing.

The owner keeps the full original identity root once, because it must publish one canonical identity index. It is not a source of accepted game authority by itself.

## Exact API and receipt semantics

All definitions and bodies are in the companion.

EffectIdentityHistoryOwner constructors:

```csharp
internal EffectIdentityHistoryOwner(JsonObject baseline, EffectIdentityFactory factory);
internal EffectIdentityHistoryOwner(
    JsonObject baseline, EffectIdentityFactory factory,
    IReadOnlyList<string> baselineEffectIds,
    IReadOnlyList<string> baselineTransitionIds);
```

Both constructors are production-consumed: initial builder uses the first; ordinary finalizer uses the second. Their inputs are cloned/copied.

Methods:

```csharp
internal JsonObject ReadSnapshot();
internal IReadOnlyList<JsonObject> ReadEntries();
internal JsonObject[] FindEntries(string effectId);
internal void CreateEntry(JsonObject entry);
internal bool TryAppendTransition(string effectId, string state, JsonObject transition);
internal void InsertBeforeTransition(
    string effectId, int anchorIndex, JsonObject expectedAnchor, JsonObject transition);
internal void RetainReplacementAgreement(
    string eventRef, EffectReplayIdentity result, EffectReplayIdentity? replaced,
    string createEventRef, string producerEffectId);
internal JsonObject Publish();
public void Dispose();
```

Read-only properties:

```csharp
internal EffectIdentityFactory Factory { get; }
internal IReadOnlyList<string> AllocatedEffectIds { get; }
internal IReadOnlyList<string> AllocatedTransitionIds { get; }
internal IReadOnlyList<EffectIdentityWriteReceipt> Writes { get; }
internal IReadOnlyList<EffectIdentityAllocationReceipt> Allocations { get; }
internal IReadOnlyList<EffectIdentityReplacementAgreement> ReplacementAgreements { get; }
```

EffectIdentityWriteReceipt records Ordinal, closed Kind, EffectId, BeforeState, AfterState, optional AnchorTransitionId/AnchorIndex/AnchorJson and exact PayloadJson. Strings are immutable; getters return copied arrays. CreateEntry payload is the whole actual created identity. Append payload is the actual appended transition. Insert payload is the actual inserted transition, and its anchor identifies the exact history location observed immediately before the write.

EffectIdentityAllocationReceipt records Ordinal, closed allocation Kind and actual returned Identity. Baseline allocated IDs are kept separately and prepended exactly as the old finalizer did. Do not confuse allocation order with canonical identity transition order.

EffectIdentityReplacementAgreement retains result/replaced typed identities, the successful write-count point, event and exact create/replace history anchors. It is an internal history agreement record, **not** a spiritual source capability or a substitute for PrepareReleasedReactionApplicationPlans/ValidateReleasedReplacementAuthority.

Publish checks that each agreed exact history anchor is still present once, marks the writer published and returns a detached root. It does not re-run application, reorder history, reallocate, replay the journal or publish files. Full ValidateAfterImages still runs immediately before production Publish. Preserve the actual existing diagnostic stage for malformed/duplicate allocator output; not every collision reaches the final canonical validator.

CaptureAnchor must select the first matching identity entry, exactly as ValidateReleasedReplacementAuthority's TryAdd dictionary does. Its create anchor keeps the existing exact-single predicate; its replace anchor uses the last matching transition, because the existing authority predicate validates the last transition rather than uniqueness across historical matching payloads. Do not introduce a Matches(effectId).Single() precondition at agreement capture. No agreement is skipped, no exception is broadly caught, and Publish's unique exact-anchor enforcement is unchanged.

In a real two-consuming same-boundary cascade O -> A -> A caused by duplicate factory output, both child entries exist at agreement validation. The first release passes against the first A entry. The second release cannot find its create event in that first entry, and old production returns effect_reaction_replacement_authority_invalid for the second event immediately after agreement validation, before any consume allocations or final ValidateAfterImages. Requiring unique entries while recording the first successful release would replace this established failed result with an earlier exception. The Integration regression freezes the old issue/event and six-call allocation prefix; it does not require a new effect_identity_duplicate_id diagnostic or move validation.

Anchor verification uses index plus expected JSON for the actual insert. It deliberately does not reject a duplicate allocated transition ID earlier than the existing final validator: an invalid colliding allocator can still produce two distinct positioned entries, and the old owning validator must diagnose that. Valid published history has unique exact IDs. Retained anchor TransitionId is nullable solely so a malformed allocator is not converted into a new earlier null exception by this recording layer; such malformed IDs cannot pass final canonical validation.

Dispose is idempotent. Publish is once-only; later writes/allocations/Publish throw InvalidOperationException. Disposed state-image reads and mutable operations throw ObjectDisposedException; immutable receipt, allocation and ID projections remain inspectable. A delegated allocator exception records no successful allocation, faults the writer and propagates the original exception. This remains a single-threaded owner under the existing synchronous planner/cache ownership; no cross-thread session/transport lifetime is introduced.

## Exact shared-method routing audit

The companion changes the identityRoot parameter from JsonObject to EffectIdentityHistoryOwner in exactly these 14 methods:

1. ApplyWoundTerminalOperation
2. ApplyTerminalOperation
3. ApplyApplication
4. ApplyDueLifecycleEvents
5. ApplyAcceptedTerminalReactionFold
6. AppendAcceptedTerminalAfterEarlierTerminal
7. HasAcceptedTerminalProjection
8. ApplyReactionExecution
9. ApplyLifecycleReduction
10. TryApplyConsumingTriggerEvidenceBeforeReplacement
11. ValidateReleasedReplacementAuthority
12. ApplyNonConsumingTriggerEvidence
13. AppendIdentityTransition
14. ValidateAfterImages

Their mechanics/source/target/lifetime/skill conditions remain unchanged. PrepareReleasedReactionApplicationPlans, runtime replacement occupancy, pre-reaction budgets and canonical phase ordering are unchanged. The changed private identity readers receive only detached targeted entries or explicit phase snapshots.

The proposed planner contains zero remaining JsonObject identityRoot parameters, zero identityRoot JSON indexers and zero direct transitions.Insert writes. The two plan constructor identity images and allocation projections are owner-derived. This audit is for the proposed code, not a claim that the repository has been changed.

## Executable TDD staging

### Step 1 — semantic RED with a real old-production positive control

Stage only these complete members from the companion's test partial, plus its existing usings/partial wrapper:

- IdentityHistoryOwner_ContractRedStartsWithRealConsumingReplacement
- IdentityOwnerReplacement
- IdentityOwnerIssues
- IdentityOwnerFactory

No new production type is statically referenced by this subset. The fixture is an exact adaptation of the owning ApplyDefinitionReaction_ExplicitReplaceClosesSameStackIdentityAndDefersReplacementEligibility scenario: real source/target catalogs, canonical effect/index, real due-resolution, real BuildResources transcript and full CompleteAcceptedBoundaryTranscript.

The test must first pass old production Success/Plan, one accepted activation/released replacement, exact consuming history and factory call order. Only then does it reflect the missing EffectIdentityHistoryOwner type. After implementation that same reflection test constructs/publishes the real owner. A bad fixture/build failure is not semantic RED.

Command, from the worktree with PowerShell 7:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~IdentityHistoryOwner_ContractRedStartsWithRealConsumingReplacement"
```

Expected before production: one test failure at the missing type assertion after the existing nonempty finalizer succeeds. If it fails earlier, fix only fixture setup or report the actual existing failure; do not waive the positive control.

Also stage the companion Integration hunk and the Fast malformed-transition theory before production. The latter needs only IdentityOwnerReplacement, IdentityOwnerIssues and IdentityOwnerFactory, already included in this old-production subset. These tests do not reference the missing owner type. Run the following old-production characterization rows, then repeat them after production; they must pass on both versions. Do not mislabel a fixture failure as RED or update their expected failure stage merely to accommodate the writer.

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IdentityHistoryOwner_TwoConsumingDuplicateChildrenKeepReplacementAuthorityFailure"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~IdentityHistoryOwner_MalformedReplacementTransitionKeepsFinalValidation"
```

The malformed theory returns null, empty or whitespace only for the first allocated replacement transition ID; create/consume IDs remain valid. All three must return final effect_identity_invalid_field with four allocator calls and no replacement-authority issue. This deliberately exercises retained nullable anchors and insertion before a malformed replacement without letting a malformed child carrier mask that path.

### Step 2 — stage all remaining tests before production

Add the full companion test file before applying production hunks. The temporarily missing owner type is a staging compile dependency, not a second semantic RED. No test-only API, observer hook or caller acceptance flag is required.

Fourteen rows across Fast and Integration:

- Reflection contract/behavior after genuine old-production consuming replacement.
- Three real production replacement rows: non-consuming, consuming last use, consuming with a remaining use. Deterministic scripted IDs prove canonical history placement and exact factory order; repeated fixture runs compare canonical identity/carrier images and allocated arrays.
- Direct owned create/replace/consume/expiry sequence: complete write receipts, exact anchor payload/index, point-in-time replacement agreement retained across later legal expiry, final canonical order and allocation receipts.
- Baseline/returned-image alias isolation, one publication, no post-publication allocation and disposal guards.
- Changed insertion anchor rejected with no mutation receipt or state change.
- Missing append target leaves writer unchanged and preserves the existing helper's diagnostic route.
- Allocator exception cannot publish or allocate again.
- Real production colliding allocator remains effect_identity_duplicate_transition validation failure, not a new history-writer exception.
- Three real production malformed replacement-transition ID rows (null, empty, whitespace) preserve final canonical effect_identity_invalid_field and the allocation prefix.
- Real Integration two-consuming duplicate-child-ID cascade preserves replacement-authority failure for the second event before consumption, rather than throwing during first-release agreement capture.

The direct writer tests are deterministic unit tests for an actual JSON write owner; they do not fabricate a signed snapshot or claim to authorize a wound/source. The replacement fixture exercises the production consumer, rather than testing only a parallel test helper.

### Step 3 — apply complete production hunks

After parent review, apply the two production-file sections of the companion. Do not overwrite unrelated edits. The source owner should recheck exact hunks against the current EffectAcceptedTurnPlanner; another agent may have shifted line numbers without changing these bodies.

Keep the existing initial and final source/target/skill validation and final canonical validation in place. Do not 'simplify' the allocator proxy into a second counter or use factory IDs generated during fixture setup as production replay.

### Step 4 — bounded verification and independent review

Read docs/testing.md first. Suggested selections, separately by physical project:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~IdentityHistoryOwner_"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests|FullyQualifiedName~EffectAcceptedTurnPlanCacheTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectResourceTriggerRoutingScaleTests.AcceptedBoundary_TwoConsumingSameStackReplacementsRecordBothUsesBeforeOriginalReplace|FullyQualifiedName~IdentityHistoryOwner_TwoConsumingDuplicateChildrenKeepReplacementAuthorityFailure|FullyQualifiedName~EffectPendingWaveIntegrationTests"
```

The last command uses actual Integration owners. Default bounded Focused timeout applies; no unmeasured expansion. Parent chooses the meaningful Fast control and inspects official summary/TRX membership and failures before accepting anything.

The existing two-consuming cascade Integration row is mandatory: the new local fixture covers one consuming replacement, not the full two-result self-cascade. Preserve all existing after-component, terminal winner, apply-definition next-turn eligibility, source/skill, initial wound and pending owners. No broad old-fixture relaxation.

### Step 5 — acceptance statement and handoff to J2

Acceptance may say: actual identity-history writes and allocation publication are owner-consumed; ordinary initial/final mechanics remain equivalent under the executed controls.

It must **not** say: complete effect draft, carrier/source journal, resumed source prefix, wound insertion, source admission, new-generation routing, cold recovery or B2 complete.

The immediate next implementation unit must add actual carrier/source/phase ownership and consume it through real prepared wound insertion/current-generation before authority/next-exchange routing, using the selected dependency-local barrier ordering. It cannot be another unused receipt/data holder.

## Risks and no-update rationale

- Shared initial-plan methods are in scope because the same ApplyApplication and lifecycle helpers write identity history there. They remain on their original validated input route; the owner does not change their signed or raw authority.
- The new owner clones the identity baseline once and captures exact mutated transition/entry payloads. Targeted entry copies for insertion/validation add bounded cost; measure the owning controls. There is no per-operation whole-carrier or whole-workspace before/after diff.
- Point-in-time agreement is checked at the original valid replacement branch, then retained by exact source/result/event anchors. Do not move that branch after consumption or require the replacement to remain the final transition forever.
- Metadata collision audit: sdd/identity-history-plan-collision-audit.md names the old/proposed source evidence and distinguishes agreement-stage duplicate-child rejection from late malformed-transition validation. The companion regressions require old-production characterization and post-change execution; source review alone is not test evidence.
- Existing helpers still own carrier mutations, source bindings, active-effect lists and processed events. J1 receipts are therefore complete **identity-write** receipts, not complete effect-operation receipts. ApplicationExecutionFacts' missing carrier edits are deliberately not papered over.
- This internal owner introduces no GM-authored command/field, gameplay capability, lifecycle timing or publication surface. No Mortal/afterlife prompt, example, manifest or runtime contract update is needed for J1. Actual live wound insertion remains subject to the already tracked #1536 GM docs/examples/source-guard requirements.
- No implementation/test result is asserted by this metadata artifact. Parent must inspect the complete companion and actual verification artifacts before tracking/dispatch/acceptance.

## Complete apply_patch companion

```diff
*** Begin Patch
*** Add File: BookOfEternityClient/Services/EffectIdentityHistoryOwner.cs
+using System.Text.Json.Nodes;
+
+namespace BookOfEternityClient.Services;
+
+internal enum EffectIdentityWriteKind
+{
+    CreateEntry,
+    AppendTransition,
+    InsertBeforeTransition
+}
+
+internal enum EffectIdentityAllocationKind
+{
+    Effect,
+    Transition,
+    Resolution
+}
+
+internal sealed record EffectIdentityAllocationReceipt(
+    long Ordinal,
+    EffectIdentityAllocationKind Kind,
+    string Identity);
+
+internal sealed record EffectIdentityWriteReceipt(
+    long Ordinal,
+    EffectIdentityWriteKind Kind,
+    string EffectId,
+    string? BeforeState,
+    string? AfterState,
+    string? AnchorTransitionId,
+    int? AnchorIndex,
+    string? AnchorJson,
+    string PayloadJson);
+
+internal sealed record EffectIdentityHistoryAnchor(
+    string EffectId,
+    string? TransitionId,
+    string TransitionJson);
+
+internal sealed class EffectIdentityReplacementAgreement
+{
+    private readonly EffectIdentityHistoryAnchor[] _anchors;
+
+    internal EffectIdentityReplacementAgreement(
+        string eventRef,
+        EffectReplayIdentity result,
+        EffectReplayIdentity? replaced,
+        long writeCount,
+        IEnumerable<EffectIdentityHistoryAnchor> anchors)
+    {
+        EventRef = eventRef;
+        Result = result;
+        Replaced = replaced;
+        WriteCount = writeCount;
+        _anchors = anchors.ToArray();
+    }
+
+    internal string EventRef { get; }
+    internal EffectReplayIdentity Result { get; }
+    internal EffectReplayIdentity? Replaced { get; }
+    internal long WriteCount { get; }
+    internal IReadOnlyList<EffectIdentityHistoryAnchor> Anchors =>
+        Array.AsReadOnly(_anchors.ToArray());
+}
+
+// Owns only identity history and the effect-factory call stream.
+// This is not a carrier/effect draft, source authority, or resumable turn.
+internal sealed class EffectIdentityHistoryOwner : IDisposable
+{
+    private readonly JsonObject _root;
+    private readonly string[] _baselineEffectIds;
+    private readonly string[] _baselineTransitionIds;
+    private readonly List<EffectIdentityWriteReceipt> _writes = new();
+    private readonly List<EffectIdentityAllocationReceipt> _allocations = new();
+    private readonly List<EffectIdentityReplacementAgreement> _agreements = new();
+    private bool _published;
+    private bool _disposed;
+    private bool _faulted;
+
+    internal EffectIdentityHistoryOwner(JsonObject baseline, EffectIdentityFactory factory)
+        : this(baseline, factory, Array.Empty<string>(), Array.Empty<string>())
+    {
+    }
+
+    internal EffectIdentityHistoryOwner(
+        JsonObject baseline,
+        EffectIdentityFactory factory,
+        IReadOnlyList<string> baselineEffectIds,
+        IReadOnlyList<string> baselineTransitionIds)
+    {
+        ArgumentNullException.ThrowIfNull(baseline);
+        ArgumentNullException.ThrowIfNull(factory);
+        ArgumentNullException.ThrowIfNull(baselineEffectIds);
+        ArgumentNullException.ThrowIfNull(baselineTransitionIds);
+        _root = baseline.DeepClone().AsObject();
+        _baselineEffectIds = baselineEffectIds.ToArray();
+        _baselineTransitionIds = baselineTransitionIds.ToArray();
+        Factory = new OwnedFactory(this, factory);
+    }
+
+    internal EffectIdentityFactory Factory { get; }
+    internal IReadOnlyList<string> AllocatedEffectIds => Array.AsReadOnly(_baselineEffectIds.Concat(
+        _allocations.Where(static value => value.Kind == EffectIdentityAllocationKind.Effect)
+            .Select(static value => value.Identity)).ToArray());
+    internal IReadOnlyList<string> AllocatedTransitionIds => Array.AsReadOnly(_baselineTransitionIds.Concat(
+        _allocations.Where(static value => value.Kind == EffectIdentityAllocationKind.Transition)
+            .Select(static value => value.Identity)).ToArray());
+    internal IReadOnlyList<EffectIdentityWriteReceipt> Writes => Array.AsReadOnly(_writes.ToArray());
+    internal IReadOnlyList<EffectIdentityAllocationReceipt> Allocations => Array.AsReadOnly(_allocations.ToArray());
+    internal IReadOnlyList<EffectIdentityReplacementAgreement> ReplacementAgreements =>
+        Array.AsReadOnly(_agreements.ToArray());
+
+    internal JsonObject ReadSnapshot()
+    {
+        EnsureNotDisposed();
+        return _root.DeepClone().AsObject();
+    }
+
+    internal IReadOnlyList<JsonObject> ReadEntries()
+    {
+        EnsureNotDisposed();
+        return Array.AsReadOnly(Entries().Select(static value => value.DeepClone().AsObject()).ToArray());
+    }
+
+    internal JsonObject[] FindEntries(string effectId)
+    {
+        EnsureNotDisposed();
+        return Matches(effectId).Select(static value => value.DeepClone().AsObject()).ToArray();
+    }
+
+    internal void CreateEntry(JsonObject entry)
+    {
+        EnsureMutable();
+        ArgumentNullException.ThrowIfNull(entry);
+        var owned = entry.DeepClone().AsObject();
+        _root["entries"]!.AsArray().Add(owned);
+        _writes.Add(new EffectIdentityWriteReceipt(
+            _writes.Count, EffectIdentityWriteKind.CreateEntry,
+            owned["effectId"]?.GetValue<string>() ?? string.Empty, null,
+            owned["state"]?.GetValue<string>(), null, null, null, owned.ToJsonString()));
+    }
+
+    internal bool TryAppendTransition(string effectId, string state, JsonObject transition)
+    {
+        EnsureMutable();
+        var matches = Matches(effectId).ToArray();
+        if (matches.Length != 1 || matches[0]["transitions"] is not JsonArray transitions)
+            return false;
+        var before = matches[0]["state"]?.GetValue<string>();
+        var owned = transition.DeepClone().AsObject();
+        matches[0]["state"] = state;
+        transitions.Add(owned);
+        _writes.Add(new EffectIdentityWriteReceipt(
+            _writes.Count, EffectIdentityWriteKind.AppendTransition,
+            effectId, before, state, null, null, null, owned.ToJsonString()));
+        return true;
+    }
+
+    // The caller supplies the exact last-transition position it just validated.
+    // Position plus payload preserves old late-validation behavior even if a
+    // deliberately colliding allocator has emitted duplicate transition IDs.
+    internal void InsertBeforeTransition(
+        string effectId, int anchorIndex, JsonObject expectedAnchor, JsonObject transition)
+    {
+        EnsureMutable();
+        var matches = Matches(effectId).ToArray();
+        if (matches.Length != 1 ||
+            matches[0]["transitions"] is not JsonArray transitions ||
+            anchorIndex < 0 || anchorIndex >= transitions.Count ||
+            transitions[anchorIndex] is not JsonObject anchor ||
+            !JsonNode.DeepEquals(anchor, expectedAnchor))
+        {
+            throw new InvalidOperationException("The validated identity-history insertion anchor changed.");
+        }
+        var state = matches[0]["state"]?.GetValue<string>();
+        var owned = transition.DeepClone().AsObject();
+        var anchorJson = anchor.ToJsonString();
+        var anchorId = anchor["transitionId"]?.GetValue<string>();
+        transitions.Insert(anchorIndex, owned);
+        _writes.Add(new EffectIdentityWriteReceipt(
+            _writes.Count, EffectIdentityWriteKind.InsertBeforeTransition,
+            effectId, state, state, anchorId, anchorIndex, anchorJson, owned.ToJsonString()));
+    }
+
+    // Called only after the existing exact replacement checks succeed.
+    // Retains their history anchors, not a caller-supplied validity flag.
+    internal void RetainReplacementAgreement(
+        string eventRef,
+        EffectReplayIdentity result,
+        EffectReplayIdentity? replaced,
+        string createEventRef,
+        string producerEffectId)
+    {
+        EnsureMutable();
+        var anchors = new List<EffectIdentityHistoryAnchor>();
+        anchors.Add(CaptureAnchor(result.EffectId, "create", createEventRef, producerEffectId, result.EffectId));
+        if (replaced != null)
+            anchors.Add(CaptureAnchor(replaced.EffectId, "replace", eventRef, replaced.EffectId, result.EffectId));
+        _agreements.Add(new EffectIdentityReplacementAgreement(
+            eventRef, result, replaced, _writes.Count, anchors));
+    }
+
+    internal JsonObject Publish()
+    {
+        EnsureMutable();
+        foreach (var agreement in _agreements)
+        {
+            foreach (var expected in agreement.Anchors)
+            {
+                var matches = Matches(expected.EffectId).ToArray();
+                if (matches.Length != 1 || matches[0]["transitions"] is not JsonArray transitions ||
+                    transitions.OfType<JsonObject>().Count(value =>
+                        string.Equals(value["transitionId"]?.GetValue<string>(),
+                            expected.TransitionId, StringComparison.Ordinal) &&
+                        JsonNode.DeepEquals(value, JsonNode.Parse(expected.TransitionJson))) != 1)
+                {
+                    throw new InvalidOperationException("An agreed replacement history anchor changed.");
+                }
+            }
+        }
+        _published = true;
+        return _root.DeepClone().AsObject();
+    }
+
+    public void Dispose() => _disposed = true;
+
+    private IEnumerable<JsonObject> Entries() =>
+        _root["entries"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();
+
+    private IEnumerable<JsonObject> Matches(string effectId) =>
+        Entries().Where(entry => string.Equals(
+            entry["effectId"]?.GetValue<string>(), effectId, StringComparison.Ordinal));
+
+    private EffectIdentityHistoryAnchor CaptureAnchor(
+        string effectId, string kind, string eventRef, string sourceEffectId, string resultEffectId)
+    {
+        // Match the existing authority dictionary's first-entry selection. A
+        // duplicate allocated effect ID must keep its existing diagnostic path.
+        var entry = Matches(effectId).First();
+        var candidates = entry["transitions"]!.AsArray().OfType<JsonObject>().Where(value =>
+            string.Equals(value["kind"]?.GetValue<string>(), kind, StringComparison.Ordinal) &&
+            string.Equals(value["eventRef"]?.GetValue<string>(), eventRef, StringComparison.Ordinal) &&
+            value["sourceEffectIds"] is JsonArray { Count: 1 } sources &&
+            string.Equals(sources[0]?.GetValue<string>(), sourceEffectId, StringComparison.Ordinal) &&
+            value["resultEffectIds"] is JsonArray { Count: 1 } results &&
+            string.Equals(results[0]?.GetValue<string>(), resultEffectId, StringComparison.Ordinal));
+        // The existing create check requires one match; replacement checks the
+        // last transition, not uniqueness among all historical replace payloads.
+        var transition = kind == "create" ? candidates.Single() : candidates.Last();
+        return new EffectIdentityHistoryAnchor(
+            effectId, transition["transitionId"]?.GetValue<string>(), transition.ToJsonString());
+    }
+
+    private string Allocate(EffectIdentityAllocationKind kind, Func<string> allocate)
+    {
+        EnsureMutable();
+        try
+        {
+            var identity = allocate();
+            _allocations.Add(new EffectIdentityAllocationReceipt(_allocations.Count, kind, identity));
+            return identity;
+        }
+        catch
+        {
+            _faulted = true;
+            throw;
+        }
+    }
+
+    private void EnsureNotDisposed()
+    {
+        if (_disposed)
+            throw new ObjectDisposedException(nameof(EffectIdentityHistoryOwner));
+    }
+
+    private void EnsureMutable()
+    {
+        EnsureNotDisposed();
+        if (_published || _faulted)
+            throw new InvalidOperationException("The identity-history owner is no longer writable.");
+    }
+
+    private sealed class OwnedFactory(
+        EffectIdentityHistoryOwner owner,
+        EffectIdentityFactory underlying) : EffectIdentityFactory
+    {
+        internal override string CreateEffectId() =>
+            owner.Allocate(EffectIdentityAllocationKind.Effect, underlying.CreateEffectId);
+        internal override string CreateTransitionId() =>
+            owner.Allocate(EffectIdentityAllocationKind.Transition, underlying.CreateTransitionId);
+        internal override string CreateResolutionId() =>
+            owner.Allocate(EffectIdentityAllocationKind.Resolution, underlying.CreateResolutionId);
+    }
+}
*** Update File: BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs
@@
         }

         var workspace = new CarrierWorkspace(plan.ResourceTriggerCarriers);
-        var identityRoot = plan.IdentityIndexAfterImage;
-        var identityState = ParseIdentity(identityRoot);
+        using var identityRoot = new EffectIdentityHistoryOwner(
+            plan.IdentityIndexAfterImage, identityFactory,
+            plan.AllocatedEffectIds, plan.AllocatedTransitionIds);
+        identityFactory = identityRoot.Factory;
+        var identityState = ParseIdentity(identityRoot.ReadSnapshot());
         issues.AddRange(identityState.Issues);
         if (identityState.State == null || issues.Count != 0)
             return Failed(issues);
@@
                 plan.SourceAuthorityFingerprint,
                 plan.TargetAuthorityFingerprint,
                 plan.AllocatedCombatantIds,
-                effectIds,
-                transitionIds,
+                identityRoot.AllocatedEffectIds,
+                identityRoot.AllocatedTransitionIds,
                 usedSources
                     .Select(static entry => entry.Key)
                     .Distinct()
@@
                 plan.CarrierBeforeImages,
                 afterImages,
                 plan.IdentityIndexBeforeImage,
-                identityRoot,
+                identityRoot.Publish(),
                 touchedPaths,
                 plan.DeletedPaths,
                 acceptedCarrierBaselines: plan.AcceptedCarrierBaselines,
@@
         var carrierCatalog = EffectCarrierCatalog.Build(carriers);
         issues.AddRange(carrierCatalog.Issues);
         var identityBeforeImage = input.PreTurnIdentityIndex?.DeepClone().AsObject();
-        var identityRoot = identityBeforeImage?.DeepClone().AsObject() ?? EmptyIdentityIndex();
-        var identityState = ParseIdentity(identityRoot);
+        using var identityRoot = new EffectIdentityHistoryOwner(identityBeforeImage ?? EmptyIdentityIndex(), identityFactory);
+        identityFactory = identityRoot.Factory;
+        var identityState = ParseIdentity(identityRoot.ReadSnapshot());
         issues.AddRange(identityState.Issues);
         if (issues.Count > 0)
             return Failed(issues);
@@
         if (issues.Count > 0)
             return Failed(issues);

-        var reactionIdentityState = ParseIdentity(identityRoot);
+        var reactionIdentityState = ParseIdentity(identityRoot.ReadSnapshot());
         issues.AddRange(reactionIdentityState.Issues);
         var reactionCarrierCatalog = EffectCarrierCatalog.Build(workspace.ToInput());
         issues.AddRange(reactionCarrierCatalog.Issues);
@@
                 input.SourceAuthority.CanonicalFingerprint,
                 targetAuthority.CanonicalFingerprint,
                 combatantIds,
-                effectIds,
-                transitionIds,
+                identityRoot.AllocatedEffectIds,
+                identityRoot.AllocatedTransitionIds,
                 usedSources.Select(static item => item.Key).ToArray(),
                 usedTargets,
                 usedSources
@@
                 carrierBeforeImages,
                 afterImages,
                 identityBeforeImage,
-                identityRoot,
+                identityRoot.Publish(),
                 touchedPaths,
                 new[] { EffectAcceptedTurnPlan.CommandPath },
                 acceptedCarrierBaselines:
@@
         WoundTerminalRequest request,
         CarrierWorkspace workspace,
         EffectIdentityState identities,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         int turn,
         List<string> transitionIds,
@@
         TerminalOperation operation,
         CarrierWorkspace workspace,
         EffectIdentityState identityState,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         int turn,
         List<string> transitionIds,
@@
         string realm,
         JsonObject eventInput,
         CarrierWorkspace workspace,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         int turn,
         List<string> effectIds,
@@
                 turn,
                 createEventRef,
                 provenance);
-            identityRoot["entries"]!.AsArray().Add(identityEntry);
+            identityRoot.CreateEntry(identityEntry);
             processedEventRefs.Add(application.EventRef);
             var reactionResult = new ReactionApplicationResult(
                 new EffectReplayIdentity(
@@
         JsonObject eventInput,
         EffectSourceAuthority? sourceAuthority,
         CarrierWorkspace workspace,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         List<string> transitionIds,
         List<JsonObject> activeEffects,
@@
         IReadOnlyList<ReleasedEffectReaction> releasedReactions,
         JsonObject eventInput,
         CarrierWorkspace workspace,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         List<string> effectIds,
         List<string> transitionIds,
@@

     private static void AppendAcceptedTerminalAfterEarlierTerminal(
         ReleasedEffectReaction released,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         List<string> transitionIds,
         HashSet<string> processedEventRefs,
@@
             EffectReactionResultBehavior.Remove);
         if (!isRemove)
         {
-            var entry = identityRoot["entries"]?.AsArray()
-                .OfType<JsonObject>()
-                .SingleOrDefault(candidate => string.Equals(
-                    candidate["effectId"]?.GetValue<string>(),
-                    reaction.EffectId,
-                    StringComparison.Ordinal));
+            var entry = identityRoot.FindEntries(reaction.EffectId).SingleOrDefault();
             if (entry?["transitions"] is not JsonArray transitions ||
                 transitions.Count == 0)
             {
@@
             }
             var suspendTransitionId = identityFactory.CreateTransitionId();
             transitionIds.Add(suspendTransitionId);
-            transitions.Insert(
+            identityRoot.InsertBeforeTransition(
+                reaction.EffectId,
                 transitions.Count - 1,
+                transitions[^1]!.AsObject(),
                 CreateTransition(
                     suspendTransitionId,
                     "suspend",
@@

     private static bool HasAcceptedTerminalProjection(
         ReleasedEffectReaction released,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         IReadOnlySet<string> processedEventRefs)
     {
         if (!processedEventRefs.Contains(
@@
         {
             return false;
         }
-        var entries = identityRoot["entries"]?.AsArray()
-            .OfType<JsonObject>()
-            .Where(entry => string.Equals(
-                entry["effectId"]?.GetValue<string>(),
-                released.Reaction.EffectId,
-                StringComparison.Ordinal))
-            .ToArray() ?? Array.Empty<JsonObject>();
+        var entries = identityRoot.FindEntries(released.Reaction.EffectId);
         if (entries.Length != 1)
             return false;
         return entries[0]["state"]?.GetValue<string>() is
@@
         string realm,
         JsonObject eventInput,
         CarrierWorkspace workspace,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         List<string> effectIds,
         List<string> transitionIds,
@@
         EffectCarrierOccurrence occurrence,
         EffectLifecycleEvent lifecycleEvent,
         CarrierWorkspace workspace,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         List<string> transitionIds,
         List<JsonObject> activeEffects,
@@
         int turn,
         Dictionary<string, JsonObject> preReactionEffects,
         IReadOnlyList<ReleasedEffectReaction> releasedReactions,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         List<string> transitionIds,
         HashSet<string> processedEventRefs,
         List<ValidationIssue> issues)
     {
-        var entries = identityRoot["entries"]?.AsArray()
-            .OfType<JsonObject>()
-            .Where(entry => string.Equals(
-                entry["effectId"]?.GetValue<string>(),
-                execution.EffectId,
-                StringComparison.Ordinal))
-            .ToArray() ?? Array.Empty<JsonObject>();
+        var entries = identityRoot.FindEntries(execution.EffectId);
         if (entries.Length != 1 ||
             !string.Equals(
                 entries[0]["state"]?.GetValue<string>(),
@@

         var transitionId = identityFactory.CreateTransitionId();
         transitionIds.Add(transitionId);
-        transitions.Insert(
+        identityRoot.InsertBeforeTransition(
+            execution.EffectId,
             transitions.Count - 1,
+            replacementTransition,
             CreateTransition(
                 transitionId,
                 "consume",
@@
         IReadOnlyList<ReleasedEffectReaction> releasedReactions,
         IReadOnlyDictionary<string, ReactionApplicationPlan> applicationPlans,
         IReadOnlyDictionary<string, ReactionApplicationResult> applicationResults,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         List<ValidationIssue> issues)
     {
         var identityEntries = new Dictionary<string, JsonObject>(
             StringComparer.Ordinal);
-        foreach (var entry in identityRoot["entries"]?.AsArray()
-                     .OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
+        foreach (var entry in identityRoot.ReadEntries())
         {
             if (TryReadExact(entry["effectId"], out var effectId))
                 identityEntries.TryAdd(effectId, entry);
@@
                       result.ResultIdentity,
                       reaction.EventRef);
             if (validExpectation && validCreate && validReplacement)
+            {
+                identityRoot.RetainReplacementAgreement(
+                    reaction.EventRef, result.ResultIdentity, expectedTarget, expectedCreateEventRef, reaction.EffectId);
                 continue;
+            }
             Add(
                 issues,
                 "effect.reactions",
@@
         EffectResourceTriggerExecution execution,
         int turn,
         CarrierWorkspace workspace,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         EffectIdentityFactory identityFactory,
         List<string> transitionIds,
         List<JsonObject> activeEffects,
@@
             StringComparison.Ordinal);

     private static void AppendIdentityTransition(
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         string effectId,
         string state,
         JsonObject transition,
         List<ValidationIssue> issues)
     {
-        var matches = identityRoot["entries"]!.AsArray()
-            .OfType<JsonObject>()
-            .Where(entry => string.Equals(
-                entry["effectId"]?.GetValue<string>(),
-                effectId,
-                StringComparison.Ordinal))
-            .ToArray();
-        if (matches.Length != 1 || matches[0]["transitions"] is not JsonArray transitions)
+        if (!identityRoot.TryAppendTransition(effectId, state, transition))
         {
             Add(
                 issues,
@@
                 effectId);
             return;
         }
-        matches[0]["state"] = state;
-        transitions.Add(transition);
     }

     private static JsonObject CreateTransition(
@@

     private static void ValidateAfterImages(
         CarrierWorkspace workspace,
-        JsonObject identityRoot,
+        EffectIdentityHistoryOwner identityRoot,
         IReadOnlyList<JsonObject> activeEffects,
         List<ValidationIssue> issues)
     {
@@
                     EffectMaterializationPhase.CanonicalActive));
         }
         issues.AddRange(EffectCarrierCatalog.Build(workspace.ToInput()).Issues);
-        issues.AddRange(ParseIdentity(identityRoot).Issues);
+        issues.AddRange(ParseIdentity(identityRoot.ReadSnapshot()).Issues);
     }

     private static void ValidateRoot(JsonObject root, List<ValidationIssue> issues)
*** Add File: BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.IdentityHistoryOwner.cs
+using System.Reflection;
+using System.Text.Json;
+using System.Text.Json.Nodes;
+using BookOfEternityClient.Services;
+using Xunit;
+
+namespace BookOfEternityClient.Tests;
+
+public sealed partial class EffectAcceptedTurnPlannerTests
+{
+    [Fact]
+    public void IdentityHistoryOwner_ContractRedStartsWithRealConsumingReplacement()
+    {
+        var production = IdentityOwnerReplacement();
+        Assert.True(production.Result.Success, IdentityOwnerIssues(production.Result));
+        var plan = Assert.IsType<EffectAcceptedTurnPlan>(production.Result.Plan);
+        var old = plan.IdentityIndexAfterImage["entries"]!.AsArray().OfType<JsonObject>()
+            .Single(value => value["effectId"]!.GetValue<string>() == production.OldId);
+        Assert.Equal(new[] { "consume", "replace" },
+            old["transitions"]!.AsArray().TakeLast(2).Select(value => value!["kind"]!.GetValue<string>()));
+        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, production.Factory.Kinds);
+
+        var type = typeof(EffectAcceptedTurnPlanner).Assembly.GetType(
+            "BookOfEternityClient.Services.EffectIdentityHistoryOwner");
+        Assert.NotNull(type); // First semantic RED only after old real production succeeds.
+        var constructor = type!.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
+            null, new[] { typeof(JsonObject), typeof(EffectIdentityFactory) }, null);
+        Assert.NotNull(constructor);
+        using var owner = Assert.IsAssignableFrom<IDisposable>(
+            constructor!.Invoke(new object[] { plan.IdentityIndexAfterImage, new IdentityOwnerFactory() }));
+        var publish = type.GetMethod("Publish", BindingFlags.Instance | BindingFlags.NonPublic);
+        Assert.NotNull(publish);
+        var image = Assert.IsType<JsonObject>(publish!.Invoke(owner, null));
+        Assert.True(JsonNode.DeepEquals(plan.IdentityIndexAfterImage, image));
+    }
+
+    [Theory]
+    [InlineData(false, 3)]
+    [InlineData(true, 1)]
+    [InlineData(true, 2)]
+    public void IdentityHistoryOwner_ProductionReplacementKeepsAllocationAndCanonicalOrder(
+        bool consumes, int uses)
+    {
+        var first = IdentityOwnerReplacement(consumes, uses);
+        var second = IdentityOwnerReplacement(consumes, uses);
+        Assert.True(first.Result.Success, IdentityOwnerIssues(first.Result));
+        Assert.True(second.Result.Success, IdentityOwnerIssues(second.Result));
+        var plan = first.Result.Plan!;
+        var other = second.Result.Plan!;
+        Assert.Equal(plan.IdentityIndexAfterImage.ToJsonString(), other.IdentityIndexAfterImage.ToJsonString());
+        Assert.Equal(plan.ResourceTriggerCarriers.PlayerEffects!.ToJsonString(),
+            other.ResourceTriggerCarriers.PlayerEffects!.ToJsonString());
+        Assert.Equal(plan.AllocatedEffectIds, other.AllocatedEffectIds);
+        Assert.Equal(plan.AllocatedTransitionIds, other.AllocatedTransitionIds);
+        Assert.Equal(first.Factory.Ids, second.Factory.Ids);
+        Assert.Equal(consumes
+            ? new[] { "effect", "transition", "transition", "transition" }
+            : new[] { "transition", "effect", "transition", "transition" }, first.Factory.Kinds);
+        var old = plan.IdentityIndexAfterImage["entries"]!.AsArray().OfType<JsonObject>()
+            .Single(value => value["effectId"]!.GetValue<string>() == first.OldId);
+        Assert.Equal(consumes ? new[] { "consume", "replace" } : new[] { "trigger", "replace" },
+            old["transitions"]!.AsArray().TakeLast(2).Select(value => value!["kind"]!.GetValue<string>()));
+        Assert.Equal(consumes ? first.Factory.Ids[3] : first.Factory.Ids[0],
+            old["transitions"]!.AsArray()[^2]!["transitionId"]!.GetValue<string>());
+        Assert.Equal(consumes ? first.Factory.Ids[1] : first.Factory.Ids[2],
+            old["transitions"]!.AsArray()[^1]!["transitionId"]!.GetValue<string>());
+    }
+
+    [Fact]
+    public void IdentityHistoryOwner_WritesExactAnchorsAndRetainsAgreementAcrossLaterExpiry()
+    {
+        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
+        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
+        var oldId = effect["effectId"]!.GetValue<string>();
+        var factory = new IdentityOwnerFactory();
+        using var owner = new EffectIdentityHistoryOwner(baseline, factory);
+        var childId = owner.Factory.CreateEffectId();
+        var replace = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "replace",
+            "turn_42:identity_owner:replace", oldId, childId);
+        var createRef = EffectAcceptedTurnPlanner.CreateApplicationTransitionEventRef(
+            "turn_42:identity_owner:replace", "replacement_result_create");
+        var create = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "create", createRef, oldId, childId);
+        Assert.True(owner.TryAppendTransition(oldId, "replaced", replace));
+        var childEntry = baseline["entries"]![0]!.DeepClone().AsObject();
+        childEntry["effectId"] = childId;
+        childEntry["state"] = "active";
+        childEntry["createdAtTurn"] = 42;
+        childEntry["transitions"] = new JsonArray(create.DeepClone());
+        owner.CreateEntry(childEntry);
+        var child = new EffectReplayIdentity(childId,
+            new ResourcePendingAuthorityBinding("accepted_application", createRef));
+        var old = new EffectReplayIdentity(oldId, new ResourcePendingAuthorityBinding("permanent", oldId));
+        owner.RetainReplacementAgreement("turn_42:identity_owner:replace", child, old, createRef, oldId);
+        var point = Assert.Single(owner.ReplacementAgreements);
+        Assert.Equal(2, point.WriteCount);
+        Assert.Equal(2, point.Anchors.Count);
+
+        var before = Assert.Single(owner.FindEntries(oldId));
+        var consume = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "consume",
+            "turn_42:identity_owner:consume", oldId, oldId);
+        var anchorIndex = before["transitions"]!.AsArray().Count - 1;
+        owner.InsertBeforeTransition(oldId, anchorIndex, replace, consume);
+        var expire = IdentityOwnerTransition(owner.Factory.CreateTransitionId(), "expire",
+            "turn_42:identity_owner:expiry", childId);
+        Assert.True(owner.TryAppendTransition(childId, "expired", expire));
+        var insertion = owner.Writes.Single(value => value.Kind == EffectIdentityWriteKind.InsertBeforeTransition);
+        Assert.Equal(replace["transitionId"]!.GetValue<string>(), insertion.AnchorTransitionId);
+        Assert.Equal(anchorIndex, insertion.AnchorIndex);
+        Assert.True(JsonNode.DeepEquals(replace, JsonNode.Parse(insertion.AnchorJson!)));
+        Assert.True(JsonNode.DeepEquals(consume, JsonNode.Parse(insertion.PayloadJson)));
+        Assert.Equal("replaced", insertion.BeforeState);
+        Assert.Equal(insertion.BeforeState, insertion.AfterState);
+        var published = owner.Publish();
+        var entries = published["entries"]!.AsArray().OfType<JsonObject>().ToArray();
+        var oldAfter = entries.Single(value => value["effectId"]!.GetValue<string>() == oldId);
+        Assert.Equal(new[] { "consume", "replace" },
+            oldAfter["transitions"]!.AsArray().TakeLast(2).Select(value => value!["kind"]!.GetValue<string>()));
+        Assert.Equal("expired", entries.Single(value => value["effectId"]!.GetValue<string>() == childId)["state"]!.GetValue<string>());
+        Assert.Equal(factory.Ids, owner.Allocations.Select(value => value.Identity));
+        Assert.Equal(new long[] { 0, 1, 2, 3 }, owner.Writes.Select(value => value.Ordinal));
+        Assert.Equal(2, point.WriteCount); // the point-in-time assertion was not silently moved to final state
+    }
+
+    [Fact]
+    public void IdentityHistoryOwner_CloneIsolationAndOnePublication()
+    {
+        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
+        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
+        var expected = baseline.ToJsonString();
+        using var owner = new EffectIdentityHistoryOwner(baseline, new IdentityOwnerFactory());
+        baseline["entries"]!.AsArray().Clear();
+        owner.ReadSnapshot()["entries"]!.AsArray().Clear();
+        Assert.Single(owner.ReadEntries())["state"] = "changed_outside_owner";
+        Assert.Single(owner.FindEntries(effect["effectId"]!.GetValue<string>()))["state"] = "changed_again";
+        Assert.Equal(expected, owner.ReadSnapshot().ToJsonString());
+        var published = owner.Publish();
+        Assert.Equal(expected, published.ToJsonString());
+        published["entries"]!.AsArray().Clear();
+        Assert.Equal(expected, owner.ReadSnapshot().ToJsonString());
+        Assert.Throws<InvalidOperationException>(() => owner.Publish());
+        Assert.Throws<InvalidOperationException>(() => owner.Factory.CreateEffectId());
+        owner.Dispose();
+        owner.Dispose();
+        Assert.Throws<ObjectDisposedException>(() => owner.ReadSnapshot());
+        Assert.Throws<ObjectDisposedException>(() => owner.Factory.CreateTransitionId());
+    }
+
+    [Fact]
+    public void IdentityHistoryOwner_RejectsChangedAnchorWithoutWriting()
+    {
+        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
+        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
+        var id = effect["effectId"]!.GetValue<string>();
+        using var owner = new EffectIdentityHistoryOwner(baseline, new IdentityOwnerFactory());
+        var before = owner.ReadSnapshot().ToJsonString();
+        var anchor = baseline["entries"]![0]!["transitions"]![0]!.DeepClone().AsObject();
+        anchor["eventRef"] = "turn_42:wrong_anchor";
+        Assert.Throws<InvalidOperationException>(() => owner.InsertBeforeTransition(id, 0, anchor,
+            IdentityOwnerTransition("effect_transition_rejected", "consume", "turn_42:consume", id, id)));
+        Assert.Empty(owner.Writes);
+        Assert.Equal(before, owner.ReadSnapshot().ToJsonString());
+    }
+
+    [Fact]
+    public void IdentityHistoryOwner_MissingAppendTargetPreservesOwnerAndExistingDiagnosticRoute()
+    {
+        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
+        var baseline = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
+        using var owner = new EffectIdentityHistoryOwner(baseline, new IdentityOwnerFactory());
+        Assert.False(owner.TryAppendTransition("effect_missing", "active",
+            IdentityOwnerTransition("effect_transition_missing", "trigger", "turn_42:missing",
+                "effect_missing", "effect_missing")));
+        Assert.Empty(owner.Writes);
+        Assert.True(JsonNode.DeepEquals(baseline, owner.Publish()));
+    }
+
+    [Fact]
+    public void IdentityHistoryOwner_AllocatorFailureCannotPublishOrAllocateAgain()
+    {
+        using var owner = new EffectIdentityHistoryOwner(
+            EffectMaterializationTestFixture.CreateIdentityIndex(),
+            new IdentityOwnerThrowingFactory());
+        Assert.Throws<IOException>(() => owner.Factory.CreateTransitionId());
+        Assert.Empty(owner.Allocations);
+        Assert.Empty(owner.Writes);
+        Assert.Throws<InvalidOperationException>(() => owner.Publish());
+        Assert.Throws<InvalidOperationException>(() => owner.Factory.CreateEffectId());
+    }
+
+    [Fact]
+    public void IdentityHistoryOwner_RealAllocatorCollisionRemainsValidationFailure()
+    {
+        var result = IdentityOwnerReplacement(collideTransitions: true);
+        Assert.False(result.Result.Success);
+        Assert.Null(result.Result.Plan);
+        Assert.Contains(result.Result.Issues, value => value.Code == "effect_identity_duplicate_transition");
+        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, result.Factory.Kinds);
+    }
+
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData(" ")]
+    public void IdentityHistoryOwner_MalformedReplacementTransitionKeepsFinalValidation(string? transitionId)
+    {
+        var result = IdentityOwnerReplacement(
+            malformedReplacementTransition: true, replacementTransitionId: transitionId);
+        Assert.False(result.Result.Success);
+        Assert.Null(result.Result.Plan);
+        Assert.Contains(result.Result.Issues, value => value.Code == "effect_identity_invalid_field");
+        Assert.DoesNotContain(result.Result.Issues,
+            value => value.Code == "effect_reaction_replacement_authority_invalid");
+        Assert.Equal(new[] { "effect", "transition", "transition", "transition" }, result.Factory.Kinds);
+    }
+
+    private static (EffectAcceptedTurnPlanningResult Result, IdentityOwnerFactory Factory, string OldId)
+        IdentityOwnerReplacement(bool consumes = true, int uses = 2, bool collideTransitions = false,
+            bool malformedReplacementTransition = false, string? replacementTransitionId = null)
+    {
+        const string rootKey = "identity_owner_reaction_root";
+        const string childKey = "identity_owner_replacement";
+        const string stackKey = "identity-owner-replacement";
+        var root = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
+        root["definitionKey"] = rootKey;
+        root["stacking"]!["stackKey"] = stackKey;
+        root["components"]![0]!["payload"]!["resultKind"] = "apply_definition";
+        root["components"]![0]!["payload"]!["definitionKey"] = childKey;
+        root["components"]![0]!["payload"]!["parameters"] = new JsonObject { ["amount"] = 3 };
+        root["components"]![0]!["payload"]!["maxExpansion"] = 2;
+        var child = EffectMaterializationTestFixture.CreateDefinition();
+        child["definitionKey"] = childKey;
+        child["stacking"]!["stackKey"] = stackKey;
+        child["stacking"]!["policy"] = "replace";
+        child["stacking"]!["maxStacks"] = 1;
+        child["stacking"]!["atMaximum"] = "no_change";
+        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "event_reaction");
+        effect["source"]!["definitionKey"] = rootKey;
+        effect["stacking"]!["stackKey"] = stackKey;
+        if (consumes)
+        {
+            root["triggers"]![0]!["consumeUses"] = true;
+            root["lifetime"] = new JsonObject
+            {
+                ["mode"] = "uses", ["initialUses"] = uses,
+                ["consumingEventTypes"] = new JsonArray("owner_damaged")
+            };
+            effect["lifetime"] = new JsonObject
+            {
+                ["mode"] = "uses", ["remainingUses"] = uses,
+                ["consumingTriggerIds"] = new JsonArray("on_owner_damaged"),
+                ["displayText"] = "Accepted uses remain"
+            };
+        }
+        effect["components"] = root["components"]!.DeepClone();
+        effect["triggers"] = root["triggers"]!.DeepClone();
+        var input = CreateReactionInput(effect, root, child);
+        var factory = new IdentityOwnerFactory(
+            collideTransitions, malformedReplacementTransition, replacementTransitionId);
+        var built = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input);
+        Assert.True(built.Success, IdentityOwnerIssues(built));
+        var plan = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);
+        Assert.Empty(factory.Ids);
+        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
+        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
+            plan, ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions), definitions);
+        Assert.Empty(due.Issues);
+        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
+        Assert.True(bootstrap.IsValid);
+        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
+        Assert.True(sources.IsValid);
+        var resources = AcceptedMechanicsPlanner.BuildResources(new AcceptedMechanicsResourceInput(
+            Turn: 42, Definitions: bootstrap.Definitions!, State: bootstrap.State!, History: bootstrap.History!,
+            Sources: sources.Catalog!, Mutations: due.Mutations,
+            InitialTriggerCandidates: due.TriggerCandidates, InitialEffectResolutionWork: due.Work,
+            EffectPlanAuthority: AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan)),
+            new AcceptedMechanicsIdentityFactory());
+        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
+        Assert.Single(resources.EffectBoundaryTranscript.AcceptedActivations);
+        Assert.Single(resources.EffectBoundaryTranscript.ReleasedReactions);
+        return (EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
+            plan, resources.EffectBoundaryTranscript, factory), factory, effect["effectId"]!.GetValue<string>());
+    }
+
+    private static JsonObject IdentityOwnerTransition(
+        string id, string kind, string eventRef, string source, params string[] results) =>
+        new()
+        {
+            ["transitionId"] = id, ["kind"] = kind, ["turn"] = 42, ["eventRef"] = eventRef,
+            ["sourceEffectIds"] = new JsonArray(JsonValue.Create(source)),
+            ["resultEffectIds"] = new JsonArray(results.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
+            ["receiptId"] = null
+        };
+
+    private static string IdentityOwnerIssues(EffectAcceptedTurnPlanningResult result) =>
+        string.Join(Environment.NewLine, result.Issues.Select(value => value.Code + ": " + value.Actual));
+
+    private sealed class IdentityOwnerFactory(bool collideTransitions = false,
+        bool malformedReplacementTransition = false, string? replacementTransitionId = null) : EffectIdentityFactory
+    {
+        internal List<string> Ids { get; } = new();
+        internal List<string> Kinds { get; } = new();
+        private int _effects;
+        private int _transitions;
+        internal override string CreateEffectId()
+        {
+            var id = "effect_identity_owner_" + ++_effects;
+            Kinds.Add("effect"); Ids.Add(id); return id;
+        }
+        internal override string CreateTransitionId()
+        {
+            _transitions++;
+            var id = "effect_transition_identity_owner_" + (collideTransitions ? 1 : _transitions);
+            if (malformedReplacementTransition && _transitions == 1)
+                id = replacementTransitionId!;
+            Kinds.Add("transition"); Ids.Add(id); return id;
+        }
+    }
+
+    private sealed class IdentityOwnerThrowingFactory : EffectIdentityFactory
+    {
+        internal override string CreateTransitionId() => throw new IOException("scripted allocator failure");
+    }
+}
*** Update File: BookOfEternityClient.IntegrationTests/EffectResourceTriggerRoutingScaleTests.cs
@@
+    [Fact]
+    public void IdentityHistoryOwner_TwoConsumingDuplicateChildrenKeepReplacementAuthorityFailure()
+    {
+        const string childKey = "identity_owner_collision_replacement";
+        var child = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
+        child["definitionKey"] = childKey;
+        child["stacking"]!["policy"] = "replace";
+        child["stacking"]!["maxStacks"] = 1;
+        child["stacking"]!["atMaximum"] = "no_change";
+        var fixture = CreateAcceptedEventBudgetFixture(
+            remainingUses: 2,
+            current: 10m,
+            reactionResultKind: "apply_definition",
+            applyDefinitionSource: child,
+            distinctReactionPerTrigger: true,
+            triggerSpecs: new[]
+            {
+                new BudgetTriggerSpec("trigger_budget_replace_10_first", "resource_damaged",
+                    Priority: 10, IncludePeriodic: false, IncludeReaction: true),
+                new BudgetTriggerSpec("trigger_budget_replace_20_second", "resource_damaged",
+                    Priority: 20, IncludePeriodic: false, IncludeReaction: true)
+            });
+        var damage = CreateBudgetMutation(fixture.Coordinate,
+            "turn_43:budget:identity_owner_duplicate_children", ResourceOperation.Damage, amount: 2m);
+        var factory = new IdentityOwnerDuplicateChildFactory();
+        var result = BuildAcceptedEventBudgetPlan(fixture, new[] { damage }, factory);
+        Assert.True(result.Resources.IsValid, Format(result.Resources.Issues));
+        var transcript = result.Resources.EffectBoundaryTranscript;
+        var accepted = transcript.AcceptedActivations
+            .OrderBy(value => value.Activation.Stamp.ActivationOrdinal).ToArray();
+        Assert.Equal(2, accepted.Length);
+        Assert.Single(accepted.Select(value => value.Boundary.BoundaryOrdinal).Distinct());
+        Assert.Equal(new int?[] { 2, 1 }, accepted.Select(value => value.Activation.Stamp.UsesBefore));
+        var releases = transcript.ReleasedReactions.OrderBy(value => value.MechanicsOrdinal).ToArray();
+        Assert.Equal(2, releases.Length);
+        Assert.All(releases, release =>
+        {
+            Assert.Equal("apply_definition", release.Reaction.ResultKind);
+            Assert.Equal(childKey, release.Reaction.DownstreamSourceKey?.DefinitionKey);
+        });
+        var finalized = Assert.IsType<EffectAcceptedTurnPlanningResult>(result.Finalized);
+        Assert.False(finalized.Success);
+        Assert.Null(finalized.Plan);
+        var issue = Assert.Single(finalized.Issues);
+        Assert.Equal("effect_reaction_replacement_authority_invalid", issue.Code);
+        Assert.Equal(releases[1].Reaction.EventRef, issue.Actual);
+        // First release passed agreement; second failed there. No consume allocation
+        // or final canonical validation occurred, and no writer exception escaped.
+        Assert.Equal(new[] { "effect", "transition", "transition", "effect", "transition", "transition" },
+            factory.Kinds);
+        Assert.Equal(new[] { "effect_identity_owner_duplicate_child", "effect_identity_owner_duplicate_child" },
+            factory.EffectIds);
+    }
+
+    private sealed class IdentityOwnerDuplicateChildFactory : EffectIdentityFactory
+    {
+        internal List<string> Kinds { get; } = new();
+        internal List<string> EffectIds { get; } = new();
+        private int _transitions;
+        internal override string CreateEffectId()
+        {
+            const string id = "effect_identity_owner_duplicate_child";
+            Kinds.Add("effect");
+            EffectIds.Add(id);
+            return id;
+        }
+        internal override string CreateTransitionId()
+        {
+            Kinds.Add("transition");
+            return "effect_transition_identity_owner_duplicate_child_" + ++_transitions;
+        }
+    }
+
     [Fact]
     public void AcceptedBoundary_TwoConsumingSameStackReplacementsRecordBothUsesBeforeOriginalReplace()
*** End Patch
```
