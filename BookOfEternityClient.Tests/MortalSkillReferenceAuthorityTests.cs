using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalSkillReferenceAuthorityTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalSkillReferenceAuthorityTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-mortal-skill-reference-authority-" + Guid.NewGuid().ToString("N"));
        _fs = new FileSystemManager(_tempRoot, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public void Classify_ExactOrdinalMatch_ReturnsExact()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Fireball", "Shadow Step" });

        var match = authority.Classify("Fireball");

        Assert.Equal(MortalSkillReferenceMatchKind.Exact, match.Kind);
        Assert.False(match.IsAmbiguous);
        Assert.Null(match.CanonicalName);
    }

    [Fact]
    public void Classify_CaseVariant_ReturnsConfusableWithCanonicalName()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Fireball" });

        var match = authority.Classify("fireball");

        Assert.Equal(MortalSkillReferenceMatchKind.Confusable, match.Kind);
        Assert.Equal("Fireball", match.CanonicalName);
        Assert.False(match.IsAmbiguous);
    }

    [Fact]
    public void Classify_CyrillicHomoglyph_ReturnsConfusable()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Fireball" });

        // 'е' below is Cyrillic U+0435, not Latin 'e'.
        var match = authority.Classify("Firеball");

        Assert.Equal(MortalSkillReferenceMatchKind.Confusable, match.Kind);
        Assert.Equal("Fireball", match.CanonicalName);
    }

    [Fact]
    public void Classify_GreekHomoglyph_ReturnsConfusable()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Attack" });

        // 'Α' below is Greek capital alpha U+0391, not Latin 'A'.
        var match = authority.Classify("Αttack");

        Assert.Equal(MortalSkillReferenceMatchKind.Confusable, match.Kind);
        Assert.Equal("Attack", match.CanonicalName);
    }

    [Fact]
    public void Classify_UnknownName_ReturnsUnknown()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Fireball" });

        var match = authority.Classify("Shadow Step");

        Assert.Equal(MortalSkillReferenceMatchKind.Unknown, match.Kind);
        Assert.Null(match.CanonicalName);
        Assert.False(match.IsAmbiguous);
    }

    [Fact]
    public void Classify_AmbiguousConfusableKey_FailsClosedWithoutCanonicalName()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Fireball", "FIREBALL" });

        var match = authority.Classify("fireball");

        Assert.Equal(MortalSkillReferenceMatchKind.Confusable, match.Kind);
        Assert.True(match.IsAmbiguous);
        Assert.Null(match.CanonicalName);
    }

    [Fact]
    public void Classify_ExactMatchInsideAmbiguousKey_StillExact()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Fireball", "FIREBALL" });

        Assert.Equal(MortalSkillReferenceMatchKind.Exact, authority.Classify("Fireball").Kind);
        Assert.Equal(MortalSkillReferenceMatchKind.Exact, authority.Classify("FIREBALL").Kind);
    }

    [Fact]
    public void Classify_NullOrWhitespace_ReturnsUnknown()
    {
        var authority = MortalSkillReferenceAuthority.Build(new[] { "Fireball" });

        Assert.Equal(MortalSkillReferenceMatchKind.Unknown, authority.Classify(null).Kind);
        Assert.Equal(MortalSkillReferenceMatchKind.Unknown, authority.Classify("   ").Kind);
    }

    [Fact]
    public void Build_IgnoresNullAndWhitespaceCanonicalNames()
    {
        var authority = MortalSkillReferenceAuthority.Build(new string?[] { null, " ", "Fireball", "Fireball" });

        Assert.Equal(1, authority.Count);
        Assert.Equal(MortalSkillReferenceMatchKind.Unknown, authority.Classify(null).Kind);
    }

    [Fact]
    public async Task PlayerStateFiles_ConfusableMasteryReference_FailsClosed()
    {
        await WriteActiveSkillsAsync("Fireball");
        await WritePlayerMasteryAsync("fireball");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        var issue = Assert.Single(issues, candidate => string.Equals(
            candidate.Code,
            "skill_mastery_confusable_reference",
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("Fireball", issue.Expected, StringComparison.Ordinal);
        Assert.Equal("fireball", issue.Actual);
        Assert.Contains("Fireball", issue.RepairHint, StringComparison.Ordinal);
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "skill_mastery_unknown_active_skill",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PlayerStateFiles_ExactMasteryReference_Passes()
    {
        await WriteActiveSkillsAsync("Fireball");
        await WritePlayerMasteryAsync("Fireball");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "skill_mastery_confusable_reference",
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "skill_mastery_unknown_active_skill",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PlayerStateFiles_UnknownMasteryReference_KeepsLegacyCode()
    {
        await WriteActiveSkillsAsync("Fireball");
        await WritePlayerMasteryAsync("Shadow Step");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles);

        Assert.Single(issues, candidate => string.Equals(
            candidate.Code,
            "skill_mastery_unknown_active_skill",
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "skill_mastery_confusable_reference",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkillContractConsistency_ConfusablePlayerInitialization_FailsClosed()
    {
        await WriteActiveSkillsAsync("Fireball");
        await WritePlayerMasteryAsync("FIREBALL");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);

        var issue = Assert.Single(issues, candidate => string.Equals(
            candidate.Code,
            "player_active_skill_mastery_initialization_confusable",
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("Fireball", issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "player_active_skill_missing_mastery_initialization",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkillContractConsistency_ExactPlayerInitialization_Passes()
    {
        await WriteActiveSkillsAsync("Fireball");
        await WritePlayerMasteryAsync("Fireball");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);

        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "player_active_skill_mastery_initialization_confusable",
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "player_active_skill_missing_mastery_initialization",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkillContractConsistency_MissingPlayerInitialization_KeepsLegacyCode()
    {
        await WriteActiveSkillsAsync("Fireball");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);

        Assert.Single(issues, candidate => string.Equals(
            candidate.Code,
            "player_active_skill_missing_mastery_initialization",
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "player_active_skill_mastery_initialization_confusable",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkillContractConsistency_ConfusableNpcActiveInitialization_FailsClosed()
    {
        await WriteNpcSkillsAsync(
            activeSkillName: "Whirlwind",
            activeMasteryName: "whirlwind");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);

        var issue = Assert.Single(issues, candidate => string.Equals(
            candidate.Code,
            "npc_skill_mastery_initialization_confusable",
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("name:Тренер Гараст", issue.Actor);
        Assert.Contains("Whirlwind", issue.Expected, StringComparison.Ordinal);
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "npc_skill_missing_mastery_initialization",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkillContractConsistency_ConfusableNpcPassiveInitialization_FailsClosed()
    {
        await WriteNpcSkillsAsync(
            passiveSkillName: "Iron Skin",
            // 'І' below is Cyrillic U+0406, not Latin 'I'.
            passiveMasteryName: "Іron Skin");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);

        var issue = Assert.Single(issues, candidate => string.Equals(
            candidate.Code,
            "npc_skill_mastery_initialization_confusable",
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal("name:Тренер Гараст", issue.Actor);
        Assert.Equal("Skills.Passive", issue.Section);
    }

    [Fact]
    public async Task SkillContractConsistency_MissingNpcInitialization_KeepsLegacyCode()
    {
        await WriteNpcSkillsAsync(activeSkillName: "Whirlwind");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);

        Assert.Single(issues, candidate => string.Equals(
            candidate.Code,
            "npc_skill_missing_mastery_initialization",
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "npc_skill_mastery_initialization_confusable",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkillContractConsistency_ExactNpcInitialization_Passes()
    {
        await WriteNpcSkillsAsync(
            activeSkillName: "Whirlwind",
            activeMasteryName: "Whirlwind",
            passiveSkillName: "Iron Skin",
            passiveMasteryName: "Iron Skin");

        var issues = await _validator.ValidateGameStateAsync(GameStateValidationPhase.SkillContractConsistency);

        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "npc_skill_mastery_initialization_confusable",
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, candidate => string.Equals(
            candidate.Code,
            "npc_skill_missing_mastery_initialization",
            StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteActiveSkillsAsync(params string[] skillNames)
    {
        var skills = string.Join(",", skillNames.Select(name => $$"""
            {
              "skillName": "{{name}}",
              "skillDescription": "Test skill.",
              "rarity": "Rare",
              "combatEffect": {
                "actionName": "{{name}}",
                "actionCost": "Main",
                "effects": [
                  {
                    "effectType": "Damage",
                    "targetType": "Enemy",
                    "effectDescription": "Test effect.",
                    "value": "45%"
                  }
                ],
                "isActivatedEffect": true
              },
              "scalingCharacteristic": "Intelligence"
            }
            """));

        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", $$"""
        {
          "activeSkillChanges": [{{skills}}]
        }
        """);
    }

    private async Task WritePlayerMasteryAsync(params string[] masteryNames)
    {
        var entries = string.Join(",", masteryNames.Select(name => $$"""
            {
              "skillName": "{{name}}",
              "newMasteryLevel": 1,
              "newCurrentMasteryProgress": 0,
              "newMasteryProgressNeeded": 100,
              "masteryLeveledUp": false
            }
            """));

        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", $$"""
        {
          "skillMasteryChanges": [{{entries}}]
        }
        """);
    }

    private async Task WriteNpcSkillsAsync(
        string? activeSkillName = null,
        string? activeMasteryName = null,
        string? passiveSkillName = null,
        string? passiveMasteryName = null)
    {
        static string ActorEntry(string skillName) => $$"""
            { "NPCName": "Тренер Гараст", "skillChanges": [ { "skillName": "{{skillName}}" } ] }
            """;

        static string MasteryEntry(string skillName) => $$"""
            { "NPCName": "Тренер Гараст", "skillName": "{{skillName}}" }
            """;

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", $$"""
        {
          "NPCActiveSkillChanges": [{{(activeSkillName == null ? "" : ActorEntry(activeSkillName))}}],
          "NPCSkillMasteryChanges": [{{(activeMasteryName == null ? "" : MasteryEntry(activeMasteryName))}}],
          "NPCPassiveSkillChanges": [{{(passiveSkillName == null ? "" : ActorEntry(passiveSkillName))}}],
          "NPCPassiveSkillMasteryChanges": [{{(passiveMasteryName == null ? "" : MasteryEntry(passiveMasteryName))}}]
        }
        """);
    }
}
