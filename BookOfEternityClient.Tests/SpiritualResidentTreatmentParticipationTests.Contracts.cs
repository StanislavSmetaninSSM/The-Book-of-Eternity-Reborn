using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentTreatmentParticipationTests
{
    private static readonly WoundOwnerCoordinate ResidentOwner = new("chaos_sea", "resident", "resident_aid", WoundCarrierCatalog.AfterlifeProfilesPath);
    private static readonly SpiritualAidLocality ResidentLocality = new("guardian_aid", "abode_aid");

    [Theory]
    [InlineData("profiles")]
    [InlineData("afterlifeEntityProfiles")]
    [InlineData("afterlifeEntityProfileUpdates")]
    public void EveryResidentRepresentationRetainsTheFullTypedOwnerAndOneMirroredIntent(string property)
    {
        var previous = ResidentProfiles();
        var row = ResidentResponse();
        var response = new JsonObject { [property] = new JsonArray(row) };
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        Assert.Equal(ResidentOwner, change.Owner);
        foreach (var mirror in new[] { "profiles", "afterlifeEntityProfiles", "afterlifeEntityProfileUpdates" })
            response[mirror] = new JsonArray(row.DeepClone());
        Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        response["profiles"]![0]![SpiritualTreatmentParticipation.ChangeProperty]!["readableEvidence"] = "Иное согласие.";
        Assert.Contains("conflicting", Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(response, previous)).Message);
    }

    [Theory]
    [InlineData("profiles", false)]
    [InlineData("profiles", true)]
    [InlineData("afterlifeEntityProfiles", false)]
    [InlineData("afterlifeEntityProfiles", true)]
    [InlineData("afterlifeEntityProfileUpdates", false)]
    [InlineData("afterlifeEntityProfileUpdates", true)]
    public void RawCanonicalResidentRecordCannotBeRewrittenOrNulled(string property, bool explicitNull)
    {
        var previous = ResidentProfiles();
        previous["profiles"]![0]![SpiritualTreatmentParticipation.RecordProperty] = ResidentRecord();
        var row = previous["profiles"]![0]!.DeepClone().AsObject();
        var response = new JsonObject { [property] = new JsonArray(row) };
        Assert.Empty(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        if (explicitNull) row[SpiritualTreatmentParticipation.RecordProperty] = null;
        else row[SpiritualTreatmentParticipation.RecordProperty]!["readableEvidence"] = "Подменённое согласие.";
        Assert.Contains("cannot mint or change", Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(response, previous)).Message);
    }

    [Fact]
    public void EqualIdsAcrossGuardianAndResidentKindsAreLegalButAreTwoDistinctChanges()
    {
        var previous = ResidentProfiles();
        previous["profiles"]!.AsArray().Add(new JsonObject
        { ["actorType"] = "guardian", ["actorId"] = "resident_aid", ["realm"] = "Chaos Sea" });
        var response = new JsonObject { ["profiles"] = new JsonArray(ResidentResponse()) };
        Assert.Equal(ResidentOwner, Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous)).Owner);
        var guardian = ResidentResponse(); guardian["actorType"] = "guardian";
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(
            new JsonObject { ["profiles"] = new JsonArray(guardian.DeepClone()) }, previous));
        guardian[SpiritualTreatmentParticipation.ChangeProperty]!["schemaVersion"] = 1;
        guardian[SpiritualTreatmentParticipation.ChangeProperty]!.AsObject().Remove("kind");
        response["profiles"]!.AsArray().Add(guardian);
        Assert.Contains("one distinct typed", Assert.Throws<InvalidDataException>(() =>
            SpiritualTreatmentParticipation.ReadChanges(response, previous)).Message);
    }

    [Theory]
    [InlineData("grant", false, true, false)]
    [InlineData("grant", true, false, false)]
    [InlineData("revoke", false, false, true)]
    public void RevocationWithLostCapacityIsValidButNeverAnAvailableGrant(string operation, bool conscious, bool capable, bool valid)
    {
        var proposal = ResidentProposal(); proposal["operation"] = operation;
        proposal["conscious"] = conscious; proposal["capable"] = capable;
        var response = new JsonObject { ["profiles"] = new JsonArray(ResidentResponse(proposal)) };
        if (!valid)
        {
            Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(response, ResidentProfiles()));
            return;
        }
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, ResidentProfiles()));
        var record = CreateResidentOutcome(change)["record"]!.AsObject();
        SpiritualTreatmentParticipation.ValidateRecord(record);
        Assert.Equal("revoked", record["status"]!.GetValue<string>());
        Assert.False(SpiritualTreatmentParticipation.IsAvailable(record));
    }

    [Fact]
    public void ResidentSelectionCarriesClosedParticipantsLocalityAndNoGuardianConfirmationFlags()
    {
        var record = ResidentRecord();
        var selection = new SpiritualSelfTreatmentSelection("wound_resident_aid", "sha256:" + new string('a', 64))
            { Aid = SpiritualTreatmentParticipation.Selection(record) };
        var root = selection.ToJson();
        Assert.Equal(5, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("player_to_resident", root["direction"]!.GetValue<string>());
        Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(root));
        Assert.Equal(ResidentOwner, selection.Aid!.Target("chaos_sea"));
        Assert.Equal(ResidentLocality, selection.Aid.Resident!.Locality);
        Assert.Equal(WoundApplicationService.ResidentAidActionText, WoundApplicationService.ActionFor(selection));
        Assert.Throws<InvalidDataException>(() => (selection with { Aid = selection.Aid with { ReceiveAid = true } }).ToJson());
        Assert.Throws<InvalidDataException>(() => (selection with { Aid = selection.Aid with { PlayerConfirmed = true } }).ToJson());
        foreach (var mutation in new[] { "kind", "realm", "carrier", "direction", "unknown", "missing", "unknown_version", "self_mode" })
        {
            var forged = root.DeepClone().AsObject();
            switch (mutation)
            {
                case "kind": forged["target"]!["ownerKind"] = "guardian"; break;
                case "realm": forged["target"]!["realm"] = "shining_abode"; break;
                case "carrier": forged["target"]!["carrierPath"] = "game_state/characters/npc.json"; break;
                case "direction": forged["direction"] = "guardian_to_player"; break;
                case "unknown": forged["confirmation"] = "player_target_confirmed"; break;
                case "unknown_version": forged["schemaVersion"] = 99; break;
                case "self_mode": forged["mode"] = "self"; break;
                default: forged.Remove("locality"); break;
            }
            Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(forged));
        }
        foreach (var receive in new[] { false, true })
        {
            var aid = new SpiritualAidSelection(receive ? "guardian_aid" : "player_soul", receive ? "player_soul" : "guardian_aid",
                "sha256:" + new string('b', 64)) { ReceiveAid = receive };
            var old = selection with { Aid = aid };
            var expected = "{\"schemaVersion\":" + (receive ? "4" : "3") + ",\"mode\":\"" + (receive ? "receive_aid" : "aid") +
                "\",\"woundId\":\"wound_resident_aid\",\"expectedWoundFingerprint\":\"sha256:" + new string('a', 64) +
                "\",\"healerId\":\"" + aid.HealerId + "\",\"targetId\":\"" + aid.TargetId +
                "\",\"participationFingerprint\":\"sha256:" + new string('b', 64) + "\"" +
                (receive ? ",\"confirmation\":\"required\"}" : "}");
            Assert.Equal(expected, old.ToJson().ToJsonString());
            Assert.Equal(old, SpiritualSelfTreatmentSelection.Parse(old.ToJson()));
        }
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("same_id_guardian_present")]
    [InlineData("wrong_kind")]
    [InlineData("wrong_realm")]
    [InlineData("wrong_carrier")]
    [InlineData("duplicate_roster")]
    [InlineData("confusable_roster")]
    [InlineData("confusable_profile")]
    [InlineData("foreign_guardian")]
    [InlineData("foreign_abode")]
    [InlineData("departed")]
    [InlineData("missing_presence")]
    [InlineData("hidden_profile")]
    [InlineData("hidden_wound")]
    [InlineData("profile_alias_conflict")]
    [InlineData("roster_alias_conflict")]
    public void ResidentAuthorityRequiresTheExactTypedRosterProfileWoundAndCurrentAbode(string scenario)
    {
        var profiles = ResidentProfiles(); var resident = profiles["profiles"]![0]!.AsObject();
        var guardians = GuardianLocality(); var residents = ResidentRoster(); var roster = residents["entries"]![0]!.AsObject();
        var woundJson = WoundContractTestData.CreateSpiritualActiveWound("wound_resident_aid", "chaos_sea", "resident",
            "resident_aid", WoundCarrierCatalog.AfterlifeProfilesPath);
        var target = ResidentOwner;
        switch (scenario)
        {
            case "same_id_guardian_present": profiles["profiles"]!.AsArray().Add(new JsonObject
                { ["actorType"] = "guardian", ["actorId"] = "resident_aid", ["realm"] = "Chaos Sea" }); break;
            case "wrong_kind": resident["actorType"] = "guardian"; break;
            case "wrong_realm": resident["realm"] = "Shining Abode"; break;
            case "wrong_carrier": target = target with { CarrierPath = "game_state/characters/npc.json" }; break;
            case "duplicate_roster": residents["entries"]!.AsArray().Add(roster.DeepClone()); break;
            case "confusable_roster": var duplicate = roster.DeepClone(); duplicate["residentId"] = "RESIDENT_AID"; residents["entries"]!.AsArray().Add(duplicate); break;
            case "confusable_profile": var alias = resident.DeepClone(); alias["actorId"] = "RESIDENT_AID"; profiles["profiles"]!.AsArray().Add(alias); break;
            case "foreign_guardian": roster["guardianId"] = "GUARDIAN_AID"; break;
            case "foreign_abode": roster["abodeId"] = "elsewhere"; break;
            case "departed": roster["isPresent"] = false; break;
            case "missing_presence": roster.Remove("isPresent"); break;
            case "hidden_profile": resident["isHidden"] = true; break;
            case "hidden_wound": woundJson["display"]!["visibility"] = "gm_only"; break;
            case "profile_alias_conflict": resident["actorRef"] = "resident_other"; break;
            case "roster_alias_conflict": roster["actorRef"] = "resident_other"; break;
        }
        var parsed = WoundMaterializationContract.Parse(woundJson.ToJsonString(), "resident.join.wound");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Actual)));
        var before = profiles.ToJsonString() + guardians.ToJsonString() + residents.ToJsonString();
        if (scenario is "valid" or "same_id_guardian_present")
        {
            var scope = SpiritualTreatmentParticipation.RequireResidentScope(guardians, residents, profiles, target, parsed.Wound!);
            Assert.Equal("abode_aid", scope.LocationId); Assert.Equal("guardian_aid", scope.CurrentGuardianId);
        }
        else Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.RequireResidentScope(guardians, residents, profiles, target, parsed.Wound!));
        Assert.Equal(before, profiles.ToJsonString() + guardians.ToJsonString() + residents.ToJsonString());
    }

    private static JsonObject ResidentProposal() => new()
    {
        ["schemaVersion"] = 3, ["kind"] = "resident_target_consent", ["operation"] = "grant",
        ["healerId"] = "player_soul", ["woundId"] = "wound_resident_aid", ["conscious"] = true, ["capable"] = true,
        ["readableEvidence"] = "Житель осознанно согласен на помощь этой души в следующий безопасный цикл."
    };
    private static JsonObject ResidentResponse(JsonObject? proposal = null) => new()
    {
        ["actorType"] = "resident", ["actorRef"] = "resident_aid",
        [SpiritualTreatmentParticipation.ChangeProperty] = proposal ?? ResidentProposal()
    };
    private static JsonObject ResidentProfiles() => new() { ["profiles"] = new JsonArray(new JsonObject
    { ["actorType"] = "resident", ["actorId"] = "resident_aid", ["realm"] = "Chaos Sea", ["locationId"] = "abode_aid" }) };
    private static JsonObject CreateResidentOutcome(SpiritualTreatmentParticipation.Change change) =>
        SpiritualTreatmentParticipation.CreateOutcome(change, ResidentLocality, new JsonObject
        { ["sessionId"] = "session_aid", ["requestId"] = "request_grant", ["snapshotToken"] = "snapshot_aid", ["realm"] = "chaos_sea", ["turn"] = 42 }, 1, "transition_aid");
    private static JsonObject ResidentRecord() => CreateResidentOutcome(new(ResidentOwner, ResidentProposal()))["record"]!.DeepClone().AsObject();
    private static JsonObject GuardianLocality()
    {
        var guardian = new JsonObject { ["guardianId"] = "guardian_aid", ["canonicalName"] = "Хранитель", ["abode"] = new JsonObject
            { ["abodeId"] = "abode_aid", ["name"] = "Обитель" } };
        return new() { ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = "guardian_aid", ["currentAbodeId"] = "abode_aid" } };
    }
    private static JsonObject ResidentRoster() => new() { ["entries"] = new JsonArray(new JsonObject
    { ["residentId"] = "resident_aid", ["guardianId"] = "guardian_aid", ["abodeId"] = "abode_aid", ["isPresent"] = true }) };
}
