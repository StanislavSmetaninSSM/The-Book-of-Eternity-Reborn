using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeActionCostRulesTests
{
    [Theory]
    [InlineData("pressure", 3, 1)]
    [InlineData("force_binding", 5, 2)]
    [InlineData("recover_spiritual_power", 0, 0)]
    public void TryGetDefinition_ReturnsCanonicalOperationCosts(string operationType, int baseCost, int minCost)
    {
        Assert.True(AfterlifeActionCostRules.TryGetDefinition(operationType, out var definition));
        Assert.Equal(baseCost, definition.BaseCost);
        Assert.Equal(minCost, definition.MinCost);
    }

    [Theory]
    [InlineData("pressure", 0, 3)]
    [InlineData("pressure", 1, 2)]
    [InlineData("pressure", 8, 1)]
    [InlineData("force_binding", 3, 2)]
    public void ResolveStandardEffectiveCost_ReducesCostByTierToMinimum(string operationType, int tier, int expected)
    {
        Assert.True(AfterlifeActionCostRules.TryGetDefinition(operationType, out var definition));

        Assert.Equal(expected, AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, tier));
    }

    [Theory]
    [InlineData(2, 3, 150, 5)]
    [InlineData(2, 2, 150, 3)]
    [InlineData(2, 1, 150, 2)]
    public void ComputeSpecialArtEffectiveCost_AppliesMultiplierAndMinimum(int minCost, int standardCost, int multiplier, int expected)
    {
        Assert.Equal(expected, AfterlifeActionCostRules.ComputeSpecialArtEffectiveCost(minCost, standardCost, multiplier));
    }
}
