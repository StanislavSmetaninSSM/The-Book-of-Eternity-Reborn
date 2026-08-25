using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task AcceptedTurn_ActionCostSpendsLedgerWithoutLegacyActionEconomy()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 6m,
            oppositionCurrent: 6m);
        var conflict = ActiveConflict("conflict_resource_cost");

        var raw = new JsonObject { ["activeConflict"] = conflict.DeepClone() };
        raw[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = new JsonObject
            {
                ["exchangeId"] = "exchange_resource_cost_42",
                ["operationType"] = "pressure",
                ["outcome"] = "success",
                ["actionCostAudit"] = new JsonObject
                {
                    ["player"] = new JsonObject
                    {
                        ["operationType"] = "pressure",
                        ["baseCost"] = 3,
                        ["minCost"] = 1,
                        ["artTier"] = 0,
                        ["effectiveCost"] = 3,
                        ["before"] = 6,
                        ["after"] = 3
                    }
                }
            }
        };
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            raw.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        var playerActionPoints = ResolvePlannedActionPoints(
            plan,
            ResourceOwnerKind.AfterlifeActor);
        Assert.Equal(3m, playerActionPoints.Current);
        var spend = Assert.Single(
            plan.HistoryAfterImage["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["eventRef"]!.GetValue<string>() ==
                     "turn_42:afterlife_conflict:exchange_resource_cost_42:player");
        Assert.Equal("spend", spend["operation"]!.GetValue<string>());
        Assert.Equal(3m, spend["appliedAmount"]!.GetValue<decimal>());

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var published = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);
        Assert.Same(plan, published);
        var canonicalConflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        Assert.False(
            Assert.IsType<JsonObject>(canonicalConflict["activeConflict"])
                .ContainsKey("actionEconomy"));
        Assert.False(canonicalConflict.ContainsKey(
            AfterlifeSpiritualConflictState.ResponseField));
    }

    [Fact]
    public async Task AcceptedTurn_PublishedSpendSatisfiesConflictResourceValidation()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 6m,
            oppositionCurrent: 6m);
        await WriteExchangeAsync(
            context,
            Exchange(
                "exchange_resource_validator_42",
                PlayerAudit("pressure", 3m, 6m, 3m)));

        var rawIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(
            rawIssues,
            issue => issue.Severity == IssueSeverity.Error);

        await using (var writeLease = await context.FileSystem
                         .AcquireCanonicalWriteLeaseAsync())
        {
            await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
        }

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(
                GameStateValidationPhase.AfterlifeSpiritualConflictState,
                new[] { AfterlifeSpiritualConflictState.StatePath }));

        Assert.DoesNotContain(
            issues,
            issue => issue.Code == "afterlife_conflict_legacy_action_economy_forbidden" ||
                     issue.Code == "afterlife_conflict_resource_projection_missing" ||
                     issue.Code?.Contains("action_cost", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ConflictValidation_RejectsCostSequenceThatDoesNotStartAtLedgerCurrent()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 6m,
            oppositionCurrent: 6m);
        await WriteProjectedExchangesAsync(
            context,
            Exchange(
                "exchange_resource_sequence_mismatch_42",
                PlayerAudit("pressure", 3m, 4m, 1m)));

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(
                GameStateValidationPhase.AfterlifeSpiritualConflictState,
                new[] { AfterlifeSpiritualConflictState.StatePath }));

        Assert.Contains(
            issues,
            issue => issue.Code == "afterlife_conflict_action_cost_sequence_mismatch");
    }

    [Fact]
    public async Task ConflictValidation_RejectsRecoveryBeyondLedgerMaximum()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 5m,
            oppositionCurrent: 6m);
        await WriteProjectedExchangesAsync(
            context,
            Exchange(
                "exchange_resource_recovery_over_cap_42",
                PlayerAudit("recover_spiritual_power", 0m, 5m, 9m)));

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(
                GameStateValidationPhase.AfterlifeSpiritualConflictState,
                new[] { AfterlifeSpiritualConflictState.StatePath }));

        Assert.Contains(
            issues,
            issue => issue.Code == "afterlife_conflict_action_recovery_exceeds_max");
    }

    [Fact]
    public async Task AcceptedTurn_RecoveryGainsLedgerPointsWithoutLegacyPool()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 2m,
            oppositionCurrent: 6m);
        await WriteExchangeAsync(
            context,
            Exchange(
                "exchange_resource_recovery_42",
                PlayerAudit(
                    "recover_spiritual_power",
                    effectiveCost: 0m,
                    before: 2m,
                    after: 5m)));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        var player = ResolvePlannedActionPoints(
            plan,
            ResourceOwnerKind.AfterlifeActor);
        Assert.Equal(5m, player.Current);
        var gain = Assert.Single(
            plan.HistoryAfterImage["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["eventRef"]!.GetValue<string>() ==
                     "turn_42:afterlife_conflict:exchange_resource_recovery_42:player");
        Assert.Equal("gain", gain["operation"]!.GetValue<string>());
        Assert.Equal(3m, gain["appliedAmount"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task AcceptedTurn_SequencesBothSidesThroughOneLedgerPlan()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 6m,
            oppositionCurrent: 6m);
        await WriteExchangesAsync(
            context,
            Exchange(
                "exchange_resource_sequence_1",
                PlayerAudit("pressure", 3m, 6m, 3m),
                OppositionAudit("guard", 2m, 6m, 4m)),
            Exchange(
                "exchange_resource_sequence_2",
                PlayerAudit("guard", 2m, 3m, 1m)));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        Assert.Equal(
            1m,
            ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(
            4m,
            ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeConflictSide).Current);
        var eventRefs = plan.HistoryAfterImage["entries"]!.AsArray()
            .OfType<JsonObject>()
            .Select(entry => entry["eventRef"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(
            "turn_42:afterlife_conflict:exchange_resource_sequence_1:player",
            eventRefs);
        Assert.Contains(
            "turn_42:afterlife_conflict:exchange_resource_sequence_1:opposition",
            eventRefs);
        Assert.Contains(
            "turn_42:afterlife_conflict:exchange_resource_sequence_2:player",
            eventRefs);
    }

    [Fact]
    public async Task AcceptedTurn_ForgedActionAuditFailsClosedWithoutValidatedPlan()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 6m,
            oppositionCurrent: 6m);
        await WriteExchangeAsync(
            context,
            Exchange(
                "exchange_resource_forged_42",
                PlayerAudit("pressure", 3m, 5m, 2m)));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(
            issues,
            issue => issue.Code == "afterlife_conflict_resource_audit_state_mismatch");
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task Preview_UsesCanonicalLedgerProjectionAndNeverLegacyActionEconomy()
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 4m,
            oppositionCurrent: 5m);

        var preview = await new AfterlifeSpiritualConflictTurnPreviewService(
                context.FileSystem)
            .BuildAsync(
                turnNumber: 42,
                preGeneratedDices1d20: [10, 11],
                currentRealm: "Chaos Sea");

        var result = Assert.IsType<JsonObject>(preview);
        Assert.False(result.ContainsKey("playerActionEconomy"));
        Assert.False(result.ContainsKey("oppositionActionEconomy"));
        var player = Assert.IsType<JsonObject>(result["playerActionPoints"]);
        Assert.Equal(4m, player["current"]!.GetValue<decimal>());
        Assert.Equal(6m, player["maximum"]!.GetValue<decimal>());
        Assert.Equal("spiritual_action_points", player["resourceKey"]!.GetValue<string>());
        var opposition = Assert.IsType<JsonObject>(result["oppositionActionPoints"]);
        Assert.Equal(5m, opposition["current"]!.GetValue<decimal>());
        Assert.Equal(6m, opposition["maximum"]!.GetValue<decimal>());
        Assert.Equal(
            "afterlife_conflict_side_cost",
            opposition["resourceOwnerId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preview_UsesAcceptedConditionMechanicsSnapshotOrFailsClosed(
        bool corruptIdentityIndex)
    {
        await using var context = await CreateActionPointContextAsync(
            playerCurrent: 4m,
            oppositionCurrent: 5m);
        var conflict = ActiveConflict("conflict_resource_cost");
        conflict["sideModel"] = "direct_duel";
        conflict["conflictPosition"] = "contested";
        conflict["playerSideStrain"] = "clear";
        conflict["oppositionSideStrain"] = "clear";
        conflict["playerSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "player",
                ["actorId"] = "player_soul",
                ["displayName"] = "Асуран"
            },
            ["supporters"] = new JsonArray()
        };
        conflict["oppositionSide"] = new JsonObject
        {
            ["leadContestant"] = new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_preview_condition",
                ["displayName"] = "Хранитель печати"
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
        };
        var effect = CreatePreviewConditionEffect();
        conflict["combatConditions"] = new JsonArray(effect.DeepClone());
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = conflict,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString());
        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        if (corruptIdentityIndex)
        {
            identity["entries"]![0]!["owner"]!["ownerId"] =
                "conflict_resource_cost:player";
        }
        await context.WriteExactJsonAsync(
            EffectIdentityState.StatePath,
            identity.ToJsonString());

        var preview = await new AfterlifeSpiritualConflictTurnPreviewService(
                context.FileSystem)
            .BuildAsync(
                turnNumber: 42,
                preGeneratedDices1d20: [10, 11],
                currentRealm: "Chaos Sea");

        if (corruptIdentityIndex)
        {
            Assert.Null(preview);
            return;
        }

        var result = Assert.IsType<JsonObject>(preview);
        var mechanics = Assert.IsType<JsonObject>(result["conditionMechanics"]);
        Assert.Equal(
            EffectMechanicsSnapshot.Source,
            mechanics["source"]!.GetValue<string>());
        var contribution = Assert.IsType<JsonObject>(
            Assert.Single(mechanics["contributions"]!.AsArray()));
        Assert.Equal(
            EffectMaterializationTestFixture.EffectId,
            contribution["conditionId"]!.GetValue<string>());
        Assert.Equal(
            ["actionCostAudit.opposition"],
            contribution["mechanicalAxes"]!.AsArray()
                .Select(static axis => axis!.GetValue<string>())
                .ToArray());
        Assert.False(contribution["isPlayerVisible"]!.GetValue<bool>());
        Assert.False(result.ContainsKey("modifiedCharacteristics"));
    }

    [Fact]
    public void ConflictReducer_StartNeverCreatesLegacyActionEconomy()
    {
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(
            AfterlifeSpiritualConflictState.CreateDefaultRoot(),
            new JsonObject
            {
                ["mode"] = AfterlifeSpiritualConflictState.ModeStart,
                ["conflictState"] = ActiveConflict("conflict_resource_start")
            });

        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.False(active.ContainsKey("actionEconomy"));
    }

    [Fact]
    public void ConflictReducer_ExchangeNeverCopiesSubmittedLegacyActionEconomy()
    {
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = ActiveConflict("conflict_resource_exchange"),
                ["recentConflicts"] = new JsonArray()
            },
            new JsonObject
            {
                ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
                ["exchange"] = Exchange(
                    "exchange_resource_no_legacy_copy",
                    PlayerAudit("pressure", 3m, 6m, 3m)),
                ["actionEconomy"] = new JsonObject
                {
                    ["player"] = new JsonObject { ["current"] = 999, ["max"] = 999 }
                }
            });

        var active = Assert.IsType<JsonObject>(projected["activeConflict"]);
        Assert.False(active.ContainsKey("actionEconomy"));
    }

    [Fact]
    public async Task AcceptedTurn_ShiningGachaSpendsScopedLedgerAcrossReturnCycle()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var initialSoul = new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 7
        };
        var initialShining = ShiningAbodeState.CreateDefaultState();
        initialShining["availability"] = ShiningAbodeState.AvailabilityActive;
        Assert.IsType<JsonObject>(initialShining["radiance"])["tier"] = 2;
        await CanonicalResourceQuartetTestFixture.CommitExplicitBootstrapAsync(
            context.FileSystem,
            bootstrap.Definitions!,
            bootstrap.State!,
            bootstrap.History!,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: AfterlifeEntityProfileState.CreateDefaultRoot(),
                SpiritualConflict: AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                SoulState: initialSoul,
                ShiningAbode: initialShining));
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 41,
            currentRealm: "Shining Abode");

        var startedReturn = initialShining.DeepClone().AsObject();
        var startedGacha = Assert.IsType<JsonObject>(startedReturn["gachaSystem"]);
        startedGacha["currentReturnCycleId"] = "shining_return_7";
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            startedReturn.ToJsonString());

        var startIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(
            startIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var startPlan = await PeekPlanAsync(context);
        var initialized = ResolvePlannedResource(
            startPlan,
            ResourceOwnerKind.AfterlifeScope,
            "gacha_attempts");
        Assert.Equal(3m, initialized.Current);
        Assert.Equal(3m, initialized.Maximum);
        await PublishAsync(context);

        var canonicalStart = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        var canonicalGacha = Assert.IsType<JsonObject>(canonicalStart["gachaSystem"]);
        Assert.False(canonicalGacha.ContainsKey("chargesPerReturn"));
        Assert.False(canonicalGacha.ContainsKey("chargesUsedThisReturn"));
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42,
            currentRealm: "Shining Abode");

        var acceptedPull = canonicalStart.DeepClone().AsObject();
        var acceptedGacha = Assert.IsType<JsonObject>(acceptedPull["gachaSystem"]);
        var history = Assert.IsType<JsonArray>(acceptedGacha["gachaHistory"]);
        history.Add(new JsonObject
        {
            ["requestId"] = "request_shining_gacha_42",
            ["returnCycleId"] = "shining_return_7",
            ["relicId"] = "relic_shining_gacha_42",
            ["turnNumber"] = 42
        });
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            acceptedPull.ToJsonString());

        var pullIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(
            pullIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var pullPlan = await PeekPlanAsync(context);
        var remaining = ResolvePlannedResource(
            pullPlan,
            ResourceOwnerKind.AfterlifeScope,
            "gacha_attempts");
        Assert.Equal(2m, remaining.Current);
        var spend = Assert.Single(
            pullPlan.HistoryAfterImage["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["eventRef"]?.GetValue<string>() ==
                     "turn_42:shining_gacha:request_shining_gacha_42");
        Assert.Equal("spend", spend["operation"]!.GetValue<string>());
        Assert.Equal(1m, spend["appliedAmount"]!.GetValue<decimal>());
    }

    [Fact]
    public void LocalShiningReturnCyclePlan_RotatesScopedLedgerThroughCommonHistory()
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        var profiles = AfterlifeEntityProfileState.CreateDefaultRoot();
        var conflict = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var guardians = new JsonObject();
        var firstSoul = new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 7
        };
        var firstShining = ShiningAbodeState.CreateDefaultState();
        firstShining["availability"] = ShiningAbodeState.AvailabilityActive;
        Assert.IsType<JsonObject>(firstShining["radiance"])["tier"] = 2;

        var first = ShiningReturnCycleResourcePlanner.Build(
            new ShiningReturnCycleResourcePlanningInput(
                Turn: 7,
                CurrentIncarnation: 7,
                definitions,
                Assert.IsType<ResourceStateLedger>(bootstrap.State),
                Assert.IsType<ResourceHistoryState>(bootstrap.History),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    conflict,
                    firstSoul,
                    firstShining,
                    guardians),
                profiles,
                firstShining,
                firstSoul));

        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));
        var firstCanonicalShining = Assert.IsType<JsonObject>(first.ShiningAfterImage);
        var firstBinding = Assert.IsType<JsonObject>(
            firstCanonicalShining["resourceOwnerBindings"]?["gachaReturn"]);
        var firstOwnerId = firstBinding["resourceOwnerId"]!.GetValue<string>();
        Assert.Equal("shining_return_7", firstBinding["returnCycleId"]!.GetValue<string>());
        var firstEntry = Assert.Single(
            Assert.IsType<ResourceStateLedger>(first.StateAfterImage).Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeScope &&
                     entry.Coordinate.ResourceOwnerId == firstOwnerId &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal(3m, firstEntry.Current);
        Assert.Equal(3m, firstEntry.Maximum);

        var secondSoul = firstSoul.DeepClone().AsObject();
        secondSoul["currentIncarnation"] = 8;
        var second = ShiningReturnCycleResourcePlanner.Build(
            new ShiningReturnCycleResourcePlanningInput(
                Turn: 8,
                CurrentIncarnation: 8,
                definitions,
                first.StateAfterImage!,
                first.HistoryAfterImage!,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    conflict,
                    firstSoul,
                    firstCanonicalShining,
                    guardians),
                profiles,
                firstCanonicalShining,
                secondSoul));

        Assert.True(second.IsValid, string.Join(Environment.NewLine, second.Issues));
        Assert.True(second.CycleChanged);
        Assert.Equal("shining_return_7", second.PreviousReturnCycleId);
        Assert.Equal("shining_return_8", second.CurrentReturnCycleId);
        var secondShining = Assert.IsType<JsonObject>(second.ShiningAfterImage);
        var secondGacha = Assert.IsType<JsonObject>(secondShining["gachaSystem"]);
        Assert.False(secondGacha.ContainsKey("chargesPerReturn"));
        Assert.False(secondGacha.ContainsKey("chargesUsedThisReturn"));
        var secondBinding = Assert.IsType<JsonObject>(
            secondShining["resourceOwnerBindings"]?["gachaReturn"]);
        var secondOwnerId = secondBinding["resourceOwnerId"]!.GetValue<string>();
        Assert.NotEqual(firstOwnerId, secondOwnerId);
        Assert.Equal("shining_return_8", secondBinding["returnCycleId"]!.GetValue<string>());

        var secondLedger = Assert.IsType<ResourceStateLedger>(second.StateAfterImage);
        Assert.DoesNotContain(
            secondLedger.Entries,
            entry => entry.Coordinate.ResourceOwnerId == firstOwnerId &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");
        var initialized = Assert.Single(
            secondLedger.Entries,
            entry => entry.Coordinate.ResourceOwnerId == secondOwnerId &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal(ResourceLifecycleState.Active, initialized.State);
        Assert.Equal(3m, initialized.Current);
        Assert.Equal(3m, initialized.Maximum);
        Assert.Contains(
            second.HistoryAfterImage!.Transitions,
            transition => transition.Coordinate.ResourceOwnerId == firstOwnerId &&
                          transition.Operation == ResourceTransitionOperation.Retire &&
                          transition.Turn == 8);
        Assert.Contains(
            second.HistoryAfterImage.Transitions,
            transition => transition.Coordinate.ResourceOwnerId == secondOwnerId &&
                          transition.Operation == ResourceTransitionOperation.Initialize &&
                          transition.Turn == 8);
    }

    [Fact]
    public void LocalShiningReturnCyclePlan_ReconfiguresSameScopeFromCurrentRadiance()
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        var profiles = AfterlifeEntityProfileState.CreateDefaultRoot();
        var conflict = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        var guardians = new JsonObject();
        var soul = new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 7
        };
        var initialShining = ShiningAbodeState.CreateDefaultState();
        initialShining["availability"] = ShiningAbodeState.AvailabilityActive;
        Assert.IsType<JsonObject>(initialShining["radiance"])["tier"] = 1;

        var first = ShiningReturnCycleResourcePlanner.Build(
            new ShiningReturnCycleResourcePlanningInput(
                Turn: 7,
                CurrentIncarnation: 7,
                definitions,
                Assert.IsType<ResourceStateLedger>(bootstrap.State),
                Assert.IsType<ResourceHistoryState>(bootstrap.History),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    conflict,
                    soul,
                    initialShining,
                    guardians),
                profiles,
                initialShining,
                soul));
        Assert.True(first.IsValid, string.Join(Environment.NewLine, first.Issues));
        var firstShining = Assert.IsType<JsonObject>(first.ShiningAfterImage);
        var binding = Assert.IsType<JsonObject>(
            firstShining["resourceOwnerBindings"]?["gachaReturn"]);
        var ownerId = binding["resourceOwnerId"]!.GetValue<string>();
        var before = Assert.Single(
            first.StateAfterImage!.Entries,
            entry => entry.Coordinate.ResourceOwnerId == ownerId &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal(2m, before.Current);
        Assert.Equal(2m, before.Maximum);

        var upgradedShining = firstShining.DeepClone().AsObject();
        Assert.IsType<JsonObject>(upgradedShining["radiance"])["tier"] = 3;
        var second = ShiningReturnCycleResourcePlanner.Build(
            new ShiningReturnCycleResourcePlanningInput(
                Turn: 8,
                CurrentIncarnation: 7,
                definitions,
                first.StateAfterImage,
                first.HistoryAfterImage!,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    conflict,
                    soul,
                    firstShining,
                    guardians),
                profiles,
                upgradedShining,
                soul));

        Assert.True(second.IsValid, string.Join(Environment.NewLine, second.Issues));
        Assert.False(second.CycleChanged);
        var afterBinding = Assert.IsType<JsonObject>(
            second.ShiningAfterImage?["resourceOwnerBindings"]?["gachaReturn"]);
        Assert.Equal(ownerId, afterBinding["resourceOwnerId"]!.GetValue<string>());
        var after = Assert.Single(
            second.StateAfterImage!.Entries,
            entry => entry.Coordinate.ResourceOwnerId == ownerId &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal(2m, after.Current);
        Assert.Equal(4m, after.Maximum);
        var reconfigure = Assert.Single(
            second.HistoryAfterImage!.Transitions,
            transition => transition.Turn == 8 &&
                          transition.Coordinate.ResourceOwnerId == ownerId &&
                          transition.Operation == ResourceTransitionOperation.Reconfigure);
        Assert.Equal(2m, reconfigure.BeforeState!.Maximum);
        Assert.Equal(4m, reconfigure.AfterState!.Maximum);
    }

    [Fact]
    public async Task LocalShiningReturnCycleService_CommitsSoulScopeAndLedgerAtomically()
    {
        await using var context = await CreateShiningGachaContextAsync(
            radianceTier: 2,
            turn: 7);
        var initialShining = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        Assert.IsType<JsonObject>(initialShining["gachaSystem"])["currentReturnCycleId"] =
            "shining_return_7";
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            initialShining.ToJsonString());
        var initialIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(initialIssues, issue => issue.Severity == IssueSeverity.Error);
        await PublishAsync(context);

        var canonicalShining = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        var projectedSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        projectedSoul["currentRealm"] = "Shining Abode";
        projectedSoul["currentIncarnation"] = 8;

        var plan = await ShiningReturnCycleResourceService.BuildAsync(
            context.FileSystem,
            canonicalShining,
            projectedSoul,
            ShiningReturnCycleTransitionKind.SynchronizeCurrentShining,
            turn: 8);

        Assert.True(plan.IsValid, string.Join(Environment.NewLine, plan.Issues));
        Assert.True(await ShiningReturnCycleResourceService.TryCommitAsync(
            context.FileSystem,
            plan));
        var committedShining = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        var committedBinding = Assert.IsType<JsonObject>(
            committedShining["resourceOwnerBindings"]?["gachaReturn"]);
        Assert.Equal(
            "shining_return_8",
            committedBinding["returnCycleId"]!.GetValue<string>());
        var committedSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        Assert.Equal(8, committedSoul["currentIncarnation"]!.GetValue<int>());
        var committedState = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            ResourceDefinitionCatalog.ParseCanonical(
                await context.FileSystem.ReadFileAsync(
                    ResourceMaterializationContract.DefinitionsPath),
                allowMissingPristine: false).Catalog!,
            allowMissingPristine: false);
        Assert.True(committedState.IsValid, string.Join(Environment.NewLine, committedState.Issues));
        var ownerId = committedBinding["resourceOwnerId"]!.GetValue<string>();
        var attempts = Assert.Single(
            committedState.Ledger!.Entries,
            entry => entry.Coordinate.ResourceOwnerId == ownerId &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal(3m, attempts.Current);
        Assert.Equal(3m, attempts.Maximum);
        var committedHistory = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            ResourceDefinitionCatalog.ParseCanonical(
                await context.FileSystem.ReadFileAsync(
                    ResourceMaterializationContract.DefinitionsPath),
                allowMissingPristine: false).Catalog!,
            allowMissingPristine: false);
        Assert.True(committedHistory.IsValid, string.Join(Environment.NewLine, committedHistory.Issues));
        Assert.Contains(
            committedHistory.History!.Transitions,
            transition => transition.Coordinate.ResourceOwnerId == ownerId &&
                          transition.Operation == ResourceTransitionOperation.Initialize &&
                          transition.Turn == 8);
    }

    [Fact]
    public async Task LocalShiningReturnCycleService_RejectsEntryFromMortalRealm()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var mortalProfiles = Profiles(PlayerSoulProfile());
        Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(mortalProfiles[
                    AfterlifeEntityProfileState.ProfilesProperty])))
            ["resourceOwnerBindings"]![0]!["state"] = "suspended";
        var mortalSoul = SoulState(spiritFocusTier: 0);
        mortalSoul["currentRealm"] = "Mortal World";
        mortalSoul["currentIncarnation"] = 4;
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        await SeedLocalAfterlifeOwnerStateAsync(
            context,
            mortalProfiles,
            mortalSoul,
            shining);

        var projectedSoul = mortalSoul.DeepClone().AsObject();
        projectedSoul["currentRealm"] = "Shining Abode";
        var plan = await ShiningReturnCycleResourceService.BuildAsync(
            context.FileSystem,
            shining,
            projectedSoul,
            ShiningReturnCycleTransitionKind.OrdinaryReentryFromChaosSea,
            turn: 62);

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.Issues,
            issue => issue.Code == "shining_return_cycle_source_realm_invalid");
    }

    [Fact]
    public async Task LocalShiningReturnCycleService_RejectsMissingPersistedOwnerAuthority()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(PlayerSoulProfile());
        var soul = SoulState(spiritFocusTier: 0);
        var preTurnShining = ShiningAbodeState.CreateDefaultState();
        preTurnShining["availability"] = ShiningAbodeState.AvailabilityActive;
        await SeedLocalAfterlifeOwnerStateAsync(
            context,
            profiles,
            soul,
            preTurnShining,
            bootstrapAuthority: false);

        var acceptedShining = preTurnShining.DeepClone().AsObject();
        Assert.IsType<JsonObject>(acceptedShining["gachaSystem"])[
            "currentReturnCycleId"] = "shining_return_64";
        var projectedSoul = soul.DeepClone().AsObject();
        projectedSoul["currentRealm"] = "Shining Abode";
        projectedSoul["currentIncarnation"] = 8;
        var plan = await ShiningReturnCycleResourceService.BuildAsync(
            context.FileSystem,
            acceptedShining,
            projectedSoul,
            ShiningReturnCycleTransitionKind.OrdinaryReentryFromChaosSea,
            turn: 64);

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.Issues,
            issue => issue.Code == "resource_owner_authority_root_stale");
        Assert.False(context.FileSystem.FileExists(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath));
    }

    [Fact]
    public async Task LocalShiningReturnCycleService_RejectsAscensionWithoutFreshMaximumEnlightenment()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(PlayerSoulProfile());
        var soul = SoulState(spiritFocusTier: 0);
        soul["currentIncarnation"] = 4;
        soul["enlightenment"] = new JsonObject
        {
            ["currentTier"] = "Новичок",
            ["experience"] = 0,
            ["level"] = 0,
            ["progressPercent"] = 0
        };
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        await SeedLocalAfterlifeOwnerStateAsync(context, profiles, soul, shining);

        var projectedSoul = soul.DeepClone().AsObject();
        projectedSoul["currentRealm"] = "Shining Abode";
        var plan = await ShiningReturnCycleResourceService.BuildAsync(
            context.FileSystem,
            shining,
            projectedSoul,
            ShiningReturnCycleTransitionKind.AscensionFromChaosSea,
            turn: 63);

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.Issues,
            issue => issue.Code == "shining_return_cycle_ascension_authority_invalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalShiningReturnCycleService_RequiresFreshConflictFreeAscensionAuthority(
        bool hasPendingLifeTransition)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(PlayerSoulProfile());
        var soul = SoulState(spiritFocusTier: 0);
        soul["currentIncarnation"] = 4;
        soul["soulProgression"] = new JsonObject
        {
            ["totalExperience"] =
                AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience
        };
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        await SeedLocalAfterlifeOwnerStateAsync(context, profiles, soul, shining);
        if (hasPendingLifeTransition)
        {
            await context.WriteExactJsonAsync(
                "game_state/control/life_transitions.json",
                new JsonObject { ["reason"] = "Voluntary" }.ToJsonString());
        }

        var projectedSoul = soul.DeepClone().AsObject();
        projectedSoul["currentRealm"] = "Shining Abode";
        var plan = await ShiningReturnCycleResourceService.BuildAsync(
            context.FileSystem,
            shining,
            projectedSoul,
            ShiningReturnCycleTransitionKind.AscensionFromChaosSea,
            turn: 64);

        Assert.Equal(!hasPendingLifeTransition, plan.IsValid);
        if (hasPendingLifeTransition)
        {
            Assert.Contains(
                plan.Issues,
                issue => issue.Code ==
                         "shining_return_cycle_lifecycle_conflict");
        }
    }

    [Fact]
    public async Task LocalAfterlifeOwnerResourceService_CommitsSpiritFocusAndLedgerAtomically()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            AfterlifeEntityProfileState.CreateDefaultRoot().ToJsonString());
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
        var initialSoul = SoulState(spiritFocusTier: 0);
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject().ToJsonString());
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            ShiningAbodeState.CreateDefaultState().ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            new JsonObject().ToJsonString());
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}");

        var acceptedProfiles = Profiles(PlayerSoulProfile());
        Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(acceptedProfiles[
                    AfterlifeEntityProfileState.ProfilesProperty])))
            .Remove(AfterlifeEntityProfileState.ResourceOwnerBindingsProperty);
        var initialPlan = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: acceptedProfiles,
                SoulState: initialSoul),
            turn: 41);
        Assert.True(initialPlan.IsValid, string.Join(Environment.NewLine, initialPlan.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            initialPlan));

        var upgradedSoul = initialSoul.DeepClone().AsObject();
        Assert.IsType<JsonObject>(
            upgradedSoul[AfterlifeSpiritualConflictState.SoulStateProfileProperty])[
                AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 1;
        var reconfigurePlan = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(SoulState: upgradedSoul),
            turn: 42);

        Assert.True(
            reconfigurePlan.IsValid,
            string.Join(Environment.NewLine, reconfigurePlan.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            reconfigurePlan));
        var committedSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        Assert.Equal(
            1,
            committedSoul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]![
                AfterlifeSpiritualConflictState.SpiritFocusTierProperty]!.GetValue<int>());

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var actionPoints = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Coordinate.ResourceOwnerId == "player_soul" &&
                     entry.Coordinate.ResourceKey == "spiritual_action_points");
        Assert.Equal(6m, actionPoints.Current);
        Assert.Equal(
            AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(1),
            actionPoints.Maximum);

        var history = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var reconfigure = Assert.Single(
            history.History!.Transitions,
            transition => transition.Coordinate.Equals(actionPoints.Coordinate) &&
                          transition.Operation == ResourceTransitionOperation.Reconfigure);
        Assert.Equal(41, actionPoints.Chronology.CreatedAtTurn);
        Assert.Equal(42, reconfigure.Turn);
        Assert.Equal(6m, reconfigure.BeforeState!.Maximum);
        Assert.Equal(actionPoints.Maximum, reconfigure.AfterState!.Maximum);
    }

    [Fact]
    public async Task LocalAfterlifeOwnerResourceService_RejectsMissingPersistedOwnerAuthority()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(PlayerSoulProfile());
        var soul = SoulState(spiritFocusTier: 0);
        await SeedLocalAfterlifeOwnerStateAsync(
            context,
            profiles,
            soul,
            ShiningAbodeState.CreateDefaultState(),
            bootstrapAuthority: false);

        var before = await context.CaptureAsync(
            new[]
            {
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath,
                AfterlifeEntityProfileState.StatePath,
                "game_state/meta/soul_state.json"
            });
        var plan = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(SoulState: soul),
            turn: 41);

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.Issues,
            issue => issue.Code == "resource_owner_authority_root_stale");
        await context.AssertUnchangedAsync(before);
        Assert.False(context.FileSystem.FileExists(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath));
    }

    [Fact]
    public async Task LocalAfterlifeOwnerResourceService_RejectsStalePersistedOwnerAuthority()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(PlayerSoulProfile());
        var soul = SoulState(spiritFocusTier: 0);
        await SeedLocalAfterlifeOwnerStateAsync(
            context,
            profiles,
            soul,
            ShiningAbodeState.CreateDefaultState());
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            "{\"schemaVersion\":1,\"historicalOwners\":[{\"realm\":\"chaos_sea\",\"ownerKind\":\"afterlife_actor\",\"resourceOwnerId\":\"forged\"}],\"capacityDrafts\":[]}");
        var before = await context.CaptureAsync(
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath);

        var plan = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(SoulState: soul),
            turn: 41);

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.Issues,
            issue => issue.Code == "resource_owner_authority_root_stale");
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task ShiningBlessingSpend_RejectsStaleAuthorityWithoutSelfHealing()
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var entitlement = Assert.IsType<JsonObject>(
            Assert.IsType<JsonObject>(
                soul[ShiningBlessingEffectState.SoulStateProperty])[
                    "memorySelection"]);
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}");
        var before = await context.CaptureAsync(
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath);

        var plan = await ShiningBlessingRerollResourceService.BuildSpendAsync(
            context.FileSystem,
            entitlement,
            turn: 43,
            amount: 1);

        Assert.False(plan.IsValid);
        Assert.Contains(
            plan.Issues,
            issue => issue.Code == "resource_owner_authority_root_stale");
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task LocalAfterlifeOwnerResourceService_SuspendsAndResumesActorResourcesAcrossRealms()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var initialProfiles = Profiles(PlayerSoulProfile());
        var initialSoul = SoulState(spiritFocusTier: 0);
        var bootstrapProfiles = initialProfiles.DeepClone().AsObject();
        Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(bootstrapProfiles[
                    AfterlifeEntityProfileState.ProfilesProperty])))
            .Remove(AfterlifeEntityProfileState.ResourceOwnerBindingsProperty);
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: bootstrapProfiles,
                SoulState: initialSoul));
        initialProfiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));

        var initialize = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: initialProfiles,
                SoulState: initialSoul),
            turn: 51);
        Assert.True(initialize.IsValid, string.Join(Environment.NewLine, initialize.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            initialize));

        var shiningProfiles = initialProfiles.DeepClone().AsObject();
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
            shiningProfiles[AfterlifeEntityProfileState.ProfilesProperty])[0])["realm"] =
            "Shining Abode";
        var shiningSoul = initialSoul.DeepClone().AsObject();
        shiningSoul["currentRealm"] = "Shining Abode";
        var enterShining = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: shiningProfiles,
                SoulState: shiningSoul),
            turn: 52);
        Assert.True(enterShining.IsValid, string.Join(Environment.NewLine, enterShining.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            enterShining));

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        var shiningState = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(shiningState.IsValid, string.Join(Environment.NewLine, shiningState.Issues));
        var chaosEntry = Assert.Single(
            shiningState.Ledger!.Entries,
            entry => entry.Coordinate.Realm == "chaos_sea" &&
                     entry.Coordinate.ResourceOwnerId == "player_soul" &&
                     entry.Coordinate.ResourceKey == "spiritual_action_points");
        var shiningEntry = Assert.Single(
            shiningState.Ledger.Entries,
            entry => entry.Coordinate.Realm == "shining_abode" &&
                     entry.Coordinate.ResourceOwnerId == "player_soul" &&
                     entry.Coordinate.ResourceKey == "spiritual_action_points");
        Assert.Equal(ResourceLifecycleState.Suspended, chaosEntry.State);
        Assert.Equal(ResourceLifecycleState.Active, shiningEntry.State);

        var returnProfiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
            returnProfiles[AfterlifeEntityProfileState.ProfilesProperty])[0])["realm"] =
            "Chaos Sea";
        var returnSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        returnSoul["currentRealm"] = "Chaos Sea";
        var returnToChaos = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: returnProfiles,
                SoulState: returnSoul),
            turn: 53);
        Assert.True(returnToChaos.IsValid, string.Join(Environment.NewLine, returnToChaos.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            returnToChaos));

        var returnedState = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(returnedState.IsValid, string.Join(Environment.NewLine, returnedState.Issues));
        chaosEntry = Assert.Single(
            returnedState.Ledger!.Entries,
            entry => entry.Coordinate.Equals(chaosEntry.Coordinate));
        shiningEntry = Assert.Single(
            returnedState.Ledger.Entries,
            entry => entry.Coordinate.Equals(shiningEntry.Coordinate));
        Assert.Equal(ResourceLifecycleState.Active, chaosEntry.State);
        Assert.Equal(ResourceLifecycleState.Suspended, shiningEntry.State);

        var history = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        Assert.Contains(history.History!.Transitions, transition =>
            transition.Turn == 52 &&
            transition.Coordinate.Equals(chaosEntry.Coordinate) &&
            transition.Operation == ResourceTransitionOperation.Suspend);
        Assert.Contains(history.History.Transitions, transition =>
            transition.Turn == 52 &&
            transition.Coordinate.Equals(shiningEntry.Coordinate) &&
            transition.Operation == ResourceTransitionOperation.Initialize);
        Assert.Contains(history.History.Transitions, transition =>
            transition.Turn == 53 &&
            transition.Coordinate.Equals(chaosEntry.Coordinate) &&
            transition.Operation == ResourceTransitionOperation.Resume);
        Assert.Contains(history.History.Transitions, transition =>
            transition.Turn == 53 &&
            transition.Coordinate.Equals(shiningEntry.Coordinate) &&
            transition.Operation == ResourceTransitionOperation.Suspend);
    }

    [Fact]
    public async Task LocalShiningReturnCycleService_PublishesPlayerProfileRealmAndResourcesAtomically()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(PlayerSoulProfile());
        var soul = SoulState(spiritFocusTier: 0);
        soul["currentIncarnation"] = 4;
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        Assert.IsType<JsonObject>(shining["radiance"])["tier"] = 2;
        await SeedLocalAfterlifeOwnerStateAsync(context, profiles, soul, shining);

        var initialize = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: profiles,
                SoulState: soul,
                ShiningAbode: shining),
            turn: 61);
        Assert.True(initialize.IsValid, string.Join(Environment.NewLine, initialize.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            initialize));

        var shiningSoul = soul.DeepClone().AsObject();
        shiningSoul["currentRealm"] = "Shining Abode";
        var returnCycle = await ShiningReturnCycleResourceService.BuildAsync(
            context.FileSystem,
            shining,
            shiningSoul,
            ShiningReturnCycleTransitionKind.OrdinaryReentryFromChaosSea,
            turn: 62);

        Assert.True(returnCycle.IsValid, string.Join(Environment.NewLine, returnCycle.Issues));
        Assert.True(await ShiningReturnCycleResourceService.TryCommitAsync(
            context.FileSystem,
            returnCycle));

        var committedProfiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));
        var player = Assert.Single(Assert.IsType<JsonArray>(
            committedProfiles[AfterlifeEntityProfileState.ProfilesProperty]).OfType<JsonObject>());
        Assert.Equal("Shining Abode", player["realm"]!.GetValue<string>());
        var bindings = Assert.IsType<JsonArray>(player["resourceOwnerBindings"])
            .OfType<JsonObject>()
            .ToDictionary(
                binding => binding["realm"]!.GetValue<string>(),
                binding => binding["state"]!.GetValue<string>(),
                StringComparer.Ordinal);
        Assert.Equal("suspended", bindings["chaos_sea"]);
        Assert.Equal("active", bindings["shining_abode"]);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var chaosActionPoints = Assert.Single(state.Ledger!.Entries, entry =>
            entry.Coordinate.Realm == "chaos_sea" &&
            entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
            entry.Coordinate.ResourceOwnerId == "player_soul" &&
            entry.Coordinate.ResourceKey == "spiritual_action_points");
        var shiningActionPoints = Assert.Single(state.Ledger.Entries, entry =>
            entry.Coordinate.Realm == "shining_abode" &&
            entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
            entry.Coordinate.ResourceOwnerId == "player_soul" &&
            entry.Coordinate.ResourceKey == "spiritual_action_points");
        Assert.Equal(ResourceLifecycleState.Suspended, chaosActionPoints.State);
        Assert.Equal(ResourceLifecycleState.Active, shiningActionPoints.State);
    }

    [Fact]
    public async Task LocalAfterlifeOwnerResourceService_MortalTransitionSuspendsRealmBoundResourceButKeepsPersistentCapabilityActive()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(PlayerSoulProfile());
        var soul = SoulState(spiritFocusTier: 0);
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        await SeedLocalAfterlifeOwnerStateAsync(context, profiles, soul, shining);

        var initialize = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: profiles,
                SoulState: soul,
                ShiningAbode: shining),
            turn: 71);
        Assert.True(initialize.IsValid, string.Join(Environment.NewLine, initialize.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            initialize));

        var shiningProfiles = profiles.DeepClone().AsObject();
        Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(
            shiningProfiles[AfterlifeEntityProfileState.ProfilesProperty])[0])["realm"] =
            "Shining Abode";
        var shiningSoul = soul.DeepClone().AsObject();
        shiningSoul["currentRealm"] = "Shining Abode";
        var enterShining = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: shiningProfiles,
                SoulState: shiningSoul),
            turn: 72);
        Assert.True(enterShining.IsValid, string.Join(Environment.NewLine, enterShining.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            enterShining));

        var rerollPlan = await ShiningBlessingRerollResourceService.BuildBootstrapAsync(
            context.FileSystem,
            new JsonObject
            {
                ["sourcePackagePreparedAtTurn"] = 73,
                ["sourceCardIds"] = new JsonArray("card_memory"),
                ["sourceCardCount"] = 1,
                ["memorySelection"] = new JsonObject
                {
                    [ShiningBlessingRerollAllocationContract.PropertyName] =
                        ShiningBlessingRerollAllocationContract.Create(2),
                    ["sourceCardIds"] = new JsonArray("card_memory")
                }
            },
            currentIncarnation: 4);
        Assert.True(rerollPlan.IsValid, string.Join(Environment.NewLine, rerollPlan.Issues));
        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(
            context.FileSystem,
            rerollPlan.Writes.ToArray()));

        var mortalProfiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));
        var mortalSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        mortalSoul["currentRealm"] = "Mortal World";
        var enterMortal = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: mortalProfiles,
                SoulState: mortalSoul),
            turn: 74);

        Assert.True(enterMortal.IsValid, string.Join(Environment.NewLine, enterMortal.Issues));
        Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
            context.FileSystem,
            enterMortal));

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var actionPoints = Assert.Single(state.Ledger!.Entries, entry =>
            entry.Coordinate.Realm == "shining_abode" &&
            entry.Coordinate.ResourceOwnerId == "player_soul" &&
            entry.Coordinate.ResourceKey == "spiritual_action_points");
        var rerolls = Assert.Single(state.Ledger.Entries, entry =>
            entry.Coordinate.Realm == "shining_abode" &&
            entry.Coordinate.ResourceOwnerId == "player_soul" &&
            entry.Coordinate.ResourceKey == "blessing_rerolls");
        Assert.Equal(ResourceLifecycleState.Suspended, actionPoints.State);
        Assert.Equal(ResourceLifecycleState.Active, rerolls.State);
        Assert.DoesNotContain(state.Ledger.Entries, entry =>
            entry.Coordinate.Realm == "mortal_world" &&
            entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);

        var committedProfiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));
        var player = Assert.Single(Assert.IsType<JsonArray>(
            committedProfiles[AfterlifeEntityProfileState.ProfilesProperty]).OfType<JsonObject>());
        Assert.All(
            Assert.IsType<JsonArray>(player["resourceOwnerBindings"]).OfType<JsonObject>(),
            binding => Assert.Equal("suspended", binding["state"]!.GetValue<string>()));

        var committedSoul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var stableMortalPlan = await AfterlifeOwnerResourceStateService.BuildAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: committedProfiles,
                SoulState: committedSoul),
            turn: 75);
        Assert.True(
            stableMortalPlan.IsValid,
            string.Join(Environment.NewLine, stableMortalPlan.Issues));
    }

    [Fact]
    public async Task AcceptedTurn_ShiningGachaCanSpendNewScopeInCreationTurn()
    {
        await using var context = await CreateShiningGachaContextAsync(
            radianceTier: 2,
            turn: 51);
        var accepted = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        var gacha = Assert.IsType<JsonObject>(accepted["gachaSystem"]);
        gacha["currentReturnCycleId"] = "shining_return_51";
        Assert.IsType<JsonArray>(gacha["gachaHistory"]).Add(
            ShiningGachaHistoryEntry(
                "request_shining_gacha_51",
                "shining_return_51",
                turn: 51));
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            accepted.ToJsonString());

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        var remaining = ResolvePlannedResource(
            plan,
            ResourceOwnerKind.AfterlifeScope,
            "gacha_attempts");
        Assert.Equal(3m, remaining.Maximum);
        Assert.Equal(2m, remaining.Current);
        Assert.Contains(
            plan.HistoryAfterImage["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["eventRef"]?.GetValue<string>() ==
                     "turn_51:shining_gacha:request_shining_gacha_51" &&
                     entry["operation"]?.GetValue<string>() == "spend");

        await PublishShiningWithFullNormalizerAsync(context);
        var canonical = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        Assert.IsType<JsonObject>(canonical["resourceOwnerBindings"]?["gachaReturn"]);
        var canonicalGacha = Assert.IsType<JsonObject>(canonical["gachaSystem"]);
        Assert.False(canonicalGacha.ContainsKey("chargesPerReturn"));
        Assert.False(canonicalGacha.ContainsKey("chargesUsedThisReturn"));
    }

    [Fact]
    public async Task AcceptedTurn_ShiningGachaSpendsMultipleAttemptsInExactHistoryOrder()
    {
        await using var context = await CreateShiningGachaContextAsync(
            radianceTier: 2,
            turn: 61);
        var accepted = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        var gacha = Assert.IsType<JsonObject>(accepted["gachaSystem"]);
        gacha["currentReturnCycleId"] = "shining_return_61";
        var history = Assert.IsType<JsonArray>(gacha["gachaHistory"]);
        history.Add(ShiningGachaHistoryEntry(
            "request_shining_gacha_61_a",
            "shining_return_61",
            turn: 61));
        history.Add(ShiningGachaHistoryEntry(
            "request_shining_gacha_61_b",
            "shining_return_61",
            turn: 61));
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            accepted.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        var remaining = ResolvePlannedResource(
            plan,
            ResourceOwnerKind.AfterlifeScope,
            "gacha_attempts");
        Assert.Equal(1m, remaining.Current);
        var spends = plan.HistoryAfterImage["entries"]!.AsArray()
            .OfType<JsonObject>()
            .Where(entry => entry["eventRef"]?.GetValue<string>()?.Contains(
                ":shining_gacha:",
                StringComparison.Ordinal) == true)
            .OrderBy(entry => entry["executionSequence"]!.GetValue<int>())
            .ToArray();
        Assert.Equal(2, spends.Length);
        Assert.Equal(3m, spends[0]["beforeState"]?["current"]!.GetValue<decimal>());
        Assert.Equal(2m, spends[0]["afterState"]?["current"]!.GetValue<decimal>());
        Assert.Equal(2m, spends[1]["beforeState"]?["current"]!.GetValue<decimal>());
        Assert.Equal(1m, spends[1]["afterState"]?["current"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task AcceptedTurn_ShiningGachaRejectsAtomicOverdraw()
    {
        await using var context = await CreateShiningGachaContextAsync(
            radianceTier: 0,
            turn: 71);
        var stateBefore = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.StatePath);
        var historyBefore = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.HistoryPath);
        var accepted = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ShiningAbodeState.StatePath));
        var gacha = Assert.IsType<JsonObject>(accepted["gachaSystem"]);
        gacha["currentReturnCycleId"] = "shining_return_71";
        var history = Assert.IsType<JsonArray>(gacha["gachaHistory"]);
        history.Add(ShiningGachaHistoryEntry(
            "request_shining_gacha_71_a",
            "shining_return_71",
            turn: 71));
        history.Add(ShiningGachaHistoryEntry(
            "request_shining_gacha_71_b",
            "shining_return_71",
            turn: 71));
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            accepted.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(
            issues,
            issue => issue.Code == "afterlife_shining_gacha_resource_exhausted");
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
        Assert.Equal(
            stateBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            historyBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
    }

    [Fact]
    public async Task AcceptedTurn_GuardianReturnInitializesPersistentAttemptLedger()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedGuardianGachaPreTurnAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 81,
            currentRealm: "Chaos Sea");
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            GuardianReturnState(
                "guardian_vesna",
                "chaos_return_81",
                reputation: 50,
                abodePower: 0).ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        var attempts = ResolvePlannedResource(
            plan,
            ResourceOwnerKind.AfterlifeActor,
            "gacha_attempts");
        Assert.Equal("guardian_vesna", attempts.Coordinate.ResourceOwnerId);
        Assert.Equal(2m, attempts.Current);
        Assert.Equal(2m, attempts.Maximum);
    }

    [Fact]
    public async Task AcceptedTurn_GuardianNewReturnReconfiguresAndRefillsPersistentAttemptLedger()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedGuardianGachaPreTurnAsync(context);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 81,
            currentRealm: "Chaos Sea");
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            GuardianReturnState(
                "guardian_vesna",
                "chaos_return_81",
                reputation: 50,
                abodePower: 0).ToJsonString());

        var initializeIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(
            initializeIssues,
            issue => issue.Severity == IssueSeverity.Error);
        await PublishAsync(context);
        await SpendOneGuardianAttemptFixtureAsync(context, definitions, turn: 81);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 82,
            currentRealm: "Chaos Sea");

        var accepted = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/guardians.json"));
        var guardian = Assert.Single(
            Assert.IsType<JsonArray>(accepted["guardians"]).OfType<JsonObject>());
        Assert.IsType<JsonObject>(guardian["gachaSystem"])["currentReturnCycleId"] =
            "chaos_return_82";
        var activeGuardian = Assert.IsType<JsonObject>(accepted["activeGuardian"]);
        Assert.IsType<JsonObject>(activeGuardian["gachaSystem"])["currentReturnCycleId"] =
            "chaos_return_82";
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            accepted.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        var attempts = ResolvePlannedResource(
            plan,
            ResourceOwnerKind.AfterlifeActor,
            "gacha_attempts");
        Assert.Equal(2m, attempts.Current);
        Assert.Equal(2m, attempts.Maximum);

        var history = ResourceHistoryState.ParseCanonical(
            plan.HistoryAfterImage.ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var cycleTransitions = history.History!.Transitions
            .Where(transition =>
                transition.Turn == 82 &&
                ResourceCoordinateComparer.Instance.Equals(
                    transition.Coordinate,
                    attempts.Coordinate))
            .OrderBy(transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Equal(2, cycleTransitions.Length);
        Assert.Equal(ResourceTransitionOperation.Reconfigure, cycleTransitions[0].Operation);
        Assert.Equal(ResourceCapacityDisposition.ClampToNewMaximum,
            cycleTransitions[0].CapacityDisposition);
        Assert.Equal(1m, cycleTransitions[0].AfterState!.Current);
        Assert.Equal(ResourceTransitionOperation.Gain, cycleTransitions[1].Operation);
        Assert.Equal("registered_system_outcome", cycleTransitions[1].OriginKind);
        Assert.Equal(1m, cycleTransitions[1].RequestedAmount);
        Assert.Equal(1m, cycleTransitions[1].AppliedAmount);
        Assert.Equal(2m, cycleTransitions[1].AfterState!.Current);
    }

    [Fact]
    public async Task AcceptedTurn_GuardianProcessGachaSpendsLedgerAndPublishesHistoryAtomically()
    {
        await using var context = await CreateInitializedGuardianGachaContextAsync(
            initialTurn: 91,
            returnCycleId: "chaos_return_91");
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 92,
            currentRealm: "Chaos Sea");
        var accepted = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/guardians.json"));
        accepted["UpdateGuardians"] = new JsonArray
        {
            new JsonObject
            {
                ["command"] = "processGacha",
                ["guardianId"] = "guardian_vesna",
                ["inkFeathersSpent"] = 50,
                ["gachaBonusAudit"] = new JsonObject
                {
                    ["baseRarity"] = "Common",
                    ["abodePowerBonusSteps"] = 0,
                    ["guardianBonusSteps"] = 0,
                    ["finalRarity"] = "Common"
                },
                ["result"] = new JsonObject
                {
                    ["relicId"] = "relic_guardian_92",
                    ["name"] = "Test guardian relic",
                    ["rarity"] = "Common"
                }
            }
        };
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            accepted.ToJsonString());

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        var attempts = ResolvePlannedResource(
            plan,
            ResourceOwnerKind.AfterlifeActor,
            "gacha_attempts");
        Assert.Equal(1m, attempts.Current);
        Assert.Contains(
            ResourceHistoryState.ParseCanonical(
                plan.HistoryAfterImage.ToJsonString(),
                ResourceDefinitionCatalog.ParseCanonical(
                    (await context.FileSystem.ReadFileAsync(
                        ResourceMaterializationContract.DefinitionsPath))!,
                    allowMissingPristine: false).Catalog!,
                allowMissingPristine: false).History!.Transitions,
            transition =>
                transition.Turn == 92 &&
                transition.Operation == ResourceTransitionOperation.Spend &&
                transition.OriginKind == "registered_system_outcome" &&
                transition.RequestedAmount == 1m &&
                transition.AppliedAmount == 1m);

        await PublishWithFullNormalizerAsync(context);
        var canonical = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/guardians.json"));
        Assert.False(canonical.ContainsKey("UpdateGuardians"));
        var guardian = Assert.Single(
            Assert.IsType<JsonArray>(canonical["guardians"]).OfType<JsonObject>());
        var gacha = Assert.IsType<JsonObject>(guardian["gachaSystem"]);
        Assert.False(gacha.ContainsKey("chargesPerReturn"));
        Assert.False(gacha.ContainsKey("chargesUsedThisReturn"));
        var historyEntry = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(gacha["gachaHistory"])));
        Assert.Equal("relic_guardian_92", historyEntry["relicId"]!.GetValue<string>());
        Assert.Equal(50, historyEntry["costInFeathers"]!.GetValue<int>());
        Assert.Equal("Common", historyEntry["finalRarity"]!.GetValue<string>());
        Assert.Equal("2026-08-15T00:00:00Z", historyEntry["timestamp"]!.GetValue<string>());
        Assert.True(ResourceMaterializationContract.IsExactIdentifier(
            historyEntry["eventId"]!.GetValue<string>()));
        var activeGuardian = Assert.IsType<JsonObject>(canonical["activeGuardian"]);
        Assert.True(JsonNode.DeepEquals(guardian, activeGuardian));
    }

    [Fact]
    public async Task AcceptedTurn_GuardianProcessGachaSpendsMultipleAttemptsInExactHistoryOrder()
    {
        await using var context = await CreateInitializedGuardianGachaContextAsync(
            initialTurn: 93,
            returnCycleId: "chaos_return_93");
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 94,
            currentRealm: "Chaos Sea");
        var accepted = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/guardians.json"));
        accepted["UpdateGuardians"] = new JsonArray
        {
            GuardianGachaCommand("relic_guardian_94_a", inkFeathersSpent: 40),
            GuardianGachaCommand("relic_guardian_94_b", inkFeathersSpent: 60)
        };
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            accepted.ToJsonString());

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(
            itemIssues,
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var plan = await PeekPlanAsync(context);
        Assert.Equal(
            0m,
            ResolvePlannedResource(
                plan,
                ResourceOwnerKind.AfterlifeActor,
                "gacha_attempts").Current);
        var spends = ResourceHistoryState.ParseCanonical(
                plan.HistoryAfterImage.ToJsonString(),
                ResourceDefinitionCatalog.ParseCanonical(
                    (await context.FileSystem.ReadFileAsync(
                        ResourceMaterializationContract.DefinitionsPath))!,
                    allowMissingPristine: false).Catalog!,
                allowMissingPristine: false).History!.Transitions
            .Where(transition =>
                transition.Turn == 94 &&
                transition.Operation == ResourceTransitionOperation.Spend &&
                transition.OriginKind == "registered_system_outcome")
            .OrderBy(static transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Equal(2, spends.Length);
        Assert.Equal(2m, spends[0].BeforeState!.Current);
        Assert.Equal(1m, spends[0].AfterState!.Current);
        Assert.Equal(1m, spends[1].BeforeState!.Current);
        Assert.Equal(0m, spends[1].AfterState!.Current);

        await PublishWithFullNormalizerAsync(context);
        var canonical = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/guardians.json"));
        var guardian = Assert.Single(
            Assert.IsType<JsonArray>(canonical["guardians"]).OfType<JsonObject>());
        var history = Assert.IsType<JsonArray>(guardian["gachaSystem"]?["gachaHistory"]);
        Assert.Collection(
            history.OfType<JsonObject>(),
            first => Assert.Equal(
                "relic_guardian_94_a",
                first["relicId"]!.GetValue<string>()),
            second => Assert.Equal(
                "relic_guardian_94_b",
                second["relicId"]!.GetValue<string>()));
    }

    [Fact]
    public async Task AcceptedTurn_GuardianProcessGachaRejectsAtomicOverdraw()
    {
        await using var context = await CreateInitializedGuardianGachaContextAsync(
            initialTurn: 95,
            returnCycleId: "chaos_return_95");
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 96,
            currentRealm: "Chaos Sea");
        var stateBefore = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.StatePath);
        var historyBefore = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.HistoryPath);
        var accepted = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/guardians.json"));
        accepted["UpdateGuardians"] = new JsonArray
        {
            GuardianGachaCommand("relic_guardian_96_a", inkFeathersSpent: 40),
            GuardianGachaCommand("relic_guardian_96_b", inkFeathersSpent: 50),
            GuardianGachaCommand("relic_guardian_96_c", inkFeathersSpent: 60)
        };
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            accepted.ToJsonString());
        var guardianBefore = await context.FileSystem.ReadFileBytesAsync(
            "game_state/meta/guardians.json");

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(
            issues,
            issue => issue.Code == "afterlife_guardian_gacha_resource_exhausted");
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
        Assert.Equal(
            stateBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            historyBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        Assert.Equal(
            guardianBefore,
            await context.FileSystem.ReadFileBytesAsync(
                "game_state/meta/guardians.json"));
    }

    [Fact]
    public async Task BlessingBootstrap_MaterializesRerollsIntoOneActorLedgerWithoutNumericSoulMirrors()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            definitions.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        var profiles = Profiles(new JsonObject
            {
                ["actorType"] = "player_soul",
                ["actorId"] = "player_soul",
                ["displayName"] = "Душа игрока",
                ["realm"] = "Shining Abode",
                ["resourceOwnerBindings"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["realm"] = "shining_abode",
                        ["resourceOwnerId"] = "player_soul",
                        ["state"] = "suspended"
                    }
                }
            });
        var bootstrapSoul = BlessingSoulState();
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            profiles.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            bootstrapSoul.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/core/player_status.json",
            new JsonObject { ["money"] = 0 }.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());

        await SeedLocalAfterlifeOwnerStateAsync(
            context,
            profiles,
            bootstrapSoul,
            ShiningAbodeState.CreateDefaultState());

        var result = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
            context.FileSystem,
            BlessingRerollPackage(),
            currentIncarnation: 3);

        Assert.True(result.Success, result.ErrorMessage);
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var blessing = Assert.IsType<JsonObject>(
            soul[ShiningBlessingEffectState.SoulStateProperty]);
        var memory = Assert.IsType<JsonObject>(blessing["memorySelection"]);
        var relic = Assert.IsType<JsonObject>(blessing["relicRefinementEntitlements"]);
        Assert.False(memory.ContainsKey("rerolls"));
        Assert.False(memory.ContainsKey("rerollsSpent"));
        Assert.False(relic.ContainsKey("rerolls"));
        Assert.False(relic.ContainsKey("rerollsSpent"));
        Assert.True(relic["freeShape"]!.GetValue<bool>());
        Assert.False(relic["freeRetune"]!.GetValue<bool>());

        var state = ResourceStateContract.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.StatePath))!,
            definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var rerolls = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate == new ResourceCoordinate(
                "shining_abode",
                ResourceOwnerKind.AfterlifeActor,
                "player_soul",
                "blessing_rerolls"));
        Assert.Equal(3m, rerolls.Current);
        Assert.Equal(3m, rerolls.Maximum);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.HistoryPath))!,
            definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var grants = history.History!.Transitions
            .Where(transition =>
                transition.Coordinate == rerolls.Coordinate &&
                transition.Operation == ResourceTransitionOperation.Gain)
            .OrderBy(transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Collection(
            grants,
            memoryGrant =>
            {
                Assert.Equal("shining_blessing_memory_42_3", memoryGrant.OriginId);
                Assert.Equal(1m, memoryGrant.AppliedAmount);
            },
            relicGrant =>
            {
                Assert.Equal("shining_blessing_relic_42_3", relicGrant.OriginId);
                Assert.Equal(2m, relicGrant.AppliedAmount);
            });
    }

    [Fact]
    public async Task BlessingMemorySelection_SpendsOnlyItsHistoryAllocationFromActorLedger()
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;

        var pending = await ShiningBlessingEffectState
            .ReadPendingMemorySelectionAsync(context.FileSystem);

        Assert.NotNull(pending);
        Assert.Equal(1, pending!.Rerolls);

        var changed = await ShiningBlessingEffectState
            .ConsumePendingMemorySelectionAsync(
                context.FileSystem,
                currentTurnNumber: 43,
                selectedCandidate: null,
                rerollsSpent: 1);

        Assert.True(changed);
        var state = ResourceStateContract.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.StatePath))!,
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "blessing_rerolls");
        Assert.True(state.Ledger!.TryResolveExact(coordinate, out var rerolls));
        Assert.Equal(2m, rerolls!.Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.HistoryPath))!,
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var spend = Assert.Single(
            history.History!.Transitions,
            transition =>
                transition.Coordinate == coordinate &&
                transition.Operation == ResourceTransitionOperation.Spend &&
                transition.OriginId == "shining_blessing_memory_42_3");
        Assert.Equal(1m, spend.AppliedAmount);

        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var memory = Assert.IsType<JsonObject>(
            soul[ShiningBlessingEffectState.SoulStateProperty]?["memorySelection"]);
        Assert.Equal(
            ShiningBlessingEffectState.GenericStatusConsumed,
            memory["status"]!.GetValue<string>());
        Assert.False(memory.ContainsKey("rerolls"));
        Assert.False(memory.ContainsKey("rerollsSpent"));
    }

    [Fact]
    public async Task BlessingRelicRefinement_SpendsOnlyItsHistoryAllocationFromActorLedger()
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;

        var changed = await ShiningBlessingEffectState.ConsumeRelicRerollsAsync(
            context.FileSystem,
            currentTurnNumber: 44,
            rerollsToConsume: 2);

        Assert.True(changed);
        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "blessing_rerolls");
        var state = ResourceStateContract.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.StatePath))!,
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        Assert.True(state.Ledger!.TryResolveExact(coordinate, out var rerolls));
        Assert.Equal(1m, rerolls!.Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.HistoryPath))!,
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var spend = Assert.Single(
            history.History!.Transitions,
            transition =>
                transition.Coordinate == coordinate &&
                transition.Operation == ResourceTransitionOperation.Spend &&
                transition.OriginId == "shining_blessing_relic_42_3");
        Assert.Equal(2m, spend.AppliedAmount);

        var memory = await ShiningBlessingEffectState
            .ReadPendingMemorySelectionAsync(context.FileSystem);
        Assert.NotNull(memory);
        Assert.Equal(1, memory!.Rerolls);
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var relic = Assert.IsType<JsonObject>(
            soul[ShiningBlessingEffectState.SoulStateProperty]?["relicRefinementEntitlements"]);
        Assert.Equal(
            ShiningBlessingEffectState.RelicStatusPendingEntitlement,
            relic["status"]!.GetValue<string>());
        Assert.True(relic["freeShape"]!.GetValue<bool>());
        Assert.False(relic.ContainsKey("rerolls"));
        Assert.False(relic.ContainsKey("rerollsSpent"));
    }

    [Fact]
    public async Task BlessingBootstrap_ExactReplayAfterSpend_DoesNotReplenishOrResetAllocation()
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;
        Assert.True(await ShiningBlessingEffectState.ConsumeRelicRerollAsync(
            context.FileSystem,
            currentTurnNumber: 44));

        var soulBefore = await context.FileSystem.ReadFileAsync(
            "game_state/meta/soul_state.json");
        var stateBefore = await context.FileSystem.ReadFileAsync(
            ResourceMaterializationContract.StatePath);
        var historyBefore = await context.FileSystem.ReadFileAsync(
            ResourceMaterializationContract.HistoryPath);

        var replay = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
            context.FileSystem,
            BlessingRerollPackage(),
            currentIncarnation: 3);

        Assert.True(replay.Success, replay.ErrorMessage);
        Assert.False(replay.StateChanged);
        Assert.Equal(
            soulBefore,
            await context.FileSystem.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(
            stateBefore,
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath));
        Assert.Equal(
            historyBefore,
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlessingBootstrap_ExactReplayRejectsMissingOrStaleAuthority(
        bool staleInsteadOfMissing)
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;
        if (staleInsteadOfMissing)
        {
            await context.WriteExactJsonAsync(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}");
        }
        else
        {
            await context.DeleteAsync(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath);
        }
        var before = await context.CaptureAsync(
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath);

        var replay = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
            context.FileSystem,
            BlessingRerollPackage(),
            currentIncarnation: 3);

        Assert.False(replay.Success);
        Assert.False(replay.StateChanged);
        Assert.Contains(
            "resource_owner_authority_root_stale",
            replay.ErrorMessage,
            StringComparison.Ordinal);
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task BlessingRerolls_CannotSpendAnotherAllocationThroughSharedActorBalance()
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;
        var soulBefore = await context.FileSystem.ReadFileAsync(
            "game_state/meta/soul_state.json");
        var stateBefore = await context.FileSystem.ReadFileAsync(
            ResourceMaterializationContract.StatePath);
        var historyBefore = await context.FileSystem.ReadFileAsync(
            ResourceMaterializationContract.HistoryPath);

        Assert.False(await ShiningBlessingEffectState
            .ConsumePendingMemorySelectionAsync(
                context.FileSystem,
                currentTurnNumber: 43,
                selectedCandidate: null,
                rerollsSpent: 2));
        Assert.False(await ShiningBlessingEffectState.ConsumeRelicRerollsAsync(
            context.FileSystem,
            currentTurnNumber: 43,
            rerollsToConsume: 3));

        Assert.Equal(
            soulBefore,
            await context.FileSystem.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(
            stateBefore,
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath));
        Assert.Equal(
            historyBefore,
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath));
    }

    [Fact]
    public async Task BlessingBootstrap_NewIncarnationExpiresPriorBalanceAndRebindsAllocation()
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;

        var replacement = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
            context.FileSystem,
            NextIncarnationRelicRerollPackage(),
            currentIncarnation: 4);

        Assert.True(replacement.Success, replacement.ErrorMessage);
        Assert.True(replacement.StateChanged);
        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "blessing_rerolls");
        var state = ResourceStateContract.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.StatePath))!,
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        Assert.True(state.Ledger!.TryResolveExact(coordinate, out var entry));
        Assert.Equal(1m, entry!.Current);
        Assert.Equal(1m, entry.Maximum);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.HistoryPath))!,
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        Assert.Contains(history.History!.Transitions, transition =>
            transition.Coordinate == coordinate &&
            transition.Operation == ResourceTransitionOperation.Spend &&
            transition.OriginId == "shining_blessing_expire_50_4");
        Assert.Contains(history.History.Transitions, transition =>
            transition.Coordinate == coordinate &&
            transition.Operation == ResourceTransitionOperation.Gain &&
            transition.OriginId == "shining_blessing_relic_50_4" &&
            transition.AppliedAmount == 1m);

        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var blessing = Assert.IsType<JsonObject>(
            soul[ShiningBlessingEffectState.SoulStateProperty]);
        Assert.Equal(4, blessing["currentIncarnation"]!.GetValue<int>());
        Assert.Null(blessing["memorySelection"]);
        var relic = Assert.IsType<JsonObject>(blessing["relicRefinementEntitlements"]);
        Assert.Equal(
            "shining_blessing_relic_50_4",
            relic["rerollResourceBinding"]!["allocationId"]!.GetValue<string>());
    }

    [Fact]
    public async Task BlessingBootstrap_NewPackageWithoutRerollsExpiresPriorAllocation()
    {
        var fixture = await CreateBlessingRerollContextAsync();
        await using var context = fixture.Context;

        var replacement = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
            context.FileSystem,
            NextIncarnationResourceOnlyPackage(),
            currentIncarnation: 4);

        Assert.True(replacement.Success, replacement.ErrorMessage);
        Assert.True(replacement.StateChanged);
        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "blessing_rerolls");
        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        Assert.True(state.Ledger!.TryResolveExact(coordinate, out var entry));
        Assert.Equal(0m, entry!.Current);
        Assert.Equal(3m, entry.Maximum);

        var history = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            fixture.Definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        Assert.Contains(
            history.History!.Transitions,
            transition => transition.Coordinate == coordinate &&
                          transition.Operation == ResourceTransitionOperation.Spend &&
                          transition.OriginId == "shining_blessing_expire_50_4" &&
                          transition.AppliedAmount == 3m &&
                          transition.AfterState!.Current == 0m);

        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var blessing = Assert.IsType<JsonObject>(
            soul[ShiningBlessingEffectState.SoulStateProperty]);
        Assert.Equal(4, blessing["currentIncarnation"]!.GetValue<int>());
        Assert.Null(blessing["memorySelection"]);
        Assert.Null(blessing["relicRefinementEntitlements"]);
    }

    private static async Task<ResourceMaterializationTestContext>
        CreateActionPointContextAsync(
            decimal playerCurrent,
            decimal oppositionCurrent)
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        var profiles = Profiles(PlayerSoulProfile());
        var conflict = ActiveConflict("conflict_resource_cost");
        var soulState = SoulState(spiritFocusTier: 0);
        var (state, history) = BuildActionPointState(
            definitions,
            profiles,
            conflict,
            soulState,
            playerCurrent,
            oppositionCurrent);
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            definitions.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            state.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            history.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            profiles.ToJsonString());
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject { ["activeConflict"] = conflict.DeepClone() }.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            soulState.ToJsonString());
        await WriteComposedAuthorityAsync(context, definitions, state, history);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42,
            currentRealm: "Chaos Sea");
        return context;
    }

    private static async Task WriteComposedAuthorityAsync(
        ResourceMaterializationTestContext context,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history)
    {
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            context.FileSystem.ReadFileAsync,
            state,
            history,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
        Assert.NotNull(authority.CanonicalAuthorityJson);
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            authority.CanonicalAuthorityJson!);
    }

    private static async Task SeedLocalAfterlifeOwnerStateAsync(
        ResourceMaterializationTestContext context,
        JsonObject profiles,
        JsonObject soul,
        JsonObject shining,
        bool bootstrapAuthority = true)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var preTurnProfiles = bootstrapAuthority
            ? AfterlifeEntityProfileState.CreateDefaultRoot()
            : profiles;
        var preTurnSoul = bootstrapAuthority ? new JsonObject() : soul;
        var preTurnShining = bootstrapAuthority
            ? ShiningAbodeState.CreateDefaultState()
            : shining;
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            preTurnProfiles.ToJsonString());
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            preTurnSoul.ToJsonString());
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            preTurnShining.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            new JsonObject().ToJsonString());
        if (bootstrapAuthority)
        {
            await context.WriteExactJsonAsync(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}");
            var acceptedProfiles = profiles.DeepClone().AsObject();
            if (acceptedProfiles[AfterlifeEntityProfileState.ProfilesProperty]
                is JsonArray acceptedProfileNodes)
            {
                foreach (var playerProfile in acceptedProfileNodes
                             .OfType<JsonObject>()
                             .Where(static profile =>
                                 string.Equals(
                                     profile["actorType"]?.GetValue<string>(),
                                     "player_soul",
                                     StringComparison.Ordinal) &&
                                 string.Equals(
                                     profile["actorId"]?.GetValue<string>(),
                                     "player_soul",
                                     StringComparison.Ordinal)))
                {
                    playerProfile.Remove(
                        AfterlifeEntityProfileState.ResourceOwnerBindingsProperty);
                }
            }
            var plan = await AfterlifeOwnerResourceStateService.BuildAsync(
                context.FileSystem,
                new AfterlifeOwnerResourceAcceptedState(
                    Profiles: acceptedProfiles,
                    SoulState: soul,
                    ShiningAbode: shining),
                turn: 1);
            Assert.True(plan.IsValid, string.Join(Environment.NewLine, plan.Issues));
            Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
                context.FileSystem,
                plan));
        }
    }

    private static JsonObject BlessingRerollPackage()
    {
        return new JsonObject
        {
            ["preparedAtTurn"] = 42,
            ["selectedCardIds"] = new JsonArray("card_memory", "card_relic"),
            ["selectedCards"] = new JsonArray
            {
                BlessingCard(
                    "card_memory",
                    "memory",
                    new JsonObject
                    {
                        ["type"] = "expand_memory_selection",
                        ["options"] = 1,
                        [ShiningBlessingRerollAllocationContract.PropertyName] =
                            ShiningBlessingRerollAllocationContract.Create(1)
                    }),
                BlessingCard(
                    "card_relic",
                    "relic",
                    new JsonObject
                    {
                        ["type"] = "grant_relic_refinement",
                        [ShiningBlessingRerollAllocationContract.PropertyName] =
                            ShiningBlessingRerollAllocationContract.Create(2),
                        ["freeShape"] = true,
                        ["freeRetune"] = false
                    })
            }
        };
    }

    private static JsonObject NextIncarnationRelicRerollPackage() =>
        new()
        {
            ["preparedAtTurn"] = 50,
            ["selectedCardIds"] = new JsonArray("card_relic_next"),
            ["selectedCards"] = new JsonArray
            {
                BlessingCard(
                    "card_relic_next",
                    "relic",
                    new JsonObject
                    {
                        ["type"] = "grant_relic_refinement",
                        [ShiningBlessingRerollAllocationContract.PropertyName] =
                            ShiningBlessingRerollAllocationContract.Create(1),
                        ["freeShape"] = false,
                        ["freeRetune"] = false
                    })
            }
        };

    private static JsonObject NextIncarnationResourceOnlyPackage() =>
        new()
        {
            ["preparedAtTurn"] = 50,
            ["selectedCardIds"] = new JsonArray("card_resource_next"),
            ["selectedCards"] = new JsonArray
            {
                BlessingCard(
                    "card_resource_next",
                    "resource",
                    new JsonObject
                    {
                        ["type"] = "grant_starting_resources",
                        ["money"] = 1,
                        ["common"] = 0,
                        ["uncommon"] = 0
                    })
            }
        };

    private static async Task<(
        ResourceMaterializationTestContext Context,
        ResourceDefinitionCatalog Definitions)> CreateBlessingRerollContextAsync()
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            definitions.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        var profiles = Profiles(new JsonObject
            {
                ["actorType"] = "player_soul",
                ["actorId"] = "player_soul",
                ["displayName"] = "Душа игрока",
                ["realm"] = "Shining Abode",
                ["resourceOwnerBindings"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["realm"] = "shining_abode",
                        ["resourceOwnerId"] = "player_soul",
                        ["state"] = "suspended"
                    }
                }
            });
        var soul = BlessingSoulState();
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            profiles.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            soul.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/core/player_status.json",
            new JsonObject { ["money"] = 0 }.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equipment"] = new JsonObject(),
                ["resources"] = new JsonObject()
            }.ToJsonString());
        await SeedLocalAfterlifeOwnerStateAsync(
            context,
            profiles,
            soul,
            ShiningAbodeState.CreateDefaultState());
        var result = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
            context.FileSystem,
            BlessingRerollPackage(),
            currentIncarnation: 3);
        Assert.True(result.Success, result.ErrorMessage);
        return (context, definitions);
    }

    private static JsonObject BlessingCard(
        string cardId,
        string family,
        JsonObject payload)
    {
        return new JsonObject
        {
            ["cardId"] = cardId,
            ["dedupeKey"] = $"{family}:{cardId}",
            ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
            ["sourceFactionId"] = "faction_dawn",
            ["displayName"] = cardId,
            ["displaySummary"] = family,
            ["sourceActorId"] = "guardian_dawn",
            ["effectFamily"] = family,
            ["rarity"] = ShiningAbodeState.RarityCommon,
            ["effectPayload"] = payload
        };
    }

    private static async Task<ResourceMaterializationTestContext>
        CreateShiningGachaContextAsync(int radianceTier, int turn)
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        var profiles = Profiles(new JsonObject
        {
            ["actorType"] = "player_soul",
            ["actorId"] = "player_soul",
            ["displayName"] = "Душа игрока",
            ["realm"] = "Shining Abode",
            ["resourceOwnerBindings"] = new JsonArray
            {
                new JsonObject
                {
                    ["realm"] = "shining_abode",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "active"
                }
            }
        });
        var soul = new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = turn,
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] =
                new JsonObject
                {
                    [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0
                }
        };
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        Assert.IsType<JsonObject>(shining["radiance"])["tier"] = radianceTier;
        await SeedLocalAfterlifeOwnerStateAsync(context, profiles, soul, shining);
        await context.WriteExactJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(
            turn,
            currentRealm: "Shining Abode");
        return context;
    }

    private static async Task<ResourceMaterializationTestContext>
        CreateInitializedGuardianGachaContextAsync(
            int initialTurn,
            string returnCycleId)
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedGuardianGachaPreTurnAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            initialTurn,
            currentRealm: "Chaos Sea");
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            GuardianReturnState(
                "guardian_vesna",
                returnCycleId,
                reputation: 50,
                abodePower: 0).ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        await PublishAsync(context);
        return context;
    }

    private static async Task SeedGuardianGachaPreTurnAsync(
        ResourceMaterializationTestContext context)
    {
        var bootstrapProfiles = Profiles(PlayerSoulProfile());
        Assert.IsType<JsonObject>(Assert.Single(
                Assert.IsType<JsonArray>(bootstrapProfiles[
                    AfterlifeEntityProfileState.ProfilesProperty])))
            .Remove(AfterlifeEntityProfileState.ResourceOwnerBindingsProperty);
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(
            context.FileSystem,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: bootstrapProfiles,
                SoulState: SoulState(spiritFocusTier: 0)));
        await context.WriteExactJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());

        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));
        Assert.IsType<JsonArray>(profiles[
                AfterlifeEntityProfileState.ProfilesProperty])
            .Add(GuardianProfile("guardian_vesna"));
        await context.WriteExactJsonAsync(
            AfterlifeEntityProfileState.StatePath,
            profiles.ToJsonString());
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
        await context.WriteExactJsonAsync(
            ShiningAbodeState.StatePath,
            ShiningAbodeState.CreateDefaultState().ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/guardians.json",
            GuardianReturnState(
                "guardian_vesna",
                returnCycleId: null,
                reputation: 50,
                abodePower: 0).ToJsonString());
    }

    private static JsonObject ShiningGachaHistoryEntry(
        string requestId,
        string returnCycleId,
        int turn) =>
        new()
        {
            ["requestId"] = requestId,
            ["factionId"] = "faction_shining_test",
            ["factionName"] = "Test faction",
            ["returnCycleId"] = returnCycleId,
            ["costInFeathers"] = 5,
            ["baseRarity"] = "Common",
            ["finalRarity"] = "Common",
            ["relicId"] = "relic_" + requestId,
            ["relicName"] = "Test relic",
            ["turnNumber"] = turn,
            ["timestamp"] = "2026-08-16T00:00:00.0000000Z"
        };

    private static JsonObject GuardianGachaCommand(
        string relicId,
        int inkFeathersSpent) =>
        new()
        {
            ["command"] = "processGacha",
            ["guardianId"] = "guardian_vesna",
            ["inkFeathersSpent"] = inkFeathersSpent,
            ["gachaBonusAudit"] = new JsonObject
            {
                ["baseRarity"] = "Common",
                ["abodePowerBonusSteps"] = 0,
                ["guardianBonusSteps"] = 0,
                ["finalRarity"] = "Common"
            },
            ["result"] = new JsonObject
            {
                ["relicId"] = relicId,
                ["name"] = "Test guardian relic",
                ["rarity"] = "Common"
            }
        };

    private static async Task WriteExchangeAsync(
        ResourceMaterializationTestContext context,
        JsonObject exchange) =>
        await WriteExchangesAsync(context, exchange);

    private static async Task WriteExchangesAsync(
        ResourceMaterializationTestContext context,
        params JsonObject[] exchanges)
    {
        var conflict = ActiveConflict("conflict_resource_cost");
        var raw = new JsonObject { ["activeConflict"] = conflict.DeepClone() };
        foreach (var exchange in exchanges)
        {
            raw = AfterlifeSpiritualConflictState.ApplyUpdate(
                raw,
                new JsonObject
                {
                    ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
                    ["exchange"] = exchange.DeepClone()
                });
        }
        var log = Assert.IsType<JsonArray>(
            Assert.IsType<JsonObject>(raw["activeConflict"])["exchangeLog"]);
        var activeConflictAfter = Assert.IsType<JsonObject>(raw["activeConflict"])
            .DeepClone()
            .AsObject();
        activeConflictAfter.Remove("combatConditions");
        raw[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
            ["exchange"] = log[0]!.DeepClone(),
            ["activeConflictAfter"] = activeConflictAfter
        };
        raw["activeConflict"] = ActiveConflict("conflict_resource_cost");
        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            raw.ToJsonString());
    }

    private static async Task WriteProjectedExchangesAsync(
        ResourceMaterializationTestContext context,
        params JsonObject[] exchanges)
    {
        var root = new JsonObject
        {
            ["activeConflict"] = ActiveConflict("conflict_resource_cost")
        };
        foreach (var exchange in exchanges)
        {
            root = AfterlifeSpiritualConflictState.ApplyUpdate(
                root,
                new JsonObject
                {
                    ["mode"] = AfterlifeSpiritualConflictState.ModeExchange,
                    ["exchange"] = exchange.DeepClone()
                });
        }

        await context.WriteExactJsonAsync(
            AfterlifeSpiritualConflictState.StatePath,
            root.ToJsonString());
    }

    private static JsonObject Exchange(
        string exchangeId,
        JsonObject playerAudit,
        JsonObject? oppositionAudit = null)
    {
        var audits = new JsonObject { ["player"] = playerAudit };
        if (oppositionAudit != null)
            audits["opposition"] = oppositionAudit;
        return new JsonObject
        {
            ["exchangeId"] = exchangeId,
            ["operationType"] = playerAudit["operationType"]!.DeepClone(),
            ["outcome"] = "success",
            ["actionCostAudit"] = audits
        };
    }

    private static JsonObject PlayerAudit(
        string operationType,
        decimal effectiveCost,
        decimal before,
        decimal after) =>
        CostAudit(operationType, effectiveCost, before, after);

    private static JsonObject OppositionAudit(
        string operationType,
        decimal effectiveCost,
        decimal before,
        decimal after) =>
        CostAudit(operationType, effectiveCost, before, after);

    private static JsonObject CostAudit(
        string operationType,
        decimal effectiveCost,
        decimal before,
        decimal after) =>
        new()
        {
            ["operationType"] = operationType,
            ["baseCost"] = effectiveCost,
            ["minCost"] = effectiveCost == 0m ? 0m : 1m,
            ["artTier"] = 0,
            ["effectiveCost"] = effectiveCost,
            ["before"] = before,
            ["after"] = after,
            ["max"] = 6m
        };

    private static async Task<AcceptedMechanicsPlan> PeekPlanAsync(
        ResourceMaterializationTestContext context)
    {
        var handoff = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem);
        Assert.NotNull(handoff);
        return Assert.IsType<AcceptedMechanicsPlan>(handoff!.Result.Plan);
    }

    private static ResourceStateEntry ResolvePlannedActionPoints(
        AcceptedMechanicsPlan plan,
        ResourceOwnerKind ownerKind) =>
        ResolvePlannedResource(plan, ownerKind, "spiritual_action_points");

    private static ResourceStateEntry ResolvePlannedResource(
        AcceptedMechanicsPlan plan,
        ResourceOwnerKind ownerKind,
        string resourceKey)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var parsed = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions),
            allowMissingPristine: false);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return Assert.Single(
            parsed.Ledger!.Entries,
            entry => entry.Coordinate.OwnerKind == ownerKind &&
                     entry.Coordinate.ResourceKey == resourceKey);
    }

    private static async Task PublishAsync(
        ResourceMaterializationTestContext context)
    {
        await using var writeLease = await context.FileSystem
            .AcquireCanonicalWriteLeaseAsync();
        await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);
    }

    private static async Task PublishWithFullNormalizerAsync(
        ResourceMaterializationTestContext context)
    {
        const string guardiansPath = "game_state/meta/guardians.json";
        var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [guardiansPath] =
                "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json"
        };
        await using var writeLease = await context.FileSystem
            .AcquireCanonicalWriteLeaseAsync();
        await context.Normalizer.BindTo(writeLease)
            .NormalizeAccumulatedStateWithPlanAsync(backups);
    }

    private static async Task PublishShiningWithFullNormalizerAsync(
        ResourceMaterializationTestContext context)
    {
        var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningAbodeState.StatePath] =
                "game_state/control/pending_turn_snapshot/" +
                ShiningAbodeState.StatePath
        };
        await using var writeLease = await context.FileSystem
            .AcquireCanonicalWriteLeaseAsync();
        await context.Normalizer.BindTo(writeLease)
            .NormalizeAccumulatedStateWithPlanAsync(backups);
    }

    private static async Task SpendOneGuardianAttemptFixtureAsync(
        ResourceMaterializationTestContext context,
        ResourceDefinitionCatalog definitions,
        int turn)
    {
        var stateResult = ResourceStateContract.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.StatePath))!,
            definitions,
            allowMissingPristine: false);
        var historyResult = ResourceHistoryState.ParseCanonical(
            (await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.HistoryPath))!,
            definitions,
            allowMissingPristine: false);
        Assert.True(stateResult.IsValid, string.Join(Environment.NewLine, stateResult.Issues));
        Assert.True(historyResult.IsValid, string.Join(Environment.NewLine, historyResult.Issues));
        var ledger = Assert.IsType<ResourceStateLedger>(stateResult.Ledger);
        var history = Assert.IsType<ResourceHistoryState>(historyResult.History);
        var before = Assert.Single(
            ledger.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Coordinate.ResourceOwnerId == "guardian_vesna" &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");
        Assert.Equal(2m, before.Current);
        var prior = Assert.Single(
            history.Transitions,
            transition => ResourceCoordinateComparer.Instance.Equals(
                transition.Coordinate,
                before.Coordinate));
        var sequence = history.Transitions
            .Where(transition => transition.Turn == turn)
            .Select(static transition => transition.ExecutionSequence)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        var eventRef = $"turn_{turn}:guardian_fixture_spend";
        var sourceId = $"guardian_fixture_spend_{turn}";
        var transition = new ResourceTransition(
            $"resource_transition_guardian_fixture_spend_{turn}",
            $"resource_operation_guardian_fixture_spend_{turn}",
            eventRef,
            "registered_system_outcome",
            sourceId,
            ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 60,
            sequence,
            before.Coordinate,
            ResourceTransitionOperation.Spend,
            RequestedAmount: 1m,
            AppliedAmount: 1m,
            ResourceTransitionOutcome.Applied,
            CapacityDisposition: null,
            before.Snapshot,
            before.Snapshot with { Current = 1m },
            new ResourceSourceEvidence(
                "registered_system_outcome",
                sourceId,
                prior.PolicyFingerprint),
            prior.PolicyFingerprint,
            ReceiptId: null,
            turn);
        var updatedHistoryResult = ResourceHistoryState.CreateValidated(
            history.Transitions.Append(transition),
            definitions);
        Assert.True(
            updatedHistoryResult.IsValid,
            string.Join(Environment.NewLine, updatedHistoryResult.Issues));
        var updatedHistory = Assert.IsType<ResourceHistoryState>(updatedHistoryResult.History);
        var updatedState = new ResourceStateLedger(ledger.Entries.Select(entry =>
            ResourceCoordinateComparer.Instance.Equals(entry.Coordinate, before.Coordinate)
                ? entry with
                {
                    Current = 1m,
                    Chronology = entry.Chronology with
                    {
                        LastTransitionId = transition.TransitionId,
                        LastEventRef = transition.EventRef,
                        LastTransitionTurn = transition.Turn
                    }
                }
                : entry));
        Assert.Empty(updatedHistory.ValidateStateAgreement(updatedState));
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.StatePath,
            updatedState.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            updatedHistory.ToCanonicalJson());
    }

    private static (ResourceStateLedger State, ResourceHistoryState History)
        BuildActionPointState(
            ResourceDefinitionCatalog definitions,
            JsonObject profiles,
            JsonObject conflict,
            JsonObject soulState,
            decimal playerCurrent,
            decimal oppositionCurrent)
    {
        var conflictRoot = new JsonObject { ["activeConflict"] = conflict.DeepClone() };
        var owners = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                new AfterlifeResourceOwnerRoots(profiles, conflictRoot, soulState),
                new AfterlifeResourceOwnerRoots(profiles, conflictRoot, soulState)));
        Assert.True(owners.IsValid, string.Join(Environment.NewLine, owners.Issues));
        var player = Assert.Single(
            owners.Authority!.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Key.ResourceOwnerId == "player_soul");
        var opposition = Assert.Single(
            owners.Authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);

        var playerResolved = ResolveActionPoints(
            definitions,
            player,
            new SpiritFocusActionPointsFormulaInput(
                FormulaOwner(player.Key),
                player.AuthorityFingerprint,
                SpiritFocusTier: 0));
        var oppositionResolved = ResolveActionPoints(
            definitions,
            opposition,
            new ConflictSideActionPointsFormulaInput(
                FormulaOwner(opposition.Key),
                opposition.AuthorityFingerprint,
                "conflict_resource_cost",
                AcceptedMaximum: 6m));

        var transitions = new List<ResourceTransition>
        {
            InitializeTransition(
                player.Key,
                playerResolved,
                playerResolved.Maximum,
                sequence: 0),
            InitializeTransition(
                opposition.Key,
                oppositionResolved,
                oppositionResolved.Maximum,
                sequence: 1)
        };
        if (playerCurrent != playerResolved.Maximum)
        {
            transitions.Add(SpendFixtureTransition(
                transitions[0],
                playerCurrent,
                sequence: transitions.Count));
        }
        if (oppositionCurrent != oppositionResolved.Maximum)
        {
            transitions.Add(SpendFixtureTransition(
                transitions[1],
                oppositionCurrent,
                sequence: transitions.Count));
        }
        var historyResult = ResourceHistoryState.CreateValidated(transitions, definitions);
        Assert.True(historyResult.IsValid, string.Join(Environment.NewLine, historyResult.Issues));
        var history = Assert.IsType<ResourceHistoryState>(historyResult.History);
        var state = new ResourceStateLedger(transitions
            .GroupBy(static transition => transition.Coordinate, ResourceCoordinateComparer.Instance)
            .Select(static group =>
            {
                var ordered = group.OrderBy(static transition => transition.ExecutionSequence)
                    .ToArray();
                var first = ordered[0];
                var last = ordered[^1];
                return new ResourceStateEntry(
                    last.Coordinate,
                    last.AfterState!.Current,
                    last.AfterState.Maximum,
                    last.AfterState.CapacityBinding,
                    last.AfterState.State,
                    new ResourceChronology(
                        first.Turn,
                        first.EventRef,
                        last.TransitionId,
                        last.EventRef,
                        last.Turn));
            }));
        Assert.Empty(history.ValidateStateAgreement(state));
        return (state, history);
    }

    private static ResolvedResourceCapacity ResolveActionPoints(
        ResourceDefinitionCatalog definitions,
        ResourceOwnerAuthorityEntry owner,
        ResourceFormulaInput formulaInput)
    {
        Assert.True(definitions.TryResolveExact("spiritual_action_points", out var definition));
        var coordinate = new ResourceCoordinate(
            owner.Key.Realm,
            owner.Key.OwnerKind,
            owner.Key.ResourceOwnerId,
            "spiritual_action_points");
        var result = ResolvedResourceCapacity.Resolve(
            definition!,
            coordinate,
            new RegisteredFormulaCapacityInput(formulaInput),
            instanceAuthorityKey: null,
            includeInitialization: true);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return Assert.IsType<ResolvedResourceCapacity>(result.Capacity);
    }

    private static ResourceTransition InitializeTransition(
        ResourceOwnerKey owner,
        ResolvedResourceCapacity capacity,
        decimal current,
        int sequence)
    {
        var coordinate = new ResourceCoordinate(
            owner.Realm,
            owner.OwnerKind,
            owner.ResourceOwnerId,
            "spiritual_action_points");
        var eventRef = $"turn_1:afterlife_fixture:{sequence + 1}";
        var sourceId = $"afterlife_fixture_{sequence + 1}";
        return new ResourceTransition(
            $"resource_transition_afterlife_fixture_{sequence + 1}",
            $"resource_operation_afterlife_fixture_{sequence + 1}",
            eventRef,
            "owner_materialization",
            sourceId,
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            sequence,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0m,
            0m,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            BeforeState: null,
            new ResourceStateSnapshot(
                current,
                capacity.Maximum,
                capacity.Binding,
                ResourceLifecycleState.Active),
            new ResourceSourceEvidence(
                "owner_materialization",
                sourceId,
                capacity.Initialization!.AuthorityFingerprint),
            capacity.Initialization.AuthorityFingerprint,
            ReceiptId: null,
            Turn: 1);
    }

    private static ResourceTransition SpendFixtureTransition(
        ResourceTransition initialization,
        decimal after,
        int sequence)
    {
        var before = initialization.AfterState!;
        Assert.InRange(after, 0m, before.Maximum);
        var amount = before.Current - after;
        Assert.True(amount > 0m);
        var eventRef = $"turn_1:afterlife_fixture:spend_{sequence + 1}";
        var sourceId = $"afterlife_fixture_spend_{sequence + 1}";
        return new ResourceTransition(
            $"resource_transition_afterlife_fixture_spend_{sequence + 1}",
            $"resource_operation_afterlife_fixture_spend_{sequence + 1}",
            eventRef,
            "action_cost",
            sourceId,
            ResourceMutationPhase.DirectCost,
            100,
            sequence,
            initialization.Coordinate,
            ResourceTransitionOperation.Spend,
            amount,
            amount,
            ResourceTransitionOutcome.Applied,
            CapacityDisposition: null,
            BeforeState: before,
            AfterState: before with { Current = after },
            new ResourceSourceEvidence(
                "action_cost",
                sourceId,
                initialization.PolicyFingerprint),
            initialization.PolicyFingerprint,
            ReceiptId: null,
            Turn: 1);
    }

    private static ResourceFormulaOwner FormulaOwner(ResourceOwnerKey owner) =>
        new(owner.Realm, owner.OwnerKind, owner.ResourceOwnerId);

    private static JsonObject Profiles(params JsonObject[] profiles) =>
        new()
        {
            [AfterlifeEntityProfileState.ProfilesProperty] =
                new JsonArray(profiles.Select(static profile => (JsonNode)profile).ToArray())
        };

    private static JsonObject PlayerSoulProfile() =>
        new()
        {
            ["actorType"] = "player_soul",
            ["actorId"] = "player_soul",
            ["displayName"] = "Душа игрока",
            ["realm"] = "Chaos Sea",
            ["resourceOwnerBindings"] = new JsonArray
            {
                new JsonObject
                {
                    ["realm"] = "chaos_sea",
                    ["resourceOwnerId"] = "player_soul",
                    ["state"] = "active"
                }
            }
        };

    private static JsonObject GuardianProfile(string guardianId) =>
        new()
        {
            ["actorType"] = "guardian",
            ["actorId"] = guardianId,
            ["displayName"] = guardianId,
            ["realm"] = "Chaos Sea",
            ["resourceOwnerBindings"] = new JsonArray
            {
                new JsonObject
                {
                    ["realm"] = "chaos_sea",
                    ["resourceOwnerId"] = guardianId,
                    ["state"] = "active"
                }
            }
        };

    private static JsonObject GuardianReturnState(
        string guardianId,
        string? returnCycleId,
        int reputation,
        int abodePower)
    {
        var guardian = new JsonObject
        {
            ["guardianId"] = guardianId,
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = reputation
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = abodePower
            }
        };
        if (returnCycleId != null)
        {
            guardian["gachaSystem"] = new JsonObject
            {
                ["currentReturnCycleId"] = returnCycleId,
                ["gachaHistory"] = new JsonArray()
            };
        }
        return new JsonObject
        {
            ["guardians"] = new JsonArray(guardian),
            ["activeGuardian"] = guardian.DeepClone()
        };
    }

    private static JsonObject SoulState(int spiritFocusTier) =>
        new()
        {
            ["currentRealm"] = "Chaos Sea",
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
            {
                [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = spiritFocusTier
            }
        };

    private static JsonObject BlessingSoulState() =>
        new()
        {
            ["soulName"] = "Soul",
            ["currentRealm"] = "Mortal World",
            ["currentIncarnation"] = 3,
            ["inkFeathers"] = new JsonObject { ["current"] = 0, ["total"] = 0 },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray()
            },
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
            {
                [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0
            }
        };

    private static JsonObject ActiveConflict(string conflictId) =>
        new()
        {
            ["conflictId"] = conflictId,
            ["realm"] = "Chaos Sea",
            ["status"] = "active",
            ["resolutionState"] = "active",
            ["playerSide"] = new JsonObject(),
            ["oppositionSide"] = new JsonObject(),
            ["exchangeLog"] = new JsonArray(),
            ["combatConditions"] = new JsonArray(),
            ["resourceOwnerBindings"] = new JsonObject
            {
                ["opposition"] = new JsonObject
                {
                    ["resourceOwnerId"] = "afterlife_conflict_side_cost"
                }
            }
        };

    private static JsonObject CreatePreviewConditionEffect()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "afterlife_combat_condition");
        effect["realm"] = "chaos_sea";
        effect["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = "conflict_resource_cost:opposition"
        };
        effect["display"]!["name"] = "Скрытая цена печати";
        effect["display"]!["description"] =
            "Цена усложняет следующее действие стороны.";
        effect["display"]!["category"] = "condition";
        effect["display"]!["visibility"] = "gm_only";
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = "art_preview_condition",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"] = new JsonArray(new JsonObject
        {
            ["componentId"] = "component_001",
            ["profile"] = "afterlife_combat_condition",
            ["priority"] = 100,
            ["payload"] = new JsonObject
            {
                ["conditionKind"] = "burden",
                ["targetSide"] = "opposition",
                ["actorId"] = "guardian_preview_condition",
                ["operations"] = new JsonArray("pressure"),
                ["axes"] = new JsonArray("actionCostAudit.opposition"),
                ["counterplay"] = new JsonArray(
                    "Ответить действием guard или counter."),
                ["payoff"] = "increase_action_cost"
            }
        });
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 2,
            ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed"),
            ["displayText"] = "Ещё два обмена"
        };
        effect["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "condition_exchange_consumed",
            ["eventType"] = "afterlife_exchange_end",
            ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"),
            ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });
        Assert.True(
            AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                effect,
                out var projected,
                out var reason),
            reason);
        foreach (var field in AfterlifeSpiritualConflictState
                     .CombatConditionProjectionFields)
        {
            effect[field] = projected[field]?.DeepClone();
        }
        return effect;
    }
}
