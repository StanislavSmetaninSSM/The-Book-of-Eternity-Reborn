using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Private resident offers retain the actual typed healer and player patient.</summary>
public sealed partial class SpiritualResidentProviderParticipationTests
{
    /// <summary>The existing proposal reader admits the new closed resident provider intent.</summary>
    [Fact]
    public void ResidentProviderOfferUsesTypedActorAndPlayerPatient()
    {
        // This is detached proposal DATA, not an accepted offer, current locality
        // authority or permission to heal. Genuine producer/callers qualify later.
        var previous = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["profiles"] = new JsonArray(
                new JsonObject { ["actorType"] = "player_soul", ["actorId"] = "player_soul", ["realm"] = "Chaos Sea" },
                new JsonObject
                {
                    ["actorType"] = "resident", ["actorId"] = "resident_provider",
                    ["realm"] = "Chaos Sea", ["locationId"] = "abode_aid"
                })
        };
        var proposal = new JsonObject
        {
            ["schemaVersion"] = 4, ["kind"] = "resident_provider_offer", ["operation"] = "offer",
            ["targetId"] = "player_soul", ["woundId"] = "wound_player_aid",
            ["providerConscious"] = true, ["providerCapable"] = true,
            ["targetConscious"] = true, ["targetCapable"] = true,
            ["readableEvidence"] = "Житель добровольно предлагает бесплатную помощь; принять её может только игрок."
        };
        var current = new JsonObject
        {
            ["afterlifeEntityProfileUpdates"] = new JsonArray(new JsonObject
            {
                ["actorType"] = "resident", ["actorRef"] = "resident_provider",
                [SpiritualTreatmentParticipation.ChangeProperty] = proposal
            })
        };
        var before = previous.ToJsonString();
        var response = current.ToJsonString();
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        Assert.Equal(new WoundOwnerCoordinate("chaos_sea", "resident", "resident_provider", WoundCarrierCatalog.AfterlifeProfilesPath), change.Owner);
        Assert.Equal("player_soul", change.Proposal["targetId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(proposal, change.Proposal));
        change.Proposal["readableEvidence"] = "Изменена только возвращённая отделённая копия.";
        Assert.Equal(before, previous.ToJsonString());
        Assert.Equal(response, current.ToJsonString());
    }
}
