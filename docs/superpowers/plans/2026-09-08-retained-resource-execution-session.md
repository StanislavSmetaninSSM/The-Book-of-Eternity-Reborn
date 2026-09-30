# Retained resource execution session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing resource reducer suspend and resume its same execution at a closed causal frontier, with unchanged ordinary production results.

**Architecture:** One owned C# iterator retains graph identities, scheduler, ledger/history, arbiter and transcript builder. The existing BuildResources production wrapper drains it; explicit stepping returns detached resource observations without sealing history/effect state. This is the fixed-graph prerequisite only, not full spiritual wound continuation.

**Tech Stack:** C#/.NET8, existing immutable resource contracts, xUnit, PowerShell7 bounded lanes.

## Global Constraints

- Tracked issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), nested T081-B2A. Follow constitution and specs/1536-complete-wound-materialization/{spec,plan,tasks}.md plus contracts/spiritual-wound-live-turn-boundary.md.
- Modify exactly BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs (BuildResources block only), BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs (sealed -> sealed partial only), and new BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs.
- Production must call BeginResourceExecution and Drain; no sidecar-only implementation, repeated graph preparation/allocation/reduction, history/transcript final freeze at checkpoint, temporary AcceptedMechanicsPlan, accepted source flag or publication.
- Preserve every original failure expression, reduction/graph/resolver order, receipt/pending/final agreement rule, resource/effect identities, applied versus replay transitions and planner work counter. Update the pause marker only after each of the four existing scheduler.Complete calls.
- Explicit pause is permitted only after an operation completes, before the next frontier/node, with no unresolved pending boundary and all currently opened parent boundaries closed. Ordinary drain does not construct checkpoint images; CheckpointCount is zero.
- A checkpoint is resource-only detached observation, not whole-exchange proof. Lawful zero-cost/zero-mutation sources remain required. This fixed prepared graph cannot reflect wound-dependent suffix changes; never claim otherwise.
- Full T081-B2 remains open for incremental effects, wound-dependent suffix recomposition, cross-side exchange/source authority and authenticated receipt/cold continuation. Scalar spiritual arts remain0..5; no art-XP objects, gameplay migration, extra player turn/OD/dice/progression or safe-cycle change.
- Same E:/Games/worktrees/boe-1536-wound-materialization, branch1536-complete-wound-materialization. Apply patches with absolute worktree paths. No remote/new branch/worktree/issue closure/session cleanup. Preserve unrelated .serena/ without inspection or staging.
- The implementer solely owns these C# files and bounded C# execution until reporting completion. PowerShell7 plus scripts/test-csharp.ps1 only, specified Focused selections/default5m. No child Fast/PreMerge/full-solution run; parent owns Fast after actual focused evidence review.
- Tests must first prove a nonempty successful old production result before the missing-API reflection RED. Use identical scripted IDs for whole versus resumed comparisons, including all observable result fields; retain every failed artifact and stop on unexpected runtime boundary changes.
- No Mortal/afterlife GM-facing capability, schema, pending transport, source-authority or UI behavior is changed by this internal prerequisite. No GM prompt/example/matrix/manifest/source-guard synchronization is required for this bounded unit; any discovered external behavior change must be reported, not silently absorbed.

### Task 1: Retain actual fixed-graph resource execution and prove unchanged production behavior

**Files:** The three production/test paths in Global Constraints. Parent owns durable plan/spec/task tracking and acceptance.

**Interfaces:** Consume existing AcceptedMechanicsResourceInput, AcceptedMechanicsIdentityFactory, graph/scheduler/working-set/transcript contracts; produce the nested session/step/checkpoint API fully implemented in the companion patch below. B1 ordinary reduction/assembly stays untouched.

- [X] **Step1: Verify the exact current method anchor and stage the semantic RED described below.**
- [X] **Step2: Apply the complete nine-test source and production companion, preserving the mechanical boundaries below.**
- [X] **Step3: Run the six exact bounded controls below, inspect actual artifacts and diagnose failures without broadening the contract.**
- [X] **Step4: Self-review, compare the production block/tests with the plan, commit only the three scoped files and write the full report.**

Companion repository path: `docs/superpowers/plans/2026-09-08-retained-resource-execution-session.patch`. It contains the complete replacement, not an instruction to invent method bodies. Rebase its file header to the absolute active worktree before apply_patch. The following design/test appendix is the controller-read complete proposal, now binding for this task. Neither the proposal nor this plan is test evidence.

## Complete production patch and exact boundaries

Use the repository companion `2026-09-08-retained-resource-execution-session.patch`. It contains every new type/method and the complete mechanically relocated iterator body. It replaces only the current BuildResources method, from its signature to its closing brace immediately before PrepareCompleteResourceGraph. It does not replace a whole file, rely on B1 line numbers, alter BuildAcceptedPlan or touch the transcript/scheduler classes.

The original resource method block was re-read by signature and compared exactly to the patch removal block after B1 edits had shifted its line numbers: matched. Do the same check when executing this proposal; do not overwrite another worker's change.

The patch introduces these fully implemented, internal members nested in AcceptedMechanicsPlanner:

