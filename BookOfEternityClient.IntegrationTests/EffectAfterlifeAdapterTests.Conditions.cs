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
        var submittedAfterImage = Assert.IsType<JsonObject>(raw["activeConflict"])
            .DeepClone()
            .AsObject();
        submittedAfterImage["combatConditions"] = new JsonArray();
        raw[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = new JsonObject
            {
                ["exchangeId"] = "exchange_direct_condition",
                ["conflictId"] = conflictId,
                ["outcome"] = "success"
            },
            ["activeConflictAfter"] = submittedAfterImage
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.SpiritualConflictPath,
            raw);

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();

        Assert.Contains(
            issues,
            issue => string.Equals(
                issue.Code,
                "afterlife_combat_condition_direct_authoring_forbidden",
                StringComparison.Ordinal));
    }

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
            ["exchange"] = new JsonObject
            {
                ["exchangeId"] = exchangeId,
                ["conflictId"] = conflictId,
                ["outcome"] = "success"
            }
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
            var active = Assert.IsType<JsonObject>(rawConflict["activeConflict"]);
            active["exchangeLog"]!.AsArray().Add(new JsonObject
            {
                ["exchangeId"] = acceptedExchangeId,
                ["conflictId"] = conflictId,
                ["outcome"] = "success"
            });
            await context.WriteJsonAsync(
                EffectMaterializationTestContext.SpiritualConflictPath,
                rawConflict);
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 43,
                currentRealm: "Shining Abode");
        }
        else
        {
            rawConflict[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
                ["exchange"] = new JsonObject
                {
                    ["exchangeId"] = acceptedExchangeId,
                    ["conflictId"] = conflictId,
                    ["outcome"] = "success"
                }
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
            ["exchange"] = new JsonObject
            {
                ["exchangeId"] = "exchange_lifetime_44",
                ["conflictId"] = conflictId,
                ["outcome"] = "success"
            }
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
            ["exchange"] = new JsonObject
            {
                ["exchangeId"] = "exchange_lifetime_45",
                ["conflictId"] = conflictId,
                ["outcome"] = "success"
            }
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
        var spiritualConflict = CreateCanonicalSpiritualConflict(conflictId);
        var conflictResourcePlan = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                SpiritualConflict: spiritualConflict),
            turn: 42);
        Assert.True(
            conflictResourcePlan.IsValid,
            string.Join(Environment.NewLine, conflictResourcePlan.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            conflictResourcePlan));
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Shining Abode");
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
