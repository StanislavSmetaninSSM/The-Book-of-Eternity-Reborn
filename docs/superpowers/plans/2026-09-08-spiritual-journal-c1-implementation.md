# Spiritual journal C1 retained exchange intervals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Parent uses superpowers:subagent-driven-development for this single coherent task. The user has explicitly authorized delegation and model choice; parent selects each model explicitly, dispatches one source-owning implementer and a separate task reviewer, and owns evidence/acceptance gates. This metadata-review child does not delegate. Steps use checkbox syntax for tracking.

**Parent tracking status:** The parent has read the entire original companion and every correction, including the accepted zero-exchange identity guard, actual-phase evidence pins, and checkpoint sequencing. This plan is tracked under #1536/T081-B2C-J2-C1 before source edits. B1 source is at66d11b02; its review and the combined B0/B1/GM Fast plus conditional FullValidation must finish before C1 source dispatch. No C1 runtime/test success is claimed.

**Goal:** Refactor the production resource planner into one retained execution owner that can accept the next resource-local whole-exchange batch before allocation and expose detached, exact closed intervals without completing the effect plan.

**Architecture:** The existing fixed-input adapter and the new live session share one real ResourceExecutionState, ledger, working history, identity factory/registry, arbiter, transcript builder and accepted sets. Fixed input retains eager preparation; live input prepares baseline lifecycle work once and only the admitted next exchange plus its causal expansion thereafter. An immutable-base routing object owns the original effect-plan reference, original owner authority and definitions and supplies its retained index's full canonical seed view before the one arbiter initialization.

**Tech Stack:** Existing .NET 8/C#, xUnit, System.Text.Json, PowerShell 7, repository bounded C# lane runner; no new package or public contract.

## Global constraints and status

- Source issue: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536). Active durable artifacts: specs/1536-complete-wound-materialization/spec.md, plan.md, tasks.md, contracts/spiritual-wound-live-turn-boundary.md. Governance: .specify/memory/constitution.md and applicable AGENTS.md.
- This is a metadata-only planning deliverable, not an implementation or accepted C1 result. No C# command was run; no repository source/test/spec/index/HEAD was written by this planner. Parent must read every companion body before tracking or dispatch.
- Approved architecture: docs/superpowers/plans/2026-09-08-spiritual-effect-draft-journal-design.md. Read together with metadata effect-draft-live-j2-execution-plan.md, spiritual-next-exchange-consumer-seams.md and spiritual-journal-c-next-seam-audit.md. The audit recommends boundaries; actual source determines APIs.
- Base: ecffabdc002cacfc3362d071c26c747d443313b2. During preparation parent advanced HEAD to 4044e8cf6a0f068127019195c6800ec3afaa2a7e and began B1; all five existing source targets below still equal their pinned base blobs. Recheck exact target hashes after B1, not HEAD alone.
- Preserve existing B2A/B2B results, side-chain semantics, statistics and allocation chronology. In particular do not weaken ResourceSession_StopResumeMatchesDrainAndAllocatesOnlyDuringPreparation.
- No temporary Complete, second completed effect plan, cloned production execution, accepted-transition replay, terminal projection on capture/decline, or reinitialized per-batch arbiter.
- This task is not source admission or full Unit C. B1's PreparedSpiritualSource has reference ownership; its CheckedExchanges strings and BuildInputBinding JSON are not typed exchange capabilities. The B1 plan/patch is reference-only until actual source lands. Do not invent PreparedSpiritualExchange.
- Pending is retained and unsealed, not unsupported, failure, completed-none or evaluated-zero. No interval/final result/history freeze is allowed while a source proof or resource receipt prerequisite is missing.
- One coherent source-state task; do not divide the planner/session/graph changes among concurrent writers. Parent-owned subagent-driven-development uses one source implementer and a subsequent independent task review (spec compliance and quality), with file-backed brief/report/diff handoffs. B0/B1 files are excluded.
- Checkpoint order is mandatory: parent completes and reviews the combined B0/B1/GM Fast checkpoint and the GM-sensitive conditional FullValidation before dispatching C1. After C1 focused verification/review, parent runs one separate meaningful C1 Fast checkpoint. Earlier B0/B1 evidence is not retroactively called a combined C1 checkpoint.
- No remote operation, cleanup or tracking mutation is authorized by this metadata deliverable.

## Exact files and responsibilities

All source paths below are relative to E:/Games/worktrees/boe-1536-wound-materialization. The companion has absolute target headers.

| Action | Path | Responsibility |
|---|---|---|
| Modify | BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs | Keep BuildResources and fixed session adapter on shared retained core; live staging lifecycle; graph preparation of new roots only; scheduler initialization accounting for completed operations |
| Create | BookOfEternityClient/Services/AcceptedMechanicsPlanner.LiveTurn.cs | Real retained execution state, append-only admitted graph extension, whole-exchange observations and pending observations |
| Modify | BookOfEternityClient/Services/AfterlifeSpiritualConflictResourceOutcome.cs | Resource-local per-exchange batches, explicit zero/missing distinction, shared exact Draft.Project witness, unchanged static side chains and fingerprints |
| Modify | BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs | Read-only full canonical seed view from the already retained immutable index |
| Create | BookOfEternityClient/Services/EffectAcceptedTurnPlanner.BaseResourceRouting.cs | Original-plan/owner/definition reference owner for baseline resolution and seed consistency |
| Modify | BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs | Preserve ten OLD tests; add semantic RED, three fixed goldens and eight further live controls |
| Modify | BookOfEternityClient.IntegrationTests/EffectResourceTriggerRoutingScaleTests.cs | Partial keyword only, to reuse the existing real accepted-plan fixture |
| Create | BookOfEternityClient.IntegrationTests/EffectResourceTriggerRoutingScaleTests.SpiritualExchangeIntervals.cs | Real immutable-plan seeding, causal closure, no-use-reset, pending and definition-ownership tests |
| Create | BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.SpiritualExchangeIntervals.cs | Real canonical-file outcome batches, both side chains, zero/missing and start/terminal prerequisites |