```csharp
internal static ResourceExecutionSession BeginResourceExecution(
    AcceptedMechanicsResourceInput input, AcceptedMechanicsIdentityFactory identityFactory);

internal sealed class ResourceExecutionSession : IDisposable
{
    internal AcceptedMechanicsResourcePlanningResult? Result { get; private set; }
    internal int CheckpointCount { get; private set; }
    internal ResourceExecutionStep AdvanceToClosedBoundary();
    internal AcceptedMechanicsResourcePlanningResult Drain();
    public void Dispose();
}

internal sealed class ResourceExecutionStep
{
    internal ResourceClosedBoundaryCheckpoint? Checkpoint { get; }
    internal AcceptedMechanicsResourcePlanningResult? Result { get; }
}

internal sealed class ResourceClosedBoundaryCheckpoint
{
    internal ResourceOperationKey LastCompletedOperation { get; }
    internal int NextExecutionSequence { get; }
    internal ResourceDefinitionCatalog Definitions { get; }
    internal ResourceStateLedger State { get; }
    internal ResourceHistoryState HistoryBaseline { get; }
    internal IReadOnlyList<ResourceTransition> PendingHistoryTransitions { get; }
    internal IReadOnlyList<ResourceTransition> AppliedTransitions { get; }
    internal IReadOnlyList<ResourceTransition> ReplayTransitions { get; }
    internal IReadOnlyList<ResourceAppliedEvent> Events { get; }
    internal IReadOnlyList<long> ClosedEffectBoundaryOrdinals { get; }
    internal AcceptedMechanicsPlannerStatistics Statistics { get; }
}
```

This excerpt documents the interface; the companion supplies the implementations, private control members and factories. There are no omitted production bodies.

The production consumer remains:

```csharp
internal static AcceptedMechanicsResourcePlanningResult BuildResources(
    AcceptedMechanicsResourceInput input,
    AcceptedMechanicsIdentityFactory identityFactory)
{
    using var session = BeginResourceExecution(input, identityFactory);
    return session.Drain();
}
```

### Mechanical transformations, not new reduction rules

- Rename/move the original body to private IEnumerable<ResourceExecutionStep> ExecuteResourceSession(input, identityFactory, session).
- Convert its 30 main-method failure returns to a Finished failure step plus yield break, retaining every original expression and Statistics call. Its local functions still return their existing validation lists; no local return becomes an iterator.
- Convert the final successful resource result to one Finished step. Finish still seals the exact pending/use transcript, freezes history once, runs final agreement, and creates the same result fields.
- After each of the four existing scheduler.Complete(node) sites, remember the exact completed ResourceOperationKey. This includes skipped operations; a skipped-operation checkpoint is merely an execution frontier, not source acceptance.
- At the top of the next loop iteration, before ReadyPendingFrontiers or taking another scheduler node, an explicit stepping caller may pause iff an operation just completed, unresolvedPendingBoundaries is empty and every key in parentBoundaryByOrdinal is in closedBoundaries.
- The checkpoint captures State via ResourceWorkingLedger.Freeze, the original immutable history baseline, workingHistory.PendingTransitions, exact applied/replayed lists, events, next execution sequence and closed boundary ordinals. **It does not call ResourceHistoryWorkingSet.Freeze or AcceptedEffectBoundaryTranscript.Builder.Freeze.** Both would seal live state and break resumption.
- The iterator's retained closure is the live execution state. No second BuildResources, graph preparation, allocator, resolver expansion or history seed runs on resume. The identity registry and prepared graph remain part of the original invocation. No second nominal allocation is presented as replay.
- Full Drain sets the private stepping request false. The closed-frontier predicate short-circuits before scanning/copying images, and CheckpointCount remains zero. Existing planner statistics are unchanged. Explicit stepping has snapshot overhead, deliberately outside the existing mechanics-work counters.

The frontier condition is conservative: all *currently opened* effect boundaries must be closed, not merely a leaf. It never means every future graph node or effect has already been validated. A two-component or nested causal effect cannot be interrupted between its still-open components. A graph with no completed resource operation (including a capacity-only or zero-mutation graph) finishes without a checkpoint; that does **not** disqualify a lawful zero-cost spiritual source.

### Lifetime, ownership and alias contract

- Begin validates null inputs synchronously, then creates one owned execution iterator. No preparation/allocation runs until Advance or Drain.
- The owner must dispose the session when it abandons it. The production wrapper uses using. Dispose is idempotent and never drains, finalizes or publishes unfinished work.
- Exactly one Advance/Drain may run at a time. Re-entrant/concurrent advance or disposal while execution is active throws InvalidOperationException, using an Interlocked gate. The normal resource identity factory/resolver can throw; the original exception propagates, the iterator is disposed and that session is faulted permanently.
- A Finished step is terminal, whether its result is complete, pending, or invalid. Further Advance/Drain throws InvalidOperationException; after Dispose it throws ObjectDisposedException. Result remains available as the immutable last terminal resource result for inspection; reading it does not execute again. The B1 once-only plan completion API is separate and unchanged.
- A Paused step has Checkpoint!=null and Result=null. Its resume point is exactly after that yield in the original invocation. No arbitrary callback or caller-supplied validity/authority flag is accepted by this API.
- Existing AcceptedMechanicsResourceInput constructors copy mutations and dependency/event requirement arrays; its catalogs/ledgers are immutable and its getters return detached collections. Checkpoints copy their own arrays and return read-only copies; later reduction cannot mutate an old checkpoint.
- Allocation factories and the existing ResourceEventMutationResolver are owned collaborators, not serializable value images. They are deliberately retained, not cloned or replaced by a filesystem callback. For this fixed-graph unit all resolver preparation occurs before the first pause. An owner must not treat mutating these collaborators as a supported suffix update.
- No session, checkpoint or step is registered as an accepted source or common plan. These constructors are internal mechanics types, not minting APIs for snapshot/source authority. The real signed loaders and final validation remain unchanged.

## Why this does not close full B2

The source outcome composer currently prepares the entire new exchange suffix, including source hashes over full exchange JSON. It chains player and opposition operations separately; it does not introduce a cross-side exchange barrier. Therefore a generic closed resource frontier does not prove that **both** branches of one exchange and their causal effects have completed. Zero amount may legitimately emit no resource mutation. Source admission must account for those facts explicitly, never infer eligibility from any one checkpoint or reject a lawful source because a zero-amount side has no transition.

