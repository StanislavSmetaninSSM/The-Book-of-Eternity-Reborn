using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ShiningPatientTreatmentAdmissionTests
{
    [Theory]
    [InlineData("resident")]
    [InlineData("radiant_actor")]
    public void ExactCurrentPatientNeedsMembershipOrHeadshipWithoutProviderArtRoleOrService(string kind)
    {
        var (input, owner, locality) = Inputs(kind);
        var before = JsonSerializer.Serialize(input);
        var admitted = SpiritualTreatmentParticipation.RequireShiningPatientAdmission(input.Shining, input.Profiles,
            kind == "resident" ? input.Residents : null, owner, locality);
        Assert.Equal(owner, admitted.Target); Assert.Equal(locality, admitted.Locality);
        Assert.Equal(kind == "radiant_actor", admitted.IsFactionHead);
        Assert.Equal(ShiningFactionHealingServiceTestData.HallId, admitted.Scope.LocationId);
        Assert.Equal(0, admitted.Profile["standardArts"]!["spiritual_healing"]!.GetValue<int>());
        Assert.False(admitted.Profile.ContainsKey("healingServiceProfile"));
        var selected = new SpiritualAidSelection("player_soul", owner.OwnerId, "sha256:" + new string('a', 64))
            { ShiningPatient = new(new("shining_abode", "player_soul", "player_soul", owner.CarrierPath), owner, locality) };
        Assert.Equal(kind == "resident" ? new[] { GuardianAbodeResidentState.StatePath } : [],
            AfterlifeWoundTreatmentAuthority.AidLocalityPathsFor(selected));
        Assert.Equal(before, JsonSerializer.Serialize(input));
    }

    [Theory]
    [InlineData("resident", "hidden")][InlineData("resident", "dead")][InlineData("resident", "profile_duplicate")]
    [InlineData("resident", "profile_case_alias")][InlineData("resident", "foreign_realm")][InlineData("resident", "incomplete")]
    [InlineData("resident", "foreign_hall")][InlineData("resident", "hidden_faction")][InlineData("resident", "broken")]
    [InlineData("resident", "missing_lifecycle")][InlineData("resident", "faction_duplicate")][InlineData("resident", "no_current_hall")]
    [InlineData("resident", "foreign_membership")][InlineData("resident", "not_ascended")][InlineData("resident", "resident_duplicate")]
    [InlineData("radiant_actor", "hidden")][InlineData("radiant_actor", "dead")][InlineData("radiant_actor", "profile_duplicate")]
    [InlineData("radiant_actor", "foreign_realm")][InlineData("radiant_actor", "foreign_hall")][InlineData("radiant_actor", "hidden_faction")]
    [InlineData("radiant_actor", "wrong_head")][InlineData("radiant_actor", "head_alias_kind")][InlineData("radiant_actor", "vacant")]
    [InlineData("radiant_actor", "political_missing")][InlineData("radiant_actor", "political_duplicate")]
    [InlineData("radiant_actor", "political_kind")][InlineData("radiant_actor", "political_faction")][InlineData("radiant_actor", "political_status")]
    public void OneInvalidCurrentDependencyCannotBecomeAVisiblePatient(string kind, string change)
    {
        var (input, owner, locality) = Inputs(kind);
        _ = SpiritualTreatmentParticipation.RequireShiningPatientAdmission(input.Shining, input.Profiles, input.Residents, owner, locality);
        var profile = input.Profiles["profiles"]![0]!.AsObject(); var faction = input.Shining["factions"]![0]!.AsObject();
        switch (change)
        {
            case "hidden": profile["isPlayerVisible"] = false; break;
            case "dead": profile["lifeState"] = "dead"; break;
            case "profile_duplicate": input.Profiles["profiles"]!.AsArray().Add(profile.DeepClone()); break;
            case "profile_case_alias": var alias = profile.DeepClone(); alias["actorId"] = owner.OwnerId.ToUpperInvariant(); input.Profiles["profiles"]!.AsArray().Add(alias); break;
            case "foreign_realm": profile["realm"] = "Chaos Sea"; break;
            case "incomplete": profile["materialization"]!["state"] = "pending"; break;
            case "foreign_hall": profile["locationId"] = "hall_other"; break;
            case "hidden_faction": faction["visibility"] = "hidden"; break;
            case "broken": faction["factionLifecycle"]!["state"] = "broken"; break;
            case "missing_lifecycle": faction.Remove("factionLifecycle"); break;
            case "faction_duplicate": input.Shining["factions"]!.AsArray().Add(faction.DeepClone()); break;
            case "no_current_hall": input.Shining.Remove("currentHallId"); break;
            case "foreign_membership": input.Residents["entries"]![0]!["shiningFactionId"] = "faction_other"; break;
            case "not_ascended": input.Residents["entries"]![0]!["ascensionState"] = "resident"; break;
            case "resident_duplicate": input.Residents["entries"]!.AsArray().Add(input.Residents["entries"]![0]!.DeepClone()); break;
            case "wrong_head": faction["leadership"]!["headActorId"] = "radiant_other"; break;
            case "head_alias_kind": faction["leadership"]!["headActorType"] = "shining_faction_head"; break;
            case "vacant": faction["leadership"]!["leadershipState"] = "vacant"; break;
            case "political_missing": input.Shining["shiningPoliticalActors"] = new JsonArray(); break;
            case "political_duplicate": input.Shining["shiningPoliticalActors"]!.AsArray().Add(input.Shining["shiningPoliticalActors"]![0]!.DeepClone()); break;
            case "political_kind": input.Shining["shiningPoliticalActors"]![0]!["actorType"] = "afterlife_actor"; break;
            case "political_faction": input.Shining["shiningPoliticalActors"]![0]!["currentFactionId"] = "faction_other"; break;
            case "political_status": input.Shining["shiningPoliticalActors"]![0]!["politicalStatus"] = "former_head"; break;
        }
        var frozen = JsonSerializer.Serialize(input);
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.RequireShiningPatientAdmission(
            input.Shining, input.Profiles, input.Residents, owner, locality));
        Assert.Empty(SpiritualTreatmentParticipation.ReadShiningPatientAdmissions(input.Shining, input.Profiles, input.Residents));
        Assert.Equal(frozen, JsonSerializer.Serialize(input));
    }

    [Theory]
    [InlineData("resident")][InlineData("radiant_actor")]
    public void ValidLifecycleChangeInvalidatesFrozenPatientBinding(string kind)
    {
        var (input, owner, locality) = Inputs(kind);
        var first = SpiritualTreatmentParticipation.RequireShiningPatientAdmission(input.Shining, input.Profiles, input.Residents, owner, locality);
        input.Shining["factions"]![0]!["factionLifecycle"]!["state"] = "weakened";
        var second = SpiritualTreatmentParticipation.RequireShiningPatientAdmission(input.Shining, input.Profiles, input.Residents, owner, locality);
        Assert.Equal(first.Scope.LocalActorIds.OrderBy(x => x), second.Scope.LocalActorIds.OrderBy(x => x));
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    internal static (ShiningFactionServiceInputs Input, WoundOwnerCoordinate Owner, SpiritualAidLocality Locality) Inputs(string kind)
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        const string id = "resident_public_healer_0";
        var profile = kind == "resident" ? input.Profiles["profiles"]![0]!.AsObject()
            : AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, id, "Shining Abode", 42);
        if (kind != "resident") input.Profiles["profiles"] = new JsonArray(profile);
        profile["lifeState"] = "alive"; profile["visibility"] = "public";
        profile["locationId"] = ShiningFactionHealingServiceTestData.HallId; profile["locationName"] = "Зал исцеления";
        profile["standardArts"]!["spiritual_healing"] = 0; profile.Remove("healingServiceProfile");
        input.Residents["entries"]![0]!["residentRole"] = "archive_support";
        if (kind == "radiant_actor")
        {
            input.Shining["factions"]![0]!["leadership"] = new JsonObject
            { ["leadershipState"] = "secure", ["headActorType"] = "radiant_actor", ["headActorId"] = id };
            input.Shining["shiningPoliticalActors"] = new JsonArray(new JsonObject
            { ["actorType"] = "radiant_actor", ["actorId"] = id, ["originFactionId"] = ShiningFactionHealingServiceTestData.FactionId,
                ["currentFactionId"] = ShiningFactionHealingServiceTestData.FactionId, ["politicalStatus"] = "head" });
        }
        return (input, new("shining_abode", kind, id, WoundCarrierCatalog.AfterlifeProfilesPath),
            SpiritualAidLocality.ForFaction(ShiningFactionHealingServiceTestData.FactionId, ShiningFactionHealingServiceTestData.HallId));
    }
}
