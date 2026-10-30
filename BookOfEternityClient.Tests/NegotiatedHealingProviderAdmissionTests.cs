using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NegotiatedHealingProviderAdmissionTests
{
    [Theory]
    [InlineData(1)] [InlineData(20)]
    public void ExactFactionOfferNeedsCurrentHealerButNoPublicCurrencyProfile(int population)
    {
        var input = ShiningFactionHealingServiceTestData.Create(population);
        var actor = input.Profiles["profiles"]![0]!.AsObject(); actor.Remove("healingServiceProfile");
        // Unselected actors are not revalidated by one selected-provider DATA call.
        foreach (var other in input.Profiles["profiles"]!.AsArray().OfType<JsonObject>().Skip(1)) other.Remove("materialization");
        var original = JsonSerializer.Serialize(input);
        var admission = Admit(input);
        Assert.Equal(ShiningFactionHealingServiceTestData.HallId, admission.Scope.LocationId);
        Assert.Equal(Locality, admission.Locality);
        Assert.Equal(admission.Fingerprint, Admit(input).Fingerprint);
        Assert.Equal(original, JsonSerializer.Serialize(input));
        input.Residents["thoughtJournal"]!.AsArray().Add(new JsonObject { ["entryId"] = "thought_other_domain" });
        Assert.Equal(admission.Fingerprint, Admit(input).Fingerprint);
    }

    [Theory]
    [InlineData("foreign_role")] [InlineData("foreign_member")] [InlineData("not_ascended")]
    [InlineData("dead")] [InlineData("hidden")] [InlineData("hidden_faction")]
    [InlineData("broken")] [InlineData("dissolved")] [InlineData("foreign_hall")]
    [InlineData("duplicate_faction")] [InlineData("duplicate_resident")] [InlineData("duplicate_actor")]
    [InlineData("missing_roster")]
    public void NegotiatedAdmissionRefusesCurrentTupleDriftWithoutMutation(string change)
    {
        var input = ShiningFactionHealingServiceTestData.Create(); Assert.NotNull(Admit(input));
        var row = input.Residents["entries"]![0]!.AsObject();
        var actor = input.Profiles["profiles"]![0]!.AsObject();
        var faction = input.Shining["factions"]![0]!.AsObject();
        switch (change)
        {
            case "foreign_role": row["residentRole"] = "archive_support"; break;
            case "foreign_member": row["shiningFactionId"] = "faction_other"; break;
            case "not_ascended": row["ascensionState"] = "resident"; break;
            case "dead": actor["lifeState"] = "dead"; break;
            case "hidden": actor["isPlayerVisible"] = false; break;
            case "hidden_faction": faction["visibility"] = "hidden"; break;
            case "broken": faction["factionLifecycle"]!["state"] = "broken"; break;
            case "dissolved": faction["factionLifecycle"]!["state"] = "dissolved"; break;
            case "foreign_hall": input.Shining["currentHallId"] = "hall_foreign"; break;
            case "duplicate_faction": input.Shining["factions"]!.AsArray().Add(faction.DeepClone()); break;
            case "duplicate_resident": input.Residents["entries"]!.AsArray().Add(row.DeepClone()); break;
            case "duplicate_actor": input.Profiles["profiles"]!.AsArray().Add(actor.DeepClone()); break;
        }
        var before = JsonSerializer.Serialize(input);
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.RequireShiningProviderAdmission(null,
            change == "missing_roster" ? null : input.Residents, input.Profiles, input.Shining, Owner, Locality));
        Assert.Equal(before, JsonSerializer.Serialize(input));
    }

    private static readonly WoundOwnerCoordinate Owner = new("shining_abode", "resident", "resident_public_healer_0", WoundCarrierCatalog.AfterlifeProfilesPath);
    private static readonly SpiritualAidLocality Locality = SpiritualAidLocality.ForFaction(ShiningFactionHealingServiceTestData.FactionId, ShiningFactionHealingServiceTestData.HallId);
    private static SpiritualProviderAdmission Admit(ShiningFactionServiceInputs input) =>
        SpiritualTreatmentParticipation.RequireShiningProviderAdmission(null, input.Residents, input.Profiles, input.Shining, Owner, Locality);
}
