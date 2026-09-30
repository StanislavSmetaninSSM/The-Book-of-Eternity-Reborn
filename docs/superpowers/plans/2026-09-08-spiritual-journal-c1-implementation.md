# Spiritual journal C1 retained exchange intervals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Parent uses superpowers:subagent-driven-development for this single coherent task. The user has explicitly authorized delegation and model choice; parent selects each model explicitly, dispatches one source-owning implementer and a separate task reviewer, and owns evidence/acceptance gates. This metadata-review child does not delegate. Steps use checkbox syntax for tracking.

**Parent tracking status:** Bounded C1 and verification follow-up C1-V1 are accepted by the parent on2026-09-13. The parent read the complete companion and corrections, verified actual source hashes against independent reviews, inspected final GREEN22/22, Integration20/20, V1 Focused2/2 and all30TRXs from corrected Fast7882/7882, and reran the exact three full fixed goldens against the same immutable corrected OLD pin. Both independent reviews are Spec PASS/Quality PASS with0openC/I/M. See the final acceptance record below for exact artifact paths and limits. B1 at66d11b02 and its prior combined Fast7870/7870 plus conditional FullValidation1920/1920 atab2dd976 remain separate historical gates. C2/D/E and full #1536 remain open.

**Goal:** Refactor the production resource planner into one retained execution owner that can accept the next resource-local whole-exchange batch before allocation and expose detached, exact closed intervals without completing the effect plan.

**Architecture:** The existing fixed-input adapter and the new live session share one real ResourceExecutionState, ledger, working history, identity factory/registry, arbiter, transcript builder and accepted sets. Fixed input retains eager preparation; live input prepares baseline lifecycle work once and only the admitted next exchange plus its causal expansion thereafter. An immutable-base routing object owns the original effect-plan reference, original owner authority and definitions and supplies its retained index's full canonical seed view before the one arbiter initialization.

**Tech Stack:** Existing .NET 8/C#, xUnit, System.Text.Json, PowerShell 7, repository bounded C# lane runner; no new package or public contract.

## Global constraints and status

Parent OLD-fixture correction2026-09-09: the initial run
20260909-001823-083-38640-deddf28fd3004abd8a095ec81408dd03-focused produced12/14,
not the expected13/14. The intended allocation RED is4versus2; the second failure
is the new golden's unconditional AppliedTransitions NotEmpty, before its pending
assertion. Existing ResourceSession_UnresolvedReceiptNeverYieldsAClosedCheckpoint
passes and the same bounded fixture deliberately has no mutations while waiting.
The corrected golden requires pending applied transitions empty, accepted pending
resolutions nonempty and transcript incomplete; other contours retain nonempty
applied transitions. No fixture input or production code changes. All three literal
copies (OLD, inverse, final companion) must agree. Preserve original
spiritual-journal-c1-old-lane.json unchanged as the invalid-golden attempt; the
corrected OLD phase uses the distinct spiritual-journal-c1-old-corrected-golden-lane.json
through both capture and full-payload comparison below. A valid corrected OLD
must precede production changes; do not refresh expected goldens after GREEN.

Parent Integration-helper correction 2026-09-13: corrected GREEN Fast passed
22/22 and all three fixed-input goldens matched byte-for-byte. Integration run
20260913-014313-714-18888-18fbed93fd1d41ebbd4a13935055a74d-focused
passed 19/20 but exposed one test-helper assumption, not a production-contract
failure: LiveCausalSemantics indexed every causal-closure operation through an
AppliedTransitions-only dictionary. The transcript contract lawfully includes
causal operations that produce no applied transition and separately retains its
complete unique ReplayStableOperationKeys set. The bounded correction asserts
non-null transcript evidence, equal unique operation/stable-key cardinality,
compares the applied subset by canonical transition ordinal, records the
non-applied count and retains the complete stable-key set. It also removes the
CS8602 helper warning. No production body, fixture, expected gameplay behavior,
causal relationship or identity-uniqueness guarantee changes. Preserve the
original failed Integration receipt; run only the named one-test Focused control
before returning the complete Integration20 gate to the parent.

