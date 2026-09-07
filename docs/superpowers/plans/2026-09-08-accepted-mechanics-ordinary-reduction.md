# Accepted mechanics ordinary reduction and assembly Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement #1536 T081-B1: retain the actual ordinary resource/effect reduction separately from exactly-once final plan assembly.

**Architecture:** Move the existing complete ordinary reduction into a typed non-publishable intermediate. The current production wrapper reduces then completes through the new APIs; completion performs only final carrier/plan assembly and caches its result. Real retained causal stop/resume and incremental effect advancement remain mandatory T081-B2 work, not a claim of this task.

**Tech Stack:** C#, .NET 8, xUnit, System.Text.Json, PowerShell 7 bounded lanes.

## Global Constraints

- Source issue: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536); this task is T081-B1 under OPEN T081-B. Constitution: `.specify/memory/constitution.md`.
- Same worktree `E:/Games/worktrees/boe-1536-wound-materialization` and branch `1536-complete-wound-materialization`; no new branch/worktree or remote action. Preserve unrelated `.serena/` without inspecting or staging it.
- This is a production-consumed completed ordinary reduction plus once-only final assembly. It is NOT a source prefix, continuation, wound witness, new cache registration or accepted-source authority; T081-B2/C/D/E and top-level T081/T084/T085 stay open.
- Preserve existing mechanics, arithmetic, snapshot authentication, receipt binding, terminal replay, effect-use semantics, resource allocation order, owner/capacity checks, Mortal wound publication authorities, diagnostics and repair contexts. No schema migration, new GM field or validity flag.
- Preserve the existing discovery/actual receipt passes. Do not claim allocate-once across separate builds or receipt resubmissions. AppliedTransitions and ReplayTransitions remain distinct.
- The production wrapper must call the new reduction and completion APIs. No temporary AcceptedMechanicsPlan is retained as intermediate evidence. Assembly must not reallocate, reduce, trigger, project registered outcomes or finalize effects.
- Use `apply_patch` with absolute worktree paths. Rebase ONLY the companion's Update File header before application; default tools otherwise target the separate main checkout.
- One implementation agent owns the three C# source/test files and all bounded C# execution. No parallel source edits or C# runs. Parent owns plans, Spec Kit, acceptance and the ledger.
- Follow `docs/testing.md` using PowerShell 7 and `scripts/test-csharp.ps1`. Use the exact Focused selections with default five-minute limits. Parent alone runs ONE Fast after implementation; no full-solution run, duplicated Fast or PreMerge at this checkpoint.
- For unexpected fixture/API failures, inspect the existing source and report a required semantic/authority change before expanding scope. Never weaken an assertion or alter existing runtime semantics merely to obtain green.
- GM synchronization: this internal behavior-preserving split introduces no GM/player capability, field, accepted-source rule, pending surface or rendering change. No Mortal/afterlife prompt, example, matrix, manifest or GM source-guard edit is required. If actual behavior changes, stop and report that boundary.

### Task 1: Separate completed ordinary reduction from once-only assembly

**Files:**

- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs` — replace only the existing BuildAcceptedPlan block; retain the rest of the planner.
- Modify: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs` — change only the class declaration to sealed partial.
- Create: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.AcceptedMechanicsReduction.cs` — six complete behavior tests and their local fixtures.
- Parent-owned complete companion: `docs/superpowers/plans/2026-09-08-accepted-mechanics-ordinary-reduction.patch`.
- Parent-owned Spec Kit: `specs/1536-complete-wound-materialization/plan.md` and `tasks.md`.

**Interfaces and complete code:** The reviewed executable appendix below supplies every new API, complete test file, helper and exact verification command. The companion supplies every production replacement body. Its 470 removed lines were mechanically matched to current source after T081-A acceptance at `9114bc2c`; the old block ends immediately before ResolvePendingBoundary.

- [ ] **Step 1: Stage only the existing-API positive control and reflection RED.**

Use the appendix's first Fact and all existing-type fixture/observer helpers. Make the existing test class partial; do not stage the other five typed Facts or production API yet. The old production wrapper must successfully reduce a nonempty ordinary input and emit one resource event before the missing-API assertion. A failed setup/build is not semantic RED.

- [ ] **Step 2: Observe and record the exact pre-API RED.**

Run the first command in the appendix. Require Assert.NotNull(method) to fail only after the old-wrapper positive control has passed. Retain the actual summary/log/TRX artifact; do not repeat that RED after implementation.

- [ ] **Step 3: Stage all six tests, then apply the exact production companion.**

The owning declaration is exactly:

```csharp
public sealed partial class EffectAcceptedTurnPlannerTests
```

Use the complete test appendix and replace only this portable companion header before calling apply_patch:

```text
*** Update File: E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs
```

No additional runtime refactor or B2 placeholder is authorized. The brief provides full bodies; the temporary missing-type compile state before applying them is not another semantic RED.

- [ ] **Step 4: Verify the six new rows and all listed owning controls.**

Run appendix commands sequentially: six new rows, owning planner/cache/effect/source-guard selection, genuine signed conflict pair, existing pending-wave owners, and the four named real Mortal publication controls. Default five-minute limits apply. Preserve every failing artifact and diagnose actual failures against unchanged mechanics. Record discovered descriptors and executed rows separately where dynamic data expands; never call repeated MemberData IDs duplicate executions.

- [ ] **Step 5: Inspect, self-review, commit only owned code/tests, and report.**

Run git diff --check and inspect the three-file diff. Confirm the wrapper consumes ReduceAcceptedPlan/CompleteAcceptedReduction and no allocation, projection or effect completion moved into assembly. Commit only the three owned files as `refactor: separate ordinary mechanics reduction from assembly (#1536)`. Write exact RED/GREEN/control commands, artifact paths, counters, warning/error/timeout/cleanup status, files, deviations and self-review to the controller's report path. Release all C# ownership. Do not change tracking checkboxes or issue state.

Parent then inspects actual evidence, runs one Fast, and requests independent task-scoped review over the recorded BASE..HEAD. The appendix Fast command is parent-owned and MUST NOT also be run by the implementer. Acceptance requires all Critical/Important findings and reviewer CannotVerify items resolved. No conditional FullValidation is needed for the unchanged GM boundary.

## Controller self-check

- All complete bodies and six concrete tests are present; no B2 implementation placeholder is supplied or required.
- Current reduction order, receipt branches, final owner agreement and original registered outcome projection are preserved. Immutable input capture preserves wound stage/anchor/direct/treatment authority; assembly input deliberately drops outcome objects and allocation factories.
- The first RED has a nonempty old-production positive control. Unit fixtures are explicit typed inputs, not fake live snapshot evidence; genuine signed and publication boundaries are exercised by retained Integration controls.
- Calling ReduceAcceptedPlan twice is a fresh build, not a continuation. Existing history replay and two-pass receipts are not mislabeled as retained causal execution.
- B2 must retain the real graph scheduler, operation identities, ledger/history, causal transcript state and accepted effect advancement. It remains tracked separately under T081-B and cannot be closed by these six ordinary tests.

## Reviewed executable appendix

The following proposal was fully inspected by the parent, including the amended nonempty pre-API positive control and exact old-block match. Its metadata-only status records provenance before execution. This plan's ownership and verification instructions govern the implementation; the B2 section records OPEN follow-on requirements, not extra code to invent during Task 1.

# T081-B proposal: B1 ordinary reduction/assembly, B2 true causal continuation

Status: metadata-only executable proposal for parent review, 2026-09-08. No repository changes, C# execution, verification claim or task acceptance. T081-B remains OPEN after B1.

## Recommendation and boundary

Apply the attached complete B1 patch only after staging the tests below. The existing production `AcceptedMechanicsPlanner.BuildAcceptedPlan` becomes exactly `CompleteAcceptedReduction(ReduceAcceptedPlan(input, inputFingerprint))`. The intermediate retains the actual selected resource result, completed ordinary effect plan, allocated definition identities and projected owner contributions; it creates no temporary publishable plan. Exactly-once assembly consumes that value. There is no new cache registration or caller validity flag.

This is an independently testable **completed ordinary reduction**, deliberately not named a source prefix or continuation. It preserves the existing planner's complete-turn behavior and makes its irreversible reduction/effect-finalization boundary explicit before carrier assembly. It does NOT satisfy the dependent-suffix, frozen source-prefix or wound-admission portions of T081-B; does not close T081-C/D/E; and is not a spiritual-wound rollout.

Alternatives considered:

- A second call to BuildAcceptedPlan or BuildResources from the old baseline: rejected as a continuation design because it allocates and re-executes again.
- Retain a complete AcceptedMechanicsPlan as a “prefix”: rejected because it already contains publishable after-images and finalized effect-use semantics.
- B1 non-publishable ordinary reduction followed by B2 retained causal execution: selected. B1 is useful in existing production now; B2 must change real graph/transcript execution, not merely expose another DTO.

Parent has selected one logical turn, internal continuation, one common publication. Nothing here revisits that product choice or adds a player action, new turn, reroll or OD charge. Full rollout retains all lawful spiritual source mechanics; no legal source is silently converted to none or perpetual repair.

## Complete production edit

The sibling file `2026-09-08-accepted-mechanics-ordinary-reduction.patch` is a complete apply_patch-format replacement of the existing method block in:

`BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`, original lines 3408–3877 inclusive, from `internal static AcceptedMechanicsPlanningResult BuildAcceptedPlan(` through its closing brace before `ResolvePendingBoundary`.

The patch supplies every new definition and method body, with no source placeholders. All other methods, including BuildResources, ResolvePendingBoundary, BuildAwaitingReceiptPlan, ResolvedPendingReplaySession and carrier assembly implementation, stay unchanged. It needs no new using directive; the existing file imports Collections.ObjectModel and JSON namespaces. It does not make the planner partial or require a new production source file.

Exact production/test interface (all nested types below belong to AcceptedMechanicsPlanner):

```csharp
internal enum AcceptedMechanicsReductionKind
{
    Rejected,
    AwaitingResourceReceipt,
    CompletedOrdinaryReduction
}

internal static AcceptedMechanicsReduction ReduceAcceptedPlan(
    AcceptedMechanicsInput input, string inputFingerprint);

internal static AcceptedMechanicsPlanningResult CompleteAcceptedReduction(
    AcceptedMechanicsReduction reduction);

internal static AcceptedMechanicsPlanningResult BuildAcceptedPlan(
    AcceptedMechanicsInput input, string inputFingerprint);

// AcceptedMechanicsReduction:
internal AcceptedMechanicsReductionKind Kind { get; }
internal CompletedOrdinaryMechanicsReduction? Completed { get; }
internal AcceptedMechanicsResourcePlanningResult? DiscoveryResources { get; }
internal AcceptedMechanicsResourcePlanningResult? SelectedResources { get; }
internal IReadOnlyList<ValidationIssue> Issues { get; }
internal AcceptedMechanicsPlanningResult Complete();

// CompletedOrdinaryMechanicsReduction:
internal AcceptedMechanicsInput Input { get; }
internal string InputFingerprint { get; }
internal ResourceDefinitionCatalog Definitions { get; }
internal AcceptedMechanicsResourcePlanningResult Resources { get; }
internal EffectAcceptedTurnPlan? Effects { get; }
internal JsonObject? PendingAfterImage { get; }
internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages { get; }
internal IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions { get; }
```

These declarations document signatures; use the complete patch bodies, not this declaration-only excerpt.

### What moves, what remains identical

1. All definition creation, capacity composition, periodic resolution, ordinary/registered mutations, discovery pass, receipt binding, actual pass, next-wave decisions, effect completion, owner agreement and registered outcome Project calls remain in their existing order inside ReduceAcceptedPlan.
2. Every old failure exit returns a Rejected reduction. It has Completed=null and lazy completion returns the same failed planning result. No new eligibility guard or error filtering is introduced.
3. Both original awaiting-receipt exits produce AwaitingResourceReceipt with Completed=null. DiscoveryResources and SelectedResources are observations of attempted waves, explicitly not completed source evidence. Completion invokes the unchanged BuildAwaitingReceiptPlan with the captured original context, unfinalized effect plan, exact pending state and detached packet.
4. The completed branch retains the *actual selected* resourceResult, not merely discoveryResourceResult. This keeps AppliedTransitions and ReplayTransitions distinct, as well as resource Events, trigger executions, accepted reactions, resolved request IDs and the exact EffectBoundaryTranscript. Its Definitions are the very catalog materialized in this call. Effects is the exact result of the one existing CompleteAcceptedBoundaryTranscript call.
5. Registered outcome Project runs once before the intermediate exists; its companion roots and ordered owner transitions are copied into that value. Assembly cannot call Project again: captured context has an empty registered-outcome list and null allocation factories. The immutable input binding, exact before-image bytes, authority fingerprints, commands and all Mortal wound stage/anchor/publication authorities remain captured through existing constructors. The pending full fingerprint still sees the exact wound stages.
6. Only final carrier composition, path-conflict checks, touched/consumed path assembly, resource projection and final AcceptedMechanicsPlan construction move to AssembleCompletedAcceptedReduction. Completed.PendingAfterImage stores the already selected canonical pending state. No reducer, source projector or identity factory is called during completion.
7. The private Lazy with ExecutionAndPublication caches one planning result (including a final assembly failure). Repeated Complete calls return the same result/plan and do not reallocate, retrigger, reproject or run final effect completion. It is not a shared/global plan cache.
8. Existing terminal semantic replay rejection remains exactly where it was and examines the same discovery result fields. Mortal stage initial agreement, terminal owner/capacity behavior, final carrier validation and publication authority arguments remain intact.

The existing discovery/actual two-pass receipt algorithm is preserved, including its existing allocations. B1 adds no second reduction but does not claim “allocate once across pending receipt resubmission.” Calling ReduceAcceptedPlan a second time is a fresh ordinary build, not a resumed prefix. The history replay test below is labeled accordingly.

The intermediate holds immutable canonical catalogs/ledgers/plans and detached root/array/map images through their existing constructors/getters; it does not retain registered outcome objects or allocation factories in its assembly input. Existing diagnostic objects retain their existing repair-context behavior; B1 does not redefine diagnostics or snapshot authentication.

## Executable TDD sequence

No command below was executed by this author. Parent must inspect the patch and draft tests, stage a tracked B1 child of T081-B, then execute and inspect artifacts.

1. Change only the owning fast test declaration from `public sealed class EffectAcceptedTurnPlannerTests` to `public sealed partial class EffectAcceptedTurnPlannerTests`.
2. Initially add the new file below with ONLY the first reflection Fact, CreateOrdinaryReductionFixture and its helper/observer definitions. All these use existing types. The Fact first builds a separate nonempty ordinary fixture (effects:false, ordinaryCount:1), runs the existing production BuildAcceptedPlan, and requires Success, a Plan and exactly one resource event. Only after that positive control does it create fresh equivalent input and reflect ReduceAcceptedPlan. Run the first exact filter. Expected semantic RED is Assert.NotNull(method) for missing ReduceAcceptedPlan, after the existing wrapper's positive assertions have passed—not a compiler error, broken fixture or failing production control. If any earlier assertion fails, correct only the real fixture setup and re-establish this precise RED; do not waive the nonempty production control.
3. Add ALL remaining five strongly typed Facts from the complete file below **before applying production code**. Their missing-API compilation state is expected but is not additional semantic RED evidence.
4. Apply the complete production patch, then run the six-row focused selection. Inspect actual errors; never relax source/receipt/fingerprint assertions to obtain green.
5. Run owning planner/cache/effect and existing real signed/pending/Mortal integration controls below, then one Fast at the meaningful checkpoint. Do not mark T081-B closed. Parent handles later review/PreMerge under the tracked plan.

These fixture-free tests run the real command parser, catalog, effect planner, graph, resource reducer, effect completion and final assembler. The source catalog and before-images are explicit typed **unit fixture inputs**, not a forged validated snapshot, live source witness or permission to publish. The projection observer contributes no mutations/authority; it only records how many times the orchestration invokes the Project interface. Actual production registered-outcome behavior remains covered by the existing signed conflict and resource integration tests.

### Complete new fast test file

Path: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.AcceptedMechanicsReduction.cs`.

Reuses unchanged existing private `CreateReactionInput(JsonObject effect, params JsonObject[] definitions)` (owning file line 2442) and `CountingFactory : EffectIdentityFactory` (line 2800). No helper edit beyond making the owning class partial.

```csharp
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAcceptedTurnPlannerTests
{
    [Fact]
    public void OrdinaryReduction_ApiIsProductionUsableBeforeItCreatesAPlan()
    {
        var productionFixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1);
        var production = AcceptedMechanicsPlanner.BuildAcceptedPlan(productionFixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(productionFixture.Input));
        Assert.True(production.Success, DescribeOrdinaryReductionIssues(production.Issues));
        var productionPlan = Assert.IsType<AcceptedMechanicsPlan>(production.Plan);
        Assert.Single(productionPlan.ResourceEvents);

        // Fresh equivalent input: the production control must not consume this evaluation.
        var fixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1,
            state: productionFixture.Input.PlanningContext!.State,
            history: productionFixture.Input.PlanningContext.History);
        var method = typeof(AcceptedMechanicsPlanner).GetMethod(
            "ReduceAcceptedPlan", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method); // Isolated semantic RED before the new API exists.
        var reduction = method!.Invoke(null,
            new object[] { fixture.Input, AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input) });
        Assert.NotNull(reduction);
        var completed = reduction!.GetType().GetProperty("Completed",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(reduction);
        Assert.NotNull(completed);
        var complete = typeof(AcceptedMechanicsPlanner).GetMethod(
            "CompleteAcceptedReduction", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(complete);
        var result = Assert.IsType<AcceptedMechanicsPlanningResult>(
            complete!.Invoke(null, new[] { reduction }));
        Assert.True(result.Success, DescribeOrdinaryReductionIssues(result.Issues));
        Assert.NotNull(result.Plan);
        Assert.Single(result.Plan!.ResourceEvents);
        Assert.Same(result, complete.Invoke(null, new[] { reduction }));
    }

    [Fact]
    public void OrdinaryReduction_RetainsDefinitionsResourcesEffectsAndCompletesOnlyOnce()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true, createDefinition: true);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction,
            reduction.Kind);
        var completed = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduction.Completed);
        Assert.True(completed.Resources.IsValid);
        Assert.NotEmpty(completed.Resources.AppliedTransitions);
        Assert.Empty(completed.Resources.ReplayTransitions);
        Assert.True(completed.Resources.EffectBoundaryTranscript!.IsComplete);
        Assert.NotNull(completed.Effects);
        Assert.True(completed.Effects!.IsAcceptedBoundaryComplete);
        Assert.True(fixture.ResourceAllocations > 0);
        Assert.True(fixture.Effects.TransitionCalls > 0);
        Assert.Equal(1, fixture.Outcome.ProjectCalls);
        Assert.Same(completed.Resources, fixture.Outcome.ProjectedResources);
        fixture.Outcome.RejectFurtherProjection = true;
        var definitions = completed.Definitions.ToCanonicalJson();
        Assert.Contains("\"resourceKey\":\"mana\"", definitions, StringComparison.Ordinal);
        var history = completed.Resources.HistoryAfterImage!.ToCanonicalJson();
        var effects = completed.Effects.IdentityIndexAfterImage.ToJsonString();
        var before = (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls);

        // Existing input and plan getters expose detached roots, not mutable aliases.
        fixture.Input.InternalInputs["changed"] = true;
        fixture.Input.PlanningContext!.DefinitionRoot["definitions"] = new JsonArray();
        completed.Input.InternalInputs["changed"] = true;
        completed.Input.PlanningContext!.EffectIdentityRoot["entries"] = new JsonArray();
        completed.Effects.IdentityIndexAfterImage["entries"] = new JsonArray();

        var first = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.True(first.Success, DescribeOrdinaryReductionIssues(first.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(first.Plan);
        Assert.Equal(definitions, plan.DefinitionAfterImage.ToJsonString());
        Assert.Equal(history, plan.HistoryAfterImage.ToJsonString());
        Assert.Equal(effects, plan.EffectIdentityAfterImage.ToJsonString());
        Assert.Equal(completed.Resources.Events, plan.ResourceEvents);
        Assert.Equal(completed.Input.PlanningContext!.Owners.Fingerprint, plan.OwnerAuthority.Fingerprint);
        Assert.Same(completed.Effects, plan.EffectPlan);
        for (var index = 0; index < 3; index++)
        {
            var repeated = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
            Assert.Same(first, repeated);
            Assert.Same(plan, repeated.Plan);
        }
        Assert.Equal(before,
            (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls));
    }

    [Fact]
    public void OrdinaryReduction_PendingWaveHasNoCompletedSourceAndNoMechanicalPublication()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: true, bounded: true);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.AwaitingResourceReceipt,
            reduction.Kind);
        Assert.Null(reduction.Completed);
        Assert.NotNull(reduction.SelectedResources);
        Assert.NotEmpty(reduction.SelectedResources!.AcceptedPendingResolutions);
        Assert.False(reduction.SelectedResources.EffectBoundaryTranscript!.IsComplete);
        Assert.False(fixture.Input.PlanningContext!.EffectPlan!.IsAcceptedBoundaryComplete);
        Assert.Equal(0, fixture.Outcome.ProjectCalls);
        fixture.Outcome.RejectFurtherProjection = true;
        var counts = (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls);
        var first = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.True(first.Success, DescribeOrdinaryReductionIssues(first.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(first.Plan);
        Assert.True(plan.AwaitsPendingResolution);
        Assert.NotNull(plan.PendingPublicationAuthority);
        Assert.Empty(plan.ResourceEvents);
        Assert.Empty(plan.EffectCarrierAfterImages);
        Assert.Empty(plan.OwnerCompanionAfterImages);
        Assert.Equal(fixture.Input.PlanningContext.State.ToCanonicalJson(), plan.StateAfterImage.ToJsonString());
        Assert.Equal(fixture.Input.PlanningContext.History.ToCanonicalJson(), plan.HistoryAfterImage.ToJsonString());
        Assert.Equal(fixture.Input.PlanningContext.EffectIdentityRoot.ToJsonString(),
            plan.EffectIdentityAfterImage.ToJsonString());
        Assert.Same(first, AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction));
        Assert.Equal(counts,
            (fixture.ResourceAllocations, fixture.Effects.EffectCalls, fixture.Effects.TransitionCalls));
    }

    [Fact]
    public void OrdinaryReduction_ExposesActualAppliedAndReplayedTransitionsSeparately()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1);
        var first = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        var accepted = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(first.Completed);
        var originalTransition = Assert.Single(accepted.Resources.AppliedTransitions);
        var next = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 2,
            state: accepted.Resources.StateAfterImage, history: accepted.Resources.HistoryAfterImage);
        // Characterize canonical history replay, not an in-memory prefix continuation.
        var replay = AcceptedMechanicsPlanner.ReduceAcceptedPlan(next.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(next.Input));
        var replayed = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(replay.Completed);
        Assert.Equal(originalTransition, Assert.Single(replayed.Resources.ReplayTransitions));
        Assert.Single(replayed.Resources.AppliedTransitions);
        Assert.Single(replayed.Resources.Events);
        var count = next.ResourceAllocations;
        var final = AcceptedMechanicsPlanner.CompleteAcceptedReduction(replay);
        Assert.True(final.Success, DescribeOrdinaryReductionIssues(final.Issues));
        Assert.Equal(replayed.Resources.HistoryAfterImage!.ToCanonicalJson(),
            final.Plan!.HistoryAfterImage.ToJsonString());
        Assert.Same(final, AcceptedMechanicsPlanner.CompleteAcceptedReduction(replay));
        Assert.Equal(count, next.ResourceAllocations);
    }

    [Fact]
    public void OrdinaryReduction_ExistingWrapperUsesSameSemanticReductionAndNeverReallocatesOnCompletion()
    {
        var direct = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1);
        var wrapped = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1,
            state: direct.Input.PlanningContext!.State,
            history: direct.Input.PlanningContext.History);
        var reduced = AcceptedMechanicsPlanner.ReduceAcceptedPlan(direct.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(direct.Input));
        var result = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduced);
        var existing = AcceptedMechanicsPlanner.BuildAcceptedPlan(wrapped.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(wrapped.Input));
        Assert.True(result.Success, DescribeOrdinaryReductionIssues(result.Issues));
        Assert.True(existing.Success, DescribeOrdinaryReductionIssues(existing.Issues));
        // Shared captured baseline plus identical scripted new IDs: bootstrap IDs are random.
        Assert.Equal(result.Plan!.PreparedPlanFingerprint, existing.Plan!.PreparedPlanFingerprint);
        Assert.Equal(direct.ResourceAllocations, wrapped.ResourceAllocations);
        Assert.Same(result, AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduced));
        Assert.Equal(direct.ResourceAllocations, wrapped.ResourceAllocations);
    }

    [Fact]
    public void OrdinaryReduction_RejectedResourcesNeverExportCompletedOrProjectAnOutcome()
    {
        var fixture = CreateOrdinaryReductionFixture(effects: false, ordinaryCount: 1, rejectSource: true);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(fixture.Input,
            AcceptedMechanicsPlanFingerprints.ComputePlanningInput(fixture.Input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.Rejected, reduction.Kind);
        Assert.Null(reduction.Completed);
        Assert.Contains(reduction.Issues, issue => issue.Code == "resource_source_unknown");
        Assert.Equal(0, fixture.Outcome.ProjectCalls);
        fixture.Outcome.RejectFurtherProjection = true;
        var count = fixture.ResourceAllocations;
        var result = AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction);
        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(reduction.Issues, result.Issues);
        Assert.Same(result, AcceptedMechanicsPlanner.CompleteAcceptedReduction(reduction));
        Assert.Equal(count, fixture.ResourceAllocations);
    }

    private static OrdinaryReductionFixture CreateOrdinaryReductionFixture(
        bool effects, bool bounded = false, bool createDefinition = false, int ordinaryCount = 0,
        ResourceStateLedger? state = null, ResourceHistoryState? history = null, bool rejectSource = false)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(1, 41, 10, 10, 10, 10, 10);
        Assert.True(bootstrap.IsValid, DescribeOrdinaryReductionIssues(bootstrap.Issues));
        var definitions = bootstrap.Definitions!;
        state ??= bootstrap.State!;
        history ??= bootstrap.History!;
        var owners = ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions);
        var fixture = new OrdinaryReductionFixture();
        EffectAcceptedTurnPlan? effectPlan = null;
        var effectCommands = new JsonObject();
        var events = new JsonObject { ["turn"] = 42, ["events"] = new JsonArray() };
        var identity = new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() };
        if (effects)
        {
            var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
            definition["triggers"]![0]!["resolutionMode"] = bounded ? "bounded_receipt" : "deterministic";
            var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_damage");
            effect["triggers"] = definition["triggers"]!.DeepClone();
            var effectInput = CreateReactionInput(effect, definition);
            var lifecycle = effectInput.EventInput["lifecycleEvents"]![0]!;
            lifecycle["eventRef"] = "turn_42:effect:on_owner_turn_end:1";
            lifecycle["causalEventRef"] = "turn_42:owner_turn_end:1";
            lifecycle["phase"] = "owner_turn_end";
            lifecycle["triggerId"] = "on_owner_turn_end";
            var result = new EffectAcceptedTurnPlanCache(fixture.Effects).GetOrBuild(effectInput);
            Assert.True(result.Success, DescribeOrdinaryReductionIssues(result.Issues));
            effectPlan = result.Plan!;
            effectCommands = effectInput.RawCommands.DeepClone().AsObject();
            events = effectInput.EventInput.DeepClone().AsObject();
            identity = effectInput.PreTurnIdentityIndex!.DeepClone().AsObject();
        }
        var changes = new JsonArray();
        var exports = new List<ResourceMutationSourceExport>();
        for (var index = 0; index < ordinaryCount; index++)
        {
            var eventRef = $"turn_42:resource:{index + 1}";
            changes.Add(new JsonObject
            {
                ["operation"] = "damage",
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["resourceKey"] = "health", ["amount"] = 1,
                ["source"] = new JsonObject { ["kind"] = "combat_outcome" },
                ["eventRef"] = eventRef, ["reason"] = "Accepted ordinary reduction characterization"
            });
            exports.Add(new ResourceMutationSourceExport("combat_outcome", eventRef, ReductionHash,
                ResourceMutationSourceState.Active, false)
            {
                BoundOwner = new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Player, "player_current")
            });
        }
        var commandsRoot = new JsonObject { ["resourceChanges"] = changes };
        if (createDefinition)
            commandsRoot["resourceDefinitionCreations"] = JsonNode.Parse("""
            [{
              "definitionRef":"mana_v1","eventRef":"turn_42:resource:1","reason":"Setting materialization",
              "definition":{
                "resourceKey":"mana","definitionVersion":1,"displayName":"Mana",
                "numericKind":"integer","unit":"point","quantum":1,
                "minimumPolicy":{"kind":"definition_fixed","value":0},
                "capacityPolicy":{"kind":"instance_fixed"},
                "initializationPolicy":{"kind":"maximum"},
                "allowedOwnerKinds":["player"],"allowedOperations":["spend","gain"],
                "defaultFloorPolicy":"reject_below_minimum","defaultCapPolicy":"clamp_to_maximum",
                "visibility":"player_visible"
              }
            }]
            """);
        var commands = ResourceAcceptedTurnInputComposer.Parse(commandsRoot.ToJsonString());
        Assert.True(commands.IsValid, DescribeOrdinaryReductionIssues(commands.Issues));
        var sources = ResourceMutationSourceCatalog.Create(
            rejectSource ? Array.Empty<ResourceMutationSourceExport>() : exports.ToArray());
        Assert.NotNull(sources.Catalog);
        Assert.Empty(sources.Issues);
        var context = new AcceptedMechanicsPlanningContext(
            definitions.ToCanonicalRoot(), definitions, state, history, owners, sources.Catalog!,
            commands, identity, effectPlan,
            registeredSystemOutcomes: new[] { fixture.Outcome },
            resourceIdentityFactory: new AcceptedMechanicsIdentityFactory(() =>
                new Guid(++fixture.ResourceAllocations, 0, 0, new byte[8])),
            effectIdentityFactory: fixture.Effects);
        var beforeImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in new[]
        {
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, ResourceMaterializationContract.CommandPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath, EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectAcceptedTurnPlan.CommandPath, EffectCarrierCatalog.PlayerPath,
            ResourcePendingResolutionState.PendingPath
        })
            beforeImages[path] = new CanonicalBeforeImage(false, null);
        fixture.Input = new AcceptedMechanicsInput(
            "session_effect_reaction", "request_reduction", "snapshot_effect_reaction", "mortal_world", 42,
            events, commands.Root, effectCommands, new JsonObject(), new JsonObject(),
            new AcceptedMechanicsAuthorityFingerprints(
                ReductionHash, owners.Fingerprint, state.Fingerprint, history.Fingerprint,
                ReductionHash, ReductionHash, ReductionHash, ReductionHash, ReductionHash,
                ReductionHash, ReductionHash, ReductionHash, ReductionHash, ReductionHash, ReductionHash),
            beforeImages, Array.Empty<ValidationIssue>(), context);
        return fixture;
    }

    private const string ReductionHash =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static string DescribeOrdinaryReductionIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Expected}; {issue.Actual}"));

    private sealed class OrdinaryReductionFixture
    {
        internal AcceptedMechanicsInput Input { get; set; } = null!;
        internal int ResourceAllocations;
        internal CountingFactory Effects { get; } = new();
        internal ReductionProjectionObserver Outcome { get; } = new();
    }

    // Observation only: no invented resource/effect behavior. Every mutation and
    // trigger above still runs through the real production planner and reducer.
    private sealed class ReductionProjectionObserver : IResourceRegisteredSystemOutcomeDraft
    {
        internal int ProjectCalls { get; private set; }
        internal bool RejectFurtherProjection { get; set; }
        internal AcceptedMechanicsResourcePlanningResult? ProjectedResources { get; private set; }
        public string Fingerprint => ReductionHash;
        public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.Empty<ResourceMutationSourceExport>();
        public IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.Empty<ResourceMutationIntent>();
        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
            new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);

        public ResourceRegisteredSystemOutcomeProjectionResult Project(
            AcceptedMechanicsResourcePlanningResult resources)
        {
            Assert.False(RejectFurtherProjection, "Assembly must not project registered outcomes again.");
            ProjectCalls++;
            ProjectedResources = resources;
            return new ResourceRegisteredSystemOutcomeProjectionResult(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                Array.Empty<AcceptedMechanicsOwnerTransition>(),
                Array.Empty<ValidationIssue>());
        }
    }
}
```

### Required focused controls

Use PowerShell 7 from the worktree. Each Focused invocation starts with the default five-minute bound; do not raise to fifteen without measured evidence for that exact coherent selection.

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests.OrdinaryReduction_ApiIsProductionUsableBeforeItCreatesAPlan"
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests.OrdinaryReduction_"
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests|FullyQualifiedName~AcceptedMechanicsPlanCacheTests|FullyQualifiedName~EffectAcceptedTurnPlannerTests|FullyQualifiedName~WoundMaterializationSourceGuardTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_SignedPressureExchangePublishesWithNoOwningPhaseErrors|FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_FinalValidationRejectsDieOutsideSignedPool"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourcePendingResolutionIntegrationTests|FullyQualifiedName~EffectPendingWaveIntegrationTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.GuaranteedResourceQuantity_PersistedConfirmedPublicationSpendsOnceAndCommitsAtFullPipelineEnd|FullyQualifiedName~MortalWoundTreatmentResolverTests.GuaranteedMixedItemAndResourceConsumption_PublishesAtomicallyOnce|FullyQualifiedName~MortalWoundTreatmentResolverTests.GuaranteedItemConsumption_ItemCapacityUsesCommonFinalizationOrdinal|FullyQualifiedName~MortalWoundTreatmentResolverTests.GuaranteedItemConsumption_RepeatedItemCapacityTransitionsChainAcrossCommonOrdinalGap"
.\scripts\test-csharp.ps1 -Lane Fast
```


