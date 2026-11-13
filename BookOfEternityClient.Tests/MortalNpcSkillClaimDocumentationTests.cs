using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks the private Mortal NPC claim authoring contract and its worked state linkage.</summary>
public sealed class MortalNpcSkillClaimDocumentationTests
{
    /// <summary>Every ordinary GM entrypoint teaches the same explicit report, not inferred actors.</summary>
    [Fact]
    public void MortalEntrypoints_RequireExplicitPrivateReportAndBoundItsGuarantee()
    {
        foreach (var path in new[] { "TaskGuides/CLI_Step_Main.txt",
                     "BookOfEternityClient/Launcher/CLI_Launch_Script.md",
                     "BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            foreach (var required in new[] { "npcSkillClaims", "actorKey", "skillName", "skillKind",
                         "claimType", "narrativeQuote", "stateDelta", "noProgress", "reason",
                         "empty array", "validated pre-turn", "not proof of prose completeness",
                         "mortal_npc_skill_claims_v1" })
                Assert.Contains(required, text, StringComparison.Ordinal);
        }
    }

    /// <summary>The example binds one exact NPC skill to its own numeric change and separate no-progress case.</summary>
    [Fact]
    public void WorkedExample_BindsExactOwnerSkillAndShowsNoProgressAndEmptyReport()
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Step_Main.txt"));
        const string begin = "BEGIN mortal_npc_skill_claims_v1 JSON\n";
        const string end = "\nEND mortal_npc_skill_claims_v1 JSON";
        Assert.Contains(begin, text, StringComparison.Ordinal);
        var offset = text.IndexOf(begin, StringComparison.Ordinal) + begin.Length;
        var finish = text.IndexOf(end, offset, StringComparison.Ordinal);
        Assert.True(finish > offset);
        using var document = JsonDocument.Parse(text[offset..finish]);
        var root = document.RootElement;
        var claim = Assert.Single(root.GetProperty("debugLogs").GetProperty("npcSkillClaims").EnumerateArray());
        var before = Assert.Single(root.GetProperty("beforeNpcSkills").GetProperty("NPCSkillMasteryChanges").EnumerateArray());
        var after = Assert.Single(root.GetProperty("npcSkills").GetProperty("NPCSkillMasteryChanges").EnumerateArray());
        Assert.Equal("id:npc_miron", claim.GetProperty("actorKey").GetString());
        Assert.Equal("active", claim.GetProperty("skillKind").GetString());
        Assert.Equal("mastery", claim.GetProperty("claimType").GetString());
        Assert.Equal("stateDelta", claim.GetProperty("resolution").GetString());
        Assert.Equal("npc_miron", before.GetProperty("NPCId").GetString());
        Assert.Equal(before.GetProperty("NPCId").GetString(), after.GetProperty("NPCId").GetString());
        Assert.Equal("Точный выпад", claim.GetProperty("skillName").GetString());
        Assert.Equal(claim.GetProperty("skillName").GetString(), after.GetProperty("skillName").GetString());
        Assert.Equal(before.GetProperty("skillName").GetString(), after.GetProperty("skillName").GetString());
        Assert.Equal(10, before.GetProperty("newCurrentMasteryProgress").GetInt32());
        Assert.Equal(11, after.GetProperty("newCurrentMasteryProgress").GetInt32());
        Assert.Contains(claim.GetProperty("narrativeQuote").GetString()!, root.GetProperty("narrativeResponse").GetProperty("response").GetString()!, StringComparison.Ordinal);
        var noProgress = Assert.Single(root.GetProperty("noProgressDebugLogs").GetProperty("npcSkillClaims").EnumerateArray());
        Assert.Equal("use", noProgress.GetProperty("claimType").GetString());
        Assert.Equal("noProgress", noProgress.GetProperty("resolution").GetString());
        Assert.False(string.IsNullOrWhiteSpace(noProgress.GetProperty("reason").GetString()));
        Assert.Contains(noProgress.GetProperty("narrativeQuote").GetString()!, root.GetProperty("noProgressNarrativeResponse").GetProperty("response").GetString()!, StringComparison.Ordinal);
        Assert.Empty(root.GetProperty("emptyDebugLogs").GetProperty("npcSkillClaims").EnumerateArray());
        Assert.Contains("not proof of prose completeness", text, StringComparison.Ordinal);
        Assert.Contains("Learning requires genuinely new", text, StringComparison.Ordinal);
    }
}