Parent staged-coordinate correction 2026-09-13: review found that the live
StageNextExchange replacement guard compared only ExchangeId. Before the first
TakeStaged call, _conflictId is still null, so a same-ordinal, same-exchange-ID
batch with a foreign ConflictId could replace the staged batch. The strengthened
existing Fast test stages the foreign replacement before AdvanceThroughExchange,
requires rejection with zero allocation, and then proves that the original
staged conflict/exchange coordinate is the interval that advances. Focused RED
20260913-015139-244-28264-0e87da30d1c64162bd4ff0565d271e57-focused
built with zero warnings/errors and failed only because no exception was thrown.
The minimal production guard compares staged ConflictId, ExchangeId and Ordinal.
Focused GREEN
20260913-015355-188-22844-f8ba2f2e53c64ec3ade14fc3b337be68-focused
passed 1/1 with zero warnings/errors and complete cleanup. No B1, C2, source
admission, GM contract or publication surface changes.

- Source issue: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536). Active durable artifacts: specs/1536-complete-wound-materialization/spec.md, plan.md, tasks.md, contracts/spiritual-wound-live-turn-boundary.md. Governance: .specify/memory/constitution.md and applicable AGENTS.md.
- The full C1 companion has been applied but is not an accepted C1 result. The helper correction passed its named one-test Focused control at 20260913-014951-936-18832-adf6eedc096049d7a413907bf6526710-focused with zero warnings/errors, and the staged-coordinate correction has the RED/GREEN evidence above. Parent still owns the repeated GREEN22, Integration20, independent review and Fast gates. No index/HEAD was written.
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

All source paths below are relative to D:/Games/worktrees/boe-1536-wound-materialization. The companion has absolute target headers.

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

- [x] Read governance, active artifacts, this plan and every companion body. Reconcile current B1 source; preserve unrelated dirty files. Verify the five base blobs and four absent new targets in the table below.
- [x] Parent verifies its B0/B1/GM combined Fast and GM-sensitive conditional FullValidation are already complete/reviewed, then records the single C1 tracked task and bounded scope before code edits. Parent dispatches one coherent source-owning implementer with the complete code companion, exact task brief and report path; the user-authorized model is selected explicitly.
- [x] Apply only the following complete OLD-compilable test patch with apply_patch. It uses no newly introduced type in compiled expressions. Its production positive succeeds on OLD; OLD then actually allocates the suffix before its first checkpoint and fails 2 versus 4 allocator calls.

```text
*** Begin Patch
*** Update File: D:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs
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
+        if (contour == "pending")
+        {
+            Assert.Empty(result.AppliedTransitions);
+            Assert.NotEmpty(result.AcceptedPendingResolutions);
+            Assert.False(result.EffectBoundaryTranscript!.IsComplete);
+        }
+        else
+            Assert.NotEmpty(result.AppliedTransitions);
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

- [x] Run the OLD Fast-project focused selection and retain its summary/log/TRX. Expected 14 executions: 13 pass (ten unchanged session controls plus three golden rows), one semantic RED with actual allocator calls 4 versus expected 2. Build failure, missing fixture, missing test or reflection failure is not the expected RED.

```powershell
Set-Location -LiteralPath 'D:/Games/worktrees/boe-1536-wound-materialization'
$c1OldPinPath = 'D:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd/spiritual-journal-c1-old-corrected-golden-lane.json'
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

- [x] Apply the emitted OLD evidence-pin patch with apply_patch before changing production code. Record this exact absolute artifact path, run ID and summary hash in the task report and parent ledger. The pin is generated only from this command's Results line, not a directory search; never relabel or overwrite it with a GREEN retry.

