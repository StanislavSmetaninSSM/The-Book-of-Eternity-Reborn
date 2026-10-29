using System.Globalization;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentProviderParticipationTests
{
    /// <summary>The existing aid codec retains one confirmed resident draw and separate helper/patient tiers.</summary>
    [Fact]
    public void ProviderRequestRetainsConfirmedRolesDrawAndIndependentNaturalTier()
    {
        var (context, selected, data) = ProviderRequestData();
        var request = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14]);
        Assert.Equal(6, request["schemaVersion"]!.GetValue<int>());
        Assert.Equal("resident_to_player", request["direction"]!.GetValue<string>());
        Assert.Equal("player_target_confirmed", request["playerConfirmation"]!.GetValue<string>());
        Assert.True(SpiritualSelfTreatmentRequestCodec.IsExactActive(request, context, selected, data));
        Assert.Equal(selected, SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request));
        Assert.True(JsonNode.DeepEquals(OwnerRoot(Provider), request["healer"]));
        Assert.True(JsonNode.DeepEquals(OwnerRoot(Patient), request["target"]));
        var projection = SpiritualHealingRequestProjection.Create(data, request);
        Assert.Equal("resident_to_player", projection["direction"]!.GetValue<string>());
        Assert.True(projection["playerConfirmationVerified"]!.GetValue<bool>());
        Assert.Equal(3, projection["healingTier"]!.GetValue<int>());
        Assert.Equal(0, projection["targetNaturalHealingTier"]!.GetValue<int>());
        Assert.Equal(WoundApplicationService.ResidentReceiveAidActionText, WoundApplicationService.ActionFor(selected));
        var frozen = request.ToJsonString();
        projection["roll"]!["values"]![0] = 1;
        request["participation"]!["record"]!["readableEvidence"] = "Изменена отделённая команда.";
        Assert.Equal("Житель предлагает бесплатную помощь; принять её может только игрок.", data.AidWitness!["record"]!["readableEvidence"]!.GetValue<string>());
        Assert.Equal(frozen, SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14]).ToJsonString());
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentRequestCodec.CreateActive(context,
            selected with { Aid = selected.Aid! with { PlayerConfirmed = false } }, data, [14]));
    }

    /// <summary>Refingerprinting cannot authorize contradictory headers, stale eligibility or absent confirmation.</summary>
    [Theory]
    [InlineData("direction")]
    [InlineData("confirmation")]
    [InlineData("missing_confirmation")]
    [InlineData("version")]
    [InlineData("self")]
    [InlineData("healer")]
    [InlineData("target")]
    [InlineData("extra")]
    [InlineData("foreign_turn")]
    [InlineData("foreign_session")]
    [InlineData("revoked")]
    public void ProviderRequestRefusesRehashedMixedOrUnavailableAuthority(string scenario)
    {
        var (context, selected, data) = ProviderRequestData();
        var request = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14]);
        switch (scenario)
        {
            case "direction": request["direction"] = "guardian_to_player"; break;
            case "confirmation": request["playerConfirmation"] = "gm_confirmed"; break;
            case "missing_confirmation": request.Remove("playerConfirmation"); break;
            case "version": request["schemaVersion"] = 4; break;
            case "self": request["kind"] = "spiritual_self_treatment_request"; request["mode"] = "self"; break;
            case "healer": request["healer"]!["ownerKind"] = "guardian"; break;
            case "target": request["target"]!["ownerKind"] = "resident"; break;
            case "extra": request["consent"] = true; break;
            case "foreign_turn": context = context with { Turn = 45 }; break;
            case "foreign_session": context = context with { SessionId = "session_other" }; break;
            case "revoked": request["participation"]!["record"]!["status"] = "revoked"; break;
        }
        // Recompute the documented outer digest. A stale digest alone must not be the refusal.
        var copy = request.DeepClone().AsObject(); copy.Remove("requestFingerprint");
        request["requestFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "spiritual_treatment_request", request["schemaVersion"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture), WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy) });
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsExactActive(request, context, selected, data));
    }

    /// <summary>A freshly generated request still cannot use revoked, expired or intervening-turn offers.</summary>
    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("intervening_turn")]
    public void ProviderRequestFactoryRefusesUnavailableOfferBeforeRetainingADraw(string scenario)
    {
        var (context, selected, data) = ProviderRequestData();
        if (scenario == "revoked")
        {
            var record = data.AidWitness!["record"]!.DeepClone().AsObject(); record["status"] = "revoked";
            data = data with { AidWitness = new JsonObject { ["record"] = record,
                ["contentFingerprint"] = SpiritualTreatmentParticipation.Fingerprint(record),
                ["grantResultFingerprint"] = "sha256:" + new string('c', 64) } };
            selected = selected with { Aid = SpiritualTreatmentParticipation.Selection(record) with { PlayerConfirmed = true } };
        }
        else if (scenario == "expired") { context = context with { Turn = 45 }; data = data with { CycleOrdinal = 3 }; }
        else context = context with { Turn = 45 }; // Even the same eligible safe-cycle ordinal cannot bridge a turn.
        Assert.Contains("unbound", Assert.Throws<InvalidDataException>(() =>
            SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14])).Message);
    }

    private static (SpiritualSelfTreatmentStageContext Context, SpiritualSelfTreatmentSelection Selection,
        AfterlifeWoundTreatmentAuthority.SourceData Data) ProviderRequestData()
    {
        // Pure codec DATA only. Synthetic witness digest is not an accepted grant or live permission.
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", WoundContractTestData.CreateSpiritualActiveWound(
            "wound_player_aid", "chaos_sea", "player_soul", "player_soul", Patient.CarrierPath));
        var catalog = WoundCarrierCatalog.Build(input.Carriers); var wound = Assert.Single(catalog.Occurrences).Wound;
        var effects = EffectCarrierCatalog.Build(new(null, null, input.Carriers.EnemyCombatants, input.Carriers.AllyCombatants, input.Carriers.AfterlifeProfiles, null));
        Assert.Empty(effects.Issues);
        var record = ProviderRecord();
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
            { Aid = SpiritualTreatmentParticipation.Selection(record) with { PlayerConfirmed = true } };
        var source = new SpiritualHealingPreparedSource(wound.WoundId, Patient, Provider, wound.Severity.Rank, 3, 0, "normal", []);
        var data = new AfterlifeWoundTreatmentAuthority.SourceData(wound, source, "chaos_sea", 2, "sha256:" + new string('b', 64),
            input.Carriers, effects, input.Identities, input.History, catalog, [new(wound, 0)], new JsonObject
            { ["record"] = record, ["contentFingerprint"] = SpiritualTreatmentParticipation.Fingerprint(record), ["grantResultFingerprint"] = "sha256:" + new string('c', 64) });
        return (new("session_provider", "request_aid", 44, new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 2 }), selection, data);
    }
}
