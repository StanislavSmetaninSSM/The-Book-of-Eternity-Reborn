using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task SpiritualExchangeInterval_ProductionOutcomeBatchesPreserveBothSideChains()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(
            Exchange("exchange_interval_1", PlayerAudit("pressure", 3m, 6m, 3m),
                OppositionAudit("guard", 2m, 6m, 4m)),
            Exchange("exchange_interval_2", PlayerAudit("guard", 1m, 3m, 2m),
                OppositionAudit("guard", 1m, 4m, 3m)));
        var prepared = AfterlifeSpiritualConflictResourceOutcome.TryCreate(
            42, fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(prepared.IsValid, string.Join(Environment.NewLine, prepared.Issues));
        Assert.Equal(2, prepared.Exchanges.Count);
        var firstBatch = prepared.Exchanges[0];
        var secondBatch = prepared.Exchanges[1];
        Assert.All(secondBatch.Mutations, mutation => Assert.Contains(
            firstBatch.Mutations, first => first.Coordinate == mutation.Coordinate &&
                mutation.Dependencies.Contains(first.Key)));
        using var session = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            fixture.Input, new AcceptedMechanicsIdentityFactory());
        session.StageNextExchange(firstBatch);
        var first = session.AdvanceThroughExchange().Interval!;
        Assert.Equal(2, first.AppliedTransitions.Count);
        Assert.Equal(new AcceptedMechanicsPlanner.ResourceOrdinalRange(0, 2), first.AppliedRange);
        Assert.Equal(2, first.AppliedTransitions.Select(value => value.Coordinate).Distinct().Count());
        session.StageNextExchange(secondBatch);
        var second = session.AdvanceThroughExchange().Interval!;
        Assert.Equal(new AcceptedMechanicsPlanner.ResourceOrdinalRange(2, 4), second.AppliedRange);
        var result = session.Drain();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Empty(prepared.Draft!.Project(result).Issues);
        Assert.Equal(fixture.Before.ToJsonString(),
            Assert.IsType<JsonObject>(await context.ReadJsonAsync(
                AfterlifeSpiritualConflictState.StatePath)).ToJsonString());
        Assert.Equal(fixture.Input.State.ToCanonicalJson(),
            (await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath))!.Trim());
    }

    [Fact]
    public async Task SpiritualExchangeInterval_ExplicitZeroAuditsAreDifferentFromMissingAudits()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(Exchange(
            "exchange_interval_zero",
            PlayerAudit("recover_spiritual_power", 0m, 6m, 6m),
            OppositionAudit("recover_spiritual_power", 0m, 6m, 6m)));
        var prepared = AfterlifeSpiritualConflictResourceOutcome.TryCreate(
            42, fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(prepared.IsValid);
        Assert.Null(prepared.Draft);
        var batch = Assert.Single(prepared.Exchanges);
        Assert.Equal(AfterlifeSpiritualConflictResourceOutcome.SideEvaluation.EvaluatedZero, batch.Player);
        Assert.Equal(AfterlifeSpiritualConflictResourceOutcome.SideEvaluation.EvaluatedZero, batch.Opposition);
        var calls = 0;
        using var zeroSession = AcceptedMechanicsPlanner.BeginLiveResourceExecution(fixture.Input,
            new AcceptedMechanicsIdentityFactory(() => { calls++; return Guid.NewGuid(); }));
        zeroSession.StageNextExchange(batch);
        Assert.Empty(zeroSession.AdvanceThroughExchange().Interval!.AppliedTransitions);
        Assert.Equal(0, calls);
        Assert.True(zeroSession.Drain().IsValid);
        accepted["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!.AsObject().Remove("opposition");
        var missing = AfterlifeSpiritualConflictResourceOutcome.TryCreate(
            42, fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(missing.IsValid);
        Assert.Equal(new[] { "opposition" }, Assert.Single(missing.Exchanges).PendingSides);
        using var missingSession = AcceptedMechanicsPlanner.BeginLiveResourceExecution(
            fixture.Input, new AcceptedMechanicsIdentityFactory());
        missingSession.StageNextExchange(missing.Exchanges[0]);
        var pending = missingSession.AdvanceThroughExchange();
        Assert.Null(pending.Interval);
        Assert.NotNull(pending.PendingExchange);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SpiritualExchangeInterval_StartAndTerminalKeepExplicitResourcePrerequisites(bool start)
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var absent = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var prepared = AfterlifeSpiritualConflictResourceOutcome.TryCreate(42,
            start ? absent : fixture.Before, start ? fixture.Before : absent,
            fixture.Owners, fixture.Input.State);
        Assert.True(prepared.IsValid);
        Assert.Null(prepared.Draft);
        Assert.Empty(prepared.Exchanges);
        Assert.Equal(start
                ? AfterlifeSpiritualConflictResourceOutcome.ContourRequirement.StartResourceInitialization
                : AfterlifeSpiritualConflictResourceOutcome.ContourRequirement.TerminalResourceClosure,
            Assert.Single(prepared.PendingRequirements));
    }

    [Fact]
    public async Task SpiritualExchangeInterval_MissingWholeAuditCannotSupplyEvaluatedEmptyEvidence()
    {
        await using var context = await CreateActionPointContextAsync(6m, 6m);
        var fixture = await ReadIntervalBaselineAsync(context);
        var accepted = fixture.Before.DeepClone().AsObject();
        accepted["activeConflict"]!["exchangeLog"] = new JsonArray(
            new JsonObject { ["exchangeId"] = "exchange_interval_missing_audit" });
        var result = AfterlifeSpiritualConflictResourceOutcome.TryCreate(
            42, fixture.Before, accepted, fixture.Owners, fixture.Input.State);
        Assert.True(result.IsValid);
        Assert.Null(result.Draft);
        Assert.Equal(new[] { "player", "opposition" }, Assert.Single(result.Exchanges).PendingSides);
    }

    private static async Task<IntervalBaseline> ReadIntervalBaselineAsync(ResourceMaterializationTestContext context)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions, allowMissingPristine: false).Ledger!;
        var history = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions, allowMissingPristine: false).History!;
        var before = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
        var roots = new AfterlifeResourceOwnerRoots(profiles, before, soul);
        var owners = AfterlifeResourceOwnerComposer.Compose(new AfterlifeResourceOwnerCompositionInput(
            definitions, roots, roots));
        Assert.True(owners.IsValid, string.Join(Environment.NewLine, owners.Issues));
        var catalog = ResourceMutationSourceCatalog.Create(Array.Empty<ResourceMutationSourceExport>());
        Assert.True(catalog.IsValid);
        return new IntervalBaseline(before, owners.Authority!, new AcceptedMechanicsResourceInput(
            42, definitions, state, history, catalog.Catalog!, Array.Empty<ResourceMutationIntent>()));
    }

    private sealed record IntervalBaseline(
        JsonObject Before, ResourceOwnerAuthority Owners, AcceptedMechanicsResourceInput Input);
}