- [x] Run the OLD integration controls. Expected 11/11: six AcceptedMechanicsPlannerScaleTests and five EffectPendingWaveIntegrationTests. Keep all exact capacity/depth/scale/receipt/wave assertions.

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedMechanicsPlannerScaleTests|FullyQualifiedName~EffectPendingWaveIntegrationTests"
```

- [x] Restore only the temporary OLD test insertion with the exact inverse below, using apply_patch. This is not git reset/checkout and removes no user edit. If its context changed, stop and reconcile the overlapping test edit.

```text
*** Begin Patch
*** Update File: D:/Games/worktrees/boe-1536-wound-materialization/BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs
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
-        if (contour == "pending")
-        {
-            Assert.Empty(result.AppliedTransitions);
-            Assert.NotEmpty(result.AcceptedPendingResolutions);
-            Assert.False(result.EffectBoundaryTranscript!.IsComplete);
-        }
-        else
-            Assert.NotEmpty(result.AppliedTransitions);
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

- [x] Apply the full companion with apply_patch. It contains every production and test body, including the retained 1,333-line original execution body refactor and every new helper. Do not reconstruct the implementation from prose. The companion's literal bytes are the code step; its nine absolute targets are authoritative. The OLD tests are included again as part of the final test file, now with eight additional live controls.
- [x] Review the diff before testing. Confirm fixed mode still performs all preparation before first execution; one original state/arbiter/allocator remains live; old mutations never enter a later expansion frontier; pending exits before any sealing/freezing; no B0/B1 file or final engine caller was changed.
- [x] Run GREEN Fast-project focused selection. Expected 22/22: ten unchanged session tests, nine live Facts including the now-green semantic test, and three exact fixed golden rows.

```powershell
Set-Location -LiteralPath 'D:/Games/worktrees/boe-1536-wound-materialization'
$c1GreenPinPath = 'D:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd/spiritual-journal-c1-green-lane.json'
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

- [x] Apply the emitted GREEN evidence-pin patch with apply_patch and record the exact path/ID/hash in the task report. A retry requires an explicitly reviewed replacement of the GREEN pin; retain the original OLD pin and identify both selected runs explicitly. No recency heuristic may choose either phase.

- [x] Run GREEN integration selection. Expected 20/20: the same eleven OLD scale/pending controls, four new accepted-plan interval Facts, and five file-backed outcome rows (three Facts plus two start/terminal theory rows).

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedMechanicsPlannerScaleTests|FullyQualifiedName~EffectPendingWaveIntegrationTests|FullyQualifiedName~SpiritualExchangeInterval_"
```

- [x] Compare the three OLD/GREEN C1_FIXED_GOLDEN payloads byte-for-byte. They serialize full canonical state/history, applied/replay/events, issue payloads, all statistics, transcript fingerprint/completeness, full pending causal data, reactions, resolved request IDs, trigger execution data and exact allocated GUID lists. The complete PowerShell comparison below reads only the two phase-pinned metadata receipts generated from the actual OLD and GREEN invocations. It checks distinct path/ID, unchanged summary hash, exact row counts, opposite semantic RED/GREEN outcomes, cleanup and all three payloads; it never enumerates lane directories to select a run.

```powershell
$c1PinRoot = 'D:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd'
$c1OldPin = Get-Content -LiteralPath (Join-Path $c1PinRoot 'spiritual-journal-c1-old-corrected-golden-lane.json') -Raw | ConvertFrom-Json
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

- [x] Parent confirms the pinned absolute paths/run IDs in both receipts equal its recorded OLD and selected GREEN invocations and the named OLD failure is allocator calls 4 versus 2, not a reflection/build failure. Any change to fixed state/history/IDs/stats/transcript requires fixing the refactor; do not refresh expected values or relax B2A/B2B assertions.
- [x] Parent reads every resulting source/test body and inspects all focused summary/log/TRX evidence, including named row membership, no skips/duplicates, zero build warnings/errors, TimedOut=false and owned-tree cleanup success. Parent obtains an independent C1 task review with both spec-compliance and quality verdicts, then runs one separate meaningful C1 Fast checkpoint. The earlier combined B0/B1/GM Fast and conditional FullValidation must already have finished before C1 dispatch; they are neither repeated here nor retroactively combined with C1. No worker Fast or repeated broad lane is included.
- [x] Parent runs the separate meaningful C1 checkpoint only after the named focused evidence and independent task review are accepted:

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
```

