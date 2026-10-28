using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualHealingCombatActionTests
{
    [Fact]
    public void StandardHealingUsesItsOwnBaseFiveCostAndNeverFallsBelowTwo()
    {
        Assert.True(AfterlifeActionCostRules.TryGetDefinition("pressure", out var pressure));
        Assert.Equal(2, AfterlifeActionCostRules.ResolveStandardEffectiveCost(pressure, 1));

        Assert.True(AfterlifeActionCostRules.TryGetDefinition("heal_spiritual_wound", out var healing));
        Assert.Equal(4, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 1));
        Assert.Equal(3, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 2));
        Assert.Equal(2, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 3));
        Assert.Equal(2, AfterlifeActionCostRules.ResolveStandardEffectiveCost(healing, 5));
        Assert.False(AfterlifeActionCostRules.HasCost("resilience"));
    }
}
