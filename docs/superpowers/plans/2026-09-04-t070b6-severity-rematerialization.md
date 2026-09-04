# T070-B.6 Severity Rematerialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish accepted Mortal `reduce_severity` treatment outcomes atomically through the existing common wound/effect/resource transaction, replacing the complete old wound-owned effect group with fresh #1535 identities while preserving the authored consequence semantics.

**Architecture:** Split treatment outcome publication into a pre-effect `Prepare` stage and a post-#1535 `Finalize` stage. A shared pure lower-severity projector validates the unchanged semantic consequence graph at the destination rank before a procedure die can become reusable, while a private treatment-rematerialization authority builds one typed wound batch that expires the complete old root/descendant group before reapplying every retained root. The cached #1535 plan remains the sole permanent effect-ID allocator; final wound bindings, declared outcome, reducer evidence, history, and common-plan fingerprints are derived only after its exact accepted result map is available.

**Tech Stack:** C# 12/.NET 8, immutable internal authority objects, `System.Text.Json.Nodes`, canonical file-backed JSON, xUnit, PowerShell 7 bounded test lanes.

## Global Constraints

- Tracked authority is GitHub issue #1536 and open Spec Kit task T070.
- Work only in `E:\Games\worktrees\boe-1536-wound-materialization` on `1536-complete-wound-materialization`; do not create another branch or worktree.
- Preserve the public six-argument `ComposeMortalWoundTreatmentPublication`, sealed request/resolution DTOs, and single `MortalWoundTreatmentResourceComposer.Finalize(resolution)` entry point.
- B.6 admits the existing B.5 singleton `no_improvement`/`stabilize` results plus closed ordered results containing `stabilize` and `reduce_severity`; `no_improvement` remains singleton. At most one stabilization and an aggregate reduction of one or two ranks are legal, and the result may not reduce below severity I.
- Support procedure and guaranteed modes and preserve the selected T067 category. Only a newly successful route uses `AppendOnce`; `partial_success` and failed categories do not acquire route completion merely because they improve another state axis.
- The standard successful procedure `[stabilize, reduce_severity(1)]`, singleton reductions, `reduce_severity(2)`, two ordered one-step reductions, and every legal placement of the optional stabilization among those two reductions are in scope. Apply their scalar state changes in authored ordinal order but perform one atomic teardown/rematerialization for the final destination rank.
- B.6 preserves the complete semantic definition graph, root-definition mapping, slot summaries, complication ordering/data, and root ownership domains. It never silently removes, weakens, or chooses consequences. A graph that exceeds the destination slot/power envelope is inapplicable before accepted roll consumption.
- Every severity change expires every active/suspended old wound-source root and reaction descendant, including descendants of an already terminal root, then applies every retained direct root with a fresh exact/confusable-disjoint effect identity. Unrelated effects remain byte-semantically unchanged.
- Every severity-rematerialized root that retains an existing definition/ownership coordinate, for both existing `worsen` and new `treat` paths, records its exact prior canonical root as first-create provenance. Genuinely new roots in any transition are parentless; removed old-definition history remains terminal and need not masquerade as a current generation. Repeated retained-coordinate severity changes form one sealed, acyclic, non-branching same-definition generation chain; retired generations and their reaction descendants must be terminal, while forged disconnected terminal identities whose definition remains current are invalid. This uses the existing identity history schema and requires no migration under the approved technical cutover.
- `Severity.LastChangeEventRef` becomes the exact accepted treatment event; `MaximumAtCreation`, recovery state/anchors/progress, active-course pointer, and care remain unchanged except for ordered stabilization effects and `Care.LastAttemptId`.
- Set the Mortal `Consequences.SlotBudget` to the resulting rank. `SlotsUsed`, definitions, and slot semantics remain the exact derived retained graph; a destination-rank validation failure is not repaired by pruning.
- Prepared slot ordinals are correlations, not durable identities. After #1535 allocates opaque IDs, reproduce its canonical ordering: sort accepted roots by actual `effectId`, retain within-root profile/summary order, and assign contiguous one-based final slots. No profile, summary, definition, ownership, or slot count may change under that client-owned reindexing.
- Raw GM effect commands remain empty. Root apply/expire operations are private typed wound-batch operations and are not inserted into the GM lifecycle-event array. Existing typed Fate lifecycle events remain independently sealed.
- Neither the outcome projector nor rematerialization planner allocates a permanent effect ID. Only the cached #1535 effect plan may return `applicationRef -> effectId` results.
- Do not treat `TransitionKind = "treat"` or reused opportunity-named fields as sufficient authority. Use a non-cyclic private authority DAG: an input-derived seed authenticates the structural source adapter; a topology fingerprint then binds the complete source export, ordered roots, prior-root generation links, terminal operations, lineage, and structural adapter; the final private seal binds seed plus topology. The generic create/worsen path must reject that batch without the matching continuation authority.
- Composition/planning are write-free. The sole canonical publisher remains `CanonicalStateNormalizer` under the existing coordinated treatment transaction; rollback/retry/quarantine ordering from B.5 remains intact.
- Do not widen `GameResponse`, add a persisted schema, migration/compatibility path, raw writer, second normalizer, GM-authored rematerialization payload, player command, browser/console surface, or afterlife runtime contract.
- `add_recovery`, `remove_complication`, `add_complication`, `apply_deterioration`, course publication, recovery ticks, heal/legacy, spiritual healing, and T069-C remain later contours.
- Preserve and do not stage the untracked `.serena/` directory.
- Use only `pwsh .\scripts\test-csharp.ps1`; never invoke raw or unbounded `dotnet test`. Run one lane at a time and never edit while it is active.
- Keep only fixture-free deterministic projection/authority/reducer checks in `BookOfEternityClient.Tests`. Move the complete file-backed `MortalWoundTreatmentResolverTests*` family and its registry companion to `BookOfEternityClient.IntegrationTests` under `RegressionIntegration`; extend `GameEngineTurnLifecycleTests` for full-engine publication/rollback/restart/replay workflows. `Fast` remains the entire physically isolated fast project and receives no file-backed treatment fixture.
- During implementation run the smallest owning `Focused` selection, then one meaningful `Fast` checkpoint after the deterministic contour is green. Reserve `PreMerge` for an explicitly requested integration/merge.
- Update the GM-facing Mortal wound guide with a worked route containing `[stabilize, reduce_severity(1)]`, destination-envelope rejection, client-owned identity rematerialization, and failed/partial route-completion semantics. Add a source guard proving the example parses. No afterlife matrix/example update is needed because this slice changes only the Mortal treatment contract. Leave T070/#1536 open.

## Design Choice

