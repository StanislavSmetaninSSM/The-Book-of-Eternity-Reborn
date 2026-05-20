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
}

internal static class AfterlifeSpiritualCombatScenarioHarness
{
    private const int DirectDuelRewardAdjustment = 1;

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

        for (var i = 0; i < rollCount; i++)
        {
            var playerDie = random.Next(1, 21);
            var oppositionDie = random.Next(1, 21);
            var playerTotal = playerDie + ComputePlayerModifier(scenario);
            var oppositionTotal = oppositionDie + ComputeOppositionModifier(scenario);
            var margin = playerTotal - oppositionTotal;
            totalMargin += margin;

            var band = NormalizeCriticalOutcomeBand(ResolveMarginOutcomeBand(margin), playerDie, oppositionDie);
            outcomeCounts[band] = outcomeCounts.GetValueOrDefault(band) + 1;

            if (OutcomeBandRank(band) > 0)
                playerSuccesses++;
            else if (OutcomeBandRank(band) < 0)
                oppositionSuccesses++;

            if (playerDie is 1 or 20 || oppositionDie is 1 or 20)
                criticalRolls++;
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
            RewardPreviewCap: rewardPreview.Cap);
    }

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
        var rewardRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(scenario.Reward.Realm);
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
        var outcomeMultiplier = mostLikelyPlayerBand == "decisive_player_success" ? 150 :
            mostLikelyPlayerBand == "player_success" ? 100 :
            0;
        var riskMultiplier = ResolveRiskMultiplier(scenario.StartingPosition);
        var difficultyMultiplier = AfterlifeSpiritualConflictState.DifficultyDefinitions[scenario.Difficulty].RewardMultiplierPercent;
        var raw = (long)baseAmount * challengeTier * outcomeMultiplier * riskMultiplier * difficultyMultiplier / 1_000_000L;

        return new RewardPreview((int)Math.Clamp(raw, 0, cap), cap);
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

internal sealed record AfterlifeSpiritualCombatDistributionResult(
    string ScenarioName,
    int RollCount,
    IReadOnlyDictionary<string, int> OutcomeBandCounts,
    double PlayerSuccessRate,
    double OppositionSuccessRate,
    double AverageMargin,
    int CriticalRollCount,
    int RewardPreviewFinalAmount,
    int RewardPreviewCap);
