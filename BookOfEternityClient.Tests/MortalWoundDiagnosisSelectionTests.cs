using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundDiagnosisSelectionTests
{
    [Theory]
    [InlineData("player", "player_current", WoundCarrierCatalog.PlayerPath)]
    [InlineData("npc", "patient_one", WoundCarrierCatalog.NpcPath)]
    public void ExactTypedMortalPurposeRoundTripsWithoutGrantingSpiritualExecution(string kind, string id, string path)
    {
        var selection = Selection() with { MortalDiagnosis = Selection().MortalDiagnosis! with
        { Target = new("mortal_world", kind, id, path) } };
        Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(selection.ToJson()));
        Assert.Equal("mortal_diagnose", selection.ToJson()["mode"]!.GetValue<string>());
        Assert.Throws<InvalidDataException>(() => selection.RequireTreatmentPurpose());
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentRequestCodec.Create(
            new("session_mortal", "request_mortal", 43, new ProgressionControl { CurrentRealm = "Mortal World" }),
            selection, "mortal_world", 1, Fingerprint, "normal", new[] { 14 }));
        var oldContext = new SpiritualSelfTreatmentStageContext("session_mortal", "request_mortal", 43,
            new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 });
        var oldSelection = new SpiritualSelfTreatmentSelection(selection.WoundId, selection.ExpectedWoundFingerprint);
        var oldSource = SpiritualSelfTreatmentRequestCodec.Create(oldContext, oldSelection, "chaos_sea", 1, Fingerprint, "normal", new[] { 14 });
        Assert.True(SpiritualSelfTreatmentRequestCodec.IsBoundActive(oldSource, oldContext, oldSelection, "chaos_sea", 1));
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundActive(oldSource, oldContext, selection, "chaos_sea", 1));
    }

    [Theory]
    [InlineData("extra")][InlineData("version")][InlineData("spiritual_target")]
    [InlineData("wrong_carrier")][InlineData("missing_provider")][InlineData("bad_path")]
    [InlineData("bad_context")][InlineData("mixed_treatment")][InlineData("mixed_spiritual_diagnosis")]
    public void ClosedPurposeAndExactTypedParticipantsCannotBeMixedOrDefaulted(string fault)
    {
        var valid = Selection(); Assert.Equal(valid, SpiritualSelfTreatmentSelection.Parse(valid.ToJson()));
        if (fault == "mixed_treatment")
        {
            Assert.Throws<InvalidDataException>(() => (valid with { Aid = new("player_soul", "guardian", Fingerprint) }).ToJson());
            return;
        }
        if (fault == "mixed_spiritual_diagnosis")
        {
            Assert.Throws<InvalidDataException>(() => (valid with { Diagnosis = new(new("chaos_sea", "player_soul", "player_soul",
                WoundCarrierCatalog.AfterlifeProfilesPath), "diagnosis", Fingerprint) }).ToJson());
            return;
        }
        var root = valid.ToJson();
        switch (fault)
        {
            case "extra": root["unowned"] = true; break;
            case "version": root["schemaVersion"] = 99; break;
            case "spiritual_target": root["target"]!["realm"] = "chaos_sea"; break;
            case "wrong_carrier": root["provider"]!["carrierPath"] = WoundCarrierCatalog.AfterlifeProfilesPath; break;
            case "missing_provider": root.Remove("provider"); break;
            case "bad_path": root["diagnosisPathId"] = " leading"; break;
            case "bad_context": root["expectedContextFingerprint"] = "unsealed"; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        var before = root.ToJsonString();
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(root));
        Assert.Equal(before, root.ToJsonString());
    }

    private const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static SpiritualSelfTreatmentSelection Selection() => new("wound_mortal", Fingerprint)
    {
        MortalDiagnosis = new(new("mortal_world", "player", "player_current", WoundCarrierCatalog.PlayerPath),
            new("mortal_world", "npc", "physician_one", WoundCarrierCatalog.NpcPath),
            "inspect_explicit_method", Fingerprint, Fingerprint)
    };
}
