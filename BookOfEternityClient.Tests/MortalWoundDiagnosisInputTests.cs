using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundDiagnosisInputTests
{
    [Theory]
    [InlineData("missing_control")][InlineData("unknown_control")][InlineData("realm")]
    [InlineData("context")][InlineData("provider")][InlineData("extra")][InlineData("same_ordinal_control")]
    public void StagedInputBindsFullControlAndTypedContextAfterIndependentOuterReseal(string fault)
    {
        var context = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player", ["targetId"] = "player_current",
            ["providerKind"] = "npc", ["providerId"] = "physician_one", ["currentLocationId"] = "loc_clinic"
        }.ToJsonString(), "diagnosis.context");
        Assert.True(context.IsValid, JsonSerializer.Serialize(context.Issues));
        var before = WoundContractTestData.CreateActiveWound();
        before["treatment"]!["diagnosisPaths"] = new JsonArray(WoundContractTestData.CreateMortalDiagnosisPath(false, 0));
        var parsed = WoundMaterializationContract.Parse(before.ToJsonString(), "diagnosis.wound");
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var wound = parsed.Wound!;
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
        {
            MortalDiagnosis = new(wound.Owner, new("mortal_world", "npc", "physician_one", WoundCarrierCatalog.NpcPath),
                "inspect_explicit_method", WoundDiagnosisEvidence.ComputePathFingerprint(wound, "inspect_explicit_method"),
                MortalWoundDiagnosisInputCodec.ContextFingerprint(context.Context!))
        };
        var frame = new SpiritualSelfTreatmentStageContext("session_diagnosis", "request_diagnosis", 42,
            new ProgressionControl { CurrentRealm = "Mortal World", CurrentWorldTimeInMinutes = 120 });
        var input = MortalWoundDiagnosisInputCodec.Create(frame, selection, context.Context!);
        var unchanged = input.ToJsonString(); var roundTrip = MortalWoundDiagnosisInputCodec.Read(input);
        Assert.Equal(selection, roundTrip.Selection); Assert.True(MortalWoundDiagnosisInputCodec.IsBound(input, frame));
        Assert.Equal(unchanged, input.ToJsonString());
        if (fault == "same_ordinal_control")
        {
            var drift = frame with { Progression = new ProgressionControl { CurrentRealm = "Mortal World", CurrentWorldTimeInMinutes = 121 } };
            Assert.Equal(frame.Turn, drift.Turn);
            Assert.Equal(frame.Progression.NextChaosSeaTurnOrdinal, drift.Progression.NextChaosSeaTurnOrdinal);
            Assert.False(MortalWoundDiagnosisInputCodec.IsBound(input, drift));
            Assert.Equal(unchanged, input.ToJsonString()); return;
        }
        var bad = input.DeepClone().AsObject();
        switch (fault)
        {
            case "missing_control": bad.Remove("control"); break;
            case "unknown_control": bad["control"]!["invented"] = true; break;
            case "realm": bad["control"]!["currentRealm"] = "Chaos Sea"; break;
            case "context": bad["context"]!["currentLocationId"] = "loc_foreign"; break;
            case "provider": bad["selection"]!["provider"]!["ownerId"] = "foreign_physician"; break;
            case "extra": bad["extra"] = 1; break;
        }
        bad["inputFingerprint"] = MortalWoundDiagnosisInputCodec.Fingerprint(bad);
        var badBytes = bad.ToJsonString();
        Assert.Throws<InvalidDataException>(() => MortalWoundDiagnosisInputCodec.Read(bad));
        Assert.Equal(badBytes, bad.ToJsonString()); Assert.Equal(unchanged, input.ToJsonString());
    }
}
