using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void LightIncarnateHistoryDocumentation_RequiresAcceptedPayloadNotOldMarker()
    {
        var guide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var api = ReadRepoFile("CLI_API_Specification.md");
        var daemon = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        foreach (var text in new[] { guide, matrix, api, daemon, examples })
        {
            foreach (var invariant in new[]
            {
                "Light Incarnate history requires accepted pre-turn payload evidence",
                "one occurrence once",
                "active exchanges and recent resolutions never share a history pool",
                "backdating alone cannot waive light_incarnate",
                "validated baseline without dice is still a current-turn boundary",
                "offline history without a validated baseline keeps its existing compatibility"
            })
            {
                Assert.Contains(invariant, text, StringComparison.Ordinal);
            }
        }
        foreach (var token in new[]
        {
            "afterlife_light_incarnate_history_authority_v1",
            "exchange_light_history_006",
            "grantedAtTurn=7",
            "resolvedAtTurn=6",
            "afterlife_conflict_light_incarnate_modifier_mismatch",
            "only the top-level summary",
            "exact pre-turn prefix",
            "not full wound or grant authority"
        })
        {
            Assert.Contains(token, examples, StringComparison.Ordinal);
        }
        using var manifest = JsonDocument.Parse(ReadRepoFile("Examples", "example_validation_manifest.json"));
        var entry = Assert.Single(
            manifest.RootElement.GetProperty("afterlifeEntityProfileCoverage").EnumerateArray(),
            item => item.GetProperty("contractId").GetString() == "afterlife_light_incarnate_history_authority_v1");
        Assert.Equal("E_CLI_Afterlife_Turns.txt", entry.GetProperty("file").GetString());
        Assert.Equal("production-validator", entry.GetProperty("validationKind").GetString());
        Assert.Contains("T085_LightIncarnateHistory_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("LightIncarnateHistoryDocumentation_", entry.GetProperty("validationRoute").GetString()!, StringComparison.Ordinal);
        Assert.Contains("not full wound or grant authority", entry.GetProperty("coverageLimit").GetString()!, StringComparison.Ordinal);
        var required = entry.GetProperty("requiredText").EnumerateArray().ToArray();
        Assert.NotEmpty(required);
        Assert.All(required, token =>
            Assert.Contains(token.GetString()!, examples, StringComparison.Ordinal));
    }
}
