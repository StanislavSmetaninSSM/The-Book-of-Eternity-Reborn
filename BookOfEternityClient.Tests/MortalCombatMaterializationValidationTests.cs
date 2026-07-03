using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalCombatMaterializationValidationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalCombatMaterializationValidationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-mortal-combat-materialization-" + Guid.NewGuid().ToString("N"));
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
    public async Task ValidateAcceptedTurnMortalCombatMaterializationAsync_FlagsExplicitCombatRewardWithoutCombatState()
    {
        await WriteMortalRealmAsync();
        await WriteExplicitCombatTurnAsync(includeCombatLog: false);

        var issues = await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync();

        var issue = Assert.Single(issues, issue =>
            string.Equals(issue.Code, "mortal_combat_state_missing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("game_state/combat/combat_log.json", issue.Expected, StringComparison.Ordinal);
        Assert.Contains("MORTAL_COMBAT_STATE_TEMPLATE.md", issue.RepairHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAcceptedTurnMortalCombatMaterializationAsync_AllowsExplicitCombatWhenCombatLogExists()
    {
        await WriteMortalRealmAsync();
        await WriteExplicitCombatTurnAsync(includeCombatLog: true);

        var issues = await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_combat_state_missing", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteMortalRealmAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
    }

    private async Task WriteExplicitCombatTurnAsync(bool includeCombatLog)
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 7,
          "playerAction": "Я вступаю в открытый бой с клятвенной тенью и применяю «Быстрый выпад»."
        }
        """);

        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "timestamp": "2026-07-02T01:19:17Z",
          "response": "Клятвенная тень выходит из полки. Ты вступаешь в открытый бой, пробиваешь ядро защиты и получаешь боевую награду."
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", """
        {
          "playerLevel": 2,
          "level": 2,
          "currentExperience": 29,
          "experience": 29,
          "totalExperience": 129,
          "experienceForNextLevel": 150,
          "experienceGained": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {
          "skillMasteryChanges": [
            {
              "skillName": "Быстрый выпад",
              "newMasteryLevel": 1,
              "newCurrentMasteryProgress": 3,
              "newMasteryProgressNeeded": 5,
              "masteryLeveledUp": false
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "healthPercentage": "100%",
          "energyPercentage": "74%",
          "poisePercentage": "86%",
          "currentCondition": "Собранная усталость после точного выпада",
          "activeConditions": [],
          "money": 0
        }
        """);

        var filesModified = includeCombatLog
            ? """
              "game_state/player/experience.json",
              "game_state/player/skill_mastery.json",
              "game_state/core/player_status.json",
              "game_state/combat/combat_log.json"
              """
            : """
              "game_state/player/experience.json",
              "game_state/player/skill_mastery.json",
              "game_state/core/player_status.json"
              """;

        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", $$"""
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 7,
          "filesModified": [
            {{filesModified}}
          ]
        }
        """);

        if (includeCombatLog)
        {
            await _fs.WriteFileAtomicAsync("game_state/combat/combat_log.json", """
            {
              "combat_log_markdown": "Открытый бой с клятвенной тенью завершён: Быстрый выпад пробил ядро защиты."
            }
            """);
        }
    }
}
