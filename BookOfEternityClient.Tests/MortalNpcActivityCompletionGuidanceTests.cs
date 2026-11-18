using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcActivityCompletionGuidanceTests
{
    [Fact]
    public void TerminalRuleAndWorkedExampleAgreeWithNpcOutcomeValidation()
    {
        var rules = XDocument.Parse(Read("Rules", "Block_19.F.txt"));
        var rule = Assert.Single(rules.Descendants("rule"), r => (string?)r.Attribute("id") == "19.F.3.1");
        var guidance = Assert.Single(rule.Elements("content"), c => (string?)c.Attribute("type") == "rule_text").Value;
        Assert.Contains("finalOutcome is mandatory and explicit", guidance, StringComparison.Ordinal);
        Assert.Contains("Completed accepts Success, SuccessWithComplication or Failure", guidance, StringComparison.Ordinal);
        Assert.Contains("Abandoned accepts only Failure", guidance, StringComparison.Ordinal);
        Assert.Contains("Never infer it from narrativeSummary", guidance, StringComparison.Ordinal);
        Assert.Contains("Do not send completionTurn: the client uses the authenticated original request turn", guidance, StringComparison.Ordinal);

        var example = Read("Examples", "E_Block_21.txt");
        const string heading = "<!-- #1516 ordinary terminal activity example: use this command family alone. -->";
        var sections = example.Split(heading, StringSplitOptions.None);
        Assert.Equal(2, sections.Length);
        // Only comments belonging to this worked example, not an arbitrary JSON command elsewhere in the file.
        var match = Regex.Match(sections[1], @"^\s*<!--(?:(?!-->)[\s\S])*?-->\s*<!--\s*(?<command>\{(?:(?!-->)[\s\S])*?\})\s*-->");
        Assert.True(match.Success, "Expected the terminal example's commentary and JSON payload.");
        var response = JsonNode.Parse(match.Groups["command"].Value)!.AsObject();
        var entry = Assert.Single(response["completeNPCActivities"]!.AsArray())!.AsObject();
        Assert.True(!response.ContainsKey("NPCActivityUpdates") || response["NPCActivityUpdates"] is JsonArray { Count: 0 });
        Assert.Equal("Completed", entry["finalState"]!.GetValue<string>());
        Assert.Equal("SuccessWithComplication", entry["finalOutcome"]!.GetValue<string>());
        Assert.False(entry.ContainsKey("completionTurn"));
        foreach (var name in new[] { "NPCId", "NPCName", "activityName", "narrativeSummary" })
            Assert.False(string.IsNullOrWhiteSpace(entry[name]?.GetValue<string>()), name);

        var root = Path.Combine(Path.GetTempPath(), "boe-npc-completion-guidance-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            // Response-scoped checks only: this fragment is not a complete turn or an authenticated NPC.
            string[] TerminalErrors()
            {
                using var document = JsonDocument.Parse(response.ToJsonString());
                return validator.ValidateResponse(document.RootElement)
                    .Where(i => i.Severity == IssueSeverity.Error &&
                        (i.Code?.StartsWith("npc_complete_activity_", StringComparison.Ordinal) == true ||
                        i.Code == "npc_activity_update_completion_composition_forbidden"))
                    .Select(i => i.Code!).ToArray();
            }
            Assert.Empty(TerminalErrors());
            entry.Remove("finalOutcome");
            Assert.Equal(new[] { "npc_complete_activity_outcome_required" }, TerminalErrors());
            entry["finalOutcome"] = "SuccessWithComplication"; entry["finalState"] = "Abandoned";
            Assert.Equal(new[] { "npc_complete_activity_outcome_state_conflict" }, TerminalErrors());
            entry["finalOutcome"] = "Failure"; entry["completionTurn"] = 42;
            Assert.Equal(new[] { "npc_complete_activity_turn_client_owned" }, TerminalErrors());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        static string Read(string directory, string file) =>
            ValidationAndRepairSource.Read(Path.Combine(TestRepoPaths.RepoRoot, directory, file));
    }
}
