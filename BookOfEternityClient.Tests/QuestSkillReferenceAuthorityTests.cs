using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QuestSkillReferenceAuthorityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedSkill_CaseOnlyReplacementDoesNotResolveOldReward(bool playerProjection)
    {
        var context = Context(Skills("fireball", "skill_new", "Replacement"), playerProjection);
        var reward = JsonValue.Create("Fireball");
        Assert.False(context.TryResolve(QuestRewardKind.Skill, "Fireball", out _));
        var issue = Assert.Single(QuestRewardAuthority.ValidateQuestRewards(History(reward), context));
        Assert.Equal(QuestRewardAuthority.MissingSkillAuthorityCode, issue.Code);
        Assert.EndsWith("skillsUnlocked[0]", issue.Path);
        var text = QuestRewardAuthority.DescribePlayerReward(QuestRewardKind.Skill, reward, context);
        Assert.DoesNotContain("Replacement", text);
        Assert.Contains("подробности пока не записаны", text);
    }

    [Fact]
    public void ExactSiblingNames_ResolveTheirOwnLabels()
    {
        var root = Skills("Fireball", "first_id", "First");
        ((JsonArray)root["activeSkillChanges"]!).Add(new JsonObject
        {
            ["skillName"] = "fireball", ["skillId"] = "second_id", ["displayName"] = "Second"
        });
        var context = Context(root, false);
        Assert.True(context.TryResolve(QuestRewardKind.Skill, "Fireball", out var first));
        Assert.Equal("First", first);
        Assert.True(context.TryResolve(QuestRewardKind.Skill, "fireball", out var second));
        Assert.Equal("Second", second);
        Assert.False(context.TryResolve(QuestRewardKind.Skill, "FIREBALL", out _));
    }

    [Fact]
    public void ExactNameAndIdDifferingOnlyByCase_BothRemainRegistered()
    {
        var context = Context(Skills("Fireball", "fireball", "Label"), false);
        Assert.True(context.TryResolve(QuestRewardKind.Skill, "Fireball", out _));
        Assert.True(context.TryResolve(QuestRewardKind.Skill, "fireball", out _));
    }

    [Fact]
    public void ExplicitHistoricalReason_RemainsValidAfterRemoval()
    {
        var reward = new JsonObject { ["skillName"] = "Fireball", ["authorityStatus"] = "HistoricalOnly", ["reason"] = "Навык забыт." };
        Assert.Empty(QuestRewardAuthority.ValidateQuestRewards(History(reward), Context(null, false)));
        reward.Remove("reason");
        Assert.Equal(QuestRewardAuthority.MissingHistoryReasonCode,
            Assert.Single(QuestRewardAuthority.ValidateQuestRewards(History(reward), Context(null, false))).Code);
    }

    [Fact]
    public void ItemAndRelationshipMatching_RemainsUnchanged()
    {
        var context = Context(null, false);
        context.Register(QuestRewardKind.Item, ["Item_A"], "Item");
        context.Register(QuestRewardKind.Relationship, ["Npc_A"], "NPC");
        Assert.True(context.TryResolve(QuestRewardKind.Item, "item_a", out _));
        Assert.True(context.TryResolve(QuestRewardKind.Relationship, "npc_a", out _));
    }

    private static QuestRewardAuthorityContext Context(JsonNode? root, bool projection) => projection
        ? QuestRewardAuthorityContext.CreatePlayerProjection(null, root, null, null, null)
        : QuestRewardAuthorityContext.Create(null, root, null, null, null);

    private static JsonObject Skills(string name, string id, string label) => new()
    {
        ["activeSkillChanges"] = new JsonArray(new JsonObject { ["skillName"] = name, ["skillId"] = id, ["displayName"] = label })
    };

    private static JsonObject History(JsonNode? reward) => new()
    {
        ["questRewards"] = new JsonArray(new JsonObject { ["skillsUnlocked"] = new JsonArray(reward?.DeepClone()) })
    };
}
