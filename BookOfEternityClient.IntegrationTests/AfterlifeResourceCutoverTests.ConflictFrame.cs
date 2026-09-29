using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static int _defaultConflictFramePreparationCount;
    [Fact]
    public async Task ConflictFrame_SignedPressureExchangePublishesWithNoOwningPhaseErrors()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);

        var rawIssues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoConflictFrameErrors(rawIssues);
        var plan = await PeekPlanAsync(context);
        Assert.Equal(3m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(3m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeConflictSide).Current);

        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Same(plan, await context.Normalizer.BindTo(lease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        var published = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        Assert.False(published.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        var active = Assert.IsType<JsonObject>(published["activeConflict"]);
        Assert.Equal("strained", active["oppositionSideStrain"]!.GetValue<string>());
        var exchange = Assert.IsType<JsonObject>(Assert.Single(active["exchangeLog"]!.AsArray()));
        Assert.Equal("exchange_conflict_frame_42", exchange["exchangeId"]!.GetValue<string>());
        Assert.Equal(10, exchange["diceAudit"]!["margin"]!.GetValue<int>());

        var finalIssues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));
        AssertNoConflictFrameErrors(finalIssues);
    }

    [Fact]
    public async Task ConflictFrame_FinalValidationRejectsDieOutsideSignedPool()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var update = Assert.IsType<JsonObject>(root[AfterlifeSpiritualConflictState.ResponseField]);
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);
        projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
        var exchange = projected["activeConflict"]!["exchangeLog"]![0]!;
        exchange["diceAudit"]!["diceUsed"]![0]!["value"] = 14;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, projected.ToJsonString());
        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "afterlife_conflict_dice_value_not_authorized");
    }

    /// <summary>
    /// Rejects validation errors while retaining their expected and actual source evidence in the failure message.
    /// </summary>
    /// <param name="issues">
    /// Validation results whose error diagnostics must be absent.
    /// </param>
    private static void AssertNoConflictFrameErrors(IEnumerable<ValidationIssue> issues)
    {
        var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine,
            errors.Select(issue => $"{issue.Code}: {issue}; expected={issue.Expected}; actual={issue.Actual}")));
    }

    /// <summary>
    /// Seeds a signed spiritual conflict with optional upstream baselines fixed before snapshot creation.
    /// </summary>
    /// <param name="hooks">
    /// Optional filesystem hooks for the fixture's real canonical reads and writes.
    /// </param>
    /// <param name="seedOriginalInputs">
    /// Optional baseline writer run before composing authority and signing the original snapshot.
    /// The two exact named intake seeders may reuse completed signed bytes; arbitrary callbacks run for each fixture.
    /// </param>
    /// <param name="signedDice">
    /// Optional exact ordered die pool signed into this test context; <see langword="null"/> uses 15, 5, 12, 8.
    /// </param>
    /// <param name="playerCurrent">
    /// Initial player action points; defaults to the ordinary maximum of six.
    /// </param>
    /// <param name="oppositionCurrent">
    /// Initial opposition action points; defaults to the ordinary maximum of six.
    /// </param>
    /// <param name="initializeContext">
    /// Optional base-session seeding before any spiritual original state is written.
    /// </param>
    /// <param name="captureOriginalSnapshot">
    /// Optional real lifecycle signer after original authority is complete; <see langword="null"/> uses the existing fixture signer.
    /// </param>
    /// <param name="bypassPreparedFixture">
    /// Builds a cacheable signed profile rather than copying it; used only by the fixture cache factories.
    /// </param>
    /// <returns>
    /// Owned filesystem context with one authenticated original pending snapshot.
    /// </returns>
    private static async Task<ResourceMaterializationTestContext> CreateCompleteConflictFrameContextAsync(
        FileSystemManagerHooks? hooks = null,
        Func<ResourceMaterializationTestContext, Task>? seedOriginalInputs = null,
        int[]? signedDice = null,
        decimal playerCurrent = 6m,
        decimal oppositionCurrent = 6m,
        Func<ResourceMaterializationTestContext, Task>? initializeContext = null,
        Func<ResourceMaterializationTestContext, Task>? captureOriginalSnapshot = null,
        bool bypassPreparedFixture = false)
    {
        var trustedKey = ReadTrustedConflictFrameKey(hooks, seedOriginalInputs, signedDice,
            playerCurrent, oppositionCurrent, initializeContext, captureOriginalSnapshot);
        var defaultFrame = trustedKey is { SeedVersion: "default_v1", OrderedDice: "15,5,12,8",
            PlayerCurrent: 6m, OppositionCurrent: 6m };
        var originalIntakeFrame = trustedKey is { SeedVersion: "original_intake_v1", OrderedDice: "15,5,12,8",
            PlayerCurrent: 6m, OppositionCurrent: 6m };
        if (trustedKey is not null && !bypassPreparedFixture)
        {
            var template = defaultFrame ? DefaultConflictFrameTemplate : originalIntakeFrame
                ? OriginalIntakeConflictFrameTemplate
                : TrustedConflictFrameTemplates.GetOrAdd(trustedKey, static key =>
                    new Lazy<Task<PreparedFixtureTree>>(() => PrepareTrustedConflictFrameTemplateAsync(key),
                        LazyThreadSafetyMode.ExecutionAndPublication));
            var prepared = await template.Value;
            var copy = await ResourceMaterializationTestContext.CreateAsync();
            try
            {
                prepared.Materialize(copy.FileSystem.GameSessionPath);
                return copy;
            }
            catch
            {
                await copy.DisposeAsync();
                throw;
            }
        }
        if (defaultFrame)
            Interlocked.Increment(ref _defaultConflictFramePreparationCount);
        if (originalIntakeFrame)
            Interlocked.Increment(ref _originalIntakeConflictFramePreparationCount);
        Interlocked.Increment(ref _uncachedConflictFramePreparationCount);
        if (trustedKey is not null)
            TrustedConflictFramePreparations.AddOrUpdate(trustedKey, 1, static (_, count) => count + 1);
        var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        if (initializeContext != null)
            await initializeContext(context);
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        var arts = new JsonObject();
        foreach (var artId in AfterlifeEntityProfileState.StandardArtIds.OrderBy(id => id, StringComparer.Ordinal))
            arts[artId] = 0;
        var player = PlayerSoulProfile();
        player["standardArts"] = arts.DeepClone();
        player["specialArts"] = new JsonArray();
        var guardian = GuardianProfile("guardian_frame");
        guardian["standardArts"] = arts.DeepClone();
        guardian["specialArts"] = new JsonArray();
        var profiles = Profiles(player, guardian);
        var soul = SoulState(spiritFocusTier: 0);
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"] = arts.DeepClone();
        var conflict = ActiveConflict("conflict_resource_cost");
        conflict["sideModel"] = "direct_duel";
        conflict["playerSideStrain"] = "clear";
        conflict["oppositionSideStrain"] = "clear";
        conflict["conflictPosition"] = "contested";
        conflict["playerSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "player",
                ["actorId"] = "player_soul",
                ["displayName"] = "Душа игрока"
            },
            ["supporters"] = new JsonArray()
        };
        conflict["oppositionSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_frame",
                ["displayName"] = "Хранитель",
                ["actorArtTierSnapshot"] = arts.DeepClone(),
                ["artAuthoritySource"] = "afterlife_entity_profiles"
            },
            ["supporters"] = new JsonArray()
        };
        var (state, history) = BuildActionPointState(definitions, profiles, conflict, soul, playerCurrent, oppositionCurrent);
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.DefinitionsPath, definitions.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.StatePath, state.ToCanonicalJson());
        await context.WriteExactJsonAsync(ResourceMaterializationTestContext.HistoryPath, history.ToCanonicalJson());
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = conflict,
            ["recentConflicts"] = new JsonArray()
        }.ToJsonString());
        if (seedOriginalInputs != null)
            await seedOriginalInputs(context);
        await WriteComposedAuthorityAsync(context, definitions, state, history);
        if (captureOriginalSnapshot != null)
            await captureOriginalSnapshot(context);
        else
            await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: signedDice ?? [15, 5, 12, 8]);
        return context;
    }

    private static async Task WriteCompleteConflictFrameExchangeAsync(ResourceMaterializationTestContext context)
    {
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var before = new JsonObject
        {
            ["playerSideStrain"] = "clear",
            ["oppositionSideStrain"] = "clear",
            ["conflictPosition"] = "contested"
        };
        var after = before.DeepClone().AsObject();
        after["oppositionSideStrain"] = "strained";
        var exchange = new JsonObject
        {
            ["exchangeId"] = "exchange_conflict_frame_42",
            ["turnNumber"] = 42,
            ["operationType"] = "pressure",
            ["outcome"] = "success",
            ["before"] = before,
            ["after"] = after,
            ["matchupAudit"] = new JsonObject
            {
                ["playerOperation"] = "pressure",
                ["oppositionOperation"] = "pressure",
                ["primaryResolutionLane"] = "pressure",
                ["matchupRationale"] = "Давление души преодолевает встречное давление хранителя.",
                ["riskProfile"] = "offensive_pressure"
            },
            ["actionCostAudit"] = new JsonObject
            {
                ["player"] = CostAudit("pressure", 3m, 6m, 3m),
                ["opposition"] = CostAudit("pressure", 3m, 6m, 3m)
            },
            ["diceAudit"] = new JsonObject
            {
                ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
                ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
                ["diceUsed"] = new JsonArray
                {
                    new JsonObject { ["side"] = "player", ["sourceIndex"] = 0, ["sides"] = 20, ["value"] = 15 },
                    new JsonObject { ["side"] = "opposition", ["sourceIndex"] = 1, ["sides"] = 20, ["value"] = 5 }
                },
                ["playerTotal"] = 15,
                ["oppositionTotal"] = 5,
                ["margin"] = 10,
                ["outcomeBand"] = "decisive_player_success",
                ["modifierBreakdown"] = new JsonObject { ["player"] = new JsonArray(), ["opposition"] = new JsonArray() }
            }
        };
        var update = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = exchange
        };
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);
        var activeAfter = Assert.IsType<JsonObject>(projected["activeConflict"]).DeepClone().AsObject();
        activeAfter.Remove("combatConditions");
        update["activeConflictAfter"] = activeAfter;
        root[AfterlifeSpiritualConflictState.ResponseField] = update;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
    }
}
