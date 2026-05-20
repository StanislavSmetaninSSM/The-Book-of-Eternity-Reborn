using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeSpiritualCombatScenarioHarnessTests
{
    [Fact]
    public void GeneratedScenarioHarness_IncludesRequiredAfterlifeCombatFamilies()
    {
        var scenarios = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios();

        Assert.Contains(scenarios, scenario => scenario.SideModel == "direct_duel");
        Assert.Contains(scenarios, scenario => scenario.SideModel == "assisted_duel");
        Assert.Contains(scenarios, scenario => scenario.SideModel == "champion_duel");
        Assert.Contains(scenarios, scenario => scenario.Tags.Contains("control-heavy"));
        Assert.Contains(scenarios, scenario => scenario.OperationType == "incarnation_resistance");

        foreach (var scenario in scenarios)
        {
            Assert.True(scenario.PlayerArtTier >= 0);
            Assert.True(scenario.OppositionArtTier >= 0);
            Assert.True(scenario.PlayerActionPointsBudget > 0);
            Assert.True(scenario.OppositionActionPointsBudget > 0);
            Assert.Contains(scenario.StartingPosition, AfterlifeSpiritualConflictState.ConflictPositions);
            Assert.Contains(scenario.Difficulty, AfterlifeSpiritualConflictState.DifficultyDefinitions.Keys);
            Assert.NotNull(scenario.Reward);
        }
    }

    [Fact]
    public void GeneratedScenarioHarness_RunsDeterministicSeededDistribution()
    {
        var scenarios = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios();
        var results = scenarios
            .Select(scenario => AfterlifeSpiritualCombatScenarioHarness.RunSeededDistribution(scenario, seed: 531, rollCount: 200))
            .ToList();

        Assert.Equal(scenarios.Count, results.Count);
        foreach (var result in results)
        {
            Assert.Equal(200, result.RollCount);
            Assert.True(result.OutcomeBandCounts.Count >= 3);
            Assert.InRange(result.PlayerSuccessRate, 0.10, 0.90);
            Assert.InRange(result.OppositionSuccessRate, 0.10, 0.90);
            Assert.True(result.CriticalRollCount > 0);
            Assert.InRange(result.RewardPreviewFinalAmount, 0, result.RewardPreviewCap);
        }
    }

    [Fact]
    public void GeneratedScenarioHarness_NonCriticalDistribution_ProgressionPositionAndFocusOutweighDice()
    {
        var baseScenario = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios()
            .Single(scenario => scenario.Name == "direct_duel_even_pressure");
        var outmatched = baseScenario with
        {
            Name = "outmatched_noncritical_pressure",
            PlayerArtTier = 0,
            OppositionArtTier = 4,
            PlayerActionPointsBudget = 1,
            PlayerActionCost = 3,
            StartingPosition = "opposition_advantaged",
            Difficulty = "hard"
        };
        var prepared = baseScenario with
        {
            Name = "prepared_noncritical_pressure",
            PlayerArtTier = 4,
            OppositionArtTier = 1,
            PlayerActionPointsBudget = 8,
            PlayerActionCost = 1,
            StartingPosition = "player_advantaged",
            Difficulty = "normal"
        };

        var weakDistribution = AfterlifeSpiritualCombatScenarioHarness.RunSeededDistribution(outmatched, seed: 514, rollCount: 2_000);
        var strongDistribution = AfterlifeSpiritualCombatScenarioHarness.RunSeededDistribution(prepared, seed: 514, rollCount: 2_000);

        Assert.True(weakDistribution.NonCriticalRollCount > 1_200);
        Assert.Equal(weakDistribution.NonCriticalRollCount, strongDistribution.NonCriticalRollCount);
        Assert.True(strongDistribution.NonCriticalAverageMargin > weakDistribution.NonCriticalAverageMargin + 15);
        Assert.True(strongDistribution.NonCriticalPlayerSuccessRate > weakDistribution.NonCriticalPlayerSuccessRate + 0.55);
        Assert.True(weakDistribution.NonCriticalOppositionSuccessRate > 0.60);
    }

    [Fact]
    public void GeneratedScenarioHarness_NonCriticalDistribution_TacticalAntiControlBeatsIgnoringControl()
    {
        var antiControl = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios()
            .Single(scenario => scenario.Name == "control_heavy_break_binding");
        var ignoresControl = antiControl with
        {
            Name = "control_heavy_pressure_spam",
            OperationType = "pressure"
        };

        var ignored = AfterlifeSpiritualCombatScenarioHarness.RunSeededDistribution(ignoresControl, seed: 516, rollCount: 2_000);
        var answered = AfterlifeSpiritualCombatScenarioHarness.RunSeededDistribution(antiControl, seed: 516, rollCount: 2_000);

        Assert.Equal(ignored.NonCriticalRollCount, answered.NonCriticalRollCount);
        Assert.True(answered.NonCriticalAverageMargin > ignored.NonCriticalAverageMargin + 1.5);
        Assert.True(answered.NonCriticalPlayerSuccessRate > ignored.NonCriticalPlayerSuccessRate + 0.06);
    }

    [Fact]
    public void GeneratedScenarioHarness_CriticalsRemainBoundedByRelativePowerAndSituation()
    {
        var baseScenario = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios()
            .Single(scenario => scenario.Name == "direct_duel_even_pressure");
        var overwhelmingOpposition = baseScenario with
        {
            Name = "natural_twenty_against_overwhelming_opposition",
            PlayerArtTier = 0,
            OppositionArtTier = 10,
            StartingPosition = "opposition_dominant",
            Difficulty = "impossible"
        };
        var overwhelmingPlayer = baseScenario with
        {
            Name = "natural_one_with_overwhelming_player_advantage",
            PlayerArtTier = 10,
            OppositionArtTier = 0,
            StartingPosition = "player_dominant",
            Difficulty = "normal"
        };

        var playerCritical = AfterlifeSpiritualCombatScenarioHarness.ResolveSingleContest(
            overwhelmingOpposition,
            playerDie: 20,
            oppositionDie: 18);
        var playerFumble = AfterlifeSpiritualCombatScenarioHarness.ResolveSingleContest(
            overwhelmingPlayer,
            playerDie: 1,
            oppositionDie: 2);

        Assert.True(playerCritical.Margin <= -20);
        Assert.Equal("player_success", playerCritical.OutcomeBand);
        Assert.True(playerFumble.Margin >= 20);
        Assert.Equal("opposition_success", playerFumble.OutcomeBand);
    }

    [Fact]
    public void GeneratedScenarioHarness_PlayerNatural20_RaisesMarginLossOnlyToOrdinaryPlayerSuccess()
    {
        var contest = AfterlifeSpiritualCombatScenarioHarness.ResolveSingleContest(
            BuildOverwhelmingOppositionScenario(),
            playerDie: 20,
            oppositionDie: 18);

        Assert.Equal("player_favorable", contest.CriticalOverrideDirection);
        Assert.Equal("decisive_opposition_success", contest.MarginOutcomeBand);
        Assert.Equal("player_success", contest.OutcomeBand);
    }

    [Fact]
    public void GeneratedScenarioHarness_OppositionNatural1_RaisesMarginLossOnlyToOrdinaryPlayerSuccess()
    {
        var contest = AfterlifeSpiritualCombatScenarioHarness.ResolveSingleContest(
            BuildOverwhelmingOppositionScenario(),
            playerDie: 2,
            oppositionDie: 1);

        Assert.Equal("player_favorable", contest.CriticalOverrideDirection);
        Assert.Equal("decisive_opposition_success", contest.MarginOutcomeBand);
        Assert.Equal("player_success", contest.OutcomeBand);
    }

    [Fact]
    public void GeneratedScenarioHarness_PlayerNatural1_LowersMarginWinOnlyToOrdinaryOppositionSuccess()
    {
        var contest = AfterlifeSpiritualCombatScenarioHarness.ResolveSingleContest(
            BuildOverwhelmingPlayerScenario(),
            playerDie: 1,
            oppositionDie: 2);

        Assert.Equal("player_unfavorable", contest.CriticalOverrideDirection);
        Assert.Equal("decisive_player_success", contest.MarginOutcomeBand);
        Assert.Equal("opposition_success", contest.OutcomeBand);
    }

    [Fact]
    public void GeneratedScenarioHarness_OppositionNatural20_LowersMarginWinOnlyToOrdinaryOppositionSuccess()
    {
        var contest = AfterlifeSpiritualCombatScenarioHarness.ResolveSingleContest(
            BuildOverwhelmingPlayerScenario(),
            playerDie: 18,
            oppositionDie: 20);

        Assert.Equal("player_unfavorable", contest.CriticalOverrideDirection);
        Assert.Equal("decisive_player_success", contest.MarginOutcomeBand);
        Assert.Equal("opposition_success", contest.OutcomeBand);
    }

    [Fact]
    public void GeneratedScenarioHarness_OpposedNaturalCriticalsCancelAndUseMarginBand()
    {
        var contest = AfterlifeSpiritualCombatScenarioHarness.ResolveSingleContest(
            BuildOverwhelmingPlayerScenario(),
            playerDie: 20,
            oppositionDie: 20);

        Assert.Equal("cancelled", contest.CriticalOverrideDirection);
        Assert.Equal(contest.MarginOutcomeBand, contest.OutcomeBand);
        Assert.Equal("decisive_player_success", contest.OutcomeBand);
    }

    [Fact]
    public void GeneratedScenarioHarness_ActionCostFormula_ReducesCostByTierButPreservesMinimumCost()
    {
        Assert.Equal(3, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("pressure", artTier: 0));
        Assert.Equal(2, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("pressure", artTier: 1));
        Assert.Equal(1, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("pressure", artTier: 2));
        Assert.Equal(1, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("pressure", artTier: 5));

        Assert.Equal(4, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("counter", artTier: 0));
        Assert.Equal(3, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("counter", artTier: 1));
        Assert.Equal(2, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("counter", artTier: 2));
        Assert.Equal(2, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("counter", artTier: 5));

        Assert.Equal(5, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("force_binding", artTier: 0));
        Assert.Equal(2, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("force_binding", artTier: 3));
        Assert.Equal(2, AfterlifeSpiritualCombatScenarioHarness.ResolveEffectiveActionCost("force_binding", artTier: 5));
    }

    [Fact]
    public void GeneratedScenarioHarness_RepeatedOneActionSpamRunsOutOfActionPoints()
    {
        var novicePlan = AfterlifeSpiritualCombatScenarioHarness.SimulateRepeatedActionPlan(
            operationType: "force_binding",
            artTier: 0,
            spiritFocusTier: 0,
            requestedRepetitions: 3);
        var masteredPlan = AfterlifeSpiritualCombatScenarioHarness.SimulateRepeatedActionPlan(
            operationType: "force_binding",
            artTier: 5,
            spiritFocusTier: 5,
            requestedRepetitions: 10);

        Assert.Equal(6, novicePlan.StartingActionPoints);
        Assert.Equal(5, novicePlan.EffectiveCost);
        Assert.Equal(1, novicePlan.ExecutedRepetitions);
        Assert.True(novicePlan.WasStoppedByScarcity);

        Assert.Equal(15, masteredPlan.StartingActionPoints);
        Assert.Equal(2, masteredPlan.EffectiveCost);
        Assert.Equal(7, masteredPlan.ExecutedRepetitions);
        Assert.True(masteredPlan.WasStoppedByScarcity);
    }

    [Fact]
    public void GeneratedScenarioHarness_SpiritFocusTierIncreasesOptionsButDoesNotRemoveScarcity()
    {
        Assert.Equal(6, AfterlifeSpiritualCombatScenarioHarness.ResolveSpiritFocusMaxActionPoints(spiritFocusTier: 0));
        Assert.Equal(7, AfterlifeSpiritualCombatScenarioHarness.ResolveSpiritFocusMaxActionPoints(spiritFocusTier: 1));
        Assert.Equal(8, AfterlifeSpiritualCombatScenarioHarness.ResolveSpiritFocusMaxActionPoints(spiritFocusTier: 2));
        Assert.Equal(10, AfterlifeSpiritualCombatScenarioHarness.ResolveSpiritFocusMaxActionPoints(spiritFocusTier: 3));
        Assert.Equal(12, AfterlifeSpiritualCombatScenarioHarness.ResolveSpiritFocusMaxActionPoints(spiritFocusTier: 4));
        Assert.Equal(15, AfterlifeSpiritualCombatScenarioHarness.ResolveSpiritFocusMaxActionPoints(spiritFocusTier: 5));

        var tierZeroCounters = AfterlifeSpiritualCombatScenarioHarness.SimulateRepeatedActionPlan(
            operationType: "counter",
            artTier: 0,
            spiritFocusTier: 0,
            requestedRepetitions: 5);
        var tierFiveCounters = AfterlifeSpiritualCombatScenarioHarness.SimulateRepeatedActionPlan(
            operationType: "counter",
            artTier: 5,
            spiritFocusTier: 5,
            requestedRepetitions: 9);

        Assert.True(tierFiveCounters.ExecutedRepetitions > tierZeroCounters.ExecutedRepetitions);
        Assert.True(tierFiveCounters.WasStoppedByScarcity);
    }

    [Fact]
    public void GeneratedScenarioHarness_RecoverSpiritualPowerRewardsSafeTimingAndPunishesPressureManeuverBinding()
    {
        Assert.Equal(3, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("guard", outcome: "success"));
        Assert.Equal(3, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("counter", outcome: "success"));
        Assert.Equal(2, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("guard", outcome: "partial_success"));

        Assert.Equal(1, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("pressure", outcome: "success"));
        Assert.Equal(1, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("maneuver", outcome: "success"));
        Assert.Equal(1, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("binding", outcome: "success"));
        Assert.Equal(1, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("force_binding", outcome: "success"));
        Assert.Equal(1, AfterlifeSpiritualCombatScenarioHarness.ResolveRecoveryDelta("force_incarnation", outcome: "success"));
    }

    [Fact]
    public void GeneratedScenarioHarness_RewardPreview_UsesRealmCurrencyAndCaps()
    {
        var scenarios = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios();

        foreach (var scenario in scenarios)
        {
            var preview = AfterlifeSpiritualCombatScenarioHarness.PreviewReward(scenario, outcomeBand: "player_success");

            Assert.Equal(scenario.Reward.Currency, preview.Currency);
            Assert.InRange(preview.ChallengeTier, 1, AfterlifeSpiritualConflictState.ConflictRewardMaxChallengeTier);
            Assert.InRange(preview.FinalAmount, 0, preview.Cap);
            if (preview.RealmKey == "chaos_sea")
            {
                Assert.Equal(AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers, preview.Currency);
                Assert.Equal(AfterlifeSpiritualConflictState.ChaosSeaConflictRewardMaxAmount, preview.Cap);
            }
            else
            {
                Assert.Equal(AfterlifeSpiritualConflictState.RewardCurrencyLightSparks, preview.Currency);
                Assert.Equal(AfterlifeSpiritualConflictState.ShiningConflictRewardMaxAmount, preview.Cap);
            }
        }
    }

    [Fact]
    public void GeneratedScenarioHarness_RewardPreview_DifficultyScalingIsDeterministicAndBounded()
    {
        var baseScenario = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios()
            .Single(scenario => scenario.Name == "direct_duel_even_pressure");
        var normal = baseScenario with { Difficulty = "normal" };
        var hard = baseScenario with { Difficulty = "hard" };
        var impossible = baseScenario with { Difficulty = "impossible" };

        var normalPreview = AfterlifeSpiritualCombatScenarioHarness.PreviewReward(normal, outcomeBand: "player_success");
        var repeatedNormalPreview = AfterlifeSpiritualCombatScenarioHarness.PreviewReward(normal, outcomeBand: "player_success");
        var hardPreview = AfterlifeSpiritualCombatScenarioHarness.PreviewReward(hard, outcomeBand: "player_success");
        var impossiblePreview = AfterlifeSpiritualCombatScenarioHarness.PreviewReward(impossible, outcomeBand: "player_success");

        Assert.Equal(normalPreview, repeatedNormalPreview);
        Assert.Equal(30, normalPreview.FinalAmount);
        Assert.Equal(37, hardPreview.FinalAmount);
        Assert.Equal(45, impossiblePreview.FinalAmount);
        Assert.True(normalPreview.FinalAmount < hardPreview.FinalAmount);
        Assert.True(hardPreview.FinalAmount < impossiblePreview.FinalAmount);
        Assert.All(new[] { normalPreview, hardPreview, impossiblePreview }, preview =>
            Assert.InRange(preview.FinalAmount, 0, preview.Cap));
    }

    private static AfterlifeSpiritualCombatScenario BuildOverwhelmingOppositionScenario()
    {
        var baseScenario = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios()
            .Single(scenario => scenario.Name == "direct_duel_even_pressure");

        return baseScenario with
        {
            Name = "overwhelming_opposition_for_critical_symmetry",
            PlayerArtTier = 0,
            OppositionArtTier = 10,
            StartingPosition = "opposition_dominant",
            Difficulty = "impossible"
        };
    }

    private static AfterlifeSpiritualCombatScenario BuildOverwhelmingPlayerScenario()
    {
        var baseScenario = AfterlifeSpiritualCombatScenarioHarness.GenerateDefaultScenarios()
            .Single(scenario => scenario.Name == "direct_duel_even_pressure");

        return baseScenario with
        {
            Name = "overwhelming_player_for_critical_symmetry",
            PlayerArtTier = 10,
            OppositionArtTier = 0,
            StartingPosition = "player_dominant",
            Difficulty = "normal"
        };
    }
}

