using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void GuidedPhysicalSelectionInputIsClosedPurposeAndFullControlBound(string mode)
    {
        var source = CreateScenario(mode == "procedure" ? "procedure_normal_uses_lowest_free_die" : mode == "course" ?
            "course_first_milestone_is_ready_at_inclusive_due_time" : "guaranteed_current_capability_proof_stabilizes", mode);
        var parsed = WoundMaterializationContract.Parse(source.Before.ToJsonString(), WoundCarrierCatalog.PlayerPath);
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var contextResult = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
        {
            ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player", ["targetId"] = "player_current",
            ["providerKind"] = "npc", ["providerId"] = "field_medic_01", ["currentLocationId"] = "loc_field_clinic_001"
        }.ToJsonString(), "guidedInput.context");
        var context = Assert.IsType<MortalWoundTreatmentAuthority.Context>(contextResult.Context);
        var control = new ProgressionControl { CurrentRealm = "Mortal World", CurrentWorldTimeInMinutes = checked((int)source.WorldMinute) };
        var frame = new SpiritualSelfTreatmentStageContext("session_guided_data", "request_guided_data", 42, control);
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
        { MortalTreatment = new(wound.Owner, new("mortal_world", "npc", "field_medic_01", WoundCarrierCatalog.NpcPath),
            source.RouteId, mode, mode == "course" ? 1 : null, MortalWoundTreatmentRouteFingerprint.Compute(wound, source.RouteId),
            MortalWoundGuidedTreatmentInputCodec.ContextFingerprint(context)) };
        var input = MortalWoundGuidedTreatmentInputCodec.Create(frame, selection, context);
        var original = input.ToJsonString();
        Assert.Equal(selection, MortalWoundGuidedTreatmentInputCodec.Read(input).Selection);
        Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(JsonNode.Parse(selection.ToJson().ToJsonString())));
        Assert.True(MortalWoundGuidedTreatmentInputCodec.IsBound(input, frame));
        Assert.False(MortalWoundDiagnosisInputCodec.IsBound(input, frame));
        Assert.False(MortalWoundGuidedTreatmentInputCodec.IsBound(input, frame with { RequestId = "foreign_request" }));
        var changedControl = new ProgressionControl { CurrentRealm = "Mortal World", CurrentWorldTimeInMinutes = checked((int)(source.WorldMinute + 1)) };
        Assert.False(MortalWoundGuidedTreatmentInputCodec.IsBound(input, frame with { Progression = changedControl }));
        var extra = input.DeepClone().AsObject(); extra["extra"] = true;
        extra["inputFingerprint"] = MortalWoundGuidedTreatmentInputCodec.Fingerprint(extra);
        Assert.Throws<InvalidDataException>(() => MortalWoundGuidedTreatmentInputCodec.Read(extra));
        var mixed = selection with { MortalDiagnosis = new(selection.MortalTreatment!.Target, selection.MortalTreatment.Provider,
            "foreign_method", selection.MortalTreatment.ExpectedRouteFingerprint, selection.MortalTreatment.ExpectedContextFingerprint) };
        Assert.Throws<InvalidDataException>(() => mixed.ToJson());
        var invalidOrdinal = selection.ToJson(); invalidOrdinal["milestoneOrdinal"] = mode == "course" ? null : JsonValue.Create(1);
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(invalidOrdinal));
        Assert.Equal(original, input.ToJsonString());
    }
}
