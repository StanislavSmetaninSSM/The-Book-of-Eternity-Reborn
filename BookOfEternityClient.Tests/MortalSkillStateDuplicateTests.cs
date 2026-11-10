using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Independent typed regressions for exact current-state duplicates (#1533 T102-A).</summary>
public sealed class MortalSkillStateDuplicateTests
{
    private static readonly string[] Sections =
    {
        "activeSkillChanges", "passiveSkillChanges", "skillMasteryChanges",
        "NPCActiveSkillChanges", "NPCPassiveSkillChanges",
        "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges"
    };

    public static IEnumerable<object[]> Collections => Sections.Select(section => new object[] { section });

    public static IEnumerable<object[]> DuplicateRows => Sections.SelectMany(row =>
        new[] { new object[] { row, false }, new object[] { row, true } });

    [Theory]
    [MemberData(nameof(DuplicateRows))]
    public void RepeatedExactName_RejectsIdenticalAndConflictingRows(string section, bool conflicting)
    {
        var first = Row(section, "Fireball");
        var second = Row(section, "Fireball");
        if (conflicting)
        {
            if (section.Contains("Mastery", StringComparison.Ordinal) || section == "skillMasteryChanges")
                second["newMasteryLevel"] = 9;
            else
                second["skillDescription"] = "A different profile";
        }
        var root = Root(section, first, second);
        var before = root.ToJsonString();
        var issue = Assert.Single(Evaluate(section, root).Where(i => i.Code == "skill_state_duplicate_exact_name"));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(section.StartsWith("NPC", StringComparison.Ordinal) ? "id:npc_a" : "player", issue.Actor);
        Assert.Equal("Fireball", issue.Actual);
        Assert.EndsWith("[1].skillName", issue.FilePath, StringComparison.Ordinal);
        Assert.Contains("[0]", issue.Expected, StringComparison.Ordinal);
        Assert.Equal(before, root.ToJsonString());
    }

    [Theory]
    [MemberData(nameof(Collections))]
    public void EveryLaterDuplicate_PointsAtTheFirstOccurrence(string section)
    {
        var root = Root(section, Row(section, "Fireball"), Row(section, "Fireball"), Row(section, "Fireball"));
        var issues = Evaluate(section, root).Where(i => i.Code == "skill_state_duplicate_exact_name").ToArray();
        Assert.Equal(2, issues.Length);
        Assert.EndsWith("[1].skillName", issues[0].FilePath, StringComparison.Ordinal);
        Assert.EndsWith("[2].skillName", issues[1].FilePath, StringComparison.Ordinal);
        Assert.All(issues, issue => Assert.Contains("[0].skillName", issue.Expected, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Collections))]
    public void SingleRowAndOrdinalSiblings_AreAccepted(string section)
    {
        Assert.Empty(Evaluate(section, Root(section, Row(section, "Fireball"))));
        Assert.Empty(Evaluate(section, Root(section, Row(section, "Fireball"), Row(section, "FIREBALL"))));
    }

    [Theory]
    [InlineData("NPCActiveSkillChanges")]
    [InlineData("NPCPassiveSkillChanges")]
    public void RepeatedOwnerGroups_ShareDuplicateTracking(string section)
    {
        var root = Root(section, Row(section, "Fireball"));
        root[section]!.AsArray().Add(new JsonObject
        {
            ["NPCId"] = "NPC_A", ["NPCName"] = "Another display name",
            ["skillChanges"] = new JsonArray(Row(section, "Fireball"))
        });
        var issue = Assert.Single(Evaluate(section, root));
        Assert.Equal("skill_state_duplicate_exact_name", issue.Code);
        Assert.EndsWith($"{section}[1].skillChanges[0].skillName", issue.FilePath, StringComparison.Ordinal);
        Assert.Contains($"{section}[0].skillChanges[0]", issue.Expected, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NPCActiveSkillChanges")]
    [InlineData("NPCPassiveSkillChanges")]
    [InlineData("NPCSkillMasteryChanges")]
    [InlineData("NPCPassiveSkillMasteryChanges")]
    public void DifferentOwners_AreIndependent(string section)
    {
        var root = Root(section, Row(section, "Fireball"));
        var other = root[section]![0]!.DeepClone().AsObject();
        root[section]![0]!["NPCName"] = "Same name";
        other["NPCId"] = "npc_b";
        other["NPCName"] = "Same name";
        root[section]!.AsArray().Add(other);
        Assert.Empty(Evaluate(section, root));
    }

    [Theory]
    [InlineData("NPCActiveSkillChanges")]
    [InlineData("NPCPassiveSkillChanges")]
    [InlineData("NPCSkillMasteryChanges")]
    [InlineData("NPCPassiveSkillMasteryChanges")]
    public void NameFallbackOwner_UsesExistingCaseInsensitiveKey(string section)
    {
        var root = Root(section, Row(section, "Fireball"));
        var first = root[section]![0]!.AsObject();
        first.Remove("NPCId");
        first["NPCName"] = "Trainer";
        var second = first.DeepClone().AsObject();
        second["NPCName"] = "TRAINER";
        root[section]!.AsArray().Add(second);
        var issue = Assert.Single(Evaluate(section, root));
        Assert.Equal("skill_state_duplicate_exact_name", issue.Code);
        Assert.Equal("name:TRAINER", issue.Actor);
    }

    [Theory]
    [MemberData(nameof(Collections))]
    public void MalformedRow_DoesNotHideExistingDiagnostics(string section)
    {
        var root = Root(section, Row(section, "Fireball"), Row(section, "Fireball"));
        var duplicate = IsNpcSkill(section)
            ? root[section]![0]!["skillChanges"]![1]!.AsObject()
            : root[section]![1]!.AsObject();
        duplicate.Remove(IsMastery(section) ? "newMasteryLevel" : "rarity");
        var issues = Evaluate(section, root);
        Assert.Single(issues.Where(i => i.Code == "skill_state_duplicate_exact_name"));
        Assert.Contains(issues, i => i.FilePath.EndsWith(
            IsMastery(section) ? ".newMasteryLevel" : ".rarity", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Collections))]
    public void UnnamedAndNonObjectRows_DoNotFabricateDuplicates(string section)
    {
        var root = Root(section, Row(section, "Fireball"));
        var rows = IsNpcSkill(section) ? root[section]![0]!["skillChanges"]!.AsArray() : root[section]!.AsArray();
        rows.Add(null);
        rows.Add(new JsonObject { ["skillName"] = " " });
        rows.Add(new JsonObject { ["skillName"] = " " });
        rows.Add(new JsonObject { ["skillName"] = 12 });
        var issues = Evaluate(section, root);
        Assert.NotEmpty(issues);
        Assert.DoesNotContain(issues, i => i.Code == "skill_state_duplicate_exact_name");
    }

    [Fact]
    public void PlayerTrimAndNpcByteNames_PreserveExistingReaderSemantics()
    {
        Assert.Contains(Evaluate("activeSkillChanges", Root("activeSkillChanges",
            Row("activeSkillChanges", " Fireball "), Row("activeSkillChanges", "Fireball"))),
            i => i.Code == "skill_state_duplicate_exact_name");
        Assert.Empty(Evaluate("NPCActiveSkillChanges", Root("NPCActiveSkillChanges",
            Row("NPCActiveSkillChanges", " Fireball "), Row("NPCActiveSkillChanges", "Fireball"))));
    }

    [Fact]
    public void ActivePassiveAndMasteryCollections_StayIndependentAcrossCalls()
    {
        foreach (var row in Sections)
        {
            var section = row;
            var root = Root(section, Row(section, "Fireball"));
            Assert.Empty(Evaluate(section, root));
            Assert.Empty(Evaluate(section, root));
        }
    }

    private static List<ValidationIssue> Evaluate(string section, JsonObject root)
    {
        using var doc = JsonDocument.Parse(root.ToJsonString());
        var issues = new List<ValidationIssue>();
        if (section.StartsWith("NPC", StringComparison.Ordinal))
        {
            if (IsNpcSkill(section))
                ValidationService.ValidateNpcSkillChanges(doc.RootElement, "npc_skills.json", issues, section);
            else
                ValidationService.ValidateNpcSkillMastery(doc.RootElement, "npc_skills.json", issues, section,
                    section == "NPCPassiveSkillMasteryChanges");
        }
        else if (section == "skillMasteryChanges")
            ValidationService.ValidatePlayerSkillMasteryCore(doc.RootElement, "skill_mastery.json", issues,
                new HashSet<string>(new[] { "Fireball", "FIREBALL" }, StringComparer.Ordinal));
        else
            ValidationService.ValidatePlayerSkillChanges(doc.RootElement, "skills.json", issues, section);
        return issues;
    }

    private static bool IsNpcSkill(string section) => section is "NPCActiveSkillChanges" or "NPCPassiveSkillChanges";
    private static bool IsMastery(string section) => section is "skillMasteryChanges" or "NPCSkillMasteryChanges" or "NPCPassiveSkillMasteryChanges";

    private static JsonObject Root(string section, params JsonObject[] rows) => new()
    {
        [section] = IsNpcSkill(section)
            ? new JsonArray(new JsonObject { ["NPCId"] = "npc_a", ["skillChanges"] = new JsonArray(rows.Cast<JsonNode?>().ToArray()) })
            : new JsonArray(rows.Cast<JsonNode?>().ToArray())
    };

    private static JsonObject Row(string section, string name)
    {
        JsonObject row;
        if (IsMastery(section))
            row = new JsonObject { ["skillName"] = name, ["newMasteryLevel"] = 1,
                ["newCurrentMasteryProgress"] = 0, ["newMasteryProgressNeeded"] = 100, ["masteryLeveledUp"] = false };
        else if (section.Contains("assive", StringComparison.Ordinal))
            row = new JsonObject { ["skillName"] = name, ["skillDescription"] = "Skill", ["rarity"] = "Common",
                ["type"] = "KnowledgeBased", ["group"] = "Archive", ["masteryLevel"] = 1,
                ["maxMasteryLevel"] = 5, ["structuredBonuses"] = null };
        else
            row = new JsonObject { ["skillName"] = name, ["skillDescription"] = "Skill", ["rarity"] = "Rare",
                ["scalingCharacteristic"] = "Intelligence", ["combatEffect"] = new JsonObject
                { ["actionName"] = name, ["actionCost"] = "Main", ["isActivatedEffect"] = true,
                    ["effects"] = new JsonArray(new JsonObject { ["effectType"] = "Damage", ["targetType"] = "Enemy",
                        ["effectDescription"] = "Hit", ["value"] = "45%", ["poiseDamage"] = "5%" }) } };
        if (section.StartsWith("NPC", StringComparison.Ordinal) && !IsNpcSkill(section))
            row["NPCId"] = "npc_a";
        return row;
    }
}
