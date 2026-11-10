using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Current known terminal DATA, registered navigation and GM instructions agree.</summary>
public sealed class WoundHistoryDocumentationTests
{
    /// <summary>The worked navigation uses actual safe projection without inventing a GM response schema.</summary>
    [Fact]
    public void WorkedHistoryNavigationUsesExistingProjectionAndRegisteredReadCommands()
    {
        const string marker = "wound_history_view_v1";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
                     "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), a => Assert.Contains(marker, a.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == "spiritual_resident_help_v6");
        Assert.Contains(marker, entry.RequiredText);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var match = Regex.Match(text[start..], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant); Assert.True(match.Success);
        var data = JsonNode.Parse(match.Groups[1].Value)!.AsObject();
        Assert.Equal("Известная зажившая рана", data["knownHealedName"]!.GetValue<string>());
        var projection = WoundPlayerProjection.Build(WoundPlayerHistoryTestData.TerminalInput());
        Assert.True(projection.IsAvailable); Assert.Equal("Известная зажившая рана", Assert.Single(projection.History).Name);
        foreach (var command in new[] { data["activeCommand"]!.GetValue<string>(), data["backCommand"]!.GetValue<string>() }
                     .Concat(data["historyCommands"]!.AsArray().Select(n => n!.GetValue<string>())))
        {
            var parsed = WoundApplicationService.ParseReadCommand(command); var history = parsed.Subcommand?.Id == "history";
            var result = ExplorerWoundCommandResultBuilder.Build(parsed.CanonicalCommand, projection, null, false, history);
            var rendered = JsonSerializer.Serialize(result, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Assert.Equal(history, rendered.Contains("Известная зажившая рана", StringComparison.Ordinal));
            Assert.Null(result.PendingGmAction); Assert.Empty(result.Prompts);
            foreach (var secret in new[] { "wound_healed_ui", "transitionResult", "sha256:", "operation_ui_", "session_ui" })
                Assert.DoesNotContain(secret, rendered);
        }
    }
}
