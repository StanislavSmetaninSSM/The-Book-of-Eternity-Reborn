using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeChronicleValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeChronicleValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-chronicle-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeChronicle_PassesChronicleValidation()
    {
        await WriteChronicleStateAsync(BuildValidChronicleJson());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_chronicle_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidAfterlifeChronicleScope_ReportsContractIssue()
    {
        await WriteChronicleStateAsync(BuildValidChronicleJson()
            .Replace("\"scopeType\": \"guardian_scene\"", "\"scopeType\": \"mortal_city\"", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_chronicle_invalid_scope_type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChronicleUpdateWithEventDescriptions_ReportsReadOnlyArchiveIssue()
    {
        await WriteChronicleStateAsync("""
        {
          "schemaVersion": 1,
          "afterlifeChronicleUpdates": [
            {
              "chronicleId": "guardian_scene_mirror",
              "scopeType": "guardian_scene",
              "scopeId": "guardian_mirror",
              "displayName": "Сцена Хранителя Зеркал",
              "eventDescriptions": ["GM пытается переписать архив напрямую."],
              "lastEventsDescription": "[Turn 5] Игрок услышал зов зеркал.",
              "persistentConsequences": [],
              "openThreads": [],
              "lastUpdatedTurn": 5
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_chronicle_update_event_descriptions_forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChronicleMissingLastEventsDescription_ReportsContractIssue()
    {
        await WriteChronicleStateAsync(BuildValidChronicleJson()
            .Replace("\"lastEventsDescription\": \"[Turn 5] Игрок услышал зов зеркал.\",", string.Empty, StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_chronicle_missing_last_events_description", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ChroniclePlayerTextWithInternalEnglishRealmTerm_ReportsPlayerFacingIssue()
    {
        await WriteChronicleStateAsync(BuildValidChronicleJson()
            .Replace(
                "Зал отражений запомнил голос игрока.",
                "Свод остается первой afterlife-точкой игрока.",
                StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_chronicle_player_text_internal_term", StringComparison.OrdinalIgnoreCase) &&
            (issue.FilePath ?? string.Empty).Contains("persistentConsequences[0]", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteChronicleStateAsync(string json) =>
        _fs.WriteFileAtomicAsync(AfterlifeChronicleState.StatePath, json);

    private static string BuildValidChronicleJson() =>
        """
        {
          "schemaVersion": 1,
          "chronicles": [
            {
              "chronicleId": "guardian_scene_mirror",
              "scopeType": "guardian_scene",
              "scopeId": "guardian_mirror",
              "displayName": "Сцена Хранителя Зеркал",
              "eventDescriptions": [
                "[Turn 4] Игрок впервые вошел в зал отражений."
              ],
              "lastEventsDescription": "[Turn 5] Игрок услышал зов зеркал.",
              "persistentConsequences": [
                "Зал отражений запомнил голос игрока."
              ],
              "openThreads": [
                "Понять, почему зеркала зовут игрока."
              ],
              "lastUpdatedTurn": 5
            }
          ]
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