This patch intentionally keeps PrepareCompleteResourceGraph and registered-source preparation unchanged. The remaining suffix has already been prepared against the original candidate image, but it is not “accepted source prefix.” Do not apply a wound to an actor then call this unchanged session's Drain and pretend its old suffix now reflects that wound. There is no suffix replacement method in this bounded API.

The exact next dependent code boundaries are:

1. **Incremental effect evidence/advancement.** AcceptedEffectBoundaryTranscript.Builder.Freeze seals the builder and validates a terminal use projection or pending frontier. ResourceHistoryWorkingSet.Freeze similarly marks the history working set frozen and increments whole-history validation counts. EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript requires a complete transcript with its exact five-field effect authority and rejects a previously completed plan. A resource checkpoint contains none of those terminal seals. The future prefix effect operation needs an immutable, nonterminal accepted-boundary view plus advancement that does not apply turn-end expiry/use projection twice. Do not change the existing final validator to accept an incomplete transcript.
2. **Suffix recomposition and stable graph identity.** PrepareCompleteResourceGraph currently preallocates and expands all known nodes; ResourceTriggerGraphExecutionScheduler captures its graph dictionaries/indegrees at construction and has no graph append/replace operation. The future path must preserve frozen accepted prefix IDs, execution coordinates, causal/transcript/arbiter state and collision registry while discarding/rebinding only unaccepted wound-dependent suffix descriptors/source hashes. It must prove exact scheduler rebuild or splice equivalence and allocate each genuinely new suffix identity once.
3. **Exchange barrier/source admission.** The parent-owned source plan must map the exact accepted conflict exchange to both side-operation branches (including lawful zero-delta evidence) plus causal closure. This checkpoint does not supply such a map, wound source proof, caller approval or owner-projection shortcut.
4. **Pending receipt continuation/cold recovery.** A pending resource result remains terminal to this session so the ordinary two-pass receipt path is preserved. ResolvedPendingReplaySession Bind/ValidateComplete, exact causal pending fingerprints/wave ordinals and terminal replay rules remain in the existing wrapper. Keeping one session alive across a new authenticated receipt and rebuilding it cold with exact identity retention is additional work, not achieved by reading Result or calling Begin a second time.

No engine/GM transport or new player action is proposed. Whole final conflict validation remains a final check, not a provisional source check. Arithmetic overflow is not fixed or asserted as an observed failure in this refactor.

## Complete tests and exact staging

Proposed source file: `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs`.

Exact owning declaration change in `AcceptedMechanicsPlannerTests.cs`:

```diff
-public sealed class AcceptedMechanicsPlannerTests
+public sealed partial class AcceptedMechanicsPlannerTests
```

Reuse unchanged real owning helpers: BaselineCharges, Intent, BaselineHealth, PeriodicEvent, InvokePeriodicResourceResolution, InvokeResourceEventResolution, CreateCatalog and the existing coordinate/source constants. The tests use actual validated typed resource fixtures, real graph/reducers/periodic resolution and actual identity factory allocation. They do not fabricate a validated snapshot or accepted spiritual source. Allocation counters and a deliberate throwing/re-entry observer instrument execution, not the mechanics result.

- Stage only ResourceSession_ContractRedHasNonemptyProductionPositiveControl, SessionOrdinaryInput, SessionResultImage, SessionIssues and SessionAllocationCounter plus the owning partial declaration first. Every type in that staged subset already exists. Run its exact filter; require successful old BuildResources with two resource events before the missing BeginResourceExecution reflection assertion. Build/setup failure is not semantic RED.
- Stage the remaining eight Facts and SessionPeriodicInput before the production patch. Missing new types during that staging interval are not another semantic RED.
- Apply the complete companion only after the source owner is released and parent approves the unit. Run the nine-row selection, then the unchanged owners and pending controls. Inspect artifacts before reporting anything green.
- Capture expected and stopped results with identical scripted IDs and the same immutable baseline. Tests compare complete canonical state/history, transitions, events, diagnostics, statistics, complete transcript fingerprint, pending evidence, accepted reaction executions and compatibility trigger projection. They separately check exact allocation identities/counts.
- The periodic tests use the real selected-component resolver. If a fixture fails an existing component/authority rule, diagnose and correct its setup without changing that rule or accepting a weaker checkpoint test.

Serialization audit: EffectReactionExecution and EffectSourceAuthorityEntry are positional records with public generated properties, but EffectReplayIdentity exposes only internal EffectId/Authority getters and would otherwise serialize as an opaque empty object. The helper below explicitly projects all reaction fields, the full downstream source definition/authority data, Parameters and replacement identity. EffectBoundedResourceResolution, ResourcePendingCausalAuthority and ResourcePendingAuthorityBinding are positional records whose generated properties are public; pending values were not opaque, but their full resolution/causal fields and JSON Source/Target are now projected explicitly for an auditable comparison. EffectTargetKey, EffectSourceKey, ResourceCoordinate, ResourceOperationKey, ResourceMutationEventRequirement and ResourceMutationResultConstraint are also positional records; their material properties serialize rather than becoming empty objects. Null JSON payloads remain distinct from empty objects. The first staged reflection helper uses only these existing production types.

Reaction coverage limit: these nine new fixtures use periodic-only resolution and do not produce a nonempty AcceptedReactionExecutions collection. Merely appending an event_reaction component to their selected-periodic resolver would not exercise the full reaction planning path. No such fixture claim is made. Nonempty accepted reactions remain covered by the unchanged owning EffectAcceptedTurnPlannerTests.AcceptedBoundary_EventReactionAfterCurrentEventRunsBeforeTerminalProjection, AcceptedBoundary_AfterComponentReactionRequiresExactAppliedEvidence and ApplyDefinitionReaction_MaterializesOnlyAfterCurrentBoundaryAndIsEligibleNextTurn, plus the retained EffectPendingWaveIntegrationTests controls. These are already included in the proposed owner filters. Extending the session fixture through a full accepted effect plan/reaction-routing setup is separate test work, not silently supplied by this comparator amendment.

