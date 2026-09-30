# Nonterminal effect prefix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose a validated detached closed effect prefix from the retained resource session without completing, sealing or replaying accepted effect work.

**Architecture:** One distinct ClosedPrefix view shares the existing transcript image capture and structural validator. A private validation mode distinguishes a closed unsealed frontier from terminal completion; the resource session consumes that view only on explicit stepping. Ordinary Drain and final Freeze retain existing semantics.

**Tech Stack:** C#/.NET8, existing effect/resource authority contracts, xUnit, PowerShell7 bounded lanes.

## Global Constraints

- Tracked issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), nested T081-B2B. Follow constitution and specs/1536-complete-wound-materialization/{spec,plan,tasks}.md plus contracts/spiritual-wound-live-turn-boundary.md.
- Modify exactly BookOfEternityClient/Services/AcceptedEffectBoundaryTranscript.cs, BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs (checkpoint/pause integration only), BookOfEternityClient.Tests/AcceptedEffectBoundaryTranscriptTests.cs (sealed -> sealed partial only), new BookOfEternityClient.Tests/AcceptedEffectBoundaryTranscriptTests.ClosedPrefix.cs, and BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs (one new Fact only).
- ClosedPrefix exposes detached accumulated evidence, not a completed transcript, AcceptedMechanicsPlan, source/instance-existence proof or publication authority. It has no completion/conversion API and cannot carry a terminal or pending seal.
- CaptureClosedPrefix does not freeze the builder, seal use projection, allocate mechanics ordinals/identities, advance the arbiter or change budgets. Repeated captures are stable; older images remain detached after advancement.
- Reuse all existing structural authority/closure/pending checks. Terminal Freeze keeps its old mandatory use projection and diagnostics. Prefix validation additionally proves every allocated ordinal has evidence, including a trailing orphan where observed ordinals remain contiguous; do not filter error codes after terminal validation.
- Exact last-use and terminal-availability lookups require EffectReplayIdentity including ResourcePendingAuthorityBinding. Matching only an effect ID or a caller flag is insufficient.
- The retained resource session captures the prefix only at its existing closed-frontier pause. Invalid prefix evidence terminates with the owning failure and no checkpoint. Ordinary Drain creates no checkpoints/prefix images and retains exact allocations, result fields and finalization.
- Preserve the committed f5e4de2f real-candidate SessionPeriodicInput and causal event resolver helpers and all nine existing session Facts; the added session Fact consumes their actual candidates and verifies two accepted component mutations plus the ordinary suffix.
- Full T081-B2 remains open for the real effect draft/journal, authorized wound insertion, next-exchange consumption, cross-side/zero-mutation source authority, suffix recomposition and authenticated receipt/cold continuation. Scalar spiritual arts remain0..5; no art-XP objects, gameplay migration, extra player turn/OD/dice/progression or safe-cycle change.
- Same E:/Games/worktrees/boe-1536-wound-materialization, branch1536-complete-wound-materialization. Apply patches with absolute worktree paths. No remote/new branch/worktree/issue closure/session cleanup. Preserve unrelated .serena/ without inspection or staging.
- The implementer solely owns these C# files and bounded C# execution until reporting completion. PowerShell7 plus scripts/test-csharp.ps1 only, exact split-project Focused selections/default5m. No child Fast/PreMerge/full-solution run; parent owns the meaningful Fast checkpoint after actual focused evidence review.
- The first test must prove a nonempty successful old terminal transcript before the missing-API reflection RED. Preserve assertions and failed artifacts; diagnose unexpected failures against the current real contracts before changing fixtures.
- No Mortal/afterlife GM-facing capability, schema, pending transport, source authority or UI behavior changes in this internal prerequisite. No GM prompt/example/matrix/manifest/source-guard synchronization is required for this bounded unit. Report any discovered external behavior change instead of silently absorbing it.

### Task 1: Capture and production-consume an unsealed closed effect prefix

**Files:** The five production/test paths in Global Constraints. Parent owns durable plan/spec/task tracking and acceptance.

**Interfaces:** Consume AcceptedEffectBoundaryTranscript.Builder, EffectReplayIdentity and the existing ResourceExecutionSession closed pause. Produce AcceptedEffectBoundaryPrefixResult, AcceptedEffectBoundaryTranscript.ClosedPrefix, Builder.CaptureClosedPrefix() and ResourceClosedBoundaryCheckpoint.EffectPrefix, fully implemented in the companion below.

- [X] **Step1: Stage only the old-API positive-control/reflection Fact and owning partial declaration; record the semantic RED.**
- [X] **Step2: Apply the complete companion production and test postimages, preserving every other test/helper and the terminal validator contract.**
- [X] **Step3: Run the three exact GREEN controls, inspect their actual summaries/logs/TRX and retain all failed artifacts.**
- [X] **Step4: Self-review complete scope and exact companion agreement; commit only the five scoped files and write the full implementation report.**