No AcceptedEffectUseArbiter.cs or EffectAcceptedTurnPlan.cs change is required: the existing plan's ResourceTriggerIndex already retains the canonical occurrence snapshots. No B0/B1 action/source/validator file or boundary manifest is edited. Existing integration class category applies to the new partial files; no test is moved into Fast.

## Verified source problem and solution

The old ResourceExecutionSession wraps one fixed iterator. ExecuteResourceSession prepares and allocates all roots and causal expansions before constructing/using its live ledger. Its arbiter, accepted maps and scheduler belong to that iterator. Merely collecting a checkpoint, observing another immutable DTO or invoking BuildResources again cannot replace the next unaccepted batch.

The shared core is moved, with complete bodies, into a private ResourceExecutionState. Its thirty-two retained state fields include metrics, identity registry, prepared descriptors, graph, candidate authority, ledger/history, applied/replay/event lists, execution sequence, direct-state images, operation maps, transcript builder, produced-event sets, applied-component sets, accepted identities/activations/boundaries, causal operation sets, closed/pending sets, original seed map, arbiter, candidate lookup and scheduler. Local causal routines and the pending frontier remain retained in the suspended iterator. The session owns this state and its one enumerator.

BuildResources -> BeginResourceExecution -> ResourceExecutionSession -> ResourceExecutionState.Run -> original final freeze/result is the actual production-consumed path. Fixed mode has no live routing and no exchange append; all preparation and one final result remain in the original order. This is not an unused observer implementation.

The live resource path is BeginLiveResourceExecution -> StageNextExchange -> AdvanceThroughExchange -> AppendBatch -> shared causal loop -> exact Draft witness -> CaptureClosedPrefix -> owned SpiritualExchangeInterval. Actual production outcome composition supplies ExchangeBatch values. The file-backed integration test uses that path directly. C1 does not switch AcceptedMechanicsPlanner.Build or GameEngine to live gameplay execution; those callers still require C2/D/E. Neither a test call nor shared production-core consumption is claimed to deliver live wound publication.

### Fixed-input versus live allocation

Fixed input remains eager: preparation allocates all fixed roots before causal execution, exactly as B2A requires. Live staging stores detached raw resource data and allocates nothing. Replacing an unaccepted staged batch changes no allocator, ledger, history, transcript or accepted set. Admission prepares only that selected batch and its hypothetical causal descriptors; these belong to the admitted exchange even if an event later remains unrealized.

A live exchange closes before another batch is prepared. Thus fixed allocation may be root1, root2, child1, whereas live allocation is root1, child1, root2. Equal GUID-to-operation assignment across those two modes would contradict deferred allocation. There is no compatibility migration or gameplay choice: static golden IDs remain exact; live accepted-prefix IDs and ordinals remain exact; live-versus-live final-suffix results and allocator lists must be byte-identical. The cross-mode causal test compares every transition semantic field, event/coordinate/value relationship and causal ordering after independently constructing unique bijections for operation/transition identities. It explicitly verifies identity uniqueness rather than dropping identity checks.

Appending builds a new immutable graph descriptor over the same old PreparedMutation references plus new descriptors. Only new operation IDs enter the expansion frontier; old producer events are not resolved or executed again. A fresh scheduler descriptor subtracts already-completed parents before making new nodes ready. Ledger/history/arbiter/transcript/accepted sets are not replaced. Live descriptor-work statistics may include graph revalidation; static golden counters must not change.

### Baseline routing and seed ownership

BaseResourceRouting.Capture accepts the actual original uncompleted EffectAcceptedTurnPlan and retains that exact reference, its ResourceOwnerAuthority and exact ResourceDefinitionCatalog. The session rejects foreign definition references and arbitrary resolver/initial-candidate injection in live mode. Initial due lifecycle resolution occurs once, before exchange admission. Resource-event expansion calls the existing ResolveResourceEventMutations against that same plan/index.

CanonicalUseSeeds enumerates existing indexed active occurrences, not allocated future candidates, and returns detached canonical records. The live arbiter receives that full view exactly once. Every newly discovered candidate's seed must equal its original record; no candidate can overwrite remaining uses. The fixed adapter still seeds only its existing candidate list, so discovering an unused live baseline seed cannot alter fixed execution/report/allocation/statistics. The later-first-candidate test proves a seed exists before any event materializes its candidate.

This is internal immutable-base resource data, not a draft version, current source generation, effect materialization, wound profile, or publishable authority. Full C2 must add newly materialized-instance registration, retirement and version binding to this same owner/arbiter; C1 intentionally does not simulate those with fingerprints or a fresh initialization.

### Whole exchange, detachment and exact witness

An admitted batch carries both side evaluations and every mutation from that exchange. The scheduler exhausts both branches and all causal descendants before an interval can be produced. There is no generic resource-node checkpoint in live mode. The old generic checkpoint API remains for fixed mode only.

ExchangeBatch separates MissingAudit, EvaluatedZero and EvaluatedMutation. Explicit zero is established only after existing audit validation of operation, numeric before/after/cost, ledger sequence and range. A missing audit does not create zero evidence. Missing data stays PendingSpiritualExchange, without interval or result. The low-level constructor is resource data, not a source seal; C2 must require the B1-owned source capability before accepting it as a journal admission. The local owner also retains exact accepted exchange IDs: nextOrdinal alone does not permit an accepted ID to be reused. Stage checks that set before any mutation; CloseExchange records an ID only after closure. Replacement of the same still-unaccepted ID/ordinal remains legal. This is local exact-identity protection, not B1 source or confusable-coordinate authority.

