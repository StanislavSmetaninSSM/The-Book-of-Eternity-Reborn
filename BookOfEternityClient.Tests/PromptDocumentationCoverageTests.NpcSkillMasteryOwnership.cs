using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PromptDocumentationCoverageTests
{
    [Fact]
    public void NpcSkillMasteryOwnership_HasBoundedGmRuleAndWorkedExample()
    {
        var example = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");
        foreach (var text in new[] { ReadRepoFile("Rules", "Block_7.txt"), ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"), example })
        foreach (var required in new[] { "npc_skill_mastery_unknown_owned_skill", "NPCPassiveSkillMasteryChanges",
                     "mastery-only update", "snapshot alone", "switch owner, invent a skill, delete progress", "request clarification" })
            Assert.Contains(required, text, StringComparison.Ordinal);
        var marker = example.IndexOf("Worked ownership example (#1533 T104-A", StringComparison.Ordinal);
        Assert.True(marker >= 0);
        var start = example.IndexOf("```json", marker, StringComparison.Ordinal) + "```json".Length;
        var end = example.IndexOf("```", start, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(example[start..end]);
        var authority = new ValidationService.NpcSkillMasteryOwnership();
        authority.CollectCore(document.RootElement.GetProperty("npcCore"));
        var issues = new List<ValidationIssue>();
        authority.ValidateSkills(document.RootElement.GetProperty("npcSkills"), "game_state/npcs/npc_skills.json", issues);
        var issue = Assert.Single(issues);
        Assert.Equal("npc_skill_mastery_unknown_owned_skill", issue.Code);
        Assert.Equal("id:npc_b", issue.Actor);
        Assert.Contains(issue.FilePath, example, StringComparison.Ordinal);
    }
}
