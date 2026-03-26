using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AbodePowerRulesTests
{
    [Theory]
    [InlineData(0, 4, 2, 0, 0)]
    [InlineData(35, 5, 3, 0, 1)]
    [InlineData(55, 6, 3, 1, 2)]
    [InlineData(75, 7, 4, 1, 3)]
    [InlineData(95, 8, 4, 2, 4)]
    public void AbodePower_DerivedRules_MatchConfiguredBands(
        int currentPower,
        int expectedTradeSlots,
        int expectedQuestCap,
        int expectedBonusCharges,
        int expectedCorrectionBudgetPoints)
    {
        Assert.Equal(expectedTradeSlots, AbodePowerRules.GetTradeSlotCount(currentPower));
        Assert.Equal(expectedQuestCap, AbodePowerRules.GetGuardianQuestCap(currentPower));
        Assert.Equal(expectedBonusCharges, AbodePowerRules.GetBonusGachaCharges(currentPower));
        Assert.Equal(expectedCorrectionBudgetPoints, AbodePowerRules.GetNextLifeCorrectionBudgetPoints(currentPower));
    }

    [Theory]
    [InlineData(-80, 10, 0)]
    [InlineData(0, 35, 1)]
    [InlineData(80, 35, 2)]
    [InlineData(150, 70, 4)]
    [InlineData(250, 85, 5)]
    public void GuardianGachaCharges_AreReputationBasePlusAbodePowerBonus(int reputation, int currentPower, int expectedCharges)
    {
        Assert.Equal(expectedCharges, GuardianGachaChargeRules.GetChargesPerReturnForReputation(reputation, currentPower));
    }

    [Fact]
    public void EnsureCanonicalState_FillsDefaultAbodePowerShape()
    {
        var guardian = new JsonObject
        {
            ["guardianId"] = "guardian_test_001"
        };

        var abodePower = AbodePowerRules.EnsureCanonicalState(guardian);

        Assert.Equal(AbodePowerRules.DefaultCurrentPower, (int?)abodePower["currentPower"]);
        Assert.Equal("Хрупкая", (string?)abodePower["tier"]);
        Assert.NotNull(abodePower["lastUpdatedAt"]);
        Assert.NotNull(abodePower["history"]);
    }

    [Theory]
    [InlineData("minor", 1, 5)]
    [InlineData("medium", 2, 12)]
    [InlineData("strong", 3, 20)]
    public void CorrectionSeverityRules_AreCentralized(string severity, int expectedBudgetCost, int expectedPowerCost)
    {
        Assert.Equal(expectedBudgetCost, AbodePowerRules.GetCorrectionSeverityBudgetCost(severity));
        Assert.Equal(expectedPowerCost, AbodePowerRules.GetCorrectionSeverityAbodePowerCost(severity));
    }

    [Theory]
    [InlineData("normal", "success", 3, 0, 3)]
    [InlineData("hard", "success", 5, 2, 7)]
    [InlineData("easy", "failure", -1, 0, -1)]
    [InlineData("normal", "partial", 2, 0, 2)]
    public void GuardianQuestPowerRules_AreCentralized(string difficulty, string outcome, int expectedBase, int expectedBonus, int expectedFinal)
    {
        var baseDelta = AbodePowerRules.ResolveGuardianQuestBasePowerDelta(difficulty, outcome);
        var bonusDelta = AbodePowerRules.ResolveGuardianQuestBonusPowerDelta(
            baseDelta,
            supportsCurrentProject: difficulty == "hard" && outcome == "success",
            defendsAgainstRivalPressure: false);

        Assert.Equal(expectedBase, baseDelta);
        Assert.Equal(expectedBonus, bonusDelta);
        Assert.Equal(expectedFinal, baseDelta + bonusDelta);
    }

    [Theory]
    [InlineData("minor assist", 1)]
    [InlineData("meaningful protection", 2)]
    [InlineData("major defensive breakthrough", 3)]
    [InlineData("minor interference", -1)]
    [InlineData("major sabotage", -2)]
    [InlineData("grand strike", -4)]
    public void ProjectPowerDeltas_AreCentralized(string classification, int expectedDelta)
    {
        if (expectedDelta >= 0)
            Assert.Equal(expectedDelta, AbodePowerRules.ResolveGuardianProjectAssistPowerDelta(classification));
        else
            Assert.Equal(expectedDelta, AbodePowerRules.ResolveGuardianProjectSabotagePowerDelta(classification));
    }

    [Theory]
    [InlineData(50, 1)]
    [InlineData(100, 2)]
    [InlineData(150, 3)]
    public void InkFeatherOfferingPowerGain_UsesCentralizedBands(int offered, int expectedPowerGain)
    {
        Assert.Equal(expectedPowerGain, AbodePowerRules.ResolvePowerGainForInkFeatherOffering(offered));
    }

    [Theory]
    [InlineData(0, "normal")]
    [InlineData(35, "normal")]
    [InlineData(55, "hard")]
    [InlineData(75, "hard")]
    [InlineData(95, "epic")]
    public void GuardianQuestDifficultyCeiling_FollowsPowerBands(int currentPower, string expectedCeiling)
    {
        Assert.Equal(expectedCeiling, AbodePowerRules.GetGuardianQuestDifficultyCeiling(currentPower));
    }

    [Theory]
    [InlineData(35, "easy", true)]
    [InlineData(35, "normal", true)]
    [InlineData(35, "hard", false)]
    [InlineData(55, "hard", true)]
    [InlineData(55, "epic", false)]
    [InlineData(95, "epic", true)]
    public void GuardianQuestDifficultyAllowance_UsesSharedCeiling(int currentPower, string difficulty, bool expectedAllowed)
    {
        Assert.Equal(expectedAllowed, AbodePowerRules.IsGuardianQuestDifficultyAllowed(currentPower, difficulty));
    }

    [Theory]
    [InlineData("common", 1)]
    [InlineData("uncommon", 1)]
    [InlineData("rare", 2)]
    [InlineData("epic", 3)]
    [InlineData("legendary", 3)]
    public void ArchiveOfferingPowerGain_UsesRebalancedBands(string rarity, int expectedPowerGain)
    {
        Assert.Equal(expectedPowerGain, AbodePowerRules.ResolvePowerGainForArchiveRarity(rarity));
    }

    [Theory]
    [InlineData("common", 1)]
    [InlineData("uncommon", 1)]
    [InlineData("rare", 2)]
    [InlineData("epic", 3)]
    [InlineData("legendary", 4)]
    public void SoulRelicOfferingPowerGain_UsesRebalancedBands(string rarity, int expectedPowerGain)
    {
        Assert.Equal(expectedPowerGain, AbodePowerRules.ResolvePowerGainForSoulRelicOffering(rarity));
    }
}