Each interval records exact half-open applied, replay, event, boundary, close, causal closure, activation, rejection, component, resource-mutation, reaction-release and terminal-reservation ranges. It owns detached suffix arrays and before/after closed-prefix snapshots. CaptureClosedPrefix validates without sealing. The session's Owns method recognizes only exact interval references its retained state emitted; an equal-looking manually constructed interval fails ownership. A caller must not turn the interval constructor or exchange strings into source authority.

The original Draft.Project and interval validation call the same exact witness function: event reference, source kind/id, coordinate, operation, before, after, requested amount and applied amount must match exactly once. Replay transitions count as replay evidence; duplicate applied/replay matches fail. Later trigger mutations of the ledger cannot substitute for, invalidate by final-value comparison, or launder this witness. Static zero/missing Draft == null and old source fingerprints/side-chain dependencies remain unchanged.

## Mandatory contours and scope gaps

| Contour | C1 resource behavior | Mandatory next authority |
|---|---|---|
| Ordinary/special/guaranteed active exchange with complete cost audit | Resource-local batches/closed intervals when both branches and descendants close | C2 binding to actual B1 source reference; D wound application |
| Explicit validated zero or recovery at maximum | Exact empty resource ranges may close | C2 source proof that both sides were evaluated; not merely an empty collection |
| Passive, champion no-invention or dice-free source with absent cost audit | Missing side remains unsealed pending, not unsupported or zero | C2 owner-bound preparation supplying actual no-mutation evaluation from the lawful source path |
| Start without pre-turn active conflict | StartResourceInitialization requirement, no invented exchange or interval | C2 actual start/source and resource-initialization binding |
| Terminal accepted root without active conflict | TerminalResourceClosure requirement, no inferred no-work fence | C2 exact pre-terminal source and post-exchange resource closure |
| Neither root active | ActiveConflictSourceBinding requirement, no invented contour | Owning lifecycle/source path decides whether there is an exchange |
| Real unresolved bounded receipt | PendingSpiritualResourceExchange with detached prerequisite data, no Result/history freeze/interval | C2/E exact owner-bound receipt resume and cold continuation |
| Newly inserted wound or retired/replaced instance | No current-version or route claim | C2 registration/version binding, then D eight consumers |

These are outstanding lawful prerequisites, not deliberate gameplay exclusions. C1 can preserve this state safely because it does not replace the engine's existing lawful execution path. If the proposed tracked task or parent acceptance requires the new live API itself to support receipt completion or start/passive/terminal admission now, this companion is insufficient for that larger acceptance; expand into the C2/E owner seam before enabling it. Do not enable a branch that falls back to a second completed plan.

### Exact pending retention and required next seam

On real pending, the state retains workingLedger, workingHistory and their pending transitions; the allocator and identityRegistry; executionSequence; original prepared descriptors and source authority; candidate authority; accepted activation/use state; transcript builder with the open frontier; scheduler and causal ancestor/remaining-operation maps; activeBatch and interval start indexes. It yields only PendingSpiritualResourceExchange with frontier ordinal, exchange coordinate, detached unresolved outputs whose predecessor actually applied, and current statistics. It does not seal the pending frontier, seal use projection, freeze the transcript, freeze history, terminal-fold effects, construct an after-image result or mark the session completed/faulted.

The iterator is suspended at that yield; its existing next instruction is a terminal iterator exit. C1 prevents all subsequent advance/drain/stage attempts with a retained-pending guard. That guard is not a resume API and must never be reported as delivered receipt-resume support.

The next C2/E change must add an owner-bound resume operation on this same ResourceExecutionSession (proposed name ResumePendingExchange, not an existing API), accepting the exact pending occurrence emitted by this owner plus the existing validated ResourcePendingResolvedBinding objects from ResolvedPendingReplaySession. It must verify original command/source authority, wave and frontier, install bindings into the original accepted candidates, prepare only newly released continuation work with the retained allocator, and continue the causal loop rather than executing the pending yield's exit or replaying accepted operations. Before runtime enablement, AcceptedMechanicsPlanner.Build's accepted pending-binding path and GameEngine's original pending-turn continuation must call that operation on the retained owner; E must provide cold reconstruction of the same authenticated prefix/identities and exactly one final publication. These signatures/callers require their own complete reviewed code; this plan supplies no imaginary binding type or implementation.

C2 must also add an owner-bound missing-side/source-proof continuation rather than replacing the already accepted portion of activeBatch. B1's existing PreparedSpiritualSource can supply reference ownership, but currently has no typed exchange binding; that precise seam remains mandatory.

### Task 1: one retained resource interval owner

Proposed tracked child: T081-B2C-J2-C1 [US3], under source issue #1536. Parent must encode this exact bounded scope in the active Spec Kit task before editing repository code. Completing this child must not close J2, B2C, T081, Unit C, D, E or #1536.

**Consumes:** existing AcceptedMechanicsResourceInput, AcceptedMechanicsIdentityFactory, EffectAcceptedTurnPlan.ResourceTriggerIndex, AcceptedEffectUseArbiter, AcceptedEffectBoundaryTranscript.Builder.CaptureClosedPrefix, actual resource outcome composition and existing accepted-plan/file-backed test fixtures.

**Produces:** the complete signatures and bodies in spiritual-journal-c1-implementation.patch: BeginLiveResourceExecution(AcceptedMechanicsResourceInput, AcceptedMechanicsIdentityFactory, BaseResourceRouting?), StageNextExchange(ExchangeBatch), AdvanceThroughExchange(), Owns(SpiritualExchangeInterval), BaseResourceRouting.Capture, BuildResult.Exchanges/PendingRequirements, and detached interval/pending data. No source/admission/publication capability is produced.

