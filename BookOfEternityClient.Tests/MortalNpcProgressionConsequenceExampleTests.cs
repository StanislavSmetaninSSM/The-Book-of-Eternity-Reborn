using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionConsequenceExampleTests
{
    [Theory]
    [InlineData("install")]
    [InlineData("replace")]
    public void WorkedProfileChange_PreservesLevelAndStoredXpThroughCoreAdapter(string operation)
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples",
            "E_CLI_Mortal_Npc_Profile_Change.txt"));
        var blocks = Regex.Matches(text, @"```json\s*([\s\S]*?)```")
            .Select(match => JsonNode.Parse(match.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, blocks.Length);
        var original = blocks[0];
        if (operation == "install") original["NPCsInScene"]![0]!.AsObject().Remove("progressionProfile");
        var originalText = original.ToJsonString();
        var current = original.DeepClone().AsObject();
        current["NPCCoreChanges"] = blocks[1]["NPCCoreChanges"]!.DeepClone();
        current["NPCCoreChanges"]![0]!["progressionProfileChange"]!["operation"] = operation;
        var authority = NpcCoreChangesContract.CreateAuthorityFromCanonicalJson(null, null, null, null) with
        {
            ProgressionSource = new("session_npc_example", "request_npc_example", 42,
                "test_authenticated_original", "mortal_world")
        };
        var evaluation = NpcCoreChangesContract.Evaluate(current, original, authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, detectDirectMutations: true);
        Assert.Empty(evaluation.Issues);
        Assert.True(evaluation.CanApply);
        NpcCoreChangesContract.Apply(current, evaluation);
        var actor = Assert.Single(current["NPCsInScene"]!.AsArray())!;
        Assert.Equal(2, actor["level"]!.GetValue<int>());
        Assert.Equal(40, actor["experience"]!.GetValue<int>());
        Assert.Equal(600, actor["experienceForNextLevel"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(blocks[1]["NPCCoreChanges"]![0]!["progressionProfileChange"]!["profile"],
            actor["progressionProfile"]));
        Assert.False(current.ContainsKey("NPCCoreChanges"));
        Assert.Equal(originalText, original.ToJsonString());
    }

    [Theory]
    [InlineData(0, "Fieldwork", 1, 6, 3)]
    [InlineData(1, "Archive", 2, 5, 4)]
    public void WorkedAlternative_UsesRealCombinedCandidateAndRequiresMatchingCompanion(
        int alternative, string awardedSkill, int mastery, int focus, int resolve)
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples",
            "E_CLI_Mortal_Npc_Progression_Consequences.txt"));
        var blocks = Regex.Matches(text, @"```json\s*([\s\S]*?)```")
            .Select(match => JsonNode.Parse(match.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(6, blocks.Length);
        var original = blocks[0];
        var originalText = original.ToJsonString();
        var world = blocks[2 + alternative * 2];
        var response = blocks[3 + alternative * 2];
        var responseText = response.ToJsonString();
        var authority = NpcCoreChangesContract.CreateAuthorityFromCanonicalJson(null, null, null,
            blocks[1].ToJsonString(), world.ToJsonString()) with
        {
            ProgressionSource = new("session_npc_example", "request_npc_example", 42,
                "test_authenticated_original", "mortal_world")
        };
        Assert.True(authority.NpcCharacteristicPolicy.IsValid);
        var raw = original.DeepClone().AsObject();
        raw["NPCCoreChanges"] = response["NPCCoreChanges"]!.DeepClone();
        var sidecar = response.DeepClone().AsObject();
        sidecar.Remove("NPCCoreChanges");

        var result = MortalNpcAcceptedSkillCandidate.Compose(original, new JsonObject(), raw, sidecar, authority);

        Assert.True(result.IsValid, string.Join(Environment.NewLine,
            result.Issues.Select(issue => $"{issue.Code}: {issue.FilePath}: {issue.Message}")));
        Assert.False(result.Core!.ContainsKey("NPCCoreChanges"));
        Assert.Empty(result.Sidecar!);
        var actor = Assert.Single(result.Core["NPCsInScene"]!.AsArray())!;
        Assert.Equal("npc_existing_001", actor["NPCId"]!.GetValue<string>());
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1558, actor["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(focus, actor["characteristics"]!["setting_defined_focus"]!.GetValue<int>());
        Assert.Equal(resolve, actor["characteristics"]!["setting_defined_resolve"]!.GetValue<int>());
        var skill = Assert.Single(actor["passiveSkills"]!.AsArray().OfType<JsonObject>()
            .Where(value => value["skillName"]!.GetValue<string>() == awardedSkill));
        Assert.Equal(mastery, skill["masteryLevel"]!.GetValue<int>());
        Assert.False(actor.AsObject().ContainsKey("skillAwards"));
        Assert.Equal(originalText, original.ToJsonString());
        Assert.Equal(responseText, response.ToJsonString());

        // The exact same valid numeric/policy command cannot discharge an award
        // through prose or its declaration when its companion command is omitted.
        var missingCompanion = MortalNpcAcceptedSkillCandidate.Compose(original, new JsonObject(),
            raw, new JsonObject(), authority);
        Assert.False(missingCompanion.IsValid);
        Assert.Contains(missingCompanion.Issues, issue => issue.Code == "npc_progression_skill_award_unfulfilled");
        Assert.Equal(originalText, original.ToJsonString());
    }
}
