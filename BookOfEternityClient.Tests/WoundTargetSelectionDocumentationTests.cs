using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>The client navigation example and GM entrypoints do not mint a healing rule.</summary>
public sealed class WoundTargetSelectionDocumentationTests
{
    /// <summary>Both closed answer packets and both effective daemon assignments share the current guidance.</summary>
    [Fact]
    public void WorkedTargetAndWoundPacketsMatchTheClosedClientPhasesAndGmGuidance()
    {
        const string marker = "wound_target_navigation_v1";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
                     "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), a => Assert.Contains(marker, a.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == "spiritual_resident_help_v6");
        Assert.Contains(marker, entry.RequiredText);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        var start = example.IndexOf(marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var packets = Regex.Matches(example[start..], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, packets.Length);
        Assert.Equal("wound_target", Assert.Single(packets[0]).Key); Assert.Equal("target_0", packets[0]["wound_target"]!.GetValue<string>());
        Assert.Equal(new[] { "confirm_wound_treatment", "wound_selection" }, packets[1].Select(p => p.Key).OrderBy(p => p, StringComparer.Ordinal));
        Assert.True(packets[1]["confirm_wound_treatment"]!.GetValue<bool>());
        Assert.Contains("ZERO queue/draw/cycle/resources", example[start..]);
        Assert.Contains("No new GM output schema", example[start..]);
    }
}
