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
        var fragments = ParseNamedJsonFences("E_CLI_Afterlife_Turns.txt", contractId);
        Assert.Equal(6, fragments.Count);
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

        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            value => value.ContractId == contractId);
        Assert.Equal("focused-fragment", entry.ValidationKind);
        AssertTruthfulValidationMetadata(entry);
    }
}