Important existing preservation rows:

- `ResourcePendingResolutionIntegrationTests.BoundedTurnEnd_BeforeReceiptPublishesOnlyPendingTechnicalState`.
- `ResourcePendingResolutionIntegrationTests.FullTurnReceipt_ResolvesThroughCommonMutationAndAdvancesEffectExactlyOnce`.
- `ResourcePendingResolutionIntegrationTests.SameTurnAppliedBoundedEffect_RebindsAcceptedApplicationAcrossReceiptResubmission`.
- `EffectPendingWaveIntegrationTests.CausalPendingWaves_RemainAtomicUntilTerminalReplay`.
- `EffectPendingWaveIntegrationTests.LastUsePendingFrontier_ReplaysExactTranscriptAndExpiresOnlyOnce`.
- `EffectPendingWaveIntegrationTests.TerminalReplay_ChangedOriginalCommandEnvelopeFailsClosedWithoutAfterImagesOrWrites`.
- `EffectPendingWaveIntegrationTests.IndependentAcceptedTurn_RestartsAtWaveZeroAndReplaysOnlyItsOwnTerminalBindings`.
- `AcceptedMechanicsPlanCacheTests.WoundPendingPlan_ProductionPlannerSealsDerivedPendingState`, `AcceptedMechanicsPlanner_WoundStagesProduceTypedCarrierPublication`, `WoundRootAssembly_ReturnedRootsAreDeeplyDetached` and the existing shared-afterlife/combatant root preservation rows.

