using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualDiagnosisProgramTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(0, true)]
    public void KnowledgePrecedesTheCompleteOrdinaryCohortWithoutPrioritizingItsWound(int tier, bool otherFirst)
    {
        var (turn, request, admission, members) = Inputs(tier, otherFirst);
        var program = SpiritualDiagnosisProgram.Build(turn, request, admission, members);
        Assert.Equal("diagnose", program.Stages[0].Kind);
        Assert.Equal(admission.Wound.WoundId, program.Stages[0].WoundId);
        Assert.Equal(2, program.KnowledgeBefore.Severity.Rank);
        Assert.Equal("known_to_player", Assert.Single(program.KnowledgeAfter.Complications).Visibility);
        Assert.Equal(program.KnowledgeBefore.Recovery, program.KnowledgeAfter.Recovery);
        Assert.NotEmpty(program.NaturalStages);
        Assert.Equal(otherFirst ? "aaa_other" : admission.Wound.WoundId, program.NaturalStages[0].WoundId);
        Assert.Equal(members.Select(m => m.Wound.WoundId), program.NaturalMembers.Select(m => m.Wound.WoundId));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(program.KnowledgeAfter),
            WoundMaterializationContract.SerializeCanonical(program.NaturalMembers.Single(m => m.Wound.WoundId == admission.Wound.WoundId).Wound));
        var natural = SpiritualHealingRequestProjection.BuildNaturalProgram(admission.Wound.Owner.Realm, 1, program.NaturalMembers);
        Assert.Equal(natural.Stages, program.NaturalStages);
        if (tier == 0) Assert.All(program.NaturalStages, s => { Assert.Equal("recover", s.Kind); Assert.Equal(2, s.SeverityRank); });
        Assert.All(program.NaturalStages, s => Assert.Null(s.AttemptId));
        Assert.NotEqual(request["requestFingerprint"]!.GetValue<string>(), program.NaturalProgramReference);
        var projection = program.Projection;
        Assert.True(JsonNode.DeepEquals(projection["naturalRecovery"], SpiritualHealingRequestProjection.CreateNatural(
            admission.Wound.Owner.Realm, 1, program.NaturalProgramReference, program.NaturalMembers)));
        projection["mode"] = "foreign";
        Assert.Equal("diagnose", program.Projection["mode"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("empty")][InlineData("missing_selected")][InlineData("duplicate")]
    [InlineData("reordered")][InlineData("changed_selected")][InlineData("wrong_tier")][InlineData("source_tier")]
    public void IncompleteOrChangedOriginalCohortCannotBecomeAnExecutableProgram(string fault)
    {
        var (turn, request, admission, members) = Inputs(0, true);
        Assert.NotNull(SpiritualDiagnosisProgram.Build(turn, request, admission, members));
        var changed = fault switch
        {
            "empty" => Array.Empty<SpiritualWoundRecoveryMember>(),
            "missing_selected" => members.Take(1).ToArray(),
            "duplicate" => members.Append(members[0]).ToArray(),
            "reordered" => members.Reverse().ToArray(),
            "changed_selected" => members.Select(m => m.Wound.WoundId == admission.Wound.WoundId
                ? m with { Wound = m.Wound with { Recovery = m.Wound.Recovery with { CurrentStepProgress = 1 } } } : m).ToArray(),
            "source_tier" => members.Select(m => m with { HealingTier = 1 }).ToArray(),
            _ => members.Select(m => m with { HealingTier = 6 }).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisProgram.Build(turn, request, admission, changed));
    }

    [Fact]
    public void HealingTierVWithCanonicalSeverityIICanReachTheActualTerminalSuffix()
    {
        var (turn, request, admission, members) = Inputs(5, false, initialProgress: 3);
        Assert.Equal(5, admission.HealingTier);
        Assert.Equal(2, admission.Wound.Severity.Rank);
        var program = SpiritualDiagnosisProgram.Build(turn, request, admission, members);
        Assert.Equal(3, program.KnowledgeAfter.Recovery.CurrentStepProgress);
        Assert.Equal(2, program.KnowledgeAfter.Severity.Rank);
        Assert.Contains(program.NaturalStages, stage => stage.Kind == "recover");
        Assert.Equal("heal", program.NaturalStages[^1].Kind);
        Assert.Equal("diagnose", program.Stages[0].Kind);
    }

    internal static (SpiritualSelfTreatmentStageContext Turn, JsonObject Request, SpiritualDiagnosisAdmission Admission,
        SpiritualWoundRecoveryMember[] Members) Inputs(int tier, bool otherFirst, int initialProgress = 0)
    {
        var prototype = SpiritualDiagnosisAdmissionTests.Context();
        var node = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(Assert.Single(prototype.Catalog.Occurrences).Wound))!.AsObject();
        node["recovery"]!["currentStepProgress"] = initialProgress;
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", node);
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var context = new WoundPlayerReadContext(input, catalog, prototype.Soul.DeepClone().AsObject(), null, WoundPlayerProjection.Build(input, catalog));
        context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = tier, ["spiritual_stabilization"] = 0 };
        context.Soul["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = tier;
        var selected = Assert.Single(context.Catalog.Occurrences).Wound;
        var admission = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(selected, "diagnose_tissue").Data);
        var turn = new SpiritualSelfTreatmentStageContext("session_diagnosis_program", "request_diagnosis_program", 43,
            new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 });
        var request = SpiritualDiagnosisRequestCodec.Create(turn, admission.Selection, admission);
        var members = new List<SpiritualWoundRecoveryMember>();
        if (otherFirst)
        {
            var otherNode = WoundContractTestData.CreateSpiritualActiveWound(woundId: "aaa_other", ownerId: "player_soul",
                carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
            var text = otherNode.ToJsonString();
            foreach (var owned in new[] { "effect_spiritual_roll", "effect_spiritual_cost", "definition_spiritual_roll", "definition_spiritual_cost",
                "component_spiritual_roll", "component_spiritual_cost", "stack_definition_spiritual_roll", "stack_definition_spiritual_cost" })
                text = text.Replace("\"" + owned + "\"", "\"" + owned + "_other\"", StringComparison.Ordinal);
            var parsed = WoundMaterializationContract.Parse(text, "fixture.other");
            Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
            members.Add(new(parsed.Wound!, tier));
        }
        members.Add(new(selected, tier));
        return (turn, request, admission, members.ToArray());
    }
}