Companion: `docs/superpowers/plans/2026-09-08-nonterminal-effect-prefix.patch`. It supplies all production bodies and all nine Facts; the same complete text follows below so this task's extracted brief is self-contained. Rebase Update/Add File headers to the absolute active worktree for apply_patch. This is planned code, not execution evidence.

**Semantic RED staging:** Before production exists, change only the existing transcript test class to sealed partial and add a partial file containing exactly the companion's using/namespace/class wrapper, ClosedPrefix_ContractRedHasExistingTerminalPositiveControl and PrefixEmptyBoundary. Those methods call only existing APIs before reflecting CaptureClosedPrefix. Do not stage the seven other builder Facts or session Fact yet: they require the new compile-time API. Expected RED is Assert.NotNull(capture) after a valid one-boundary old terminal transcript, not compilation/setup failure. Then apply the complete companion, reconciling the already-staged declaration/file postimage rather than duplicating them.

**Exact bounded commands:**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ClosedPrefix_ContractRedHasExistingTerminalPositiveControl"
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedEffectBoundaryTranscriptTests|FullyQualifiedName~AcceptedEffectUseArbiterTests|FullyQualifiedName~ResourceSession_"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectPendingWaveIntegrationTests"
```

The first command is RED staging; the remaining three must pass after implementation. The builder selection includes all eight new builder Facts and all ten retained-session Facts. Keep Fast and Integration filters separate. Parent handles independent review and one meaningful Fast control; do not run either a whole suite or FullValidation for this internal-only change.

**Report contract:** State status, recorded base and final commit, exact changed files, complete commands, artifact paths, test counts/failures/skips/duplicates, build warnings/errors, wall time/timeout and owned-process cleanup. Explain every deviation from the companion and whether an assertion changed. Confirm ordinary drain and terminal validation preserve their original behavior, metadata/unrelated .serena/ remain untouched, and no live wound insertion/source proof is claimed.

**Bounded scope rationale:** This nine-test view is a prerequisite, not a substitute for materialized effect state. The next required owner must retain actual history/materialization edits and preserve consumption-before-replacement anchors, then be consumed by real wound insertion and the next exchange. Capturing a view alone cannot accept full B2 or any live GM-facing wound source.

## Complete executable companion

```diff
*** Begin Patch
*** Update File: BookOfEternityClient/Services/AcceptedEffectBoundaryTranscript.cs
@@
 internal sealed record AcceptedEffectBoundaryTranscriptResult(
     AcceptedEffectBoundaryTranscript? Transcript,
     IReadOnlyList<EffectBoundaryTranscriptIssue> Issues)
 {
     internal bool IsValid => Transcript != null && Issues.Count == 0;
 }

