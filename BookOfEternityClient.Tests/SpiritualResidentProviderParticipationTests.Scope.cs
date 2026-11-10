using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentProviderParticipationTests
{
    /// <summary>The resident healer joins live locality separately from the player's wound.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("same_id_guardian_present")]
    [InlineData("wrong_kind")]
    [InlineData("wrong_realm")]
    [InlineData("duplicate_roster")]
    [InlineData("confusable_profile")]
    [InlineData("foreign_guardian")]
    [InlineData("foreign_abode")]
    [InlineData("departed")]
    [InlineData("missing_presence")]
    [InlineData("hidden_provider")]
    [InlineData("hidden_patient_wound")]
    [InlineData("foreign_patient")]
    [InlineData("profile_alias_conflict")]
    [InlineData("roster_alias_conflict")]
    public void ProviderAuthorityJoinsTheResidentHealerAndSeparatePlayerWound(string scenario)
    {
        var profiles = ProviderProfiles(); var profile = profiles["profiles"]![0]!.AsObject();
        var guardian = new JsonObject { ["guardianId"] = Locality.GuardianId, ["canonicalName"] = "Хранитель",
            ["abode"] = new JsonObject { ["abodeId"] = Locality.AbodeId, ["name"] = "Обитель" } };
        var guardians = new JsonObject { ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = Locality.GuardianId, ["currentAbodeId"] = Locality.AbodeId } };
        var entry = new JsonObject { ["residentId"] = Provider.OwnerId, ["guardianId"] = Locality.GuardianId,
            ["abodeId"] = Locality.AbodeId, ["isPresent"] = true };
        var residents = new JsonObject { ["entries"] = new JsonArray(entry) };
        var woundJson = WoundContractTestData.CreateSpiritualActiveWound("wound_player_aid", "chaos_sea", "player_soul", "player_soul", Patient.CarrierPath);
        switch (scenario)
        {
            case "same_id_guardian_present": profiles["profiles"]!.AsArray().Add(new JsonObject
                { ["actorType"] = "guardian", ["actorId"] = Provider.OwnerId, ["realm"] = "Chaos Sea" }); break;
            case "wrong_kind": profile["actorType"] = "guardian"; break;
            case "wrong_realm": profile["realm"] = "Shining Abode"; break;
            case "duplicate_roster": residents["entries"]!.AsArray().Add(entry.DeepClone()); break;
            case "confusable_profile": var alias = profile.DeepClone(); alias["actorId"] = "RESIDENT_PROVIDER"; profiles["profiles"]!.AsArray().Add(alias); break;
            case "foreign_guardian": entry["guardianId"] = "guardian_elsewhere"; break;
            case "foreign_abode": entry["abodeId"] = "abode_elsewhere"; break;
            case "departed": entry["isPresent"] = false; break;
            case "missing_presence": entry.Remove("isPresent"); break;
            case "hidden_provider": profile["isHidden"] = true; break;
            case "hidden_patient_wound": woundJson["display"]!["visibility"] = "gm_only"; break;
            case "foreign_patient": woundJson["owner"]!["ownerId"] = "another_soul"; break;
            case "profile_alias_conflict": profile["actorRef"] = "resident_other"; break;
            case "roster_alias_conflict": entry["actorRef"] = "resident_other"; break;
        }
        var wound = WoundMaterializationContract.Parse(woundJson.ToJsonString(), "provider.scope.wound");
        Assert.True(wound.IsValid, string.Join(";", wound.Issues));
        var before = profiles.ToJsonString() + guardians.ToJsonString() + residents.ToJsonString();
        var aid = SpiritualTreatmentParticipation.Selection(ProviderRecord());
        if (scenario is "valid" or "same_id_guardian_present")
        {
            var scope = SpiritualTreatmentParticipation.RequireAidScope(guardians, residents, profiles, aid, wound.Wound!);
            Assert.Equal(Locality.GuardianId, scope.CurrentGuardianId); Assert.Equal(Locality.AbodeId, scope.LocationId);
        }
        else Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.RequireAidScope(guardians, residents, profiles, aid, wound.Wound!));
        Assert.Equal(before, profiles.ToJsonString() + guardians.ToJsonString() + residents.ToJsonString());
    }

    /// <summary>Protected thought entries belong to the resident healer and never the player patient.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProviderMemoryUsesTheHealerOwnerInsteadOfThePatient(bool wrongPatientOwner)
    {
        var record = ProviderRecord();
        var commands = new JsonArray(new JsonObject
        {
            ["entryId"] = "provider_memory_44", ["residentId"] = wrongPatientOwner ? "player_soul" : Provider.OwnerId,
            ["turn"] = 44, ["timestamp"] = "2026-10-29T04:00:00Z", ["title"] = "Моя помощь душе",
            ["summary"] = "Я добровольно помогаю душе; решение принять помощь принадлежит ей."
        });
        var prior = new JsonObject(); var before = commands.ToJsonString();
        if (wrongPatientOwner) Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(
            commands, SpiritualTreatmentParticipation.RecordOwnerId(record), 44, prior));
        else
        {
            var admitted = SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(commands, SpiritualTreatmentParticipation.RecordOwnerId(record), 44, prior);
            Assert.Equal(Provider.OwnerId, Assert.Single(admitted)!["residentId"]!.GetValue<string>());
            admitted[0]!["summary"] = "Отделённый результат.";
        }
        Assert.Equal(before, commands.ToJsonString()); Assert.Empty(prior);
    }
}
