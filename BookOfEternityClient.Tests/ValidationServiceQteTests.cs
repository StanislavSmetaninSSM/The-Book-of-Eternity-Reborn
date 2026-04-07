using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ValidationServiceQteTests : IDisposable
{
    private const string QteNormalizerBackupDirectory = "game_state/control/qte_normalizer_backups";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ValidationServiceQteTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-qte-validator-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_DoesNotMislabelBrokenClientRuntimeAsOfferJsonFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, """
        {
          "qteId": "qte_bridge",
          "title": "Bridge Escape",
          "offerText": "A cinematic moment begins.",
          "introNarrative": "You leap toward the bridge.",
          "startChapterId": "chapter_1",
          "chapters": [
            {
              "chapterId": "chapter_1",
              "title": "Bridge",
              "narrative": "The bridge shakes.",
              "actions": [
                {
                  "actionId": "jump",
                  "label": "Jump",
                  "successText": "You make the jump.",
                  "partialText": "You barely hold on.",
                  "failText": "You fall.",
                  "check": {
                    "type": "TimingBar",
                    "baseDifficulty": 2,
                    "primaryCharacteristic": "dexterity"
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "done_success" },
                    "partial": { "terminalOutcomeId": "done_partial" },
                    "fail": { "terminalOutcomeId": "done_fail" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "done_success",
              "title": "Success",
              "finalNarrative": "You survive.",
              "gmSummary": "Success outcome.",
              "responseFragment": {
                "experienceGained": 100,
                "response": "You survive."
              }
            },
            {
              "outcomeId": "done_partial",
              "title": "Partial",
              "finalNarrative": "You survive, wounded.",
              "gmSummary": "Partial outcome.",
              "responseFragment": {
                "experienceGained": 10,
                "response": "You survive, wounded."
              }
            },
            {
              "outcomeId": "done_fail",
              "title": "Fail",
              "finalNarrative": "You fall.",
              "gmSummary": "Fail outcome.",
              "responseFragment": {
                "response": "You fall."
              }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, "{ invalid qte runtime");

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue => issue.Code == "qte_offer_invalid_json");
    }

    [Fact]
    public async Task ValidateGameStateAsync_IgnoresQteNormalizerBackupArtifacts()
    {
        await _fs.WriteFileAtomicAsync($"{QteNormalizerBackupDirectory}/stale_backup.json", "{ invalid backup json");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.StartsWith(QteNormalizerBackupDirectory + "/", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