```csharp
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlannerTests
{
    [Fact]
    public void ResourceSession_ContractRedHasNonemptyProductionPositiveControl()
    {
        var input = SessionOrdinaryInput();
        var production = AcceptedMechanicsPlanner.BuildResources(input, new SessionAllocationCounter().Factory);
        Assert.True(production.IsValid, SessionIssues(production));
        Assert.Equal(2, production.Events.Count);
        var method = typeof(AcceptedMechanicsPlanner).GetMethod(
            "BeginResourceExecution", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method); // First semantic RED, after real old-production success.
        var counter = new SessionAllocationCounter();
        using var session = Assert.IsAssignableFrom<IDisposable>(
            method!.Invoke(null, new object[] { input, counter.Factory }));
        var drain = session.GetType().GetMethod("Drain", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(drain);
        var result = Assert.IsType<AcceptedMechanicsResourcePlanningResult>(drain!.Invoke(session, null));
        Assert.Equal(SessionResultImage(production), SessionResultImage(result));
    }

    [Fact]
    public void ResourceSession_StopResumeMatchesDrainAndAllocatesOnlyDuringPreparation()
    {
        var input = SessionOrdinaryInput();
        var controlCounter = new SessionAllocationCounter();
        using var controlSession = AcceptedMechanicsPlanner.BeginResourceExecution(input, controlCounter.Factory);
        var control = controlSession.Drain();
        Assert.True(control.IsValid, SessionIssues(control));
        Assert.Equal(0, controlSession.CheckpointCount);
        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        Assert.Equal(0, counter.Calls);
        var first = session.AdvanceToClosedBoundary();
        Assert.Null(first.Result);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(first.Checkpoint);
        Assert.Single(checkpoint.AppliedTransitions);
        Assert.Single(checkpoint.PendingHistoryTransitions);
        Assert.Single(checkpoint.Events);
        Assert.Empty(checkpoint.ReplayTransitions);
        Assert.Equal(1, checkpoint.NextExecutionSequence);
        Assert.Equal(0, checkpoint.Statistics.HistoryFreezeCount);
        Assert.Equal(9m, Assert.Single(checkpoint.State.Entries).Current);
        Assert.Null(session.Result);
        Assert.Equal(controlCounter.Calls, counter.Calls);
        var calls = counter.Calls;
        var frozen = checkpoint.State.ToCanonicalJson();

        var second = session.AdvanceToClosedBoundary();
        Assert.Equal(2, second.Checkpoint!.AppliedTransitions.Count);
        Assert.Equal(8m, Assert.Single(second.Checkpoint.State.Entries).Current);
        var result = session.Drain();
        Assert.Equal(SessionResultImage(control), SessionResultImage(result));
        Assert.Equal(controlCounter.Ids, counter.Ids);
        Assert.Equal(calls, counter.Calls);
        Assert.Equal(frozen, checkpoint.State.ToCanonicalJson());
        Assert.Single(checkpoint.AppliedTransitions);
        Assert.Equal(2, session.CheckpointCount);
        Assert.Same(result, session.Result);
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
        Assert.Throws<InvalidOperationException>(() => session.AdvanceToClosedBoundary());
        Assert.Throws<InvalidOperationException>(() => session.Drain());
        Assert.Equal(calls, counter.Calls);
    }

    [Fact]
    public void ResourceSession_DoesNotPauseInsideAnOpenTwoComponentEffectBoundary()
    {
        var input = SessionPeriodicInput(bounded: false);
        var controlCounter = new SessionAllocationCounter();
        var control = AcceptedMechanicsPlanner.BuildResources(input, controlCounter.Factory);
        Assert.True(control.IsValid, SessionIssues(control));
        Assert.Equal(3, control.AppliedTransitions.Count);
        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        var step = session.AdvanceToClosedBoundary();
        Assert.Null(step.Result);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(step.Checkpoint);
        Assert.Single(checkpoint.ClosedEffectBoundaryOrdinals);
        Assert.Equal(2, checkpoint.AppliedTransitions.Count);
        Assert.All(checkpoint.AppliedTransitions,
            transition => Assert.Equal(ResourceMutationPhase.EffectTrigger, transition.Phase));
        Assert.Equal(0, checkpoint.Statistics.HistoryFreezeCount);
        var result = session.Drain();
        Assert.True(result.EffectBoundaryTranscript!.IsComplete);
        Assert.Equal(SessionResultImage(control), SessionResultImage(result));
        Assert.Equal(controlCounter.Ids, counter.Ids);
        Assert.Equal(controlCounter.Calls, counter.Calls);
    }

    [Fact]
    public void ResourceSession_RetainsPreparedResolverWorkAcrossCausalChildPause()
    {
        var baseline = BaselineHealth(current: 3m);
        var source = new ResourceMutationSourceExport(
            "combat_outcome", "session_resource_root", FingerprintB,
            ResourceMutationSourceState.Active, false, PlayerOwner);
        var root = new ResourceMutationIntent(
            "turn_43:session_resource_root:1", HealthCoordinate, 3m,
            new ResourceMutationSourceRequest(source.SourceKind, source.SourceId, ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(), Array.Empty<ResourceMutationEventRequirement>(), null);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_restore");
        effect["triggers"]![0]!["triggerId"] = "on_resource_depleted";
        effect["triggers"]![0]!["eventType"] = "resource_depleted";
        AcceptedMechanicsResourceInput CreateInput(Action visit) => new(
            43, baseline.Definitions, baseline.State, baseline.History, CreateCatalog(source), new[] { root },
            EventMutationResolver: (resourceEvent, producer) =>
            {
                visit();
                return resourceEvent.EventKind == "resource_depleted"
                    ? InvokeResourceEventResolution(effect, "on_resource_depleted", producer,
                        resourceEvent.EventKind, 43, baseline.Targets, baseline.Owners, baseline.Definitions)
                    : new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                        Array.Empty<ResourceMutationSourceExport>(), Array.Empty<ResourceMutationIntent>(),
                        Array.Empty<ValidationIssue>());
            });
        var expectedVisits = 0;
        var expectedCounter = new SessionAllocationCounter();
        var expected = AcceptedMechanicsPlanner.BuildResources(
            CreateInput(() => expectedVisits++), expectedCounter.Factory);
        Assert.True(expected.IsValid, SessionIssues(expected));
        Assert.Equal(2, expected.AppliedTransitions.Count);
        var visits = 0;
        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(
            CreateInput(() => visits++), counter.Factory);
        var checkpoint = session.AdvanceToClosedBoundary().Checkpoint!;
        Assert.NotNull(checkpoint);
        Assert.Equal(2, checkpoint.AppliedTransitions.Count);
        Assert.Single(checkpoint.ClosedEffectBoundaryOrdinals);
        Assert.Equal(expectedVisits, visits);
        Assert.True(visits > 0);
        var result = session.Drain();
        Assert.Equal(expectedVisits, visits);
        Assert.Equal(expectedCounter.Ids, counter.Ids);
        Assert.Equal(SessionResultImage(expected), SessionResultImage(result));
    }

    [Fact]
    public void ResourceSession_UnresolvedReceiptNeverYieldsAClosedCheckpoint()
    {
        var input = SessionPeriodicInput(bounded: true);
        var control = AcceptedMechanicsPlanner.BuildResources(input, new SessionAllocationCounter().Factory);
        Assert.True(control.IsValid, SessionIssues(control));
        Assert.NotEmpty(control.AcceptedPendingResolutions);
        Assert.False(control.EffectBoundaryTranscript!.IsComplete);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, new SessionAllocationCounter().Factory);
        var step = session.AdvanceToClosedBoundary();
        Assert.Null(step.Checkpoint);
        Assert.NotNull(step.Result);
        Assert.Equal(0, session.CheckpointCount);
        Assert.Equal(SessionResultImage(control), SessionResultImage(step.Result!));
        Assert.Throws<InvalidOperationException>(() => session.AdvanceToClosedBoundary());
    }

    [Fact]
    public void ResourceSession_FailureBeforeExecutionAndAfterCheckpointRemainFailures()
    {
        var ordinary = SessionOrdinaryInput();
        var unknown = new AcceptedMechanicsResourceInput(
            2, ordinary.Definitions, ordinary.State, ordinary.History,
            CreateCatalog(), ordinary.Mutations);
        using var rejected = AcceptedMechanicsPlanner.BeginResourceExecution(
            unknown, new SessionAllocationCounter().Factory);
        var failure = rejected.AdvanceToClosedBoundary();
        Assert.Null(failure.Checkpoint);
        Assert.False(failure.Result!.IsValid);
        Assert.Contains(failure.Result.Issues, issue => issue.Code == "resource_source_unknown");
        Assert.Null(failure.Result.StateAfterImage);
        Assert.Equal(0, rejected.CheckpointCount);
        Assert.Throws<InvalidOperationException>(() => rejected.Drain());

        var invalidSuffix = SessionOrdinaryInput(secondAmount: 100m);
        var expected = AcceptedMechanicsPlanner.BuildResources(
            invalidSuffix, new SessionAllocationCounter().Factory);
        Assert.False(expected.IsValid);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(
            invalidSuffix, new SessionAllocationCounter().Factory);
        var prefix = session.AdvanceToClosedBoundary().Checkpoint;
        Assert.NotNull(prefix);
        Assert.Single(prefix!.AppliedTransitions);
        var suffix = session.AdvanceToClosedBoundary();
        Assert.Null(suffix.Checkpoint);
        Assert.False(suffix.Result!.IsValid);
        Assert.Null(suffix.Result.StateAfterImage);
        Assert.Null(suffix.Result.HistoryAfterImage);
        Assert.Equal(SessionResultImage(expected), SessionResultImage(suffix.Result));
        Assert.Single(prefix.AppliedTransitions); // Observation never grants final acceptance.
    }

    [Fact]
    public void ResourceSession_ReplayEvidenceAndNewSuffixRemainDistinctAcrossPause()
    {
        var full = SessionOrdinaryInput();
        var firstInput = new AcceptedMechanicsResourceInput(
            2, full.Definitions, full.State, full.History, full.Sources, new[] { full.Mutations[0] });
        var first = AcceptedMechanicsPlanner.BuildResources(firstInput, new SessionAllocationCounter().Factory);
        Assert.True(first.IsValid, SessionIssues(first));
        var input = new AcceptedMechanicsResourceInput(
            2, full.Definitions, first.StateAfterImage!, first.HistoryAfterImage!, full.Sources, full.Mutations);
        var controlCounter = new SessionAllocationCounter(seed: 50);
        var expected = AcceptedMechanicsPlanner.BuildResources(input, controlCounter.Factory);
        Assert.True(expected.IsValid, SessionIssues(expected));
        var counter = new SessionAllocationCounter(seed: 50);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        var prefix = session.AdvanceToClosedBoundary().Checkpoint!;
        Assert.Empty(prefix.AppliedTransitions);
        Assert.Empty(prefix.PendingHistoryTransitions);
        Assert.Empty(prefix.Events);
        Assert.Equal(Assert.Single(first.AppliedTransitions), Assert.Single(prefix.ReplayTransitions));
        var result = session.Drain();
        Assert.Single(result.AppliedTransitions);
        Assert.Single(result.ReplayTransitions);
        Assert.Single(result.Events);
        Assert.Equal(SessionResultImage(expected), SessionResultImage(result));
        Assert.Equal(controlCounter.Ids, counter.Ids);
    }

    [Fact]
    public void ResourceSession_DetachesInputArraysAndRetainsCheckpointImagesAfterResume()
    {
        var input = SessionOrdinaryInput();
        var mutations = input.Mutations.ToList();
        var captured = new AcceptedMechanicsResourceInput(
            2, input.Definitions, input.State, input.History, input.Sources, mutations);
        var expected = AcceptedMechanicsPlanner.BuildResources(
            captured, new SessionAllocationCounter().Factory);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(
            captured, new SessionAllocationCounter().Factory);
        mutations.Clear();
        var returnedDependencies = Assert.IsType<ResourceOperationKey[]>(captured.Mutations[1].Dependencies);
        returnedDependencies[0] = returnedDependencies[0] with { EventRef = "changed_dependency" };
        var checkpoint = session.AdvanceToClosedBoundary().Checkpoint!;
        var state = checkpoint.State.ToCanonicalJson();
        var history = checkpoint.HistoryBaseline.ToCanonicalJson();
        checkpoint.Definitions.ToCanonicalRoot()["definitions"] = new System.Text.Json.Nodes.JsonArray();
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResourceTransition>)checkpoint.AppliedTransitions).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResourceTransition>)checkpoint.PendingHistoryTransitions).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResourceAppliedEvent>)checkpoint.Events).Clear());
        Assert.Equal(SessionResultImage(expected), SessionResultImage(session.Drain()));
        Assert.Equal(state, checkpoint.State.ToCanonicalJson());
        Assert.Equal(history, checkpoint.HistoryBaseline.ToCanonicalJson());
        Assert.Single(checkpoint.AppliedTransitions);
    }

    [Fact]
    public void ResourceSession_RejectsNullDisposedFaultedAndReentrantUse()
    {
        var input = SessionOrdinaryInput();
        Assert.Throws<ArgumentNullException>(() =>
            AcceptedMechanicsPlanner.BeginResourceExecution(null!, new SessionAllocationCounter().Factory));
        Assert.Throws<ArgumentNullException>(() =>
            AcceptedMechanicsPlanner.BeginResourceExecution(input, null!));
        var disposedCounter = new SessionAllocationCounter();
        var disposed = AcceptedMechanicsPlanner.BeginResourceExecution(input, disposedCounter.Factory);
        disposed.Dispose();
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposed.AdvanceToClosedBoundary());
        Assert.Throws<ObjectDisposedException>(() => disposed.Drain());
        Assert.Equal(0, disposedCounter.Calls);

        var faultCounter = new SessionAllocationCounter
        {
            OnAllocation = () => throw new InvalidOperationException("allocation probe failure")
        };
        using var faulted = AcceptedMechanicsPlanner.BeginResourceExecution(input, faultCounter.Factory);
        var failure = Assert.Throws<InvalidOperationException>(() => faulted.Drain());
        Assert.Equal("allocation probe failure", failure.Message);
        var count = faultCounter.Calls;
        Assert.Throws<InvalidOperationException>(() => faulted.AdvanceToClosedBoundary());
        Assert.Null(faulted.Result);
        Assert.Equal(count, faultCounter.Calls);

        var counter = new SessionAllocationCounter();
        using var active = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        counter.OnAllocation = () =>
        {
            Assert.Throws<InvalidOperationException>(() => active.AdvanceToClosedBoundary());
            Assert.Throws<InvalidOperationException>(() => active.Dispose());
        };
        Assert.True(active.Drain().IsValid);
        Assert.True(counter.Calls > 0);
    }

    private static AcceptedMechanicsResourceInput SessionOrdinaryInput(decimal secondAmount = 1m)
    {
        var baseline = BaselineCharges();
        var first = Intent("turn_2:session:1", "action_cost", "action_alpha", ResourceOperation.Spend, 1m);
        var second = Intent("turn_2:session:2", "action_cost", "action_alpha", ResourceOperation.Spend,
            secondAmount, dependencies: new[] { first.Key });
        return new AcceptedMechanicsResourceInput(
            2, baseline.Definitions, baseline.State, baseline.History, baseline.Sources, new[] { first, second });
    }

    private static AcceptedMechanicsResourceInput SessionPeriodicInput(bool bounded)
    {
        var baseline = BaselineHealth(current: 10m);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["triggers"]![0]!["resolutionMode"] = bounded ? "bounded_receipt" : "deterministic";
        if (!bounded)
        {
            var second = effect["components"]![0]!.DeepClone().AsObject();
            second["componentId"] = "component_session_second";
            effect["components"]!.AsArray().Add(second);
            effect["triggers"]![0]!["componentIds"]!.AsArray().Add("component_session_second");
        }
        var resolution = InvokePeriodicResourceResolution(
            effect, "on_owner_turn_end", PeriodicEvent(),
            baseline.Targets, baseline.Owners, baseline.Definitions);
        Assert.Empty(resolution.Issues);
        if (!bounded)
            Assert.Equal(2, resolution.Mutations.Count);
        var sources = resolution.SourceExports.ToList();
        var mutations = resolution.Mutations.ToList();
        if (!bounded)
        {
            var source = new ResourceMutationSourceExport(
                "combat_outcome", "session_ordinary_suffix", FingerprintB,
                ResourceMutationSourceState.Active, false, PlayerOwner);
            sources.Add(source);
            mutations.Add(new ResourceMutationIntent(
                "turn_43:session_suffix:1", HealthCoordinate, 1m,
                new ResourceMutationSourceRequest(source.SourceKind, source.SourceId, ResourceOperation.Damage),
                Array.Empty<ResourceOperationKey>(), Array.Empty<ResourceMutationEventRequirement>(), null));
        }
        return new AcceptedMechanicsResourceInput(
            PeriodicEvent().Turn, baseline.Definitions, baseline.State, baseline.History,
            CreateCatalog(sources.ToArray()), mutations,
            InitialTriggerCandidates: resolution.TriggerCandidates,
            InitialEffectResolutionWork: resolution.Work);
    }

    private static string SessionIssues(AcceptedMechanicsResourcePlanningResult result) =>
        string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Expected}; {issue.Actual}"));

    private static string SessionResultImage(AcceptedMechanicsResourcePlanningResult result) =>
        JsonSerializer.Serialize(new
        {
            result.IsValid,
            State = result.StateAfterImage?.ToCanonicalJson(),
            History = result.HistoryAfterImage?.ToCanonicalJson(),
            result.AppliedTransitions,
            result.ReplayTransitions,
            result.Events,
            Issues = result.Issues.Select(issue => new
            {
                issue.FilePath, issue.Code, issue.Severity, issue.Message, issue.Expected, issue.Actual,
                issue.Category, issue.Actor, issue.Section, issue.RepairHint, issue.RepairTargetFiles
            }),
            result.Statistics,
            Transcript = result.EffectBoundaryTranscript?.Fingerprint,
            Complete = result.EffectBoundaryTranscript?.IsComplete,
            Pending = result.AcceptedPendingResolutions.Select(pending => new
            {
                Resolution = new
                {
                    pending.Resolution.EventRef, pending.Resolution.EffectId,
                    pending.Resolution.EffectAuthority,
                    Source = pending.Resolution.Source.ToJsonString(),
                    pending.Resolution.SourceAuthority,
                    Target = pending.Resolution.Target.ToJsonString(),
                    pending.Resolution.TargetAuthority,
                    pending.Resolution.TriggerId, pending.Resolution.ComponentId,
                    pending.Resolution.TriggerEventRef, pending.Resolution.EventKind,
                    pending.Resolution.Coordinate, pending.Resolution.ResourceAuthority,
                    pending.Resolution.Operation, pending.Resolution.MinimumAmount,
                    pending.Resolution.MaximumAmount, pending.Resolution.SourceAuthorityFingerprint,
                    pending.Resolution.PolicyFingerprint, pending.Resolution.Dependencies,
                    pending.Resolution.EventRequirements, pending.Resolution.ResultConstraint,
                    pending.Resolution.RemainingUseBudget, pending.Resolution.SafeSourceLabel,
                    pending.Resolution.SafeTargetLabel, pending.Resolution.SafeResourceLabel,
                    pending.Resolution.SafeOperationLabel, pending.Resolution.AfterComponentId
                },
                CausalAuthority = new
                {
                    pending.CausalAuthority.EffectId, pending.CausalAuthority.TriggerId,
                    pending.CausalAuthority.ActivationEventRef, pending.CausalAuthority.TriggerEventRef,
                    pending.CausalAuthority.ResourceProducerOperationKey, pending.CausalAuthority.Priority,
                    pending.CausalAuthority.ActivationOrdinal, pending.CausalAuthority.ConsumesUse,
                    pending.CausalAuthority.UsesBefore, pending.CausalAuthority.ComponentId,
                    pending.CausalAuthority.AfterComponentId, pending.CausalAuthority.CandidateFingerprint,
                    pending.CausalAuthority.TranscriptPrefixFingerprint, pending.CausalAuthority.WaveOrdinal
                }
            }),
            Reactions = result.AcceptedReactionExecutions.Select(reaction => new
            {
                reaction.EventRef, reaction.TriggerEventRef, reaction.CausalEventRef,
                reaction.Turn, reaction.EventKind, reaction.Target, reaction.EffectId,
                reaction.TriggerId, reaction.ComponentId, reaction.ResultKind,
                reaction.Dependency, reaction.AfterComponentId, reaction.MaxExpansion,
                DownstreamSource = reaction.DownstreamSource == null ? null : new
                {
                    reaction.DownstreamSource.Key,
                    Definition = reaction.DownstreamSource.Definition.ToJsonString(),
                    reaction.DownstreamSource.Materializable, reaction.DownstreamSource.Active,
                    reaction.DownstreamSource.SameTurn, reaction.DownstreamSource.SourceRef,
                    SatisfiedPredicates = reaction.DownstreamSource.SatisfiedPredicates
                        .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    reaction.DownstreamSource.RequiredApplicationAuthority
                },
                Parameters = reaction.Parameters?.ToJsonString(),
                reaction.DownstreamSourceKey, reaction.ComponentPriority,
                ReplacementTarget = reaction.ReplacementTarget == null ? null : new
                {
                    reaction.ReplacementTarget.EffectId,
                    Authority = new
                    {
                        reaction.ReplacementTarget.Authority.BindingKind,
                        reaction.ReplacementTarget.Authority.AuthorityId
                    }
                }
            }),
            result.AcceptedResolvedPendingRequestIds,
            Triggers = result.ResourceTriggerExecutions.Select(execution => new
            {
                execution.EffectId, execution.TriggerId, execution.EventKind, execution.EventRef,
                execution.MutationKeys, execution.RemainingUseBudget, execution.ComponentIds, execution.TriggerEventRef,
                Components = execution.ComponentIdsByMutation?.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                    .Select(pair => new { Key = pair.Key, pair.Value })
            })
        });

    private sealed class SessionAllocationCounter
    {
        private int _next;
        internal SessionAllocationCounter(int seed = 0)
        {
            _next = seed;
            Factory = new AcceptedMechanicsIdentityFactory(() =>
            {
                Calls++;
                OnAllocation?.Invoke();
                var id = new Guid(++_next, 0, 0, new byte[8]);
                Ids.Add(id);
                return id;
            });
        }
        internal Action? OnAllocation { get; set; }
        internal int Calls { get; private set; }
        internal List<Guid> Ids { get; } = new();
        internal AcceptedMechanicsIdentityFactory Factory { get; }
    }
}
```

