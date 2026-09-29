using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectResourceSpendGainTests
{
    [Theory]
    [InlineData("periodic_spend", "floorPolicy", "registered_resource_floor")]
    [InlineData("periodic_gain", "capPolicy", "registered_resource_cap")]
    public void Parse_ResourceOperationAndBoundRemainExact(string profile, string policyField, string policy)
    {
        using var document = JsonDocument.Parse(Component(profile, policyField, policy).ToJsonString());
        var parsed = EffectComponentProfiles.ParsePeriodicResourceComponent(document.RootElement, "component");
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        Assert.Equal(profile == "periodic_spend" ? ResourceOperation.Spend : ResourceOperation.Gain, parsed.Component!.Operation);
        Assert.Equal("spiritual_action_points", parsed.Component.ResourceKey);
        Assert.Equal(policy, parsed.Component.BoundPolicy);
        Assert.Equal(1m, parsed.Component.Amount);
    }

    [Theory]
    [InlineData("periodic_spend", "floorPolicy", "registered_resource_floor")]
    [InlineData("periodic_gain", "capPolicy", "registered_resource_cap")]
    public void Parse_DamageMetadataIsNotPartOfSpendGain(string profile, string policyField, string policy)
    {
        var component = Component(profile, policyField, policy);
        component["payload"]!["damageType"] = "physical";
        using var document = JsonDocument.Parse(component.ToJsonString());
        var parsed = EffectComponentProfiles.ParsePeriodicResourceComponent(document.RootElement, "component");
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, issue => issue.FilePath.EndsWith(".damageType", StringComparison.Ordinal));
    }

    private static JsonObject Component(string profile, string policyField, string policy) => new()
    {
        ["componentId"] = "ap_delta", ["profile"] = profile, ["priority"] = 0,
        ["payload"] = new JsonObject
        {
            ["resource"] = "spiritual_action_points", ["amount"] = 1,
            [policyField] = policy
        }
    };
}
