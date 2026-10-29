using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentProviderParticipationTests
{
    private static readonly WoundOwnerCoordinate Provider = new("chaos_sea", "resident", "resident_provider", WoundCarrierCatalog.AfterlifeProfilesPath);
    private static readonly WoundOwnerCoordinate Patient = new("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
    private static readonly SpiritualAidLocality Locality = new("guardian_aid", "abode_aid");

    /// <summary>All current raw representations retain one typed provider intent.</summary>
    [Theory]
    [InlineData("profiles")]
    [InlineData("afterlifeEntityProfiles")]
    [InlineData("afterlifeEntityProfileUpdates")]
    public void ProviderRepresentationsCoalesceOnlyTheSameTypedOffer(string property)
    {
        var previous = ProviderProfiles();
        var row = new JsonObject { ["actorType"] = "resident", ["actorRef"] = Provider.OwnerId,
            [SpiritualTreatmentParticipation.ChangeProperty] = ProviderProposal() };
        var response = new JsonObject { [property] = new JsonArray(row.DeepClone()) };
        Assert.Equal(Provider, Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous)).Owner);
        foreach (var mirror in new[] { "profiles", "afterlifeEntityProfiles", "afterlifeEntityProfileUpdates" })
            response[mirror] = new JsonArray(row.DeepClone());
        Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        response["profiles"]![0]![SpiritualTreatmentParticipation.ChangeProperty]!["readableEvidence"] = "Другое предложение.";
        Assert.Contains("conflicting", Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(response, previous)).Message);
    }

    /// <summary>The outcome records the resident healer rather than confusing it with the wound owner.</summary>
    [Fact]
    public void ProviderOutcomeUsesExactRolesAndDetachedCanonicalRecord()
    {
        var binding = ProviderBinding();
        var outcome = SpiritualTreatmentParticipation.CreateOutcome(new(Provider, ProviderProposal()), Locality, binding, 1, "transition_provider");
        var record = outcome["record"]!.AsObject();
        Assert.True(JsonNode.DeepEquals(ProviderRecord(), record));
        Assert.Equal(Provider, SpiritualTreatmentParticipation.RecordOwner(record));
        Assert.Equal(Patient, SpiritualTreatmentParticipation.RecordTarget(record));
        Assert.True(SpiritualTreatmentParticipation.IsProviderOffer(record));
        Assert.Equal(6, SpiritualTreatmentParticipation.ResultVersion(record));
        record["binding"]!["turn"] = 99;
        Assert.Equal(43, binding["turn"]!.GetValue<int>());
    }

    /// <summary>Raw profile data cannot replace or null a client-owned provider record.</summary>
    [Theory]
    [InlineData("profiles", false)]
    [InlineData("profiles", true)]
    [InlineData("afterlifeEntityProfiles", false)]
    [InlineData("afterlifeEntityProfiles", true)]
    [InlineData("afterlifeEntityProfileUpdates", false)]
    [InlineData("afterlifeEntityProfileUpdates", true)]
    public void RawProviderRecordCannotBeForged(string property, bool nullRecord)
    {
        var previous = ProviderProfiles();
        previous["profiles"]![0]![SpiritualTreatmentParticipation.RecordProperty] = ProviderRecord();
        var row = previous["profiles"]![0]!.DeepClone().AsObject();
        var current = new JsonObject { [property] = new JsonArray(row) };
        Assert.Empty(SpiritualTreatmentParticipation.ReadChanges(current, previous));
        if (nullRecord) row[SpiritualTreatmentParticipation.RecordProperty] = null;
        else row[SpiritualTreatmentParticipation.RecordProperty]!["readableEvidence"] = "Подмена предложения.";
        Assert.Contains("cannot mint or change", Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ReadChanges(current, previous)).Message);
    }

    /// <summary>Every offer fact is required and revocation never authorizes treatment.</summary>
    [Theory]
    [InlineData("providerConscious")]
    [InlineData("providerCapable")]
    [InlineData("targetConscious")]
    [InlineData("targetCapable")]
    public void ProviderFactsAreTypedAndRevocationIsUnavailable(string field)
    {
        var record = ProviderRecord(); record[field] = null;
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ValidateRecord(record));
        record[field] = false;
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ValidateRecord(record));
        record["status"] = "revoked";
        SpiritualTreatmentParticipation.ValidateRecord(record);
        Assert.False(SpiritualTreatmentParticipation.IsAvailable(record));
    }

    /// <summary>A patient must confirm this exact resident offer; the old target-resident family stays exact.</summary>
    [Fact]
    public void ProviderSelectionPreservesDirectionAndExplicitPlayerConfirmation()
    {
        var aid = SpiritualTreatmentParticipation.Selection(ProviderRecord());
        Assert.True(aid.ReceiveAid); Assert.False(aid.PlayerConfirmed);
        Assert.Equal(6, aid.FamilyVersion); Assert.Equal(Locality.GuardianId, aid.GuardianId);
        Assert.Equal(Provider, aid.Healer("chaos_sea")); Assert.Equal(Patient, aid.Target("chaos_sea"));
        var selected = new SpiritualSelfTreatmentSelection("wound_player_aid", "sha256:" + new string('a', 64)) { Aid = aid };
        foreach (var confirmed in new[] { false, true })
        {
            var current = selected with { Aid = aid with { PlayerConfirmed = confirmed } };
            var json = current.ToJson();
            Assert.Equal(6, json["schemaVersion"]!.GetValue<int>());
            Assert.Equal("receive_aid", json["mode"]!.GetValue<string>());
            Assert.Equal("resident_to_player", json["direction"]!.GetValue<string>());
            Assert.Equal(confirmed ? "player_target_confirmed" : "required", json["confirmation"]!.GetValue<string>());
            Assert.Equal(current, SpiritualSelfTreatmentSelection.Parse(json));
        }
        var old = selected with { Aid = new("player_soul", Provider.OwnerId, aid.ParticipationFingerprint)
            { Resident = new(Patient, Provider, Locality) } };
        var legacy = old.ToJson();
        Assert.Equal(5, legacy["schemaVersion"]!.GetValue<int>());
        Assert.Equal("player_to_resident", legacy["direction"]!.GetValue<string>());
        Assert.False(legacy.ContainsKey("confirmation"));
        Assert.Equal(old, SpiritualSelfTreatmentSelection.Parse(legacy));
    }

    /// <summary>Closed provider selections reject contradictory or incomplete coordinates and confirmation.</summary>
    [Theory]
    [InlineData("healer_kind")]
    [InlineData("patient")]
    [InlineData("realm")]
    [InlineData("carrier")]
    [InlineData("direction")]
    [InlineData("version")]
    [InlineData("missing_confirmation")]
    [InlineData("unknown_confirmation")]
    [InlineData("extra")]
    public void ProviderSelectionRefusesMixedFamilies(string scenario)
    {
        var selected = new SpiritualSelfTreatmentSelection("wound_player_aid", "sha256:" + new string('a', 64))
            { Aid = SpiritualTreatmentParticipation.Selection(ProviderRecord()) };
        var root = selected.ToJson();
        switch (scenario)
        {
            case "healer_kind": root["healer"]!["ownerKind"] = "guardian"; break;
            case "patient": root["target"]!["ownerId"] = "another_soul"; break;
            case "realm": root["healer"]!["realm"] = "shining_abode"; break;
            case "carrier": root["healer"]!["carrierPath"] = "game_state/player/wounds.json"; break;
            case "direction": root["direction"] = "player_to_resident"; break;
            case "version": root["schemaVersion"] = 5; break;
            case "missing_confirmation": root.Remove("confirmation"); break;
            case "unknown_confirmation": root["confirmation"] = "gm_confirmed"; break;
            default: root["healerId"] = Provider.OwnerId; break;
        }
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(root));
    }

    private static JsonObject ProviderProposal() => new()
    {
        ["schemaVersion"] = 4, ["kind"] = "resident_provider_offer", ["operation"] = "offer",
        ["targetId"] = "player_soul", ["woundId"] = "wound_player_aid",
        ["providerConscious"] = true, ["providerCapable"] = true, ["targetConscious"] = true, ["targetCapable"] = true,
        ["readableEvidence"] = "Житель предлагает бесплатную помощь; принять её может только игрок."
    };
    private static JsonObject ProviderProfiles() => new() { ["profiles"] = new JsonArray(new JsonObject
    { ["actorType"] = "resident", ["actorId"] = Provider.OwnerId, ["realm"] = "Chaos Sea", ["locationId"] = Locality.AbodeId }) };
    private static JsonObject ProviderBinding() => new()
    { ["sessionId"] = "session_provider", ["requestId"] = "request_offer", ["snapshotToken"] = "snapshot_provider", ["realm"] = "chaos_sea", ["turn"] = 43 };
    private static JsonObject OwnerRoot(WoundOwnerCoordinate owner) => new()
    { ["realm"] = owner.Realm, ["ownerKind"] = owner.OwnerKind, ["ownerId"] = owner.OwnerId, ["carrierPath"] = owner.CarrierPath };
    private static JsonObject ProviderRecord() => new()
    {
        ["schemaVersion"] = 4, ["kind"] = "resident_provider_offer", ["status"] = "offered",
        ["healer"] = OwnerRoot(Provider), ["target"] = OwnerRoot(Patient), ["woundId"] = "wound_player_aid",
        ["locality"] = new JsonObject { ["guardianId"] = Locality.GuardianId, ["abodeId"] = Locality.AbodeId },
        ["providerConscious"] = true, ["providerCapable"] = true, ["targetConscious"] = true, ["targetCapable"] = true,
        ["readableEvidence"] = "Житель предлагает бесплатную помощь; принять её может только игрок.",
        ["binding"] = ProviderBinding(), ["acceptedCycleOrdinal"] = 1, ["eligibleCycleOrdinal"] = 2,
        ["primaryTransitionId"] = "transition_provider"
    };
}
