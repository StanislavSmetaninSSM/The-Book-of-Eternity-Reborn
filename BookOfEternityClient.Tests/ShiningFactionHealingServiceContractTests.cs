using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningFactionHealingServiceContractTests
{
    [Fact]
    public void ExactShiningResidentCurrencyQuoteUsesTheExistingHealingCheckAndPrice()
    {
        const string healerId = "resident_public_healer";
        const string hallId = "hall_public_healing";
        var provider = new WoundOwnerCoordinate("shining_abode", "resident", healerId,
            WoundCarrierCatalog.AfterlifeProfilesPath);
        var actor = AfterlifeHealingServiceTestData.Actor(healerId);
        actor["actorType"] = "resident";
        actor["realm"] = "Shining Abode";
        actor["locationId"] = hallId;
        actor["standardArts"]!["spiritual_healing"] = 3;
        actor["healingServiceProfile"]!["realm"] = "shining_abode";
        actor["healingServiceProfile"]!["locationRef"] = hallId;
        actor["healingServiceProfile"]!["priceMultiplierPercent"] = 101;
        var node = WoundContractTestData.CreateSpiritualActiveWound(realm: "shining_abode",
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var parsed = WoundMaterializationContract.Parse(node.ToJsonString(), "faction-service.causal.wound");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Message)));
        var wound = parsed.Wound!;
        Assert.Equal(2, wound.Severity.Rank);
        var prepared = SpiritualHealingResolver.PrepareSource(new(wound, provider, actor, null,
            new EffectMechanicsSnapshot(true, [], [], [])));
        Assert.True(prepared.IsValid, string.Join(";", prepared.Issues.Select(i => i.Message)));
        var profile = Assert.IsType<AfterlifeHealingServiceProfile>(
            AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actor));
        var actorBefore = actor.ToJsonString(); var woundBefore = WoundMaterializationContract.SerializeCanonical(wound);
        var quote = AfterlifeHealingServiceContract.CreateQuote(profile, wound, prepared.Source!, hallId);
        Assert.Equal(actorBefore, actor.ToJsonString());
        Assert.Equal(woundBefore, WoundMaterializationContract.SerializeCanonical(wound));
        // Missing support must fail here, after real profile/check preparation, not in fixture setup.
        Assert.NotNull(quote);
        Assert.Equal(provider, quote.Provider);
        Assert.Equal(wound.Owner, quote.Patient);
        Assert.Equal(51, quote.Amount);
        Assert.Equal(3, quote.HealingTier);
        Assert.Equal(quote, AfterlifeHealingServiceContract.ParseQuote(quote.ToJson()));
    }
}