## Bounded controller commands

PowerShell 7, worktree root; these commands have NOT been executed by this author. Default Focused bound is five minutes. Raise a bound only on measured evidence for that exact coherent selection, and retain failed artifacts.

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests.ResourceSession_ContractRedHasNonemptyProductionPositiveControl"
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests.ResourceSession_"
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests|FullyQualifiedName~EffectAcceptedTurnPlannerTests|FullyQualifiedName~AcceptedMechanicsPlanCacheTests|FullyQualifiedName~WoundMaterializationSourceGuardTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedMechanicsPlannerScaleTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourcePendingResolutionIntegrationTests|FullyQualifiedName~EffectPendingWaveIntegrationTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_SignedPressureExchangePublishesWithNoOwningPhaseErrors|FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_FinalValidationRejectsDieOutsideSignedPool"
```

Parent owns one Fast at a meaningful checkpoint; the implementer must not duplicate it. Parent later chooses review/PreMerge timing. No FullValidation/GM example lane is needed solely for this internal behavior-preserving session; if implementation changes gameplay/pending/authority behavior, stop and report the synchronization boundary instead.

## Review checklist and limits

- Only BuildResources is replaced; no conflict validator, signed snapshot loader, engine, GM schema, registered outcome, scheduler comparer, resource arithmetic, receipt binding or final transcript validation changes.
- Verify all 30 original failure expressions and the final result construction are retained; all four scheduler completion sites update the pause marker only after completion.
- No history/transcript final freeze at checkpoint; no eager checkpoint copy during full production drain; source/definition/resource/effect identities are never regenerated on resume.
- The complete final state/transcript and existing work counters match an uninterrupted run for direct, causal child, multi-component, replay, pending and failing paths.
- Terminal, disposed, faulted and re-entrant sessions cannot be resumed. Caller abandonment cannot publish anything.
- Tests are a proposed executable design, not passing evidence. The source method anchor was checked read-only; no C# verification was performed here.
- This bounded resource session is a real pause/resume prerequisite, but full T081-B2 remains OPEN for effect advancement, dependent suffix recomposition, exchange-specific source proof and receipt/cold reconstruction.


## Commit and report contract

```powershell
git diff --check
git diff --stat
git add -- BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs
git commit -m "refactor: retain resource execution across closed boundaries (#1536)"
```

Report exact changed files/commit, RED and each GREEN command/artifact, discovery/execution/pass/fail/skip/duplicate counts, wall time, build warnings/errors and owned-tree cleanup. Read summary/log/TRX rather than trusting console status. Record any fixture corrections separately and their source-grounded cause; do not change unrelated production contracts. Keep caller-abandonment, failed suffix and pending result non-publication explicit. Parent independently reviews the full recorded pre-dispatch BASE..HEAD and runs Fast; no completion of full B2 or #1536 is authorized here.

## Accepted execution — 2026-09-08

T081-B2A is complete at `f5e4de2f`, recorded review base
`fc6887e64863a3fdc1cc26678cb616b7d92a8b7e`. Independent review is Spec compliant /
Quality Approved with zero Critical, Important or Minor findings. Parent inspected
the complete changed scope, exact1499-line production postimage, original-vs-actual
test diff, and actual summaries/logs/TRXs. All assertions remain unchanged.

The proposed periodic fixtures needed explicit typed initial/causal candidates:
the low-level resolver returns mutation/pending data but does not create those
candidates. The corrected helper carries its real component maps, pending outputs,
source exports and origin. This is test fixture repair, not weaker validation.

All artifact paths below are relative to `TestResults/test-lanes/`:

| Control | Result | Wall time | Artifact |
|---|---|---|---|
| Old positive then missing-API semantic RED |0/1|1:12.6288105|`20260908-052947-920-35004-ba9a5f0153634b7fb3939596d0575f2e-focused`|
| Historical fixture failure |6/9|1:09.6296742|`20260908-053201-712-37552-6a4803daa9e743eba73f3234c6de0669-focused`|
| ResourceSession tests |9/9|0:32.6324641|`20260908-053649-352-8868-31fc14199d454069b90e36d4841f3dbf-focused`|
| Owning planners/cache/source guards |432/432|0:40.5632905|`20260908-053729-443-20212-44c9db4ed2b44033987dc1674ccfdf04-focused`|
| Integration scale |6/6|0:55.5283539|`20260908-053814-523-47356-98300183fe814f02b2c3f8056c2142b0-focused`|
| Integration pending/receipt waves |11/11|1:03.5593275|`20260908-053914-661-46836-a665ed97498745cd877935ac709f3936-focused`|
| Integration signed conflict |2/2|0:23.5681495|`20260908-054025-421-12484-b51b8d26b61743119d1a1a92feffb5d1-focused`|
| Parent Fast |7767/7767|5:00.6051031|`20260908-055317-817-28696-712efc4947374b77bed621709e33ea9c-fast`|

Every GREEN control has build warnings/errors0/0, no skipped or duplicated
executions, default5m bound and complete owned-tree cleanup. Fast finished exit0
with TimedOut=false at the exact elapsed time shown, not a claim of under5m.
All26 Fast TRXs contain7767 distinct executions and7713 unique method IDs,
zero cross-descriptor duplicate IDs, and all nine new session Facts. Existing
dynamic theories account for the54 additional executed rows.

Only nested B2A is accepted; top-level79/177 and full T081/B2/C/D/E/T084/T085/T177
remain open. This internal change introduces no GM-authored/runtime contract,
so Mortal/afterlife prompts, examples, matrix and manifests need no update here.
No remote publication, issue closure, new worktree or session cleanup occurred.
