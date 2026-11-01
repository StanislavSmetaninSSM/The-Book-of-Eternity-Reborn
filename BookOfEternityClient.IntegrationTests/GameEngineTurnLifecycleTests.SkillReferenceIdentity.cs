using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task WriteValidationRepairRequestAsync_SkillReferenceIdentityErrors_AddsBoundedRenameOnlyPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/player/skill_mastery.json.skillMasteryChanges[0].skillName",
                IssueSeverity.Error,
                "skillMasteryChanges должен ссылаться на exact canonical skillName, а не на case-variant или Unicode-confusable форму",
                code: "skill_mastery_confusable_reference",
                section: "Skills.Active",
                expected: "Fireball",
                actual: "fireball",
                repairHint: "Перепиши skillName посимвольно на точное canonical имя 'Fireball'."),
            new(
                "game_state/player/skill_mastery.json",
                IssueSeverity.Error,
                "Новый active skill покрыт mastery initialization только под confusable именем",
                code: "player_active_skill_mastery_initialization_confusable",
                section: "Skills.Active",
                expected: "skillMasteryChanges contains exact Fireball",
                actual: "only a case-variant or Unicode-confusable mastery entry exists",
                repairHint: "Перепиши skillName соответствующей записи посимвольно на 'Fireball'."),
            new(
                "game_state/npcs/npc_skills.json.NPCSkillMasteryChanges",
                IssueSeverity.Error,
                "Новый NPC skill покрыт mastery initialization только под confusable именем",
                code: "npc_skill_mastery_initialization_confusable",
                actor: "name:Тренер Гараст",
                section: "Skills.Active",
                expected: "NPCSkillMasteryChanges contains name:Тренер Гараст + exact Whirlwind",
                actual: "only a case-variant or Unicode-confusable mastery entry exists",
                repairHint: "Перепиши skillName соответствующей записи посимвольно на 'Whirlwind'.")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_skill_reference_identity_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("high", packet.GetProperty("priority").GetString());

        var targetFiles = packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("game_state/player/skills_active.json", targetFiles);
        Assert.Contains("game_state/player/skill_mastery.json", targetFiles);
        Assert.Contains("game_state/npcs/npc_skills.json", targetFiles);
        Assert.DoesNotContain(targetFiles, item => item != null && item.Contains("skillMasteryChanges[0]", StringComparison.Ordinal));
        Assert.Contains("Rules/Block_7.txt", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var corrections = packet.GetProperty("exactFieldCorrections").EnumerateArray().ToArray();
        Assert.Equal(3, corrections.Length);
        var confusableCorrection = Assert.Single(corrections, item =>
            string.Equals(item.GetProperty("code").GetString(), "skill_mastery_confusable_reference", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("game_state/player/skill_mastery.json.skillMasteryChanges[0].skillName", confusableCorrection.GetProperty("path").GetString());
        Assert.Equal("Fireball", confusableCorrection.GetProperty("expected").GetString());
        Assert.Equal("fireball", confusableCorrection.GetProperty("actual").GetString());

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, item => item.Contains("only allowed edit is rewriting", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, item => item.Contains("exact canonical name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, item => item.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("Do not delete mastery entries", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("Do not invent new skills", StringComparison.OrdinalIgnoreCase));
    }
}