B1 intentionally introduces no GM-authored fields, accepted source rule, pending root, renderer behavior or afterlife mechanics. No GM prompt/example/contract migration is required for this internal behavior-preserving split. Parent tracking documents must explicitly preserve T081-B's remaining obligations. Later B2/C/D runtime changes still require the normal contract/prompt/example synchronization.

## B2: actual executable continuation requirements, not a completed-result wrapper

The existing graph/session/transcript API establishes why B1 cannot be called a prefix cursor:

| Current seam | Retained execution required for B2 |
| --- | --- |
| BuildResources 5545; AllocatedIdentityRegistry at 8850; PrepareCapacityTransitions/PrepareMutations before graph construction | Retain the allocated operation/transition IDs and alias collision registry, selected definitions and capacity preparations. Do not allocate a nominally identical prefix again. Exact new suffix IDs may be allocated once when that suffix is legally prepared. |
| PrepareCompleteResourceGraph 6790, CompleteResourceGraphPreparation 8933 | Today this expands/prepares the complete known graph before reduction. Retain prepared mutations keyed both by ResourceOperationKey and operation ID, trigger candidates, source exports and lineage/depth authority. A wound-dependent suffix prepared against the old actor/effect image must be invalidated/recomposed, not frozen as already accepted. |
| ResourceTriggerGraph.CreateExecutionScheduler 9050; ResourceTriggerGraphExecutionScheduler 9381 | Retain the scheduler's indegrees, runnable/skipped sets, current in-flight node and completed graph frontier, with the existing causal-lane selection policy. A plain OrderedNodes index is insufficient: execution depends on produced events and innermost open boundaries. |
| BuildResources 5687 onward | Retain workingLedger, workingHistory, events, appliedTransitions, replayTransitions, executionSequence, directBaselineState and postDirectState. The latter is captured at the first RegisteredSystemOutcome-or-later mutation and must not be recomputed from a wound-modified suffix baseline. Retain original resource/dice/event coordinates and exact causal proof for accepted prefix transitions. |
| BuildResources 5744–5835 and causal local functions through the scheduling loop | Retain effectTranscriptBuilder; producedEvents; appliedTriggerMutationKeys; acceptedCandidates/identities/activations/boundaries; acceptedByBoundary; remainingOperationsByBoundary; completedOperationIds; parentBoundaryByOrdinal; closedBoundaries; unresolvedPendingBoundaries; candidatesByProducerEvent; and the accepted-use arbiter state. These are actual execution state, not reconstructible from owner projection. |
| AcceptedEffectBoundaryTranscript.Builder.SealUseProjection 1412; SealPendingFrontier 1435; Freeze 1478 | Freeze caches and seals the builder; subsequent mutation throws. A pending frontier is not a complete source prefix. A wound continuation must stop at an exact closed causal boundary and expose a detached prefix checkpoint without sealing terminal use projection or pretending a pending transcript is complete. That requires a new internal checkpoint operation consumed by the running session, not relaxed final transcript validation. |
| EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript 2205; FinalizeAfterResourceGraphCore 2285 | The existing finalizer rejects an already-completed plan and requires a complete transcript with exact five-field authority including skill scope. It is a final ordinary boundary API, not an incremental update API. Do not call it once per source and again on the same final plan. Prefix effect state needed by later exchanges must be produced by a real accepted-boundary advancement mechanism while terminal/common projection remains once-only. |
| ResolvedPendingReplaySession 3070; Bind and ValidateComplete; ResolvePendingBoundary | Preserve existing authenticated receipt binding, causal material, wave ordinals and terminal semantic replay rules. In-memory continuation must keep the prepared identities/replay consumption state and resume only the exact admitted receipt output. Cold reconstruction needs exact retained identities and proof; recreating the old discovery/actual passes with new IDs is not continuation. |

