using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeHealingServiceContractTests
{
    [Fact]
    public void ValidPublicElyaraProfileAndQuoteRoundTripWithoutAuthorityOrInputMutation()
    {
        var actor = AfterlifeHealingServiceTestData.Actor(); var original = actor.ToJsonString();
        var profile = Assert.IsType<AfterlifeHealingServiceProfile>(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(
            AfterlifeHealingServiceTestData.Provider(), actor));
        var (wound, check) = AfterlifeHealingServiceTestData.WoundAndCheck(actor);
        var quote = Assert.IsType<AfterlifeHealingServiceQuote>(AfterlifeHealingServiceContract.CreateQuote(profile, wound, check,
            AfterlifeHealingServiceTestData.AbodeId));
        Assert.Equal(wound.Severity.Rank switch { 1 => 25, 2 => 50, 3 => 100, _ => 200 }, quote.Amount);
        Assert.Equal(5, quote.HealingTier);
        Assert.Equal(quote, AfterlifeHealingServiceContract.ParseQuote(quote.ToJson()));
        quote.ToJson()["amount"] = 0;
        Assert.Equal(quote, AfterlifeHealingServiceContract.ParseQuote(quote.ToJson()));
        Assert.Equal(original, actor.ToJsonString());
    }

    [Theory]
    [InlineData("absent_service")] [InlineData("dead")] [InlineData("missing_life")] [InlineData("hidden")]
    [InlineData("restricted")] [InlineData("unavailable")] [InlineData("relationship")] [InlineData("condition")]
    [InlineData("tier")] [InlineData("elyara_discount")] [InlineData("foreign_provider")] [InlineData("foreign_location")]
    [InlineData("unknown_field")] [InlineData("fractional_percent")] [InlineData("unknown_compensation")]
    [InlineData("foreign_realm")] [InlineData("duplicate_compensation")]
    public void CapabilityOrMalformedServiceDoesNotGrantPublicAccess(string change)
    {
        var actor = AfterlifeHealingServiceTestData.Actor(); var service = actor["healingServiceProfile"]!.AsObject();
        switch (change)
        {
            case "absent_service": actor.Remove("healingServiceProfile"); break;
            case "dead": actor["lifeState"] = "dead"; break;
            case "missing_life": actor.Remove("lifeState"); break;
            case "hidden": actor["visibility"] = "hidden"; break;
            case "restricted": service["visibility"] = "restricted"; break;
            case "unavailable": service["availability"] = "unavailable"; break;
            case "relationship": service["minimumRelationship"] = 5; break;
            case "condition": service["accessConditions"] = new JsonArray("not_proven"); break;
            case "tier": actor["standardArts"]!["spiritual_healing"] = 4; break;
            case "elyara_discount": service["priceMultiplierPercent"] = 50; break;
            case "foreign_provider": service["providerId"] = "another_guardian"; break;
            case "foreign_location": service["locationRef"] = "another_abode"; break;
            case "unknown_field": service["priceOverride"] = 1; break;
            case "fractional_percent": service["priceMultiplierPercent"] = 99.5; break;
            case "unknown_compensation": service["compensationKinds"] = new JsonArray("blood"); break;
            case "foreign_realm": actor["realm"] = "Shining Abode"; break;
            case "duplicate_compensation": service["compensationKinds"] = new JsonArray("ink_feathers", "ink_feathers"); break;
        }
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(AfterlifeHealingServiceTestData.Provider(), actor));
    }

    [Fact]
    public void FreshChangedTermsInvalidateQuoteWhileDetachedDataStaysStable()
    {
        const string otherId = "guardian_public_test";
        var actor = AfterlifeHealingServiceTestData.Actor(otherId); var owner = AfterlifeHealingServiceTestData.Provider(otherId);
        var oldProfile = Assert.IsType<AfterlifeHealingServiceProfile>(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(owner, actor));
        var (wound, check) = AfterlifeHealingServiceTestData.WoundAndCheck(actor, owner);
        var quote = Assert.IsType<AfterlifeHealingServiceQuote>(AfterlifeHealingServiceContract.CreateQuote(oldProfile, wound, check,
            AfterlifeHealingServiceTestData.AbodeId));
        actor["healingServiceProfile"]!["priceMultiplierPercent"] = 151;
        var fresh = Assert.IsType<AfterlifeHealingServiceProfile>(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(owner, actor));
        var changed = Assert.IsType<AfterlifeHealingServiceQuote>(AfterlifeHealingServiceContract.CreateQuote(fresh, wound, check,
            AfterlifeHealingServiceTestData.AbodeId));
        Assert.NotEqual(quote.Amount, changed.Amount); Assert.NotEqual(quote.Fingerprint, changed.Fingerprint);
        Assert.Equal(quote, AfterlifeHealingServiceContract.CreateQuote(oldProfile, wound, check, AfterlifeHealingServiceTestData.AbodeId));
        Assert.Null(AfterlifeHealingServiceContract.CreateQuote(fresh, wound, check, "elsewhere"));
        var rollChanged = AfterlifeHealingServiceContract.CreateQuote(fresh, wound, check with { RollMode = "advantage" },
            AfterlifeHealingServiceTestData.AbodeId);
        Assert.NotNull(rollChanged); Assert.NotEqual(changed.CheckFingerprint, rollChanged.CheckFingerprint);
    }

    [Theory]
    [InlineData("amount")] [InlineData("compensationKind")] [InlineData("quoteFingerprint")] [InlineData("extra")]
    public void AlteredOrOpenQuoteRejects(string field)
    {
        var root = AfterlifeHealingServiceTestData.Quote().ToJson();
        root[field] = field == "amount" ? JsonValue.Create(1) : JsonValue.Create("changed");
        Assert.Throws<InvalidDataException>(() => AfterlifeHealingServiceContract.ParseQuote(root));
    }
}
