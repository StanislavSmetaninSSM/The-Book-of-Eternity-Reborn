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
    /// <summary>
    /// Routes actual Unicode validation output to a single identity repair packet.
    /// </summary>
    [Fact]
    public async Task WriteValidationRepairRequestAsync_SkillReferenceUnicode_UsesOnlyIdentityPacket()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {"activeSkillChanges":[{"skillName":"Fireball"}]}
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {"skillMasteryChanges":[{"skillName":"Firеball","newMasteryLevel":2,"newCurrentMasteryProgress":7,"newMasteryProgressNeeded":100,"masteryLeveledUp":false}]}
        """);
        var validator = new ValidationService(_fs, Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        var issues = (await validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles))
            .Where(i => i.Code is "skill_mastery_unknown_active_skill" or "skill_mastery_confusable_reference").ToList();
        var before = await _fs.ReadFileAsync("game_state/player/skill_mastery.json");
        using var request = await WriteSkillIdentityRepairForTestAsync(issues);
        var packet = Assert.Single(request.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_skill_reference_identity_repair", packet.GetProperty("kind").GetString());
        Assert.Equal(before, await _fs.ReadFileAsync("game_state/player/skill_mastery.json"));
    }

    /// <summary>
    /// Retains multiple corrections sharing a legacy section path.
    /// </summary>
    [Fact]
    public async Task WriteValidationRepairRequestAsync_SkillReferenceMultipleActors_RetainsEveryCorrection()
    {
        var issues = new List<ValidationIssue>
        {
            new("game_state/npcs/npc_skills.json.NPCSkillMasteryChanges", IssueSeverity.Error, "first", code: "npc_skill_mastery_initialization_confusable", actor: "name:Trainer A", expected: "Whirlwind", actual: "whirlwind", repairHint: "Trainer A: Whirlwind"),
            new("game_state/npcs/npc_skills.json.NPCSkillMasteryChanges", IssueSeverity.Error, "second", code: "npc_skill_mastery_initialization_confusable", actor: "name:Trainer B", expected: "Fireball", actual: "fireball", repairHint: "Trainer B: Fireball")
        };
        using var request = await WriteSkillIdentityRepairForTestAsync(issues);
        var packet = Assert.Single(request.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        var corrections = packet.GetProperty("exactFieldCorrections").EnumerateArray().ToArray();
        Assert.Equal(2, corrections.Length);
        Assert.Contains(corrections, c => c.GetProperty("expected").GetString() == "Whirlwind" && c.GetProperty("repairHint").GetString() == "Trainer A: Whirlwind");
        Assert.Contains(corrections, c => c.GetProperty("expected").GetString() == "Fireball" && c.GetProperty("repairHint").GetString() == "Trainer B: Fireball");
    }

    /// <summary>
    /// Leaves an ambiguous identity unresolved in the repair instructions.
    /// </summary>
    [Fact]
    public async Task WriteValidationRepairRequestAsync_SkillReferenceAmbiguous_InstructsStopWithoutGuessing()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {"activeSkillChanges":[{"skillName":"Fireball"},{"skillName":"FIREBALL"}]}
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {"skillMasteryChanges":[{"skillName":"fireball","newMasteryLevel":1,"newCurrentMasteryProgress":0,"newMasteryProgressNeeded":100,"masteryLeveledUp":false}]}
        """);
        var validator = new ValidationService(_fs, Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        var issues = (await validator.ValidateGameStateAsync(GameStateValidationPhase.PlayerStateFiles | GameStateValidationPhase.SkillContractConsistency))
            .Where(i => i.Code is "skill_mastery_confusable_reference" or "player_active_skill_mastery_initialization_confusable").ToList();
        using var request = await WriteSkillIdentityRepairForTestAsync(issues);
        var packet = Assert.Single(request.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        var corrections = packet.GetProperty("exactFieldCorrections").EnumerateArray().ToArray();
        Assert.NotEmpty(corrections);
        Assert.All(corrections, c => Assert.Contains("several distinct", c.GetProperty("expected").GetString(), StringComparison.Ordinal));
        Assert.Contains(packet.GetProperty("safeCorrectionRules").EnumerateArray(), r => r.GetString()!.Contains("stop", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Writes a real validation repair request and opens its immutable JSON image.
    /// </summary>
    /// <param name="issues">
    /// The identity diagnostics to pass to the production repair request writer.
    /// </param>
    /// <returns>
    /// The written request document, which the caller must dispose.
    /// </returns>
    private async Task<JsonDocument> WriteSkillIdentityRepairForTestAsync(List<ValidationIssue> issues)
    {
        var method = typeof(GameEngine).GetMethod("WriteValidationRepairRequestAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(CreateGameEngine(), new object[] { "обработки хода", issues, 1 })!;
        return JsonDocument.Parse((await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
    }
}
