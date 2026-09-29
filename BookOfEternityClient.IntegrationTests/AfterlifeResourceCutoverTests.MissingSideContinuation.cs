using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task MissingSideContinuation_AliasCollisionRejectsBeforeAllocation()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(Exchange("exchange_resume_collision",
            PlayerAudit("pressure", 2m, 6m, 4m), OppositionAudit("guard", 1m, 6m, 5m)));
        var good = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var checkedSource = Assert.Single(good.Sources);
        using var hash = new ResourceFingerprintBuilder("spiritual-missing-side-source-v1");
        hash.Append(checkedSource.SourceKind);
        hash.Append(checkedSource.SourceId);
        hash.Append(checkedSource.AuthorityFingerprint);
        hash.Append("opposition");
        hash.Append(0);
        var alias = "spiritual_resume_" + hash.Build().Replace("sha256:", "", StringComparison.Ordinal);
        var reserved = ResourceMutationSourceCatalog.Create(new[] { checkedSource with { SourceId = alias } });
        Assert.True(reserved.IsValid);
        var input = new AcceptedMechanicsResourceInput(42, fixture.Input.Definitions,
            fixture.Input.State, fixture.Input.History, reserved.Catalog!, Array.Empty<ResourceMutationIntent>());
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        var initial = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var calls = 0;
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        session.StageNextExchange(initial);
        var wait = session.AdvanceThroughExchange().PendingExchange!;
        Assert.NotNull(wait);
        var beforeResume = calls;
        var rejected = MissingResume(session, wait, good);
        Assert.Null(rejected.Step);
        Assert.Contains(rejected.Issues, issue => issue.Code == "resource_missing_side_alias_collision");
        Assert.Equal(beforeResume, calls);
        Assert.NotEmpty(MissingResume(session, wait, good).Issues);
        Assert.Equal(beforeResume, calls);
    }

    [Fact]
    public async Task MissingSideContinuation_RetainsKnownMutationAndRemapsNextDependency()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(Exchange("exchange_resume_1",
            PlayerAudit("pressure", 2m, 6m, 4m), OppositionAudit("guard", 1m, 6m, 5m)));
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        var initial = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(initial.IsValid);
        var calls = 0;
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        session.StageNextExchange(Assert.Single(initial.Exchanges));
        var wait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(
            session.AdvanceThroughExchange().PendingExchange);
        // OLD reaches this real waiting control, then fails the new API assertion.
        Assert.Equal(new[] { "opposition" }, wait.MissingAuditSides);
        Assert.True(calls > 0);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"] =
            OppositionAudit("guard", 1m, 6m, 5m);
        var complete = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(complete.IsValid);
        var first = MissingResume(session, wait, Assert.Single(complete.Exchanges));
        Assert.Empty(first.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(first.Step!.Interval);
        Assert.Equal(2, interval.AppliedTransitions.Count);
        Assert.Empty(interval.ReplayTransitions);
        var oldTransition = Assert.Single(interval.AppliedTransitions, t => t.EventRef.EndsWith(":player"));
        var newTransition = Assert.Single(interval.AppliedTransitions, t => t.EventRef.EndsWith(":opposition"));
        Assert.Equal("exchange_resume_1", oldTransition.OriginId);
        Assert.StartsWith("spiritual_resume_", newTransition.OriginId);
        var beforeStale = calls;
        Assert.NotEmpty(MissingResume(session, wait, complete.Exchanges[0]).Issues);
        Assert.Equal(beforeStale, calls);
        accepted["activeConflict"]!["exchangeLog"]!.AsArray().Add(Exchange("exchange_resume_2",
            PlayerAudit("guard", 1m, 4m, 3m), OppositionAudit("guard", 1m, 5m, 4m)));
        var next = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(next.IsValid);
        session.StageNextExchange(next.Exchanges[1]);
        Assert.Equal(2, session.AdvanceThroughExchange().Interval!.AppliedTransitions.Count);
        var final = session.Drain();
        Assert.True(final.IsValid, string.Join(Environment.NewLine, final.Issues));
        Assert.Equal(4, final.AppliedTransitions.Count);
        Assert.Single(final.AppliedTransitions, t => t.EventRef == oldTransition.EventRef);
        Assert.Contains(oldTransition, final.AppliedTransitions);
        Assert.Contains(newTransition, final.AppliedTransitions);
    }

    [Fact]
    public async Task MissingSideContinuation_TwoMissingSidesThenExplicitZeroClosesOriginalInterval()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(new JsonObject
            { ["exchangeId"] = "exchange_resume_zero" });
        var initial = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        var calls = 0;
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        session.StageNextExchange(Assert.Single(initial.Exchanges));
        var firstWait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(
            session.AdvanceThroughExchange().PendingExchange);
        Assert.Equal(0, calls);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"] = new JsonObject
            { ["player"] = PlayerAudit("pressure", 2m, 6m, 4m) };
        var player = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        var partial = MissingResume(session, firstWait, Assert.Single(player.Exchanges));
        Assert.Empty(partial.Issues);
        Assert.Null(partial.Step!.Interval);
        var secondWait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(partial.Step.PendingExchange);
        Assert.Equal(new[] { "opposition" }, secondWait.MissingAuditSides);
        var afterPlayer = calls;
        Assert.True(afterPlayer > 0);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"] =
            OppositionAudit("recover_spiritual_power", 0m, 6m, 6m);
        var both = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.NotEmpty(MissingResume(session, firstWait, both.Exchanges[0]).Issues);
        Assert.Equal(afterPlayer, calls);
        var closed = MissingResume(session, secondWait, both.Exchanges[0]);
        Assert.Empty(closed.Issues);
        Assert.Equal(afterPlayer, calls);
        var interval = closed.Step!.Interval!;
        Assert.Equal(new AcceptedMechanicsPlanner.ResourceOrdinalRange(0, 1), interval.AppliedRange);
        Assert.Single(interval.AppliedTransitions);
        Assert.Empty(interval.ReplayTransitions);
        Assert.True(session.Drain().IsValid);
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("mutation")]
    [InlineData("source")]
    [InlineData("coordinate")]
    [InlineData("stale")]
    public async Task MissingSideContinuation_InvalidCompletionDoesNotAllocateAndSameWaitCanRepair(string change)
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(Exchange("exchange_resume_reject",
            PlayerAudit("pressure", 2m, 6m, 4m), OppositionAudit("guard", 1m, 6m, 5m)));
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        var initial = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        var calls = 0;
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        session.StageNextExchange(Assert.Single(initial.Exchanges));
        var wait = session.AdvanceThroughExchange().PendingExchange!;
        Assert.NotNull(wait);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"] =
            OppositionAudit("guard", 1m, 6m, 5m);
        var good = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var bad = good;
        if (change == "raw")
        {
            var changed = accepted.DeepClone().AsObject();
            changed["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"] =
                PlayerAudit("guard", 2m, 6m, 4m);
            bad = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
                fixture.Before, changed, fixture.Owners, fixture.Input.State).Exchanges);
        }
        else if (change != "stale")
        {
            var mutations = good.Mutations.ToArray();
            var sources = good.Sources.ToArray();
            if (change == "mutation") mutations[0] = mutations[0] with { Amount = 1m };
            if (change == "coordinate") mutations[0] = mutations[0] with { Coordinate = mutations[1].Coordinate };
            if (change == "source") sources[0] = sources[0] with { AuthorityFingerprint = "sha256:forged" };
            bad = new AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch(good.ConflictId,
                good.ExchangeId, good.Ordinal, good.Player, good.Opposition, sources, mutations,
                Array.Empty<AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition>());
        }
        var beforeRejected = calls;
        var rejected = MissingResume(session, change == "stale" ? wait with { } : wait, bad);
        Assert.Null(rejected.Step);
        Assert.NotEmpty(rejected.Issues);
        Assert.Equal(beforeRejected, calls);
        var repaired = MissingResume(session, wait, good);
        Assert.Empty(repaired.Issues);
        Assert.Equal(2, repaired.Step!.Interval!.AppliedTransitions.Count);
        Assert.True(session.Drain().IsValid);
    }

    [Theory]
    [InlineData("remapped")]
    [InlineData("merged")]
    public async Task MissingSideContinuation_DerivedBatchCannotLaunderProducerProvenance(string route)
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(
            Exchange("exchange_provenance_1", PlayerAudit("guard", 1m, 6m, 5m),
                OppositionAudit("guard", 1m, 6m, 5m)),
            Exchange("exchange_provenance_2", PlayerAudit("guard", 1m, 5m, 4m),
                OppositionAudit("guard", 1m, 5m, 4m)));
        accepted["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!.AsObject().Remove("opposition");
        var initial = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(initial.IsValid);
        var calls = 0;
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        session.StageNextExchange(initial.Exchanges[0]);
        Assert.NotNull(session.AdvanceThroughExchange().Interval);
        session.StageNextExchange(initial.Exchanges[1]);
        var wait = session.AdvanceThroughExchange().PendingExchange!;
        Assert.NotNull(wait);
        accepted["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"] =
            OppositionAudit("guard", 1m, 5m, 4m);
        var checkedCompletion = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges[1];
        var opposition = Assert.Single(checkedCompletion.Mutations, m => m.EventRef.EndsWith(":opposition"));
        var wrongPredecessor = Assert.Single(initial.Exchanges[0].Mutations, m => m.EventRef.EndsWith(":player")).Key;
        var forgedAliases = new Dictionary<ResourceOperationKey, ResourceOperationKey>
            { [Assert.Single(opposition.Dependencies)] = wrongPredecessor };
        AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch forged;
        if (route == "remapped")
        {
            forged = Assert.IsType<AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch>(
                InvokeMissing(checkedCompletion, "RemapDependencies", forgedAliases));
        }
        else
        {
            // Even a producer-built extension made with a caller map is execution
            // data. Its Merged batch must never become new raw producer evidence.
            var extension = InvokeMissing(initial.Exchanges[1], "PrepareMissingSide",
                checkedCompletion, initial.Exchanges[1], forgedAliases);
            Assert.NotNull(extension);
            forged = Assert.IsType<AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch>(
                MissingProperty(extension, "Merged"));
        }
        Assert.Equal(wrongPredecessor, Assert.Single(Assert.Single(forged.Mutations,
            m => m.EventRef.EndsWith(":opposition")).Dependencies));
        var beforeRejected = calls;
        var rejected = MissingResume(session, wait, forged);
        Assert.Null(rejected.Step);
        Assert.NotEmpty(rejected.Issues);
        Assert.Equal(beforeRejected, calls);
        var repaired = MissingResume(session, wait, checkedCompletion);
        Assert.Empty(repaired.Issues);
        Assert.Equal(2, repaired.Step!.Interval!.AppliedTransitions.Count);
        Assert.True(session.Drain().IsValid);
    }

    [Fact]
    public async Task MissingSideContinuation_TwoNonzeroFillsThenLaterMissingWaitKeepRawProducerEvidence()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(new JsonObject
            { ["exchangeId"] = "exchange_raw_1" });
        var initial = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var calls = 0;
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        session.StageNextExchange(initial);
        var firstWait = session.AdvanceThroughExchange().PendingExchange!;
        Assert.NotNull(firstWait);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"] = new JsonObject
            { ["player"] = PlayerAudit("pressure", 2m, 6m, 4m) };
        var player = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var partial = MissingResume(session, firstWait, player);
        Assert.Empty(partial.Issues);
        var secondWait = Assert.IsType<AcceptedMechanicsPlanner.PendingSpiritualExchange>(partial.Step!.PendingExchange);
        Assert.Equal(new[] { "opposition" }, secondWait.MissingAuditSides);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"] =
            OppositionAudit("guard", 1m, 6m, 5m);
        var complete = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var firstClosed = MissingResume(session, secondWait, complete);
        Assert.Empty(firstClosed.Issues);
        var retainedPrefix = firstClosed.Step!.Interval!.AppliedTransitions.ToArray();
        Assert.Equal(2, retainedPrefix.Length);
        Assert.All(retainedPrefix, transition => Assert.StartsWith("spiritual_resume_", transition.OriginId));

        accepted["activeConflict"]!["exchangeLog"]!.AsArray().Add(Exchange("exchange_raw_2",
            PlayerAudit("guard", 1m, 4m, 3m), OppositionAudit("guard", 1m, 5m, 4m)));
        accepted["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!.AsObject().Remove("opposition");
        var laterRaw = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges[1];
        // The session remaps this batch's predecessor before execution. The
        // mapped execution copy must not replace its retained raw producer batch.
        session.StageNextExchange(laterRaw);
        var laterWait = session.AdvanceThroughExchange().PendingExchange!;
        Assert.NotNull(laterWait);
        accepted["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"] =
            OppositionAudit("guard", 1m, 5m, 4m);
        var laterComplete = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges[1];
        var beforeStale = calls;
        Assert.NotEmpty(MissingResume(session, secondWait, laterComplete).Issues);
        Assert.Equal(beforeStale, calls);
        var laterClosed = MissingResume(session, laterWait, laterComplete);
        Assert.Empty(laterClosed.Issues);
        Assert.Equal(2, laterClosed.Step!.Interval!.AppliedTransitions.Count);
        var final = session.Drain();
        Assert.True(final.IsValid, string.Join(Environment.NewLine, final.Issues));
        Assert.Equal(4, final.AppliedTransitions.Count);
        Assert.Equal(retainedPrefix, final.AppliedTransitions.Take(2));
        Assert.Empty(final.ReplayTransitions);
        Assert.Equal(4, final.AppliedTransitions.Select(t => t.OperationId).Distinct().Count());
    }

    [Fact]
    public async Task MissingSideContinuation_PreparationIsInspectableOwnedAndSingleUse()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(Exchange("exchange_prepared",
            PlayerAudit("pressure", 2m, 6m, 4m), OppositionAudit("guard", 1m, 6m, 5m)));
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        var initial = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var calls = 0;
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        session.StageNextExchange(initial);
        var wait = session.AdvanceThroughExchange().PendingExchange!;
        Assert.NotNull(wait);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["opposition"] =
            OppositionAudit("guard", 1m, 6m, 5m);
        var completion = Assert.Single(AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State).Exchanges);
        var beforePrepare = calls;
        var first = MissingPreparation(session, wait, completion);
        Assert.Same(completion, MissingProperty(first, "CheckedCompletion"));
        var aliases = Assert.IsAssignableFrom<IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey>>(
            MissingProperty(first, "Aliases"));
        Assert.Single(aliases);
        Assert.NotEmpty(Assert.IsAssignableFrom<IReadOnlyList<ResourceMutationSourceExport>>(
            MissingProperty(first, "CheckedSources")));
        Assert.Equal(beforePrepare, calls);
        var current = MissingPreparation(session, wait, completion);
        Assert.NotSame(first, current);
        var stale = MissingCommit(session, first);
        Assert.Null(stale.Step);
        Assert.NotEmpty(stale.Issues);
        Assert.Equal(beforePrepare, calls);
        using var foreign = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        var foreignRejected = MissingCommit(foreign, current);
        Assert.Null(foreignRejected.Step);
        Assert.NotEmpty(foreignRejected.Issues);
        Assert.Equal(beforePrepare, calls);
        var committed = MissingCommit(session, current);
        Assert.Empty(committed.Issues);
        var newTransition = Assert.Single(committed.Step!.Interval!.AppliedTransitions,
            t => t.EventRef.EndsWith(":opposition"));
        Assert.Equal(Assert.Single(aliases).Value.OriginId, newTransition.OriginId);
        var afterCommit = calls;
        Assert.NotEmpty(MissingCommit(session, current).Issues);
        Assert.Equal(afterCommit, calls);
        Assert.True(session.Drain().IsValid);
    }

    [Fact]
    public async Task MissingSideContinuation_OneFrozenProducerContextPerComposition()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(
            Exchange("exchange_snapshot_1", PlayerAudit("guard", 1m, 6m, 5m),
                OppositionAudit("guard", 1m, 6m, 5m)),
            Exchange("exchange_snapshot_2", PlayerAudit("guard", 1m, 5m, 4m),
                OppositionAudit("guard", 1m, 5m, 4m)));
        var produced = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(produced.IsValid);
        var firstImage = MissingProducerImage(produced.Exchanges[0]);
        var secondImage = MissingProducerImage(produced.Exchanges[1]);
        var shared = MissingProperty(firstImage, "Context");
        Assert.Same(shared, MissingProperty(secondImage, "Context"));
        Assert.NotEqual(MissingProperty(firstImage, "Index"), MissingProperty(secondImage, "Index"));
        var frozenCandidate = Assert.IsType<JsonObject>(MissingProperty(shared, "Candidate"));
        var frozenBefore = Assert.IsType<JsonObject>(MissingProperty(shared, "Before"));
        Assert.NotSame(accepted, frozenCandidate);
        Assert.NotSame(fixture.Before, frozenBefore);
        var expected = frozenCandidate.ToJsonString();
        accepted["activeConflict"]!["exchangeLog"]!.AsArray().Clear();
        Assert.Equal(expected, frozenCandidate.ToJsonString());
    }

    private static object InvokeMissing(object target, string name, params object[] arguments)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var method = target.GetType().GetMethod(name, flags);
        Assert.True(method != null, "The missing-side owner needs " + name + ".");
        return method!.Invoke(target, arguments)!;
    }

    private static object MissingProperty(object value, string name) => value.GetType()
        .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(value)!;

    private static object MissingProducerImage(AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch batch)
    {
        var field = typeof(AfterlifeSpiritualConflictResourceOutcome)
            .GetField("ProducerImages", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var table = field!.GetValue(null)!;
        var arguments = new object?[] { batch, null };
        Assert.True((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments)!);
        return arguments[1]!;
    }

    private static object MissingPreparation(object session, object wait, object completion)
    {
        var result = InvokeMissing(session, "PrepareMissingAuditSide", wait, completion);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(MissingProperty(result, "Issues")));
        var prepared = MissingProperty(result, "Preparation");
        Assert.NotNull(prepared);
        return prepared;
    }

    private static (AcceptedMechanicsPlanner.ResourceExecutionStep? Step, IReadOnlyList<ValidationIssue> Issues)
        MissingCommit(object session, object preparation)
    {
        var result = InvokeMissing(session, "CommitMissingAuditSide", preparation);
        return ((AcceptedMechanicsPlanner.ResourceExecutionStep?)MissingProperty(result, "Step"),
            Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(MissingProperty(result, "Issues")));
    }

    private static (AcceptedMechanicsPlanner.ResourceExecutionStep? Step, IReadOnlyList<ValidationIssue> Issues)
        MissingResume(AcceptedMechanicsPlanner.ResourceExecutionSession session,
            AcceptedMechanicsPlanner.PendingSpiritualExchange pending,
            AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch batch)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var method = session.GetType().GetMethod("ResumeMissingAuditSide", flags);
        Assert.NotNull(method);
        var result = method!.Invoke(session, new object[] { pending, batch })!;
        return ((AcceptedMechanicsPlanner.ResourceExecutionStep?)result.GetType().GetProperty("Step", flags)!.GetValue(result),
            (IReadOnlyList<ValidationIssue>)result.GetType().GetProperty("Issues", flags)!.GetValue(result)!);
    }
}
