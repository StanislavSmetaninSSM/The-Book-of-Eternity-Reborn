using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningFactionHealingSupportContractTests
{
    [Fact]
    public void AuthoredHealingRoleSurvivesExistingNormalization()
    {
        var resident = new JsonObject
        {
            ["ascensionState"] = "ascended",
            ["shiningFactionId"] = "faction_healing_support",
            ["residentRole"] = "archive_support",
            ["isPresent"] = false // Chaos abode presence is not Shining membership.
        };
        ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot: null);
        Assert.Equal("archive_support", resident["residentRole"]!.GetValue<string>());

        resident["residentRole"] = "healing_support";
        ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot: null);
        Assert.Equal("healing_support", resident["residentRole"]!.GetValue<string>());
        Assert.False(resident.ContainsKey("standardArts"));
    }
}
