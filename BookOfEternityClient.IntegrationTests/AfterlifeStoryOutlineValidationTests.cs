using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class AfterlifeStoryOutlineValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeStoryOutlineValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-story-outline-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeStoryOutline_PassesValidation()
    {
        await WriteOutlineStateAsync(BuildValidOutlineJson());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeStory);

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_story_outline_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MissingMainArc_ReportsContractIssue()
    {
        await WriteOutlineStateAsync(BuildValidOutlineJson()
            .Replace("\"mainArc\": \"Скрытые следы Крыльев Ангелов\",", string.Empty, StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeStory);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_story_outline_missing_main_arc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PlayerVisibleTextInOutline_ReportsPrivateSurfaceIssue()
    {
        await WriteOutlineStateAsync(BuildValidOutlineJson()
            .Replace("\"lastUpdatedTurn\": 9", "\"playerVisibleText\": \"Сареф уже рядом.\",\n      \"lastUpdatedTurn\": 9", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeStory);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_story_outline_player_visible_text_forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MissingLastUpdatedTurn_ReportsContractIssue()
    {
        await WriteOutlineStateAsync(BuildValidOutlineJson()
            .Replace("\"lastUpdatedTurn\": 9", "\"unusedTestMarker\": 9", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.AfterlifeStory);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_story_outline_missing_last_updated_turn", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteOutlineStateAsync(string json) =>
        _fs.WriteFileAtomicAsync(AfterlifeStoryOutlineState.StatePath, json);

    internal static string BuildValidOutlineJson() =>
        """
        {
          "schemaVersion": 1,
          "mainArc": "Скрытые следы Крыльев Ангелов",
          "realmArc": "Море Хаоса подталкивает Душу к зеркальной Обители",
          "actorSubplots": [
            {
              "actorRef": "guardian:azalia",
              "summary": "Азалия хочет проверить, не повторяет ли Душа ее старую ошибку."
            }
          ],
          "factionOrInstitutionArcs": [
            {
              "institutionRef": "shining_faction:wings_shadow",
              "summary": "Пока это только тень слухов, не player-visible факт."
            }
          ],
          "loomingThreatsOrOpportunities": [
            "Черный прилив может открыть путь к забытой Обители."
          ],
          "pendingRevelations": [
            "Имя Сарефа пока нельзя раскрывать игроку."
          ],
          "nextLikelySceneBeats": [
            "Если игрок вернется к Азалии, она предложит сцену памяти."
          ],
          "playerAgencyNotes": "Не форсировать раскрытие Сарефа; если игрок уйдет в другую ветку, обновить план.",
          "lastUpdatedTurn": 9
        }
        """;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}