+internal sealed record AcceptedEffectBoundaryPrefixResult(
+    AcceptedEffectBoundaryTranscript.ClosedPrefix? Prefix,
+    IReadOnlyList<EffectBoundaryTranscriptIssue> Issues)
+{
+    internal bool IsValid => Prefix != null && Issues.Count == 0;
+}
+
 internal sealed class AcceptedEffectBoundaryTranscript
 {
@@
     private static bool CandidateClaimsAvailabilityEffectId(
         EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
         string effectId)
     {
@@
         return candidate.ReactionOutputs.Any(reaction => string.Equals(
             ResolveTerminalAvailabilityEffectId(reaction),
             effectId,
             StringComparison.Ordinal));
     }

+    internal sealed class ClosedPrefix
+    {
+        private readonly AcceptedEffectBoundaryTranscript _image;
+
+        internal ClosedPrefix(AcceptedEffectBoundaryTranscript image)
+        {
+            ArgumentNullException.ThrowIfNull(image);
+            if (image.UseProjectionOrdinal != null ||
+                image.PendingFrontierBoundaryOrdinal != null)
+            {
+                throw new ArgumentException(
+                    "A closed prefix cannot carry a terminal or pending seal.",
+                    nameof(image));
+            }
+            _image = image;
+        }
+
+        internal EffectAcceptedPlanAuthorityStamp? PlanAuthority => _image.PlanAuthority;
+        internal string Fingerprint => _image.Fingerprint;
+        internal IReadOnlyList<EffectEventBoundaryStamp> Boundaries => _image.Boundaries;
+        internal IReadOnlyList<EffectEventBoundaryCloseStamp> BoundaryCloses => _image.BoundaryCloses;
+        internal IReadOnlyList<EffectBoundaryCausalClosureStamp> CausalClosures => _image.CausalClosures;
+        internal IReadOnlyList<AcceptedEffectBoundaryActivation> AcceptedActivations => _image.AcceptedActivations;
+        internal IReadOnlyList<RejectedEffectBoundaryActivation> RejectedActivations => _image.RejectedActivations;
+        internal IReadOnlyList<AppliedEffectComponentEvidence> AppliedComponentEvidence => _image.AppliedComponentEvidence;
+        internal IReadOnlyList<AcceptedResourceMutationEvidence> ResourceMutations => _image.ResourceMutations;
+        internal IReadOnlyList<ReleasedEffectReaction> ReleasedReactions => _image.ReleasedReactions;
+        internal IReadOnlyList<EffectTerminalAvailabilityReservation> TerminalAvailabilityReservations =>
+            _image.TerminalAvailabilityReservations;
+        internal IReadOnlyDictionary<EffectReactionExpansionKey, EffectReactionExpansionUsage> ExpansionUsage =>
+            _image.ExpansionUsage;
+
+        // Evidence lookup, not a source/instance-existence or general eligibility check.
+        internal bool TryGetLastConsumedUseBudget(EffectReplayIdentity subject, out int remainingUses)
+        {
+            ArgumentNullException.ThrowIfNull(subject);
+            var accepted = _image._acceptedActivations
+                .Where(value =>
+                    value.Activation.Stamp.ConsumesUse &&
+                    string.Equals(value.Activation.Stamp.Identity.EffectId, subject.EffectId, StringComparison.Ordinal) &&
+                    value.Activation.Stamp.EffectAuthority == subject.Authority)
+                .OrderByDescending(static value => value.Activation.Stamp.ActivationOrdinal)
+                .FirstOrDefault();
+            if (accepted?.Activation.UsesAfter is { } remaining)
+            {
+                remainingUses = remaining;
+                return true;
+            }
+            remainingUses = default;
+            return false;
+        }
+
+        // A positive answer reports exact terminal evidence, not a newly minted effect identity.
+        internal bool HasTerminalAvailabilityEvidence(EffectReplayIdentity subject)
+        {
+            ArgumentNullException.ThrowIfNull(subject);
+            return _image._terminalAvailabilityReservations.Any(value => value.Subject == subject) ||
+                _image._releasedReactions.Any(value =>
+                    ResolveTerminalAvailabilitySubject(
+                        value.Reaction,
+                        new EffectReplayIdentity(
+                            value.Activation.Identity.EffectId,
+                            value.Activation.EffectAuthority)) == subject);
+        }
+    }
+
     internal sealed class Builder
     {
+    private enum ValidationBoundary
+    {
+        Terminal,
+        ClosedPrefix
+    }
+
     private readonly EffectAcceptedPlanAuthorityStamp? _planAuthority;
@@
         _pendingFrontierBoundaryOrdinal = boundary.BoundaryOrdinal;
         return null;
     }

+    private AcceptedEffectBoundaryTranscript CaptureImage() =>
+        new(
+            _planAuthority,
+            _boundaries,
+            _boundaryCloses,
+            _causalClosures,
+            _accepted,
+            _rejected,
+            _applied,
+            _resourceMutations,
+            _released,
+            _terminalReservations,
+            _expansion,
+            _useProjectionOrdinal,
+            _pendingFrontierBoundaryOrdinal);
+
+    internal AcceptedEffectBoundaryPrefixResult CaptureClosedPrefix()
+    {
+        EnsureMutable();
+        if (_useProjectionOrdinal != null || _pendingFrontierBoundaryOrdinal != null)
+        {
+            return new AcceptedEffectBoundaryPrefixResult(
+                null,
+                Array.AsReadOnly(new[]
+                {
+                    new EffectBoundaryTranscriptIssue(
+                        "effect_boundary_prefix_sealed",
+                        "an unsealed closed causal prefix",
+                        _useProjectionOrdinal != null ? "terminal" : "pending")
+                }));
+        }
+        var issues = _issues.Concat(Validate(ValidationBoundary.ClosedPrefix)).ToArray();
+        return issues.Length == 0
+            ? new AcceptedEffectBoundaryPrefixResult(
+                new ClosedPrefix(CaptureImage()),
+                Array.Empty<EffectBoundaryTranscriptIssue>())
+            : new AcceptedEffectBoundaryPrefixResult(null, Array.AsReadOnly(issues));
+    }
+
-    internal AcceptedEffectBoundaryTranscriptResult Freeze()
-    {
-        if (_frozenResult != null)
-            return _frozenResult;
-        var issues = _issues
-            .Concat(Validate())
-            .ToArray();
-        if (issues.Length != 0)
-        {
-            _frozenResult = new AcceptedEffectBoundaryTranscriptResult(
-                null,
-                Array.AsReadOnly(issues));
-            return _frozenResult;
-        }
-        _frozenResult = new AcceptedEffectBoundaryTranscriptResult(
-            new AcceptedEffectBoundaryTranscript(
-                _planAuthority,
-                _boundaries,
-                _boundaryCloses,
-                _causalClosures,
-                _accepted,
-                _rejected,
-                _applied,
-                _resourceMutations,
-                _released,
-                _terminalReservations,
-                _expansion,
-                _useProjectionOrdinal,
-                _pendingFrontierBoundaryOrdinal),
-            Array.Empty<EffectBoundaryTranscriptIssue>());
-        return _frozenResult;
-    }
+    internal AcceptedEffectBoundaryTranscriptResult Freeze()
+    {
+        if (_frozenResult != null)
+            return _frozenResult;
+        var issues = _issues
+            .Concat(Validate(ValidationBoundary.Terminal))
+            .ToArray();
+        if (issues.Length != 0)
+        {
+            _frozenResult = new AcceptedEffectBoundaryTranscriptResult(
+                null,
+                Array.AsReadOnly(issues));
+            return _frozenResult;
+        }
+        _frozenResult = new AcceptedEffectBoundaryTranscriptResult(
+            CaptureImage(),
+            Array.Empty<EffectBoundaryTranscriptIssue>());
+        return _frozenResult;
+    }
@@
-    private IReadOnlyList<EffectBoundaryTranscriptIssue> Validate()
+    private IReadOnlyList<EffectBoundaryTranscriptIssue> Validate(ValidationBoundary validationBoundary)
     {
         var issues = new List<EffectBoundaryTranscriptIssue>();
@@
-        if (pendingFrontier != null)
-        {
-            if (_useProjectionOrdinal != null ||
-                closesByBoundary.ContainsKey(pendingFrontier.Value))
-            {
-                issues.Add(Issue(
-                    "effect_boundary_pending_frontier_invalid",
-                    "one open pending frontier and no use projection",
-                    pendingFrontier.Value.ToString(CultureInfo.InvariantCulture)));
-            }
-        }
-        else if (_useProjectionOrdinal == null)
-        {
-            issues.Add(Issue(
-                "effect_boundary_use_projection_missing",
-                "one terminal use/lifetime projection ordinal",
-                "null"));
-        }
-        if (_useProjectionOrdinal != null || pendingFrontier != null)
-        {
-            var mechanicsOrdinals = _boundaries
-                .Select(static value => value.OpenMechanicsOrdinal)
-                .Concat(_boundaryCloses.Select(static value => value.MechanicsOrdinal))
-                .Concat(_accepted.Select(static value => value.MechanicsOrdinal))
-                .Concat(_terminalReservations.Select(
-                    static value => value.MechanicsOrdinal))
-                .Concat(_resourceMutations.Select(static value => value.MechanicsOrdinal))
-                .Concat(_released.Select(static value => value.MechanicsOrdinal))
-                .Concat(_useProjectionOrdinal is { } projectionOrdinal
-                    ? new[] { projectionOrdinal }
-                    : Array.Empty<long>())
-                .OrderBy(static value => value)
-                .ToArray();
-            if (mechanicsOrdinals.Distinct().Count() != mechanicsOrdinals.Length ||
-                mechanicsOrdinals.Where((value, index) => value != index).Any() ||
-                _useProjectionOrdinal != null &&
-                mechanicsOrdinals[^1] != _useProjectionOrdinal.Value)
-            {
-                issues.Add(Issue(
-                    "effect_boundary_mechanics_order_invalid",
-                    "zero-based contiguous unique authority ordinals ending in use projection",
-                    string.Join(",", mechanicsOrdinals)));
-            }
-        }
-        return issues;
+        if (pendingFrontier != null)
+        {
+            if (_useProjectionOrdinal != null ||
+                closesByBoundary.ContainsKey(pendingFrontier.Value))
+            {
+                issues.Add(Issue(
+                    "effect_boundary_pending_frontier_invalid",
+                    "one open pending frontier and no use projection",
+                    pendingFrontier.Value.ToString(CultureInfo.InvariantCulture)));
+            }
+        }
+        else if (_useProjectionOrdinal == null &&
+                 validationBoundary == ValidationBoundary.Terminal)
+        {
+            issues.Add(Issue(
+                "effect_boundary_use_projection_missing",
+                "one terminal use/lifetime projection ordinal",
+                "null"));
+        }
+        if (_useProjectionOrdinal != null || pendingFrontier != null ||
+            validationBoundary == ValidationBoundary.ClosedPrefix)
+        {
+            var mechanicsOrdinals = _boundaries
+                .Select(static value => value.OpenMechanicsOrdinal)
+                .Concat(_boundaryCloses.Select(static value => value.MechanicsOrdinal))
+                .Concat(_accepted.Select(static value => value.MechanicsOrdinal))
+                .Concat(_terminalReservations.Select(
+                    static value => value.MechanicsOrdinal))
+                .Concat(_resourceMutations.Select(static value => value.MechanicsOrdinal))
+                .Concat(_released.Select(static value => value.MechanicsOrdinal))
+                .Concat(_useProjectionOrdinal is { } projectionOrdinal
+                    ? new[] { projectionOrdinal }
+                    : Array.Empty<long>())
+                .OrderBy(static value => value)
+                .ToArray();
+            if (mechanicsOrdinals.Distinct().Count() != mechanicsOrdinals.Length ||
+                mechanicsOrdinals.Where((value, index) => value != index).Any() ||
+                _useProjectionOrdinal != null &&
+                mechanicsOrdinals[^1] != _useProjectionOrdinal.Value)
+            {
+                issues.Add(Issue(
+                    "effect_boundary_mechanics_order_invalid",
+                    "zero-based contiguous unique authority ordinals ending in use projection",
+                    string.Join(",", mechanicsOrdinals)));
+            }
+            if (validationBoundary == ValidationBoundary.ClosedPrefix &&
+                mechanicsOrdinals.LongLength != _nextMechanicsOrdinal)
+            {
+                issues.Add(Issue(
+                    "effect_boundary_prefix_frontier_invalid",
+                    "one observed mechanics ordinal for every allocated prefix ordinal",
+                    "observed=" + mechanicsOrdinals.LongLength.ToString(CultureInfo.InvariantCulture) +
+                    ";next=" + _nextMechanicsOrdinal.ToString(CultureInfo.InvariantCulture)));
+            }
+        }
+        return issues;
     }
*** Update File: BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs
@@
         internal ResourceClosedBoundaryCheckpoint(
             ResourceOperationKey lastCompletedOperation,
             int nextExecutionSequence,
             ResourceDefinitionCatalog definitions,
             ResourceStateLedger state,
@@
             IReadOnlyList<ResourceTransition> replayTransitions,
             IReadOnlyList<ResourceAppliedEvent> events,
             IEnumerable<long> closedEffectBoundaryOrdinals,
-            AcceptedMechanicsPlannerStatistics statistics)
+            AcceptedMechanicsPlannerStatistics statistics,
+            AcceptedEffectBoundaryTranscript.ClosedPrefix effectPrefix)
         {
             LastCompletedOperation = lastCompletedOperation;
@@
             _events = events.ToArray();
             _closedEffectBoundaryOrdinals = closedEffectBoundaryOrdinals.OrderBy(value => value).ToArray();
             Statistics = statistics;
+            EffectPrefix = effectPrefix;
         }