Use one deterministic rematerialization of the unchanged semantic graph. The rejected alternatives are: patching old effects in place, which violates FR-027 and preserves forbidden runtime identities; automatically pruning or weakening arbitrary GM-authored consequences, for which the sealed `reduce_severity { steps }` intent carries no authority; and requesting a second GM turn, which breaks the atomic treatment transaction. A later operation that intentionally changes the graph must carry its own closed typed graph authority.

---

### Task 1: Restore the Fast/Integration ownership boundary

**Files:**

- Move: every `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests*.cs` file to `BookOfEternityClient.IntegrationTests/`
- Move: `BookOfEternityClient.Tests/MortalWoundTreatmentAcceptedStateRegistryTests.cs` to `BookOfEternityClient.IntegrationTests/`
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`
- Modify: `docs/testing.md`
- Modify: `specs/1505-test-suite-performance/research.md`
- Modify: `specs/1505-test-suite-performance/data-model.md`
- Modify: `specs/1505-test-suite-performance/quickstart.md`
- Modify: `specs/1505-test-suite-performance/tasks.md`

**Boundary:**

The partial resolver fixture persists canonical state, rehydrates it, exercises leases/registries/claims, publishes through the normalizer, and starts later turns. It is therefore `RegressionIntegration`, not Fast. Move the complete partial family together; moving isolated methods would keep hidden fixture coupling and make lane ownership harder to audit. This is the approved #1551 follow-up and remains in the current #1536 branch.

- [x] **Step 1: Make the exact boundary guards RED**

Add the full explicit resolver-family list and registry companion to `FastTestBoundaryTests.ReviewedHeavySourcePaths`, which requires each file to exist exactly once under Integration and nowhere in Fast/TestSupport. Add only the primary partial source and standalone registry source to `IntegrationTestBoundaryTests.RegressionIntegrationSources`, requiring literal class-level `[Trait("Category", "RegressionIntegration")]` there; one attribute on the primary partial declaration categorizes the merged resolver type without duplicating traits across all 25 source fragments. Add a boundary assertion that the exact 25-file partial-family inventory is present. Update the exact source manifests in the #1505 research/tasks artifacts. Run:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests" -TimeoutMinutes 15
```

Expected: the new exact-location assertions fail while the old files remain in Fast; the test projects still compile and discovery is non-empty.

- [x] **Step 2: Move the fixture mechanically and categorize it**

Move all 25 `MortalWoundTreatmentResolverTests*.cs` sources and `MortalWoundTreatmentAcceptedStateRegistryTests.cs` without altering their namespace, class names, test bodies, or helper visibility. Add `RegressionIntegration` to the main partial resolver declaration and registry declaration. Do not duplicate any source across projects.

- [x] **Step 3: Document the durable lane rule**

Record in `docs/testing.md` and every #1505 exact-manifest artifact (`research.md`, `data-model.md`, `quickstart.md`, and `tasks.md`) that treatment resolver tests requiring accepted-state files, persistence, leases, resource claims, publication, restart, or replay belong to Integration. Update the executable-manifest descriptions from 32 to 58 reviewed-heavy sources and from 33 to 35 class-level RegressionIntegration sources. Fixture-free parsers, projectors, fingerprints, reducers, and pure policy tests remain in Fast. Do not raise the five-minute Fast limit and do not optimize assertions merely to save seconds.

- [x] **Step 4: Prove both projects and the retained RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests|FullyQualifiedName~MortalWoundTreatmentAcceptedStateRegistryTests" -TimeoutMinutes 15
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~ProcedureFinalization_ConsumesOnlySelectedSupplyAndReleasesEveryOtherHeldClaim" -TimeoutMinutes 15
```

Expected: boundary/registry controls pass. The retained procedure theory is discovered in Integration; its no-improvement row passes and its selected successful reduction row remains RED only at `mortal_wound_treatment_publication_slice_unsupported`.

- [x] **Step 5: Commit the lane correction**

Stage the explicit moved files and boundary/docs manifests, never `.serena/`, then commit:

```powershell
git commit -m "test(wounds): move file-backed treatment tests to integration (#1551 #1536)"
```

---

### Task 2: Add one shared destination-rank projection policy

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentSeverityReductionPlanner.cs`
- Create: `BookOfEternityClient/Services/WoundPersistedConsequenceEnvelopeAdapter.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentWorkingWoundSimulator.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentContract.WoundGraph.cs`
- Create/Test: `BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`

**Interfaces:**

- Produce:

```csharp
internal sealed class MortalWoundTreatmentSeverityReductionProjection
{
    internal WoundMaterializationEnvelope Before { get; }
    internal WoundMaterializationEnvelope ProvisionalAfter { get; }
    internal int Steps { get; }
    internal IReadOnlyList<MortalWoundTreatmentRematerializationRoot> Roots { get; }
    internal string Fingerprint { get; }
}

internal sealed record MortalWoundTreatmentRematerializationRoot(
    string PriorEffectId,
    string DefinitionKey,
    WoundRootOwnershipDomain OwnershipDomain,
    IReadOnlyList<WoundEffectSlotAgreement> Slots);

internal sealed record MortalWoundTreatmentSeverityReductionProjectionResult(
    MortalWoundTreatmentSeverityReductionProjection? Projection,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Projection is not null && Issues.Count == 0;
}

internal static class MortalWoundTreatmentSeverityReductionPlanner
{
    internal static MortalWoundTreatmentSeverityReductionProjectionResult Project(
        WoundMaterializationEnvelope before,
        int steps,
        string resultingLastChangeEventRef);
}
```

- The result exposes a projection only when every root, definition, slot, ownership domain, complication, and destination-rank power rule recomputes exactly; invalid results expose no actionable partial projection.

- [ ] **Step 1: Add a compile-safe outer RED through existing APIs**

In the now-Integration resolver fixture, add/retain tests that invoke the existing six-argument composer and working-wound simulator, without naming a not-yet-created production type:

```text
ProcedureReduction_DestinationSlotOverflowRejectsBeforeDieClaim
ProcedureReduction_DestinationPowerOverflowRejectsBeforeDieClaim
ProcedureReduction_RejectedBandLeavesLowestFreeDieForNextLegalRoute
```

Use a valid rank-III wound reduced to II with (a) too many slots and (b) a one-slot `action_control.forbid` or `characteristic_modifier add 3`. Assert the attempt is inapplicable before any die/resource claim and that the next legal band obtains the same lowest free die. Run the exact Integration filter and retain the behavioral RED; compilation and discovery must succeed.

- [ ] **Step 2: Implement detached root/ownership reconstruction**

Build one exact ownership map from `rootBindings`, then replace base ownership with `ForComplication(complicationId)` for every pairwise-disjoint `ownedEffectIds` member. Order projection roots by canonical root-binding order and attach all reciprocal consequence entries for the old effect ID in one-based slot order. Reject missing/duplicate/confusable bindings, a complication-owned ID without one root, or one root owned by multiple complications.