- [ ] Read governance, active artifacts, this plan and every companion body. Reconcile current B1 source; preserve unrelated dirty files. Verify the five base blobs and four absent new targets in the table below.
- [ ] Parent verifies its B0/B1/GM combined Fast and GM-sensitive conditional FullValidation are already complete/reviewed, then records the single C1 tracked task and bounded scope before code edits. Parent dispatches one coherent source-owning implementer with the complete code companion, exact task brief and report path; the user-authorized model is selected explicitly.
- [ ] Apply only the following complete OLD-compilable test patch with apply_patch. It uses no newly introduced type in compiled expressions. Its production positive succeeds on OLD; OLD then actually allocates the suffix before its first checkpoint and fails 2 versus 4 allocator calls.

```text
*** Begin Patch
*** Update File: E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs
@@
+    private readonly Xunit.Abstractions.ITestOutputHelper _liveGoldenOutput;
+
+    public AcceptedMechanicsPlannerTests(Xunit.Abstractions.ITestOutputHelper liveGoldenOutput) =>
+        _liveGoldenOutput = liveGoldenOutput;
+
+    [Theory]
+    [InlineData("ordinary")]
+    [InlineData("periodic")]
+    [InlineData("pending")]
+    public void ResourceSession_FixedInputGoldenCapture(string contour)
+    {
+        var input = contour == "ordinary" ? SessionOrdinaryInput() :
+            SessionPeriodicInput(bounded: contour == "pending");
+        var counter = new SessionAllocationCounter();
+        var result = AcceptedMechanicsPlanner.BuildResources(input, counter.Factory);
+        Assert.True(result.IsValid, SessionIssues(result));
+        Assert.NotEmpty(result.AppliedTransitions);
+        if (contour == "pending")
+            Assert.NotEmpty(result.AcceptedPendingResolutions);
+        _liveGoldenOutput.WriteLine("C1_FIXED_GOLDEN:" + contour + ":" +
+            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
+                SessionResultImage(result) + "\n" + JsonSerializer.Serialize(counter.Ids))));
+    }
+
+    [Fact]
+    public void ResourceSession_LiveContractRedHasOldProductionPositive()
+    {
+        var input = SessionOrdinaryInput();
+        var old = AcceptedMechanicsPlanner.BuildResources(input, new SessionAllocationCounter().Factory);
+        Assert.True(old.IsValid, SessionIssues(old));
+        Assert.Equal(2, old.AppliedTransitions.Count);
+        var counter = new SessionAllocationCounter();
+        var begin = typeof(AcceptedMechanicsPlanner).GetMethod(
+            "BeginLiveResourceExecution", BindingFlags.Static | BindingFlags.NonPublic);
+        if (begin == null)
+        {
+            using var oldSession = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
+            var first = oldSession.AdvanceToClosedBoundary();
+            Assert.Single(first.Checkpoint!.AppliedTransitions);
+            Assert.Equal(2, counter.Calls); // OLD semantic RED: suffix already allocated, actual 4.
+            return;
+        }
+
+        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
+        var owner = typeof(AfterlifeSpiritualConflictResourceOutcome);
+        var expectedType = owner.GetNestedType("ExpectedTransition", BindingFlags.NonPublic)!;
+        var sideType = owner.GetNestedType("SideEvaluation", BindingFlags.NonPublic)!;
+        var batchType = owner.GetNestedType("ExchangeBatch", BindingFlags.NonPublic)!;
+        var mutation = input.Mutations[0];
+        var expected = Array.CreateInstance(expectedType, 1);
+        expected.SetValue(Activator.CreateInstance(expectedType, flags, null, new object[]
+        {
+            mutation.EventRef, mutation.Source.SourceKind, mutation.Source.SourceId,
+            mutation.Coordinate, ResourceTransitionOperation.Spend, 10m, 9m, 1m
+        }, null), 0);
+        var batch = Activator.CreateInstance(batchType, flags, null, new object[]
+        {
+            "live_conflict", "live_exchange_0", 0,
+            Enum.Parse(sideType, "EvaluatedMutation"), Enum.Parse(sideType, "EvaluatedZero"),
+            input.Sources.Exports, new[] { mutation }, expected
+        }, null);
+        var baseline = new AcceptedMechanicsResourceInput(input.Turn, input.Definitions,
+            input.State, input.History, input.Sources, Array.Empty<ResourceMutationIntent>());
+        using var live = Assert.IsAssignableFrom<IDisposable>(
+            begin.Invoke(null, new object?[] { baseline, counter.Factory, null }));
+        live.GetType().GetMethod("StageNextExchange", flags)!.Invoke(live, new[] { batch });
+        Assert.Equal(0, counter.Calls);
+        var step = live.GetType().GetMethod("AdvanceThroughExchange", flags)!.Invoke(live, null)!;
+        var interval = step.GetType().GetProperty("Interval", flags)!.GetValue(step);
+        Assert.NotNull(interval);
+        var transitions = Assert.IsAssignableFrom<IReadOnlyList<ResourceTransition>>(
+            interval!.GetType().GetProperty("AppliedTransitions", flags)!.GetValue(interval));
+        Assert.Single(transitions);
+        Assert.Equal(2, counter.Calls);
+    }
+
     private static AcceptedMechanicsResourceInput SessionOrdinaryInput(decimal secondAmount = 1m)
*** End Patch
```

- [ ] Run the OLD Fast-project focused selection and retain its summary/log/TRX. Expected 14 executions: 13 pass (ten unchanged session controls plus three golden rows), one semantic RED with actual allocator calls 4 versus expected 2. Build failure, missing fixture, missing test or reflection failure is not the expected RED.