internal static class AfterlifeSpiritualCombatScenarioHarness
{
    private const int DirectDuelRewardAdjustment = 1;

    private static readonly IReadOnlyDictionary<string, ActionCostDefinition> ActionCosts =
        new Dictionary<string, ActionCostDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["pressure"] = new(3, 1),
            ["guard"] = new(2, 1),
            ["counter"] = new(4, 2),
            ["maneuver"] = new(3, 1),
            ["binding"] = new(4, 2),
            ["force_binding"] = new(5, 2),
            ["break_binding"] = new(3, 1),
            ["incarnation_resistance"] = new(3, 1),
            ["champion_coordination"] = new(2, 1),
            ["recover_spiritual_power"] = new(0, 0)
        };

    public static IReadOnlyList<AfterlifeSpiritualCombatScenario> GenerateDefaultScenarios() =>
    [
        new(
            Name: "direct_duel_even_pressure",
            SideModel: "direct_duel",
            OperationType: "pressure",
            PlayerArtTier: 2,
            OppositionArtTier: 2,
            PlayerActionPointsBudget: 6,
            OppositionActionPointsBudget: 6,
            PlayerActionCost: 2,
            OppositionActionCost: 2,
            StartingPosition: "contested",
            Difficulty: "normal",
            Reward: new AfterlifeSpiritualCombatRewardContext(
                Realm: "Chaos Sea",
                Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                OpposingLeadStrength: 4),
            Tags: TagSet("direct")),

        new(
            Name: "assisted_duel_player_with_support",
            SideModel: "assisted_duel",
            OperationType: "guard",
            PlayerArtTier: 1,
            OppositionArtTier: 3,
            PlayerActionPointsBudget: 7,
            OppositionActionPointsBudget: 6,
            PlayerActionCost: 2,
            OppositionActionCost: 2,
            StartingPosition: "opposition_advantaged",
            Difficulty: "hard",
            Reward: new AfterlifeSpiritualCombatRewardContext(
                Realm: "Chaos Sea",
                Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                OpposingLeadStrength: 6),
            PlayerSupportModifier: 3,
            Tags: TagSet("assisted", "support")),

        new(
            Name: "champion_duel_ally_lead",
            SideModel: "champion_duel",
            OperationType: "champion_coordination",
            PlayerArtTier: 1,
            OppositionArtTier: 4,
            PlayerActionPointsBudget: 8,
            OppositionActionPointsBudget: 7,
            PlayerActionCost: 3,
            OppositionActionCost: 2,
            StartingPosition: "contested",
            Difficulty: "hard",
            Reward: new AfterlifeSpiritualCombatRewardContext(
                Realm: "Shining Abode",
                Currency: AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
                OpposingLeadStrength: 7),
            PlayerSupportModifier: 5,
            Tags: TagSet("champion", "support")),

        new(
            Name: "control_heavy_break_binding",
            SideModel: "direct_duel",
            OperationType: "break_binding",
            PlayerArtTier: 3,
            OppositionArtTier: 2,
            PlayerActionPointsBudget: 7,
            OppositionActionPointsBudget: 6,
            PlayerActionCost: 3,
            OppositionActionCost: 2,
            StartingPosition: "opposition_advantaged",
            Difficulty: "normal",
            Reward: new AfterlifeSpiritualCombatRewardContext(
                Realm: "Chaos Sea",
                Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                OpposingLeadStrength: 5),
            Control: new AfterlifeSpiritualCombatControlContext(
                ControllerSide: "opposition",
                Level: "bound",
                SourceOperation: "binding"),
            PlayerSupportModifier: 1,
            Tags: TagSet("control-heavy", "anti-control")),

        new(
            Name: "forced_incarnation_resistance",
            SideModel: "direct_duel",
            OperationType: "incarnation_resistance",
            PlayerArtTier: 3,
            OppositionArtTier: 4,
            PlayerActionPointsBudget: 8,
            OppositionActionPointsBudget: 8,
            PlayerActionCost: 3,
            OppositionActionCost: 4,
            StartingPosition: "opposition_dominant",
            Difficulty: "impossible",
            Reward: new AfterlifeSpiritualCombatRewardContext(
                Realm: "Chaos Sea",
                Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
                OpposingLeadStrength: 9),
            Control: new AfterlifeSpiritualCombatControlContext(
                ControllerSide: "opposition",
                Level: "locked",
                SourceOperation: "force_incarnation"),
            PlayerSupportModifier: 4,
            Tags: TagSet("control-heavy", "forced-incarnation", "anti-control"))
    ];

    private static HashSet<string> TagSet(params string[] tags) =>
        new(tags, StringComparer.OrdinalIgnoreCase);

    public static AfterlifeSpiritualCombatDistributionResult RunSeededDistribution(
        AfterlifeSpiritualCombatScenario scenario,
        int seed,
        int rollCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rollCount, 0);

        var random = new Random(seed);
        var outcomeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var playerSuccesses = 0;
        var oppositionSuccesses = 0;
        var criticalRolls = 0;
        var totalMargin = 0;
        var nonCriticalRolls = 0;
        var nonCriticalPlayerSuccesses = 0;
        var nonCriticalOppositionSuccesses = 0;
        var nonCriticalTotalMargin = 0;

        for (var i = 0; i < rollCount; i++)
        {
            var playerDie = random.Next(1, 21);
            var oppositionDie = random.Next(1, 21);
            var contest = ResolveSingleContest(scenario, playerDie, oppositionDie);
            var margin = contest.Margin;
            totalMargin += margin;

            var band = contest.OutcomeBand;
            outcomeCounts[band] = outcomeCounts.GetValueOrDefault(band) + 1;

            if (OutcomeBandRank(band) > 0)
                playerSuccesses++;
            else if (OutcomeBandRank(band) < 0)
                oppositionSuccesses++;

            if (contest.HasNaturalCritical)
            {
                criticalRolls++;
            }
            else
            {
                nonCriticalRolls++;
                nonCriticalTotalMargin += margin;

                if (OutcomeBandRank(band) > 0)
                    nonCriticalPlayerSuccesses++;
                else if (OutcomeBandRank(band) < 0)
                    nonCriticalOppositionSuccesses++;
            }
        }

        var rewardPreview = ComputeRewardPreview(scenario, outcomeCounts);
        return new AfterlifeSpiritualCombatDistributionResult(
            ScenarioName: scenario.Name,
            RollCount: rollCount,
            OutcomeBandCounts: outcomeCounts,
            PlayerSuccessRate: playerSuccesses / (double)rollCount,
            OppositionSuccessRate: oppositionSuccesses / (double)rollCount,
            AverageMargin: totalMargin / (double)rollCount,
            CriticalRollCount: criticalRolls,
            RewardPreviewFinalAmount: rewardPreview.FinalAmount,
            RewardPreviewCap: rewardPreview.Cap,
            NonCriticalRollCount: nonCriticalRolls,
            NonCriticalPlayerSuccessRate: nonCriticalRolls == 0 ? 0 : nonCriticalPlayerSuccesses / (double)nonCriticalRolls,
            NonCriticalOppositionSuccessRate: nonCriticalRolls == 0 ? 0 : nonCriticalOppositionSuccesses / (double)nonCriticalRolls,
            NonCriticalAverageMargin: nonCriticalRolls == 0 ? 0 : nonCriticalTotalMargin / (double)nonCriticalRolls);
    }

    public static AfterlifeSpiritualCombatContestResult ResolveSingleContest(
        AfterlifeSpiritualCombatScenario scenario,
        int playerDie,
        int oppositionDie)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(playerDie, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(playerDie, 20);
        ArgumentOutOfRangeException.ThrowIfLessThan(oppositionDie, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(oppositionDie, 20);

        var playerTotal = playerDie + ComputePlayerModifier(scenario);
        var oppositionTotal = oppositionDie + ComputeOppositionModifier(scenario);
        var margin = playerTotal - oppositionTotal;
        var marginBand = ResolveMarginOutcomeBand(margin);
        var outcomeBand = NormalizeCriticalOutcomeBand(marginBand, playerDie, oppositionDie);
        var criticalOverrideDirection = ResolveCriticalOverrideDirection(playerDie, oppositionDie);

        return new AfterlifeSpiritualCombatContestResult(
            PlayerDie: playerDie,
            OppositionDie: oppositionDie,
            PlayerTotal: playerTotal,
            OppositionTotal: oppositionTotal,
            Margin: margin,
            MarginOutcomeBand: marginBand,
            OutcomeBand: outcomeBand,
            HasNaturalCritical: playerDie is 1 or 20 || oppositionDie is 1 or 20,
            CriticalOverrideDirection: criticalOverrideDirection);
    }

    public static int ResolveEffectiveActionCost(string operationType, int artTier)
    {
        if (!ActionCosts.TryGetValue(operationType, out var cost))
            throw new ArgumentOutOfRangeException(nameof(operationType), operationType, "Unsupported spiritual art operation.");

        return Math.Max(cost.MinCost, cost.BaseCost - Math.Max(0, artTier));
    }

    public static int ResolveSpiritFocusMaxActionPoints(int spiritFocusTier) =>
        AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(spiritFocusTier);

    public static AfterlifeSpiritualCombatActionPointPlanResult SimulateRepeatedActionPlan(
        string operationType,
        int artTier,
        int spiritFocusTier,
        int requestedRepetitions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requestedRepetitions, 0);

        var current = ResolveSpiritFocusMaxActionPoints(spiritFocusTier);
        var starting = current;
        var cost = ResolveEffectiveActionCost(operationType, artTier);
        var executed = 0;

        while (executed < requestedRepetitions && current >= cost)
        {
            current -= cost;
            executed++;
        }

        return new AfterlifeSpiritualCombatActionPointPlanResult(
            StartingActionPoints: starting,
            EffectiveCost: cost,
            RequestedRepetitions: requestedRepetitions,
            ExecutedRepetitions: executed,
            RemainingActionPoints: current,
            WasStoppedByScarcity: executed < requestedRepetitions);
    }

    public static int ResolveRecoveryDelta(string oppositionOperation, string outcome)
    {
        if (TokenEquals(oppositionOperation, "pressure", "maneuver", "binding", "force_binding", "force_incarnation"))
            return 1;

        return TokenEquals(outcome, "success") ? 3 :
            TokenEquals(outcome, "partial_success") ? 2 :
            0;
    }

    private static bool TokenEquals(string? actual, params string[] expected) =>
        expected.Any(token => string.Equals(actual, token, StringComparison.OrdinalIgnoreCase));

    private static int ComputePlayerModifier(AfterlifeSpiritualCombatScenario scenario) =>
        scenario.PlayerArtTier * 2 +
        scenario.PlayerSupportModifier +
        ComputePositionModifier(scenario.StartingPosition, "player") +
        ComputeActionPointModifier(scenario.PlayerActionPointsBudget, scenario.PlayerActionCost) +
        ComputeControlModifier(scenario.Control, "player", scenario.OperationType);

    private static int ComputeOppositionModifier(AfterlifeSpiritualCombatScenario scenario) =>
        scenario.OppositionArtTier * 2 +
        scenario.OppositionSupportModifier +
        ComputePositionModifier(scenario.StartingPosition, "opposition") +
        ComputeActionPointModifier(scenario.OppositionActionPointsBudget, scenario.OppositionActionCost) +
        ComputeControlModifier(scenario.Control, "opposition", scenario.OperationType) +
        AfterlifeSpiritualConflictState.DifficultyDefinitions[scenario.Difficulty].OppositionDiceModifier;

    private static int ComputePositionModifier(string position, string side) =>
        position switch
        {
            "player_advantaged" when side == "player" => 2,
            "player_dominant" when side == "player" => 4,
            "opposition_advantaged" when side == "opposition" => 2,
            "opposition_dominant" when side == "opposition" => 4,
            _ => 0
        };

    private static int ComputeActionPointModifier(int budget, int cost) =>
        budget >= cost ? 0 : -4;

    private static int ComputeControlModifier(
        AfterlifeSpiritualCombatControlContext? control,
        string side,
        string operationType)
    {
        if (control == null)
            return 0;

        var rank = ControlRank(control.Level);
        if (rank <= 0)
            return 0;

        if (side == "player" && control.ControllerSide == "opposition")
        {
            return operationType is "break_binding" or "incarnation_resistance" or "counter"
                ? -rank
                : -rank * 2;
        }

        if (side == "opposition" && control.ControllerSide == "player")
            return -rank * 2;

        return 0;
    }

    private static int ControlRank(string level) =>
        level switch
        {
            "hindered" => 1,
            "bound" => 2,
            "locked" => 3,
            _ => 0
        };

    private static string ResolveMarginOutcomeBand(int margin) =>
        margin >= 8 ? "decisive_player_success" :
        margin >= 3 ? "player_success" :
        margin >= -2 ? "mixed_or_no_effect" :
        margin >= -7 ? "opposition_success" :
        "decisive_opposition_success";

    private static string NormalizeCriticalOutcomeBand(string marginBand, int playerDie, int oppositionDie)
    {
        var playerCriticalSuccess = (playerDie == 20 ? 1 : 0) + (oppositionDie == 1 ? 1 : 0);
        var playerCriticalFailure = (playerDie == 1 ? 1 : 0) + (oppositionDie == 20 ? 1 : 0);

        if (playerCriticalSuccess > playerCriticalFailure)
            return OutcomeBandRank(marginBand) < 1 ? "player_success" : marginBand;

        if (playerCriticalFailure > playerCriticalSuccess)
            return OutcomeBandRank(marginBand) > -1 ? "opposition_success" : marginBand;

        return marginBand;
    }

    private static string ResolveCriticalOverrideDirection(int playerDie, int oppositionDie)
    {
        var playerCriticalSuccess = (playerDie == 20 ? 1 : 0) + (oppositionDie == 1 ? 1 : 0);
        var playerCriticalFailure = (playerDie == 1 ? 1 : 0) + (oppositionDie == 20 ? 1 : 0);

        if (playerCriticalSuccess > playerCriticalFailure)
            return "player_favorable";

        if (playerCriticalFailure > playerCriticalSuccess)
            return "player_unfavorable";

        return playerCriticalSuccess > 0 ? "cancelled" : "none";
    }

    private static int OutcomeBandRank(string band) =>
        band switch
        {
            "decisive_player_success" => 2,
            "player_success" => 1,
            "mixed_or_no_effect" => 0,
            "opposition_success" => -1,
            "decisive_opposition_success" => -2,
            _ => 0
        };

    private static RewardPreview ComputeRewardPreview(
        AfterlifeSpiritualCombatScenario scenario,
        IReadOnlyDictionary<string, int> outcomeCounts)
    {
        var mostLikelyPlayerBand = outcomeCounts
            .Where(pair => OutcomeBandRank(pair.Key) > 0)
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .FirstOrDefault() ?? "mixed_or_no_effect";
        var preview = PreviewReward(scenario, mostLikelyPlayerBand);

        return new RewardPreview(preview.FinalAmount, preview.Cap);
    }

    public static AfterlifeSpiritualCombatRewardPreview PreviewReward(
        AfterlifeSpiritualCombatScenario scenario,
        string outcomeBand)
    {
        var rewardRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(scenario.Reward.Realm) ?? "chaos_sea";
        var baseAmount = rewardRealmKey == "shining_abode"
            ? AfterlifeSpiritualConflictState.ShiningConflictRewardBaseAmount
            : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardBaseAmount;
        var cap = rewardRealmKey == "shining_abode"
            ? AfterlifeSpiritualConflictState.ShiningConflictRewardMaxAmount
            : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardMaxAmount;
        var challengeTier = ResolveChallengeTier(
            scenario.Reward.OpposingLeadStrength,
            scenario.SideModel,
            scenario.StartingPosition);
        var outcomeMultiplier = outcomeBand == "decisive_player_success" ? 150 :
            outcomeBand == "player_success" ? 100 :
            0;
        var riskMultiplier = ResolveRiskMultiplier(scenario.StartingPosition);
        var difficultyMultiplier = AfterlifeSpiritualConflictState.DifficultyDefinitions[scenario.Difficulty].RewardMultiplierPercent;
        var raw = (long)baseAmount * challengeTier * outcomeMultiplier * riskMultiplier * difficultyMultiplier / 1_000_000L;

        return new AfterlifeSpiritualCombatRewardPreview(
            RealmKey: rewardRealmKey,
            Currency: scenario.Reward.Currency,
            ChallengeTier: challengeTier,
            OutcomeMultiplierPercent: outcomeMultiplier,
            RiskMultiplierPercent: riskMultiplier,
            DifficultyMultiplierPercent: difficultyMultiplier,
            FinalAmount: (int)Math.Clamp(raw, 0, cap),
            Cap: cap);
    }

    private static int ResolveChallengeTier(int opposingLeadStrength, string sideModel, string startingPosition)
    {
        var strengthTier = opposingLeadStrength switch
        {
            <= 2 => 1,
            <= 5 => 2,
            <= 8 => 3,
            <= 11 => 4,
            _ => 5
        };
        var sideModelAdjustment = sideModel == "direct_duel" ? DirectDuelRewardAdjustment : 0;
        var positionAdjustment = startingPosition switch
        {
            "opposition_dominant" => 2,
            "opposition_advantaged" => 1,
            "player_advantaged" => -1,
            "player_dominant" => -2,
            _ => 0
        };

        return Math.Clamp(
            strengthTier + sideModelAdjustment + positionAdjustment,
            1,
            AfterlifeSpiritualConflictState.ConflictRewardMaxChallengeTier);
    }

    private static int ResolveRiskMultiplier(string startingPosition) =>
        startingPosition switch
        {
            "opposition_dominant" => 150,
            "opposition_advantaged" => 125,
            "player_advantaged" => 75,
            "player_dominant" => 50,
            _ => 100
        };

    private readonly record struct RewardPreview(int FinalAmount, int Cap);

    private readonly record struct ActionCostDefinition(int BaseCost, int MinCost);
}

