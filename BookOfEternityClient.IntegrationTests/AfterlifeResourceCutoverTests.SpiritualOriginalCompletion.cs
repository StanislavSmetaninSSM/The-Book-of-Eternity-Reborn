using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Verifies that the spiritual completion owner cannot drain a positive wound source
    /// until its exact decision is retained, then completes and retries the same result once.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCompletion_RequiresEveryPositiveSourceDecisionBeforeDrain()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(
            lease, advanced.Step!.Interval!, Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);

        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(
            OriginalCaptureField(capture, "_effects"));
        var phaseCount = draft.Phases.Count;
        Assert.Empty(await sourceOwner.CheckCompletionInputsAsync(
            lease, sourceOwner.CheckedExchanges.Count, resources, draft));
        Assert.Contains(await sourceOwner.CheckCompletionInputsAsync(
            lease, sourceOwner.CheckedExchanges.Count + 1, resources, draft),
            issue => issue.Code == "spiritual_source_exchange_incomplete");
        var incomplete = await capture.CompleteEffectsAsync(lease);
        Assert.Null(incomplete.Plan);
        Assert.Contains(incomplete.Issues,
            issue => issue.Code == "spiritual_wound_decision_required");
        Assert.Null(resources.Result);
        var originalInput = Assert.IsType<AcceptedMechanicsInput>(
            OriginalCaptureField(capture, "_input"));
        var preparedSource = await sourceOwner.PrepareContinuationAsync(lease);
        var sourceCompletion = preparedSource.Ticket!.ValidateCompletion(
            sourceOwner,
            lease,
            sourceOwner.CheckedExchanges.Count,
            resources,
            draft);
        Assert.True(sourceCompletion.Success,
            string.Join(Environment.NewLine, sourceCompletion.Issues));
        var prematureReduction = resources.SealOriginalSpiritualReduction(
            originalInput.PlanningContext!.EffectPlan!,
            sourceCompletion.Projection!);
        Assert.False(prematureReduction.Success);
        Assert.Contains(prematureReduction.Issues,
            issue => issue.Code == "spiritual_original_reduction_incomplete");
        Assert.Equal(phaseCount, draft.Phases.Count);
        Assert.Null(draft.GetType().GetField("identityRoot",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(draft));
        Assert.True(capture.IsCurrentOwner);

        var decline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef,
            decision = "none"
        });
        var declined = await capture.MaterializeWoundAsync(
            lease, admitted.Admission!, decline, null);
        AssertNoConflictFrameErrors(declined.Issues);
        Assert.Null(declined.Wound);
        var canonicalConflict = await context.FileSystem.ReadFileBytesAsync(
            lease, AfterlifeSpiritualConflictState.StatePath);
        var completed = await capture.CompleteEffectsAsync(lease);
        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
        Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
        Assert.NotNull(resources.Result);
        using var foreignDraft = EffectAcceptedTurnPlanner.EffectAcceptedDraft.Begin(
            originalInput.PlanningContext!.EffectPlan!,
            new EffectIdentityFactory());
        Assert.Contains(await sourceOwner.CheckCompletionInputsAsync(
            lease, sourceOwner.CheckedExchanges.Count, resources, foreignDraft),
            issue => issue.Code == "spiritual_original_completion_owner_mismatch");
        var foreignCompletion = foreignDraft.Complete(
            resources.Result!.EffectBoundaryTranscript!);
        Assert.True(foreignCompletion.Success,
            string.Join(Environment.NewLine, foreignCompletion.Issues));
        var foreignPreparedSource = await sourceOwner.PrepareContinuationAsync(lease);
        var foreignSourceCompletion = foreignPreparedSource.Ticket!.ValidateCompletion(
            sourceOwner,
            lease,
            sourceOwner.CheckedExchanges.Count,
            resources,
            draft);
        Assert.True(foreignSourceCompletion.Success,
            string.Join(Environment.NewLine, foreignSourceCompletion.Issues));
        var foreignConsumptionIssues = foreignSourceCompletion.Projection!.Consume(
            sourceOwner,
            lease,
            sourceOwner.CheckedExchanges.Count,
            resources,
            draft,
            foreignCompletion.Plan!);
        Assert.Contains(foreignConsumptionIssues,
            issue => issue.Code == "spiritual_original_completion_owner_mismatch");
        var foreignReduction = resources.SealOriginalSpiritualReduction(
            foreignCompletion.Plan!,
            foreignSourceCompletion.Projection!);
        Assert.False(foreignReduction.Success);
        Assert.Contains(foreignReduction.Issues,
            issue => issue.Code == "spiritual_original_reduction_effect_mismatch");
        var reduced = await capture.CompleteOrdinaryReductionAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        Assert.Same(reduced, await capture.CompleteOrdinaryReductionAsync(lease));
        var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        Assert.Same(resources.Result, reduction.Resources);
        Assert.Same(completed.Plan, reduction.Effects);
        var projectedConflict = Assert.IsType<JsonObject>(
            reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
        Assert.True(JsonNode.DeepEquals(
            originalInput.PlanningContext!.OwnerCompanionAfterImages[
                AfterlifeSpiritualConflictState.StatePath],
            projectedConflict));
        Assert.Equal(canonicalConflict, await context.FileSystem.ReadFileBytesAsync(
            lease, AfterlifeSpiritualConflictState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Verifies that spiritual completion consumes the installed wound routing image and
    /// retains the exact inserted root and source without publishing canonical files.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCompletion_CompletesInsertedRootThroughOwnedRouting()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(
            OriginalCaptureField(capture, "_effects"));
        var originalInput = Assert.IsType<AcceptedMechanicsInput>(
            OriginalCaptureField(capture, "_input"));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(
            lease, advanced.Step!.Interval!, Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var inserted = await capture.MaterializeWoundAsync(
            lease, admitted.Admission!, OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef),
            "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(inserted.Issues);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(inserted.Wound);
        var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
        var canonical = new Dictionary<string, byte[]?>();
        foreach (var path in new[]
                 {
                     AfterlifeEntityProfileState.StatePath,
                     EffectAcceptedTurnPlan.IdentityIndexPath,
                     WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath
                 })
            canonical.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));

        var completed = await capture.CompleteEffectsAsync(lease);
        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(completed.Plan);
        var effect = Assert.Single(plan.ActiveEffects,
            value => value["effectId"]?.GetValue<string>() == root.EffectId);
        Assert.Equal(wound.WoundId, effect["source"]?["sourceId"]?.GetValue<string>());
        Assert.Contains(plan.Sources, source => source.Kind == "wound" &&
            source.SourceId == wound.WoundId && source.DefinitionKey == root.DefinitionKey);
        Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
        var foreignCompletion = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            originalInput.PlanningContext!.EffectPlan!,
            resources.Result!.EffectBoundaryTranscript!,
            new EffectIdentityFactory());
        Assert.True(foreignCompletion.Success,
            string.Join(Environment.NewLine, foreignCompletion.Issues));
        Assert.DoesNotContain(foreignCompletion.Plan!.ActiveEffects,
            value => value["effectId"]?.GetValue<string>() == root.EffectId);
        var transcript = resources.Result.EffectBoundaryTranscript!;
        Assert.False(draft.OwnsCompletion(
            foreignCompletion.Plan,
            resources.Routing,
            transcript));
        Assert.True(draft.OwnsCompletion(
            completed.Plan!,
            resources.Routing,
            transcript));
        var preparedSource = await sourceOwner.PrepareContinuationAsync(lease);
        var sourceCompletion = preparedSource.Ticket!.ValidateCompletion(
            sourceOwner,
            lease,
            sourceOwner.CheckedExchanges.Count,
            resources,
            draft);
        Assert.True(sourceCompletion.Success,
            string.Join(Environment.NewLine, sourceCompletion.Issues));
        var foreignConsumptionIssues = sourceCompletion.Projection!.Consume(
            sourceOwner,
            lease,
            sourceOwner.CheckedExchanges.Count,
            resources,
            draft,
            foreignCompletion.Plan);
        Assert.Contains(foreignConsumptionIssues,
            issue => issue.Code == "spiritual_original_completion_owner_mismatch");
        var foreignReduction = resources.SealOriginalSpiritualReduction(
            foreignCompletion.Plan,
            sourceCompletion.Projection!);
        Assert.False(foreignReduction.Success);
        Assert.Contains(foreignReduction.Issues,
            issue => issue.Code == "spiritual_original_reduction_effect_mismatch");
        var reduced = await capture.CompleteOrdinaryReductionAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        Assert.Same(reduced, await capture.CompleteOrdinaryReductionAsync(lease));
        var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        Assert.Same(completed.Plan, reduction.Effects);
        var liveWounds = Assert.IsType<SpiritualLiveWoundCompletion>(
            reduction.LiveWoundCompletion);
        Assert.True(reduction.HasLiveWoundWork);
        var liveInsertion = Assert.Single(liveWounds.Insertions);
        Assert.Equal(wound.WoundId, liveInsertion.Wound.WoundId);
        Assert.Equal(wound.LastTransition.TransitionId,
            liveInsertion.Wound.LastTransition.TransitionId);
        Assert.True(JsonNode.DeepEquals(liveInsertion.ReducedState.Identity,
            liveWounds.FinalState.Identity));
        Assert.True(JsonNode.DeepEquals(liveInsertion.ReducedState.History,
            liveWounds.FinalState.History));
        Assert.Null(SpiritualLiveWoundCompletion.Seal(draft,
            completed.Plan!, resources.Routing!, transcript, resources, []));
        Assert.Throws<InvalidOperationException>(() =>
            new AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction(
                reduction.Input, reduction.InputFingerprint, reduction.Input.PlanningContext!,
                reduction.Definitions, reduction.Resources, reduction.Effects, null,
                reduction.OwnerCompanionAfterImages, reduction.OwnerTransitions,
                hasLiveWoundWork: false, issuanceKey: new object()));
        Assert.Throws<InvalidOperationException>(() =>
            new AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction(
            reduction.Input, reduction.InputFingerprint, reduction.Input.PlanningContext!,
            reduction.Definitions, reduction.Resources, reduction.Effects, null,
            reduction.OwnerCompanionAfterImages, reduction.OwnerTransitions,
            hasLiveWoundWork: true, issuanceKey: new object()));
        var prematurePlan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                reduction, reduction.Resources));
        Assert.Null(prematurePlan.Plan);
        Assert.Contains(prematurePlan.Issues, issue =>
            issue.Code == "spiritual_live_wound_receipt_join_required");
        Assert.Contains(reduction.Effects!.ActiveEffects,
            value => value["effectId"]?.GetValue<string>() == root.EffectId);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        foreach (var pair in canonical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Verifies that the reduction carries the source owner's final conflict image after a
    /// later signed exchange, rather than the conflict root captured before that exchange.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCompletion_ReductionUsesFinalSourceConflictImage()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var firstLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(firstLease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(firstLease));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(
            OriginalCaptureField(capture, "_effects"));
        var originalInput = Assert.IsType<AcceptedMechanicsInput>(
            OriginalCaptureField(capture, "_input"));
        var initialConflict = originalInput.PlanningContext!.OwnerCompanionAfterImages[
            AfterlifeSpiritualConflictState.StatePath];
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var first = await capture.AdvanceNextResourceExchangeAsync(firstLease);
        AssertNoConflictFrameErrors(first.Issues);
        var firstSource = Assert.Single(sourceOwner.Sources);
        var firstAdmission = await capture.AdmitWoundSourceAsync(
            firstLease, first.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(firstAdmission.Issues);
        var firstOffer = await capture.ReadWoundOpportunityAsync(firstLease, firstAdmission.Admission!);
        AssertNoConflictFrameErrors(firstOffer.Issues);
        var firstDecline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = firstOffer.Opportunity!.PublicRef,
            decision = "none"
        });
        AssertNoConflictFrameErrors((await capture.MaterializeWoundAsync(
            firstLease, firstAdmission.Admission!, firstDecline, null)).Issues);
        var stalePreparedSource = await sourceOwner.PrepareContinuationAsync(firstLease);
        var staleSourceCompletion = stalePreparedSource.Ticket!.ValidateCompletion(
            sourceOwner,
            firstLease,
            sourceOwner.CheckedExchanges.Count,
            resources,
            draft);
        Assert.True(staleSourceCompletion.Success,
            string.Join(Environment.NewLine, staleSourceCompletion.Issues));
        await firstLease.DisposeAsync();

        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var finalConflict = await ReadProjectedSourceContinuationCandidateAsync(context);
        Assert.False(JsonNode.DeepEquals(initialConflict, finalConflict));
        await using var finalLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var second = await capture.AdvanceNextResourceExchangeAsync(finalLease);
        AssertNoConflictFrameErrors(second.Issues);
        var secondInterval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            second.Step!.Interval);
        var secondSource = Assert.Single(sourceOwner.Sources,
            source => source.ExchangeId == secondInterval.ExchangeId);
        if (secondSource.Calculation.MaximumSeverityRank > 0)
        {
            var secondAdmission = await capture.AdmitWoundSourceAsync(
                finalLease, secondInterval, secondSource);
            AssertNoConflictFrameErrors(secondAdmission.Issues);
            var secondOffer = await capture.ReadWoundOpportunityAsync(
                finalLease, secondAdmission.Admission!);
            AssertNoConflictFrameErrors(secondOffer.Issues);
            var secondDecline = JsonSerializer.SerializeToElement(new
            {
                opportunityRef = secondOffer.Opportunity!.PublicRef,
                decision = "none"
            });
            AssertNoConflictFrameErrors((await capture.MaterializeWoundAsync(
                finalLease, secondAdmission.Admission!, secondDecline, null)).Issues);
        }

        var completed = await capture.CompleteEffectsAsync(finalLease);
        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
        var staleConsumptionIssues = staleSourceCompletion.Projection!.Consume(
            sourceOwner,
            finalLease,
            sourceOwner.CheckedExchanges.Count,
            resources,
            draft,
            completed.Plan!);
        Assert.Contains(staleConsumptionIssues,
            issue => issue.Code is "spiritual_source_continuation_ticket_lease" or
                "spiritual_source_continuation_ticket_stale");
        var staleReduction = resources.SealOriginalSpiritualReduction(
            completed.Plan!,
            staleSourceCompletion.Projection!);
        Assert.False(staleReduction.Success);
        Assert.Contains(staleReduction.Issues,
            issue => issue.Code == "spiritual_original_reduction_effect_mismatch");
        var reduced = await capture.CompleteOrdinaryReductionAsync(finalLease);

        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var projectedConflict = reduction.OwnerCompanionAfterImages[
            AfterlifeSpiritualConflictState.StatePath];
        Assert.True(JsonNode.DeepEquals(finalConflict, projectedConflict));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, finalLease));
    }

    /// <summary>
    /// Verifies that completion rejects an unexecuted exchange retained in the fresh source
    /// candidate, then succeeds after that suffix is executed and any required decision is retained.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCompletion_RejectsUnexecutedCandidateSuffix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));

        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var firstSource = Assert.Single(sourceOwner.Sources);
        var firstAdmission = await capture.AdmitWoundSourceAsync(
            lease, first.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(firstAdmission.Issues);
        var firstOffer = await capture.ReadWoundOpportunityAsync(lease, firstAdmission.Admission!);
        AssertNoConflictFrameErrors(firstOffer.Issues);
        var firstDecline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = firstOffer.Opportunity!.PublicRef,
            decision = "none"
        });
        AssertNoConflictFrameErrors((await capture.MaterializeWoundAsync(
            lease, firstAdmission.Admission!, firstDecline, null)).Issues);

        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var incomplete = await capture.CompleteEffectsAsync(lease);
        Assert.Null(incomplete.Plan);
        Assert.Contains(incomplete.Issues,
            issue => issue.Code == "spiritual_source_exchange_incomplete");
        Assert.Null(resources.Result);

        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(second.Issues);
        var secondInterval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            second.Step!.Interval);
        var secondSource = Assert.Single(sourceOwner.Sources,
            source => source.ExchangeId == secondInterval.ExchangeId);
        if (secondSource.Calculation.MaximumSeverityRank > 0)
        {
            var secondAdmission = await capture.AdmitWoundSourceAsync(
                lease, secondInterval, secondSource);
            AssertNoConflictFrameErrors(secondAdmission.Issues);
            var secondOffer = await capture.ReadWoundOpportunityAsync(lease, secondAdmission.Admission!);
            AssertNoConflictFrameErrors(secondOffer.Issues);
            var secondDecline = JsonSerializer.SerializeToElement(new
            {
                opportunityRef = secondOffer.Opportunity!.PublicRef,
                decision = "none"
            });
            AssertNoConflictFrameErrors((await capture.MaterializeWoundAsync(
                lease, secondAdmission.Admission!, secondDecline, null)).Issues);
        }

        var completed = await capture.CompleteEffectsAsync(lease);
        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
        Assert.NotNull(resources.Result);
    }

    /// <summary>
    /// Verifies that completion rechecks the source candidate before returning its cached
    /// result and rejects a newly appended exchange that was never executed.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCompletion_RevalidatesSourceBeforeCachedRetry()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(
            lease, advanced.Step!.Interval!, Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef,
            decision = "none"
        });
        AssertNoConflictFrameErrors((await capture.MaterializeWoundAsync(
            lease, admitted.Admission!, decline, null)).Issues);
        var completed = await capture.CompleteEffectsAsync(lease);
        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));

        await lease.DisposeAsync();
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        await using var retryLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var retry = await capture.CompleteEffectsAsync(retryLease);
        Assert.Null(retry.Plan);
        Assert.Contains(retry.Issues,
            issue => issue.Code == "spiritual_source_exchange_incomplete");
        Assert.NotSame(completed, retry);
    }

    /// <summary>
    /// Verifies that completion cannot drain the original resource owner before its source
    /// session has been bound to the completed original resource prefix.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCompletion_RejectsPendingOriginalPrefix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));

        var incomplete = await capture.CompleteEffectsAsync(lease);

        Assert.Null(incomplete.Plan);
        Assert.Contains(incomplete.Issues,
            issue => issue.Code == "spiritual_source_prefix_pending");
        Assert.Null(resources.Result);
        Assert.True(capture.IsCurrentOwner);
    }

    /// <summary>
    /// Verifies that completion preserves the legacy source capture whose accepted exchange
    /// frontier is unbounded while still requiring its complete checked stream.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCompletion_CompletesLegacyUnboundedSourceCapture()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(
            lease, advanced.Step!.Interval!, Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = JsonSerializer.SerializeToElement(new
        {
            opportunityRef = offered.Opportunity!.PublicRef,
            decision = "none"
        });
        AssertNoConflictFrameErrors((await capture.MaterializeWoundAsync(
            lease, admitted.Admission!, decline, null)).Issues);

        var completed = await capture.CompleteEffectsAsync(lease);

        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
        Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
