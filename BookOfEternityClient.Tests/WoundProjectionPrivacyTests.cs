using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundProjectionPrivacyTests
{
    private static string Text(WoundPlayerProjectionResult result) => JsonSerializer.Serialize(result,
        new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    [Theory]
    [InlineData("hidden")]
    [InlineData("gm_only")]
    public void Build_HiddenWoundNeverLeaksNameSymptomsOrCounts(string visibility)
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["display"]!["visibility"] = visibility;
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node));
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Active);
        Assert.DoesNotContain("Рваная", Text(result));
        Assert.DoesNotContain("кровотечение", Text(result));
    }

    [Fact]
    public void Build_DiscoveredHiddenRouteIsKnownButGmOnlyNeverProjects()
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["treatment"]!["routes"]![0]!["visibility"] = "hidden";
        var known = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node));
        Assert.Single(Assert.Single(known.Active).Methods);
        node["treatment"]!["routes"]![0]!["visibility"] = "gm_only";
        var restricted = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node));
        Assert.Empty(Assert.Single(restricted.Active).Methods);
    }

    [Fact]
    public void Build_UnknownHiddenRouteDoesNotLeakThroughRequirements()
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["treatment"]!["routes"]![0]!["visibility"] = "hidden";
        node["treatment"]!["knownRouteIds"] = new System.Text.Json.Nodes.JsonArray();
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node));
        Assert.Empty(Assert.Single(result.Active).Methods);
        Assert.DoesNotContain("clean_and_suture", Text(result));
    }

    [Fact]
    public void Build_RecursiveOutputContainsNoCanonicalEnvelopeOrReferences()
    {
        var node = WoundContractTestData.CreateActiveWound();
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node));
        Assert.True(result.IsAvailable);
        var text = Text(result);
        foreach (var forbidden in new[] { "game_state/", "wound_test_", "effect_wound_", "clean_and_suture", "sha256:", "schemaVersion", "authorityRef", "sourceFingerprint", "lastTransition" })
            Assert.DoesNotContain(forbidden, text);
    }

    [Fact]
    public void Build_UnacceptedEffectsAndHiddenSelfFailClosed()
    {
        var input = WoundPlayerProjectionTestData.Input(WoundContractTestData.CreateActiveWound());
        Assert.False(WoundPlayerProjection.Build(input with { Effects = new(false, [], [], []) }).IsAvailable);
        var hidden = WoundPlayerProjection.Build(input with { SelfVisible = false });
        Assert.True(hidden.IsAvailable);
        Assert.Empty(hidden.Active);
        Assert.Empty(hidden.History);
    }
}
