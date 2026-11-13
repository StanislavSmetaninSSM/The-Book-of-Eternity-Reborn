using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalNpcProgressionPublicationTests
{
    [Fact]
    public async Task RawValidationAndCoordinator_TwoLevelGainWithMatchingSkillAwardPublishesCombinedCandidate()
    {
        using var fixture = await Fixture.Create(includeSkillSidecar: true);
        var authenticatedInputs = new Dictionary<string, byte[]?>();
        foreach (var path in fixture.Backups.Values.Append("game_state/control/pending_turn_snapshot.json"))
            authenticatedInputs.Add(path, await fixture.Files.ReadFileBytesAsync(path));

        var raw = JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
        var progression = raw["NPCCoreChanges"]![0]!["progression"]!.AsObject();
        // The signed original is level 1 with 99 XP: 99 + 566 = 100 + 565.
        // Preserve the fixture's exact session/request/turn source unchanged.
        progression["experienceDelta"] = 566;
        progression["level"] = 3;
        progression["experience"] = 0;
        progression["experienceForNextLevel"] = 1558;
        progression["skillAwards"] = new JsonArray(new JsonObject
        {
            ["kind"] = "passive", ["skillName"] = "Archive", ["award"] = "newSkill"
        });
        await fixture.Files.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, raw.ToJsonString());

        const string skillsPath = MortalNpcAcceptedSkillInput.SidecarPath;
        var pending = JsonNode.Parse((await fixture.Files.ReadFileAsync(skillsPath))!)!.AsObject();
        // Same production-valid profile used by MortalNpcSkillPublicationTests.WritePending.
        pending["NPCPassiveSkillChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_source", ["skillChanges"] = new JsonArray(new JsonObject
            {
                ["skillName"] = "Archive", ["skillDescription"] = "Archive knowledge", ["rarity"] = "Common",
                ["type"] = "KnowledgeBased", ["group"] = "Archives", ["masteryLevel"] = 1,
                ["maxMasteryLevel"] = 5, ["structuredBonuses"] = null, ["knowledgeDomain"] = "Archives",
                ["unlockedActiveSkillsCount"] = 0, ["maxUnlockableActiveSkills"] = 2
            })
        });
        await fixture.Files.WriteFileAtomicAsync(skillsPath, pending.ToJsonString());

        var rawIssues = await fixture.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync();
        Assert.True(rawIssues.Count == 0, string.Join(Environment.NewLine,
            rawIssues.Select(issue => $"{issue.Code}: {issue.FilePath}: {issue.Message}")));
        Assert.Equal(raw.ToJsonString(), JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.ToJsonString());
        Assert.Equal(pending.ToJsonString(), JsonNode.Parse((await fixture.Files.ReadFileAsync(skillsPath))!)!.ToJsonString());

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);

        Assert.Empty(result.Issues);
        Assert.Null(result.MechanicsPlan);
        var core = JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
        Assert.False(core.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(core["NPCsInScene"]!.AsArray())!.AsObject();
        Assert.Equal("npc_source", actor["NPCId"]!.GetValue<string>());
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1558, actor["experienceForNextLevel"]!.GetValue<int>());
        var skill = Assert.Single(actor["passiveSkills"]!.AsArray())!;
        Assert.Equal("Archive", skill["skillName"]!.GetValue<string>());
        Assert.Equal(1, skill["masteryLevel"]!.GetValue<int>());
        Assert.False(actor.ContainsKey("skillAwards"));
        Assert.False(actor.ContainsKey("experienceDelta"));
        Assert.Empty(JsonNode.Parse((await fixture.Files.ReadFileAsync(skillsPath))!)!.AsObject());
        foreach (var (path, bytes) in authenticatedInputs)
            Assert.Equal(bytes, await fixture.Files.ReadFileBytesAsync(path));
    }
}
