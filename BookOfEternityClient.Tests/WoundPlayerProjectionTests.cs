using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundPlayerProjectionTests
{
    [Fact]
    public void Build_VisibleWoundAndKnownMethodAppearWithoutDiagnosis()
    {
        var input = WoundPlayerProjectionTestData.Input(WoundContractTestData.CreateActiveWound());
        var result = WoundPlayerProjection.Build(input);
        Assert.True(result.IsAvailable);
        var wound = Assert.Single(result.Active);
        Assert.Equal("Рваная рана левого бока", wound.Name);
        Assert.Equal("II", wound.Severity);
        Assert.Contains(wound.Facts, f => f.Label == "Уход" && f.Value == "Не обработана");
        Assert.Contains(wound.Facts, f => f.Label == "Восстановление" && f.Value.Contains("стабилизац"));
        Assert.Contains(wound.Facts, f => f.Value.Contains("кровотечение"));
        Assert.Single(wound.Methods);
        Assert.Empty(wound.Diagnosis);
        Assert.Empty(result.History);
    }

    [Fact]
    public void Build_EmptyIsExplicitAndDoesNotInventHistory()
    {
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input());
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Active);
        Assert.Empty(result.History);
        Assert.Contains("нет видимых активных ран", result.StatusMessage);
    }

    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void Build_CurrentRealmNeverReturnsMortalWounds(string realm)
    {
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(
            WoundContractTestData.CreateActiveWound()) with { Realm = realm });
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Active);
    }

    [Fact]
    public void Build_IncoherentAuthorityReturnsUnavailableNotHealthy()
    {
        var input = WoundPlayerProjectionTestData.Input(WoundContractTestData.CreateActiveWound());
        input.Carriers.PlayerWounds!["activeWounds"]![0]!["display"]!["name"] = "Подменённая рана";
        var result = WoundPlayerProjection.Build(input);
        Assert.False(result.IsAvailable);
        Assert.Equal(WoundPlayerProjection.UnavailableMessage, result.StatusMessage);
        Assert.Empty(result.Active);
        Assert.Empty(result.History);
    }

    [Fact]
    public void Build_ResourceRequirementsUseReadableLabelsWithoutAvailabilityClaim()
    {
        var node = WoundContractTestData.CreateActiveWound();
        var route = node["treatment"]!["routes"]![0]!;
        var resource = new JsonObject { ["kind"] = "resource_quantity", ["resourceRef"] = "energy",
            ["quantity"] = 2, ["ownerRole"] = "target" };
        route["requirements"]!.AsArray().Add(resource);
        var input = WoundPlayerProjectionTestData.Input(node);
        var text = JsonSerializer.Serialize(WoundPlayerProjection.Build(input), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("Энергия", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(resource!["resourceRef"]!.GetValue<string>(), text, StringComparison.Ordinal);
        Assert.Contains("не подтверждают наличие", text);
    }
}
