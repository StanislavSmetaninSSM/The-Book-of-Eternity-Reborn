using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundPlayerProjectionTests
{
    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public void Build_AfterlifeCurrentSelfIsPositiveAndForeignSoulIsExcluded(string realm)
    {
        var self = WoundContractTestData.CreateSpiritualActiveWound("wound_soul", realm, ownerId: "player_soul",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var foreign = WoundContractTestData.CreateSpiritualActiveWound("wound_foreign", realm, ownerId: "other_soul",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        foreign["display"]!["name"] = "Чужая закрытая рана";
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.InputInRealm(realm, self, foreign));
        Assert.True(result.IsAvailable);
        Assert.Single(result.Active);
        Assert.DoesNotContain(result.Active, w => w.Name == "Чужая закрытая рана");
    }

    [Fact]
    public void Build_NpcAndStoredOldAfterlifeWoundsDoNotMixWithCurrentMortalSelf()
    {
        var mortal = WoundContractTestData.CreateActiveWound();
        var npc = WoundContractTestData.CreateActiveWound("wound_npc", ownerKind: "npc", ownerId: "npc_other", carrierPath: WoundCarrierCatalog.NpcPath);
        npc["display"]!["name"] = "Чужая физическая рана";
        var soul = WoundContractTestData.CreateSpiritualActiveWound("wound_old_soul", ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(mortal, npc, soul));
        Assert.True(result.IsAvailable);
        Assert.Equal("Рваная рана левого бока", Assert.Single(result.Active).Name);
    }

    [Fact]
    public void Build_KnownItemLabelsAndUnknownRequirementsStayReadableAndDeclareNoAvailability()
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["treatment"]!["routes"]![0]!["requirements"]![0]!["ownerRole"] = "target";
        var input = WoundPlayerProjectionTestData.Input(node) with
        { KnownItemNames = new Dictionary<string, string> { ["sterile_thread"] = "Стерильная хирургическая нить" } };
        var method = Assert.Single(Assert.Single(WoundPlayerProjection.Build(input).Active).Methods);
        Assert.Contains(method.Requirements, q => q.Contains("Стерильная хирургическая нить") && q.Contains("1 шт."));
        Assert.Contains(method.Requirements, q => q.Contains("не ниже 2 ступени"));
        Assert.Contains("не подтверждают наличие", method.Policy);
        var unknown = Assert.Single(Assert.Single(WoundPlayerProjection.Build(input with { KnownItemNames = new Dictionary<string, string>() }).Active).Methods);
        Assert.Contains(unknown.Requirements, q => q.StartsWith("Лечебные материалы"));
        Assert.DoesNotContain(unknown.Requirements, q => q.Contains("sterile_thread") || q.Contains("field_medicine"));
    }

    [Fact]
    public void Build_HiddenOrPrivateProviderResourceReferenceNeverBecomesLabelOrQuantity()
    {
        var node = WoundContractTestData.CreateActiveWound();
        node["treatment"]!["routes"]![0]!["requirements"]!.AsArray().Add(new JsonObject
        { ["kind"] = "resource_quantity", ["resourceRef"] = "private_essence", ["quantity"] = 123, ["ownerRole"] = "provider" });
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node));
        var method = Assert.Single(Assert.Single(result.Active).Methods);
        Assert.Contains("Необходимый запас участника следует уточнить", method.Requirements);
        Assert.DoesNotContain(method.Requirements, q => q.Contains("private_essence") || q.Contains("123"));
    }
    [Theory]
    [InlineData("owner_visible", "provider", false)]
    [InlineData("owner_visible", "target", true)]
    [InlineData("hidden", "target", false)]
    [InlineData("gm_only", "target", false)]
    public void Build_DeclaredResourceVisibilityIntersectsOwnership(string visibility, string role, bool readable)
    {
        var catalog = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(catalog.TryResolveExact("energy", out var energy));
        var raw = catalog.ToCanonicalRoot()["definitions"]!.AsArray().OfType<JsonObject>()
            .Single(d => d["resourceKey"]!.GetValue<string>() == "energy").DeepClone().AsObject();
        raw.Remove("materialization");
        raw["resourceKey"] = "private_essence"; raw["displayName"] = "Личный запас эссенции";
        raw["visibility"] = visibility;
        raw["capacityPolicy"] = new JsonObject { ["kind"] = "instance_fixed" };
        var created = ResourceDefinitionCatalog.MaterializeProposal(JsonSerializer.SerializeToElement(raw), catalog,
            42, "turn_42:known_resource", () => new("definition_private_essence", "seal_private_essence"));
        Assert.True(created.IsValid, string.Join(";", created.Issues));
        var node = WoundContractTestData.CreateActiveWound();
        node["treatment"]!["routes"]![0]!["requirements"]!.AsArray().Add(new JsonObject
        { ["kind"] = "resource_quantity", ["resourceRef"] = "private_essence", ["quantity"] = 123, ["ownerRole"] = role });
        var result = WoundPlayerProjection.Build(WoundPlayerProjectionTestData.Input(node) with { Resources = catalog.With(created.Definition!) });
        var method = Assert.Single(Assert.Single(result.Active).Methods);
        Assert.Equal(readable, method.Requirements.Any(q => q.Contains("Личный запас эссенции")));
        Assert.Equal(readable, method.Requirements.Any(q => q.Contains("123")));
        Assert.DoesNotContain(method.Requirements, q => q.Contains("private_essence"));
    }

}
