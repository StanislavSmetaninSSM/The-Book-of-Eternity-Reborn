using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualDiagnosisBindingTests
{
    [Theory]
    [InlineData("missing")][InlineData("ordinal")][InlineData("foreign_actor")]
    [InlineData("tier")][InlineData("unknown_field")][InlineData("source")]
    public void ActualRequestFactoryRefusesIncompleteOrChangedRequirementProofData(string fault)
    {
        var context = SpiritualDiagnosisAdmissionTests.Context();
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context)
            .Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue").Data);
        var turn = new SpiritualSelfTreatmentStageContext("session_binding", "request_binding", 43,
            new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 });
        Assert.Equal(admitted.Selection, SpiritualDiagnosisRequestCodec.ReadSelection(
            SpiritualDiagnosisRequestCodec.Create(turn, admitted.Selection, admitted)));
        var bindings = admitted.RequirementBindings;
        Assert.Single(bindings);
        switch (fault)
        {
            case "missing": bindings.Clear(); break;
            case "ordinal": bindings[0]!["ordinal"] = 1; break;
            case "foreign_actor": bindings[0]!["actor"]!["ownerId"] = "foreign_player"; break;
            case "tier": bindings[0]!["tier"] = 6; break;
            case "unknown_field": bindings[0]!["unownedExtra"] = true; break;
            case "source": bindings[0]!["sourceDependencies"]![0]!["path"] = "game_state/world/current_location.json"; break;
        }
        var changed = admitted with { RequirementBindings = bindings };
        Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisRequestCodec.Create(turn, changed.Selection, changed));
        // The exported proof cannot mutate the actual operation-owned original.
        Assert.True(SpiritualDiagnosisRequestCodec.IsExact(SpiritualDiagnosisRequestCodec.Create(turn, admitted.Selection, admitted),
            turn, admitted.Selection, admitted));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void SelfConsistentClaimCannotReplaceTheActualDeclaredPathOrRequirementFingerprint(bool requirements)
    {
        var context = SpiritualDiagnosisAdmissionTests.Context();
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context)
            .Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue").Data);
        var turn = new SpiritualSelfTreatmentStageContext("session_binding", "request_binding", 43,
            new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 });
        var retained = SpiritualDiagnosisRequestCodec.Create(turn, admitted.Selection, admitted);
        var falseFingerprint = "sha256:" + new string('c', 64);
        var forged = requirements ? admitted with { RequirementFingerprint = falseFingerprint }
            : admitted with { PathFingerprint = falseFingerprint, Selection = admitted.Selection with
                { Diagnosis = admitted.Selection.Diagnosis! with { ExpectedDiagnosisPathFingerprint = falseFingerprint } } };
        var changed = retained.DeepClone().AsObject();
        changed["diagnosis"]![requirements ? "requirementFingerprint" : "pathFingerprint"] = falseFingerprint;
        changed["diagnosis"]!["attemptId"] = "spiritual_diagnosis_attempt_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "spiritual_diagnosis_request", "1", turn.SessionId, turn.RequestId, "43", "1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(forged.Selection.ToJson()) })[7..];
        changed.Remove("requestFingerprint");
        changed["requestFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis_request", "1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(changed) });
        // Closed request DATA can be self-consistent without possessing its original
        // wound. The actual admission factory must check the independent declaration.
        Assert.Equal(forged.Selection, SpiritualDiagnosisRequestCodec.ReadSelection(changed));
        Assert.False(SpiritualDiagnosisRequestCodec.IsExact(changed, turn, forged.Selection, forged));
        var failure = Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisRequestCodec.Create(turn, forged.Selection, forged));
        Assert.Contains("actual selected path/requirements/check", failure.Message, StringComparison.Ordinal);
    }

}