- [ ] **Step 3: Extract one persisted-graph envelope adapter**

Extract/reuse the graph traversal currently embedded in `MortalWoundTreatmentContract.WoundGraph.cs`: start from every persisted direct root definition, include its complete `event_reaction/apply_definition` expansion graph, and construct `WoundDetachedMortalEnvelopeRequest`. The shared adapter must call `WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(resultingRank, ...)`, require zero issues, and require its derived ordered slot/profile rows to agree exactly with persisted consequence entries and `SlotsUsed`. Keep the original treatment-contract validation on the same helper so creation and reduction cannot drift.

This is the authoritative destination-rank component-power verdict. `WoundMaterializationContract.Parse` remains a separate canonical structural round-trip, not a substitute for the severity-aware catalog.

- [ ] **Step 4: Project the destination envelope without allocating identity**

Clone through canonical serialization, compute `resultingRank = checked(before.Rank - steps)`, require `steps` in `1..2` and `resultingRank >= 1`, set:

```csharp
Severity = before.Severity with
{
    Value = resultingRank switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => throw new InvalidOperationException()
    },
    Rank = resultingRank,
    LastChangeEventRef = resultingLastChangeEventRef
};
Consequences = before.Consequences with { SlotBudget = resultingRank };
```

Keep `MaximumAtCreation`, recovery, care, course, complications, definitions, bindings, entries, lifecycle, treatment routes, display, and origin unchanged. Validate the full persisted graph with the shared severity-aware adapter, require exact derived-slot agreement, then reparse with `WoundMaterializationContract.Parse` for canonical structural agreement. Do not prune, clamp, weaken, or rewrite authored components.

- [ ] **Step 5: Add the fixture-free projector tests and make them GREEN**

After the outer RED exists, add the new production type and direct tests together. If the typed surface is not yet implemented, use a minimal throwing shell only long enough to obtain a discovered behavioral RED; do not commit a CS0246/compile-failing test state. Cover:

```text
Project_ValidReductionPreservesSemanticGraphAndChangesNoRuntimeIdentity (III->II, III->I)
Project_DestinationEnvelopeRejectsExcessSlotsWithoutPruning
Project_DestinationEnvelopeRejectsOverpoweredComponentWithoutWeakening
Project_DestinationEnvelopeRejectsReactionExpansionPowerWithoutWeakening
```

For the valid cases assert destination severity/value/slotBudget, exact definitions/root bindings/entries/complications/recovery/course preservation, and unchanged old effect IDs in the provisional projection. Explicitly prove that the projector allocates no replacement identity.

- [ ] **Step 6: Seal the complete projection**

Compute a domain/versioned fingerprint over canonical before/provisional after, steps, event ref, and every ordered root's old ID, definition key, ownership domain, slot/profile/summary tuple. Clone every mutable member on construction and readback so later caller mutation cannot preserve the seal.

- [ ] **Step 7: Reuse the projector in T067 simulation**

Replace the simulator's direct rank/value assignment with:

```csharp
var projection = MortalWoundTreatmentSeverityReductionPlanner.Project(
    before,
    reduceSeverity.Steps,
    before.Severity.LastChangeEventRef);
if (!projection.IsValid || projection.Projection is null)
    return false;
candidate = projection.Projection.ProvisionalAfter;
```

The simulator still owns no permanent IDs and no accepted transition chronology; the existing event ref is used only to validate destination graph legality. This makes every authored band reject before dice/resource finalization if its unchanged graph cannot fit the lower rank.

- [ ] **Step 8: Run pure projection and T067 applicability controls**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests&FullyQualifiedName~Project_"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~Reduction" -TimeoutMinutes 15
```

Expected: destination-rank projection tests and pre-attempt inapplicability/free-reroll guards pass with warning-free build and complete cleanup.

- [ ] **Step 9: Commit the shared projection**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentSeverityReductionPlanner.cs BookOfEternityClient/Services/WoundPersistedConsequenceEnvelopeAdapter.cs BookOfEternityClient/Services/MortalWoundTreatmentWorkingWoundSimulator.cs BookOfEternityClient/Services/MortalWoundTreatmentContract.WoundGraph.cs BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs
git commit -m "feat(wounds): validate lower-severity projections (#1536)"
```

---

### Task 3: Split ordered treatment outcome preparation from finalization

**Files:**

- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`

**Interfaces:**

- Produce:

```csharp
internal sealed class MortalWoundTreatmentOutcomePreparation
{
    internal WoundMaterializationEnvelope Before { get; }
    internal WoundMaterializationEnvelope ProvisionalAfter { get; }
    internal string TransitionId { get; }
    internal MortalWoundTreatmentSeverityReductionProjection? SeverityReduction { get; }
    internal string Fingerprint { get; }
}

internal sealed record MortalWoundTreatmentOutcomePreparationResult(
    MortalWoundTreatmentOutcomePreparation? Preparation,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Preparation is not null && Issues.Count == 0;
}

internal static MortalWoundTreatmentOutcomePreparationResult Prepare(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptRequest request,
    MortalWoundTreatmentResolution resolution,
    long currentGameMinute);

internal static MortalWoundTreatmentOutcomePublicationResult Finalize(
    MortalWoundTreatmentOutcomePreparation preparation,
    MortalWoundTreatmentResolution resolution,
    WoundEffectOperationBatch? rematerializationBatch,
    IReadOnlyDictionary<string, EffectAcceptedApplicationResult> applicationByRef);
```

- Retain `Compose(...)` as the B.5 compatibility wrapper only for results that need no effect rematerialization: it calls `Prepare`, then `Finalize(preparation, resolution, null, an empty application map)`. A severity-changing result returned through `Compose` must fail closed instead of pretending its old IDs are final. Task 3 implements only this no-rematerialization finalization branch; Task 5 completes the reduction branch after the authenticated batch exists.
- In `TreatmentContinuationAuthority` and `TreatmentContinuationView`, replace the pre-effect final-state fields `After`, `DeclaredOutcome`, and `OutcomePublicationFingerprint` with detached `OutcomePreparation` plus its recomputed fingerprint. Task 4 adds nullable `MortalWoundTreatmentRematerializationAuthority`. The continuation must never carry a supposedly final wound or resulting effect-ID list before #1535 returns.
- In `WoundAcceptedTurnPlanner.ComposeTreatmentContinuationFinalPlan`, replace the old direct reads of `continuation.After` and `continuation.DeclaredOutcome` in the same Task 3 commit. The B.5 branch calls `Finalize(continuation.OutcomePreparation, resolution, null, emptyApplicationMap)` and consumes that verified result; a severity-changing preparation still returns the explicit unsupported/fail-closed result until Task 5 supplies the authenticated batch and map. Do not leave an intermediate uncompilable consumer for a later task.

- [ ] **Step 1: Add compile-safe ordered-outcome REDs**

Through the existing composer/resolution surface, add discovered tests for standard `[stabilize, reduce_severity(1)]`, singleton one/two-step reduction, two ordered one-step reductions, both two-operation stabilize/reduce orders, all three legal placements of stabilization among two one-step reductions, guaranteed mode, failed-category reduction without route completion, and these resealed mismatches: changed steps, ordinal, intent fingerprint, and route completion. Do not reference `Prepare` until its production signature exists; use the existing composer or reflection for the initial RED. Require the standard successful route to fail only at the current explicit unsupported-slice boundary.

- [ ] **Step 2: Generalize exact intent agreement to an ordered list**

Recompose intents only through `MortalWoundTreatmentOutcomeIntentComposer.TryCompose`. Require exact count/order/type, operation ordinal, kind, declared-operation fingerprint, intent fingerprint, and typed fields (`Steps` for reduction). Accept exactly the following grammar rather than a finite shape list:

```text
[no_improvement]
[stabilize]
[reduce_severity(1|2)]
[stabilize?, reduce_severity(1|2)] in either operation order
[reduce_severity(1), reduce_severity(1)]
[stabilize, reduce_severity(1), reduce_severity(1)] in any of the three
  stabilization positions
