using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Resident participation DATA uses the existing profile admission path.</summary>
public sealed partial class SpiritualResidentTreatmentParticipationTests
{
    /// <summary>A resident may grant exact player treatment without becoming a Guardian.</summary>
    [Fact]
    public void ResidentConsentUsesTheActualExistingProfileProposalReader()
    {
        // Detached preexisting profile DATA, not a grant receipt or live locality
        // authority. The actual accepted producer and guided callers qualify later.
        var previous = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["profiles"] = new JsonArray(new JsonObject
            {
                ["actorType"] = "resident", ["actorId"] = "resident_aid",
                ["realm"] = "Chaos Sea", ["locationId"] = "abode_aid"
            })
        };
        var proposal = new JsonObject
        {
            ["schemaVersion"] = 3, ["kind"] = "resident_target_consent",
            ["operation"] = "grant", ["healerId"] = "player_soul",
            ["woundId"] = "wound_resident_aid", ["conscious"] = true, ["capable"] = true,
            ["readableEvidence"] = "Житель осознанно разрешает душе лечить эту рану в следующий безопасный цикл."
        };
        var current = new JsonObject
        {
            ["afterlifeEntityProfileUpdates"] = new JsonArray(new JsonObject
            {
                ["actorType"] = "resident", ["actorRef"] = "resident_aid",
                [SpiritualTreatmentParticipation.ChangeProperty] = proposal
            })
        };
        var before = previous.ToJsonString();
        var response = current.ToJsonString();
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        Assert.Equal("resident_aid", change.TargetId);
        Assert.True(JsonNode.DeepEquals(proposal, change.Proposal));
        Assert.Equal(before, previous.ToJsonString());
        Assert.Equal(response, current.ToJsonString());
    }
}