internal sealed record AfterlifeSpiritualCombatScenario(
    string Name,
    string SideModel,
    string OperationType,
    int PlayerArtTier,
    int OppositionArtTier,
    int PlayerActionPointsBudget,
    int OppositionActionPointsBudget,
    int PlayerActionCost,
    int OppositionActionCost,
    string StartingPosition,
    string Difficulty,
    AfterlifeSpiritualCombatRewardContext Reward,
    AfterlifeSpiritualCombatControlContext? Control = null,
    int PlayerSupportModifier = 0,
    int OppositionSupportModifier = 0,
    IReadOnlySet<string>? Tags = null)
{
    public IReadOnlySet<string> Tags { get; init; } = Tags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

internal sealed record AfterlifeSpiritualCombatControlContext(
    string ControllerSide,
    string Level,
    string SourceOperation);

internal sealed record AfterlifeSpiritualCombatRewardContext(
    string Realm,
    string Currency,
    int OpposingLeadStrength);

internal sealed record AfterlifeSpiritualCombatRewardPreview(
    string RealmKey,
    string Currency,
    int ChallengeTier,
    int OutcomeMultiplierPercent,
    int RiskMultiplierPercent,
    int DifficultyMultiplierPercent,
    int FinalAmount,
    int Cap);

internal sealed record AfterlifeSpiritualCombatDistributionResult(
    string ScenarioName,
    int RollCount,
    IReadOnlyDictionary<string, int> OutcomeBandCounts,
    double PlayerSuccessRate,
    double OppositionSuccessRate,
    double AverageMargin,
    int CriticalRollCount,
    int RewardPreviewFinalAmount,
    int RewardPreviewCap,
    int NonCriticalRollCount,
    double NonCriticalPlayerSuccessRate,
    double NonCriticalOppositionSuccessRate,
    double NonCriticalAverageMargin);

internal sealed record AfterlifeSpiritualCombatContestResult(
    int PlayerDie,
    int OppositionDie,
    int PlayerTotal,
    int OppositionTotal,
    int Margin,
    string MarginOutcomeBand,
    string OutcomeBand,
    bool HasNaturalCritical,
    string CriticalOverrideDirection);

internal sealed record AfterlifeSpiritualCombatActionPointPlanResult(
    int StartingActionPoints,
    int EffectiveCost,
    int RequestedRepetitions,
    int ExecutedRepetitions,
    int RemainingActionPoints,
    bool WasStoppedByScarcity);