```

Equivalently, outside the two singleton cases, accept an ordered list containing one or two `reduce_severity` operations and zero or one `stabilize`, with aggregate reduction `1..2`; a two-reduction list therefore requires two one-step reductions. Reject every other kind/count/aggregate and any mixed `no_improvement`. Do not infer the selected category from this grammar and do not parse route JSON. This must remain aligned with T067's already-valid three-operation results instead of introducing a narrower publication contract.

- [ ] **Step 3: Apply scalar operations in sealed ordinal order**

Start from the exact route-source wound with accepted attempt metadata, route completion, and one deterministic `treat` transition. Recompute the declared ordinal list and apply its scalar state semantics in that order; stabilization reuses the B.5 anchor/blocker transform. Sum checked reduction steps, then invoke the shared projector once against the fully transformed scalar shell using `request.Coordinates.EventRef`. Because B.6 reduction does not change any non-severity field, this single aggregate projection is state-equivalent while retaining the exact authored order in the preparation fingerprint. Multiple reduction operations produce one final-rank projection and one later effect batch, not an observable intermediate canonical state.

- [ ] **Step 4: Separate provisional and final fingerprints**

Use distinct domains/versions:

```text
book_of_eternity.mortal_wound_treatment.outcome_preparation / 1
book_of_eternity.mortal_wound_treatment.outcome_publication / 2
```

The preparation seal binds exact before, provisional after, request/result/resolution fingerprints, ordered intent fingerprints, transition ID, route completion, current minute, and nullable severity-projection fingerprint. The final seal additionally binds the accepted final wound and `WoundDeclaredTransitionOutcome`, including fresh resulting effect IDs.

- [ ] **Step 5: Freeze a fail-closed finalization seam for later identity allocation**

For no reduction, require a null rematerialization batch, an empty application map, and canonical equality with `ProvisionalAfter`; create the final declaration only from that sealed preparation. For reduction, Task 3 must fail closed before creating `DeclaredOutcome` even if a caller hands it arbitrary effect results. Do not invent a projection-root-to-ID correspondence here: projection roots intentionally have no `applicationRef`, and only Task 4's authenticated batch can establish that mapping. Task 5 completes this branch by passing that exact batch and the independently verified result map into the same `Finalize` surface.

- [ ] **Step 6: Add direct tests after the typed surface exists and make them GREEN**

Add fixture-free tests for `Prepare_StabilizeThenReducePreservesDeclaredOrdinalOrder`, `Prepare_TwoOneStepReductionsAggregateToOneAtomicDestination`, all three placements of stabilization among two one-step reductions, the four sealed mismatch cases, exact unchanged-result finalization, and fail-closed reduction finalization without an authenticated batch. Direct tests may name the new `Prepare`/`Finalize` types only after their production declarations compile. Mapping, fresh-ID, complication-ownership, and canonical slot-reindex tests belong to Task 5, where the real batch/result authority exists.

- [ ] **Step 7: Run ordered preparation/finalization controls**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests&FullyQualifiedName~Prepare_|FullyQualifiedName~FinalizeReduction_"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&(FullyQualifiedName~ProcedureScalarOutcomePlanner_|FullyQualifiedName~ProcedurePartialSuccessSingleton_|FullyQualifiedName~ProcedureRepeatedStabilization_)" -TimeoutMinutes 15
```

Expected: new ordered/provisional/final contract tests pass and every B.5 scalar test remains green.

- [ ] **Step 8: Commit the two-phase outcome planner**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs
git commit -m "refactor(wounds): split treatment outcome publication (#1536)"
```

---

### Task 4: Build a generation-aware sealed rematerialization batch for #1535

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentSeverityRematerializationPlanner.cs`
- Create: `BookOfEternityClient/Services/WoundEffectIdentityLineageAnalyzer.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanCache.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`
- Modify: `BookOfEternityClient/Services/EffectIdentityState.cs`
- Modify: `BookOfEternityClient/Services/EffectSourceAuthority.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/WoundReactionLineageAuthority.cs`
- Verify: `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs`
- Test: `BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs`
- Test: `BookOfEternityClient.Tests/WoundEffectLineagePlannerTests.cs`
- Test: `BookOfEternityClient.Tests/WoundReactionLineageAuthorityTests.cs`
- Test: `BookOfEternityClient.Tests/EffectIdentityStateTests.Wounds.cs`
- Test: `BookOfEternityClient.Tests/EffectSourceAuthorityTests.Wounds.cs`
- Test: `BookOfEternityClient.Tests/EffectAcceptedTurnInputComposerTests.Wounds.cs`
- Test: `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs`

**Interfaces:**

- Produce one private immutable `MortalWoundTreatmentRematerializationAuthority` bound to the continuation and one `WoundEffectOperationBatch` only when `OutcomePreparation.SeverityReduction` is non-null.
- Keep `WoundPreparedTransitionAuthority` as the batch's structural adapter for existing #1535 code, with `TransitionKind = "treat"`; authorization comes from the independently recomputed private treatment seal, never from that discriminator alone.
- Extend each rematerialized `WoundRootEffectApplication` with nullable exact `PriorRootEffectId`. `treat` supplies a full old-root/new-application bijection because B.6 preserves the graph. `worsen` supplies a bijection only for roots retaining the same definition and ownership domain; genuinely new roots at any severity are parentless, while removed old roots are terminal-only history. #1535 writes a non-null predecessor into the new root's first-create `sourceEffectIds`; it is provenance, not an instruction to mutate the retired identity.

- [ ] **Step 1: Add compile-safe lineage and authority REDs through existing planners**

