using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task OriginalSpiritualCapture_SameTurnDefinitionPrecedesOrdinaryAndExchangeResources()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(spend: 1m, gain: 1m).ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var begun = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begun));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var baseline = Assert.IsType<AcceptedMechanicsResourceInput>(
            OriginalCaptureField(resources, "_originalResourceInput"));
        Assert.True(baseline.Definitions.TryResolveExact("mana", out var definition));
        Assert.NotNull(definition);
        var originalInput = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
        Assert.False(originalInput.PlanningContext!.Definitions.TryResolveExact("mana", out _));
        var pendingInput = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(resources, "_pendingInput"));
        Assert.Same(baseline.Definitions, pendingInput.PlanningContext!.Definitions);
        Assert.Equal(AcceptedMechanicsPlanFingerprints.ComputeInput(originalInput.CreateBinding()),
            AcceptedMechanicsPlanFingerprints.ComputeInput(pendingInput.CreateBinding()));
        Assert.True(resources.Routing!.OwnsDefinitions(baseline.Definitions));
        var advanced = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(advanced.Issues);
        Assert.NotNull(advanced.Step!.Interval);
        // Read the actual resource owner for this resource-only test. This does
        // not manufacture a common completion or publish an accepted game turn.
        var result = resources.Drain();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        // The definition is materialized without asking the old afterlife owner
        // catalog to acquire a new same-turn mana capability. Existing spiritual
        // action points provide genuine baseline mutations before the exchange.
        Assert.DoesNotContain(result.AppliedTransitions, value => value.Coordinate.ResourceKey == "mana");
        var ordinary = result.AppliedTransitions
            .Where(value => value.EventRef is "turn_42:resource:2" or "turn_42:resource:3")
            .OrderBy(value => value.ExecutionSequence).ToArray();
        Assert.Equal(new[] { ResourceTransitionOperation.Spend, ResourceTransitionOperation.Gain },
            ordinary.Select(value => value.Operation));
        Assert.Equal(new[] { 6m, 5m }, ordinary.Select(value => value.BeforeState!.Current));
        Assert.Equal(new[] { 5m, 6m }, ordinary.Select(value => value.AfterState!.Current));
        Assert.All(ordinary, value => Assert.Equal("spiritual_action_points", value.Coordinate.ResourceKey));
        Assert.True(ordinary.Last().ExecutionSequence < advanced.Step.Interval!.AppliedTransitions.Min(value => value.ExecutionSequence));
        Assert.Equal(2, advanced.Step.Interval.AppliedTransitions.Count);
        Assert.All(advanced.Step.Interval.AppliedTransitions, value => Assert.Equal(3m, value.AfterState!.Current));
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task OriginalSpiritualCapture_ResourceFailureRevokesOwnersWithoutPublication()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(spend: 4m, gain: 0m).ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var observed = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     AfterlifeSpiritualConflictState.StatePath, EffectAcceptedTurnPlan.CommandPath,
                     EffectIdentityState.StatePath })
            observed[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        var captured = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var begun = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begun));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(capture, "_source"));
        var retainedSource = Assert.Single(source.Sources);
        var revision = source.ContinuationRevision;
        var failed = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        Assert.Null(failed.Step);
        // Signed source admission still validates the original 6 -> 3 audit.
        // The ordinary spend succeeds first (6 -> 2); the later exchange cost
        // of three then fails in the real reducer, after the graph has advanced.
        Assert.Contains(failed.Issues, issue => issue.Code == "resource_mutation_below_minimum");
        var resourceFailure = Assert.IsType<AcceptedMechanicsResourcePlanningResult>(resources.Result);
        Assert.False(resourceFailure.IsValid);
        Assert.True(resourceFailure.Statistics.HistoryAppendCount > 0);
        Assert.Null(resourceFailure.StateAfterImage);
        Assert.Null(resourceFailure.HistoryAfterImage);
        Assert.Empty(resourceFailure.AppliedTransitions);
        Assert.Equal(revision, source.ContinuationRevision);
        Assert.False(source.Owns(retainedSource));
        Assert.False(Assert.IsType<bool>(OriginalCaptureProperty(capture, "IsCurrentOwner")));
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        foreach (var pair in observed)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
    }

    [Fact]
    public async Task OriginalSpiritualCapture_ChronologicalPrefixDiffersFromLegacyGlobalPhases()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
            OriginalCaptureDefinitionAndActionPointCommand(spend: 1m, gain: 1m).ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var fixedIssues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        Assert.Contains(fixedIssues, issue => issue.Code == "afterlife_conflict_resource_transition_mismatch");
        var captured = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var begun = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begun));
        var advanced = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(advanced.Issues);
        Assert.NotNull(advanced.Step!.Interval);
        // Owner decision 2026-09-16: ordinary prefix once, then chronological exchanges.
        // The fixed adapter keeps its legacy global phase order.
    }

    [Fact]
    public async Task OriginalSpiritualCapture_SameTurnSpiritFocusCapacityPrecedesExchange()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        // The existing accepted-turn owner composer derives this registered
        // capacity from a real soul-state update, without new owner capabilities.
        const string soulPath = "game_state/meta/soul_state.json";
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(soulPath));
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]![
            AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 2;
        await context.WriteExactJsonAsync(soulPath, soul.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var begun = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begun));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            OriginalCaptureField(capture, "_resources"));
        var advanced = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step!.Interval);
        var result = resources.Drain();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var capacity = Assert.Single(result.AppliedTransitions, value =>
            value.Operation == ResourceTransitionOperation.Reconfigure &&
            value.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
            value.Coordinate.ResourceKey == "spiritual_action_points");
        Assert.Equal(6m, capacity.BeforeState!.Maximum);
        Assert.Equal(8m, capacity.AfterState!.Maximum);
        Assert.Equal(6m, capacity.AfterState.Current);
        Assert.Equal(ResourceCapacityDisposition.ClampToNewMaximum, capacity.CapacityDisposition);
        Assert.True(capacity.ExecutionSequence < interval.AppliedTransitions.Min(value => value.ExecutionSequence));
        Assert.All(interval.AppliedTransitions, value => Assert.Equal(3m, value.AfterState!.Current));
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    private static JsonObject OriginalCaptureDefinitionAndActionPointCommand(decimal spend, decimal gain)
    {
        var command = ResourceMaterializationValidationTests.DefinitionCreationCommand();
        command["resourceDefinitionCreations"]![0]!["definition"]!["allowedOwnerKinds"] = new JsonArray("afterlife_actor");
        var changes = new JsonArray(new JsonObject
        {
            ["operation"] = "spend",
            ["target"] = new JsonObject { ["kind"] = "afterlife_actor", ["targetId"] = "player_soul" },
            ["resourceKey"] = "spiritual_action_points", ["amount"] = spend,
            ["source"] = new JsonObject { ["kind"] = "action_cost" },
            ["eventRef"] = "turn_42:resource:2", ["reason"] = "Original captured resource cost"
        });
        if (gain > 0m)
            changes.Add(new JsonObject
            {
                ["operation"] = "gain",
                ["target"] = new JsonObject { ["kind"] = "afterlife_actor", ["targetId"] = "player_soul" },
                ["resourceKey"] = "spiritual_action_points", ["amount"] = gain,
                ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                ["eventRef"] = "turn_42:resource:3", ["reason"] = "Original captured resource recovery"
            });
        command["resourceChanges"] = changes;
        return command;
    }

    private static object? OriginalCaptureField(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);

    [Fact]
    public async Task OriginalSpiritualCapture_RealEmptyEffectHandoffPrecedesResourceReduction()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var sourceControl = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(sourceControl.Issues);
        var oldSourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(sourceControl.Session);
        var oldSource = Assert.Single(oldSourceOwner.Sources);
        Assert.True(oldSourceOwner.Owns(oldSource));
        var observed = new Dictionary<string, byte[]?>();
        foreach (var path in oldSourceOwner.SelectedPaths.Append(EffectAcceptedTurnPlan.CommandPath))
            observed[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);

        var result = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);

        AssertNoConflictFrameErrors(OriginalCaptureIssues(result));
        var capture = OriginalCaptureProperty(result, "Capture");
        Assert.NotNull(capture);
        Assert.True(Assert.IsType<bool>(OriginalCaptureProperty(capture, "IsCurrentOwner")));
        Assert.False(oldSourceOwner.Owns(oldSource));
        Assert.True(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        var begin = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begin));
        var advanced = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step!.Interval);
        Assert.NotEmpty(interval.AppliedTransitions);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        foreach (var pair in observed)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease));
    }

    [Fact]
    public async Task OriginalSpiritualCapture_MissingNextExchangeDoesNotReexecuteClosedPrefix()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(captured));
        var capture = OriginalCaptureProperty(captured, "Capture")!;
        var begin = await InvokeOriginalCaptureAsync(capture, "BeginResourceExecutionAsync", lease);
        AssertNoConflictFrameErrors(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(begin));
        var first = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
            await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
        AssertNoConflictFrameErrors(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step!.Interval);
        var transitions = interval.AppliedTransitions.ToArray();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var absent = Assert.IsType<AcceptedMechanicsPlanner.ResourceContinuationResult>(
                await InvokeOriginalCaptureAsync(capture, "AdvanceNextResourceExchangeAsync", lease));
            Assert.Null(absent.Step);
            Assert.Contains(absent.Issues, issue => issue.Code == "spiritual_resource_exchange_missing");
            Assert.Equal(transitions, interval.AppliedTransitions);
            Assert.True(Assert.IsType<bool>(OriginalCaptureProperty(capture, "IsCurrentOwner")));
        }
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task OriginalSpiritualCapture_ChangedObservedInputPermanentlyRevokesCapture()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        object capture;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var result = await InvokeOriginalCaptureAsync(context.Validator,
                "CaptureSpiritualOriginalTurnAsync", lease);
            AssertNoConflictFrameErrors(OriginalCaptureIssues(result));
            capture = OriginalCaptureProperty(result, "Capture")!;
            Assert.NotNull(capture);
        }
        const string path = "game_state/meta/soul_state.json";
        var before = await context.FileSystem.ReadFileAsync(path);
        var changed = JsonNode.Parse(before!)!.AsObject();
        changed["unowned_candidate_change"] = true;
        await context.WriteExactJsonAsync(path, changed.ToJsonString());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var result = await InvokeOriginalCaptureAsync(capture, "CheckRetainedInputsAsync", lease);
            Assert.Contains(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(result),
                issue => issue.Code == "spiritual_original_input_changed");
            Assert.False(Assert.IsType<bool>(OriginalCaptureProperty(capture, "IsCurrentOwner")));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _));
        }
        await context.WriteExactJsonAsync(path, before!);
        Assert.False(Assert.IsType<bool>(OriginalCaptureProperty(capture, "IsCurrentOwner")));
    }

    [Fact]
    public async Task OriginalSpiritualCapture_NewCaptureReplacesTheOriginalOwner()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var firstResult = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(firstResult));
        var first = OriginalCaptureProperty(firstResult, "Capture")!;
        var secondResult = await InvokeOriginalCaptureAsync(context.Validator,
            "CaptureSpiritualOriginalTurnAsync", lease);
        AssertNoConflictFrameErrors(OriginalCaptureIssues(secondResult));
        var second = OriginalCaptureProperty(secondResult, "Capture")!;
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.False(Assert.IsType<bool>(OriginalCaptureProperty(first, "IsCurrentOwner")));
        Assert.True(Assert.IsType<bool>(OriginalCaptureProperty(second, "IsCurrentOwner")));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData("wounds")]
    [InlineData("party")]
    [InlineData("vehicles")]
    [InlineData("guardians")]
    [InlineData("lifecycle")]
    public async Task OriginalSpiritualCapture_PreviouslyUnstagedCommandInputStillRevokesCapture(string kind)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var path = kind switch
        {
            "wounds" => AcceptedMechanicsPlan.WoundCommandPath,
            "party" => "game_state/misc/player_interactions.json",
            "vehicles" => StorageTransportMoveService.VehiclesPath,
            "guardians" => "game_state/meta/guardians.json",
            "lifecycle" => "game_state/control/life_transitions.json",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        object capture;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var result = await InvokeOriginalCaptureAsync(context.Validator, "CaptureSpiritualOriginalTurnAsync", lease);
            AssertNoConflictFrameErrors(OriginalCaptureIssues(result));
            capture = OriginalCaptureProperty(result, "Capture")!;
            var input = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
            Assert.DoesNotContain(path, input.BeforeImages.Keys);
        }
        await context.WriteExactJsonAsync(path, new JsonObject { ["changed_after_capture"] = true }.ToJsonString());
        await using var changedLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issues = Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            await InvokeOriginalCaptureAsync(capture, "CheckRetainedInputsAsync", changedLease));
        Assert.Contains(issues, issue => issue.Code == "spiritual_original_input_changed");
        Assert.False(Assert.IsType<bool>(OriginalCaptureProperty(capture, "IsCurrentOwner")));
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, changedLease, out _));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, changedLease));
    }

    [Fact]
    public async Task OriginalSpiritualCapture_RecoveryExchangePrecedesLaterCost()
    {
        await using var context = await CreateActionPointContextAsync(3m, 3m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(
            Exchange("exchange_chronological_1",
                PlayerAudit("recover_spiritual_power", 0m, 3m, 5m),
                OppositionAudit("recover_spiritual_power", 0m, 3m, 5m)),
            Exchange("exchange_chronological_2", PlayerAudit("pressure", 3m, 5m, 2m),
                OppositionAudit("pressure", 3m, 5m, 2m)));
        var batches = AfterlifeSpiritualConflictResourceOutcome.TryCreate(
            42, fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(batches.IsValid, string.Join(Environment.NewLine, batches.Issues));
        using var resources = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            fixture.Input, new AcceptedMechanicsIdentityFactory());
        resources.StageNextExchange(batches.Exchanges[0]);
        var first = resources.AdvanceThroughExchange();
        Assert.NotNull(first.Interval);
        Assert.All(first.Interval.AppliedTransitions, value =>
        {
            Assert.Equal(ResourceTransitionOperation.Gain, value.Operation);
            Assert.Equal(5m, value.AfterState!.Current);
        });
        resources.StageNextExchange(batches.Exchanges[1]);
        var second = resources.AdvanceThroughExchange();
        Assert.NotNull(second.Interval);
        Assert.All(second.Interval.AppliedTransitions, value =>
        {
            Assert.Equal(ResourceTransitionOperation.Spend, value.Operation);
            Assert.Equal(5m, value.BeforeState!.Current);
            Assert.Equal(2m, value.AfterState!.Current);
        });
        var result = resources.Drain();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Empty(batches.Draft!.Project(result).Issues);
    }

    private static object InvokeOriginalCapture(object owner, string methodName, params object[] arguments)
    {
        var method = owner.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(method != null, "Actual original-turn capture needs " + methodName + ".");
        return method!.Invoke(owner, arguments)!;
    }

    private static async Task<object> InvokeOriginalCaptureAsync(
        object owner, string methodName, params object[] arguments)
    {
        var task = Assert.IsAssignableFrom<Task>(InvokeOriginalCapture(owner, methodName, arguments));
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static object? OriginalCaptureProperty(object owner, string name) =>
        owner.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(owner);

    private static IReadOnlyList<ValidationIssue> OriginalCaptureIssues(object result) =>
        Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(OriginalCaptureProperty(result, "Issues"));
}
