using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Closed detached DATA contracts, never a live consent witness or publication authority.</summary>
public sealed class ShiningPatientTreatmentCodecTests
{
    [Theory]
    [InlineData("resident")]
    [InlineData("radiant_actor")]
    public void ExistingOutcomeFactoryProducesDistinctShiningRecordAndResultFamily(string kind)
    {
        var proposal = Proposal(); var before = proposal.ToJsonString();
        var binding = new JsonObject { ["sessionId"] = "session_patient", ["requestId"] = "turn_43",
            ["snapshotToken"] = "snapshot_patient", ["realm"] = "shining_abode", ["turn"] = 43 };
        var bindingBefore = binding.ToJsonString();
        JsonObject? outcome = null;
        var refusal = Record.Exception(() => { outcome = SpiritualTreatmentParticipation.CreateOutcome(
            new(Owner(kind), proposal), Locality, binding, 42, "transition_patient"); });
        Assert.Null(refusal);
        var record = outcome!["record"]!.AsObject();
        Assert.Equal(5, record["schemaVersion"]!.GetValue<int>());
        Assert.Equal("shining_target_consent", record["kind"]!.GetValue<string>());
        Assert.Equal("granted", record["status"]!.GetValue<string>());
        Assert.Equal(8, SpiritualTreatmentParticipation.ResultVersion(record));
        Assert.Equal(Owner(kind), SpiritualTreatmentParticipation.RecordTarget(record));
        Assert.Equal(new WoundOwnerCoordinate("shining_abode", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath),
            SpiritualTreatmentParticipation.RecordHealer(record));
        Assert.Equal(Locality, SpiritualTreatmentParticipation.RecordLocality(record));
        Assert.Equal(42, record["acceptedCycleOrdinal"]!.GetValue<int>());
        Assert.Equal(43, record["eligibleCycleOrdinal"]!.GetValue<int>());
        Assert.Equal("transition_patient", record["primaryTransitionId"]!.GetValue<string>());
        Assert.Equal(before, proposal.ToJsonString()); Assert.Equal(bindingBefore, binding.ToJsonString());
    }

    [Theory]
    [InlineData("resident")]
    [InlineData("radiant_actor")]
    public void ExistingSelectionParserPreservesExactFamilyEightWithoutResidentAlias(string kind)
    {
        var raw = new JsonObject
        {
            ["schemaVersion"] = 8, ["mode"] = "shining_entity_aid", ["direction"] = "player_to_shining_entity",
            ["woundId"] = "wound_shining_patient", ["expectedWoundFingerprint"] = Fingerprint,
            ["healer"] = SpiritualHealingRequestProjection.OwnerJson(new("shining_abode", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)),
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(Owner(kind)),
            ["locality"] = SpiritualTreatmentParticipation.LocalityJson(Locality), ["participationFingerprint"] = Fingerprint
        };
        var before = raw.ToJsonString(); SpiritualSelfTreatmentSelection? parsed = null;
        var refusal = Record.Exception(() => { parsed = SpiritualSelfTreatmentSelection.Parse(raw); });
        Assert.Null(refusal); Assert.NotNull(parsed!.Aid);
        Assert.Equal(8, parsed.Aid.FamilyVersion); Assert.False(parsed.Aid.ReceiveAid);
        Assert.Null(parsed.Aid.Resident); Assert.Null(parsed.Aid.GuardianProviderLocality); Assert.Null(parsed.Aid.Compensation);
        Assert.Equal(Owner(kind), parsed.Aid.Target("shining_abode"));
        Assert.True(JsonNode.DeepEquals(raw, parsed.ToJson())); Assert.Equal(before, raw.ToJsonString());
    }

    private const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static SpiritualAidLocality Locality => SpiritualAidLocality.ForFaction("faction_patient", "hall_patient");
    private static WoundOwnerCoordinate Owner(string kind) => new("shining_abode", kind, "shining_patient", WoundCarrierCatalog.AfterlifeProfilesPath);
    private static JsonObject Proposal() => new()
    {
        ["schemaVersion"] = 5, ["kind"] = "shining_target_consent", ["operation"] = "grant", ["healerId"] = "player_soul",
        ["woundId"] = "wound_shining_patient", ["locality"] = SpiritualTreatmentParticipation.LocalityJson(Locality),
        ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Пациент осознанно согласился на помощь."
    };
}