Extend `WoundEffectLineagePlannerTests` through its existing production planner surface. Keep the existing forged-disconnected-terminal test unchanged and add:

```text
Plan_CurrentRootWithAuthenticatedRetiredGeneration_IsAccepted
Plan_TwoRetiredGenerationsAndCurrentRoot_AreAccepted
Plan_GenerationForkCycleWrongDefinitionOrCrossDomain_IsRejected
Plan_SameDefinitionReactionDescendantCannotMasqueradeAsGenerationPredecessor
Plan_RetiredGenerationWithActiveMember_IsRejected
Plan_WorsenThenTreatSharesOneAuthenticatedGenerationChain
Plan_InitialCreateRemainsParentless
```

In `WoundReactionLineageAuthorityTests.cs`, call `WoundReactionLineageAuthority.Build` directly for a current root with one and then two authenticated terminal predecessor generations, and prove that a reaction descendant can be resolved from the current rematerialized root. Add negative sibling rows showing that a terminal predecessor cannot itself become the reaction parent and that the current generation still obeys the exact outgoing `apply_definition` edge. This locks both consumers of the shared analyzer, not only accepted-mechanics terminal planning.

Add outer batch tests using existing types/reflection for: a complete current teardown, private authority required for `treat`, missing/reordered terminal with all public outer fingerprints resealed, and changed projection/source export. Every test must compile and be discovered before implementation; obtain the expected behavioral RED rather than a missing-type build error.

- [ ] **Step 2: Derive deterministic non-permanent group and root coordinates**

Derive one exact/confusable-unique `LocalWoundRef` from the request fingerprint, result fingerprint, and transition ID through a domain-separated SHA-256 writer. For each projection root in canonical order, derive `ApplicationRef` and root `OperationKey` from those same sealed coordinates plus root ordinal, prior effect ID, and definition key. Use one positive mechanics ordinal and one-based apply operation ordinals. The group alias and root coordinates are ephemeral plan coordinates, not permanent wound/effect IDs.

- [ ] **Step 3: Export the exact retained source graph and generation predecessor**

Create one non-materializable source export containing every detached canonical `wound/<woundId>/<definitionKey>` definition key. Assign the same derived `LocalWoundRef` to `WoundEffectOperationBatch.LocalWoundRef`, `WoundEffectSourceExport.SourceRef`, and every root application's `SourceSelector.SourceRef`. Keep the stable wound ID in `WoundEffectSourceExport.SourceId` and in every root application's `ExpectedSourceKey.SourceId`; each same-turn application selector has `SourceId = null` exactly as the existing #1535 composer requires. Bind owner/realm to the wound, causal event and event semantic fingerprint to the exact accepted event. Each root application uses empty parameters, exact target/source selectors, derived carrier coordinate, component count, materialization fingerprint, preserved slot semantics/ownership domain, and the projection root's exact old effect ID as `PriorRootEffectId`.

- [ ] **Step 4: Make lineage validation generation-aware without accepting garbage history**

Create one linear `WoundEffectIdentityLineageAnalyzer` and make both the accepted-mechanics/terminal planner and `WoundReactionLineageAuthority` consume its detached classification; do not maintain two subtly different traversals. It distinguishes across both `worsen` and `treat`:

- a reaction child: one first-create parent whose definition is an authorized outgoing `apply_definition` edge;
- a direct root: zero parents when genuinely new, or one same-source/same-definition/same-target/same-carrier-domain predecessor when retaining a canonical root coordinate across severity rematerialization;
- a current generation: the exact root bound by the current wound/source authority plus its current reaction descendants;
- a retired generation: an authenticated predecessor root plus its reaction descendants, all terminal.

During full-rematerialization post-apply validation, `ApplicationRootLineage` rows are the new current heads and no old root is emitted as `ExistingRootLineage`; old roots exist only in sealed `PriorRootEffectId`, terminal operations, and durable identity history. `ExistingRootLineage` remains reserved for roots genuinely retained as current by a future partial same-rank graph change. On a later turn, the canonical wound's existing root binding is the current head and its first-create predecessor evidence reconstructs the retired chain. Preserve this distinction in source-export/topology fingerprints.

Require each retained-coordinate predecessor chain to be finite, acyclic, unique, and non-branching, to end at exactly one zero-parent origin, and to preserve exact source group, definition, target, carrier owner/stack authority, and ownership domain. Exclude the same-definition successor edge while traversing a generation's reaction descendants. Reject a retired generation containing any active/suspended member, a successor fork, cross-domain or wrong-definition predecessor, cycle, current duplicate, or a disconnected terminal identity whose definition remains in the current graph. Historical terminal identities for definitions removed by a legal graph-changing worsen remain allowed, matching the existing validator contract; any active/suspended identity still requires a current definition and current closure. Return only the current active/suspended closure to the terminal-operation planner. Thus the existing forged-current-definition negative remains RED/GREEN while a second and third legitimate treatment and a graph-changing worsen remain valid.

- [ ] **Step 5: Plan complete current-source teardown before apply**

Call `WoundEffectTerminalOperationPlanner.Plan` with every old root binding, the sealed pre-turn effect carriers/index, the accepted treatment event, the same mechanics ordinal, and an operation offset equal to the root-application count. For a full severity rematerialization, export only the new application rows as current `RootLineageAuthority`; bind every old root through the matching application's `PriorRootEffectId`, terminal operation, and durable first-create generation evidence. Reserve `ExistingRootLineage` for genuinely retained current roots in later partial same-rank graph changes. This must discover all active/suspended descendants even when their current root is terminal.

- [ ] **Step 6: Add a non-cyclic private authority DAG**

Seal:

```csharp
internal sealed record MortalWoundTreatmentRematerializationAuthority(
    string PreparedInputFingerprint,
    string RequestFingerprint,
    string ResolutionAuthorityFingerprint,
    string ResultFingerprint,
    string AttemptId,
    string OperationKey,
    string TransitionId,
    string ExpectedBeforeFingerprint,
    string ProjectionFingerprint,
    string AuthoritySeedFingerprint,
    string SourceExportFingerprint,
    string BatchTopologyFingerprint,
    string AuthoritySeal);
```

Compute `AuthoritySeedFingerprint` only from prepared input, request, resolution/result, attempt, operation, transition, expected-before, and projection fingerprints. Set `source.OpportunityId = AttemptId` and `source.OpportunityAuthorityFingerprint = AuthoritySeedFingerprint`; the structural `WoundPreparedTransitionAuthority` uses the same seed and never references the final seal. After source export, roots (including prior-root IDs), lineage, terminals, and structural adapter exist, compute `BatchTopologyFingerprint` over their complete ordered canonical representations. Compute the final `AuthoritySeal` from the seed plus topology fingerprint. There is no source-export-to-final-seal back-reference.