### Smallest honest remaining executable unit

B2 must integrate a live resource/effect execution session with the existing production drain, not just add a state holder:

1. Move the existing preparation, working sets, scheduler, causal local functions and transcript builder into one owned session. Its ordinary full-drain route replaces the internals of BuildResources and preserves exact existing results/statistics. No second allocator is used when reading a checkpoint or completing that session.
2. Add an internal stop/resume path at a typed closed causal source boundary, together with the builder checkpoint/accepted effect advancement it actually requires. Keep unresolved receipt frontiers a distinct outcome. The prefix result contains exact applied/replayed evidence and immutable images, but no AcceptedMechanicsPlan or final effect completion seal.
3. Resume from the same owned state after the later authorized wound decision changes the actor/effect contribution. Freeze accepted source prefix and original coordinates only; rebuild/rebind the unaccepted wound-dependent suffix. The true B2 caller must consume this stop/resume path. Until that caller exists, label only the ordinary session refactor accepted, not prefix continuation.
4. Drain to terminal, seal full transcript, finish effect projection exactly once, run registered outcome agreement on the actual transcript and invoke the B1 final assembly once. Strict source admission and the whole-turn validation stay separate; none of this makes a whole final conflict validator provisional.

The exact public-to-internal owner/continuation lifetime and engine transport are parent-owned later work. This proposal deliberately supplies no fictitious B2 method signatures whose required checkpoint types do not exist yet.

