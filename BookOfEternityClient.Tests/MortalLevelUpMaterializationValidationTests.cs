using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLevelUpMaterializationValidationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalLevelUpMaterializationValidationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-mortal-level-up-materialization-" + Guid.NewGuid().ToString("N"));
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
    public async Task ValidateAcceptedTurnMortalLevelUpMaterializationAsync_FlagsXpOverThresholdWithoutLevelUp()
    {
        await WriteMortalRealmAsync();
        await WriteAcceptedTurnEnvelopeAsync();
        await WriteExperienceAsync(playerLevel: 2, totalExperience: 161, experienceForNextLevel: 150);
        await WriteStatPointsAsync(unspentStatPoints: 0, awardedThroughLevel: 2);

        var issues = await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync();

        var issue = Assert.Single(issues, issue =>
            string.Equals(issue.Code, "mortal_level_up_materialization_missing", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("game_state/player/experience.json", issue.FilePath);
        Assert.Contains("playerLevel", issue.RepairHint, StringComparison.Ordinal);
        Assert.Contains("experienceForNextLevel", issue.RepairHint, StringComparison.Ordinal);
        Assert.Contains("levelUpStatPointsAwardedThroughLevel", issue.RepairHint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAcceptedTurnMortalLevelUpMaterializationAsync_AllowsXpBelowThreshold()
    {
        await WriteMortalRealmAsync();
        await WriteAcceptedTurnEnvelopeAsync();
        await WriteExperienceAsync(playerLevel: 2, totalExperience: 149, experienceForNextLevel: 150);
        await WriteStatPointsAsync(unspentStatPoints: 0, awardedThroughLevel: 2);

        var issues = await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_level_up_materialization_missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnMortalLevelUpMaterializationAsync_AllowsAdvancedLevelWithNextThreshold()
    {
        await WriteMortalRealmAsync();
        await WriteAcceptedTurnEnvelopeAsync();
        await WriteExperienceAsync(playerLevel: 3, totalExperience: 161, experienceForNextLevel: 300);
        await WriteStatPointsAsync(unspentStatPoints: 0, awardedThroughLevel: 2);

        var issues = await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_level_up_materialization_missing", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteMortalRealmAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World"
        }
        """);
    }

    private async Task WriteAcceptedTurnEnvelopeAsync()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 19,
          "playerAction": "Сверяю имя Матея Руна с речным весовым реестром."
        }
        """);

        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 19,
          "filesModified": [
            "game_state/player/experience.json"
          ]
        }
        """);
    }

    private async Task WriteExperienceAsync(int playerLevel, int totalExperience, int experienceForNextLevel)
    {
        var currentExperience = Math.Max(0, totalExperience - 100);
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", $$"""
        {
          "playerLevel": {{playerLevel}},
          "level": {{playerLevel}},
          "currentExperience": {{currentExperience}},
          "experience": {{currentExperience}},
          "totalExperience": {{totalExperience}},
          "experienceForNextLevel": {{experienceForNextLevel}},
          "experienceGained": 12
        }
        """);
    }

    private async Task WriteStatPointsAsync(int unspentStatPoints, int awardedThroughLevel)
    {
        await _fs.WriteFileAtomicAsync("game_state/player/stat_points.json", $$"""
        {
          "unspentStatPoints": {{unspentStatPoints}},
          "levelUpStatPointsAwardedThroughLevel": {{awardedThroughLevel}}
        }
        """);
    }
}