```powershell
Set-Location -LiteralPath 'E:/Games/worktrees/boe-1536-wound-materialization'
$c1OldPinPath = 'E:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd/spiritual-journal-c1-old-lane.json'
if (Test-Path -LiteralPath $c1OldPinPath) { throw 'Existing OLD evidence pin requires explicit parent reconciliation; never replace it by recency.' }
& .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests.ResourceSession_" 6>&1 | Tee-Object -Variable c1OldOutput
$c1OldExit = $LASTEXITCODE
$c1OldMatches = [regex]::Matches(($c1OldOutput | ForEach-Object { $_.ToString() }) -join "`n", '(?m)^  Results: (.+)$')
if ($c1OldMatches.Count -ne 1) { throw 'Expected exactly one result path from this OLD invocation.' }
$c1OldPath = (Resolve-Path -LiteralPath $c1OldMatches[0].Groups[1].Value.Trim()).Path
$c1OldSummary = Join-Path $c1OldPath 'summary.json'
$c1OldPin = [ordered]@{
    Phase = 'OLD'
    ResultsPath = $c1OldPath
    RunId = Split-Path -Path $c1OldPath -Leaf
    ExitCode = $c1OldExit
    SummarySha256 = (Get-FileHash -LiteralPath $c1OldSummary -Algorithm SHA256).Hash
}
$c1OldJson = $c1OldPin | ConvertTo-Json
"*** Begin Patch"
"*** Add File: $c1OldPinPath"
$c1OldJson -split "`r?`n" | ForEach-Object { '+' + $_ }
"*** End Patch"
```

- [ ] Apply the emitted OLD evidence-pin patch with apply_patch before changing production code. Record this exact absolute artifact path, run ID and summary hash in the task report and parent ledger. The pin is generated only from this command's Results line, not a directory search; never relabel or overwrite it with a GREEN retry.

- [ ] Run the OLD integration controls. Expected 11/11: six AcceptedMechanicsPlannerScaleTests and five EffectPendingWaveIntegrationTests. Keep all exact capacity/depth/scale/receipt/wave assertions.

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedMechanicsPlannerScaleTests|FullyQualifiedName~EffectPendingWaveIntegrationTests"
```

- [ ] Restore only the temporary OLD test insertion with the exact inverse below, using apply_patch. This is not git reset/checkout and removes no user edit. If its context changed, stop and reconcile the overlapping test edit.

```text
*** Begin Patch
*** Update File: E:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs
@@
-    private readonly Xunit.Abstractions.ITestOutputHelper _liveGoldenOutput;
-
-    public AcceptedMechanicsPlannerTests(Xunit.Abstractions.ITestOutputHelper liveGoldenOutput) =>
-        _liveGoldenOutput = liveGoldenOutput;
-
-    [Theory]
-    [InlineData("ordinary")]
-    [InlineData("periodic")]
-    [InlineData("pending")]
-    public void ResourceSession_FixedInputGoldenCapture(string contour)
-    {
-        var input = contour == "ordinary" ? SessionOrdinaryInput() :
-            SessionPeriodicInput(bounded: contour == "pending");
-        var counter = new SessionAllocationCounter();
-        var result = AcceptedMechanicsPlanner.BuildResources(input, counter.Factory);
-        Assert.True(result.IsValid, SessionIssues(result));
-        Assert.NotEmpty(result.AppliedTransitions);
-        if (contour == "pending")
-            Assert.NotEmpty(result.AcceptedPendingResolutions);
-        _liveGoldenOutput.WriteLine("C1_FIXED_GOLDEN:" + contour + ":" +
-            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
-                SessionResultImage(result) + "\n" + JsonSerializer.Serialize(counter.Ids))));
-    }
-
-    [Fact]
-    public void ResourceSession_LiveContractRedHasOldProductionPositive()
-    {
-        var input = SessionOrdinaryInput();
-        var old = AcceptedMechanicsPlanner.BuildResources(input, new SessionAllocationCounter().Factory);
-        Assert.True(old.IsValid, SessionIssues(old));
-        Assert.Equal(2, old.AppliedTransitions.Count);
-        var counter = new SessionAllocationCounter();
-        var begin = typeof(AcceptedMechanicsPlanner).GetMethod(
-            "BeginLiveResourceExecution", BindingFlags.Static | BindingFlags.NonPublic);
-        if (begin == null)
-        {
-            using var oldSession = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
-            var first = oldSession.AdvanceToClosedBoundary();
-            Assert.Single(first.Checkpoint!.AppliedTransitions);
-            Assert.Equal(2, counter.Calls); // OLD semantic RED: suffix already allocated, actual 4.
-            return;
-        }
-
-        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
-        var owner = typeof(AfterlifeSpiritualConflictResourceOutcome);
-        var expectedType = owner.GetNestedType("ExpectedTransition", BindingFlags.NonPublic)!;
-        var sideType = owner.GetNestedType("SideEvaluation", BindingFlags.NonPublic)!;
-        var batchType = owner.GetNestedType("ExchangeBatch", BindingFlags.NonPublic)!;
-        var mutation = input.Mutations[0];
-        var expected = Array.CreateInstance(expectedType, 1);
-        expected.SetValue(Activator.CreateInstance(expectedType, flags, null, new object[]
-        {
-            mutation.EventRef, mutation.Source.SourceKind, mutation.Source.SourceId,
-            mutation.Coordinate, ResourceTransitionOperation.Spend, 10m, 9m, 1m
-        }, null), 0);
-        var batch = Activator.CreateInstance(batchType, flags, null, new object[]
-        {
-            "live_conflict", "live_exchange_0", 0,
-            Enum.Parse(sideType, "EvaluatedMutation"), Enum.Parse(sideType, "EvaluatedZero"),
-            input.Sources.Exports, new[] { mutation }, expected
-        }, null);
-        var baseline = new AcceptedMechanicsResourceInput(input.Turn, input.Definitions,
-            input.State, input.History, input.Sources, Array.Empty<ResourceMutationIntent>());
-        using var live = Assert.IsAssignableFrom<IDisposable>(
-            begin.Invoke(null, new object?[] { baseline, counter.Factory, null }));
-        live.GetType().GetMethod("StageNextExchange", flags)!.Invoke(live, new[] { batch });
-        Assert.Equal(0, counter.Calls);
-        var step = live.GetType().GetMethod("AdvanceThroughExchange", flags)!.Invoke(live, null)!;
-        var interval = step.GetType().GetProperty("Interval", flags)!.GetValue(step);
-        Assert.NotNull(interval);
-        var transitions = Assert.IsAssignableFrom<IReadOnlyList<ResourceTransition>>(
-            interval!.GetType().GetProperty("AppliedTransitions", flags)!.GetValue(interval));
-        Assert.Single(transitions);
-        Assert.Equal(2, counter.Calls);
-    }
-
     private static AcceptedMechanicsResourceInput SessionOrdinaryInput(decimal secondAmount = 1m)
*** End Patch
```

