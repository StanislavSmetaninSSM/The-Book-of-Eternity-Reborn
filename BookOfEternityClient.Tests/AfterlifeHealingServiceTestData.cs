using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

internal static class AfterlifeHealingServiceTestData
{
    internal const string ElyaraId = "guard_system_elyara_001";
    internal const string AbodeId = "abode_system_elyara_001";
    internal static WoundOwnerCoordinate Provider(string id = ElyaraId) =>
        new("chaos_sea", "guardian", id, WoundCarrierCatalog.AfterlifeProfilesPath);

    internal static JsonObject Actor(string id = ElyaraId) => new()
    {
        ["actorType"] = "guardian", ["actorId"] = id, ["realm"] = "Chaos Sea", ["visibility"] = "public",
        ["lifeState"] = "alive", ["locationId"] = AbodeId,
        ["standardArts"] = new JsonObject { ["spiritual_healing"] = 5 },
        ["healingServiceProfile"] = new JsonObject
        {
            ["providerId"] = id, ["realm"] = "chaos_sea", ["locationRef"] = AbodeId,
            ["visibility"] = "public", ["availability"] = "available", ["minimumRelationship"] = null,
            ["priceMultiplierPercent"] = 100, ["compensationKinds"] = new JsonArray("ink_feathers", "favor", "quest", "free_aid"),
            ["accessConditions"] = new JsonArray()
        }
    };

    internal static (WoundMaterializationEnvelope Wound, SpiritualHealingPreparedSource Check) WoundAndCheck(
        JsonObject? actor = null, WoundOwnerCoordinate? provider = null)
    {
        actor ??= Actor(); provider ??= Provider();
        var node = WoundContractTestData.CreateSpiritualActiveWound(ownerId: "player_soul",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var wound = WoundMaterializationContract.Parse(node.ToJsonString(), "service.fixture.wound");
        Assert.True(wound.IsValid, string.Join(";", wound.Issues.Select(i => i.Message)));
        var source = SpiritualHealingResolver.PrepareSource(new(wound.Wound!, provider, actor, null,
            new EffectMechanicsSnapshot(true, [], [], [])));
        Assert.True(source.IsValid, string.Join(";", source.Issues.Select(i => i.Message)));
        return (wound.Wound!, source.Source!);
    }

    internal static AfterlifeHealingServiceQuote Quote()
    {
        var actor = Actor(); var profile = Assert.IsType<AfterlifeHealingServiceProfile>(
            AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(Provider(), actor));
        var (wound, check) = WoundAndCheck(actor);
        return Assert.IsType<AfterlifeHealingServiceQuote>(AfterlifeHealingServiceContract.CreateQuote(profile, wound, check, AbodeId));
    }

    internal static JsonObject Soul(int current = 500, bool total = true)
    {
        var wallet = new JsonObject { ["current"] = current, ["_note"] = "retained wallet annotation" };
        if (total) wallet["total"] = 900;
        return new() { ["currentRealm"] = "Chaos Sea", ["inkFeathers"] = wallet, ["_untouched"] = "retained soul data" };
    }
}
