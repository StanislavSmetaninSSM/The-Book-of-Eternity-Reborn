using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    /// <summary>
    /// Checks the authored binding fragments through retained dice arithmetic and continuation parsing.
    /// </summary>
    [Fact]
    public void SpiritualBindingResultClosureWorkedExample_ValidatesArithmeticControlAndSeparateResponses()
    {
        const string contractId = "spiritual_wound_binding_result_closure_v1";
        var fragments = ParseNamedJsonFences("E_CLI_Afterlife_Wound_Continuation.txt", contractId);
        Assert.Equal(9, fragments.Count);
        var original = fragments[0];
        var corrected = fragments[1];
        Assert.Equal("binding", original["operationType"]!.GetValue<string>());
        Assert.Equal("binding", corrected["operationType"]!.GetValue<string>());
        Assert.Equal("success", original["outcome"]!.GetValue<string>());
        Assert.Equal("no_effect", corrected["outcome"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(original["before"], corrected["before"]));
        Assert.True(JsonNode.DeepEquals(original["before"]!["controlState"], corrected["after"]!["controlState"]));
        Assert.True(JsonNode.DeepEquals(original["after"]!["conflictPosition"], corrected["after"]!["conflictPosition"]));
        Assert.True(JsonNode.DeepEquals(original["after"]!["playerSideStrain"], corrected["after"]!["playerSideStrain"]));
        Assert.True(JsonNode.DeepEquals(original["diceAudit"]!["diceUsed"], corrected["diceAudit"]!["diceUsed"]));
        Assert.Equal(15, original["diceAudit"]!["playerTotal"]!.GetValue<int>());
        Assert.Equal(13, corrected["diceAudit"]!["playerTotal"]!.GetValue<int>());
        Assert.Equal(10, original["diceAudit"]!["oppositionTotal"]!.GetValue<int>());
        Assert.Equal(10, corrected["diceAudit"]!["oppositionTotal"]!.GetValue<int>());
        Assert.Equal(5, original["diceAudit"]!["margin"]!.GetValue<int>());
        Assert.Equal(3, corrected["diceAudit"]!["margin"]!.GetValue<int>());
        Assert.Equal("player_success", original["diceAudit"]!["outcomeBand"]!.GetValue<string>());
        Assert.Equal("player_success", corrected["diceAudit"]!["outcomeBand"]!.GetValue<string>());
        Assert.Empty(corrected["diceAudit"]!["modifierBreakdown"]!["player"]!.AsArray());
        foreach (var fragment in new[] { original, corrected })
        {
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualDiceAudit(fragment,
                JsonNode.Parse("{\"acceptedD20Values\":[5,15,13,10]}")!.AsObject(), issues);
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        }

        Assert.Equal("hindered", fragments[3]["activeConflict"]!["controlState"]!["level"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(corrected["after"]!["controlState"],
            fragments[4]["activeConflict"]!["controlState"]));
        var first = SpiritualWoundContinuationProtocol.ReadResponse(JsonSerializer.SerializeToElement(fragments[2]));
        var second = SpiritualWoundContinuationProtocol.ReadResponse(JsonSerializer.SerializeToElement(fragments[5]));
        Assert.Empty(first.WoundDecisions);
        Assert.Empty(second.WoundDecisions);
        Assert.NotEqual(first.ContinuationId, second.ContinuationId);
        var finalRequest = new SpiritualWoundContinuationRequest
        {
            ContinuationId = second.ContinuationId, Phase = "dependent_draft", Offer = null,
            SceneTextSource = new() { Path = "output/narrative_response.json", Field = "response" },
            DependentDraftFields = [new() { Path = AfterlifeSpiritualConflictState.StatePath,
                JsonPointer = "/activeConflict/controlState" }]
        };
        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(finalRequest, second));
        Assert.NotEmpty(SpiritualWoundContinuationProtocol.ValidateResponse(finalRequest, first));

        var soulArts = fragments[6]["afterlifeCombatProfile"]!["artTiers"]!.AsObject();
        var profiles = fragments[7]["profiles"]!.AsArray();
        var playerArts = Assert.Single(profiles, row => row!["actorId"]!.GetValue<string>() == "player_soul")!["standardArts"]!;
        var guardianArts = Assert.Single(profiles, row => row!["actorId"]!.GetValue<string>() == "guardian_frame")!["standardArts"]!;
        Assert.Equal(3, soulArts["force_binding"]!.GetValue<int>());
        Assert.Equal(3, playerArts["force_binding"]!.GetValue<int>());
        Assert.Equal(0, soulArts["binding"]!.GetValue<int>());
        Assert.Equal(0, playerArts["binding"]!.GetValue<int>());
        Assert.Equal(1, guardianArts["pressure"]!.GetValue<int>());
        foreach (var required in new[] { "spiritual_resilience", "spiritual_healing" })
            Assert.Equal(0, soulArts[required]!.GetValue<int>());
        var costs = fragments[8]["activeConflict"]!["exchangeLog"]!.AsArray();
        Assert.Equal(2, costs.Count);
        Assert.Equal("pressure", costs[0]!["operationType"]!.GetValue<string>());
        Assert.Equal("force_binding", costs[1]!["operationType"]!.GetValue<string>());
        for (var index = 0; index < costs.Count; index++)
        {
            foreach (var side in new[] { "player", "opposition" })
            {
                var audit = costs[index]!["actionCostAudit"]![side]!;
                var operation = side == "player" ? costs[index]!["operationType"]!.GetValue<string>() : "pressure";
                Assert.Equal(operation, audit["operationType"]!.GetValue<string>());
                var tier = (side == "player" ? soulArts[operation] : guardianArts[operation])!.GetValue<int>();
                Assert.Equal(tier, audit["artTier"]!.GetValue<int>());
                if (side == "player") Assert.Equal(tier, playerArts[operation]!.GetValue<int>());
                Assert.True(AfterlifeActionCostRules.TryGetDefinition(operation, out var definition));
                Assert.Equal(definition.BaseCost, audit["baseCost"]!.GetValue<int>());
                Assert.Equal(definition.MinCost, audit["minCost"]!.GetValue<int>());
                var cost = AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, tier);
                Assert.Equal(index == 0 && side == "player" ? 3 : 2, cost);
                Assert.Equal(cost, audit["effectiveCost"]!.GetValue<int>());
                var before = index == 0 ? 6 : side == "player" ? 3 : 4;
                var after = index == 0 ? side == "player" ? 3 : 4 : side == "player" ? 1 : 2;
                Assert.Equal(before, audit["before"]!.GetValue<int>());
                Assert.Equal(after, audit["after"]!.GetValue<int>());
                Assert.Equal(before - cost, after);
            }
        }

        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            value => value.ContractId == contractId);
        Assert.Equal("focused-fragment", entry.ValidationKind);
        AssertTruthfulValidationMetadata(entry);
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, source, StringComparison.Ordinal));
    }
}
