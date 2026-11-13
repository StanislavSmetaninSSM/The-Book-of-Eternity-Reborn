using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcSkillDisplayIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactCaseSiblings_RetainTheirOwnSkillAndMastery(bool passive)
    {
        var root = CreateRoot(passive,
            new JsonArray(
                new JsonObject { ["skillName"] = "Fireball", ["description"] = "First" },
                new JsonObject { ["skillName"] = "fireball", ["description"] = "Second" }),
            new JsonArray(
                Mastery("Fireball", 3, 2),
                Mastery("fireball", 7, 5)));
        var original = root.ToJsonString();
        using var doc = JsonDocument.Parse(original);

        var display = ExplorerMode.BuildNpcSkillDisplay(doc, "npc_owner", "Owner");
        var skills = passive ? display.Passive : display.Active;

        Assert.Equal(2, skills.Count);
        var first = Assert.Single(skills, skill => skill.Data["skillName"]!.GetValue<string>() == "Fireball");
        var second = Assert.Single(skills, skill => skill.Data["skillName"]!.GetValue<string>() == "fireball");
        Assert.Equal("First", first.Data["description"]!.GetValue<string>());
        Assert.Equal(3, first.MasteryLevel);
        Assert.Equal(2, first.CurrentMasteryProgress);
        Assert.Equal("Second", second.Data["description"]!.GetValue<string>());
        Assert.Equal(7, second.MasteryLevel);
        Assert.Equal(5, second.CurrentMasteryProgress);
        Assert.Equal(original, doc.RootElement.GetRawText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedCaseOnlyRemoval_RetainsOtherExactSkill(bool passive)
    {
        var root = CreateRoot(passive,
            new JsonArray(new JsonObject { ["skillName"] = "fireball" }),
            new JsonArray());
        // Canonical end-state after the owner removed Fireball: the exact-case sibling remains.
        // Command reduction/removal is covered by the pure composer, not replayed by the UI.
        using var doc = JsonDocument.Parse(root.ToJsonString());

        var display = ExplorerMode.BuildNpcSkillDisplay(doc, "npc_owner", "Owner");

        var retained = Assert.Single(passive ? display.Passive : display.Active);
        Assert.Equal("fireball", retained.Data["skillName"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedActorMatchingAndEmptyCatalog_RemainExact(bool passive)
    {
        var root = CreateRoot(passive,
            new JsonArray(new JsonObject { ["skillName"] = "Fireball" }),
            new JsonArray(Mastery("Fireball", 4, 1)));
        using (var doc = JsonDocument.Parse(root.ToJsonString()))
        {
            // Existing NPC ID matching is deliberately independent of skill-name matching.
            var display = ExplorerMode.BuildNpcSkillDisplay(doc, "NPC_OWNER", "Different name");
            var skill = Assert.Single(passive ? display.Passive : display.Active);
            Assert.Equal(4, skill.MasteryLevel);
            Assert.True(ExplorerMode.BuildNpcSkillDisplay(doc, "npc_other", "Other").IsEmpty);
        }

        root[passive ? "passiveSkills" : "activeSkills"] = new JsonArray();
        using var removed = JsonDocument.Parse(root.ToJsonString());
        Assert.True(ExplorerMode.BuildNpcSkillDisplay(removed, "npc_owner", "Owner").IsEmpty);
    }

    private static JsonObject Mastery(string name, int level, int progress) => new()
    {
        ["NPCId"] = "npc_owner",
        ["skillName"] = name,
        ["masteryLevel"] = level,
        ["currentMasteryProgress"] = progress
    };

    private static JsonObject CreateRoot(bool passive, JsonArray skills, JsonArray mastery)
    {
        foreach (var skill in skills.OfType<JsonObject>())
        {
            var accepted = mastery.OfType<JsonObject>().SingleOrDefault(row =>
                row["skillName"]!.GetValue<string>() == skill["skillName"]!.GetValue<string>());
            if (accepted is null) continue;
            skill["masteryLevel"] = accepted["masteryLevel"]!.DeepClone();
            skill["currentMasteryProgress"] = accepted["currentMasteryProgress"]!.DeepClone();
        }
        return new JsonObject
        {
            ["NPCId"] = "npc_owner", ["name"] = "Owner",
            [passive ? "passiveSkills" : "activeSkills"] = skills
        };
    }
}
