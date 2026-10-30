using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ShiningFactionHealingServiceContractTests
{
    [Theory]
    [InlineData("active")] [InlineData("weakened")] [InlineData("leaderless")]
    public void ExplicitCurrentFactionServiceIncludesSupportedLeaderlessWithoutTradePrivileges(string lifecycle)
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var faction = input.Shining["factions"]![0]!.AsObject();
        faction["factionLifecycle"]!["state"] = lifecycle;
        Assert.True(SarefMainStoryState.IsPlayerVisibleShiningFaction(faction));
        var actor = input.Profiles["profiles"]![0]!.AsObject();
        Assert.Empty(ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(actor),
            "independent.service.profile", requireEnvelope: true, canTradeEvidence: false));
        var before = Serialize(input);
        var service = Assert.Single(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, input.Profiles));
        var quote = Quote(service.Profile, actor);
        var admission = AfterlifeWoundTreatmentAuthority.RequireServiceAdmission(null, input.Profiles, quote, input.Shining, input.Residents);
        Assert.Equal(service.AdmissionFingerprint, admission.ProviderFingerprint);
        Assert.Equal(ShiningFactionHealingServiceTestData.HallId, admission.Scope.LocationId);
        Assert.Equal("Целитель 1", service.Name); Assert.Equal(51, quote.Amount);
        Assert.Equal(before, Serialize(input));
        Assert.False(faction["materialization"]!["capabilities"]!["canTrade"]!.GetValue<bool>());
        Assert.DoesNotContain("Элиар", WoundApplicationService.ActionFor(new(quote.WoundId, quote.WoundFingerprint)
            { Service = new(quote) }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("historical_broken")] [InlineData("historical_dissolved")] [InlineData("invalid_lifecycle")]
    [InlineData("missing_lifecycle")] [InlineData("malformed_lifecycle")] [InlineData("hidden_faction")]
    [InlineData("incomplete_faction")] [InlineData("duplicate_faction")] [InlineData("foreign_faction_hall")]
    [InlineData("foreign_member")] [InlineData("not_ascended")] [InlineData("wrong_role")] [InlineData("duplicate_resident")]
    [InlineData("duplicate_profile")] [InlineData("duplicate_realm")] [InlineData("foreign_actor")]
    [InlineData("hidden_actor")] [InlineData("dead_actor")] [InlineData("missing_life")] [InlineData("incomplete_actor")]
    [InlineData("missing_service")] [InlineData("unavailable")] [InlineData("conditions")] [InlineData("relationship")]
    [InlineData("noncurrency")] [InlineData("foreign_actor_hall")] [InlineData("wrong_tier")]
    public void RoleOrUnprovedCurrentServiceNeverBecomesAnExecutableProvider(string change)
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var faction = input.Shining["factions"]![0]!.AsObject();
        var resident = input.Residents["entries"]![0]!.AsObject();
        var actor = input.Profiles["profiles"]![0]!.AsObject();
        switch (change)
        {
            case "historical_broken": faction["factionLifecycle"]!["state"] = "broken"; break;
            case "historical_dissolved": faction["factionLifecycle"]!["state"] = "dissolved"; break;
            case "invalid_lifecycle": faction["factionLifecycle"]!["state"] = "unknown"; break;
            case "missing_lifecycle": faction.Remove("factionLifecycle"); break;
            case "malformed_lifecycle": faction["factionLifecycle"] = true; break;
            case "hidden_faction": faction["visibility"] = "hidden"; break;
            case "incomplete_faction": faction["materialization"]!["state"] = "pending"; break;
            case "duplicate_faction": input.Shining["factions"]!.AsArray().Add(faction.DeepClone()); break;
            case "foreign_faction_hall": faction["hallId"] = "hall_other"; break;
            case "foreign_member": resident["shiningFactionId"] = "faction_other"; break;
            case "not_ascended": resident["ascensionState"] = "resident"; break;
            case "wrong_role": resident["residentRole"] = "archive_support"; break;
            case "duplicate_resident": input.Residents["entries"]!.AsArray().Add(resident.DeepClone()); break;
            case "duplicate_profile": input.Profiles["profiles"]!.AsArray().Add(actor.DeepClone()); break;
            case "duplicate_realm": var other = actor.DeepClone(); other["realm"] = "Chaos Sea"; input.Profiles["profiles"]!.AsArray().Add(other); break;
            case "foreign_actor": actor["actorId"] = "resident_other"; break;
            case "hidden_actor": actor["isPlayerVisible"] = false; break;
            case "dead_actor": actor["lifeState"] = "dead"; break;
            case "missing_life": actor.Remove("lifeState"); break;
            case "incomplete_actor": actor["materialization"]!["state"] = "pending"; break;
            case "missing_service": actor.Remove("healingServiceProfile"); break;
            case "unavailable": actor["healingServiceProfile"]!["availability"] = "unavailable"; break;
            case "conditions": actor["healingServiceProfile"]!["accessConditions"] = new JsonArray("Нужно разрешение"); break;
            case "relationship": actor["healingServiceProfile"]!["minimumRelationship"] = 10; break;
            case "noncurrency": actor["healingServiceProfile"]!["compensationKinds"] = new JsonArray("favor"); break;
            case "foreign_actor_hall": actor["locationId"] = "hall_other"; actor["healingServiceProfile"]!["locationRef"] = "hall_other"; break;
            case "wrong_tier": actor["standardArts"]!["spiritual_healing"] = 0; break;
        }
        var before = Serialize(input);
        Assert.Empty(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, input.Profiles));
        Assert.Equal(before, Serialize(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExactProviderTupleDetectsRoleOrMembershipDriftEvenWhenLocalitySetsStayTheSame(bool membership)
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var first = input.Shining["factions"]![0]!.AsObject();
        var second = first.DeepClone().AsObject(); second["factionId"] = "faction_second";
        second["materialization"]!["factionId"] = "faction_second";
        second["materialization"]!["materializationId"] = "fmat_second";
        input.Shining["factions"]!.AsArray().Add(second);
        var service = Assert.Single(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, input.Profiles));
        var quote = Quote(service.Profile, input.Profiles["profiles"]![0]!.AsObject());
        var beforeScope = service.Scope;
        if (membership) input.Residents["entries"]![0]!["shiningFactionId"] = "faction_second";
        else input.Residents["entries"]![0]!["residentRole"] = "archive_support";
        var afterScope = ShiningCurrentHallContract.RequireCurrentScope(input.Shining, input.Residents);
        Assert.Equal(beforeScope.LocalActorIds.OrderBy(x => x), afterScope.LocalActorIds.OrderBy(x => x));
        Assert.Equal(beforeScope.LocalFactionIds.OrderBy(x => x), afterScope.LocalFactionIds.OrderBy(x => x));
        if (membership)
            Assert.NotEqual(service.AdmissionFingerprint, AfterlifeWoundTreatmentAuthority.RequireServiceAdmission(
                null, input.Profiles, quote, input.Shining, input.Residents).ProviderFingerprint);
        else Assert.Throws<InvalidDataException>(() => AfterlifeWoundTreatmentAuthority.RequireServiceAdmission(
                null, input.Profiles, quote, input.Shining, input.Residents));
    }

    [Fact]
    public void OwnerSpecificDependenciesKeepExistingElyaraOrderAndRequireTheFactionRoster()
    {
        var elyara = AfterlifeHealingServiceTestData.Quote();
        Assert.Equal(AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths,
            AfterlifeWoundTreatmentAuthority.ServiceLocalityPathsFor(elyara));
        var input = ShiningFactionHealingServiceTestData.Create();
        var service = Assert.Single(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, input.Profiles));
        var quote = Quote(service.Profile, input.Profiles["profiles"]![0]!.AsObject());
        Assert.Equal(new[] { GuardianAbodeResidentState.StatePath }, AfterlifeWoundTreatmentAuthority.ServiceLocalityPathsFor(quote));
        Assert.Throws<InvalidDataException>(() => AfterlifeWoundTreatmentAuthority.RequireServiceAdmission(null,
            input.Profiles, quote, input.Shining, residents: null));
        Assert.Equal(elyara, AfterlifeHealingServiceContract.ParseQuote(elyara.ToJson()));
    }

    private static AfterlifeHealingServiceQuote Quote(AfterlifeHealingServiceProfile profile, JsonObject actor)
    {
        var node = WoundContractTestData.CreateSpiritualActiveWound(realm: "shining_abode", ownerId: "player_soul",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var wound = WoundMaterializationContract.Parse(node.ToJsonString(), "faction-service.quote");
        Assert.True(wound.IsValid);
        var check = SpiritualHealingResolver.PrepareSource(new(wound.Wound!, profile.Provider, actor, null,
            new EffectMechanicsSnapshot(true, [], [], [])));
        Assert.True(check.IsValid);
        return Assert.IsType<AfterlifeHealingServiceQuote>(AfterlifeHealingServiceContract.CreateQuote(profile, wound.Wound!, check.Source!, profile.LocationRef));
    }

    private static string Serialize(ShiningFactionServiceInputs input) => JsonSerializer.Serialize(input);
}
