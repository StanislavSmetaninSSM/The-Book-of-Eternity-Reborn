using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentProviderParticipationTests
{
    /// <summary>Explicit provider consideration is detached proposal DATA, never player confirmation or a new debt ledger.</summary>
    [Theory]
    [InlineData("guardian", 2, "provider_offer")]
    [InlineData("resident", 4, "resident_provider_offer")]
    public void ProviderOfferRetainsClosedExplicitNoncurrencyConsideration(string ownerKind, int version, string kind)
    {
        var previous = ProviderProfiles();
        var profile = previous["profiles"]![0]!.AsObject();
        profile["actorType"] = ownerKind;
        profile["actorId"] = ownerKind + "_negotiated";
        var proposal = ProviderProposal();
        proposal["schemaVersion"] = version; proposal["kind"] = kind;
        var current = new JsonObject { ["afterlifeEntityProfileUpdates"] = new JsonArray(new JsonObject
        {
            ["actorType"] = ownerKind, ["actorRef"] = profile["actorId"]!.DeepClone(),
            [SpiritualTreatmentParticipation.ChangeProperty] = proposal
        }) };
        Assert.Single(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        var terms = new JsonObject
        {
            ["kind"] = "favor", ["agreementRef"] = "agreement_healer_archive",
            ["readableTerms"] = "Помочь целителю перенести архив после принятой попытки лечения."
        };
        proposal["compensation"] = terms;
        var before = previous.ToJsonString() + current.ToJsonString();
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        Assert.True(JsonNode.DeepEquals(terms, change.Proposal["compensation"]));
        change.Proposal["compensation"]!["readableTerms"] = "Изменена только экспортированная копия.";
        Assert.Equal(before, previous.ToJsonString() + current.ToJsonString());
    }

    /// <summary>Shining provider locality has its own closed coordinates, rather than Guardian/Abode aliases.</summary>
    [Theory]
    [InlineData("faction")] [InlineData("elyara")]
    public void ShiningProviderLocalityRoundTripsItsExactClosedVariant(string variant)
    {
        var chaos = new JsonObject { ["guardianId"] = "guardian_aid", ["abodeId"] = "abode_aid" };
        Assert.True(JsonNode.DeepEquals(chaos, SpiritualTreatmentParticipation.LocalityJson(
            SpiritualTreatmentParticipation.ReadLocality(chaos))));
        var input = variant == "faction"
            ? new JsonObject { ["factionId"] = "faction_healer_archive", ["hallId"] = "hall_healer_archive" }
            : new JsonObject { ["kind"] = "elyara_shining_hall", ["hallId"] = "hall_elyara_companion" };
        var before = input.ToJsonString();
        var parsed = SpiritualTreatmentParticipation.ReadLocality(input);
        var exported = SpiritualTreatmentParticipation.LocalityJson(parsed);
        Assert.True(JsonNode.DeepEquals(input, exported));
        exported["hallId"] = "hall_foreign";
        Assert.Equal(before, input.ToJsonString());
        Assert.True(JsonNode.DeepEquals(input, SpiritualTreatmentParticipation.LocalityJson(parsed)));
    }
}
