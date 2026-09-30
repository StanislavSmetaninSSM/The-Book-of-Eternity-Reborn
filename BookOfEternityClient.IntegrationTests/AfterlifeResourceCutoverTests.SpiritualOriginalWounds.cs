using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualWound_SourceFrontierAdmitsOnlyTheExecutedExchange(bool invalidSuffix)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: invalidSuffix);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        Assert.NotNull(first.Step?.Interval);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var source = Assert.Single(owner.Sources);
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (invalidSuffix)
        {
            Assert.NotEmpty(second.Issues);
            Assert.Null(second.Step);
            Assert.Same(source, Assert.Single(owner.Sources));
            Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
        }
        else
        {
            AssertNoConflictFrameErrors(second.Issues);
            Assert.Equal(1, second.Step!.Interval!.Ordinal);
            Assert.Equal(2, owner.Sources.Count);
            Assert.Same(source, owner.Sources[0]);
            Assert.Equal(new[] { 0, 1, 2, 3 }, owner.ClaimedDice);
        }
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task OriginalSpiritualWound_DeclineConsumesOpportunityWithoutInitializingDraft()
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
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(lease, advanced.Step!.Interval!, Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decline = JsonSerializer.SerializeToElement(new { opportunityRef = offered.Opportunity!.PublicRef, decision = "none" });
        var result = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decline, null);
        AssertNoConflictFrameErrors(result.Issues);
        Assert.Null(result.Wound);
        Assert.Same(result, await capture.MaterializeWoundAsync(lease, admitted.Admission!, decline, null));
        var changed = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            OriginalSpiritualWoundDecision(offered.Opportunity.PublicRef), "Чужое давление надломило волю хранителя.");
        Assert.Null(changed.Wound);
        Assert.Contains(changed.Issues, issue => issue.Code == "spiritual_wound_selection_conflict");
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
        Assert.False(Assert.IsType<bool>(draft.GetType().GetField("_initializationAttempted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(draft)));
        Assert.Empty(draft.Phases);
        Assert.False(draft.HasPendingWoundIntegration);
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, WoundIdentityState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Verifies that multiple explicit wound declines leave one retained resource/effect schedule
    /// equivalent to ordinary completion, including its nonempty identity-allocation order.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualWound_MultipleDeclinesPreserveOneOrdinaryGlobalSchedule()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 18, 3]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        var secondDice = candidate["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
        secondDice["diceUsed"]![0]!["value"] = 18;
        secondDice["diceUsed"]![1]!["value"] = 3;
        secondDice["playerTotal"] = 18;
        secondDice["oppositionTotal"] = 3;
        secondDice["margin"] = 15;
        secondDice["outcomeBand"] = "decisive_player_success";
        var secondPlayerCost = candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!;
        secondPlayerCost["before"] = 4;
        secondPlayerCost["after"] = 1;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        var canonical = new Dictionary<string, byte[]?>();
        foreach (var path in new[]
                 {
                     ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath,
                     AfterlifeSpiritualConflictState.StatePath,
                     AfterlifeEntityProfileState.StatePath,
                     EffectAcceptedTurnPlan.CommandPath,
                     EffectAcceptedTurnPlan.IdentityIndexPath,
                     WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath
                 })
        {
            canonical.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));
        }

        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(
            OriginalCaptureField(capture, "_effects"));
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var originalInput = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
        var ordinaryBase = Assert.IsType<EffectAcceptedTurnPlan>(originalInput.PlanningContext!.EffectPlan);
        var draftFactory = new ScenarioThreeEffectIdentityFactory();
        draft.GetType().GetField("identityFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(draft, draftFactory);

        for (var ordinal = 0; ordinal < 2; ordinal++)
        {
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step!.Interval);
            Assert.Equal(ordinal, interval.Ordinal);
            var source = Assert.Single(sourceOwner.Sources,
                candidate => candidate.ExchangeId == interval.ExchangeId);
            var admitted = await capture.AdmitWoundSourceAsync(lease, interval, source);
            AssertNoConflictFrameErrors(admitted.Issues);
            var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
            AssertNoConflictFrameErrors(offered.Issues);
            var decline = JsonSerializer.SerializeToElement(new
            {
                opportunityRef = offered.Opportunity!.PublicRef,
                decision = "none"
            });
            var declined = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decline, null);
            AssertNoConflictFrameErrors(declined.Issues);
            Assert.Null(declined.Wound);
            Assert.Same(declined,
                await capture.MaterializeWoundAsync(lease, admitted.Admission!, decline, null));
            Assert.False(Assert.IsType<bool>(draft.GetType().GetField("_initializationAttempted",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(draft)));
            Assert.Empty(draft.Phases);
            Assert.False(draft.HasPendingWoundIntegration);
        }

        Assert.Equal(new[] { 0, 1, 2, 3 }, sourceOwner.ClaimedDice);
        Assert.Equal(new[] { "exchange_conflict_frame_42", "exchange_source_second" },
            sourceOwner.Sources.Select(source => source.ExchangeId));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var completed = await capture.CompleteEffectsAsync(lease);
        Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
        var resourceResult = Assert.IsType<AcceptedMechanicsResourcePlanningResult>(resources.Result);
        Assert.Equal(1, resourceResult.Statistics.HistoryFreezeCount);
        var acceptedActivation = Assert.Single(resourceResult.EffectBoundaryTranscript!.AcceptedActivations);
        Assert.Equal(EffectMaterializationTestFixture.EffectId,
            acceptedActivation.Activation.Stamp.Identity.EffectId);
        var ordinaryFactory = new ScenarioThreeEffectIdentityFactory();
        var ordinary = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            ordinaryBase, resourceResult.EffectBoundaryTranscript, ordinaryFactory);
        Assert.True(ordinary.Success, string.Join(Environment.NewLine, ordinary.Issues));
        var ordinaryPlan = Assert.IsType<EffectAcceptedTurnPlan>(ordinary.Plan);
        var completedPlan = Assert.IsType<EffectAcceptedTurnPlan>(completed.Plan);
        Assert.Equal(ordinaryPlan.AcceptedBoundaryFinalPlanFingerprint,
            completedPlan.AcceptedBoundaryFinalPlanFingerprint);
        Assert.Equal(ordinaryPlan.AllocatedEffectIds, completedPlan.AllocatedEffectIds);
        Assert.Equal(ordinaryPlan.AllocatedTransitionIds, completedPlan.AllocatedTransitionIds);
        Assert.Equal(ordinaryFactory.Kinds, draftFactory.Kinds);
        Assert.NotEmpty(draftFactory.Kinds);
        Assert.Empty(completedPlan.AllocatedEffectIds);
        Assert.NotEmpty(completedPlan.AllocatedTransitionIds);
        var completedIdentity = EffectIdentityState.Parse(
            JsonSerializer.SerializeToElement(completedPlan.IdentityIndexAfterImage),
            EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(completedIdentity.Issues);
        var completedEffect = Assert.Single(completedIdentity.State!.Entries);
        Assert.Equal("expired", completedEffect.State);
        Assert.Equal(new[] { "create", "expire" },
            completedEffect.Transitions.Select(transition => transition.Kind));
        Assert.Equal(acceptedActivation.Activation.Stamp.Identity.EventRef,
            completedEffect.Transitions[^1].EventRef);
        var identityOwner = Assert.IsType<EffectIdentityHistoryOwner>(
            draft.GetType().GetField("identityRoot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(draft));
        Assert.Equal(draftFactory.Kinds.Count, identityOwner.AllocationCount);
        Assert.Equal(identityOwner.Allocations.Select(allocation => allocation.Identity),
            completedPlan.AllocatedEffectIds.Concat(completedPlan.AllocatedTransitionIds));
        Assert.NotEmpty(identityOwner.Writes);
        Assert.Equal(6, draft.Phases.Count);
        Assert.Throws<InvalidOperationException>(() =>
            draft.Complete(resourceResult.EffectBoundaryTranscript!));
        Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
        Assert.Equal(6, draft.Phases.Count);
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, WoundIdentityState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        foreach (var pair in canonical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Allocates one owned wound root while retaining its routing obligation and unpublished turn boundary.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualWound_InsertionAllocatesRealOwnedRootWithoutCompletingTurn()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(lease, advanced.Step!.Interval!, Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef);
        var seedIssues = new List<ValidationIssue>();
        var seedMethod = capture.GetType().GetMethod("ReadInitialWoundState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(seedMethod);
        var seed = Assert.IsType<WoundOperationBeforeData>(seedMethod.Invoke(capture, new object[] { seedIssues, true }));
        AssertNoConflictFrameErrors(seedIssues);
        Assert.NotNull(seed.WoundIdentity);
        Assert.NotNull(seed.WoundHistory);
        var result = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(result.Issues);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(result.Wound);
        Assert.NotEqual("new_wound", wound.WoundId);
        var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
        var actualEffects = Assert.IsType<List<JsonObject>>(draft.GetType().GetField("activeEffects", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(draft));
        Assert.Contains(actualEffects, effect => effect["effectId"]?.GetValue<string>() == root.EffectId &&
            effect["source"]?["sourceId"]?.GetValue<string>() == wound.WoundId);
        Assert.DoesNotContain(draft.Phases, phase => phase.Phase is
            EffectAcceptedTurnPlanner.EffectDraftPhase.FinalLifetime or EffectAcceptedTurnPlanner.EffectDraftPhase.FinalTerminal);
        var repeated = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            "Чужое давление надломило волю хранителя.");
        Assert.Same(result, repeated);
        var copiedAdmission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(admitted.Admission, null));
        var foreign = await capture.MaterializeWoundAsync(lease, copiedAdmission, decision,
            "Чужое давление надломило волю хранителя.");
        Assert.Null(foreign.Wound);
        Assert.Contains(foreign.Issues, issue => issue.Code == "spiritual_wound_frontier_mismatch");
        var changed = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            "Иное описание травмы хранителя.");
        Assert.Null(changed.Wound);
        Assert.Contains(changed.Issues, issue => issue.Code == "spiritual_wound_selection_conflict");
        Assert.Same(result, await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            "Чужое давление надломило волю хранителя."));
        Assert.Single(actualEffects, effect => effect["effectId"]?.GetValue<string>() == root.EffectId);
        Assert.False(draft.HasPendingWoundIntegration);
        Assert.Null(Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources")).Result);
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        var drained = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources")).Drain();
        Assert.True(drained.IsValid);
        var prematureCompletion = draft.Complete(drained.EffectBoundaryTranscript!);
        Assert.Contains(prematureCompletion.Issues, issue => issue.Code == "spiritual_wound_routing_required");
    }

    /// <summary>
    /// Proves private wound insertion retains its original owner, routing and unpublished turn boundary.
    /// </summary>
    /// <param name="apply">
    /// Applies the genuine prepared insertion under its owner's allocation scope when <see langword="true"/>.
    /// </param>
    /// <param name="collidingRoots">
    /// Supplies sibling roots with a forbidden stacking collision when <see langword="true"/>.
    /// </param>
    /// <param name="unsupportedResourceProfile">
    /// Supplies an unsupported wound resource component when <see langword="true"/>.
    /// </param>
    /// <param name="metered">
    /// Gives the accepted root a finite use budget when <see langword="true"/>.
    /// </param>
    /// <returns>
    /// A task completing after preparation, rejection or insertion preserves the expected authority boundary.
    /// </returns>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    public async Task OriginalSpiritualWound_OwnedPreparationAndPrivateInsertionPreserveTurnBoundary(bool apply, bool collidingRoots, bool unsupportedResourceProfile, bool metered)
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
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(lease, advanced.Step!.Interval!, Assert.Single(sourceOwner.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef);
        if (metered)
        {
            var amended = JsonNode.Parse(decision.GetRawText())!.AsObject();
            var definition = amended["proposal"]!["consequenceDefinitions"]![0]!["definition"]!.AsObject();
            definition["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["initialUses"] = 2,
                ["consumingEventTypes"] = new JsonArray("owner_turn_end")
            };
            foreach (var trigger in definition["triggers"]!.AsArray().OfType<JsonObject>())
                trigger["consumeUses"] = true;
            decision = JsonSerializer.SerializeToElement(amended);
        }
        if (unsupportedResourceProfile)
        {
            var amended = JsonNode.Parse(decision.GetRawText())!.AsObject();
            var definition = amended["proposal"]!["consequenceDefinitions"]![0]!["definition"]!.AsObject();
            var gain = EffectMaterializationTestFixture.CreateDefinition("periodic_gain")["components"]![0]!.DeepClone();
            gain["componentId"] = "wound_resource_gain";
            gain["payload"]!["resource"] = "spiritual_action_points";
            definition["components"]!.AsArray().Add(gain);
            definition["triggers"] = new JsonArray(new JsonObject
            {
                ["triggerId"] = "gain_after_spend", ["eventType"] = "resource_spent", ["priority"] = 100,
                ["componentIds"] = new JsonArray("wound_resource_gain"), ["consumeUses"] = false,
                ["resolutionMode"] = "deterministic"
            });
            decision = JsonSerializer.SerializeToElement(amended);
        }
        if (collidingRoots)
        {
            var amended = JsonNode.Parse(decision.GetRawText())!.AsObject();
            var definitions = amended["proposal"]!["consequenceDefinitions"]!.AsArray();
            var first = definitions[0]!.AsObject();
            first["definition"]!["stacking"]!["policy"] = "replace";
            var sibling = first.DeepClone().AsObject();
            sibling["definitionRef"] = "will_hindrance_sibling";
            sibling["definition"]!["definitionKey"] = "will_hindrance_sibling";
            definitions.Add(sibling);
            decision = JsonSerializer.SerializeToElement(amended);
        }
        var issues = new List<ValidationIssue>();
        var selected = await ValidationService.SpiritualOriginalTurnCapture.WoundSelection.SelectAsync(
            capture, lease, admitted.Admission!, decision, "Чужое давление надломило волю хранителя.", issues);
        if (collidingRoots || unsupportedResourceProfile)
        {
            Assert.Null(selected);
            Assert.Contains(issues, issue => issue.Code == "wound_materialization_owned_source_graph_invalid" &&
                issue.FilePath.EndsWith(collidingRoots ? ".stacking.stackKey" : ".components[1].profile", StringComparison.Ordinal));
            var untouchedDraft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
            Assert.Null(untouchedDraft.GetType().GetField("activeEffects", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(untouchedDraft));
            Assert.Empty(untouchedDraft.Phases);
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, WoundIdentityState.StatePath));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return;
        }
        AssertNoConflictFrameErrors(issues);
        Assert.NotNull(selected);
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
        Assert.True(selected.IsCurrentFor(draft));
        Assert.Empty(draft.Phases);
        Assert.Null(draft.GetType().GetField("activeEffects", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(draft));
        var copy = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSelection>(
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(selected, null));
        Assert.False(copy.IsCurrentFor(draft));
        var repeated = await ValidationService.SpiritualOriginalTurnCapture.WoundSelection.SelectAsync(
            capture, lease, admitted.Admission!, decision, "Чужое давление надломило волю хранителя.", issues);
        AssertNoConflictFrameErrors(issues);
        Assert.Same(selected, repeated);
        var changed = await ValidationService.SpiritualOriginalTurnCapture.WoundSelection.SelectAsync(
            capture, lease, admitted.Admission!, decision, "Изменённая сцена.", issues);
        Assert.Null(changed);
        Assert.Contains(issues, issue => issue.Code == "spiritual_wound_selection_conflict");
        issues.Clear();
        var before = draft.AdvanceForWoundInsertion(selected, issues);
        AssertNoConflictFrameErrors(issues);
        Assert.NotNull(before);
        Assert.True(before.IsCurrent);
        Assert.Same(before, draft.AdvanceForWoundInsertion(selected, issues));
        Assert.Empty(draft.Phases);
        var beforeCopy = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundBeforeAuthority>(
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(before, null));
        Assert.False(beforeCopy.IsCurrent);
        Assert.Throws<InvalidOperationException>(() =>
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundBeforeAuthority.Retain(draft,
                EffectAcceptedTurnPlanner.EffectDraftWoundSelection.From(selected)));
        var detachedIdentity = before.Data.WoundIdentity!;
        detachedIdentity["entries"]!.AsArray().Add(new JsonObject());
        Assert.Empty(before.Data.WoundIdentity!["entries"]!.AsArray());
        var prepared = WoundAcceptedTurnPlanner.PrepareFromDraft(before);
        AssertNoConflictFrameErrors(prepared.Issues);
        Assert.True(prepared.Success);
        Assert.Same(before, prepared.Plan!.DraftBefore);
        Assert.True(before.MatchesPrepared(prepared.Plan));
        Assert.Empty(WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared.Plan));
        Assert.Same(prepared, WoundAcceptedTurnPlanner.PrepareFromDraft(before));
        var rejectedCopy = WoundAcceptedTurnPlanner.PrepareFromDraft(beforeCopy);
        Assert.False(rejectedCopy.Success);
        var operationSources = EffectAcceptedTurnInputComposer.BuildPreparedWoundOperationAuthority(prepared.Plan);
        AssertNoConflictFrameErrors(operationSources.Issues);
        Assert.Single(operationSources.SnapshotSameTurnWoundGroups());
        Assert.True(WoundAcceptedTurnPlannerCore.SameTurnWoundAuthorityAgrees(prepared.Plan, operationSources));
        var separatePreparation = WoundAcceptedTurnPlanner.Prepare(selected.Input, new SeparateWoundScopeAllocator());
        AssertNoConflictFrameErrors(separatePreparation.Issues);
        Assert.True(separatePreparation.Success);
        var separateSources = EffectAcceptedTurnInputComposer.BuildPreparedWoundOperationAuthority(separatePreparation.Plan!);
        Assert.NotEqual(Assert.Single(separatePreparation.Plan!.AllocatedWoundIds), Assert.Single(prepared.Plan.AllocatedWoundIds));
        var combinedSources = separateSources.WithNewPreparedWound(prepared.Plan);
        AssertNoConflictFrameErrors(combinedSources.Issues);
        Assert.Equal(2, combinedSources.SnapshotSameTurnWoundGroups().Count);
        var ordinaryDuplicate = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(),
            separateSources.SnapshotSameTurnWoundGroups().Concat(operationSources.SnapshotSameTurnWoundGroups()).ToArray(),
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: separateSources.SnapshotWoundGroupAuthorities().Concat(operationSources.SnapshotWoundGroupAuthorities()).ToArray()));
        Assert.Contains(ordinaryDuplicate.Issues, issue => issue.Code == "effect_source_authority_duplicate_ref");
        var emptySources = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            Array.Empty<EffectSourceExport>(), Array.Empty<EffectSourceExport>(), new HashSet<string>(StringComparer.Ordinal)));
        var singleCanonicalView = emptySources.WithNewPreparedWound(prepared.Plan);
        Assert.Equal(operationSources.CanonicalFingerprint, singleCanonicalView.CanonicalFingerprint);
        Assert.NotEqual(operationSources.Fingerprint, singleCanonicalView.Fingerprint);
        foreach (var entry in combinedSources.SnapshotSameTurnWoundEntries())
        {
            var target = combinedSources.SnapshotWoundGroupAuthorities().Single(group => group.Key.SourceId == entry.Key.SourceId).Target;
            var canonical = combinedSources.ResolveCanonicalBinding(entry.Key, target.Kind);
            AssertNoConflictFrameErrors(canonical.Issues);
            Assert.True(canonical.Success);
            var unresolved = combinedSources.Resolve(new JsonObject
            {
                ["kind"] = "wound", ["sourceRef"] = entry.SourceRef, ["definitionKey"] = entry.Key.DefinitionKey
            }, entry.Key.Realm, target.Kind, null);
            Assert.Contains(unresolved.Issues, issue => issue.Code == "effect_source_selector_unresolved");
        }
        var ordinaryFinal = WoundAcceptedTurnPlanner.Finalize(prepared.Plan,
            new WoundEffectBatchPlanningResult(null, Array.Empty<ValidationIssue>()));
        Assert.Contains(ordinaryFinal.Issues, issue => issue.Code == "spiritual_wound_owned_application_required");
        Assert.Single(prepared.Plan.AllocatedWoundIds);
        Assert.Empty(draft.Phases);
        if (apply)
        {
            var applyMethod = draft.GetType().GetMethod("ApplyPreparedWound", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(applyMethod);
            var clock = Assert.IsType<SpiritualWoundProjectionClock>(OriginalCaptureProperty(
                OriginalCaptureField(capture, "_allocations")!, "Clock"));
            object? applied;
            using (var allocationScope = clock.BeginSpeculation())
            {
                applied = applyMethod.Invoke(draft, new object[] { before, prepared.Plan, issues });
                AssertNoConflictFrameErrors(issues);
                Assert.NotNull(applied);
                allocationScope.Commit();
            }
            var insertion = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>(applied);
            Assert.False(before.IsCurrent);
            Assert.True(insertion.Matches(prepared.Plan));
            Assert.True(before.MatchesPrepared(prepared.Plan));
            Assert.Equal(1, insertion.VersionAfter);
            var root = Assert.Single(insertion.Wound.Consequences.OwnedEffectSources.RootBindings);
            var application = Assert.Single(insertion.Applications);
            Assert.Equal(root.EffectId, application.EffectId);
            Assert.NotEmpty(insertion.IdentityWrites);
            Assert.NotEmpty(insertion.Allocations);
            Assert.NotEmpty(insertion.CarrierEdits);
            var active = Assert.IsType<List<JsonObject>>(draft.GetType().GetField("activeEffects", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(draft));
            Assert.Contains(active, effect => effect["effectId"]?.GetValue<string>() == root.EffectId &&
                effect["source"]?["sourceId"]?.GetValue<string>() == insertion.Wound.WoundId);
            Assert.Same(insertion, applyMethod.Invoke(draft, new object[] { before, prepared.Plan, issues }));
            Assert.Empty(draft.Phases);
            var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
            var routing = resources.Routing!;
            var prepareRouting = routing.GetType().GetMethod("PrepareWoundInsertion", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(prepareRouting);
            var version = prepareRouting.Invoke(routing, new object[] { insertion, issues });
            AssertNoConflictFrameErrors(issues);
            Assert.NotNull(version);
            var mechanics = Assert.IsType<EffectMechanicsSnapshot>(version.GetType().GetProperty("Mechanics", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(version));
            Assert.True(mechanics.IsAccepted);
            Assert.Contains(mechanics.Components, component => component.EffectId == root.EffectId &&
                component.Profile == "spiritual_roll_hindrance");
            var copiedInsertion = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>(
                typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(insertion, null));
            Assert.Null(prepareRouting.Invoke(routing, new object[] { copiedInsertion, issues }));
            Assert.Contains(issues, issue => issue.Code == "spiritual_wound_routing_insertion_mismatch");
            issues.Clear();
            var register = resources.GetType().GetMethod("RegisterWoundInstances", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(register);
            var registration = register.Invoke(resources, new object[] { insertion, issues });
            AssertNoConflictFrameErrors(issues);
            Assert.NotNull(registration);
            Assert.Same(registration, register.Invoke(resources, new object[] { insertion, issues }));
            var originalInput = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
            using (var foreignDraft = EffectAcceptedTurnPlanner.EffectAcceptedDraft.Begin(
                originalInput.PlanningContext!.EffectPlan!, new EffectIdentityFactory()))
                Assert.Throws<InvalidOperationException>(() => foreignDraft.AcceptInstalledWoundRouting(insertion, resources));
            var instanceIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(
                registration.GetType().GetProperty("NewInstanceIds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registration));
            Assert.Equal(root.EffectId, Assert.Single(instanceIds));
            if (metered)
            {
                var state = resources.GetType().GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resources)!;
                var arbiter = Assert.IsType<AcceptedEffectUseArbiter>(state.GetType().GetField("arbiter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state));
                var budgets = Assert.IsType<Dictionary<string, int>>(arbiter.GetType().GetField("_remainingUses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arbiter));
                Assert.Equal(2, budgets[root.EffectId]);
                Assert.DoesNotContain(arbiter.AcceptedTranscript, stamp => stamp.Identity.EffectId == root.EffectId);
            }
            Assert.Null(register.Invoke(resources, new object[] { copiedInsertion, issues }));
            Assert.Contains(issues, issue => issue.Code == "spiritual_wound_instance_registration_mismatch");
            issues.Clear();
            var blockedAdvance = await capture.AdvanceNextResourceExchangeAsync(lease);
            Assert.Contains(blockedAdvance.Issues, issue => issue.Code == "spiritual_wound_routing_required");
        }
        Assert.True(sourceOwner.IsOriginalAbsent(WoundIdentityState.StatePath));
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, WoundIdentityState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData("ordinary")]
    [InlineData("guaranteed")]
    [InlineData("zero")]
    public async Task OriginalSpiritualWound_OpportunityUsesAffectedActorAndOwnedEvent(string variant)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        if (variant != "ordinary")
        {
            var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
            var art = SourceOwnerSpecialArt("player_soul", "player_soul");
            art["spiritualWoundEnvelope"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["maximumSeverityRank"] = variant == "zero" ? 0 : 1,
                ["guaranteedSeverityRank"] = variant == "guaranteed" ? 1 : null
            };
            profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
            await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        }
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (variant != "ordinary")
        {
            var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            var update = root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject();
            var exchange = update["exchange"]!.AsObject();
            exchange["specialArtAudit"] = new JsonObject
            {
                ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
                ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
                ["costMultiplierPercent"] = 200, ["effectNote"] = "Искусство оставляет след в духовном узоре."
            };
            var cost = exchange["actionCostAudit"]!["player"]!.AsObject();
            cost["effectiveCost"] = 6;
            cost["after"] = 0;
            cost["specialArtId"] = "art_source_owner";
            cost["specialCostMultiplierPercent"] = 200;
            cost["standardEffectiveCost"] = 3;
            update["activeConflictAfter"]!["exchangeLog"]!.AsArray()[0] = exchange.DeepClone();
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        }
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var source = Assert.Single(sourceOwner.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, advanced.Step!.Interval!, source);
        AssertNoConflictFrameErrors(admitted.Issues);

        var result = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(result.Issues);
        if (variant == "zero")
        {
            Assert.Null(result.Opportunity);
            Assert.Null(result.Binding);
            return;
        }
        var opportunity = Assert.IsType<WoundOpportunityAuthority>(result.Opportunity);
        Assert.True(WoundOpportunityAuthority.HasCompleteShape(opportunity));
        Assert.Equal("guardian", opportunity.Owner.OwnerKind);
        Assert.Equal(source.AffectedActor.Split(':', 2)[1], opportunity.Owner.OwnerId);
        Assert.Equal(WoundCarrierCatalog.AfterlifeProfilesPath, opportunity.Owner.CarrierPath);
        Assert.Equal("afterlife_strain_transition_v1", opportunity.ProfileKey);
        Assert.Equal(source.Calculation.MaximumSeverityRank, opportunity.MaximumSeverityRank);
        Assert.Equal(variant == "guaranteed" ? 1 : (int?)null, opportunity.MinimumSeverityRank);
        var safeOffer = ValidationService.ProjectC2SafeOffer(opportunity);
        Assert.Equal(1, safeOffer.MinimumSeverityRank);
        Assert.Equal(variant == "guaranteed" ? 1 : (int?)null,
            safeOffer.RequiredSeverityRank);
        Assert.Equal(variant == "guaranteed"
            ? new[] { "materialize" }
            : new[] { "none", "materialize" }, safeOffer.AllowedDecisions);
        Assert.Null(opportunity.GuaranteedTrigger); // The signed art has no creation timestamp.
        var clone = WoundAcceptedTurnData.CloneOpportunity(opportunity);
        Assert.True(WoundOpportunityAuthority.HasCompleteShape(clone));
        var declined = WoundOpportunityDecisionAuthority.Evaluate(clone,
            new(opportunity.PublicRef, "none", null, null), Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.Equal(variant != "guaranteed", declined.Success);
        var selected = WoundOpportunityDecisionAuthority.Evaluate(clone,
            new(opportunity.PublicRef, "materialize", 1, "new_wound"), Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(selected.Success, string.Join(Environment.NewLine, selected.Issues));
        var repeated = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        Assert.Same(result, repeated);
        if (variant == "guaranteed")
        {
            Assert.Contains(declined.Issues, issue => issue.Code == "wound_guaranteed_result_required");
            var wrongRank = WoundOpportunityDecisionAuthority.Evaluate(clone,
                new(opportunity.PublicRef, "materialize", 2, "new_wound"), Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.Contains(wrongRank.Issues, issue => issue.Code == "wound_guaranteed_severity_mismatch");
            foreach (var altered in new[]
            {
                clone with { Owner = clone.Owner with { OwnerId = "other_guardian" } },
                clone with { SourceId = "other_art" },
                clone with { MinimumSeverityRank = null },
                clone with { MaximumSeverityRank = 4 },
                clone with { SnapshotToken = "other_snapshot" },
                clone with { EventAuthorityId = "other_event" },
                clone with { PublicRef = "other_opportunity" }
            })
            {
                var resealed = altered with { AuthorityFingerprint = WoundOpportunityAuthority.RecomputeAuthorityFingerprint(altered) };
                Assert.False(WoundOpportunityAuthority.HasCompleteShape(resealed));
            }
            var declineJson = System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                opportunityRef = opportunity.PublicRef, decision = "none"
            });
            var composedDecline = WoundResponseInputComposer.Compose(result.Binding!, new[] { clone },
                new[] { declineJson }, null, Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.Contains(composedDecline.Issues, issue => issue.Code == "wound_guaranteed_result_required");
            var wrongTurn = WoundResponseInputComposer.Compose(result.Binding! with { Turn = 43 }, new[] { clone },
                new[] { declineJson }, null, Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.Contains(wrongTurn.Issues, issue => issue.Code == "wound_response_opportunity_invalid");
            Assert.Multiple(
                () =>
                {
                    var copiedProof = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.OriginalSourceGuarantee>(
                        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(opportunity.OriginalSourceGuarantee, null));
                    Assert.False(WoundOpportunityAuthority.HasCompleteShape(clone with { OriginalSourceGuarantee = copiedProof }));
                },
                () =>
                {
                    var response = WoundResponseInputComposer.Compose(result.Binding!, new[] { clone },
                        new[] { OriginalSpiritualWoundDecision(opportunity.PublicRef) }, "Чужое давление надломило волю хранителя.",
                        Array.Empty<WoundOpportunityDecisionReceipt>());
                    Assert.True(response.Success, string.Join(Environment.NewLine, response.Issues.Select(issue => issue.Code + ": " + issue)));
                    Assert.Equal(opportunity.GuaranteedTriggerId, Assert.Single(response.Transitions).ProposedAfter!.Origin.GuaranteedTriggerId);
                    Assert.True(WoundOpportunityAuthority.HasCompleteShape(Assert.Single(response.MaterializedOpportunities)));
                    var serialized = response.CommandRoot!["commands"]![0]!["opportunity"]!;
                    Assert.Null(serialized["guaranteedTrigger"]);
                    Assert.Equal("signed_original_source", serialized["originalSourceGuarantee"]!["provenance"]!.GetValue<string>());
                    Assert.Null(serialized["originalSourceGuarantee"]!["materializedAtTurn"]);
                    var unowned = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(response.CommandRoot));
                    Assert.False(unowned.Success);
                    var owned = WoundResponseInputComposer.ParseCommandRoot(
                        JsonSerializer.SerializeToElement(response.CommandRoot), [opportunity]);
                    Assert.True(owned.Success, string.Join(Environment.NewLine,
                        owned.Issues.Select(issue => issue.Code + ": " + issue)));
                    Assert.Same(opportunity.OriginalSourceGuarantee,
                        Assert.Single(owned.Commands).Opportunity.OriginalSourceGuarantee);
                    var tampered = response.CommandRoot.DeepClone().AsObject();
                    tampered["commands"]![0]!["opportunity"]!["originalSourceGuarantee"]!["requiredSeverityRank"] = 2;
                    var rejected = WoundResponseInputComposer.ParseCommandRoot(
                        JsonSerializer.SerializeToElement(tampered), [opportunity]);
                    Assert.False(rejected.Success);
                    Assert.Contains(rejected.Issues, issue =>
                        issue.Code == "wound_command_opportunity_invalid");
                });
        }
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Allocates a distinct test source scope while preserving the proposal's local references.
    /// </summary>
    private sealed class SeparateWoundScopeAllocator : IWoundAcceptedTurnIdentityAllocator
    {
        private readonly WoundAcceptedTurnIdentityAllocator _inner = new();

        /// <summary>
        /// Creates a wound identity distinct from the default allocator's identity.
        /// </summary>
        /// <param name="scope">
        /// Original preparation identity scope.
        /// </param>
        /// <returns>
        /// A deterministic identifier for the separate test scope.
        /// </returns>
        public string CreateWoundId(WoundAcceptedTurnIdentityScope scope) => _inner.CreateWoundId(scope) + "_initial";

        /// <summary>
        /// Creates a distinct application identity without changing its local proposal reference.
        /// </summary>
        /// <param name="scope">
        /// Original preparation identity scope.
        /// </param>
        /// <param name="localApplicationRef">
        /// Proposal-local root application reference.
        /// </param>
        /// <param name="definitionKey">
        /// Exact source definition key.
        /// </param>
        /// <param name="operationKey">
        /// Exact wound operation key.
        /// </param>
        /// <returns>
        /// A deterministic application identifier for the separate test scope.
        /// </returns>
        public string CreateApplicationRef(WoundAcceptedTurnIdentityScope scope, string localApplicationRef,
            string definitionKey, string operationKey) =>
            _inner.CreateApplicationRef(scope, localApplicationRef, definitionKey, operationKey) + "_initial";

        /// <summary>
        /// Creates a distinct transition identity for the separate preparation.
        /// </summary>
        /// <param name="scope">
        /// Original preparation identity scope.
        /// </param>
        /// <param name="localTransitionRef">
        /// Proposal-local transition reference.
        /// </param>
        /// <returns>
        /// A deterministic transition identifier for the separate test scope.
        /// </returns>
        public string CreateTransitionId(WoundAcceptedTurnIdentityScope scope, string localTransitionRef) =>
            _inner.CreateTransitionId(scope, localTransitionRef) + "_initial";
    }

    /// <summary>
    /// Builds a GM-authored mental wound proposal without client-assigned effect source links.
    /// </summary>
    /// <param name="opportunityRef">
    /// Public reference of the signed opportunity being resolved.
    /// </param>
    /// <param name="profile">
    /// Exact spiritual consequence profile; defaults to roll hindrance.
    /// </param>
    /// <param name="operation">
    /// Operation burdened by this consequence; defaults to pressure.
    /// </param>
    /// <param name="targetKind">
    /// Persistent effect target kind permitted by the proposal; defaults to guardian.
    /// </param>
    /// <returns>
    /// A rank I spiritual wound decision with one source-bound consequence definition.
    /// </returns>
    private static JsonElement OriginalSpiritualWoundDecision(string opportunityRef,
        string profile = "spiritual_roll_hindrance", string operation = "pressure", string targetKind = "guardian")
    {
        var proposal = JsonNode.Parse("""
            {
              "classification": { "woundType": "Надлом воли", "locationProfile": { "kind": "mental", "readableLocus": "воля хранителя" } },
              "display": { "name": "Надлом воли", "description": "Давление нарушило способность сосредоточиться.",
                "visibleSymptoms": ["неуверенность"], "prognosis": "Воля восстановится после исцеления.",
                "visibility": "known_to_player", "acquisitionNarration": "Чужое давление надломило волю хранителя." },
              "severity": "I", "complications": [], "consequenceDefinitions": [],
              "treatment": { "diagnosisPaths": [], "routes": [], "knownRouteIds": [], "completedRouteIds": [] },
              "recovery": { "mode": "requires_stabilization", "clockKind": "afterlife_safe_cycle", "cadence": 1,
                "currentStepProgress": 0, "currentStepThreshold": 3, "lastTickKey": null,
                "blockers": ["not_stabilized"], "carryOverflow": true, "deteriorationPolicy": null }
            }
            """)!.AsObject();
        if (targetKind == "player")
        {
            proposal["classification"]!["locationProfile"]!["readableLocus"] = "воля души";
            proposal["display"]!["acquisitionNarration"] = "Чужое давление надломило волю души.";
        }
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(profile, targetKind: targetKind, operation: operation,
            magnitude: profile == "spiritual_action_cost_burden" ? JsonValue.Create(1) : null);
        definition["links"] = new JsonArray(); // The client assigns the actual wound source identity.
        proposal["consequenceDefinitions"]!.AsArray().Add(new JsonObject
        {
            ["definitionRef"] = "will_hindrance",
            ["definition"] = definition,
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = profile,
                    ["readableSummary"] = targetKind == "player" ? "Душе труднее сосредоточиться." : "Хранителю труднее сосредоточиться." })
            }
        });
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["opportunityRef"] = opportunityRef, ["decision"] = "materialize", ["woundRef"] = "new_wound", ["proposal"] = proposal
        });
    }

    [Fact]
    public async Task OriginalSpiritualWound_CompletedExecutorCannotRetainOrMintInsertionAdmission()
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
        var interval = advanced.Step!.Interval!;
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var source = Assert.Single(sourceOwner.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, interval, source);
        AssertNoConflictFrameErrors(admitted.Issues);
        Assert.True(capture.OwnsWoundAdmission(admitted.Admission));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var completed = resources.Drain();
        Assert.True(completed.IsValid, string.Join(Environment.NewLine, completed.Issues));
        Assert.True(resources.Owns(interval)); // Historical evidence remains owned.
        Assert.False(capture.OwnsWoundAdmission(admitted.Admission));
        var rejected = await capture.AdmitWoundSourceAsync(lease, interval, source);
        Assert.Null(rejected.Admission);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_wound_frontier_mismatch");
    }

    [Theory]
    [InlineData("owned")]
    [InlineData("source_copy")]
    [InlineData("interval_copy")]
    public async Task OriginalSpiritualWound_AdmissionRequiresActualClosedSourceAndInterval(string variant)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step!.Interval);
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var source = Assert.Single(sourceOwner.Sources);
        var selectedSource = variant == "source_copy" ? source with { } : source;
        var selectedInterval = variant == "interval_copy"
            ? Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
                typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(interval, null))
            : interval;

        var admitted = await capture.AdmitWoundSourceAsync(lease, selectedInterval, selectedSource);
        if (variant != "owned")
        {
            Assert.Null(admitted.Admission);
            Assert.Contains(admitted.Issues, issue => issue.Code == "spiritual_wound_frontier_mismatch");
            var genuine = await capture.AdmitWoundSourceAsync(lease, interval, source);
            AssertNoConflictFrameErrors(genuine.Issues);
            Assert.NotNull(genuine.Admission);
        }
        else
        {
            AssertNoConflictFrameErrors(admitted.Issues);
            var admission = admitted.Admission!;
            Assert.Same(source, admission.Source);
            Assert.Same(interval, admission.Interval);
            var repeated = await capture.AdmitWoundSourceAsync(lease, interval, source);
            AssertNoConflictFrameErrors(repeated.Issues);
            Assert.Same(admission, repeated.Admission);
            Assert.True(capture.OwnsWoundAdmission(admission));
            capture.Dispose();
            Assert.False(capture.OwnsWoundAdmission(admission));
        }
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualWound_CachedAdmissionRechecksSourceAndRevision(bool changeRevision)
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
        var interval = advanced.Step!.Interval!;
        var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var source = Assert.Single(sourceOwner.Sources);
        var first = await capture.AdmitWoundSourceAsync(lease, interval, source);
        AssertNoConflictFrameErrors(first.Issues);
        Assert.NotNull(first.Admission);
        if (changeRevision)
        {
            var continued = await sourceOwner.ContinueAsync(lease);
            AssertNoConflictFrameErrors(continued.Issues);
            Assert.False(capture.OwnsWoundAdmission(first.Admission));
        }
        else
        {
            var changed = await ReadSourceMissingCandidateAsync(context);
            changed["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                System.Text.Encoding.UTF8.GetBytes(changed.ToJsonString()));
        }
        var rejected = await capture.AdmitWoundSourceAsync(lease, interval, source);
        Assert.Null(rejected.Admission);
        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Emits deterministic effect identities while recording the exact allocation call order for
    /// the scenario-three ordinary-versus-retained completion comparison.
    /// </summary>
    private sealed class ScenarioThreeEffectIdentityFactory : EffectIdentityFactory
    {
        private readonly List<string> _kinds = new();
        private int _effectOrdinal;
        private int _transitionOrdinal;
        private int _resolutionOrdinal;

        /// <summary>
        /// Gets the ordered allocation kinds requested by the effect finalizer.
        /// </summary>
        internal IReadOnlyList<string> Kinds => _kinds.AsReadOnly();

        /// <inheritdoc/>
        internal override string CreateEffectId()
        {
            _kinds.Add("effect");
            return $"effect_scenario_three_{++_effectOrdinal}";
        }

        /// <inheritdoc/>
        internal override string CreateTransitionId()
        {
            _kinds.Add("transition");
            return $"effect_transition_scenario_three_{++_transitionOrdinal}";
        }

        /// <inheritdoc/>
        internal override string CreateResolutionId()
        {
            _kinds.Add("resolution");
            return $"effect_resolution_scenario_three_{++_resolutionOrdinal}";
        }
    }
}