@@
         internal IReadOnlyList<long> ClosedEffectBoundaryOrdinals =>
             Array.AsReadOnly(_closedEffectBoundaryOrdinals.ToArray());
         internal AcceptedMechanicsPlannerStatistics Statistics { get; }
+        internal AcceptedEffectBoundaryTranscript.ClosedPrefix EffectPrefix { get; }
     }
@@
             if (session.StopAtClosedBoundary &&
                 completedAtBoundary != null &&
                 unresolvedPendingBoundaries.Count == 0 &&
                 parentBoundaryByOrdinal.Keys.All(closedBoundaries.Contains))
             {
+                var prefix = effectTranscriptBuilder.CaptureClosedPrefix();
+                if (!prefix.IsValid || prefix.Prefix == null)
+                {
+                    yield return ResourceExecutionStep.Finished(Failure(
+                        prefix.Issues.SelectMany(issue => Issue(
+                            issue.Code, issue.Expected, issue.Actual)).ToArray(),
+                        Statistics(workingHistory, metrics)));
+                    yield break;
+                }
                 var checkpoint = new ResourceClosedBoundaryCheckpoint(
                     completedAtBoundary, executionSequence, input.Definitions,
                     workingLedger.Freeze(), input.History, workingHistory.PendingTransitions,
                     appliedTransitions, replayTransitions, events, closedBoundaries,
-                    Statistics(workingHistory, metrics));
+                    Statistics(workingHistory, metrics), prefix.Prefix);
                 completedAtBoundary = null;
                 yield return ResourceExecutionStep.Paused(checkpoint);
             }
