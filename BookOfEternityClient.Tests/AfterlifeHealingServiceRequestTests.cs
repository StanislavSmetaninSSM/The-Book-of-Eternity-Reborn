using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeHealingServiceRequestTests
{
    [Fact]
    public void PublicServiceSelectionRoundTripsExactQuoteWithoutPrivateAidGrant()
    {
        var quote = AfterlifeHealingServiceTestData.Quote();
        var root = new JsonObject
        {
            ["schemaVersion"] = 7, ["mode"] = "service", ["woundId"] = quote.WoundId,
            ["expectedWoundFingerprint"] = quote.WoundFingerprint, ["quote"] = quote.ToJson(),
            ["confirmation"] = "required"
        };
        var parsed = SpiritualSelfTreatmentSelection.Parse(root);
        Assert.True(JsonNode.DeepEquals(root, parsed.ToJson()));
        Assert.Null(parsed.Aid);
        Assert.Null(parsed.Combat);
        Assert.Null(root["participationFingerprint"]);
    }

    [Fact]
    public void RetainedServiceRequestBindsRealCheckExactWalletAndDrawWithoutPaying()
    {
        var (context, selection, data, soul) = RequestData();
        var before = soul.ToJsonString();
        var request = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selection, data, [14]);
        Assert.True(SpiritualSelfTreatmentRequestCodec.IsBoundActive(request, context, selection, "chaos_sea", 2));
        Assert.True(SpiritualSelfTreatmentRequestCodec.IsExactActive(request, context, selection, data));
        Assert.Equal(selection, SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request));
        Assert.Equal(7, request["schemaVersion"]!.GetValue<int>());
        Assert.Equal(50, request["payment"]!["amount"]!.GetValue<int>());
        Assert.Equal(500, request["payment"]!["walletBefore"]!["current"]!.GetValue<int>());
        Assert.Equal(450, request["payment"]!["walletAfter"]!["current"]!.GetValue<int>());
        var projection = SpiritualHealingRequestProjection.Create(data, request);
        Assert.Equal("service", projection["mode"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(data.ServiceQuote!.ToJson(), projection["quote"]));
        Assert.False(projection.ContainsKey("payment")); // GM receives terms/check, never permission to write a wallet receipt.
        Assert.Equal(before, soul.ToJsonString());
        var changedWallet = AfterlifeHealingServicePlanner.PreparePayment(data.ServiceQuote!, data.ServicePayment!.AttemptId,
            AfterlifeHealingServiceTestData.Soul(current: 501));
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsExactActive(request, context, selection, data with { ServicePayment = changedWallet }));
    }

    [Theory]
    [InlineData("quote")][InlineData("payment")][InlineData("wallet")][InlineData("attempt")]
    [InlineData("turn")][InlineData("confirmation")][InlineData("healer")][InlineData("target")]
    [InlineData("roll")][InlineData("source")][InlineData("extra")][InlineData("family")]
    public void AlteredClosedServiceRequestNeverBinds(string change)
    {
        var (context, selection, data, _) = RequestData();
        var request = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selection, data, [14]);
        switch (change)
        {
            case "quote": request["quote"]!["amount"] = 1; break;
            case "payment": request["payment"]!["paymentFingerprint"] = "sha256:" + new string('f', 64); break;
            case "wallet": request["payment"]!["walletAfter"]!["current"] = 499; break;
            case "attempt": request["attempt"]!["attemptId"] = "foreign_attempt"; break;
            case "turn": request["turn"] = context.Turn + 1; break;
            case "confirmation": request["playerConfirmation"] = "required"; break;
            case "healer": request["healer"]!["ownerId"] = "foreign_healer"; break;
            case "target": request["target"]!["ownerId"] = "foreign_patient"; break;
            case "roll": request["attempt"]!["roll"]!["values"] = new JsonArray(20); break;
            case "source": request["activeSource"]!["healingTier"] = 4; break;
            case "extra": request["participation"] = new JsonObject(); break;
            case "family": request["schemaVersion"] = 4; break;
        }
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundActive(request, context, selection, "chaos_sea", 2));
    }

    [Theory]
    [InlineData("unconfirmed")][InlineData("aid")][InlineData("combat")][InlineData("payment")]
    public void ServiceCreationCannotMixFamiliesOrSkipConfirmation(string change)
    {
        var (context, selection, data, _) = RequestData();
        if (change == "unconfirmed") selection = selection with { Service = selection.Service! with { PlayerConfirmed = false } };
        if (change == "aid") selection = selection with { Aid = new("healer", "player_soul", "sha256:" + new string('a', 64)) };
        if (change == "combat") selection = selection with { Combat = new("conflict", "sha256:" + new string('a', 64), 1) };
        if (change == "payment") data = data with { ServicePayment = null };
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentRequestCodec.CreateActive(context, selection, data, [14]));
    }

    // Real pure resolver/wound inputs; synthetic source digest is comparison
    // DATA only and cannot supply an original snapshot, lease or accepted payment.
    private static (SpiritualSelfTreatmentStageContext Context, SpiritualSelfTreatmentSelection Selection,
        AfterlifeWoundTreatmentAuthority.SourceData Data, JsonObject Soul) RequestData()
    {
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", WoundContractTestData.CreateSpiritualActiveWound(
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath));
        var catalog = WoundCarrierCatalog.Build(input.Carriers); var wound = Assert.Single(catalog.Occurrences).Wound;
        var actor = AfterlifeHealingServiceTestData.Actor();
        var profile = AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(AfterlifeHealingServiceTestData.Provider(), actor)!;
        var check = SpiritualHealingResolver.PrepareSource(new(wound, profile.Provider, actor, null, input.Effects)).Source!;
        var quote = AfterlifeHealingServiceContract.CreateQuote(profile, wound, check, profile.LocationRef)!;
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, quote.WoundFingerprint) { Service = new(quote, true) };
        var context = new SpiritualSelfTreatmentStageContext("session_service", "request_service", 44,
            new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 2 });
        var digest = "sha256:" + new string('b', 64); var soul = AfterlifeHealingServiceTestData.Soul();
        var payment = AfterlifeHealingServicePlanner.PreparePayment(quote,
            SpiritualSelfTreatmentRequestCodec.ServiceAttemptId(context, quote, "chaos_sea", 2, digest), soul)!;
        var effects = EffectCarrierCatalog.Build(new(null, null, input.Carriers.EnemyCombatants, input.Carriers.AllyCombatants, input.Carriers.AfterlifeProfiles, null));
        Assert.Empty(effects.Issues);
        var data = new AfterlifeWoundTreatmentAuthority.SourceData(wound, check, "chaos_sea", 2, digest,
            input.Carriers, effects, input.Identities, input.History, catalog, [new(wound, 0)], ServiceQuote: quote, ServicePayment: payment);
        return (context, selection, data, soul);
    }
}
