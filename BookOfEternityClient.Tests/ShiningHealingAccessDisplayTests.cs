using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningHealingAccessDisplayTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void PublicRestrictedDeclarationDoesNotProveAdmission(bool available)
    {
        var (resident, profile, owner) = Inputs(3);
        profile["healingServiceProfile"]!["availability"] = available ? "available" : "unavailable";
        var before = profile.ToJsonString();
        var terms = Assert.IsType<AfterlifeHealingServiceDisplayTerms>(AfterlifeHealingServiceContract.ReadPublicDisplayTerms(owner, profile));
        Assert.Equal(available, terms.DeclaredAvailable); Assert.Equal(25, terms.MinimumRelationship);
        Assert.Equal(new[] { "Согласие Совета" }, terms.AccessConditions);
        Assert.Equal(new[] { "favor" }, terms.CompensationKinds);
        Assert.True(ShiningFactionHealingSupport.TryReadHealerTier(resident, profile, out var tier)); Assert.Equal(3, tier);
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(owner, profile));
        Assert.Equal(before, profile.ToJsonString());
    }

    [Theory]
    [InlineData("absent")] [InlineData("private")] [InlineData("provider")] [InlineData("realm")]
    [InlineData("location")] [InlineData("unknown_field")] [InlineData("low_price")] [InlineData("high_price")]
    [InlineData("fractional_price")] [InlineData("string_price")] [InlineData("bad_condition")]
    [InlineData("blank_condition")] [InlineData("string_relationship")] [InlineData("fractional_relationship")]
    [InlineData("unknown_compensation")] [InlineData("duplicate_compensation")]
    [InlineData("hidden_actor")] [InlineData("dead_actor")] [InlineData("invalid_availability")]
    public void UnknownPrivateOrMalformedDeclarationsSupplyNoPublicTerms(string change)
    {
        var (_, profile, owner) = Inputs(3); var service = profile["healingServiceProfile"]!.AsObject();
        switch (change)
        {
            case "absent": profile.Remove("healingServiceProfile"); break;
            case "private": service["visibility"] = "restricted"; break;
            case "provider": service["providerId"] = "foreign"; break;
            case "realm": service["realm"] = "chaos_sea"; break;
            case "location": service["locationRef"] = "foreign_location"; break;
            case "unknown_field": service["secretAgreement"] = "private_payload"; break;
            case "low_price": service["priceMultiplierPercent"] = 49; break;
            case "high_price": service["priceMultiplierPercent"] = 201; break;
            case "fractional_price": service["priceMultiplierPercent"] = 125.5; break;
            case "string_price": service["priceMultiplierPercent"] = "125"; break;
            case "bad_condition": service["accessConditions"] = new JsonArray(new JsonObject { ["privateId"] = "secret" }); break;
            case "blank_condition": service["accessConditions"] = new JsonArray(" "); break;
            case "string_relationship": service["minimumRelationship"] = "25"; break;
            case "fractional_relationship": service["minimumRelationship"] = 25.5; break;
            case "unknown_compensation": service["compensationKinds"] = new JsonArray("blood"); break;
            case "duplicate_compensation": service["compensationKinds"] = new JsonArray("favor", "favor"); break;
            case "hidden_actor": profile["isPlayerVisible"] = false; break;
            case "dead_actor": profile["lifeState"] = "dead"; break;
            case "invalid_availability": service["availability"] = true; break;
        }
        var before = profile.ToJsonString();
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicDisplayTerms(owner, profile));
        Assert.Equal(before, profile.ToJsonString());
    }

    [Theory]
    [InlineData(1, false, 0)] [InlineData(1, true, 1)] [InlineData(3, true, 3)] [InlineData(5, true, 4)]
    public void TariffsRequireExplicitCurrencyAndRespectCurrentHealingTier(int tier, bool currency, int rows)
    {
        var (resident, profile, _) = Inputs(tier);
        if (currency) profile["healingServiceProfile"]!["compensationKinds"] = new JsonArray("ink_feathers", "favor");
        var member = Project(resident, profile);
        var lines = ShiningFactionHealingSupport.DescribeAccess(member);
        var prices = lines.Where(l => l.StartsWith("Объявленный тариф", StringComparison.Ordinal)).ToArray();
        Assert.Equal(rows, prices.Length);
        if (currency) Assert.Contains("для раны I: 32 Чернильных Перьев", prices[0]);
        if (tier == 1) Assert.DoesNotContain(lines, l => l.Contains("для раны II", StringComparison.Ordinal));
        if (rows >= 2) Assert.Contains("для раны II: 63 Чернильных Перьев", prices[1]);
        Assert.Contains("Объявленный приём временно недоступен.", lines);
        Assert.Contains("Лечение через команду этого целителя пока недоступно.", lines);
    }

    [Fact]
    public void ExportsAreImmutableAndNextProjectionReadsChangedTerms()
    {
        var (resident, profile, _) = Inputs(3);
        var before = resident.ToJsonString() + profile.ToJsonString();
        var member = Project(resident, profile); var terms = Assert.IsType<AfterlifeHealingServiceDisplayTerms>(member.DisplayTerms);
        Assert.Equal(before, resident.ToJsonString() + profile.ToJsonString());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)terms.AccessConditions)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)terms.CompensationKinds)[0] = "ink_feathers");
        profile["healingServiceProfile"]!["accessConditions"]![0] = "Новое согласие";
        profile["healingServiceProfile"]!["minimumRelationship"] = null;
        var fresh = Project(resident, profile);
        Assert.Equal("Согласие Совета", Assert.Single(terms.AccessConditions));
        Assert.Equal("Новое согласие", Assert.Single(fresh.DisplayTerms!.AccessConditions));
        Assert.Null(fresh.DisplayTerms.MinimumRelationship);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SameNamedResidentsUseExactPrivateIdentityAndDuplicatesFailClosed(bool duplicate)
    {
        var (first, a, _) = Inputs(1, "first"); var (second, b, _) = Inputs(5, "second");
        first["displayName"] = "Общее имя"; second["displayName"] = "Общее имя";
        a["healingServiceProfile"]!["accessConditions"]![0] = "Первое согласие";
        b["healingServiceProfile"]!["accessConditions"]![0] = "Второе согласие";
        var profiles = new JsonArray(a, b); if (duplicate) profiles.Add(a.DeepClone());
        var result = ShiningFactionHealingSupport.ProjectVisibleRoster(new() { ["factionId"] = "faction_access" },
            new() { ["entries"] = new JsonArray(first, second) }, new() { ["profiles"] = profiles });
        if (duplicate) Assert.Equal("second", Assert.Single(result).ResidentId);
        else
        {
            Assert.Equal(2, result.Count); Assert.All(result, row => Assert.Equal("Общее имя", row.Name));
            Assert.Equal("Первое согласие", Assert.Single(result.Single(r => r.ResidentId == "first").DisplayTerms!.AccessConditions));
            Assert.Equal("Второе согласие", Assert.Single(result.Single(r => r.ResidentId == "second").DisplayTerms!.AccessConditions));
        }
    }

    [Fact]
    public void DisplayTermsNeverBypassActorMaterializationOrTheExactCarrier()
    {
        var (resident, profile, owner) = Inputs(3);
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicDisplayTerms(owner with { CarrierPath = "game_state/player/wounds.json" }, profile));
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicDisplayTerms(owner with { OwnerId = "foreign" }, profile));
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicDisplayTerms(null!, profile));
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicDisplayTerms(owner, null!));
        profile["materialization"]!["state"] = "pending";
        Assert.Empty(ShiningFactionHealingSupport.ProjectVisibleRoster(new() { ["factionId"] = "faction_access" },
            new() { ["entries"] = new JsonArray(resident) }, new() { ["profiles"] = new JsonArray(profile) }));
    }

    private static ShiningFactionRosterEntry Project(JsonObject resident, JsonObject profile) => Assert.Single(
        ShiningFactionHealingSupport.ProjectVisibleRoster(new() { ["factionId"] = "faction_access" },
            new() { ["entries"] = new JsonArray(resident.DeepClone()) },
            new() { ["profiles"] = new JsonArray(profile.DeepClone()) }));

    private static (JsonObject Resident, JsonObject Profile, WoundOwnerCoordinate Owner) Inputs(int tier, string id = "healer")
    {
        var resident = new JsonObject { ["residentId"] = id, ["displayName"] = "Целитель", ["residentRole"] = "healing_support",
            ["ascensionState"] = "ascended", ["shiningFactionId"] = "faction_access" };
        var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", id, "Shining Abode", 42);
        actor["standardArts"]!["spiritual_healing"] = tier; actor["lifeState"] = "alive";
        actor["healingServiceProfile"] = new JsonObject
        {
            ["providerId"] = id, ["realm"] = "shining_abode", ["locationRef"] = actor["locationId"]!.DeepClone(),
            ["visibility"] = "public", ["availability"] = "unavailable", ["minimumRelationship"] = 25,
            ["priceMultiplierPercent"] = 125, ["compensationKinds"] = new JsonArray("favor"),
            ["accessConditions"] = new JsonArray("Согласие Совета")
        };
        return (resident, actor, new("shining_abode", "resident", id, WoundCarrierCatalog.AfterlifeProfilesPath));
    }
}
