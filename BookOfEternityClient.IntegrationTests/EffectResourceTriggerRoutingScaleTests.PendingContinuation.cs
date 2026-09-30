using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectResourceTriggerRoutingScaleTests
{
    [Fact]
    public void LivePendingContinuation_RetainsCompletedCrossPhaseDependencyOnResume()
    {
        var fixture = CreateAcceptedEventBudgetFixture(remainingUses: 1, current: 5m,
            triggerSpecs: new[] { new BudgetTriggerSpec("pending_damage", "resource_damaged", 10, true, false,
                ResolutionMode: "bounded_receipt") });
        var source = new ResourceMutationSourceExport("narrative_outcome", "chronological_damage",
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ResourceMutationSourceState.Active, false,
            new ResourceOwnerKey(fixture.Coordinate.Realm, fixture.Coordinate.OwnerKind, fixture.Coordinate.ResourceOwnerId));
        var catalog = ResourceMutationSourceCatalog.Create(fixture.Sources.Exports.Append(source));
        Assert.True(catalog.IsValid, Format(catalog.Issues));
        fixture = fixture with { Sources = catalog.Catalog! };
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(fixture.Plan, fixture.Owners, fixture.Definitions);
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), new LiveIntervalIdentityCounter().Factory, route);
        InvokePendingOwner(session, "BindOriginalPendingContext", PendingContinuationInput(fixture));
        var restore = CreateBudgetMutation(fixture.Coordinate, "turn_43:chronological:1", ResourceOperation.Restore, 1m);
        session.StageNextExchange(LiveBudgetBatch(fixture, restore, 0, 5m));
        Assert.NotNull(session.AdvanceThroughExchange().Interval);
        var damage = CreateBudgetMutation(fixture.Coordinate, "turn_43:chronological:2", ResourceOperation.Damage, 1m,
            new[] { restore.Key }) with
        {
            Source = new ResourceMutationSourceRequest(source.SourceKind, source.SourceId, ResourceOperation.Damage)
        };
        session.StageNextExchange(LiveBudgetBatch(fixture, damage, 1, 6m));
        var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualResourceExchange>(
            session.AdvanceThroughExchange().PendingResource);
        var resumed = ResumePendingOwner(session, wait, PendingReceipts(PendingPacket(session, wait), 2m));
        Assert.NotNull(resumed.Interval);
        var result = session.Drain();
        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(new[] { 6m, 5m, 7m }, result.AppliedTransitions.Select(value => value.AfterState!.Current));
    }

    [Fact]
    public void LivePendingContinuation_TwoWavesKeepTheSameExecutionAndSpentUses()
    {
        var fixture = PendingContinuationFixture();
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        // OLD-compatible positive control: actual accepted bounded execution,
        // before checking the missing continuation contract by reflection.
        using (var control = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), new LiveIntervalIdentityCounter().Factory, route))
        {
            control.StageNextExchange(LiveBudgetBatch(fixture,
                CreateBudgetMutation(fixture.Coordinate, "turn_43:pending_control:1",
                    ResourceOperation.Damage, 1m), 0, 5m));
            var pending = control.AdvanceThroughExchange();
            Assert.NotEmpty(pending.PendingResource!.RequiredOutputs);
            Assert.Null(pending.Interval);
            Assert.Equal(0, pending.PendingResource.Statistics.HistoryFreezeCount);
        }

        var counter = new LiveIntervalIdentityCounter();
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), counter.Factory, route);
        InvokePendingOwner(session, "BindOriginalPendingContext", PendingContinuationInput(fixture));
        var damage = CreateBudgetMutation(fixture.Coordinate, "turn_43:pending_live:1",
            ResourceOperation.Damage, 1m);
        session.StageNextExchange(LiveBudgetBatch(fixture, damage, 0, 5m));
        var first = session.AdvanceThroughExchange().PendingResource!;
        var firstPacket = PendingPacket(session, first);
        var firstPacketText = firstPacket.ToJsonString();
        var firstAllocations = counter.Calls;
        firstPacket.Clear();
        Assert.Equal(firstPacketText, PendingPacket(session, first).ToJsonString());
        var secondStep = ResumePendingOwner(session, first,
            PendingReceipts(PendingPacket(session, first), 2m));
        Assert.Null(secondStep.Interval);
        Assert.NotNull(secondStep.PendingResource);
        Assert.NotSame(first, secondStep.PendingResource);
        Assert.True(counter.Calls > firstAllocations);
        Assert.Equal(0, secondStep.PendingResource.Statistics.HistoryFreezeCount);
        var secondPacket = PendingPacket(session, secondStep.PendingResource);
        Assert.NotEqual(firstPacketText, secondPacket.ToJsonString());
        var beforeStale = counter.Calls;
        var stale = InvokePendingOwner(session, "ResumePendingResource", first,
            PendingReceipts(JsonNode.Parse(firstPacketText)!.AsObject(), 2m));
        Assert.Null(PendingResultProperty(stale, "Step"));
        Assert.NotEmpty(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            PendingResultProperty(stale, "Issues")));
        Assert.Equal(beforeStale, counter.Calls);
        Assert.Equal(secondPacket.ToJsonString(),
            PendingPacket(session, secondStep.PendingResource).ToJsonString());

        var finishedExchange = ResumePendingOwner(session, secondStep.PendingResource,
            PendingReceipts(secondPacket, 2m));
        Assert.NotNull(finishedExchange.Interval);
        Assert.Null(finishedExchange.PendingResource);
        Assert.Null(finishedExchange.Result);
        var prefix = finishedExchange.Interval;
        Assert.Equal(3, prefix.AppliedTransitions.Count);
        Assert.Equal(new decimal[] { 4m, 6m, 8m },
            prefix.AppliedTransitions.Select(value => value.AfterState!.Current));
        Assert.Equal(2, prefix.EffectAfter.AcceptedActivations.Count);
        Assert.Equal(new int?[] { 2, 1 }, prefix.EffectAfter.AcceptedActivations
            .Select(value => value.Activation.Stamp.UsesBefore));
        var prefixIds = prefix.AppliedTransitions.Select(value => value.TransitionId).ToArray();
        session.StageNextExchange(LiveBudgetBatch(fixture,
            CreateBudgetMutation(fixture.Coordinate, "turn_43:pending_live:2",
                ResourceOperation.Damage, 1m, new[] { damage.Key }), 1, 8m));
        Assert.NotNull(session.AdvanceThroughExchange().Interval);
        var result = session.Drain();
        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(4, result.AppliedTransitions.Count);
        Assert.Equal(prefixIds, result.AppliedTransitions.Take(3)
            .Select(value => value.TransitionId));
        Assert.Equal(2, result.EffectBoundaryTranscript!.AcceptedActivations.Count);
        Assert.Equal(4, result.AppliedTransitions.Select(value => value.OperationId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
    }

    [Theory]
    [InlineData("narrated_no_state_change")]
    [InlineData("resource_delta")]
    public void LivePendingContinuation_NoStateChangeClosesWithoutDescendantActivation(
        string resultKind)
    {
        var fixture = PendingContinuationFixture();
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        var counter = new LiveIntervalIdentityCounter();
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), counter.Factory, route);
        InvokePendingOwner(session, "BindOriginalPendingContext", PendingContinuationInput(fixture));
        session.StageNextExchange(LiveBudgetBatch(fixture,
            CreateBudgetMutation(fixture.Coordinate, "turn_43:no_state_change:1",
                ResourceOperation.Damage, 1m), 0, 5m));
        var wait = session.AdvanceThroughExchange().PendingResource!;
        var output = Assert.Single(wait.RequiredOutputs);
        Assert.Equal(0m, output.MinimumAmount);
        var packet = PendingPacket(session, wait);
        var request = Assert.Single(packet["requests"]!.AsArray())!.AsObject();
        var requestId = request["requestId"]!.GetValue<string>();
        var receipt = new JsonObject
        {
            ["requestId"] = requestId,
            ["resultKind"] = resultKind,
            ["reason"] = "Accepted no-state-change continuation"
        };
        if (string.Equals(resultKind, "resource_delta", StringComparison.Ordinal))
            receipt["amount"] = 0m;

        var resumed = ResumePendingOwner(session, wait, new JsonArray(receipt));
        Assert.Null(resumed.PendingResource);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            resumed.Interval);
        var firstTransition = interval.AppliedTransitions[0];
        Assert.Equal(ResourceTransitionOperation.Damage, firstTransition.Operation);
        Assert.Equal(1m, firstTransition.RequestedAmount);
        Assert.Equal(1m, firstTransition.AppliedAmount);
        Assert.Equal(5m, firstTransition.BeforeState!.Current);
        Assert.Equal(4m, firstTransition.AfterState!.Current);
        Assert.Single(interval.AppliedTransitions);
        var accepted = Assert.Single(interval.EffectAfter.AcceptedActivations);
        Assert.Equal("pending_damage", accepted.Activation.Stamp.Identity.TriggerId);
        Assert.Equal(2, accepted.Activation.Stamp.UsesBefore);
        Assert.DoesNotContain(interval.EffectAfter.AcceptedActivations, value =>
            string.Equals(value.Activation.Stamp.Identity.TriggerId, "pending_restore",
                StringComparison.Ordinal));
        Assert.DoesNotContain(interval.EffectAfter.AppliedComponentEvidence, value =>
            value.Transition.AppliedAmount != 0m);

        var result = session.Drain();
        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(firstTransition.TransitionId,
            result.AppliedTransitions[0].TransitionId);
        Assert.Equal(new[] { requestId }, result.AcceptedResolvedPendingRequestIds);
        Assert.Single(result.EffectBoundaryTranscript!.AcceptedActivations);
        Assert.Equal(4m, result.StateAfterImage!.Entries.Single(value =>
            value.Coordinate == fixture.Coordinate).Current);
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
    }

    [Fact]
    public void LivePendingContinuation_ZeroDeltaCompletesAfterComponentWithoutPhantomReaction()
    {
        var fixture = CreateAcceptedEventBudgetFixture(
            remainingUses: 1,
            current: 5m,
            reactionResultKind: "bounded_receipt",
            reactionDependency: "after_component",
            reactionAfterComponentId: "component_budget_periodic",
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec(
                    "pending_after_component",
                    "resource_damaged",
                    Priority: 10,
                    IncludePeriodic: true,
                    IncludeReaction: true,
                    ResolutionMode: "bounded_receipt")
            });
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), new LiveIntervalIdentityCounter().Factory, route);
        InvokePendingOwner(session, "BindOriginalPendingContext", PendingContinuationInput(fixture));
        session.StageNextExchange(LiveBudgetBatch(fixture,
            CreateBudgetMutation(fixture.Coordinate,
                "turn_43:zero_after_component:1",
                ResourceOperation.Damage,
                1m),
            0,
            5m));
        var wait = session.AdvanceThroughExchange().PendingResource!;
        var packet = PendingPacket(session, wait);
        var request = Assert.Single(packet["requests"]!.AsArray())!.AsObject();
        var requestId = request["requestId"]!.GetValue<string>();

        var resumed = ResumePendingOwner(session, wait, new JsonArray(new JsonObject
        {
            ["requestId"] = requestId,
            ["resultKind"] = "resource_delta",
            ["amount"] = 0m,
            ["reason"] = "Accepted explicit zero continuation"
        }));

        Assert.Null(resumed.PendingResource);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(
            resumed.Interval);
        var transition = Assert.Single(interval.AppliedTransitions);
        Assert.Equal(ResourceTransitionOperation.Damage, transition.Operation);
        Assert.Equal(4m, transition.AfterState!.Current);
        var activation = Assert.Single(interval.EffectAfter.AcceptedActivations);
        Assert.Equal("pending_after_component", activation.Activation.Stamp.Identity.TriggerId);
        Assert.Equal(1, activation.Activation.Stamp.UsesBefore);
        Assert.Empty(interval.EffectAfter.AppliedComponentEvidence);
        Assert.Empty(interval.EffectAfter.ReleasedReactions);
        Assert.DoesNotContain(interval.Events, resourceEvent =>
            string.Equals(resourceEvent.EventKind, "resource_restored", StringComparison.Ordinal));

        var result = session.Drain();
        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Equal(new[] { requestId }, result.AcceptedResolvedPendingRequestIds);
        Assert.Single(result.AppliedTransitions);
        Assert.Empty(result.AcceptedReactionExecutions);
        Assert.Empty(result.EffectBoundaryTranscript!.ReleasedReactions);
        Assert.Equal(4m, result.StateAfterImage!.Entries.Single(value =>
            value.Coordinate == fixture.Coordinate).Current);
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("request")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    public void LivePendingContinuation_InvalidReceiptLeavesTheWaitReusable(string corruption)
    {
        var fixture = PendingContinuationFixture();
        var route = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            fixture.Plan, fixture.Owners, fixture.Definitions);
        var counter = new LiveIntervalIdentityCounter();
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            LiveBudgetBaseline(fixture), counter.Factory, route);
        InvokePendingOwner(session, "BindOriginalPendingContext", PendingContinuationInput(fixture));
        session.StageNextExchange(LiveBudgetBatch(fixture,
            CreateBudgetMutation(fixture.Coordinate, "turn_43:invalid_receipt:1",
                ResourceOperation.Damage, 1m), 0, 5m));
        var wait = session.AdvanceThroughExchange().PendingResource!;
        var packet = PendingPacket(session, wait);
        var receipts = PendingReceipts(packet, 2m);
        switch (corruption)
        {
            case "amount": receipts[0]!["amount"] = 1000m; break;
            case "request": receipts[0]!["requestId"] = "resource_resolution_foreign"; break;
            case "duplicate": receipts.Add(receipts[0]!.DeepClone()); break;
            case "missing": receipts.Clear(); break;
        }
        var allocations = counter.Calls;
        var rejected = InvokePendingOwner(session, "ResumePendingResource", wait, receipts);
        Assert.Null(PendingResultProperty(rejected, "Step"));
        Assert.NotEmpty(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            PendingResultProperty(rejected, "Issues")));
        Assert.Equal(allocations, counter.Calls);
        Assert.Null(session.Result);
        Assert.Equal(packet.ToJsonString(), PendingPacket(session, wait).ToJsonString());
        Assert.NotNull(ResumePendingOwner(session, wait,
            PendingReceipts(packet, 2m)).PendingResource);
    }

    private static AcceptedEventBudgetFixture PendingContinuationFixture() =>
        CreateAcceptedEventBudgetFixture(remainingUses: 2, current: 5m,
            triggerSpecs: new[]
            {
                new BudgetTriggerSpec("pending_damage", "resource_damaged", 10, true, false,
                    ResolutionMode: "bounded_receipt"),
                new BudgetTriggerSpec("pending_restore", "resource_restored", 20, true, false,
                    ResolutionMode: "bounded_receipt")
            });

    // Resource-layer fixture only; this does not claim spiritual source admission.
    private static AcceptedMechanicsInput PendingContinuationInput(AcceptedEventBudgetFixture fixture)
    {
        var commands = ResourceAcceptedTurnInputComposer.Parse("{}");
        Assert.True(commands.IsValid, Format(commands.Issues));
        var context = new AcceptedMechanicsPlanningContext(fixture.Definitions.ToCanonicalRoot(),
            fixture.Definitions, fixture.State, fixture.History, fixture.Owners, fixture.Sources,
            commands, fixture.Plan.IdentityIndexAfterImage, fixture.Plan);
        const string hash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        return new AcceptedMechanicsInput("session_live_pending", "request_live_pending",
            "snapshot_live_pending", "mortal_world", 43,
            new JsonObject { ["acceptedEvents"] = new JsonArray() }, commands.Root,
            new JsonObject { ["effectChanges"] = new JsonArray(),
                ["effectEventReports"] = new JsonArray(), ["effectResolutionReceipts"] = new JsonArray() },
            new JsonObject(), new JsonObject(),
            new AcceptedMechanicsAuthorityFingerprints(hash, fixture.Owners.Fingerprint,
                fixture.State.Fingerprint, fixture.History.Fingerprint, hash, hash, hash,
                hash, hash, hash, hash, hash, hash, hash, hash),
            new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal),
            Array.Empty<ValidationIssue>(), context);
    }

    private static object InvokePendingOwner(object session, string name, params object[] arguments)
    {
        var method = session.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(method != null, "The retained resource owner needs " + name + ".");
        return method!.Invoke(session, arguments)!;
    }

    private static object? PendingResultProperty(object result, string name) =>
        result.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance)!.GetValue(result);

    private static JsonObject PendingPacket(object session, object wait) =>
        Assert.IsType<JsonObject>(InvokePendingOwner(session, "ReadPendingResourceRequest", wait));

    private static JsonArray PendingReceipts(JsonObject packet, decimal amount) => new(
        packet["requests"]!.AsArray().Select(request => (JsonNode)new JsonObject
        {
            ["requestId"] = request!["requestId"]!.GetValue<string>(),
            ["resultKind"] = "resource_delta", ["amount"] = amount,
            ["reason"] = "Accepted bounded continuation"
        }).ToArray());

    private static AcceptedMechanicsPlanner.ResourceExecutionStep ResumePendingOwner(
        object session, object wait, JsonArray receipts)
    {
        var result = InvokePendingOwner(session, "ResumePendingResource", wait, receipts);
        var issues = Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            PendingResultProperty(result, "Issues"));
        Assert.True(issues.Count == 0, Format(issues));
        return Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionStep>(
            PendingResultProperty(result, "Step"));
    }
}
