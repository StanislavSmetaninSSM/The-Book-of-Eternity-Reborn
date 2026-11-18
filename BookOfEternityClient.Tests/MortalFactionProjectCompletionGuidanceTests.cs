using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalFactionProjectCompletionGuidanceTests
{
    [Fact]
    public void MortalFactionProjectCompletionGuidance_UsesExplicitIntegerTurn()
    {
        var rule = ReadRepoFile("Rules", "Block_19.F.txt");
        var completionRule = rule[rule.IndexOf("Completing Faction Projects", StringComparison.Ordinal)..];
        Assert.Contains("nonnegative JSON integer", completionRule, StringComparison.Ordinal);
        Assert.Contains("\"completionTurn\": 184", completionRule, StringComparison.Ordinal);
        var example = ReadRepoFile("Examples", "E_Block_21.txt");
        Assert.Contains("\"completionTurn\": 184", example, StringComparison.Ordinal);
        Assert.DoesNotContain("\"completionTurn\": \"184\"", example, StringComparison.Ordinal);
        foreach (var variant in new[] { "fixed", "broken" })
        {
            var fixture = JsonNode.Parse(ReadRepoFile("FileSystemExample", "validator_fixtures",
                "faction_project_existing_id", variant, "faction_projects.json"))!;
            var value = Assert.IsAssignableFrom<JsonValue>(fixture["completeFactionProjects"]![0]!["completionTurn"]);
            Assert.True(value.TryGetValue<int>(out var turn) && turn >= 0);
        }
    }
    private static string ReadRepoFile(params string[] parts) =>
        ValidationAndRepairSource.Read(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(parts).ToArray()));
}