- [x] Record the no-GM-update rationale below, remaining C2/D/E obligations and actual verification paths. Parent alone marks this bounded task complete after review/evidence. Commit only the reviewed tracked scope if separately authorized; no blanket git add, remote publication or issue closure.

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
SHA-256: 8E3805E2BCEB4A3990452FB7D2E519E4574651A8C718D15DAA776EFF1B3913C4
Parent promotion normalizes bare diff-context blank lines only. The D:/Games target-header translation changes no postimage; the corrected pending-golden branch changes only the session-test postimage.

Initial-review postimages remain under E:/Games/The Book of Eternity Reborn/.git/worktrees/boe-1536-wound-materialization/sdd/spiritual-journal-c1-postimages/ as historical snapshots. These bounded corrections supersede the historical planner, session-test and accepted-plan interval-test postimages; reconstruct current final postimages from the companion. An in-memory whole-line replay of the corrected companion against current HEAD required exactly one match for each of eighteen update hunks and applied them in order; all four Add File bodies also matched their actual targets exactly. Relative to the initial-review postimages, the planner postimage has the two-line full staged-coordinate guard expansion; the session postimage contains the already recorded four retained-ID guard lines and 49-line deterministic test insertion, the five-line pending-golden expectation expansion, and the six-line pre-advance foreign-conflict regression; the accepted-plan interval-test postimage contains the eleven-line causal-helper correction described above. The other six postimages are unchanged by these corrections. Both latest deltas compiled and passed their exact named one-test Focused controls; independent review and parent full gates remain pending. There was no git apply or index write.

Postimage line counts: AcceptedMechanicsPlanner.cs 8740; LiveTurn.cs 1660; outcome 606; EffectAcceptedTurnPlanner.cs 9255; BaseResourceRouting.cs 77; session tests 891; scale original 5580; scale new 255; afterlife new 148. Hashes and exact replay are mechanical evidence only, not review acceptance or full-gate test success.

## Review risks and acceptance boundary

1. The real refactor is intentionally substantial: moving the original iterator preserves behavior by shared code, but only OLD/GREEN named evidence and unchanged fixed goldens can establish compatibility.
2. Live graph reconstruction revisits descriptor validation but not accepted execution. Parent must audit completed-operation scheduler treatment, causal closure membership, exact prefix IDs and work counters; performance claims apply only where tests measure them.
3. Full original seeds are immutable-base data. C2 must not use this seed accessor as a registry for current draft generations or newly created wounds.
4. Missing-side and bounded-receipt observations are safe retained pending states with no supplied resume implementation. Their continuation and actual production caller are an explicit engineering dependency before runtime cutover.
5. A resource interval is insufficient for source admission. C2 must combine real interval ownership with the real B1 source reference and same exchange/version/branch ownership, not just content/fingerprints.
6. Static outcome composition still reconstructs its separate original per-side chain. Live C2 must supply the next actual source evaluation using retained prefix/current resource evidence while preserving exact Draft witness; it cannot discover the future by allocating and discarding a suffix.
7. Parent's B1 work may change HEAD; exact source targets must be rehashed immediately before applying. Any overlapping change invalidates mechanical applicability and requires a new reviewed companion.

The permitted C1 completion statement is: shared production resource core retained, live resource-local whole-exchange staging/closure implemented and tested, fixed compatibility proven, pending/source prerequisites kept unsealed. It is not: full journal admission, complete C, wound materialization, receipt-resume, cold reconstruction or single-publication engine cutover.

## Observed Fast verification follow-up (2026-09-13)

Tracked under #1536 / T081-B2C-J2-C1-V1 before edits. The first parent Fast run
`20260913-020621-837-26732-a5004cf3b74f43dca464405178624e3d-fast` stopped after
5669 executions with two failures: the wound ownership inventory still expects
the former non-partial planner declaration, and the invalid parameter-set test
expects English PowerShell prose on the restored Russian Windows installation.