- [ ] Apply the full companion with apply_patch. It contains every production and test body, including the retained 1,333-line original execution body refactor and every new helper. Do not reconstruct the implementation from prose. The companion's literal bytes are the code step; its nine absolute targets are authoritative. The OLD tests are included again as part of the final test file, now with eight additional live controls.
- [ ] Review the diff before testing. Confirm fixed mode still performs all preparation before first execution; one original state/arbiter/allocator remains live; old mutations never enter a later expansion frontier; pending exits before any sealing/freezing; no B0/B1 file or final engine caller was changed.
- [ ] Run GREEN Fast-project focused selection. Expected 22/22: ten unchanged session tests, nine live Facts including the now-green semantic test, and three exact fixed golden rows.

```powershell
Set-Location -LiteralPath 'E:/Games/worktrees/boe-1536-wound-materialization'
$c1GreenPinPath = 'E:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd/spiritual-journal-c1-green-lane.json'
if (Test-Path -LiteralPath $c1GreenPinPath) { throw 'Existing GREEN evidence pin requires explicit parent reconciliation; never replace it by recency.' }
& .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests.ResourceSession_" 6>&1 | Tee-Object -Variable c1GreenOutput
$c1GreenExit = $LASTEXITCODE
$c1GreenMatches = [regex]::Matches(($c1GreenOutput | ForEach-Object { $_.ToString() }) -join "`n", '(?m)^  Results: (.+)$')
if ($c1GreenMatches.Count -ne 1) { throw 'Expected exactly one result path from this GREEN invocation.' }
$c1GreenPath = (Resolve-Path -LiteralPath $c1GreenMatches[0].Groups[1].Value.Trim()).Path
$c1GreenSummary = Join-Path $c1GreenPath 'summary.json'
$c1GreenPin = [ordered]@{
    Phase = 'GREEN'
    ResultsPath = $c1GreenPath
    RunId = Split-Path -Path $c1GreenPath -Leaf
    ExitCode = $c1GreenExit
    SummarySha256 = (Get-FileHash -LiteralPath $c1GreenSummary -Algorithm SHA256).Hash
}
$c1GreenJson = $c1GreenPin | ConvertTo-Json
"*** Begin Patch"
"*** Add File: $c1GreenPinPath"
$c1GreenJson -split "`r?`n" | ForEach-Object { '+' + $_ }
"*** End Patch"
```

- [ ] Apply the emitted GREEN evidence-pin patch with apply_patch and record the exact path/ID/hash in the task report. A retry requires an explicitly reviewed replacement of the GREEN pin; retain the original OLD pin and identify both selected runs explicitly. No recency heuristic may choose either phase.

- [ ] Run GREEN integration selection. Expected 20/20: the same eleven OLD scale/pending controls, four new accepted-plan interval Facts, and five file-backed outcome rows (three Facts plus two start/terminal theory rows).

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedMechanicsPlannerScaleTests|FullyQualifiedName~EffectPendingWaveIntegrationTests|FullyQualifiedName~SpiritualExchangeInterval_"
```

- [ ] Compare the three OLD/GREEN C1_FIXED_GOLDEN payloads byte-for-byte. They serialize full canonical state/history, applied/replay/events, issue payloads, all statistics, transcript fingerprint/completeness, full pending causal data, reactions, resolved request IDs, trigger execution data and exact allocated GUID lists. The complete PowerShell comparison below reads only the two phase-pinned metadata receipts generated from the actual OLD and GREEN invocations. It checks distinct path/ID, unchanged summary hash, exact row counts, opposite semantic RED/GREEN outcomes, cleanup and all three payloads; it never enumerates lane directories to select a run.

