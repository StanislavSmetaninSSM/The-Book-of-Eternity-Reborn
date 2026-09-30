using System.Reflection;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedEffectBoundaryTranscriptTests
{
    [Fact]
    public void ClosedPrefix_ContractRedHasExistingTerminalPositiveControl()
    {
        var control = new AcceptedEffectBoundaryTranscript.Builder();
        PrefixEmptyBoundary(control, "positive");
        control.SealUseProjection();
        var terminal = control.Freeze();
        Assert.True(terminal.IsValid, Format(terminal.Issues));
        Assert.Single(terminal.Transcript!.Boundaries);

        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        PrefixEmptyBoundary(builder, "positive");
        var capture = typeof(AcceptedEffectBoundaryTranscript.Builder).GetMethod(
            "CaptureClosedPrefix", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(capture); // semantic RED only after the real old terminal path succeeds
        var captured = capture!.Invoke(builder, null)!;
        var prefix = captured.GetType().GetProperty(
            "Prefix", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(prefix);
        Assert.NotNull(prefix!.GetValue(captured));
        builder.SealUseProjection();
        var after = builder.Freeze();
        Assert.True(after.IsValid, Format(after.Issues));
        Assert.Equal(terminal.Transcript.Fingerprint, after.Transcript!.Fingerprint);
    }

    [Fact]
    public void ClosedPrefix_IsDetachedAndRepeatedCaptureDoesNotAdvanceOrSeal()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        PrefixEmptyBoundary(builder, "first");
        var first = builder.CaptureClosedPrefix();
        Assert.True(first.IsValid, Format(first.Issues));
        var old = first.Prefix!;
        Assert.Single(old.Boundaries);
        Assert.Single(old.BoundaryCloses);
        var fingerprint = old.Fingerprint;
        Assert.Equal(fingerprint, builder.CaptureClosedPrefix().Prefix!.Fingerprint);

        PrefixEmptyBoundary(builder, "second");
        var second = builder.CaptureClosedPrefix();
        Assert.True(second.IsValid, Format(second.Issues));
        Assert.Equal(2, second.Prefix!.Boundaries.Count);
        Assert.Single(old.Boundaries);
        Assert.Equal(fingerprint, old.Fingerprint);
        builder.SealUseProjection();
        var completed = builder.Freeze();
        Assert.True(completed.IsValid, Format(completed.Issues));
        Assert.True(completed.Transcript!.IsComplete);
        Assert.NotEqual(second.Prefix.Fingerprint, completed.Transcript.Fingerprint);
        Assert.Throws<InvalidOperationException>(() => builder.CaptureClosedPrefix());
    }

    [Fact]
    public void ClosedPrefix_RejectsOpenBoundaryWithoutPermanentlySealing()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var boundary = builder.OpenBoundary(
            null, null, "owner_turn_end", "turn_2:prefix:open",
            null, null, -1,
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>());
        Assert.Null(BindEmpty(builder, boundary));
        var invalid = builder.CaptureClosedPrefix();
        Assert.False(invalid.IsValid);
        Assert.Null(invalid.Prefix);
        Assert.Contains(invalid.Issues, issue => issue.Code == "effect_boundary_close_missing");
        Assert.Null(builder.CloseBoundary(boundary));
        Assert.True(builder.CaptureClosedPrefix().IsValid);
        builder.SealUseProjection();
        Assert.True(builder.Freeze().IsValid);
    }

    [Fact]
    public void ClosedPrefix_RejectsOrdinalGapWithoutFilteringTerminalErrors()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        builder.RecordMutationExecution(); // no corresponding mutation evidence: genuine ordinal gap
        PrefixEmptyBoundary(builder, "gap");
        var result = builder.CaptureClosedPrefix();
        Assert.False(result.IsValid);
        Assert.Null(result.Prefix);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_boundary_mechanics_order_invalid");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "effect_boundary_use_projection_missing");
    }

    [Fact]
    public void ClosedPrefix_RejectsTrailingOrphanAgainstActualMechanicsFrontier()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        PrefixEmptyBoundary(builder, "trailing_orphan");
        Assert.True(builder.CaptureClosedPrefix().IsValid);
        Assert.Equal(2L, builder.RecordMutationExecution()); // advances next to 3, no evidence for ordinal 2
        var result = builder.CaptureClosedPrefix();
        Assert.False(result.IsValid);
        Assert.Null(result.Prefix);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("effect_boundary_prefix_frontier_invalid", issue.Code);
        Assert.Equal("observed=2;next=3", issue.Actual);
        // Observed [0,1] is contiguous: this specifically tests the missing trailing evidence.
        Assert.DoesNotContain(result.Issues, value => value.Code == "effect_boundary_mechanics_order_invalid");
        builder.SealUseProjection();
        var final = builder.Freeze();
        Assert.False(final.IsValid);
        Assert.Contains(final.Issues, value => value.Code == "effect_boundary_mechanics_order_invalid");
        Assert.DoesNotContain(final.Issues, value => value.Code == "effect_boundary_prefix_frontier_invalid");
    }

    [Fact]
    public void ClosedPrefix_TerminalRequirementIsUnchangedAndSealedCaptureIsRejected()
    {
        var unsealed = new AcceptedEffectBoundaryTranscript.Builder();
        PrefixEmptyBoundary(unsealed, "missing");
        Assert.True(unsealed.CaptureClosedPrefix().IsValid);
        var invalidFinal = unsealed.Freeze();
        Assert.False(invalidFinal.IsValid);
        Assert.Contains(invalidFinal.Issues, issue => issue.Code == "effect_boundary_use_projection_missing");

        var sealedBuilder = new AcceptedEffectBoundaryTranscript.Builder();
        PrefixEmptyBoundary(sealedBuilder, "sealed");
        sealedBuilder.SealUseProjection();
        var invalidPrefix = sealedBuilder.CaptureClosedPrefix();
        Assert.False(invalidPrefix.IsValid);
        Assert.Contains(invalidPrefix.Issues, issue => issue.Code == "effect_boundary_prefix_sealed");
        Assert.True(sealedBuilder.Freeze().IsValid);
    }

    [Fact]
    public void ClosedPrefix_RealArbitrationReportsExactLastUseWithoutChangingArbiter()
    {
        var identity = Identity("prefix_last_use");
        var authority = new ResourcePendingAuthorityBinding("permanent", identity.EffectId);
        var seed = new CanonicalEffectUseSeed(identity.EffectId, 1);
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(identity, 100, true, authority),
            seed, null, Array.Empty<ResourceOperationKey>(), Array.Empty<string>(),
            new Dictionary<ResourceOperationKey, string>(),
            Array.Empty<EffectAcceptedTurnPlanner.EffectBoundedResourceResolution>(),
            Array.Empty<EffectReactionExecution>(),
            EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty,
            effectAuthority: authority);
        var initialized = AcceptedEffectUseArbiter.Initialize(new[] { seed });
        Assert.True(initialized.IsValid);
        var arbiter = initialized.Arbiter!;
        var arbitration = arbiter.Arbitrate(new[] { candidate.Activation });
        Assert.True(arbitration.IsValid);
        var activation = Assert.Single(arbitration.AcceptedActivations);
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        var boundary = builder.OpenBoundary(null, null, identity.EventKind,
            identity.TriggerEventRef, null, null, -1, new[] { candidate });
        builder.RecordAccepted(boundary, activation, candidate);
        Assert.Null(builder.ReserveTerminalAvailability(boundary, activation, candidate));
        Assert.Null(BindEmpty(builder, boundary));
        Assert.Null(builder.CloseBoundary(boundary));

        var captured = builder.CaptureClosedPrefix();
        Assert.True(captured.IsValid, Format(captured.Issues));
        var prefix = captured.Prefix!;
        var exact = new EffectReplayIdentity(identity.EffectId, authority);
        Assert.True(prefix.TryGetLastConsumedUseBudget(exact, out var remaining));
        Assert.Equal(0, remaining);
        Assert.True(prefix.HasTerminalAvailabilityEvidence(exact));
        var foreign = new EffectReplayIdentity(identity.EffectId,
            new ResourcePendingAuthorityBinding("accepted_application", "turn_2:foreign_application"));
        Assert.False(prefix.TryGetLastConsumedUseBudget(foreign, out _));
        Assert.False(prefix.HasTerminalAvailabilityEvidence(foreign));
        Assert.True(arbiter.TryGetRemainingUses(identity.EffectId, out var live));
        Assert.Equal(0, live);
        Assert.Equal(prefix.Fingerprint, builder.CaptureClosedPrefix().Prefix!.Fingerprint);
        Assert.Single(arbiter.AcceptedTranscript);
        builder.SealUseProjection();
        Assert.True(builder.Freeze().IsValid);
    }

    [Fact]
    public void ClosedPrefix_ContainsRealReleasedReactionAndPreservesTerminalAuthority()
    {
        var builder = new AcceptedEffectBoundaryTranscript.Builder();
        const string effectId = "effect_prefix_removed";
        RecordTerminalRelease(builder, effectId, "prefix_removed", 0);
        var captured = builder.CaptureClosedPrefix();
        Assert.True(captured.IsValid, Format(captured.Issues));
        var prefix = captured.Prefix!;
        Assert.Single(prefix.ReleasedReactions);
        Assert.Single(prefix.ExpansionUsage);
        Assert.True(prefix.HasTerminalAvailabilityEvidence(new EffectReplayIdentity(
            effectId, new ResourcePendingAuthorityBinding("permanent", effectId))));
        Assert.False(prefix.HasTerminalAvailabilityEvidence(new EffectReplayIdentity(
            effectId, new ResourcePendingAuthorityBinding("accepted_application", "turn_2:other_root"))));
        Assert.False(prefix.TryGetLastConsumedUseBudget(new EffectReplayIdentity(
            effectId, new ResourcePendingAuthorityBinding("permanent", effectId)), out _));
        builder.SealUseProjection();
        var final = builder.Freeze();
        Assert.True(final.IsValid, Format(final.Issues));
        Assert.Single(final.Transcript!.ReleasedReactions);
        Assert.Equal(prefix.ExpansionUsage.Single().Value, final.Transcript.ExpansionUsage.Single().Value);
    }

    private static void PrefixEmptyBoundary(
        AcceptedEffectBoundaryTranscript.Builder builder, string suffix)
    {
        var boundary = builder.OpenBoundary(
            null, null, "owner_turn_end", "turn_2:prefix:" + suffix,
            null, null, -1,
            Array.Empty<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>());
        Assert.Null(BindEmpty(builder, boundary));
        Assert.Null(builder.CloseBoundary(boundary));
    }
}