Required B2 executable controls before claiming T081-B:

- One ordinary uninterrupted run versus the same session stopped at an already-closed boundary then resumed with no change: exact definitions/operation/transition/effect identities, applied/replayed evidence, resource events, final state/history/carriers and allocation counts.
- Two current exchanges: accept the first source, apply its legal wound effect, then validate/reduce the dependent later exchange with original turn/dice/resource coordinates. A stale pre-wound suffix must fail or be recomposed; it must not be falsely frozen as accepted.
- Stop/resume while a bounded resource receipt wave is unresolved: no source eligibility and no mechanical/owner/carrier publication. Supply exact receipt, then complete or enter the next wave retaining prior allocated identity/evidence. Forged, stale, duplicate and swapped bindings fail closed.
- A last-use/terminal reaction and after-component dependency crossing the selected boundary: accepted-use counts and terminal reservations match the uninterrupted causal trace; no premature expiry or repeated finalization.
- Retry and cold reconstruction retain exact accepted prefix identity/proof; changed source prefix rejected; changed unaccepted suffix handled under the new continuation contract, not silently accepted as old evidence.
- Failed suffix/decision/final validation produces no common publication. Repeated terminal completion returns one immutable plan and makes no further allocation, Project call or trigger execution.

There is no product-choice blocker for B1. The genuine B2 technical dependency is a production-consumed mutable execution/checkpoint boundary and prefix effect advancement; the current sealed completed transcript/plan APIs cannot implement it honestly. Arithmetic overflow remains a risk only, not an established failing fixture or a reason to expand this refactor. The genuine signed fixture accepted by the parent remains separate evidence; this author ran no C# controls.