```powershell
$c1PinRoot = 'E:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd'
$c1OldPin = Get-Content -LiteralPath (Join-Path $c1PinRoot 'spiritual-journal-c1-old-lane.json') -Raw | ConvertFrom-Json
$c1GreenPin = Get-Content -LiteralPath (Join-Path $c1PinRoot 'spiritual-journal-c1-green-lane.json') -Raw | ConvertFrom-Json
if ($c1OldPin.Phase -cne 'OLD' -or $c1GreenPin.Phase -cne 'GREEN') { throw 'Incorrect evidence phases.' }
if ($c1OldPin.RunId -eq $c1GreenPin.RunId -or $c1OldPin.ResultsPath -eq $c1GreenPin.ResultsPath) { throw 'OLD and GREEN must be distinct explicitly pinned runs.' }
$c1GoldenRows = @{}
foreach ($c1Pin in @($c1OldPin, $c1GreenPin)) {
    $c1Resolved = (Resolve-Path -LiteralPath $c1Pin.ResultsPath).Path
    if ((Split-Path -Path $c1Resolved -Leaf) -cne $c1Pin.RunId) { throw 'Pinned run ID/path mismatch.' }
    $c1SummaryPath = Join-Path $c1Resolved 'summary.json'
    if ((Get-FileHash -LiteralPath $c1SummaryPath -Algorithm SHA256).Hash -cne $c1Pin.SummarySha256) { throw 'Pinned summary changed.' }
    $c1Summary = Get-Content -LiteralPath $c1SummaryPath -Raw | ConvertFrom-Json
    $c1ExpectedTotal = if ($c1Pin.Phase -ceq 'OLD') { 14 } else { 22 }
    $c1ExpectedFailed = if ($c1Pin.Phase -ceq 'OLD') { 1 } else { 0 }
    if ($c1Summary.EffectiveLane -ne 'Focused' -or
        $c1Summary.Tests.Total -ne $c1ExpectedTotal -or
        $c1Summary.Tests.Executed -ne $c1ExpectedTotal -or
        $c1Summary.Tests.Failed -ne $c1ExpectedFailed -or
        $c1Summary.Tests.Passed -ne ($c1ExpectedTotal - $c1ExpectedFailed) -or
        $c1Summary.ExitCode -ne $c1ExpectedFailed -or
        $c1Pin.ExitCode -ne $c1Summary.ExitCode -or
        $c1Summary.TimedOut -or !$c1Summary.OwnedTreeCleanupSucceeded -or
        @($c1Summary.DuplicateTests).Count -ne 0) {
        throw "Pinned $($c1Pin.Phase) lane has unexpected result evidence."
    }
    $c1Rows = @{}
    $c1SemanticOutcomes = @()
    foreach ($c1Trx in Get-ChildItem -LiteralPath $c1Resolved -Filter '*.trx' -Recurse -File) {
        [xml]$c1Xml = Get-Content -LiteralPath $c1Trx.FullName -Raw
        foreach ($c1Test in $c1Xml.SelectNodes("//*[local-name()='UnitTestResult']")) {
            if ($c1Test.testName -match '(?:^|\.)ResourceSession_LiveContractRedHasOldProductionPositive$') {
                $c1SemanticOutcomes += [string]$c1Test.outcome
            }
            foreach ($c1Output in $c1Test.SelectNodes("./*[local-name()='Output']/*[local-name()='StdOut']")) {
                foreach ($c1Match in [regex]::Matches($c1Output.InnerText, '(?m)^C1_FIXED_GOLDEN:(ordinary|periodic|pending):([A-Za-z0-9+/=]+)\s*$')) {
                    $c1Key = $c1Match.Groups[1].Value
                    if ($c1Rows.ContainsKey($c1Key)) { throw "Duplicate pinned C1 golden row: $c1Key" }
                    $c1Rows[$c1Key] = $c1Match.Groups[2].Value
                }
            }
        }
    }
    $c1ExpectedSemantic = if ($c1Pin.Phase -ceq 'OLD') { 'Failed' } else { 'Passed' }
    if ($c1SemanticOutcomes.Count -ne 1 -or $c1SemanticOutcomes[0] -cne $c1ExpectedSemantic) {
        throw 'Pinned OLD/GREEN semantic test outcome mismatch.'
    }
    if ($c1Rows.Count -ne 3) { throw 'Pinned C1 golden capture is incomplete.' }
    $c1GoldenRows[$c1Pin.Phase] = $c1Rows
}
foreach ($c1Contour in @('ordinary', 'periodic', 'pending')) {
    if ($c1GoldenRows.GREEN[$c1Contour] -cne $c1GoldenRows.OLD[$c1Contour]) {
        throw "C1 fixed-input golden changed: $c1Contour"
    }
    "C1 fixed-input golden exact: $c1Contour"
}
$c1OldPin, $c1GreenPin | Select-Object Phase, RunId, ResultsPath, SummarySha256
```

