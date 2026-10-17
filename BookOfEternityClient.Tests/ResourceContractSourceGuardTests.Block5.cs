using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ResourceContractSourceGuardTests
{
    // The former monolith row is now seven actual rule bodies over the same assertion core.
    [Theory]
    [InlineData("Units_Ids")]
    [InlineData("Characteristics_Progression")]
    [InlineData("Combat_Foundations")]
    [InlineData("Items_Resources")]
    [InlineData("Narrative_Social")]
    [InlineData("Wounds")]
    [InlineData("World_Chronology")]
    public void Block5ActualModulesKeepResourceAndItemAuthorityGuardsNonVacuous(string topic)
    {
        var rule = "Rules/Block_5_" + topic + ".txt"; var example = "Examples/E_Block_5_" + topic + ".txt";
        AssertActiveGmContract(rule);
        Assert.Empty(FindRemovedAuthorityViolations(rule)); Assert.Empty(FindRemovedAuthorityViolations(example));
        foreach (var path in new[] { rule, example })
        {
            Assert.True(IsItemAuthorityPath(path));
            Assert.True(RemovedItemPropertyPattern.IsMatch("\"resource\": 100"));
            Assert.False(IsExplicitRejectionLine("\"resource\": 100"));
        }
    }
}
