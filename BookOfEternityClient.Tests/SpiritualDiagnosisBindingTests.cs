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
}