- [ ] Parent confirms the pinned absolute paths/run IDs in both receipts equal its recorded OLD and selected GREEN invocations and the named OLD failure is allocator calls 4 versus 2, not a reflection/build failure. Any change to fixed state/history/IDs/stats/transcript requires fixing the refactor; do not refresh expected values or relax B2A/B2B assertions.
- [ ] Parent reads every resulting source/test body and inspects all focused summary/log/TRX evidence, including named row membership, no skips/duplicates, zero build warnings/errors, TimedOut=false and owned-tree cleanup success. Parent obtains an independent C1 task review with both spec-compliance and quality verdicts, then runs one separate meaningful C1 Fast checkpoint. The earlier combined B0/B1/GM Fast and conditional FullValidation must already have finished before C1 dispatch; they are neither repeated here nor retroactively combined with C1. No worker Fast or repeated broad lane is included.
- [ ] Parent runs the separate meaningful C1 checkpoint only after the named focused evidence and independent task review are accepted:

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
```

- [ ] Record the no-GM-update rationale below, remaining C2/D/E obligations and actual verification paths. Parent alone marks this bounded task complete after review/evidence. Commit only the reviewed tracked scope if separately authorized; no blanket git add, remote publication or issue closure.

## Coverage and non-claims

The nine new Fast Facts prove: semantic old-eager RED; final-suffix replacement/no discarded allocations and exact live-vs-live identities; accepted/foreign exchange rejection and real owner-reference/detachment; exact before-witness tampering with no final images; exact empty zero ranges; rejection of an accepted zero exchange ID at the next ordinal while still allowing unaccepted replacement and a distinct next zero ID; missing-side pending; disposal/fault/reentrancy; separate replay/new ranges and immutable prefix.

The four accepted-plan Facts prove real causal descendants close before an interval; remaining uses are not reset; a previously unused original seed can activate on a later event; a real bounded pending receipt cannot mint an interval or freeze history; foreign definition references fail before allocation. The causal comparison validates event and transcript producer/parent/closure/component relationships through unique operation/transition mappings. It does not claim allocation-ID chronology matches between eager and live modes.

The file-backed outcome tests preserve both original side chains, consume production TryCreate batches, validate the original Draft.Project witness, leave canonical files unchanged, distinguish explicit evaluated-zero from missing side/whole audit, and retain start/terminal requirements. Synthetic Fast batches and the accepted-plan resource fixture are low-level resource observations, never source-admission positives.

This is not all eight wound-profile consumption, same-turn actual wound materialization, cross-source retirement, source generation binding, receipt-resume, cold restart or engine publication coverage. Those are mandatory C2/D/E acceptance tests. No C1 test treats a missing source as evaluated-empty proof. The static existing receipt tests remain enabled and unchanged.

## GM-facing synchronization decision

No GM-authored capability or runtime contract changes in pure C1: no command, actionType, JSON schema, pending/control file, receipt format, response, report, normalizer, authority acceptance path or final publication behavior is added/changed. New types are intentionally client-owned internal resource data; the tracked task must record that distinction. Therefore this task has no Mortal World/afterlife prompt, example, matrix, manifest or documentation-source-guard mutation, and no FullValidation run.

B1's source/action GM synchronization remains B1-owned. C2/D/E runtime admission, lifecycle, pending/receipt or publication changes must revisit OtherGuides/Afterlife_Contract_Matrix.md, Examples/E_CLI_Afterlife_Turns.txt, Examples/example_validation_manifest.json, AfterlifeDocumentationCoverageTests, ExampleDocumentationValidationTests and relevant prompt entrypoints together with worked lawful examples. Do not use this C1 rationale to waive that later synchronization.

## Mechanical verification and hash gate

Five existing target blobs, verified equal both in the working files and at ecffabdc002cacfc3362d071c26c747d443313b2:

| Existing target | Git blob SHA-1 |
|---|---|
| BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs | 9e990378743d7137ce4141cb106687126441b951 |
| BookOfEternityClient/Services/AfterlifeSpiritualConflictResourceOutcome.cs | 34baab0a1396d5989b25fbcc2109628d21a0c300 |
| BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs | 74d2b10f666c70f46536887734431dbf73475232 |
| BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs | 1c349407f6430558b6a8c67e36742736789d5b97 |
| BookOfEternityClient.IntegrationTests/EffectResourceTriggerRoutingScaleTests.cs | 74529d409e2dc70fd7c46f956e8f3019de4930ba |

Companion: docs/superpowers/plans/2026-09-08-spiritual-journal-c1-implementation.patch
SHA-256: A2D460BD7B48BBE34187698E5EED96D8EB79480482AF807E8CF36B0F3BA8B4D1
Parent promotion normalizes bare diff-context blank lines only; all nine source/test postimages are unchanged.

Initial-review postimages remain under E:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd/spiritual-journal-c1-postimages/ as historical snapshots. This bounded correction changes only the plan and complete companion, so its planner/session-test snapshots are superseded; reconstruct the current postimages from the companion, not those two historical files. Only the companion is applied to source. Read-only git diff --no-index produced five unified diffs with eight context lines. An in-memory whole-line replay required exactly one match for each of eighteen hunks and applied them in order. The initial companion reconstructed all original review postimages exactly. The corrected companion was fully replayed again: only the session's four retained-ID guard lines and the 49-line deterministic test insertion differ from those reviewed postimages; the other seven file bodies are exact. Four Add File targets were confirmed absent and have complete bodies. There was no substring-only applicability assertion, git apply, index write or C# run.

Postimage line counts: AcceptedMechanicsPlanner.cs 8738; LiveTurn.cs 1660; outcome 606; EffectAcceptedTurnPlanner.cs 9255; BaseResourceRouting.cs 77; session tests 880; scale original 5580; scale new 244; afterlife new 148. Hashes and exact replay are mechanical evidence only, not compilation or test success.

## Review risks and acceptance boundary

1. The real refactor is intentionally substantial: moving the original iterator preserves behavior by shared code, but only OLD/GREEN named evidence and unchanged fixed goldens can establish compatibility.
2. Live graph reconstruction revisits descriptor validation but not accepted execution. Parent must audit completed-operation scheduler treatment, causal closure membership, exact prefix IDs and work counters; performance claims apply only where tests measure them.
3. Full original seeds are immutable-base data. C2 must not use this seed accessor as a registry for current draft generations or newly created wounds.
4. Missing-side and bounded-receipt observations are safe retained pending states with no supplied resume implementation. Their continuation and actual production caller are an explicit engineering dependency before runtime cutover.
5. A resource interval is insufficient for source admission. C2 must combine real interval ownership with the real B1 source reference and same exchange/version/branch ownership, not just content/fingerprints.
6. Static outcome composition still reconstructs its separate original per-side chain. Live C2 must supply the next actual source evaluation using retained prefix/current resource evidence while preserving exact Draft witness; it cannot discover the future by allocating and discarding a suffix.
7. Parent's B1 work may change HEAD; exact source targets must be rehashed immediately before applying. Any overlapping change invalidates mechanical applicability and requires a new reviewed companion.

The permitted C1 completion statement is: shared production resource core retained, live resource-local whole-exchange staging/closure implemented and tested, fixed compatibility proven, pending/source prerequisites kept unsealed. It is not: full journal admission, complete C, wound materialization, receipt-resume, cold reconstruction or single-publication engine cutover.
