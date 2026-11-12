using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalCanonicalSkillMasteryTests
{
    [Theory]
    [InlineData("Fireball", null)]
    [InlineData("fireball", "skill_mastery_confusable_reference")]
    [InlineData("Firеball", "skill_mastery_confusable_reference")]
    [InlineData("RemovedSpell", "skill_mastery_unknown_active_skill")]
    public void CurrentClosure_UsesExactCurrentNamesAndExistingIndexedDiagnostics(string reference, string? code)
    {
        var issues = ValidationService.ValidateCanonicalPlayerSkillMastery(Active("Fireball"), Mastery(reference));
        if (code is null) { Assert.Empty(issues); return; }
        var issue = Assert.Single(issues);
        Assert.Equal(code, issue.Code);
        Assert.Equal("game_state/player/skill_mastery.json.skillMasteryChanges[0].skillName", issue.FilePath);
        Assert.Equal(reference, issue.Actual);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    [Fact]
    public void CurrentClosure_AmbiguityNeverSelectsATargetAndExactSiblingsRemainValid()
    {
        var active = Active("Fireball", "fireball");
        Assert.Empty(ValidationService.ValidateCanonicalPlayerSkillMastery(active, Mastery("Fireball", "fireball")));
        var issue = Assert.Single(ValidationService.ValidateCanonicalPlayerSkillMastery(active, Mastery("FIREBALL")));
        Assert.Equal("skill_mastery_confusable_reference", issue.Code);
        Assert.Contains("several distinct skills", issue.Expected, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentClosure_MissingSourcesArePristineOnlyWithoutReferencesAndEachCallIsFresh()
    {
        Assert.Empty(ValidationService.ValidateCanonicalPlayerSkillMastery(null, null));
        Assert.Empty(ValidationService.ValidateCanonicalPlayerSkillMastery(null, Mastery()));
        Assert.Empty(ValidationService.ValidateCanonicalPlayerSkillMastery(Active("Fireball"), Mastery("Fireball")));
        Assert.Equal("skill_mastery_unknown_active_skill", Assert.Single(
            ValidationService.ValidateCanonicalPlayerSkillMastery(null, Mastery("Fireball"))).Code);
        Assert.Empty(ValidationService.ValidateCanonicalPlayerSkillMastery(Active("Fireball"), Mastery("Fireball")));
    }

    [Theory]
    [InlineData("bad", "{}", "invalid_json_file")]
    [InlineData("{}", "bad", "invalid_json_file")]
    [InlineData("[]", "{}", "expected_object")]
    [InlineData("{}", "[]", "expected_object")]
    [InlineData("{\"activeSkillChanges\":null}", "{}", "expected_array")]
    [InlineData("{}", "{\"skillMasteryChanges\":null}", "expected_array")]
    [InlineData("{\"activeSkillChanges\":[null]}", "{}", "expected_object")]
    public void CurrentClosure_MalformedPresentRootsFailClosed(string active, string mastery, string code)
    {
        Assert.Contains(ValidationService.ValidateCanonicalPlayerSkillMastery(active, mastery), issue => issue.Code == code);
    }

    [Fact]
    public void CurrentClosure_PreservesIndependentDuplicateAndMasteryFieldFindings()
    {
        var mastery = JsonNode.Parse(Mastery("Fireball", "Fireball"))!;
        mastery["skillMasteryChanges"]![1]!["newMasteryLevel"] = 0;
        var issues = ValidationService.ValidateCanonicalPlayerSkillMastery(Active("Fireball"), mastery.ToJsonString());
        Assert.Contains(issues, issue => issue.Code == "skill_state_duplicate_exact_name");
        Assert.Contains(issues, issue => issue.Code == "skill_mastery_non_positive_level");
    }

    private static string Active(params string[] names) => new JsonObject
    {
        ["activeSkillChanges"] = new JsonArray(names.Select(name => (JsonNode)new JsonObject { ["skillName"] = name }).ToArray())
    }.ToJsonString();

    private static string Mastery(params string[] names) => new JsonObject
    {
        ["skillMasteryChanges"] = new JsonArray(names.Select(name => (JsonNode)new JsonObject
        {
            ["skillName"] = name, ["newMasteryLevel"] = 2, ["newCurrentMasteryProgress"] = 7,
            ["newMasteryProgressNeeded"] = 100, ["masteryLeveledUp"] = false
        }).ToArray())
    }.ToJsonString();
}
