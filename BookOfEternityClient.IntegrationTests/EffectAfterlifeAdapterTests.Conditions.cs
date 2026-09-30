using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAfterlifeAdapterTests
{
    public static TheoryData<string, string> SpiritualConditionKindsAndAxes =>
        new()
        {
            { "mark", "rollMode" },
            { "ward", "conflictPosition" },
            { "burden", "controlState" },
            { "opening", "playerSideStrain" },
            { "vow", "oppositionSideStrain" },
            { "mark", "tempoAdvantage" },
            { "ward", "counterPayoff" },
            { "burden", "actionCostAudit" },
            { "opening", "actionCostAudit.player" },
            { "vow", "actionCostAudit.opposition" },
            { "mark", "specialArtAudit.effectNote" },
            { "ward", "specialArtAudits.effectNote" }
        };

    [Theory]
    [MemberData(nameof(SpiritualConditionKindsAndAxes))]
    public void SpiritualConditionAdapter_ProjectsEveryKindAndLegalAxis(
        string conditionKind,
        string axis)
    {
        var effect = CreateCanonicalSpiritualConditionEffect(
            conditionKind,
            axis,
            new JsonObject
            {
                ["mode"] = "uses",
                ["remainingUses"] = 2,
                ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed")
            });

        Assert.True(
            AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                effect,
                out var condition,
                out var reason),
            reason);
        Assert.Equal(conditionKind, condition["kind"]!.GetValue<string>());
        Assert.Equal(
            new[] { axis },
            condition["mechanicalAxes"]!.AsArray()
                .Select(node => node!.GetValue<string>())
                .ToArray());
    }

    [Theory]
    [InlineData("uses", "remainingUses", 3)]
    [InlineData("turns", "remainingExchanges", 4)]
    [InlineData("scene", "expiresAtScene", 0)]
    public void SpiritualConditionAdapter_DerivesOneSpecializedDurationFromCommonLifetime(
        string mode,
        string expectedField,
        int expectedCount)
    {
        var lifetime = mode switch
        {
            "uses" => new JsonObject
            {
                ["mode"] = mode,
                ["remainingUses"] = expectedCount,
                ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed")
            },
            "turns" => new JsonObject
            {
                ["mode"] = mode,
                ["remainingTurns"] = expectedCount,
                ["advancePhase"] = "afterlife_exchange_end"
            },
            "scene" => new JsonObject
            {
                ["mode"] = mode,
                ["sceneId"] = "afterlife_conflict_duration_scene",
                ["onSceneExit"] = "expire"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        var effect = CreateCanonicalSpiritualConditionEffect(
            "mark",
            "rollMode",
            lifetime);

        Assert.True(
            AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                effect,
                out var condition,
                out var reason),
            reason);
        var duration = Assert.IsType<JsonObject>(condition["duration"]);
        Assert.True(duration.ContainsKey(expectedField));
        if (expectedCount > 0)
            Assert.Equal(expectedCount, duration[expectedField]!.GetValue<int>());
        else
            Assert.Equal(
                "afterlife_conflict_duration_scene",
                duration[expectedField]!.GetValue<string>());
    }

    [Fact]
    public void SpiritualConditionComponent_RejectsIndependentDurationCounters()
    {
        var effect = CreateCanonicalSpiritualConditionEffect(
            "mark",
            "rollMode",
            new JsonObject
            {
                ["mode"] = "uses",
                ["remainingUses"] = 2,
                ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed")
            });
        var component = Assert.IsType<JsonObject>(effect["components"]![0]);
        var payload = Assert.IsType<JsonObject>(component["payload"]);
        payload["exchangeLimit"] = 2;
        payload["sceneLimit"] = 1;
        using var document = JsonDocument.Parse(component.ToJsonString());
        var issues = new List<ValidationIssue>();

        EffectComponentProfiles.ValidateComponent(
            document.RootElement,
            "component",
            issues);

        Assert.Contains(
            issues,
            issue => string.Equals(
                issue.Code,
                "effect_materialization_invalid_component",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("permanent")]
    [InlineData("owner_turn_end")]
    public void SpiritualConditionDefinition_RejectsLifetimeOutsideClosedAdapter(
        string lifetimePolicy)
    {
        var lifetime = string.Equals(
            lifetimePolicy,
            "permanent",
            StringComparison.Ordinal)
            ? new JsonObject { ["mode"] = "permanent" }
            : new JsonObject
            {
                ["mode"] = "turns",
                ["initialTurns"] = 2,
                ["advancePhase"] = lifetimePolicy
            };
        var definition = CreateSpiritualConditionDefinition(
            lifetime: lifetime);
        using var document = JsonDocument.Parse(
            new JsonArray(definition).ToJsonString());

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "definitions",
            "shining_abode");

        Assert.Contains(
            issues,
            issue => string.Equals(
                issue.Code,
                "effect_source_definition_afterlife_condition_lifetime_invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SpiritualConditionApply_PublishesOneCommonIdentityInSpecializedCarrier()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string sourceArtId = "art_opposition_burden";
        const string conflictId = "afterlife_conflict_effect_001";
        await BootstrapSpiritualConditionAuthorityAsync(
            context,
            conflictId,
            sourceArtId);

        var command = EffectMaterializationTestFixture.CreateApplyCommand("guardian");
        command["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = $"{conflictId}:opposition"
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = sourceArtId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject();
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        var handoff = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem);
        Assert.NotNull(handoff);
        var planned = Assert.IsType<AcceptedMechanicsPlan>(handoff!.Result.Plan);
        var plannedConflict = Assert.IsType<JsonObject>(
            planned.EffectCarrierAfterImages[
                EffectMaterializationTestContext.SpiritualConflictPath]);
        var plannedCatalog = EffectCarrierCatalog.Build(
            new EffectCarrierCatalogInput(
                PlayerEffects: null,
                NpcEffects: null,
                EnemyCombatants: null,
                AllyCombatants: null,
                AfterlifeProfiles: null,
                SpiritualConflict: plannedConflict));
        Assert.True(
            plannedCatalog.Issues.Count == 0,
            DescribeIssues(plannedCatalog.Issues));

        var backups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups));

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        var condition = Assert.IsType<JsonObject>(
            Assert.Single(active["combatConditions"]!.AsArray()));
        Assert.Equal(
            condition["effectId"]!.GetValue<string>(),
            condition["conditionId"]!.GetValue<string>());
        Assert.Equal("burden", condition["kind"]!.GetValue<string>());
        Assert.Equal("opposition", condition["targetSide"]!.GetValue<string>());
        Assert.Equal(
            "spiritual_conflict_side",
            condition["target"]!["kind"]!.GetValue<string>());
        Assert.Equal(
            $"{conflictId}:opposition",
            condition["target"]!["targetId"]!.GetValue<string>());
        Assert.Equal(
            new[] { "pressure" },
            condition["affectedOperations"]!.AsArray()
                .Select(node => node!.GetValue<string>())
                .ToArray());
        Assert.Equal(
            new[] { "rollMode" },
            condition["mechanicalAxes"]!.AsArray()
                .Select(node => node!.GetValue<string>())
                .ToArray());
        Assert.Equal(
            "impose_disadvantage",
            condition["payoff"]!["effect"]!.GetValue<string>());
        Assert.NotNull(condition["duration"]);

        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal(
            condition["effectId"]!.GetValue<string>(),
            entry["effectId"]!.GetValue<string>());
        Assert.Equal(
            EffectMaterializationTestContext.SpiritualConflictPath,
            entry["owner"]!["carrierPath"]!.GetValue<string>());
        Assert.Equal("combatConditions", entry["owner"]!["collection"]!.GetValue<string>());
    }

    /// <summary>
    /// Rejects only direct condition-carrier authoring after the otherwise valid declared-after wrapper passes raw validation.
    /// </summary>
    /// <returns>
    /// A task completing after the exact authored-field diagnostic and unchanged canonical, private and draft bytes are checked.
    /// </returns>
    [Fact]
    public async Task SpiritualConditionDirectWrapperAuthoring_IsRejected()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string conflictId = "afterlife_conflict_direct_condition";
        await BootstrapSpiritualConditionAuthorityAsync(
            context,
            conflictId,
            "art_direct_condition_guard");
        var raw = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        Assert.IsType<JsonObject>(raw["activeConflict"])["playerSideStrain"] = "strained";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            raw);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Shining Abode",
            preGeneratedDices1d20: new[] { 15, 5 });

        var exchange = await CreateSpiritualConditionExchangeAsync(
            context, conflictId, "exchange_direct_condition", turn: 43);
        exchange["outcome"] = "success";
        exchange["after"]!["playerSideStrain"] = "clear";
        exchange["diceAudit"] = new JsonObject
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
            ["modifierBreakdown"] = new JsonObject
            {
                ["player"] = new JsonArray(),
                ["opposition"] = new JsonArray()
            }
        };
        var update = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = exchange
        };
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(raw, update);
        Assert.False(projected.ContainsKey("lastInvalidUpdate"));
        var submittedAfterImage = Assert.IsType<JsonObject>(projected["activeConflict"])
            .DeepClone()
            .AsObject();
        submittedAfterImage.Remove("combatConditions");
        update["activeConflictAfter"] = submittedAfterImage;
        raw[AfterlifeSpiritualConflictState.ResponseField] = update;
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            raw);

        var validIssues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            validIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(validIssues));
        Assert.NotNull(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
        await AssertSpiritualConditionActionPointsAsync(context, playerCurrent: 6m);

        submittedAfterImage["combatConditions"] = new JsonArray();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            raw);
        var pathsBefore = Directory.GetFiles(context.FileSystem.GameSessionPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(context.FileSystem.GameSessionPath, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var bytesBefore = await context.CaptureBytesAsync(pathsBefore);

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();

        var forbiddenPath = EffectMaterializationTestContext.SpiritualConflictPath + "." +
            AfterlifeSpiritualConflictState.ResponseField + ".activeConflictAfter.combatConditions";
        Assert.True(
            issues.Any(issue =>
                issue.Severity == IssueSeverity.Error &&
                issue.Code == "afterlife_combat_condition_direct_authoring_forbidden" &&
                issue.FilePath == forbiddenPath &&
                issue.Expected == "combatConditions absent from GM-authored conflict lifecycle updates" &&
                issue.Actual == "direct combatConditions field present"),
            DescribeIssues(issues));
        var pathsAfter = Directory.GetFiles(context.FileSystem.GameSessionPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(context.FileSystem.GameSessionPath, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(pathsBefore, pathsAfter);
        var bytesAfter = await context.CaptureBytesAsync(pathsAfter);
        foreach (var (path, content) in bytesBefore)
            Assert.Equal(content, bytesAfter[path]);
    }

    /// <summary>
    /// Publishes a condition bound to the exact accepted neutral exchange in the current turn.
    /// </summary>
    /// <returns>
    /// A task completing after effect identity, chronology, lifetime and lawful action-point publication checks.
    /// </returns>
    [Fact]
    public async Task SpiritualConditionApply_UsesExactSameTurnExchangeAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string conflictId = "afterlife_conflict_exchange_effect";
        const string sourceArtId = "art_exchange_condition";
        const string exchangeId = "exchange_condition_effect_43";
        await BootstrapSpiritualConditionAuthorityAsync(
            context,
            conflictId,
            sourceArtId);
        var rawConflict = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = await CreateSpiritualConditionExchangeAsync(
                context, conflictId, exchangeId, turn: 43)
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            rawConflict);

        var command = EffectMaterializationTestFixture.CreateApplyCommand("guardian");
        command["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = $"{conflictId}:opposition"
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = sourceArtId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject();
        command["eventRef"] = new JsonObject
        {
            ["kind"] = "afterlife_exchange",
            ["authorityId"] = exchangeId
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));

        var backups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
            backups));
        await AssertSpiritualConditionActionPointsAsync(context, playerCurrent: 4m);
        var canonical = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        var active = Assert.IsType<JsonObject>(canonical["activeConflict"]);
        var condition = Assert.IsType<JsonObject>(
            Assert.Single(active["combatConditions"]!.AsArray()));
        Assert.Equal(
            $"afterlife_exchange:{conflictId}:{exchangeId}",
            condition["chronology"]!["createdEventRef"]!.GetValue<string>());
        Assert.Equal(
            2,
            condition["lifetime"]!["remainingUses"]!.GetValue<int>());
        Assert.Contains(
            active["exchangeLog"]!.AsArray().OfType<JsonObject>(),
            exchange => string.Equals(
                exchange["exchangeId"]?.GetValue<string>(),
                exchangeId,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Rejects effect authority from a published historical exchange or a different current exchange ID.
    /// </summary>
    /// <param name="historical">
    /// <see langword="true"/> publishes the exchange before the new snapshot; otherwise only a different current exchange is accepted.
    /// </param>
    /// <returns>
    /// A task completing after the event-authority mismatch is observed.
    /// </returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SpiritualConditionApply_RejectsUnacceptedExchangeAuthority(
        bool historical)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string conflictId = "afterlife_conflict_exchange_rejection";
        const string sourceArtId = "art_exchange_rejection";
        const string acceptedExchangeId = "exchange_authoritative_43";
        const string forgedExchangeId = "exchange_forged_43";
        await BootstrapSpiritualConditionAuthorityAsync(
            context,
            conflictId,
            sourceArtId);
        var rawConflict = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        if (historical)
        {
            rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
                ["exchange"] = await CreateSpiritualConditionExchangeAsync(
                    context, conflictId, acceptedExchangeId, turn: 43)
            };
            await context.WriteJsonAsync(
                EffectMaterializationTestContext.SpiritualConflictPath,
                rawConflict);
            var historicalIssues = await context.ValidateAcceptedTurnRawMechanicsAsync();
            Assert.True(
                historicalIssues.All(issue => issue.Severity != IssueSeverity.Error),
                DescribeIssues(historicalIssues));
            var historicalBackups = await context.ReadPendingSnapshotBackupsAsync();
            Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
                historicalBackups));
            await AssertSpiritualConditionActionPointsAsync(context, playerCurrent: 4m);
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 44,
                currentRealm: "Shining Abode");
        }
        else
        {
            rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
                ["exchange"] = await CreateSpiritualConditionExchangeAsync(
                    context, conflictId, acceptedExchangeId, turn: 43)
            };
            await context.WriteJsonAsync(
                EffectMaterializationTestContext.SpiritualConflictPath,
                rawConflict);
        }

        var command = CreateSpiritualConditionApplyCommand(
            conflictId,
            sourceArtId,
            "afterlife_exchange",
            historical ? acceptedExchangeId : forgedExchangeId);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();

        Assert.Contains(
            issues,
            issue => string.Equals(
                issue.Code,
                "effect_plan_event_authority_mismatch",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing_condition_actor")]
    [InlineData("player_soul")]
    [InlineData("Guardian_Condition_Source")]
    public async Task SpiritualConditionApply_RejectsActorOutsideExactTargetSide(
        string targetActorId)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string conflictId = "afterlife_conflict_participant_binding";
        const string sourceArtId = "art_participant_binding";
        await BootstrapSpiritualConditionAuthorityAsync(
            context,
            conflictId,
            sourceArtId,
            targetActorId);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateSpiritualConditionApplyCommand(
                    conflictId,
                    sourceArtId,
                    "accepted_turn",
                    "turn_43")));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();

        Assert.Contains(
            issues,
            issue => string.Equals(
                issue.Code,
                "effect_target_spiritual_participant_unresolved",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Advances a finite condition over two complete neutral exchanges and publishes its expiration.
    /// </summary>
    /// <param name="lifetimeMode">
    /// Finite turns or uses policy consumed at afterlife_exchange_end.
    /// </param>
    /// <returns>
    /// A task completing after lifetime, identity transition and exact resource payment assertions.
    /// </returns>
    [Theory]
    [InlineData("turns")]
    [InlineData("uses")]
    public async Task SpiritualConditionFiniteExchangeLifetime_AdvancesAndExpires(
        string lifetimeMode)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string conflictId = "afterlife_conflict_exchange_lifetime";
        const string sourceArtId = "art_exchange_lifetime";
        await BootstrapSpiritualConditionAuthorityAsync(
            context,
            conflictId,
            sourceArtId,
            lifetime: lifetimeMode == "turns"
                ? new JsonObject
                {
                    ["mode"] = "turns",
                    ["initialTurns"] = 2,
                    ["advancePhase"] = "afterlife_exchange_end"
                }
                : new JsonObject
                {
                    ["mode"] = "uses",
                    ["initialUses"] = 2,
                    ["consumingEventTypes"] = new JsonArray(
                        "afterlife_exchange_end")
                });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateSpiritualConditionApplyCommand(
                    conflictId,
                    sourceArtId,
                    "accepted_turn",
                    "turn_43")));
        var applyIssues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            applyIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(applyIssues));
        var applyBackups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
            applyBackups));
        var appliedCondition = await ReadOnlySpiritualCondition(context, conflictId);
        Assert.Equal(
            2,
            appliedCondition["duration"]![
                lifetimeMode == "turns"
                    ? "remainingExchanges"
                    : "remainingUses"]!.GetValue<int>());

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 44,
            currentRealm: "Shining Abode");
        var rawConflict = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = await CreateSpiritualConditionExchangeAsync(
                context, conflictId, "exchange_lifetime_44", turn: 44)
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            rawConflict);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot());

        var exchangeIssues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            exchangeIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(exchangeIssues));
        var exchangeBackups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
            exchangeBackups));

        await AssertSpiritualConditionActionPointsAsync(context, playerCurrent: 4m);
        var condition = await ReadOnlySpiritualCondition(context, conflictId);
        Assert.Equal(
            1,
            condition["lifetime"]![
                lifetimeMode == "turns"
                    ? "remainingTurns"
                    : "remainingUses"]!.GetValue<int>());
        Assert.Equal(
            1,
            condition["duration"]![
                lifetimeMode == "turns"
                    ? "remainingExchanges"
                    : "remainingUses"]!.GetValue<int>());

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 45,
            currentRealm: "Shining Abode");
        rawConflict = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = await CreateSpiritualConditionExchangeAsync(
                context, conflictId, "exchange_lifetime_45", turn: 45)
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            rawConflict);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot());
        var terminalIssues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            terminalIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(terminalIssues));
        var terminalBackups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
            terminalBackups));

        await AssertSpiritualConditionActionPointsAsync(context, playerCurrent: 2m);
        var terminalRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        var terminalConflict = Assert.IsType<JsonObject>(terminalRoot["activeConflict"]);
        Assert.Empty(terminalConflict["combatConditions"]!.AsArray());
        var identity = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var identityEntry = Assert.IsType<JsonObject>(
            Assert.Single(identity["entries"]!.AsArray()));
        Assert.Equal("expired", identityEntry["state"]!.GetValue<string>());
        Assert.Equal(
            "expire",
            identityEntry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task SpiritualConditionSceneLifetime_BindsToExactConflictScene()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string conflictId = "afterlife_conflict_scene_lifetime";
        const string sourceArtId = "art_scene_lifetime";
        await BootstrapSpiritualConditionAuthorityAsync(
            context,
            conflictId,
            sourceArtId,
            lifetime: new JsonObject
            {
                ["mode"] = "scene",
                ["onSceneExit"] = "expire"
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                CreateSpiritualConditionApplyCommand(
                    conflictId,
                    sourceArtId,
                    "accepted_turn",
                    "turn_43")));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
            backups));

        var condition = await ReadOnlySpiritualCondition(context, conflictId);
        Assert.Equal(
            conflictId,
            condition["lifetime"]!["sceneId"]!.GetValue<string>());
        Assert.Equal(
            conflictId,
            condition["duration"]!["expiresAtScene"]!.GetValue<string>());

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 44,
            currentRealm: "Shining Abode");
        var rawConflict = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeRepairCancel
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            rawConflict);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot());

        var closeIssues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            closeIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(closeIssues));
        var closeBackups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
            closeBackups));
        var closed = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        Assert.Null(closed["activeConflict"]);
        var identity = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var identityEntry = Assert.IsType<JsonObject>(
            Assert.Single(identity["entries"]!.AsArray()));
        Assert.Equal("expired", identityEntry["state"]!.GetValue<string>());
        Assert.Equal(
            "expire",
            identityEntry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void ApplyExchange_PreservesCommonConditionCarrier()
    {
        const string conflictId = "afterlife_conflict_condition_preservation";
        var root = CreateCanonicalSpiritualConflict(conflictId);
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        var expectedConditions = new JsonArray(new JsonObject
        {
            ["effectId"] = "effect_condition_preserved"
        });
        active["combatConditions"] = expectedConditions.DeepClone();
        var replacement = active.DeepClone().AsObject();
        replacement.Remove("combatConditions");
        var update = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = new JsonObject
            {
                ["exchangeId"] = "exchange_condition_preservation",
                ["conflictId"] = conflictId,
                ["outcome"] = "success"
            },
            ["activeConflictAfter"] = replacement
        };

        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root, update);

        var projectedActive = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.True(JsonNode.DeepEquals(
            expectedConditions,
            projectedActive["combatConditions"]));
    }

    /// <summary>
    /// Commits complete original participant profiles and conflict resource owners before signing condition authority.
    /// </summary>
    /// <param name="context">
    /// Fresh context receiving the accepted bootstrap and real signed original snapshot.
    /// </param>
    /// <param name="conflictId">
    /// Exact conflict ID retained by the condition target and exchange authority.
    /// </param>
    /// <param name="sourceArtId">
    /// Exact original player special-art ID exporting the condition definition.
    /// </param>
    /// <param name="targetActorId">
    /// Payload actor selector; defaults to the opposition guardian and may intentionally be invalid for rejection tests.
    /// </param>
    /// <param name="lifetime">
    /// Optional finite or scene lifetime; <see langword="null"/> uses the two-use exchange policy.
    /// </param>
    /// <returns>
    /// A task completing after lawful six-point owners and both original realm profiles are captured.
    /// </returns>
    private static async Task BootstrapSpiritualConditionAuthorityAsync(
        EffectMaterializationTestContext context,
        string conflictId,
        string sourceArtId,
        string targetActorId = "guardian_condition_source",
        JsonObject? lifetime = null)
    {
        await context.MaterializeAfterlifeActorAsync(
            "player_soul",
            "player_soul",
            "Shining Abode",
            sourceArtId,
            CreateSpiritualConditionDefinition(targetActorId, lifetime));
        var profiles = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var guardian = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            "guardian", "guardian_condition_source", "Shining Abode", materializedAtTurn: 42);
        guardian["standardArts"]!["pressure"] = 2;
        guardian["activeEffects"] = new JsonArray();
        profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().Add(guardian);
        var spiritualConflict = CreateCanonicalSpiritualConflict(conflictId);
        var conflictResourcePlan = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: profiles,
                SpiritualConflict: spiritualConflict),
            turn: 42);
        Assert.True(
            conflictResourcePlan.IsValid,
            string.Join(Environment.NewLine, conflictResourcePlan.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            conflictResourcePlan));
        await AssertSpiritualConditionActionPointsAsync(context, playerCurrent: 6m);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Shining Abode");
    }

    /// <summary>
    /// Creates a non-harmful current guard exchange using the actual canonical player balance.
    /// </summary>
    /// <param name="context">
    /// Context containing the signed original profiles and active resource owners.
    /// </param>
    /// <param name="conflictId">
    /// Exact active conflict ID to retain in the exchange.
    /// </param>
    /// <param name="exchangeId">
    /// Exact current exchange ID tested by the effect event authority.
    /// </param>
    /// <param name="turn">
    /// Current turn signed by the owning snapshot.
    /// </param>
    /// <returns>
    /// A complete guard/no_effect exchange costing two player action points and preserving tactical state.
    /// </returns>
    private static async Task<JsonObject> CreateSpiritualConditionExchangeAsync(
        EffectMaterializationTestContext context,
        string conflictId,
        string exchangeId,
        int turn)
    {
        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        Assert.Equal(conflictId, active["conflictId"]!.GetValue<string>());
        var projection = AfterlifeConflictActionPointProjectionService.Resolve(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            active);
        Assert.True(projection.IsValid, DescribeIssues(projection.Issues));
        var player = projection.Projection!.Player;
        Assert.True(player.Current >= 2m);
        var before = new JsonObject
        {
            ["playerSideStrain"] = active["playerSideStrain"]!.DeepClone(),
            ["oppositionSideStrain"] = active["oppositionSideStrain"]!.DeepClone(),
            ["conflictPosition"] = active["conflictPosition"]!.DeepClone()
        };
        if (active.ContainsKey("controlState"))
            before["controlState"] = active["controlState"]?.DeepClone();
        return new JsonObject
        {
            ["exchangeId"] = exchangeId,
            ["conflictId"] = conflictId,
            ["turnNumber"] = turn,
            ["operationType"] = "guard",
            ["outcome"] = "no_effect",
            ["before"] = before,
            ["after"] = before.DeepClone(),
            ["matchupAudit"] = new JsonObject
            {
                ["playerOperation"] = "guard",
                ["oppositionOperation"] = "passive",
                ["primaryResolutionLane"] = "guard",
                ["matchupRationale"] = "Душа удерживает защиту; хранитель сохраняет состояние без действия.",
                ["riskProfile"] = "safe_defense"
            },
            ["actionCostAudit"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["operationType"] = "guard",
                    ["baseCost"] = 2,
                    ["minCost"] = 1,
                    ["artTier"] = 0,
                    ["effectiveCost"] = 2,
                    ["before"] = player.Current,
                    ["after"] = player.Current - 2m,
                    ["max"] = player.Maximum
                }
            }
        };
    }

    /// <summary>
    /// Checks the canonical conflict action points after accepted publication or initial bootstrap.
    /// </summary>
    /// <param name="context">
    /// Context whose real canonical resource ledger supplies both owner balances.
    /// </param>
    /// <param name="playerCurrent">
    /// Exact expected player balance after the corresponding neutral guard payment.
    /// </param>
    /// <returns>
    /// A task completing after exact current balances and unchanged six-point capacities are checked.
    /// </returns>
    private static async Task AssertSpiritualConditionActionPointsAsync(
        EffectMaterializationTestContext context,
        decimal playerCurrent)
    {
        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        var projection = AfterlifeConflictActionPointProjectionService.Resolve(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            active);
        Assert.True(projection.IsValid, DescribeIssues(projection.Issues));
        Assert.Equal(playerCurrent, projection.Projection!.Player.Current);
        Assert.Equal(6m, projection.Projection.Player.Maximum);
        Assert.Equal(6m, projection.Projection.Opposition.Current);
        Assert.Equal(6m, projection.Projection.Opposition.Maximum);
    }

    private static JsonObject CreateSpiritualConditionApplyCommand(
        string conflictId,
        string sourceArtId,
        string eventKind,
        string authorityId)
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand("guardian");
        command["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = $"{conflictId}:opposition"
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = sourceArtId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject();
        command["eventRef"] = new JsonObject
        {
            ["kind"] = eventKind,
            ["authorityId"] = authorityId
        };
        return command;
    }

    private static JsonObject CreateSpiritualConditionDefinition(
        string targetActorId = "guardian_condition_source",
        JsonObject? lifetime = null)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "afterlife_combat_condition");
        definition["allowedRealms"] = new JsonArray("shining_abode");
        definition["allowedTargetKinds"] = new JsonArray("spiritual_conflict_side");
        definition["display"]!["name"] = "Печать тяжести";
        definition["display"]!["description"] =
            "Печать мешает стороне противника удерживать давление.";
        definition["lifetime"] = (lifetime ?? new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("afterlife_exchange_end")
        }).DeepClone();
        definition["triggers"] = string.Equals(
            definition["lifetime"]!["mode"]!.GetValue<string>(),
            "uses",
            StringComparison.Ordinal)
            ? new JsonArray(new JsonObject
            {
                ["triggerId"] = "condition_exchange_consumed",
                ["eventType"] = "afterlife_exchange_end",
                ["priority"] = 100,
                ["componentIds"] = new JsonArray("component_001"),
                ["consumeUses"] = true,
                ["resolutionMode"] = "deterministic"
            })
            : new JsonArray();
        var payload = definition["components"]![0]!["payload"]!.AsObject();
        payload["conditionKind"] = "burden";
        payload["targetSide"] = "opposition";
        payload["actorId"] = targetActorId;
        payload["operations"] = new JsonArray("pressure");
        payload["axes"] = new JsonArray("rollMode");
        payload["counterplay"] = new JsonArray("Ответить действием guard или counter.");
        payload["payoff"] = "impose_disadvantage";
        return definition;
    }

    private static async Task<JsonObject> ReadOnlySpiritualCondition(
        EffectMaterializationTestContext context,
        string conflictId)
    {
        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath))!.AsObject();
        var active = Assert.IsType<JsonObject>(root["activeConflict"]);
        Assert.Equal(conflictId, active["conflictId"]!.GetValue<string>());
        return Assert.IsType<JsonObject>(
            Assert.Single(active["combatConditions"]!.AsArray()));
    }

    private static JsonObject CreateCanonicalSpiritualConflict(string conflictId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["dangerMode"] = "hostile",
                ["conflictId"] = conflictId,
                ["realm"] = "Shining Abode",
                ["sideModel"] = "direct_duel",
                ["status"] = "active",
                ["resolutionState"] = "active",
                ["conflictPosition"] = "contested",
                ["playerSideStrain"] = "clear",
                ["oppositionSideStrain"] = "clear",
                ["playerSide"] = new JsonObject
                {
                    ["leadContestant"] = new JsonObject
                    {
                        ["actorType"] = "player",
                        ["actorId"] = "player_soul",
                        ["displayName"] = "Асуран"
                    },
                    ["supporters"] = new JsonArray()
                },
                ["oppositionSide"] = new JsonObject
                {
                    ["leadContestant"] = new JsonObject
                    {
                        ["actorType"] = "guardian",
                        ["actorId"] = "guardian_condition_source",
                        ["displayName"] = "Хранитель печати",
                        ["actorArtTierSnapshot"] = new JsonObject { ["pressure"] = 2 },
                        ["artAuthoritySource"] = "guardian_state"
                    },
                    ["supporters"] = new JsonArray(),
                    ["resourceMaterialization"] = new JsonObject
                    {
                        ["resources"] = new JsonArray(new JsonObject
                        {
                            ["resourceKey"] = "spiritual_action_points",
                            ["maximum"] = 6
                        })
                    }
                },
                ["combatConditions"] = new JsonArray(),
                ["exchangeLog"] = new JsonArray()
            },
            ["recentConflicts"] = new JsonArray()
        };

    private static JsonObject CreateCanonicalSpiritualConditionEffect(
        string conditionKind,
        string axis,
        JsonObject lifetime)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "afterlife_combat_condition");
        effect["realm"] = "shining_abode";
        effect["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = "afterlife_conflict_duration_scene:opposition"
        };
        effect["display"]!["category"] = "condition";
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = "art_condition_projection",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"] = new JsonArray(new JsonObject
        {
            ["componentId"] = "component_001",
            ["profile"] = "afterlife_combat_condition",
            ["priority"] = 100,
            ["payload"] = new JsonObject
            {
                ["conditionKind"] = conditionKind,
                ["targetSide"] = "opposition",
                ["actorId"] = "guardian_condition_source",
                ["operations"] = new JsonArray("pressure"),
                ["axes"] = new JsonArray(axis),
                ["counterplay"] = new JsonArray("Ответить точным действием."),
                ["payoff"] = "impose_disadvantage"
            }
        });
        effect["lifetime"] = lifetime.DeepClone();
        return effect;
    }
}
