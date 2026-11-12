using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalPassiveSkillCharacteristicAuthorityTests : IDisposable
{
    private const string SkillPath = "game_state/player/skills_passive.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-passive-characteristic-authority-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly CharacteristicsService _service;

    public MortalPassiveSkillCharacteristicAuthorityTests()
    {
        _fs = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        var state = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        _service = new CharacteristicsService(_fs, state, NullLogger<CharacteristicsService>.Instance);
    }

    [Theory]
    [InlineData("null", "+5 strength")]
    [InlineData("empty", "+5 strength")]
    [InlineData("missing", "+5 strength")]
    [InlineData("malformed", "+5 strength")]
    [InlineData("null", "-3 strength")]
    [InlineData("empty", "+7 Strength and perception")]
    public async Task ComputeAsync_DisplaySummaryNeverGrantsCharacteristicBonus(string shape, string summary)
    {
        var skill = CreateSkill(summary);
        switch (shape)
        {
            case "empty": skill["structuredBonuses"] = new JsonArray(); break;
            case "missing": skill.Remove("structuredBonuses"); break;
            case "malformed": skill["structuredBonuses"] = "not an array"; break;
        }
        if (shape is "null" or "empty") AssertValidProfile(skill);
        var actual = await ComputeSkillAsync(skill);
        Assert.Equal(0, actual.Permanent);
        Assert.Equal(0, actual.Temporary);
    }

    [Theory]
    [InlineData("Permanent", null, 2, 0)]
    [InlineData("Conditional", "When lifting a gate", 0, 2)]
    public async Task ComputeAsync_StructuredBonusAppliesExactlyOnceDespiteConflictingSummary(
        string application, string? condition, int permanent, int temporary)
    {
        var skill = CreateSkill("+5 strength");
        skill["structuredBonuses"] = StructuredBonus(application, condition);
        AssertValidProfile(skill);
        var actual = await ComputeSkillAsync(skill);
        Assert.Equal(permanent, actual.Permanent);
        Assert.Equal(temporary, actual.Temporary);
    }

    [Fact]
    public async Task ComputeAsync_NoSummaryAndNoStructuredBonusRemainsZero()
    {
        var actual = await ComputeSkillAsync(CreateSkill(null));
        Assert.Equal((0, 0), actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComputeAsync_RepeatedReadDoesNotRetainRemovedStructuredBonus(bool removeSkill)
    {
        var skill = CreateSkill("+5 strength");
        skill["structuredBonuses"] = StructuredBonus("Permanent", null);
        Assert.Equal((2, 0), await ComputeSkillAsync(skill));
        if (removeSkill)
            await _fs.WriteFileAtomicAsync(SkillPath, "{\"passiveSkillChanges\":[]}");
        else
            await WriteSkillAsync(CreateSkill("+5 strength"));
        var result = await _service.ComputeAsync();
        Assert.Equal(0, result.Stats[Characteristics.Strength].PermanentBonus);
        Assert.Equal(0, result.Stats[Characteristics.Strength].TemporaryBonus);
    }

    internal static JsonObject CreateSkill(string? summary) => new()
    {
        ["skillName"] = "Narrative discipline",
        ["skillDescription"] = "A narrative skill with optional structured mechanics.",
        ["rarity"] = "Common", ["type"] = "KnowledgeBased", ["group"] = "Knowledge",
        ["masteryLevel"] = 1, ["maxMasteryLevel"] = 5,
        ["structuredBonuses"] = null, ["playerStatBonus"] = summary
    };

    private static JsonArray StructuredBonus(string application, string? condition) => new(new JsonObject
    {
        ["bonusType"] = "Characteristic", ["valueType"] = "Flat", ["target"] = "strength",
        ["value"] = 2, ["application"] = application, ["condition"] = condition,
        ["description"] = "+2 strength"
    });

    internal static void AssertValidProfile(JsonObject skill)
    {
        using var document = JsonDocument.Parse(skill.ToJsonString());
        var issues = new List<ValidationIssue>();
        MortalSkillProfileContract.ValidatePassiveSkillObject(document.RootElement, "skill", issues);
        Assert.Empty(issues);
    }

    internal async Task<(int Permanent, int Temporary)> ComputeSkillAsync(JsonObject skill)
    {
        var bytes = await WriteSkillAsync(skill);
        var result = await _service.ComputeAsync();
        Assert.Equal(bytes, await _fs.ReadFileAsync(SkillPath));
        var strength = result.Stats[Characteristics.Strength];
        return (strength.PermanentBonus, strength.TemporaryBonus);
    }

    private async Task<string> WriteSkillAsync(JsonObject skill)
    {
        var bytes = new JsonObject { ["passiveSkillChanges"] = new JsonArray(skill.DeepClone()) }.ToJsonString();
        await _fs.WriteFileAtomicAsync(SkillPath, bytes);
        return bytes;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
