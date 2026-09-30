using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExampleDocumentationValidationTests
{
    internal static IReadOnlyList<JsonObject> SpiritualSourceActionExamples() =>
        ParseNamedJsonFences("E_CLI_Afterlife_Turns.txt", "spiritual_wound_source_action_v1");

    [Fact]
    public void SpiritualSourceAction_DocumentedTargetsUseProductionShapeValidator()
    {
        var examples = SpiritualSourceActionExamples();
        Assert.Equal(2, examples.Count);
        var locations = examples[0];
        var playerExchange = Assert.IsType<JsonObject>(locations["playerExchange"]);
        var oppositionExchange = Assert.IsType<JsonObject>(locations["oppositionExchange"]);

        foreach (var (exchange, expectedActorType, expectedActorId, expectedRetrauma) in new[]
                 {
                     (playerExchange, "guardian", "guardian_frame", "wound_torn_resonance"),
                     (oppositionExchange, "player", "player_soul", (string?)null)
                 })
        {
            var issues = new List<ValidationIssue>();
            ValidationService.ValidateSpiritualWoundSourceActionShape(
                exchange, "documented.exchange", issues);
            Assert.Empty(issues);
            var action = exchange["incomingAction"] as JsonObject ?? exchange;
            var target = Assert.IsType<JsonObject>(action["spiritualWoundTarget"]);
            Assert.Equal(
                expectedRetrauma is null
                    ? new[] { "actorId", "actorType" }
                    : new[] { "actorId", "actorType", "retraumaWoundRef" },
                target.Select(static property => property.Key)
                    .OrderBy(static property => property, StringComparer.Ordinal));
            Assert.Equal(expectedActorType, target["actorType"]!.GetValue<string>());
            Assert.Equal(expectedActorId, target["actorId"]!.GetValue<string>());
            if (expectedRetrauma is null)
                Assert.False(target.ContainsKey("retraumaWoundRef"));
            else
                Assert.Equal(expectedRetrauma, target["retraumaWoundRef"]!.GetValue<string>());
        }

        var forbidden = playerExchange.DeepClone().AsObject();
        forbidden["sourceSeverityCap"] = 2;
        var forbiddenIssues = new List<ValidationIssue>();
        ValidationService.ValidateSpiritualWoundSourceActionShape(
            forbidden, "documented.exchange", forbiddenIssues);
        Assert.Contains(forbiddenIssues,
            issue => issue.Code == "spiritual_source_computed_field_forbidden");
    }

    [Fact]
    public void SpiritualSourceAction_ManifestDeclaresFragmentOnlyProductionRoutes()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot, "Examples", "example_validation_manifest.json")));
        var contract = Assert.Single(
            manifest.RootElement.GetProperty("afterlifeEntityProfileCoverage").EnumerateArray(),
            item => item.GetProperty("contractId").GetString() ==
                    "spiritual_wound_source_action_v1");
        Assert.Equal("production-validator", contract.GetProperty("validationKind").GetString());
        Assert.Equal(new[] { "Chaos Sea", "Shining Abode" },
            contract.GetProperty("realms").EnumerateArray()
                .Select(static item => item.GetString()).ToArray());
        var route = contract.GetProperty("validationRoute").GetString();
        Assert.Contains("ValidateSpiritualWoundSourceActionShape", route, StringComparison.Ordinal);
        Assert.Contains("BeginSpiritualWoundSourceSessionAsync", route, StringComparison.Ordinal);
        Assert.Contains("TerminalClosure", route, StringComparison.Ordinal);
        Assert.Contains("not a complete response or accepted turn",
            contract.GetProperty("focusedFragmentReason").GetString(), StringComparison.Ordinal);
        Assert.Contains("C/D/E", contract.GetProperty("coverageLimit").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(2, SpiritualSourceActionExamples().Count);
    }
}