The continuation owns a detached authority and binds its final seal. `PrepareTreatmentContinuationCandidate` and the prepared-plan cache recompute seed, complete topology, and final seal independently from sealed baselines. A caller who deletes/reorders a terminal and recomputes all public preparation/common fingerprints must still fail because it cannot borrow or recompute the private seal from a different topology.

Reorder the publication composition without changing its public entry point: read canonical baselines, build the empty-opportunity `WoundAcceptedTurnInput`, compute its input fingerprint, prepare the nullable rematerialization batch/private authority, and only then mint the treatment continuation. The prepared-plan cache invokes the same pure rematerialization planner again and compares the complete recomputed result. This ordering removes any continuation/batch construction cycle and lets neither object self-attest the other.

- [ ] **Step 7: Admit exactly zero-or-one treatment batch**

Keep zero batches for unchanged severity. Require exactly one batch for reduction, one prepared wound, the same existing wound ID, and the deterministic transition ID. Update preparation/cache/common fingerprints and replay comparison to bind a treatment batch's complete topology. Generic create/worsen validation still accepts only its existing authorities; a `treat` batch without the matching private continuation fails.

- [ ] **Step 8: Teach #1535 all severity-rematerialization predecessor provenance**

In `EffectAcceptedTurnPlanner`, select the exact pre-turn wound as `terminalWound` for both `worsen` and private-authorized `treat` batches. Update source-export and terminal-operation validation to obtain the effective operation key/before fingerprint from the verified authority. Before any allocation, require every non-null `PriorRootEffectId` to be the exact pre-turn canonical root of the same wound/source, definition, owner, target, stack/carrier authority, and ownership domain. Require a full old-root/new-application bijection for graph-preserving `treat`; for graph-changing `worsen`, require an exact bijection only across retained definition/domain coordinates, require genuinely new coordinates to be parentless, and require removed old roots only in terminal operations/history. A same-definition reaction descendant cannot satisfy this proof. Update the existing worsen batch builder and its cache/fingerprints/tests to supply this partial exact matching policy.

Replace the ambiguous internal `createSourceEffectId` handoff with a closed runtime provenance discriminator: `Direct`, `Reaction(parentEffectId)`, or `SeverityGeneration(parentEffectId)`. Only the sealed reaction executor may produce `Reaction`; only an authenticated severity batch may produce `SeverityGeneration`; ordinary/raw applications cannot provide either. `CreateIdentityEntry` serializes the expected empty/singleton `sourceEffectIds`, and `IdentityEvidenceMatches` verifies it exactly. Keep the raw command root empty and the lifecycle array equal to the ordinary accepted lifecycle plus any separately sealed Fate event. Execution order remains typed wound terminations first and typed wound root applications afterward.

- [ ] **Step 9: Add direct typed tests after interfaces exist and make all controls GREEN**

Add fixture-free tests for complete terminal lineage, deterministic detached/write-free planning, seed/topology/final-seal recomputation, every missing/extra/reordered/changed root or terminal, changed lineage/projection/source export, exact/confusable identity reuse, and exact first-create predecessor evidence. In Fast, obtain accepted effect results from the real pure `WoundEffectBatchPlanner.Build` result, not a hand-written result and not the filesystem/cache-backed prepared-plan API; mutate only detached copies for rejection cases. Any test that specifically exercises `WoundAcceptedTurnPlanCache` remains in Integration.

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests&FullyQualifiedName~PrepareReductionBatch_|FullyQualifiedName~ReductionEffectHandoff_"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundEffectBatchPlannerTests|FullyQualifiedName~WoundEffectLineagePlannerTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectIdentityStateWoundTests|FullyQualifiedName~EffectSourceAuthorityWoundTests|FullyQualifiedName~EffectAcceptedTurnInputComposerWoundTests|FullyQualifiedName~AcceptedMechanicsPlannerTests" -TimeoutMinutes 15
```

Expected: all treatment rematerialization batch/tamper tests and the retained #1535 create/worsen suite pass with worsen now emitting the shared generation provenance and create remaining parentless.

- [ ] **Step 10: Commit the sealed effect bridge**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentSeverityRematerializationPlanner.cs BookOfEternityClient/Services/WoundEffectIdentityLineageAnalyzer.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanCache.cs BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs BookOfEternityClient/Services/EffectIdentityState.cs BookOfEternityClient/Services/EffectSourceAuthority.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs BookOfEternityClient/Services/WoundReactionLineageAuthority.cs BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs BookOfEternityClient.Tests/WoundEffectBatchPlannerTests.cs BookOfEternityClient.Tests/WoundEffectLineagePlannerTests.cs BookOfEternityClient.Tests/WoundReactionLineageAuthorityTests.cs BookOfEternityClient.Tests/EffectIdentityStateTests.Wounds.cs BookOfEternityClient.Tests/EffectSourceAuthorityTests.Wounds.cs BookOfEternityClient.Tests/EffectAcceptedTurnInputComposerTests.Wounds.cs BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs
git commit -m "feat(wounds): seal treatment effect rematerialization (#1536)"
```

---

### Task 5: Finalize fresh identities into one canonical treatment transition

**Files:**

- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`
- Modify: `BookOfEternityClient/Services/WoundTransitionReducer.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`

**Interfaces:**

- `ComposeTreatmentContinuationFinalPlan` converts the already independently derived `EffectAcceptedApplicationResult` list to one exact-by-`applicationRef` map and supplies that map plus the authenticated treatment batch to `Finalize`; it does not inspect effect carrier JSON to guess IDs.
- `MortalWoundTreatmentOutcomePublicationPlanner.Finalize` owns the only reduction branch that consumes the authenticated batch/result map, invokes the existing exact `BuildFinalWound` helper, and produces the final `After`, declared outcome, and outcome-publication fingerprint used by reducer/history/common agreement. No caller supplies a prebuilt final wound.

- [ ] **Step 1: Add final handoff REDs through the existing publication API**

Add compile-safe Integration rows proving: standard procedure and guaranteed reduction are still unsupported before implementation; failed/partial reduction does not `AppendOnce`; a legal zero-root physical wound requests one authenticated empty rematerialization batch and no effect ID; and missing/extra/reordered/changed result mappings fail closed. Run the existing API surface first and retain discovered RED evidence before changing finalization.

- [ ] **Step 2: Rebuild the final wound from exact #1535 results**

Pass the verified application results into `ComposeTreatmentContinuationFinalPlan`, require exact/confusable-unique `applicationRef` coverage of the authenticated treatment batch, and call `Finalize(preparation, resolution, batch, applicationByRef)`. Make the existing `BuildFinalWound` helper internally reusable by the outcome finalizer. For a reduction, `Finalize` calls `BuildFinalWound(preparation.ProvisionalAfter, batch, applicationByRef)`; there is no overload accepting caller-authored `finalizedAfter`. Change complication ownership reconstruction from append semantics to exact replacement: every complication's final `ownedEffectIds` is the ordered set of accepted new root IDs whose sealed ownership domain names that complication; no prior root ID may remain.

- [ ] **Step 3: Finalize outcome and reducer evidence after identity allocation**

Inside `Finalize`, key a one-to-one mapping by the batch's sealed `applicationRef` from every projected root to one fresh final root with the same definition, within-root ordered slot profile/summary semantics, and ownership domain. Reconstruct the only allowed final wound: substitute accepted effect IDs, sort roots by actual effect ID exactly as #1535 does, assign contiguous one-based slot ordinals in that root order while preserving within-root semantic order, and rebuild each complication's ordered owned-root set. Require the full contract, exact semantic equality on every other field, and FR-027 exact/confusable disjointness from all prior roots before creating `DeclaredOutcome`. Then feed the exact final wound and declared outcome into `WoundTransitionReducer.Reduce(kind: "treat")`. Require the reducer to emit one `WoundEffectTransitionIntent("replace", oldIds, newIds)` when either side is non-empty, one carrier update, and one ordinary treatment history intent. Keep the existing <=2 non-heal reduction and fresh-root checks as independent defense.

- [ ] **Step 4: Seal final common-plan agreement**

Update continuation/bundle/common-plan agreement so final after/outcome fingerprints are derived from the accepted effect result map and final plan, not stored before #1535. Reject a valid result map borrowed from another prepared plan, changed effect ID, changed application order, changed complication mapping, stale before wound, or post-seal carrier substitution.

- [ ] **Step 5: Preserve zero-root and non-reduction behavior**

A legal physical wound with zero mechanical roots still uses one sealed empty rematerialization batch on severity change so its source transition is authenticated; it allocates no effect ID but still publishes severity/history atomically. Existing singleton stabilization/no-improvement paths keep zero batches and byte-equivalent B.5 outcomes.

- [ ] **Step 6: Turn the retained procedure success row GREEN**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&FullyQualifiedName~ProcedureFinalization_ConsumesOnlySelectedSupplyAndReleasesEveryOtherHeldClaim" -TimeoutMinutes 15
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests&FullyQualifiedName~FinalizeReduction_"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundTransitionReducerTests&FullyQualifiedName~Treat"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&(FullyQualifiedName~ProcedureRepeatedStabilization_|FullyQualifiedName~ProcedureNoImprovement_)" -TimeoutMinutes 15
```

Expected: both rows of the retained theory and all new finalization controls pass; the published successful row is severity II, has only fresh root IDs, spends only the selected supply, retains the reusable tool, and appends one treatment history row.

- [ ] **Step 7: Commit final wound publication**

```powershell
git add -- BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs BookOfEternityClient/Services/WoundTransitionReducer.cs BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs
git commit -m "feat(wounds): publish lower severity with fresh effects (#1536)"
```

---

### Task 6: Prove coordinated rollback, retry, replay, and carrier parity

**Files:**

- Create/Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs`
- Test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`
- Verify: `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublicationTransaction.cs`
- Verify: `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs`
- Verify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`

**Interfaces:**

- Reuse the B.5 one-use transaction receipt, before-image inventory, compensation, rearm, quarantine, and cold request reconstruction. Add no severity-specific writer or second transaction.

- [ ] **Step 1: Add the full-engine RED fixture before coordinator changes**

Extend the existing `GameEngineTurnLifecycleTests` partial class; do not create a new fixture/category. Add standard compound, singleton two-step, partial/failed route completion, guaranteed, zero-root, failure compensation, exact retry, cold replay, carrier parity, and two-distinct-treatment cases. Capture wound/effect/resource/history/source roots, output, command/pending files, and claim state. Run the exact `GameEngineTurnLifecycleTests&SeverityReduction` Integration selector and retain any behavioral RED before altering coordinator code.

- [ ] **Step 2: Publish the complete player contour**

Prove old roots/descendants become terminal, all new roots are active with fresh IDs and exact wound source keys, wound carrier/index/history and effect carriers/index/history/source authority agree, only selected resources are consumed, unrelated effects and all untouched files remain unchanged, and player output contains one accepted treatment result.

- [ ] **Step 3: Inject failures after effect and common-plan writes**

At each existing coordinator fault seam, assert every governed byte and claim family returns to its exact before state, the durable command/pending request and confirmed hold remain for retryable compensation, and only the original receipt-owned plan is rearmed. A competing or changed plan must remain protected and force restart-required failure.

- [ ] **Step 4: Retry and cold replay exactly once**

The exact retry must reuse reference-equivalent cached plan authority and the same created effect IDs; successful completion must quarantine durable command/pending authority before claim release. After copying canonical roots to a fresh filesystem and clearing process-local registries, replay must return `ExactReplay` and change no wound/effect/resource/history/output byte.

- [ ] **Step 5: Prove owner carrier parity**

Run the same procedure rematerialization for player, NPC, and combatant/member targets selected through the existing nearby/canonical target authority. Assert the wound and new effects land only in the correct carrier, the player carrier is untouched for non-player targets, and no caller supplies a target ID outside accepted state.

- [ ] **Step 6: Prove repeated treatment across generations**

Publish two distinct accepted reduction attempts against the same wound (for example III->II and II->I, using a graph legal at I). Assert the second attempt validates the retired predecessor generation, creates a third fresh root, terminates only the then-current root/descendants, preserves earlier terminal history, appends exactly one treatment transition/history row per attempt, spends each attempt exactly once, and survives restart/replay. Pair it with the unchanged forged-disconnected-terminal negative in the fixture-free lineage suite.

- [ ] **Step 7: Run the coherent Integration contour with measured headroom**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GameEngineTurnLifecycleTests&FullyQualifiedName~SeverityReduction" -TimeoutMinutes 15
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GameEngineTurnLifecycleTests&FullyQualifiedName~Procedure" -TimeoutMinutes 15
```

Expected: all selected cases pass with no warning/error, duplicate test ID, timeout, or cleanup failure. Record measured wall time; do not move these workflows back into Fast to save a lane command.

- [ ] **Step 8: Commit lifecycle closure**

```powershell
git add -- BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs
git commit -m "test(wounds): prove severity publication lifecycle (#1536)"
```

---

### Task 7: Review and close only the B.6 contour

**Files:**