The nine-file source companion remains unchanged by this follow-up. Make only
the required verification corrections in `WoundMaterializationSourceGuardTests.cs`
and `FastTestBoundaryTests.cs`: match the actual exact partial declaration, and
assert the stable structured PowerShell parameter-binding error identity while
preserving direct CLI invocation, rejected argument combinations, nonzero exit,
no lane-start evidence, and existing deadlines/cleanup. Do not match a growing
list of translated messages or accept any generic nonzero failure. Parent owns
the combined focused verification and one justified corrected Fast run after
independent review. No GM-facing contract, runner implementation, production
behavior, category placement or FullValidation boundary changes.

## Parent acceptance record (2026-09-13)

All C1 implementation and evidence steps above have been completed. The original
failed OLD, first Integration and first Fast artifacts remain unchanged. The
eleven existing cold-build CS1998 warnings were recorded against unchanged HEAD
sources; the final controls below have zero build warnings and errors under
system SDK10.0.401/runtime8.0.31, without changing target frameworks.

Artifacts are rooted at `TestResults/test-lanes/` in the active D:/Games/worktrees/boe-1536-wound-materialization worktree:

- Corrected OLD14: `20260913-005724-621-24132-7177304af20d4d5b9f92e1a109925d41-focused`,13PASS+1 intended allocator RED, immutable corrected-golden pin.
- Final GREEN22: `20260913-020234-712-11164-faf7fdb9bf894ebe8e66b756575d371e-focused`,22/22; selected GREEN pin with the previous selection archived.
- Final Integration20: `20260913-020431-636-11572-980395682de54d869a14cbbe8e5d5af0-focused`,20/20.
- V1 combined Focused: `20260913-021849-370-28256-7730cf11f9aa40dc936651f6a5259b99-focused`,2/2.
- Corrected Fast: `20260913-022031-360-25788-7b45153e40764e18b9d0ef56d558d744-fast`,7882/7882 in3:18.8668776/default5m; summary SHA256 `95C62D3A779B737474372817F63BB0D9FA644F0F974BDC4C45532F14554E19B7`.

Every final lane exits0 with no timeout, skips or cross-TRX duplicate test IDs and
complete owned-tree cleanup. Parent parsed all7882 Fast rows across30TRXs and
confirmed7882 unique execution IDs. The seven repeated test-definition IDs are
ordinary theory rows within their owning single TRX, matching the runner's
explicit same-TRX deduplication contract; none occurs in multiple shards.

Scratch evidence directory: `.superpowers/sdd/2026-09-08-spiritual-journal-c1-implementation/`.
`c1-fixed-golden-corrected-comparison.log` confirms all three full payloads exact.
`c1-full-independent-review.md` (SHA256 E24E635D20818FDE00C1E7361CBC7B749B966C0C1E3C80B93504DFFF3815527D)
and `c1-fast-verification-review.md` (SHA256 D9116ED71BAF8B1ACD68E5AA8C325B54ED50160703249CB523C1B49A49B30733)
both pass spec and quality with no open findings. Parent inspected these reviews
and the reviewed source hashes, not an agent completion statement alone.

Completion covers the shared retained resource core and resource-local exchange
staging/closure only. Source admission, original-turn generation binding,
registration/retirement, real pending resume, all wound consumers, cold restart
and single final publication remain mandatory C2/D/E work. The no-GM-update
rationale above still applies only to C1. No commit, push, issue closure,
PreMerge or C1 FullValidation was performed.

### Scope clarification 2026-09-16

Owner chose chronological spiritual exchanges in #1536 C2-ORDER. Fixed/live
comparison above applies to inputs with matching execution order; it does not
require global phase sorting across future live exchanges. Fixed-adapter goldens
remain unchanged. See the chronological section of the live-turn boundary
contract for the ordinary prefix and recovery-before-later-cost examples.
