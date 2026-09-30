using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void AfterlifeActionPointEffect_CompleteSourceUsesClosedProductionContract(string realm)
    {
        var root = Assert.Single(ParseNamedJsonFences("E_CLI_Afterlife_Turns.txt", "afterlife_ap_effect_v1"));
        var definitions = Assert.IsType<JsonArray>(root["activeEffectDefinitions"]);
        using var document = JsonDocument.Parse(definitions.ToJsonString());
        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(document.RootElement, "apExample", realm));
        var definition = Assert.Single(definitions)!;
        Assert.Equal("periodic_gain", definition["components"]![0]!["profile"]!.GetValue<string>());
        Assert.Equal("spiritual_action_points", definition["components"]![0]!["payload"]!["resource"]!.GetValue<string>());
        Assert.Equal(1, definition["lifetime"]!["initialUses"]!.GetValue<int>());
        Assert.True(definition["triggers"]![0]!["consumeUses"]!.GetValue<bool>());
    }
}
