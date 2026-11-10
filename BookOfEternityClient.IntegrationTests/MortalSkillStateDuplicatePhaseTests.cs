using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Real state-file validation without snapshots or changed composition input (#1533 T102-A).</summary>
public sealed class MortalSkillStateDuplicatePhaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-skill-duplicate-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalSkillStateDuplicatePhaseTests()
    {
        _fs = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData("activeSkillChanges", "game_state/player/skills_active.json", false)]
    [InlineData("passiveSkillChanges", "game_state/player/skills_passive.json", false)]
    [InlineData("skillMasteryChanges", "game_state/player/skill_mastery.json", false)]
    [InlineData("NPCActiveSkillChanges", "game_state/npcs/npc_skills.json", true)]
    [InlineData("NPCPassiveSkillChanges", "game_state/npcs/npc_skills.json", true)]
    [InlineData("NPCSkillMasteryChanges", "game_state/npcs/npc_skills.json", true)]
    [InlineData("NPCPassiveSkillMasteryChanges", "game_state/npcs/npc_skills.json", true)]
    public async Task UnchangedCurrentStateWithoutSnapshot_RejectsExactDuplicates(string section, string path, bool npc)
    {
        var row = Row(section, "Fireball");
        var rows = new JsonArray(row, row.DeepClone());
        var root = new JsonObject { [section] = section is "NPCActiveSkillChanges" or "NPCPassiveSkillChanges"
            ? new JsonArray(new JsonObject { ["NPCId"] = "npc_a", ["skillChanges"] = rows }) : rows };
        var original = root.ToJsonString();
        await _fs.WriteFileAtomicAsync(path, original);
        var phase = npc ? GameStateValidationPhase.NpcStateFiles : GameStateValidationPhase.PlayerStateFiles;
        var issues = await _validator.ValidateGameStateAsync(phase);
        var issue = Assert.Single(issues.Where(i => i.Code == "skill_state_duplicate_exact_name"));
        Assert.StartsWith(path + "." + section, issue.FilePath, StringComparison.Ordinal);
        Assert.Equal(npc ? "id:npc_a" : "player", issue.Actor);
        Assert.Equal(original, await _fs.ReadFileAsync(path));
        // A fresh operation must still reject unchanged state; prior success/failure is never cached.
        Assert.Single((await _validator.ValidateGameStateAsync(phase)).Where(i => i.Code == "skill_state_duplicate_exact_name"));
    }

    [Fact]
    public async Task DuplicateMastery_StillReportsIndependentInvalidField()
    {
        var first = Row("skillMasteryChanges", "Fireball");
        var second = first.DeepClone().AsObject();
        second["newMasteryLevel"] = -1;
        await WriteAsync("game_state/player/skills_active.json", new JsonObject
        { ["activeSkillChanges"] = new JsonArray(Row("activeSkillChanges", "Fireball")) });
        await WriteAsync("game_state/player/skill_mastery.json", new JsonObject
        { ["skillMasteryChanges"] = new JsonArray(first, second) });
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);
        Assert.Single(issues.Where(i => i.Code == "skill_state_duplicate_exact_name"));
        Assert.Contains(issues, i => i.Code == "skill_mastery_non_positive_level" && i.FilePath.EndsWith("[1].newMasteryLevel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OrdinalSiblingsAndActivePassiveCollections_KeepExactReferenceBehavior()
    {
        await WriteAsync("game_state/player/skills_active.json", new JsonObject
        { ["activeSkillChanges"] = new JsonArray(Row("activeSkillChanges", "Fireball"), Row("activeSkillChanges", "FIREBALL")) });
        await WriteAsync("game_state/player/skills_passive.json", new JsonObject
        { ["passiveSkillChanges"] = new JsonArray(Row("passiveSkillChanges", "Fireball")) });
        await WriteAsync("game_state/player/skill_mastery.json", new JsonObject
        { ["skillMasteryChanges"] = new JsonArray(Row("skillMasteryChanges", "Fireball"), Row("skillMasteryChanges", "FIREBALL")) });
        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.PlayerStateFiles | GameStateValidationPhase.SkillContractConsistency);
        Assert.Empty(issues);
    }

    private Task WriteAsync(string path, JsonObject root) => _fs.WriteFileAtomicAsync(path, root.ToJsonString());

    private static JsonObject Row(string section, string name)
    {
        if (section.Contains("Mastery", StringComparison.Ordinal) || section == "skillMasteryChanges")
        {
            var row = new JsonObject { ["skillName"] = name, ["newMasteryLevel"] = 1, ["newCurrentMasteryProgress"] = 0,
                ["newMasteryProgressNeeded"] = 100, ["masteryLeveledUp"] = false };
            if (section.StartsWith("NPC", StringComparison.Ordinal)) row["NPCId"] = "npc_a";
            return row;
        }
        if (section.Contains("assive", StringComparison.Ordinal))
            return new JsonObject { ["skillName"] = name, ["skillDescription"] = "Skill", ["rarity"] = "Common",
                ["type"] = "KnowledgeBased", ["group"] = "Archive", ["masteryLevel"] = 1, ["maxMasteryLevel"] = 5, ["structuredBonuses"] = null };
        return new JsonObject { ["skillName"] = name, ["skillDescription"] = "Skill", ["rarity"] = "Rare",
            ["scalingCharacteristic"] = "Intelligence", ["combatEffect"] = new JsonObject
            { ["actionName"] = name, ["actionCost"] = "Main", ["isActivatedEffect"] = true,
                ["effects"] = new JsonArray(new JsonObject { ["effectType"] = "Damage", ["targetType"] = "Enemy",
                    ["effectDescription"] = "Hit", ["value"] = "45%", ["poiseDamage"] = "5%" }) } };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
