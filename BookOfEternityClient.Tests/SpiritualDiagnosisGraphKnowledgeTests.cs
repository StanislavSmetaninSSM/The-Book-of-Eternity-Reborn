using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>File-free program/graph DATA and strict scope guards, not a live owner or published receipt.</summary>
public sealed class SpiritualDiagnosisGraphKnowledgeTests
{
    [Theory]
    [InlineData("diagnosed")]
    [InlineData("unselected")]
    [InlineData("ordinary")]
    public void GraphAdmissionAndPredecessorBindingPreserveCompleteCurrentComplicationKnowledge(string purpose)
    {
        var (turn, request, admission, members) = SpiritualDiagnosisProgramTests.Inputs(5, true, initialProgress: 3);
        Assert.Equal(5, admission.HealingTier);
        Assert.NotEqual(admission.Wound.WoundId, members[0].Wound.WoundId);
        Assert.Equal(3, admission.Wound.Recovery.CurrentStepProgress);
        Assert.Equal("hidden", Assert.Single(admission.Wound.Complications).Visibility);
        var program = SpiritualDiagnosisProgram.Build(turn, request, admission, members);
        var knowledgeAfter = program.KnowledgeAfter;
        Assert.Equal("known_to_player", Assert.Single(knowledgeAfter.Complications).Visibility);
        Assert.Contains(program.NaturalStages, stage => stage.WoundId == admission.Wound.WoundId && stage.Kind == "heal");
        var originals = members.Select(member => member.Wound).ToArray();
        string FrozenInputs() => request.ToJsonString() + string.Join("\n", originals.Select(WoundMaterializationContract.SerializeCanonical))
            + WoundMaterializationContract.SerializeCanonical(program.KnowledgeAfter);
        var frozen = FrozenInputs();
        var original = purpose == "unselected" ? originals[0] : originals.Single(wound => wound.WoundId == admission.Wound.WoundId);
        var expectedBefore = purpose == "diagnosed" ? knowledgeAfter : original;
        var graphSources = AfterlifeWoundHealingPlanner.Continuation.SelectGraphAdmissionWounds(originals,
            purpose == "ordinary" ? null : program);
        Assert.Equal(originals.Select(wound => wound.WoundId), graphSources.Select(wound => wound.WoundId));
        Assert.Equal(originals.Select(wound => wound.Owner), graphSources.Select(wound => wound.Owner));
        var source = Assert.Single(graphSources, wound => wound.WoundId == original.WoundId);
        var requested = Assert.Single(program.Projection["destinationGraphs"]!.AsArray().OfType<JsonObject>(), row =>
            row["stageRef"]!.GetValue<string>() == SpiritualHealingRequestProjection.GraphRef(program.NaturalProgramReference, original.WoundId, 1));
        Assert.Equal(1, requested["severityRank"]!.GetValue<int>());

        // The same explicit GM consequence choice used by AuthorNaturalGraphs:
        // one rank-I roll hindrance, without a complication-visibility command.
        var entry = Assert.Single(original.Consequences.Entries, item => item.ProfileKey == "spiritual_roll_hindrance");
        var binding = Assert.Single(original.Consequences.OwnedEffectSources.RootBindings, item => item.EffectId == entry.EffectId);
        var definition = JsonNode.Parse(Assert.Single(original.Consequences.OwnedEffectSources.Definitions,
            item => item.GetProperty("definitionKey").GetString() == binding.DefinitionKey).GetRawText())!.AsObject();
        definition["links"] = new JsonArray();
        var definitions = new JsonArray(new JsonObject
        {
            ["definitionRef"] = binding.DefinitionKey, ["definition"] = definition,
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = entry.ProfileKey,
                    ["readableSummary"] = "Явно описанное Мастером ослабленное последствие." })
            }
        });
        var map = source.Complications.ToDictionary(complication =>
            SpiritualHealingRequestProjection.ComplicationRef(program.NaturalProgramReference, complication.ComplicationId),
            complication => complication.ComplicationId, StringComparer.Ordinal);
        var parsed = WoundResponseInputComposer.ParseSpiritualHealingGraph(JsonSerializer.SerializeToElement(definitions),
            source, 1, requested["stageRef"]!.GetValue<string>(), map);
        var bound = WoundResponseInputComposer.BindSpiritualHealingGraphPredecessors(parsed, expectedBefore);
        Assert.Equal(entry.EffectId, Assert.Single(bound.Roots).Reference.Value);
        Assert.Equal(WoundWorkingReferenceOrigin.Existing, Assert.Single(bound.Roots).Reference.Origin);
        Assert.Single(bound.Definitions); Assert.Single(bound.Entries);
        if (purpose == "diagnosed") AssertScopeRefusesKnowledgeRegression(original, knowledgeAfter);
        Assert.Equal(frozen, FrozenInputs());

        // Complete ordered metadata, including references, state, text, modifier,
        // owned roots and visibility. Collection implementation identity is irrelevant.
        var expected = WoundMaterializationContract.ImportOwnedGraph(expectedBefore.Complications, expectedBefore.Consequences);
        Assert.Equal(JsonSerializer.Serialize(expected.Complications), JsonSerializer.Serialize(bound.Complications));
    }

    private static void AssertScopeRefusesKnowledgeRegression(WoundMaterializationEnvelope original,
        WoundMaterializationEnvelope knowledgeAfter)
    {
        // Independent well-formed stage DATA for the existing scope consumer.
        // This is not an accepted result or a claim to execute a recovery step.
        var before = knowledgeAfter with { Recovery = knowledgeAfter.Recovery with { CurrentStepProgress = 0 } };
        var stage = Assert.Single(SpiritualHealingRequestProjection.BuildNaturalProgram(before.Owner.Realm, 1,
            [new(before, 0)]).Stages);
        Assert.Equal(before.Severity.Rank, stage.SeverityRank);
        var after = before with
        {
            Recovery = before.Recovery with { CurrentStepProgress = stage.Progress, CurrentStepThreshold = 2 * stage.SeverityRank,
                LastTickKey = stage.TickKey },
            LastTransition = new(stage.TransitionId, checked(before.LastTransition.Ordinal + 1), 43, stage.Kind)
        };
        Assert.True(WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(before), "graph.scope.before").IsValid);
        Assert.True(WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(after), "graph.scope.after").IsValid);
        var published = new SpiritualHealingPublishedStage(before, after, stage.OperationKey, "graph_scope_event", stage.TickKey, stage.AttemptId);
        SpiritualHealingPersistedResult.ValidateStageScope(published, stage);
        var regressed = after with { Complications = original.Complications };
        Assert.True(WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(regressed), "graph.scope.regressed").IsValid);
        var failure = Assert.Throws<InvalidDataException>(() => SpiritualHealingPersistedResult.ValidateStageScope(
            published with { After = regressed }, stage));
        Assert.Equal("Spiritual stage changed unrelated care/recovery/complication fields.", failure.Message);
    }
}