*** Update File: BookOfEternityClient.Tests/AcceptedEffectBoundaryTranscriptTests.cs
@@
-public sealed class AcceptedEffectBoundaryTranscriptTests
+public sealed partial class AcceptedEffectBoundaryTranscriptTests
 {
*** Add File: BookOfEternityClient.Tests/AcceptedEffectBoundaryTranscriptTests.ClosedPrefix.cs
+using System.Reflection;
+using BookOfEternityClient.Services;
+using Xunit;
+
+namespace BookOfEternityClient.Tests;
+
+public sealed partial class AcceptedEffectBoundaryTranscriptTests
+{
+    [Fact]
+    public void ClosedPrefix_ContractRedHasExistingTerminalPositiveControl()
+    {
+        var control = new AcceptedEffectBoundaryTranscript.Builder();
+        PrefixEmptyBoundary(control, "positive");
+        control.SealUseProjection();
+        var terminal = control.Freeze();
+        Assert.True(terminal.IsValid, Format(terminal.Issues));
+        Assert.Single(terminal.Transcript!.Boundaries);
+
+        var builder = new AcceptedEffectBoundaryTranscript.Builder();
+        PrefixEmptyBoundary(builder, "positive");
+        var capture = typeof(AcceptedEffectBoundaryTranscript.Builder).GetMethod(
+            "CaptureClosedPrefix", BindingFlags.Instance | BindingFlags.NonPublic);
+        Assert.NotNull(capture); // semantic RED only after the real old terminal path succeeds
+        var captured = capture!.Invoke(builder, null)!;
+        var prefix = captured.GetType().GetProperty(
+            "Prefix", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
+        Assert.NotNull(prefix);
+        Assert.NotNull(prefix!.GetValue(captured));
+        builder.SealUseProjection();
+        var after = builder.Freeze();
+        Assert.True(after.IsValid, Format(after.Issues));
+        Assert.Equal(terminal.Transcript.Fingerprint, after.Transcript!.Fingerprint);
+    }
+
+    [Fact]
+    public void ClosedPrefix_IsDetachedAndRepeatedCaptureDoesNotAdvanceOrSeal()
+    {
+        var builder = new AcceptedEffectBoundaryTranscript.Builder();
+        PrefixEmptyBoundary(builder, "first");
+        var first = builder.CaptureClosedPrefix();
+        Assert.True(first.IsValid, Format(first.Issues));
+        var old = first.Prefix!;
+        Assert.Single(old.Boundaries);
+        Assert.Single(old.BoundaryCloses);
+        var fingerprint = old.Fingerprint;
+        Assert.Equal(fingerprint, builder.CaptureClosedPrefix().Prefix!.Fingerprint);
+
+        PrefixEmptyBoundary(builder, "second");
+        var second = builder.CaptureClosedPrefix();
+        Assert.True(second.IsValid, Format(second.Issues));
+        Assert.Equal(2, second.Prefix!.Boundaries.Count);
+        Assert.Single(old.Boundaries);
+        Assert.Equal(fingerprint, old.Fingerprint);
+        builder.SealUseProjection();
+        var completed = builder.Freeze();
+        Assert.True(completed.IsValid, Format(completed.Issues));
+        Assert.True(completed.Transcript!.IsComplete);
+        Assert.NotEqual(second.Prefix.Fingerprint, completed.Transcript.Fingerprint);
+        Assert.Throws<InvalidOperationException>(() => builder.CaptureClosedPrefix());
+    }
+
+    [Fact]
+    public void ClosedPrefix_RejectsOpenBoundaryWithoutPermanentlySealing()
+    {
+        var builder = new AcceptedEffectBoundaryTranscript.Builder();
+        var boundary = builder.OpenBoundary(
+            null, null, "owner_turn_end", "turn_2:prefix:open",
+            null, null, -1,
+            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>());
+        Assert.Null(BindEmpty(builder, boundary));
+        var invalid = builder.CaptureClosedPrefix();
+        Assert.False(invalid.IsValid);
+        Assert.Null(invalid.Prefix);
+        Assert.Contains(invalid.Issues, issue => issue.Code == "effect_boundary_close_missing");
+        Assert.Null(builder.CloseBoundary(boundary));
+        Assert.True(builder.CaptureClosedPrefix().IsValid);
+        builder.SealUseProjection();
+        Assert.True(builder.Freeze().IsValid);
+    }
+
+    [Fact]
+    public void ClosedPrefix_RejectsOrdinalGapWithoutFilteringTerminalErrors()
+    {
+        var builder = new AcceptedEffectBoundaryTranscript.Builder();
+        builder.RecordMutationExecution(); // no corresponding mutation evidence: genuine ordinal gap
+        PrefixEmptyBoundary(builder, "gap");
+        var result = builder.CaptureClosedPrefix();
+        Assert.False(result.IsValid);
+        Assert.Null(result.Prefix);
+        Assert.Contains(result.Issues, issue => issue.Code == "effect_boundary_mechanics_order_invalid");
+        Assert.DoesNotContain(result.Issues, issue => issue.Code == "effect_boundary_use_projection_missing");
+    }
+
+    [Fact]
+    public void ClosedPrefix_RejectsTrailingOrphanAgainstActualMechanicsFrontier()
+    {
+        var builder = new AcceptedEffectBoundaryTranscript.Builder();
+        PrefixEmptyBoundary(builder, "trailing_orphan");
+        Assert.True(builder.CaptureClosedPrefix().IsValid);
+        Assert.Equal(2L, builder.RecordMutationExecution()); // advances next to 3, no evidence for ordinal 2
+        var result = builder.CaptureClosedPrefix();
+        Assert.False(result.IsValid);
+        Assert.Null(result.Prefix);
+        var issue = Assert.Single(result.Issues);
+        Assert.Equal("effect_boundary_prefix_frontier_invalid", issue.Code);
+        Assert.Equal("observed=2;next=3", issue.Actual);
+        // Observed [0,1] is contiguous: this specifically tests the missing trailing evidence.
+        Assert.DoesNotContain(result.Issues, value => value.Code == "effect_boundary_mechanics_order_invalid");
+        builder.SealUseProjection();
+        var final = builder.Freeze();
+        Assert.False(final.IsValid);
+        Assert.Contains(final.Issues, value => value.Code == "effect_boundary_mechanics_order_invalid");
+        Assert.DoesNotContain(final.Issues, value => value.Code == "effect_boundary_prefix_frontier_invalid");
+    }
+
+    [Fact]
+    public void ClosedPrefix_TerminalRequirementIsUnchangedAndSealedCaptureIsRejected()
+    {
+        var unsealed = new AcceptedEffectBoundaryTranscript.Builder();
+        PrefixEmptyBoundary(unsealed, "missing");
+        Assert.True(unsealed.CaptureClosedPrefix().IsValid);
+        var invalidFinal = unsealed.Freeze();
+        Assert.False(invalidFinal.IsValid);
+        Assert.Contains(invalidFinal.Issues, issue => issue.Code == "effect_boundary_use_projection_missing");
+
+        var sealedBuilder = new AcceptedEffectBoundaryTranscript.Builder();
+        PrefixEmptyBoundary(sealedBuilder, "sealed");
+        sealedBuilder.SealUseProjection();
+        var invalidPrefix = sealedBuilder.CaptureClosedPrefix();
+        Assert.False(invalidPrefix.IsValid);
+        Assert.Contains(invalidPrefix.Issues, issue => issue.Code == "effect_boundary_prefix_sealed");
+        Assert.True(sealedBuilder.Freeze().IsValid);
+    }
+
+    [Fact]
+    public void ClosedPrefix_RealArbitrationReportsExactLastUseWithoutChangingArbiter()
+    {
+        var identity = Identity("prefix_last_use");
+        var authority = new ResourcePendingAuthorityBinding("permanent", identity.EffectId);
+        var seed = new CanonicalEffectUseSeed(identity.EffectId, 1);
+        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
+            new EffectActivationCandidate(identity, 100, true, authority),
+            seed, null, Array.Empty<ResourceOperationKey>(), Array.Empty<string>(),
+            new Dictionary<ResourceOperationKey, string>(),
+            Array.Empty<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>(),
+            Array.Empty<EffectReactionExecution>(),
+            EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty,
+            effectAuthority: authority);
+        var initialized = AcceptedEffectUseArbiter.Initialize(new[] { seed });
+        Assert.True(initialized.IsValid);
+        var arbiter = initialized.Arbiter!;
+        var arbitration = arbiter.Arbitrate(new[] { candidate.Activation });
+        Assert.True(arbitration.IsValid);
+        var activation = Assert.Single(arbitration.AcceptedActivations);
+        var builder = new AcceptedEffectBoundaryTranscript.Builder();
+        var boundary = builder.OpenBoundary(null, null, identity.EventKind,
+            identity.TriggerEventRef, null, null, -1, new[] { candidate });
+        builder.RecordAccepted(boundary, activation, candidate);
+        Assert.Null(builder.ReserveTerminalAvailability(boundary, activation, candidate));
+        Assert.Null(BindEmpty(builder, boundary));
+        Assert.Null(builder.CloseBoundary(boundary));
+
+        var captured = builder.CaptureClosedPrefix();
+        Assert.True(captured.IsValid, Format(captured.Issues));
+        var prefix = captured.Prefix!;
+        var exact = new EffectReplayIdentity(identity.EffectId, authority);
+        Assert.True(prefix.TryGetLastConsumedUseBudget(exact, out var remaining));
+        Assert.Equal(0, remaining);
+        Assert.True(prefix.HasTerminalAvailabilityEvidence(exact));
+        var foreign = new EffectReplayIdentity(identity.EffectId,
+            new ResourcePendingAuthorityBinding("accepted_application", "turn_2:foreign_application"));
+        Assert.False(prefix.TryGetLastConsumedUseBudget(foreign, out _));
+        Assert.False(prefix.HasTerminalAvailabilityEvidence(foreign));
+        Assert.True(arbiter.TryGetRemainingUses(identity.EffectId, out var live));
+        Assert.Equal(0, live);
+        Assert.Equal(prefix.Fingerprint, builder.CaptureClosedPrefix().Prefix!.Fingerprint);
+        Assert.Single(arbiter.AcceptedTranscript);
+        builder.SealUseProjection();
+        Assert.True(builder.Freeze().IsValid);
+    }
+
+    [Fact]
+    public void ClosedPrefix_ContainsRealReleasedReactionAndPreservesTerminalAuthority()
+    {
+        var builder = new AcceptedEffectBoundaryTranscript.Builder();
+        const string effectId = "effect_prefix_removed";
+        RecordTerminalRelease(builder, effectId, "prefix_removed", 0);
+        var captured = builder.CaptureClosedPrefix();
+        Assert.True(captured.IsValid, Format(captured.Issues));
+        var prefix = captured.Prefix!;
+        Assert.Single(prefix.ReleasedReactions);
+        Assert.Single(prefix.ExpansionUsage);
+        Assert.True(prefix.HasTerminalAvailabilityEvidence(new EffectReplayIdentity(
+            effectId, new ResourcePendingAuthorityBinding("permanent", effectId))));
+        Assert.False(prefix.HasTerminalAvailabilityEvidence(new EffectReplayIdentity(
+            effectId, new ResourcePendingAuthorityBinding("accepted_application", "turn_2:other_root"))));
+        Assert.False(prefix.TryGetLastConsumedUseBudget(new EffectReplayIdentity(
+            effectId, new ResourcePendingAuthorityBinding("permanent", effectId)), out _));
+        builder.SealUseProjection();
+        var final = builder.Freeze();
+        Assert.True(final.IsValid, Format(final.Issues));
+        Assert.Single(final.Transcript!.ReleasedReactions);
+        Assert.Equal(prefix.ExpansionUsage.Single().Value, final.Transcript.ExpansionUsage.Single().Value);
+    }
+
+    private static void PrefixEmptyBoundary(
+        AcceptedEffectBoundaryTranscript.Builder builder, string suffix)
+    {
+        var boundary = builder.OpenBoundary(
+            null, null, "owner_turn_end", "turn_2:prefix:" + suffix,
+            null, null, -1,
+            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>());
+        Assert.Null(BindEmpty(builder, boundary));
+        Assert.Null(builder.CloseBoundary(boundary));
+    }
+}
*** Update File: BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.ResourceExecutionSession.cs
@@
 public sealed partial class AcceptedMechanicsPlannerTests
 {
+    [Fact]
+    public void ResourceSession_ClosedPrefixIsConsumedWithoutChangingFinalResultOrAllocations()
+    {
+        var input = SessionPeriodicInput(bounded: false);
+        var expectedCounter = new SessionAllocationCounter();
+        var expected = AcceptedMechanicsPlanner.BuildResources(input, expectedCounter.Factory);
+        Assert.True(expected.IsValid, SessionIssues(expected));
+        Assert.Equal(3, expected.AppliedTransitions.Count);
+
+        var counter = new SessionAllocationCounter();
+        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
+        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(
+            session.AdvanceToClosedBoundary().Checkpoint);
+        var prefix = checkpoint.EffectPrefix;
+        Assert.Single(prefix.AcceptedActivations);
+        Assert.Equal(2, prefix.AppliedComponentEvidence.Count);
+        Assert.Equal(2, prefix.ResourceMutations.Count);
+        Assert.Equal(checkpoint.ClosedEffectBoundaryOrdinals,
+            prefix.BoundaryCloses.Select(value => value.Boundary.BoundaryOrdinal).OrderBy(value => value));
+        var before = prefix.Fingerprint;
+        var originalMutationIds = prefix.ResourceMutations.Select(value => value.Transition.TransitionId).ToArray();
+        var calls = counter.Calls;
+        var actual = session.Drain();
+        Assert.True(actual.IsValid, SessionIssues(actual));
+        Assert.Equal(SessionResultImage(expected), SessionResultImage(actual));
+        Assert.Equal(expectedCounter.Ids, counter.Ids);
+        Assert.Equal(calls, counter.Calls);
+        Assert.Equal(before, prefix.Fingerprint);
+        Assert.Equal(originalMutationIds, prefix.ResourceMutations.Select(value => value.Transition.TransitionId));
+        Assert.Equal(2, prefix.ResourceMutations.Count);
+        Assert.Equal(3, actual.EffectBoundaryTranscript.ResourceMutations.Count);
+    }
+
     [Fact]
     public void ResourceSession_ContractRedHasNonemptyProductionPositiveControl()
*** End Patch
```

## Accepted execution — 2026-09-08

T081-B2B is complete at `02fafbdf9cc57101f3c9655c90afc519ad12c2c3`, recorded
review base `3e4518cb5ed9b0534d1d62b2d846fae4d8887156`. Independent task review
is Spec compliant / Quality Approved, zero Critical/Important/Minor findings.
Parent read the complete report and reconstructed all five expected postimages
from the companion: four are exact; the transcript only wraps the Validate
parameter onto a second line. No existing assertion or corrected B2A helper changed.

Actual artifacts below are relative to `TestResults/test-lanes/`:

| Control | Result | Wall time | Artifact |
|---|---|---|---|
| Old terminal positive then reflection RED |0/1|1:08.8167407|`20260908-060841-898-44648-cca25cfbf0ea4201b1cb4c68f0e2407b-focused`|
| Transcript/arbiter/session |45/45|1:09.4421773|`20260908-061218-403-45508-76b102a1dd934cdda8bea26ad793d9cf-focused`|
| Accepted effect planner |109/109|0:16.2685881|`20260908-061333-228-20812-52f948db89ba4838ad4d4a75ad24a30f-focused`|
| Integration pending waves |5/5|1:29.5636335|`20260908-061353-815-13104-3d0bd76e92394d2dbe083898be46bb4c-focused`|
| Parent Fast |7776/7776|4:59.1412643|`20260908-143347-839-21648-4521cf2b487841adb63f6c0eff95649c-fast`|

Parent inspected all summaries/logs/TRXs. Every GREEN control used the default5m
bound, finished exit0/TimedOut=false, build warnings/errors0/0, no skipped or
duplicate executions, and complete owned-tree cleanup. All26FastTRXs contain
7776 distinct executions,7722 unique method IDs and no cross-descriptor duplicates;
all8new builder Facts and10session Facts are present. Existing dynamic theories
account for54extra rows. The review's non-diff-verifiable execution/cleanup items
are resolved by actual artifacts; the code commit contains only the five scoped
files, and unrelated .serena metadata was not staged or manually edited.

Only nested B2B is accepted. Top-level79/177 and fullT081/B2/C/D/E/T084/T085/T177
remain open. This unsealed view does not implement source admission, actual effect
materialization during continuation or live wound insertion. No GM-facing contract
changed, so Mortal/afterlife prompts/examples/matrix/manifests need no update here.
No remote publication, issue closure, branch/worktree or session cleanup occurred.
