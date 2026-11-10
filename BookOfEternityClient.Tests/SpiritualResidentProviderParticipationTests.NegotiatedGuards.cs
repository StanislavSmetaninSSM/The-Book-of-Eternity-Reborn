using System.Globalization;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentProviderParticipationTests
{
    [Theory]
    [InlineData("favor")] [InlineData("debt")] [InlineData("quest")] [InlineData("allegiance")] [InlineData("free_aid")]
    public void NegotiatedTermsBindTheSameConfirmedRequestAndRetainedDraw(string kind)
    {
        var (context, selected, data) = ProviderRequestData();
        var record = data.AidWitness!["record"]!.DeepClone().AsObject();
        record["compensation"] = Terms(kind);
        selected = selected with { Aid = SpiritualTreatmentParticipation.Selection(record) with { PlayerConfirmed = true } };
        data = data with { AidWitness = new JsonObject { ["record"] = record,
            ["contentFingerprint"] = SpiritualTreatmentParticipation.Fingerprint(record),
            ["grantResultFingerprint"] = "sha256:" + new string('c', 64) },
            NegotiatedWalletInvariant = SpiritualTreatmentWalletInvariant.Capture(new JsonObject()) };
        var request = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14]);
        Assert.True(SpiritualSelfTreatmentRequestCodec.IsExactActive(request, context, selected, data));
        Assert.Equal(selected, SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request));
        Assert.Equal(14, request["attempt"]!["roll"]!["values"]![0]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(Terms(kind), SpiritualHealingRequestProjection.Create(data, request)["compensation"]));
        Assert.Contains("без списания Чернильных Перьев", WoundApplicationService.ActionFor(selected));
        Assert.DoesNotContain("бесплат", WoundApplicationService.ActionFor(selected));
        Assert.Contains("Перенести архив", WoundApplicationService.ConfirmationFor(selected));
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected,
            data with { NegotiatedWalletInvariant = null }, [14]));
        var originalId = request["attempt"]!["attemptId"]!.GetValue<string>();
        var forged = request.DeepClone().AsObject();
        forged["currencyInvariant"] = SpiritualTreatmentWalletInvariant.Capture(new JsonObject { ["inkFeathers"] = new JsonObject() }).ToJson();
        RehashNegotiatedRequest(forged);
        Assert.Equal(originalId, forged["attempt"]!["attemptId"]!.GetValue<string>());
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundAid(forged, context, selected, data.Realm, data.CycleOrdinal));
        var fabricatedData = data with { NegotiatedWalletInvariant = SpiritualTreatmentWalletInvariant.Capture(
            new JsonObject { ["inkFeathers"] = new JsonObject { ["total"] = 99 } }) };
        var fabricatedRequest = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, fabricatedData, [14]);
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsExactActive(fabricatedRequest, context, selected, data));
        Assert.False(SpiritualTreatmentParticipation.MatchesSelection(record, selected.Aid! with
            { Compensation = selected.Aid!.Compensation! with { ReadableTerms = "Иное обязательство." } }));
    }

    [Theory]
    [InlineData("null")] [InlineData("currency")] [InlineData("missing_kind")]
    [InlineData("missing_ref")] [InlineData("missing_terms")] [InlineData("blank_terms")] [InlineData("extra")]
    public void NegotiatedConsiderationRejectsNullCurrencyAndIncompleteTerms(string mutation)
    {
        var proposal = ProviderProposal(); proposal["compensation"] = Terms("favor");
        switch (mutation)
        {
            case "null": proposal["compensation"] = null; break;
            case "currency": proposal["compensation"]!["kind"] = "ink_feathers"; break;
            case "missing_kind": proposal["compensation"]!.AsObject().Remove("kind"); break;
            case "missing_ref": proposal["compensation"]!.AsObject().Remove("agreementRef"); break;
            case "missing_terms": proposal["compensation"]!.AsObject().Remove("readableTerms"); break;
            case "blank_terms": proposal["compensation"]!["readableTerms"] = " "; break;
            default: proposal["compensation"]!["amount"] = 1; break;
        }
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadCompensation(proposal));
    }

    [Theory]
    [InlineData("faction")] [InlineData("elyara")]
    public void ShiningProviderSelectionRetainsExactOwnerLocalityAndTerms(string variant)
    {
        var resident = variant == "faction";
        var owner = new WoundOwnerCoordinate("shining_abode", resident ? "resident" : "guardian",
            resident ? "resident_negotiated" : AfterlifeWoundTreatmentAuthority.ElyaraId, WoundCarrierCatalog.AfterlifeProfilesPath);
        var locality = resident ? SpiritualAidLocality.ForFaction("faction_negotiated", "hall_negotiated")
            : SpiritualAidLocality.ForElyara("hall_negotiated");
        var proposal = ProviderProposal(); proposal["schemaVersion"] = resident ? 4 : 2;
        proposal["kind"] = resident ? "resident_provider_offer" : "provider_offer";
        proposal["locality"] = SpiritualTreatmentParticipation.LocalityJson(locality); proposal["compensation"] = Terms("debt");
        var binding = ProviderBinding(); binding["realm"] = "shining_abode";
        var record = SpiritualTreatmentParticipation.CreateOutcome(new(owner, proposal), locality, binding, 1, "transition_negotiated")["record"]!.AsObject();
        SpiritualTreatmentParticipation.ValidateRecord(record);
        var aid = SpiritualTreatmentParticipation.Selection(record);
        Assert.Equal(owner, aid.Healer("shining_abode"));
        Assert.Equal(locality, aid.ExplicitLocality); Assert.Throws<InvalidDataException>(() => aid.GuardianId);
        Assert.True(SpiritualTreatmentParticipation.MatchesSelection(record, aid));
        var selection = new SpiritualSelfTreatmentSelection("wound_player_aid", "sha256:" + new string('a', 64)) { Aid = aid };
        Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(selection.ToJson()));
        var wrongHall = selection.ToJson(); wrongHall["locality"]!["hallId"] = "hall_foreign";
        var parsed = SpiritualSelfTreatmentSelection.Parse(wrongHall);
        Assert.False(SpiritualTreatmentParticipation.MatchesSelection(record, parsed.Aid!));
        var mixed = selection.ToJson(); mixed["locality"]!["guardianId"] = "guardian_fake";
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(mixed));
    }

    [Theory]
    [InlineData("absent")] [InlineData("null")] [InlineData("empty")]
    [InlineData("zero")] [InlineData("extra_fields")]
    public void NegotiatedWalletPreservesPresenceAndEveryOriginalField(string shape)
    {
        var original = new JsonObject { ["currentRealm"] = "Shining Abode" };
        switch (shape)
        {
            case "null": original["inkFeathers"] = null; break;
            case "empty": original["inkFeathers"] = new JsonObject(); break;
            case "zero": original["inkFeathers"] = new JsonObject { ["total"] = 0 }; break;
            case "extra_fields": original["inkFeathers"] = new JsonObject { ["total"] = 12, ["metadata"] = new JsonArray("retained", 9) }; break;
        }
        var invariant = SpiritualTreatmentWalletInvariant.Capture(original);
        var exported = invariant.ToJson();
        Assert.Equal(invariant, SpiritualTreatmentWalletInvariant.Parse(exported));
        var proposed = original.DeepClone().AsObject(); proposed["ordinaryOtherDomain"] = true;
        invariant.RequirePreserved(proposed);
        exported["walletBefore"]!["present"] = !original.ContainsKey("inkFeathers");
        invariant.RequirePreserved(original); // Export mutation never changes retained DATA.
        proposed["inkFeathers"] = new JsonObject { ["total"] = 999 };
        Assert.Throws<InvalidDataException>(() => invariant.RequirePreserved(proposed));
        Assert.Equal(shape != "absent", original.ContainsKey("inkFeathers"));
    }

    [Fact]
    public void NegotiatedSourceDependenciesPreserveChaosOrderWithoutDuplicatingShiningState()
    {
        Assert.Equal(AfterlifeWoundTreatmentAuthority.AidLocalityPaths,
            AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(Locality));
        var faction = SpiritualAidLocality.ForFaction("faction_negotiated", "hall_negotiated");
        var elyara = SpiritualAidLocality.ForElyara("hall_negotiated");
        Assert.Equal(new[] { GuardianAbodeResidentState.StatePath }, AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(faction));
        Assert.Equal(new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0] }, AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(elyara));
        foreach (var locality in new[] { faction, elyara })
        {
            var paths = AfterlifeWoundTreatmentAuthority.SourcePaths.Concat(AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(locality)).ToArray();
            Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
            Assert.Single(paths, p => p == ShiningAbodeState.StatePath);
        }
    }

    private static JsonObject Terms(string kind) => new()
    { ["kind"] = kind, ["agreementRef"] = "agreement_healer_archive", ["readableTerms"] = "Перенести архив целителя после принятой попытки лечения." };

    private static void RehashNegotiatedRequest(JsonObject request)
    {
        var copy = request.DeepClone().AsObject(); copy.Remove("requestFingerprint");
        request["requestFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "spiritual_treatment_request", request["schemaVersion"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture), WoundAcceptedTurnFingerprintWriter.CanonicalJson(copy) });
    }
}
