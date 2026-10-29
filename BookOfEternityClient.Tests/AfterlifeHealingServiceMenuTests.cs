using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeHealingServiceMenuTests
{
    [Fact]
    public void PublicQuoteUsesExistingReadContextWithoutGrantWritesOrPrivateUiBindings()
    {
        var (context, guardians) = Inputs();
        var before = context.Soul.ToJsonString() + context.Input.Carriers.AfterlifeProfiles!.ToJsonString() + guardians.ToJsonString();
        var choice = Assert.Single(WoundApplicationService.BuildPublicServiceChoices(context, guardians, []));
        Assert.True(choice.Available); Assert.Null(choice.Selection.Aid); Assert.False(choice.Selection.Service!.PlayerConfirmed);
        Assert.Equal(50, choice.Selection.Service.Quote.Amount);
        Assert.Equal(WoundApplicationService.ServiceActionText, WoundApplicationService.ActionFor(choice.Selection));
        Assert.Contains("50", WoundApplicationService.ConfirmationFor(choice.Selection));
        Assert.Contains("неудачу", choice.Reason);
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", context.Projection, new("generation_service", [choice]), treatment: true);
        var json = JsonSerializer.Serialize(result);
        foreach (var token in new[] { "guard_system_elyara_001", "wound_spiritual", "sha256:", "walletBefore", "quoteFingerprint", "carrierPath" })
            Assert.DoesNotContain(token, json);
        Assert.Equal(before, context.Soul.ToJsonString() + context.Input.Carriers.AfterlifeProfiles!.ToJsonString() + guardians.ToJsonString());
    }

    [Theory]
    [InlineData("dead")][InlineData("hidden")][InlineData("relocated")][InlineData("price")]
    [InlineData("tier")][InlineData("locality")][InlineData("conflict")][InlineData("shining")]
    public void FreshProviderOrLocalityLossRemovesExecutableService(string change)
    {
        var (context, guardians) = Inputs();
        var actor = context.Input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>().Single(p =>
            p["actorId"]?.GetValue<string>() == AfterlifeWoundTreatmentAuthority.ElyaraId);
        switch (change)
        {
            case "dead": actor["lifeState"] = "dead"; break;
            case "hidden": actor["isHidden"] = true; break;
            case "relocated": actor["locationId"] = "abode_elsewhere"; actor["healingServiceProfile"]!["locationRef"] = "abode_elsewhere"; break;
            case "price": actor["healingServiceProfile"]!["priceMultiplierPercent"] = 101; break;
            case "tier": actor["standardArts"]!["spiritual_healing"] = 4; break;
            case "locality": guardians["chaosSeaNavigation"]!["currentAbodeId"] = "abode_elsewhere"; break;
            case "conflict": context = context with { SpiritualConflict = new JsonObject { ["activeConflict"] = new JsonObject() } }; break;
            case "shining": context = context with { Input = context.Input with { Realm = "shining_abode" } }; break;
        }
        Assert.Empty(WoundApplicationService.BuildPublicServiceChoices(context, guardians, []));
    }

    [Fact]
    public void InsufficientFundsKeepsReadableQuoteWithoutExecutableChoiceOrMutation()
    {
        var (context, guardians) = Inputs(); context.Soul["inkFeathers"]!["current"] = 49;
        var before = context.Soul.ToJsonString();
        var choice = Assert.Single(WoundApplicationService.BuildPublicServiceChoices(context, guardians, []));
        Assert.False(choice.Available); Assert.Contains("Недостаточно", choice.Reason);
        Assert.Equal(50, choice.Selection.Service!.Quote.Amount); Assert.Equal(before, context.Soul.ToJsonString());
    }

    private static (WoundPlayerReadContext Context, JsonObject Guardians) Inputs()
    {
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", WoundContractTestData.CreateSpiritualActiveWound(
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath));
        var soul = AfterlifeHealingServiceTestData.Soul();
        var actor = AfterlifeHealingServiceTestData.Actor(); actor["activeWounds"] = new JsonArray(); actor["activeEffects"] = new JsonArray();
        input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().Add(actor);
        var guardian = new JsonObject { ["guardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId,
            ["abode"] = new JsonObject { ["abodeId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId } };
        var guardians = new JsonObject { ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId,
                ["currentAbodeId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId } };
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        return (new(input, catalog, soul, null, WoundPlayerProjection.Build(input, catalog)), guardians);
    }
}
