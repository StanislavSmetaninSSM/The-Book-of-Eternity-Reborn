using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

// Pure typed dispatch/composed-image DATA. No live grant, owner or permission.
public sealed class ShiningPatientProjectedAdmissionTests
{
    [Theory]
    [InlineData("resident")][InlineData("radiant_actor")]
    public void PlayerToPatientProjectionUsesExactH0TargetInsteadOfProviderRules(string kind)
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs(kind);
        var record = PatientRecord(owner, locality); var before = record.ToJsonString() + input.Profiles.ToJsonString() + input.Shining.ToJsonString() + input.Residents.ToJsonString();
        Assert.Equal(0, input.Profiles["profiles"]![0]!["standardArts"]!["spiritual_healing"]!.GetValue<int>());
        var actual = SpiritualTreatmentParticipation.RequireProjectedShiningAidAdmission(record, null, input.Residents, input.Profiles, input.Shining);
        Assert.True(actual.IsPatient); Assert.Equal(owner, actual.Actor); Assert.Equal(locality, actual.Locality);
        Assert.NotEqual(SpiritualTreatmentParticipation.RecordHealer(record), actual.Actor);
        SpiritualTreatmentParticipation.RequireSameProjectedShiningPatientAdmission(actual, actual);
        Assert.Equal(before, record.ToJsonString() + input.Profiles.ToJsonString() + input.Shining.ToJsonString() + input.Residents.ToJsonString());
    }

    [Theory]
    [InlineData("faction")][InlineData("elyara")]
    public void ReceivingProviderProjectionKeepsItsOwnTypedHealerAndAdmission(string kind)
    {
        var (record, guardians, input, owner, locality) = Provider(kind);
        var expected = SpiritualTreatmentParticipation.RequireShiningProviderAdmission(guardians, input.Residents, input.Profiles, input.Shining, owner, locality);
        var actual = SpiritualTreatmentParticipation.RequireProjectedShiningAidAdmission(record, guardians, input.Residents, input.Profiles, input.Shining);
        Assert.False(actual.IsPatient); Assert.Equal(owner, actual.Actor); Assert.Equal(locality, actual.Locality); Assert.Equal(expected.Fingerprint, actual.Fingerprint);
        Assert.Equal(owner, SpiritualTreatmentParticipation.RecordHealer(record));
    }

    [Theory]
    [InlineData("resident", "membership")][InlineData("resident", "hall")][InlineData("resident", "lifecycle")]
    [InlineData("resident", "identity")][InlineData("resident", "missing_binding")]
    [InlineData("radiant_actor", "head")][InlineData("radiant_actor", "hall")][InlineData("radiant_actor", "lifecycle")]
    [InlineData("radiant_actor", "identity")][InlineData("radiant_actor", "missing_binding")]
    public void ActualComposedPatientTupleMustStillEqualItsTrustedProjection(string kind, string change)
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs(kind);
        var record = PatientRecord(owner, locality);
        var expected = SpiritualTreatmentParticipation.RequireProjectedShiningAidAdmission(record, null, input.Residents, input.Profiles, input.Shining);
        switch (change)
        {
            case "membership": input.Residents["entries"]![0]!["shiningFactionId"] = "foreign_faction"; break;
            case "head": input.Shining["factions"]![0]!["leadership"]!["headActorId"] = "foreign_head"; break;
            case "hall": input.Shining["currentHallId"] = "foreign_hall"; break;
            case "lifecycle": input.Shining["factions"]![0]!["factionLifecycle"]!["state"] = "weakened"; break;
            case "identity": input.Profiles["profiles"]![0]!["actorId"] = "foreign_actor"; break;
        }
        var before = input.Profiles.ToJsonString() + input.Residents.ToJsonString() + input.Shining.ToJsonString();
        Assert.Throws<InvalidDataException>(() =>
        {
            var actual = SpiritualTreatmentParticipation.RequireProjectedShiningAidAdmission(record, null, input.Residents, input.Profiles, input.Shining);
            if (change == "lifecycle") Assert.NotEqual(expected.Fingerprint, actual.Fingerprint); // Two lawful tuples, not an invalid baseline.
            SpiritualTreatmentParticipation.RequireSameProjectedShiningPatientAdmission(change == "missing_binding" ? null : expected, actual);
        });
        Assert.Equal(before, input.Profiles.ToJsonString() + input.Residents.ToJsonString() + input.Shining.ToJsonString());
    }

    [Fact]
    public void ProviderProjectionCannotReplacePatientBindingEvenWithAnEqualFingerprint()
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs("resident");
        var expected = SpiritualTreatmentParticipation.RequireProjectedShiningAidAdmission(PatientRecord(owner, locality), null, input.Residents, input.Profiles, input.Shining);
        var (record, guardians, provided, _, _) = Provider("faction");
        var provider = SpiritualTreatmentParticipation.RequireProjectedShiningAidAdmission(record, guardians, provided.Residents, provided.Profiles, provided.Shining);
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.RequireSameProjectedShiningPatientAdmission(expected,
            provider with { Fingerprint = expected.Fingerprint }));
    }

    private static JsonObject PatientRecord(WoundOwnerCoordinate owner, SpiritualAidLocality locality) =>
        SpiritualTreatmentParticipation.CreateOutcome(new(owner, new JsonObject
        {
            ["schemaVersion"] = 5, ["kind"] = "shining_target_consent", ["operation"] = "grant", ["healerId"] = "player_soul",
            ["woundId"] = "wound_projected_patient", ["locality"] = SpiritualTreatmentParticipation.LocalityJson(locality),
            ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Я согласен на отдельную попытку лечения."
        }), locality, Binding(), 1, "transition_projected_patient")["record"]!.DeepClone().AsObject();

    private static (JsonObject Record, JsonObject? Guardians, ShiningFactionServiceInputs Input, WoundOwnerCoordinate Owner, SpiritualAidLocality Locality) Provider(string kind)
    {
        var input = ShiningFactionHealingServiceTestData.Create(); JsonObject? guardians = null;
        var owner = new WoundOwnerCoordinate("shining_abode", "resident", "resident_public_healer_0", WoundCarrierCatalog.AfterlifeProfilesPath);
        var locality = SpiritualAidLocality.ForFaction(ShiningFactionHealingServiceTestData.FactionId, ShiningFactionHealingServiceTestData.HallId);
        if (kind == "elyara")
        {
            owner = new("shining_abode", "guardian", AfterlifeWoundTreatmentAuthority.ElyaraId, WoundCarrierCatalog.AfterlifeProfilesPath);
            locality = SpiritualAidLocality.ForElyara(ShiningFactionHealingServiceTestData.HallId);
            var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", owner.OwnerId, "Shining Abode", 42);
            actor["lifeState"] = "alive"; actor["visibility"] = "public";
            actor["locationId"] = locality.LocationId; actor["locationName"] = "Зал исцеления"; actor["standardArts"]!["spiritual_healing"] = 5;
            actor["healingServiceProfile"] = AfterlifeHealingServiceTestData.Actor()["healingServiceProfile"]!.DeepClone();
            actor["healingServiceProfile"]!["realm"] = "shining_abode"; actor["healingServiceProfile"]!["locationRef"] = locality.LocationId;
            input = input with { Profiles = new JsonObject { ["profiles"] = new JsonArray(actor) } };
            guardians = new JsonObject { ["guardians"] = new JsonArray(new JsonObject { ["guardianId"] = owner.OwnerId }) };
        }
        var proposal = new JsonObject
        {
            ["schemaVersion"] = kind == "elyara" ? 2 : 4, ["kind"] = kind == "elyara" ? "provider_offer" : "resident_provider_offer", ["operation"] = "offer",
            ["targetId"] = "player_soul", ["woundId"] = "wound_projected_player", ["locality"] = SpiritualTreatmentParticipation.LocalityJson(locality),
            ["providerConscious"] = true, ["providerCapable"] = true, ["targetConscious"] = true, ["targetCapable"] = true,
            ["readableEvidence"] = "Я предлагаю помощь; душа подтверждает её отдельно."
        };
        var record = SpiritualTreatmentParticipation.CreateOutcome(new(owner, proposal), locality, Binding(), 1, "transition_projected_provider")["record"]!.DeepClone().AsObject();
        return (record, guardians, input, owner, locality);
    }
    private static JsonObject Binding() => new() { ["sessionId"] = "session_projected", ["requestId"] = "request_projected",
        ["snapshotToken"] = "snapshot_projected", ["realm"] = "shining_abode", ["turn"] = 43 };
}
