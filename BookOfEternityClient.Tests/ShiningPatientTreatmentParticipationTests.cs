using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ShiningPatientTreatmentParticipationTests
{
    // Missing explicit Shining patient dispatch must fail before any live authority is involved.
    [Theory]
    [InlineData("resident")]
    [InlineData("radiant_actor")]
    public void ExistingProfileReaderAcceptsExactShiningPatientProposalWithoutMutation(string kind)
    {
        const string patientId = "shining_patient";
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, patientId, "Shining Abode", 42);
        profile["locationId"] = "hall_patient";
        var previous = new JsonObject { ["schemaVersion"] = 1, ["profiles"] = new JsonArray(profile) };
        var proposal = new JsonObject
        {
            ["schemaVersion"] = 5, ["kind"] = "shining_target_consent", ["operation"] = "grant",
            ["healerId"] = "player_soul", ["woundId"] = "wound_shining_patient",
            ["locality"] = new JsonObject { ["factionId"] = "faction_patient", ["hallId"] = "hall_patient" },
            ["conscious"] = true, ["capable"] = true,
            ["readableEvidence"] = "Пациент осознанно разрешил игроку лечить известную рану в следующий безопасный цикл."
        };
        var current = new JsonObject
        {
            ["afterlifeEntityProfileUpdates"] = new JsonArray(new JsonObject
            {
                ["actorType"] = kind, ["actorRef"] = patientId,
                [SpiritualTreatmentParticipation.ChangeProperty] = proposal
            })
        };
        var before = previous.ToJsonString(); var response = current.ToJsonString();
        IReadOnlyList<SpiritualTreatmentParticipation.Change>? changes = null;
        var refusal = Record.Exception(() => changes = SpiritualTreatmentParticipation.ReadChanges(current, previous));
        Assert.Null(refusal);
        var change = Assert.Single(changes!);
        Assert.Equal(new WoundOwnerCoordinate("shining_abode", kind, patientId, WoundCarrierCatalog.AfterlifeProfilesPath), change.Owner);
        Assert.True(JsonNode.DeepEquals(proposal, change.Proposal));
        Assert.Equal(before, previous.ToJsonString()); Assert.Equal(response, current.ToJsonString());
    }
}