- Modify: `docs/superpowers/plans/2026-09-04-t070b6-severity-rematerialization.md`
- Modify: `specs/1536-complete-wound-materialization/tasks.md`
- Modify: `specs/1536-complete-wound-materialization/spec.md`
- Modify: `specs/1536-complete-wound-materialization/plan.md`
- Modify: `specs/1536-complete-wound-materialization/research.md`
- Modify: `specs/1536-complete-wound-materialization/data-model.md`
- Verify: `specs/1536-complete-wound-materialization/contracts/mortal-wound-treatment.md`
- Modify: `specs/1536-complete-wound-materialization/contracts/wound-effects-and-atomicity.md`
- Modify: `specs/1536-complete-wound-materialization/contracts/wound-materialization-command.md`
- Modify: `specs/1536-complete-wound-materialization/contracts/wound-canonical-lifecycle.md`
- Modify: `OtherGuides/Wound_Materialization_Contract.md`
- Modify: `Examples/E_CLI_Wound_Materialization.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify/Test: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`
- Test: `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`

**Interfaces:**

- Closure evidence must distinguish deterministic Fast coverage from file-backed Integration coverage and must not mark T070 or #1536 complete.

- [ ] **Step 1: Add a GM-documentation source guard as RED**

Add a test that extracts the exact `mortal_wound_treatment_reduce_severity_v1` JSON from the GM guide/example and validates it through the production treatment/wound parser. Require an ordered success result containing `stabilize` followed by `reduce_severity { steps: 1 }`, and required prose for destination-envelope rejection, no automatic pruning/weakening, client-owned fresh effect IDs, and failed/partial route-completion behavior. Run the exact prompt-documentation test and observe RED on the missing marker before editing documentation.

- [ ] **Step 2: Publish the worked GM example and manifest entry**

Update the guide and CLI example with one complete rank-III Mortal physical wound route whose successful outcome is `[stabilize, reduce_severity(1)]` and whose retained graph is legal at rank II. State clearly that the GM authors the wound graph and route result, the client validates the unchanged graph at the destination rank and rematerializes fresh identities, and invalid lower-rank power/slot envelopes reject the route rather than pruning mechanics. State that only a newly successful category completes a route; `partial_success` and failed categories do not. Add the exact marker/validation route to the manifest. Do not document client-private fingerprints, terminal-operation payloads, or IDs as GM-authored fields.

Run:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests&FullyQualifiedName~WoundTreatmentReduceSeverity"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests" -TimeoutMinutes 15
pwsh .\scripts\test-csharp.ps1 -Lane FullValidation
```

Expected: source guard and example-manifest validation pass, then the conditional documentation boundary control passes. Record that no Chaos Sea/Shining Abode matrix, afterlife example, or afterlife daemon contract changed because B.6 is strictly Mortal.

- [ ] **Step 3: Run the complete deterministic owning selection**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests|FullyQualifiedName~WoundEffectBatchPlannerTests|FullyQualifiedName~WoundEffectLineagePlannerTests|FullyQualifiedName~WoundTransitionReducerTests|FullyQualifiedName~EffectIdentityStateWoundTests|FullyQualifiedName~EffectSourceAuthorityWoundTests|FullyQualifiedName~EffectAcceptedTurnInputComposerWoundTests|FullyQualifiedName~AcceptedMechanicsPlannerTests" -TimeoutMinutes 15
```

Expected: every discovered owning case passes with pristine build and cleanup.

- [ ] **Step 4: Run retained B.5 and reducer/#1535 controls**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests&(FullyQualifiedName~ProcedureRepeatedStabilization_|FullyQualifiedName~ProcedurePartialSuccessSingleton_|FullyQualifiedName~ProcedureTreatmentLifecycleAgreement_|FullyQualifiedName~ProcedureTreatmentAuthority_|FullyQualifiedName~ProcedureFinalization_ConsumesOnlySelectedSupplyAndReleasesEveryOtherHeldClaim)" -TimeoutMinutes 15
```

Expected: every retained case passes; no old-ID acceptance, scalar regression, or generic effect-command bypass appears.

- [ ] **Step 5: Run one Fast checkpoint**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: the complete physically isolated Fast project finishes below the unchanged five-minute limit, with the file-backed resolver family absent. If it does not, investigate ownership or genuine regressions; do not micro-optimize assertions merely to gain seconds.

- [ ] **Step 6: Run the complete moved resolver family once after final code**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests" -TimeoutMinutes 15
```

Expected: the complete moved resolver family passes under its bounded Integration owner. Use the maximum documented 15-minute Focused budget because this is the one exhaustive post-move control for the formerly Fast, file-backed family; do not trade coverage for assertion-level timing tricks. If measured execution cannot reliably fit that ceiling, partition the complete family into explicit, non-overlapping Focused filters whose combined discovery count proves no omitted resolver case; do not raise an unsupported lane limit.

- [ ] **Step 7: Run the owning lifecycle Integration selection once after final code**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GameEngineTurnLifecycleTests&(FullyQualifiedName~SeverityReduction|FullyQualifiedName~ProcedureTreatment)" -TimeoutMinutes 15
```

Expected: all B.6 lifecycle cases pass. Do not run `PreMerge`, `DeepValidation`, `LifecycleIntegration`, push, merge, or close #1536 without a later explicit request.

- [ ] **Step 8: Perform two-stage independent review**

First review exact spec/plan compliance, especially FR-018/027/062/063/064, severity-aware power validation, generation-chain provenance, non-cyclic private authority, teardown completeness, final effect-ID derivation, GM example coverage, and deferred-scope honesty. After remediating findings with a RED-first cycle, run a second code-quality/security review for cache/clone/fingerprint gaps, confusable identity reuse, disconnected/forked/cyclic lineage, rollback/replay asymmetry, and Fast/Integration placement. Require zero Critical/Important findings before closure.

- [ ] **Step 9: Record B.6 evidence without closing T070**

Mark this plan's completed boxes, append the exact commits/artifact directories/counts/durations/review verdict and Mortal-doc/afterlife-no-update rationale to `tasks.md`, and state that complication operations, add-recovery/course/recovery publication, heal/legacy, and T069-C remain open. Leave the T070 checkbox and GitHub issue #1536 open.

- [ ] **Step 10: Commit bounded closure documentation**

```powershell
git add -- docs/superpowers/plans/2026-09-04-t070b6-severity-rematerialization.md specs/1536-complete-wound-materialization/spec.md specs/1536-complete-wound-materialization/plan.md specs/1536-complete-wound-materialization/research.md specs/1536-complete-wound-materialization/data-model.md specs/1536-complete-wound-materialization/contracts/wound-effects-and-atomicity.md specs/1536-complete-wound-materialization/contracts/wound-materialization-command.md specs/1536-complete-wound-materialization/contracts/wound-canonical-lifecycle.md specs/1536-complete-wound-materialization/tasks.md OtherGuides/Wound_Materialization_Contract.md Examples/E_CLI_Wound_Materialization.txt Examples/example_validation_manifest.json BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs
git commit -m "docs(wounds): record severity rematerialization closure (#1536)"
git status --short
git show --check --stat --oneline HEAD
```

Expected: only the pre-existing untracked `.serena/` remains; no push, PR, merge, or issue closure occurs.
