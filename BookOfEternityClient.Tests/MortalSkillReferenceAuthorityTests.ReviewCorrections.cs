using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalSkillReferenceAuthorityTests
{
    /// <summary>
    /// Replaces only the confusable reference diagnosis while retaining independent errors.
    /// </summary>
    [Fact]
    public async Task PlayerStateFiles_UnicodeAndUnknownReferences_KeepIndependentDiagnostics()
    {
        await WriteActiveSkillsAsync("Fireball");
        await WritePlayerMasteryAsync("Firеball", "Unknown Skill");
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);
        var unknown = Assert.Single(issues, i => i.Code == "skill_mastery_unknown_active_skill");
        Assert.Equal("Unknown Skill", unknown.Actual);
        var confusable = Assert.Single(issues, i => i.Code == "skill_mastery_confusable_reference");
        Assert.Equal("Fireball", confusable.Expected);
        Assert.EndsWith("[0].skillName", confusable.FilePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Preserves exact canonical names and refuses a target for ambiguous references.
    /// </summary>
    /// <param name="reference">
    /// The mastery reference to validate against both distinct canonical names.
    /// </param>
    /// <param name="ambiguous">
    /// Whether the reference must fail without a literal canonical target.
    /// </param>
    [Theory]
    [InlineData("fireball", true)]
    [InlineData("Fireball", false)]
    [InlineData("FIREBALL", false)]
    public async Task PlayerStateFiles_CanonicalCaseCollision_PreservesExactIdentity(string reference, bool ambiguous)
    {
        await WriteActiveSkillsAsync("Fireball", "FIREBALL");
        await WritePlayerMasteryAsync(reference);
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);
        var identity = issues.Where(i => i.Code == "skill_mastery_confusable_reference").ToArray();
        if (ambiguous)
        {
            var issue = Assert.Single(identity);
            Assert.Contains("several distinct", issue.Expected, StringComparison.Ordinal);
            Assert.DoesNotContain("точное canonical имя '", issue.RepairHint, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(identity);
            Assert.DoesNotContain(issues, i => i.Code == "skill_mastery_unknown_active_skill");
        }
    }

    /// <summary>
    /// Keeps truly missing initialization while replacing confusable-only coverage.
    /// </summary>
    [Fact]
    public async Task SkillContractConsistency_UnicodeAndMissingPlayerInitialization_KeepIndependentDiagnostics()
    {
        await WriteActiveSkillsAsync("Fireball", "Shadow Step");
        await WritePlayerMasteryAsync("Firеball");
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);
        var missing = Assert.Single(issues, i => i.Code == "player_active_skill_missing_mastery_initialization");
        Assert.Contains("Shadow Step", missing.Expected, StringComparison.Ordinal);
        var identity = Assert.Single(issues, i => i.Code == "player_active_skill_mastery_initialization_confusable");
        Assert.Equal("Fireball", identity.Expected);
        Assert.Equal("Firеball", identity.Actual);
        Assert.EndsWith("skillMasteryChanges[0].skillName", identity.FilePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Accepts exact initialization coverage regardless of mastery entry order.
    /// </summary>
    /// <param name="exactFirst">
    /// Whether the exact reference precedes its confusable variant.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SkillContractConsistency_PlayerExactCoverage_IsOrderIndependent(bool exactFirst)
    {
        await WriteActiveSkillsAsync("Fireball");
        await WritePlayerMasteryAsync(exactFirst ? new[] { "Fireball", "fireball" } : new[] { "fireball", "Fireball" });
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);
        Assert.DoesNotContain(issues, i => i.Code == "player_active_skill_mastery_initialization_confusable");
    }

    /// <summary>
    /// Preserves actor ownership and exact coverage for both NPC skill sections.
    /// </summary>
    /// <param name="passive">
    /// Whether to validate passive rather than active mastery initialization.
    /// </param>
    /// <param name="exactFirst">
    /// Whether the exact mastery entry precedes its confusable variant.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SkillContractConsistency_NpcExactCoverage_IsOrderIndependent(bool passive, bool exactFirst)
    {
        var skillSection = passive ? "NPCPassiveSkillChanges" : "NPCActiveSkillChanges";
        var masterySection = passive ? "NPCPassiveSkillMasteryChanges" : "NPCSkillMasteryChanges";
        var names = exactFirst ? new[] { "Whirlwind", "whirlwind" } : new[] { "whirlwind", "Whirlwind" };
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [skillSection] = new[] { new { NPCName = "Trainer A", skillChanges = new[] { new { skillName = "Whirlwind" } } } },
            [masterySection] = names.Select(n => new { NPCName = "Trainer A", skillName = n }).ToArray()
        }));
        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);
        Assert.DoesNotContain(issues, i => i.Code == "npc_skill_mastery_initialization_confusable");
    }

    /// <summary>
    /// Gives every actor and mastery entry a distinct correction coordinate.
    /// </summary>
    [Fact]
    public async Task SkillContractConsistency_MultipleNpcCorrections_HaveIndexedPathsAndLiteralValues()
    {
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
        {
          "NPCActiveSkillChanges": [
            {"NPCName":"Trainer A","skillChanges":[{"skillName":"Whirlwind"},{"skillName":"Fireball"}]},
            {"NPCName":"Trainer B","skillChanges":[{"skillName":"Fireball"}]}
          ],
          "NPCSkillMasteryChanges": [
            {"NPCName":"Trainer A","skillName":"whirlwind"},
            {"NPCName":"Trainer A","skillName":"fireball"},
            {"NPCName":"Trainer B","skillName":"fireball"}
          ]
        }
        """);
        var issues = (await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency))
            .Where(i => i.Code == "npc_skill_mastery_initialization_confusable").ToArray();
        Assert.Equal(3, issues.Length);
        Assert.Equal(3, issues.Select(i => i.FilePath).Distinct().Count());
        Assert.All(issues, i => Assert.Matches(@"NPCSkillMasteryChanges\[\d\]\.skillName$", i.FilePath));
        Assert.Equal(new[] { "Fireball", "Fireball", "Whirlwind" }, issues.Select(i => i.Expected).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(2, issues.Count(i => i.Actor == "name:Trainer A"));
    }
}
