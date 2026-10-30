using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualDiagnosisReductionTests
{
    [Theory]
    [InlineData("progress", "wound_transition_diagnosis_mechanics_changed")]
    [InlineData("partial", "wound_transition_diagnosis_fact_unauthorized")]
    [InlineData("display", "wound_transition_diagnosis_display_changed")]
    public void SpiritualProofDoesNotPermitAnUndeclaredAfterImage(string fault, string code)
    {
        var (turn, retained, admitted, after) = KnowledgeFixture("chaos_sea");
        var valid = SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(turn, retained, admitted, after,
            after.LastTransition.TransitionId, "diagnosis_guard_operation", "turn_43:diagnosis_guard");
        Assert.True(WoundTransitionReducer.Reduce(valid).IsValid);
        var changed = fault switch
        {
            "progress" => after with { Recovery = after.Recovery with { CurrentStepProgress = after.Recovery.CurrentStepProgress + 1 } },
            "display" => after with { Display = after.Display with { Name = "Undeclared replacement" } },
            _ => after with { Complications = admitted.Wound.Complications }
        };
        var request = SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(turn, retained, admitted, changed,
            after.LastTransition.TransitionId, "diagnosis_guard_operation", "turn_43:diagnosis_guard");
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.False(reduced.IsValid);
        Assert.Contains(reduced.Issues, issue => issue.Code == code);
    }

    [Fact]
    public void SpiritualProofDoesNotTurnTheMortalFactoryIntoSpiritualAuthority()
    {
        var (turn, retained, admitted, after) = KnowledgeFixture("chaos_sea");
        var valid = SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(turn, retained, admitted, after,
            after.LastTransition.TransitionId, "diagnosis_guard_operation", "turn_43:diagnosis_guard");
        Assert.True(WoundTransitionReducer.Reduce(valid).IsValid);
        var mortal = WoundDiagnosisEvidence.CreateRequest(valid.TransitionId, turn.RequestId, valid.OperationKey,
            retained["diagnosis"]!["attemptId"]!.GetValue<string>(), valid.EventRef, turn.Turn, admitted.Wound, after,
            admitted.Selection.Diagnosis!.DiagnosisPathId, "success", admitted.RequirementFingerprint,
            retained["requestFingerprint"]!.GetValue<string>());
        Assert.Null(Assert.IsType<WoundDiagnosisEvidence>(mortal.Evidence).SpiritualProof);
        var rejected = WoundTransitionReducer.Reduce(mortal);
        Assert.False(rejected.IsValid);
        Assert.Contains(rejected.Issues, issue => issue.Code == "wound_transition_active_source_invalid");
        var tampered = valid with { ProposedAfter = after with { Recovery = after.Recovery with { CurrentStepProgress = 1 } } };
        Assert.Contains(WoundTransitionReducer.Reduce(tampered).Issues,
            issue => issue.Code == "wound_transition_diagnosis_evidence_mismatch");
        var other = KnowledgeFixture("shining_abode");
        var foreign = SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(other.Turn, other.Request, other.Admitted,
            other.After, after.LastTransition.TransitionId, valid.OperationKey, valid.EventRef);
        Assert.Contains(WoundTransitionReducer.Reduce(valid with { Evidence = foreign.Evidence }).Issues,
            issue => issue.Code == "wound_transition_diagnosis_evidence_mismatch");
    }

    [Fact]
    public void SpiritualFactoryRequiresTheExactRetainedContextAndExportsDetachedProof()
    {
        var (turn, retained, admitted, after) = KnowledgeFixture("chaos_sea");
        var request = SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(turn, retained, admitted, after,
            after.LastTransition.TransitionId, "diagnosis_guard_operation", "turn_43:diagnosis_guard");
        var proof = Assert.IsType<SpiritualDiagnosisTransitionProof>(Assert.IsType<WoundDiagnosisEvidence>(request.Evidence).SpiritualProof);
        proof.Request["diagnosis"]!["healingTier"] = 5;
        Assert.True(WoundTransitionReducer.Reduce(request).IsValid);
        var changed = retained.DeepClone().AsObject(); changed["requestId"] = "foreign_request";
        Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(turn,
            changed, admitted, after, after.LastTransition.TransitionId, request.OperationKey, request.EventRef));
        Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(
            turn with { Turn = 44 }, retained, admitted, after, after.LastTransition.TransitionId, request.OperationKey, request.EventRef));
    }

    private static (SpiritualSelfTreatmentStageContext Turn, System.Text.Json.Nodes.JsonObject Request,
        SpiritualDiagnosisAdmission Admitted, WoundMaterializationEnvelope After) KnowledgeFixture(string realm)
    {
        var context = SpiritualDiagnosisAdmissionTests.Context(realm);
        var before = Assert.Single(context.Catalog.Occurrences).Wound;
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(before, "diagnose_tissue").Data);
        var turn = new SpiritualSelfTreatmentStageContext("session_diagnosis_guard", "request_diagnosis_guard", 43,
            new ProgressionControl { CurrentRealm = context.Soul["currentRealm"]!.GetValue<string>(), NextChaosSeaTurnOrdinal = 1 });
        var request = SpiritualDiagnosisRequestCodec.Create(turn, admitted.Selection, admitted);
        var after = before with
        {
            Complications = before.Complications.Select(c => c with { Visibility = "known_to_player" }).ToArray(),
            LastTransition = new("transition_diagnosis_guard", before.LastTransition.Ordinal + 1, turn.Turn, "diagnose")
        };
        return (turn, request, admitted, after);
    }
}
