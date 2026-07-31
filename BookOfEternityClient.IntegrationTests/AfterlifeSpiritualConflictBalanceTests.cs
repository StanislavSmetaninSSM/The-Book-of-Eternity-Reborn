using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeSpiritualConflictBalanceTests : IDisposable
{
    private static readonly int[] AuthoritativeConflictDice =
    {
        5, 18, 14, 9, 11, 7, 20, 1, 13, 6, 16, 8, 12, 4, 10, 15, 3, 17, 2, 19
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeSpiritualConflictBalanceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-spiritual-conflict-balance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    public static IEnumerable<object[]> BalanceMatrix()
    {
        yield return [new BalanceCase(
            "equal_sides_average_roll",
            PlayerDieIndex: 4,
            OppositionDieIndex: 3,
            PlayerModifier: 0,
            OppositionModifier: 0,
            ExpectedMargin: 2,
            ExpectedBand: "mixed_or_no_effect",
            ExpectedOutcome: "no_effect",
            BeforePosition: "contested",
            AfterPosition: "contested",
            Reading: "Equal sides do not auto-win from a small roll edge.")];

        yield return [new BalanceCase(
            "weak_player_vs_average_guardian",
            PlayerDieIndex: 2,
            OppositionDieIndex: 3,
            PlayerModifier: 0,
            OppositionModifier: 4,
            ExpectedMargin: 1,
            ExpectedBand: "mixed_or_no_effect",
            ExpectedOutcome: "no_effect",
            BeforePosition: "contested",
            AfterPosition: "contested",
            Reading: "A good player roll can avoid collapse but not beat an average Guardian outright.")];

        yield return [new BalanceCase(
            "upgraded_chaos_player_vs_average_guardian",
            PlayerDieIndex: 2,
            OppositionDieIndex: 3,
            PlayerModifier: 3,
            OppositionModifier: 2,
            ExpectedMargin: 6,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "A one-to-two art-tier investment matters without becoming decisive.")];

        yield return [new BalanceCase(
            "same_upgrade_bad_roll",
            PlayerDieIndex: 0,
            OppositionDieIndex: 1,
            PlayerModifier: 3,
            OppositionModifier: 2,
            ExpectedMargin: -12,
            ExpectedBand: "decisive_opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_dominant",
            Reading: "Dice can still create a dramatic loss.")];

        yield return [new BalanceCase(
            "returned_shining_soul_retained_radiance",
            PlayerDieIndex: 12,
            OppositionDieIndex: 11,
            PlayerModifier: 4,
            OppositionModifier: 3,
            ExpectedMargin: 5,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "Retained Radiance remains relevant after return to Chaos Sea.")];

        yield return [new BalanceCase(
            "weak_player_aided_by_strong_champion",
            PlayerDieIndex: 8,
            OppositionDieIndex: 9,
            PlayerModifier: 6,
            OppositionModifier: 4,
            ExpectedMargin: 9,
            ExpectedBand: "decisive_player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_dominant",
            Reading: "Champion support solves the weak-player-plus-strong-ally case without mass combat.")];

        yield return [new BalanceCase(
            "strong_guardian_vs_novice",
            PlayerDieIndex: 3,
            OppositionDieIndex: 4,
            PlayerModifier: 0,
            OppositionModifier: 6,
            ExpectedMargin: -8,
            ExpectedBand: "decisive_opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_dominant",
            Reading: "A novice should not trade evenly with a high-authority Guardian on average rolls.")];

        yield return [new BalanceCase(
            "four_tier_advantage_average_roll",
            PlayerDieIndex: 2,
            OppositionDieIndex: 3,
            PlayerModifier: 6,
            OppositionModifier: 1,
            ExpectedMargin: 10,
            ExpectedBand: "decisive_player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_dominant",
            Reading: "Large progression advantage is decisive on normal dice.")];

        yield return [new BalanceCase(
            "four_tier_advantage_extreme_bad_roll",
            PlayerDieIndex: 0,
            OppositionDieIndex: 1,
            PlayerModifier: 6,
            OppositionModifier: 1,
            ExpectedMargin: -8,
            ExpectedBand: "decisive_opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_dominant",
            Reading: "Even a large advantage does not erase rare dramatic reversals.")];

        yield return [new BalanceCase(
            "source_of_light_lead_average_roll",
            PlayerDieIndex: 4,
            OppositionDieIndex: 3,
            PlayerModifier: SourceOfLightCapstoneState.LeadDiceBonus,
            OppositionModifier: 6,
            ExpectedMargin: 4,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "Light Incarnate strongly shifts a lead duel without forcing decisive success.")];

        yield return [new BalanceCase(
            "source_of_light_lead_extreme_bad_roll",
            PlayerDieIndex: 0,
            OppositionDieIndex: 1,
            PlayerModifier: SourceOfLightCapstoneState.LeadDiceBonus,
            OppositionModifier: 6,
            ExpectedMargin: -11,
            ExpectedBand: "decisive_opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_dominant",
            Reading: "Light Incarnate does not erase extreme dice reversals against strong opposition.")];

        yield return [new BalanceCase(
            "source_of_light_support_role",
            PlayerDieIndex: 8,
            OppositionDieIndex: 9,
            PlayerModifier: SourceOfLightCapstoneState.SupportDiceBonus,
            OppositionModifier: 6,
            ExpectedMargin: 5,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "Support-role Light Incarnate is useful, but smaller than the lead-contestant bonus.")];

        yield return [new BalanceCase(
            "non_critical_high_roll_does_not_dominate_authority",
            PlayerDieIndex: 19,
            OppositionDieIndex: 18,
            PlayerModifier: 0,
            OppositionModifier: 20,
            ExpectedMargin: -3,
            ExpectedBand: "opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_advantaged",
            Reading: "A high non-critical d20 roll does not beat overwhelming opposition authority by itself.")];

        yield return [new BalanceCase(
            "natural_twenty_normalized_success_against_overwhelming_authority",
            PlayerDieIndex: 6,
            OppositionDieIndex: 1,
            PlayerModifier: 0,
            OppositionModifier: 20,
            ExpectedMargin: -18,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "Natural 20 makes the action succeed, but only as normalized success against superior authority.")];

        yield return [new BalanceCase(
            "natural_one_normalized_failure_despite_overwhelming_authority",
            PlayerDieIndex: 7,
            OppositionDieIndex: 18,
            PlayerModifier: 20,
            OppositionModifier: 0,
            ExpectedMargin: 19,
            ExpectedBand: "opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_advantaged",
            Reading: "Natural 1 makes the action fail, but only as normalized failure rather than impossible catastrophe.")];
    }

    public static IEnumerable<object[]> RewardBalanceMatrix()
    {
        yield return [new RewardBalanceCase(
            "chaos_contested_victory",
            Realm: "Chaos Sea",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            OpposingLeadStrength: 3,
            SideModel: "direct_duel",
            StartingConflictPosition: "contested",
            OutcomeBand: "player_success",
            ExpectedChallengeTier: 3,
            ExpectedOutcomeMultiplierPercent: 100,
            ExpectedRiskMultiplierPercent: 100,
            ExpectedFinalAmount: 30,
            Reading: "A normal contested Chaos Sea win gives a noticeable but bounded Feather reward.")];

        yield return [new RewardBalanceCase(
            "chaos_contested_victory_hard_difficulty",
            Realm: "Chaos Sea",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            OpposingLeadStrength: 3,
            SideModel: "direct_duel",
            StartingConflictPosition: "contested",
            OutcomeBand: "player_success",
            ExpectedChallengeTier: 3,
            ExpectedOutcomeMultiplierPercent: 100,
            ExpectedRiskMultiplierPercent: 100,
            ExpectedFinalAmount: 37,
            Reading: "Hard difficulty raises a Chaos Sea victory reward without changing the strategic envelope.",
            Difficulty: "hard",
            ExpectedDifficultyOppositionModifier: 1,
            ExpectedDifficultyRewardMultiplierPercent: 125)];

        yield return [new RewardBalanceCase(
            "chaos_contested_victory_impossible_difficulty",
            Realm: "Chaos Sea",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            OpposingLeadStrength: 3,
            SideModel: "direct_duel",
            StartingConflictPosition: "contested",
            OutcomeBand: "player_success",
            ExpectedChallengeTier: 3,
            ExpectedOutcomeMultiplierPercent: 100,
            ExpectedRiskMultiplierPercent: 100,
            ExpectedFinalAmount: 45,
            Reading: "Impossible difficulty increases reward more strongly, while its combat modifier remains smaller than positional dominance.",
            Difficulty: "impossible",
            ExpectedDifficultyOppositionModifier: 2,
            ExpectedDifficultyRewardMultiplierPercent: 150)];

        yield return [new RewardBalanceCase(
            "chaos_low_risk_weak_conflict",
            Realm: "Chaos Sea",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers,
            OpposingLeadStrength: 1,
            SideModel: "champion_duel",
            StartingConflictPosition: "player_dominant",
            OutcomeBand: "player_success",
            ExpectedChallengeTier: 1,
            ExpectedOutcomeMultiplierPercent: 100,
            ExpectedRiskMultiplierPercent: 50,
            ExpectedFinalAmount: 5,
            Reading: "Weak/no-risk Chaos Sea wins stay low-value and should not become a farm loop.")];

        yield return [new RewardBalanceCase(
            "shining_low_risk_weak_conflict",
            Realm: "Shining Abode",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            OpposingLeadStrength: 1,
            SideModel: "champion_duel",
            StartingConflictPosition: "player_dominant",
            OutcomeBand: "player_success",
            ExpectedChallengeTier: 1,
            ExpectedOutcomeMultiplierPercent: 100,
            ExpectedRiskMultiplierPercent: 50,
            ExpectedFinalAmount: 0,
            Reading: "Trivial Shining wins may pay zero Light Sparks because that currency is intentionally scarce.")];

        yield return [new RewardBalanceCase(
            "shining_high_risk_decisive_cap",
            Realm: "Shining Abode",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            OpposingLeadStrength: 12,
            SideModel: "direct_duel",
            StartingConflictPosition: "opposition_dominant",
            OutcomeBand: "decisive_player_success",
            ExpectedChallengeTier: 5,
            ExpectedOutcomeMultiplierPercent: 150,
            ExpectedRiskMultiplierPercent: 150,
            ExpectedFinalAmount: AfterlifeSpiritualConflictState.ShiningConflictRewardMaxAmount,
            Reading: "Even the hardest Shining victory is capped so Light Sparks stay scarce.")];
    }

    [Fact]
    public void EnlightenmentRankLadder_ReachesAscensionReadyAtSixtyExperience()
    {
        var finalRank = AfterlifeSpiritualConflictState.EnlightenmentRanks
            .Single(rank => rank.Rank == 5);

        Assert.Equal(AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience, finalRank.RequiredProgress);
        Assert.Equal(60, AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience);
        Assert.Equal(4, AfterlifeProgressionTuning.CultivateEnlightenmentExperiencePerFeather);
        Assert.Equal(80, AfterlifeProgressionTuning.ComputeCultivateEnlightenmentExperienceGain(20));
    }

    [Fact]
    public void DifficultyModifiers_RemainBelowStrategicAfterlifeCombatBonuses()
    {
        var impossible = AfterlifeSpiritualConflictState.DifficultyDefinitions["impossible"];
        const int dominantPositionModifier = 4;
        const int lightIncarnateLeadModifier = 8;

        Assert.Equal(2, impossible.OppositionDiceModifier);
        Assert.True(impossible.OppositionDiceModifier < dominantPositionModifier);
        Assert.True(impossible.OppositionDiceModifier < lightIncarnateLeadModifier);
    }

    [Theory]
    [MemberData(nameof(BalanceMatrix))]
    public async Task ValidateGameStateAsync_AfterlifeConflictBalanceMatrix_AcceptsExpectedDiceBands(BalanceCase scenario)
    {
        await WriteSoulStateAsync();
        var diceAudit = BuildDiceAudit(scenario);
        await WriteConflictStateWithExchangeAsync(scenario, diceAudit);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Equal(scenario.ExpectedMargin, diceAudit["margin"]?.GetValue<int>());
        Assert.Equal(scenario.ExpectedBand, diceAudit["outcomeBand"]?.GetValue<string>());
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase) == true ||
            string.Equals(issue.Code, "afterlife_conflict_exchange_missing_dice_audit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NaturalTwentyCritical_RejectsMarginOnlyFailureBand()
    {
        await WriteSoulStateAsync();
        var scenario = new BalanceCase(
            "bad_critical_band",
            PlayerDieIndex: 6,
            OppositionDieIndex: 1,
            PlayerModifier: 0,
            OppositionModifier: 20,
            ExpectedMargin: -18,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "Natural 20 cannot be recorded as a failed action.");
        var diceAudit = BuildDiceAudit(scenario);
        diceAudit["outcomeBand"] = "decisive_opposition_success";
        await WriteConflictStateWithExchangeAsync(scenario, diceAudit);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_outcome_band_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CriticalOutcomeShift_RequiresNormalizationAudit()
    {
        await WriteSoulStateAsync();
        var scenario = new BalanceCase(
            "missing_critical_normalization",
            PlayerDieIndex: 6,
            OppositionDieIndex: 1,
            PlayerModifier: 0,
            OppositionModifier: 20,
            ExpectedMargin: -18,
            ExpectedBand: "player_success",
            ExpectedOutcome: "success",
            BeforePosition: "contested",
            AfterPosition: "player_advantaged",
            Reading: "Natural 20 needs scale normalization when it overrides a margin loss.");
        var diceAudit = BuildDiceAudit(scenario);
        diceAudit.Remove("criticalResult");
        await WriteConflictStateWithExchangeAsync(scenario, diceAudit);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_missing_critical_result", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CriticalNormalizationAudit_RejectsContradictoryBand()
    {
        await WriteSoulStateAsync();
        var scenario = new BalanceCase(
            "contradictory_critical_normalization",
            PlayerDieIndex: 7,
            OppositionDieIndex: 18,
            PlayerModifier: 20,
            OppositionModifier: 0,
            ExpectedMargin: 19,
            ExpectedBand: "opposition_success",
            ExpectedOutcome: "setback",
            BeforePosition: "contested",
            AfterPosition: "opposition_advantaged",
            Reading: "Natural 1 normalization cannot claim player success.");
        var diceAudit = BuildDiceAudit(scenario);
        if (diceAudit["criticalResult"] is JsonObject criticalResult)
            criticalResult["normalizedOutcomeBand"] = "player_success";
        await WriteConflictStateWithExchangeAsync(scenario, diceAudit);
        await WritePreTurnActiveConflictSnapshotAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_dice_critical_normalized_band_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(RewardBalanceMatrix))]
    public async Task ValidateGameStateAsync_AfterlifeConflictRewardBalanceMatrix_AcceptsExpectedEnvelope(RewardBalanceCase scenario)
    {
        Assert.Equal(scenario.ExpectedFinalAmount, ComputeRewardFinalAmount(scenario));

        await WriteRewardBalanceCurrentStateAsync(scenario);
        await WriteResolvedConflictRewardStateAsync(scenario);
        await WriteRewardBalanceSnapshotAsync(scenario);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_conflict_reward_", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_conflict_dice_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_AfterlifeRewardBalance_RejectsWrongRealmCurrency()
    {
        var scenario = new RewardBalanceCase(
            "chaos_wrong_currency",
            Realm: "Chaos Sea",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            OpposingLeadStrength: 3,
            SideModel: "direct_duel",
            StartingConflictPosition: "contested",
            OutcomeBand: "player_success",
            ExpectedChallengeTier: 3,
            ExpectedOutcomeMultiplierPercent: 100,
            ExpectedRiskMultiplierPercent: 100,
            ExpectedFinalAmount: 30,
            Reading: "Chaos Sea rewards must not mint Light Sparks.");

        await WriteSoulStateAsync("Chaos Sea", inkFeathers: 50);
        await WriteResolvedConflictRewardStateAsync(scenario);
        await WriteRewardBalanceSnapshotAsync(scenario, preTurnInkFeathers: 20);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_wrong_currency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AfterlifeRewardBalance_RejectsAmountAboveRealmCap()
    {
        var scenario = new RewardBalanceCase(
            "shining_reward_over_cap",
            Realm: "Shining Abode",
            Currency: AfterlifeSpiritualConflictState.RewardCurrencyLightSparks,
            OpposingLeadStrength: 12,
            SideModel: "direct_duel",
            StartingConflictPosition: "opposition_dominant",
            OutcomeBand: "decisive_player_success",
            ExpectedChallengeTier: 5,
            ExpectedOutcomeMultiplierPercent: 150,
            ExpectedRiskMultiplierPercent: 150,
            ExpectedFinalAmount: AfterlifeSpiritualConflictState.ShiningConflictRewardMaxAmount + 1,
            Reading: "Realm caps must remain authoritative even if the GM overstates finalAmount.");

        await WriteSoulStateAsync("Shining Abode", inkFeathers: 20);
        await WriteShiningStateAsync(lightSparks: 5 + scenario.ExpectedFinalAmount);
        await WriteResolvedConflictRewardStateAsync(scenario);
        await WriteRewardBalanceSnapshotAsync(scenario, preTurnLightSparks: 5);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_conflict_reward_amount_over_cap", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject BuildDiceAudit(BalanceCase scenario)
    {
        var playerDie = AuthoritativeConflictDice[scenario.PlayerDieIndex];
        var oppositionDie = AuthoritativeConflictDice[scenario.OppositionDieIndex];
        var playerTotal = playerDie + scenario.PlayerModifier;
        var oppositionTotal = oppositionDie + scenario.OppositionModifier;
        var margin = playerTotal - oppositionTotal;
        Assert.Equal(scenario.ExpectedMargin, margin);
        var marginBand = ExpectedBand(margin);
        Assert.Equal(scenario.ExpectedBand, ExpectedBand(margin, playerDie, oppositionDie));

        var audit = new JsonObject
        {
            ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
            ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
            ["diceUsed"] = new JsonArray(
                new JsonObject
                {
                    ["side"] = "player",
                    ["sourceIndex"] = scenario.PlayerDieIndex,
                    ["sides"] = 20,
                    ["value"] = playerDie
                },
                new JsonObject
                {
                    ["side"] = "opposition",
                    ["sourceIndex"] = scenario.OppositionDieIndex,
                    ["sides"] = 20,
                    ["value"] = oppositionDie
                }),
            ["playerTotal"] = playerTotal,
            ["oppositionTotal"] = oppositionTotal,
            ["margin"] = margin,
            ["outcomeBand"] = scenario.ExpectedBand,
            ["modifierBreakdown"] = new JsonObject
            {
                ["player"] = new JsonArray(
                    new JsonObject
                    {
                        ["source"] = "balance audit player progression/support modifier",
                        ["value"] = scenario.PlayerModifier
                    }),
                ["opposition"] = new JsonArray(
                    new JsonObject
                    {
                        ["source"] = "balance audit opposition progression/support modifier",
                        ["value"] = scenario.OppositionModifier
                    })
            }
        };

        if (!string.Equals(marginBand, scenario.ExpectedBand, StringComparison.Ordinal))
        {
            audit["criticalResult"] = new JsonObject
            {
                ["playerNaturalRoll"] = playerDie,
                ["oppositionNaturalRoll"] = oppositionDie,
                ["marginOutcomeBand"] = marginBand,
                ["normalizedOutcomeBand"] = scenario.ExpectedBand,
                ["scaleLimit"] = "Critical result changes success/failure only; it does not authorize impossible scale beyond the side authority.",
                ["narrativeConstraint"] = scenario.Reading
            };
        }

        return audit;
    }

    private static string ExpectedBand(int margin) =>
        margin >= 8 ? "decisive_player_success" :
        margin >= 3 ? "player_success" :
        margin >= -2 ? "mixed_or_no_effect" :
        margin >= -7 ? "opposition_success" :
        "decisive_opposition_success";

    private static string ExpectedBand(int margin, int playerDie, int oppositionDie)
    {
        var marginBand = ExpectedBand(margin);
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

    private static int ComputeRewardFinalAmount(RewardBalanceCase scenario)
    {
        var rewardRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(scenario.Realm);
        var baseAmount = string.Equals(rewardRealmKey, "shining_abode", StringComparison.Ordinal)
            ? AfterlifeSpiritualConflictState.ShiningConflictRewardBaseAmount
            : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardBaseAmount;
        var cap = string.Equals(rewardRealmKey, "shining_abode", StringComparison.Ordinal)
            ? AfterlifeSpiritualConflictState.ShiningConflictRewardMaxAmount
            : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardMaxAmount;

        var raw = (long)baseAmount *
                  scenario.ExpectedChallengeTier *
                  scenario.ExpectedOutcomeMultiplierPercent *
                  scenario.ExpectedRiskMultiplierPercent *
                  scenario.ExpectedDifficultyRewardMultiplierPercent /
                  1_000_000L;
        return (int)Math.Clamp(raw, 0, cap);
    }

    private Task WriteSoulStateAsync()
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
    }

    private Task WriteSoulStateAsync(string realm, int inkFeathers)
    {
        return _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", BuildSoulStateJson(realm, inkFeathers));
    }

    private Task WriteShiningStateAsync(int lightSparks)
    {
        return _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, BuildShiningStateJson(lightSparks));
    }

    private Task WriteGameSettingsAsync(string difficulty)
    {
        var definition = AfterlifeSpiritualConflictState.DifficultyDefinitions[difficulty];
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath, $$"""
        {
          "difficulty": "{{definition.Difficulty}}",
          "hardMode": {{(string.Equals(definition.Difficulty, "hard", StringComparison.Ordinal) ? "true" : "false")}},
          "impossibleMode": {{(string.Equals(definition.Difficulty, "impossible", StringComparison.Ordinal) ? "true" : "false")}}
        }
        """);
    }

    private async Task WriteRewardBalanceCurrentStateAsync(RewardBalanceCase scenario)
    {
        if (!string.IsNullOrWhiteSpace(scenario.Difficulty))
            await WriteGameSettingsAsync(scenario.Difficulty);

        if (IsShiningRealm(scenario))
        {
            await WriteSoulStateAsync("Shining Abode", inkFeathers: 20);
            await WriteShiningStateAsync(lightSparks: 5 + scenario.ExpectedFinalAmount);
            return;
        }

        await WriteSoulStateAsync("Chaos Sea", inkFeathers: 20 + scenario.ExpectedFinalAmount);
    }

    private Task WriteResolvedConflictRewardStateAsync(RewardBalanceCase scenario)
    {
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": null,
          "recentConflicts": [
            {
              "mode": "resolve",
              "conflictId": "afterlife_conflict_balance_001",
              "realm": {{JsonSerializer.Serialize(scenario.Realm)}},
              "sideModel": {{JsonSerializer.Serialize(scenario.SideModel)}},
              "resolutionState": "resolved",
              "resolvedAtTurn": 7,
              "operationType": "pressure",
              "playerOutcome": "won",
              "diceAudit": {{BuildRewardDiceAudit(scenario).ToJsonString()}},
              "summary": {{JsonSerializer.Serialize(scenario.Reading)}},
              "{{AfterlifeSpiritualConflictState.RewardAuditProperty}}": {{BuildRewardAudit(scenario).ToJsonString()}}
            }
          ]
        }
        """);
    }

    private static JsonObject BuildRewardDiceAudit(RewardBalanceCase scenario)
    {
        var playerDieIndex = string.Equals(scenario.OutcomeBand, "decisive_player_success", StringComparison.OrdinalIgnoreCase)
            ? 6
            : 2;
        var oppositionDieIndex = string.Equals(scenario.OutcomeBand, "decisive_player_success", StringComparison.OrdinalIgnoreCase)
            ? 7
            : 3;
        var playerDie = AuthoritativeConflictDice[playerDieIndex];
        var oppositionDie = AuthoritativeConflictDice[oppositionDieIndex];
        var playerModifier = string.Equals(scenario.OutcomeBand, "decisive_player_success", StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;
        var oppositionModifier = scenario.ExpectedDifficultyOppositionModifier;
        var playerTotal = playerDie + playerModifier;
        var oppositionTotal = oppositionDie + oppositionModifier;
        var margin = playerTotal - oppositionTotal;

        Assert.Equal(scenario.OutcomeBand, ExpectedBand(margin));

        var audit = new JsonObject
        {
            ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
            ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
            ["diceUsed"] = new JsonArray(
                new JsonObject
                {
                    ["side"] = "player",
                    ["sourceIndex"] = playerDieIndex,
                    ["sides"] = 20,
                    ["value"] = playerDie
                },
                new JsonObject
                {
                    ["side"] = "opposition",
                    ["sourceIndex"] = oppositionDieIndex,
                    ["sides"] = 20,
                    ["value"] = oppositionDie
                }),
            ["playerTotal"] = playerTotal,
            ["oppositionTotal"] = oppositionTotal,
            ["margin"] = margin,
            ["outcomeBand"] = scenario.OutcomeBand,
            ["modifierBreakdown"] = new JsonObject
            {
                ["player"] = new JsonArray(
                    new JsonObject
                    {
                        ["source"] = "reward balance audit player modifier",
                        ["value"] = playerModifier
                    }),
                ["opposition"] = new JsonArray(
                    new JsonObject
                    {
                        ["source"] = "reward balance audit opposition modifier",
                        ["value"] = 0
                    })
            }
        };

        if (!string.IsNullOrWhiteSpace(scenario.Difficulty))
        {
            audit["difficultyAudit"] = BuildDifficultyAudit(scenario);
            if (audit["modifierBreakdown"] is JsonObject modifierBreakdown &&
                modifierBreakdown["opposition"] is JsonArray oppositionModifiers)
            {
                oppositionModifiers.Add(new JsonObject
                {
                    ["modifierType"] = "game_difficulty",
                    ["source"] = "Сложность игры",
                    ["value"] = scenario.ExpectedDifficultyOppositionModifier
                });
            }
        }

        return audit;
    }

    private static JsonObject BuildDifficultyAudit(RewardBalanceCase scenario)
    {
        var definition = AfterlifeSpiritualConflictState.DifficultyDefinitions[scenario.Difficulty!];
        return new JsonObject
        {
            ["difficulty"] = definition.Difficulty,
            ["russianLabel"] = definition.RussianLabel,
            ["source"] = $"{AfterlifeSpiritualConflictState.DifficultySettingsPath}.difficulty",
            ["oppositionModifier"] = scenario.ExpectedDifficultyOppositionModifier,
            ["rewardMultiplierPercent"] = scenario.ExpectedDifficultyRewardMultiplierPercent
        };
    }

    private static JsonObject BuildRewardAudit(RewardBalanceCase scenario)
    {
        var rewardRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(scenario.Realm);
        var baseAmount = string.Equals(rewardRealmKey, "shining_abode", StringComparison.Ordinal)
            ? AfterlifeSpiritualConflictState.ShiningConflictRewardBaseAmount
            : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardBaseAmount;

        var audit = new JsonObject
        {
            ["realm"] = scenario.Realm,
            ["currency"] = scenario.Currency,
            ["baseAmount"] = baseAmount,
            ["opposingLeadStrength"] = scenario.OpposingLeadStrength,
            ["sideModel"] = scenario.SideModel,
            ["startingConflictPosition"] = scenario.StartingConflictPosition,
            ["challengeTier"] = scenario.ExpectedChallengeTier,
            ["outcomeMultiplierPercent"] = scenario.ExpectedOutcomeMultiplierPercent,
            ["riskMultiplierPercent"] = scenario.ExpectedRiskMultiplierPercent,
            ["riskReason"] = $"Started from {scenario.StartingConflictPosition} for balance audit.",
            ["finalAmount"] = scenario.ExpectedFinalAmount,
            ["resolvedAtTurn"] = 7,
            ["narrativeReason"] = scenario.Reading
        };

        if (!string.IsNullOrWhiteSpace(scenario.Difficulty))
            audit["difficultyAudit"] = BuildDifficultyAudit(scenario);

        return audit;
    }

    private static string BuildSoulStateJson(string realm, int inkFeathers) => $$"""
    {
      "soulName": "Асуран",
      "currentRealm": {{JsonSerializer.Serialize(realm)}},
      "inkFeathers": {
        "current": {{inkFeathers}},
        "total": {{inkFeathers}}
      }
    }
    """;

    private static string BuildShiningStateJson(int lightSparks) => $$"""
    {
      "availability": "active",
      "radiance": {
        "experience": 250,
        "tier": 2
      },
      "lightSparks": {{lightSparks}},
      "halls": [],
      "factions": [],
      "shiningPoliticalActors": [],
      "gates": {
        "draftVersion": 0,
        "hasOpenDraft": false,
        "isStale": false,
        "nextCandidateCursor": 0,
        "rerollsRemaining": 0,
        "allCandidateBlessingCards": [],
        "availableBlessingCards": [],
        "shownBlessingCardIds": [],
        "selectedBlessingCardIds": []
      },
      "preparedIncarnationPackage": null,
      "gachaSystem": {
        "chargesPerReturn": 0,
        "chargesUsedThisReturn": 0,
        "currentReturnCycleId": "",
        "gachaHistory": []
      }
    }
    """;

    private Task WriteConflictStateWithExchangeAsync(BalanceCase scenario, JsonObject diceAudit)
    {
        return _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, $$"""
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_balance_001",
            "realm": "Chaos Sea",
            "sideModel": {{JsonSerializer.Serialize(scenario.Name == "weak_player_aided_by_strong_champion" ? "champion_duel" : "direct_duel")}},
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": {{JsonSerializer.Serialize(scenario.AfterPosition)}},
            "resolutionState": "active",
            "exchangeLog": [
              {
                "exchangeId": {{JsonSerializer.Serialize("exchange_balance_" + scenario.Name)}},
                "operationType": "pressure",
                "outcome": {{JsonSerializer.Serialize(scenario.ExpectedOutcome)}},
                "before": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "clear",
                  "conflictPosition": {{JsonSerializer.Serialize(scenario.BeforePosition)}}
                },
                "after": {
                  "playerSideStrain": "clear",
                  "oppositionSideStrain": "clear",
                  "conflictPosition": {{JsonSerializer.Serialize(scenario.AfterPosition)}}
                },
                "diceAudit": {{diceAudit.ToJsonString()}}
              }
            ]
          },
          "recentConflicts": []
        }
        """);
    }

    private async Task WritePreTurnActiveConflictSnapshotAsync()
    {
        const string soul = """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """;
        const string conflict = """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_balance_001",
            "realm": "Chaos Sea",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Асуран"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "guardian",
                "actorId": "guardian_liora",
                "displayName": "Лиора",
                "actorArtTierSnapshot": {
                  "pressure": 2,
                  "guard": 1
                },
                "artAuthoritySource": "guardian_state"
              },
              "supporters": []
            },
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "resolutionState": "active",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """;

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, conflict);
        await WriteValidatedSnapshotManifestAsync(
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, conflict));
    }

    private async Task WriteRewardBalanceSnapshotAsync(
        RewardBalanceCase scenario,
        int? preTurnInkFeathers = null,
        int? preTurnLightSparks = null)
    {
        var soul = BuildSoulStateJson(scenario.Realm, preTurnInkFeathers ?? 20);
        var conflict = BuildRewardPreTurnActiveConflictRootJson(scenario);
        var snapshotFiles = new List<(string Path, string Json)>
        {
            ("game_state/meta/soul_state.json", soul),
            (AfterlifeSpiritualConflictState.StatePath, conflict)
        };

        await WriteSnapshotFileAsync("game_state/meta/soul_state.json", soul);
        await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.StatePath, conflict);
        var settings = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath);
        if (!string.IsNullOrWhiteSpace(settings))
        {
            await WriteSnapshotFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath, settings);
            snapshotFiles.Add((AfterlifeSpiritualConflictState.DifficultySettingsPath, settings));
        }

        if (IsShiningRealm(scenario))
        {
            var shining = BuildShiningStateJson(preTurnLightSparks ?? 5);
            await WriteSnapshotFileAsync(ShiningAbodeState.StatePath, shining);
            snapshotFiles.Add((ShiningAbodeState.StatePath, shining));
            await WriteValidatedSnapshotManifestAsync(snapshotFiles.ToArray());
            return;
        }

        await WriteValidatedSnapshotManifestAsync(snapshotFiles.ToArray());
    }

    private static string BuildRewardPreTurnActiveConflictRootJson(RewardBalanceCase scenario) => $$"""
    {
      "schemaVersion": 1,
      "activeConflict": {
        "conflictId": "afterlife_conflict_balance_001",
        "realm": {{JsonSerializer.Serialize(scenario.Realm)}},
        "sideModel": {{JsonSerializer.Serialize(scenario.SideModel)}},
        "playerSide": {
          "leadContestant": {
            "actorType": "player",
            "actorId": "player_soul",
            "displayName": "Асуран"
          },
          "supporters": []
        },
        "oppositionSide": {
          "leadContestant": {
            "actorType": "guardian",
            "actorId": "guardian_liora",
            "displayName": "Лиора",
            "actorArtTierSnapshot": {
              "pressure": {{Math.Max(0, scenario.OpposingLeadStrength - 1)}}
            },
            "artAuthoritySource": "guardian_state"
          },
          "supporters": []
        },
        "playerSideStrain": "clear",
        "oppositionSideStrain": "clear",
        "conflictPosition": {{JsonSerializer.Serialize(scenario.StartingConflictPosition)}},
        "resolutionState": "active",
        "exchangeLog": []
      },
      "recentConflicts": []
    }
    """;

    private static bool IsShiningRealm(RewardBalanceCase scenario) =>
        string.Equals(
            AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(scenario.Realm),
            "shining_abode",
            StringComparison.Ordinal);

    private Task WriteSnapshotFileAsync(string logicalPath, string json)
    {
        return _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{logicalPath}", json);
    }

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_spiritual_conflict_balance_tests";
        const string requestId = "request_spiritual_conflict_balance_tests";
        const int turnNumber = 7;
        const string playerAction = "[AFTERLIFE_SPIRITUAL_ACTION: afterlife_conflict_balance_001] Проверяю баланс духовного поединка.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": {{JsonSerializer.Serialize(playerAction)}},
          "preGeneratedDices1d20": {{JsonSerializer.Serialize(AuthoritativeConflictDice)}}
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in snapshotFiles)
        {
            files[path] = $"game_state/control/pending_turn_snapshot/{path}";
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-05-06T00:00:00Z",
            ["playerAction"] = playerAction,
            ["preGeneratedDices1d20"] = JsonSerializer.SerializeToNode(AuthoritativeConflictDice),
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "afterlife-spiritual-conflict-balance-tests",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }

    public sealed record BalanceCase(
        string Name,
        int PlayerDieIndex,
        int OppositionDieIndex,
        int PlayerModifier,
        int OppositionModifier,
        int ExpectedMargin,
        string ExpectedBand,
        string ExpectedOutcome,
        string BeforePosition,
        string AfterPosition,
        string Reading);

    public sealed record RewardBalanceCase(
        string Name,
        string Realm,
        string Currency,
        int OpposingLeadStrength,
        string SideModel,
        string StartingConflictPosition,
        string OutcomeBand,
        int ExpectedChallengeTier,
        int ExpectedOutcomeMultiplierPercent,
        int ExpectedRiskMultiplierPercent,
        int ExpectedFinalAmount,
        string Reading,
        string? Difficulty = null,
        int ExpectedDifficultyOppositionModifier = 0,
        int ExpectedDifficultyRewardMultiplierPercent = 100);
}
