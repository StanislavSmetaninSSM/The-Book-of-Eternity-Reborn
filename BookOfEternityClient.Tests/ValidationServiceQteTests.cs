using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
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

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_AcceptsValidMashInputConfig()
    {
        await WriteMashInputOfferAsync();

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "qte_invalid_check_type", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_mash_input_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_AcceptsValidPatternMemoryConfig()
    {
        await WritePatternMemoryOfferAsync();

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "qte_invalid_check_type", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_pattern_memory_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Theory]
    [InlineData("emptyKeys", "qte_mash_input_keys_empty")]
    [InlineData("unsupportedKey", "qte_mash_input_key_invalid")]
    [InlineData("duplicateKey", "qte_mash_input_key_duplicate")]
    [InlineData("tooShortDuration", "qte_mash_input_duration_out_of_range")]
    [InlineData("excessiveDuration", "qte_mash_input_duration_out_of_range")]
    [InlineData("zeroTargetPresses", "qte_mash_input_target_invalid")]
    [InlineData("impossibleTargetPresses", "qte_mash_input_target_impossible")]
    [InlineData("stringPartialThreshold", "qte_mash_input_partial_threshold_invalid")]
    [InlineData("outOfRangePartialThreshold", "qte_mash_input_partial_threshold_out_of_range")]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsMalformedMashInputConfig(
        string mutation,
        string expectedCode)
    {
        await WriteMashInputOfferAsync(mutation);

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missingConfig", "qte_pattern_memory_config_missing")]
    [InlineData("emptyAlphabet", "qte_pattern_memory_alphabet_empty")]
    [InlineData("duplicateToken", "qte_pattern_memory_alphabet_duplicate")]
    [InlineData("unsupportedToken", "qte_pattern_memory_alphabet_token_invalid")]
    [InlineData("shortSequence", "qte_pattern_memory_sequence_length_out_of_range")]
    [InlineData("longSequence", "qte_pattern_memory_sequence_length_out_of_range")]
    [InlineData("shortReveal", "qte_pattern_memory_reveal_ms_out_of_range")]
    [InlineData("longReveal", "qte_pattern_memory_reveal_ms_out_of_range")]
    [InlineData("shortInputTimeout", "qte_pattern_memory_input_timeout_ms_out_of_range")]
    [InlineData("sequenceImpossibleTimeout", "qte_pattern_memory_input_timeout_ms_impossible")]
    [InlineData("negativeMistakes", "qte_pattern_memory_allowed_mistakes_out_of_range")]
    [InlineData("failureImpossibleMistakes", "qte_pattern_memory_allowed_mistakes_out_of_range")]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsMalformedPatternMemoryConfig(
        string mutation,
        string expectedCode)
    {
        await WritePatternMemoryOfferAsync(mutation);

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteMashInputOfferAsync(string? mutation = null)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var offer = JsonNode.Parse("""
        {
          "qteId": "qte_mash_test",
          "title": "Дверь захлопывается",
          "offerText": "Нужно быстро протолкнуть створку плечом.",
          "introNarrative": "Каменная дверь начинает закрываться перед героем.",
          "startChapterId": "door",
          "chapters": [
            {
              "chapterId": "door",
              "title": "Последний рывок",
              "narrative": "Остаётся короткое окно для усилия.",
              "actions": [
                {
                  "actionId": "push_door",
                  "label": "Продавить дверь",
                  "check": {
                    "type": "MashInput",
                    "baseDifficulty": 3,
                    "primaryCharacteristic": "strength",
                    "config": {
                      "keys": ["space"],
                      "durationMs": 2500,
                      "targetPresses": 12,
                      "partialThreshold": 0.5
                    }
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "open" },
                    "partial": { "terminalOutcomeId": "stuck" },
                    "fail": { "terminalOutcomeId": "caught" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "open",
              "title": "Проход открыт",
              "finalNarrative": "Дверь поддаётся.",
              "gmSummary": "Игрок успел открыть проход.",
              "responseFragment": {
                "response": "Вы врываетесь в проход.",
                "experienceGained": 30
              }
            },
            {
              "outcomeId": "stuck",
              "title": "Узкий просвет",
              "finalNarrative": "Дверь оставляет только узкую щель.",
              "gmSummary": "Игрок получил частичный исход.",
              "responseFragment": {
                "response": "Вы протискиваетесь с потерей времени.",
                "experienceGained": 5
              }
            },
            {
              "outcomeId": "caught",
              "title": "Дверь закрылась",
              "finalNarrative": "Створка захлопывается.",
              "gmSummary": "Игрок не успел продавить дверь.",
              "responseFragment": {
                "response": "Дверь отрезает путь.",
                "currentPoiseChange": -10
              }
            }
          ]
        }
        """)!.AsObject();

        var config = offer["chapters"]![0]!["actions"]![0]!["check"]!["config"]!.AsObject();
        switch (mutation)
        {
            case "emptyKeys":
                config["keys"] = new JsonArray();
                break;
            case "unsupportedKey":
                config["keys"] = new JsonArray("enter");
                break;
            case "duplicateKey":
                config["keys"] = new JsonArray("space", "space");
                break;
            case "tooShortDuration":
                config["durationMs"] = 100;
                break;
            case "excessiveDuration":
                config["durationMs"] = 25000;
                break;
            case "zeroTargetPresses":
                config["targetPresses"] = 0;
                break;
            case "impossibleTargetPresses":
                config["durationMs"] = 1000;
                config["targetPresses"] = 40;
                break;
            case "stringPartialThreshold":
                config["partialThreshold"] = "half";
                break;
            case "outOfRangePartialThreshold":
                config["partialThreshold"] = 1.25;
                break;
        }

        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, offer.ToJsonString());
    }

    private async Task WritePatternMemoryOfferAsync(string? mutation = null)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var offer = JsonNode.Parse("""
        {
          "qteId": "qte_pattern_memory_test",
          "title": "Рунный замок",
          "offerText": "Нужно запомнить вспышки рун и повторить их на камнях.",
          "introNarrative": "На арке загорается короткая последовательность знаков.",
          "startChapterId": "rune_lock",
          "chapters": [
            {
              "chapterId": "rune_lock",
              "title": "Память рун",
              "narrative": "Замок принимает только точное повторение ритма.",
              "actions": [
                {
                  "actionId": "repeat_runes",
                  "label": "Повторить узор",
                  "check": {
                    "type": "PatternMemory",
                    "baseDifficulty": 3,
                    "primaryCharacteristic": "intelligence",
                    "config": {
                      "alphabet": ["q", "w", "e", "space"],
                      "sequenceLength": 4,
                      "revealMs": 2500,
                      "inputTimeoutMs": 6000,
                      "allowedMistakes": 1
                    }
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "seal_open" },
                    "partial": { "terminalOutcomeId": "seal_flickers" },
                    "fail": { "terminalOutcomeId": "alarm" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "seal_open",
              "title": "Печать открыта",
              "finalNarrative": "Руны вспыхивают в правильном порядке, и арка открывается.",
              "gmSummary": "Игрок успешно повторил PatternMemory последовательность.",
              "responseFragment": {
                "response": "Печать пропускает вас дальше.",
                "experienceGained": 40
              }
            },
            {
              "outcomeId": "seal_flickers",
              "title": "Печать ослабла",
              "finalNarrative": "Часть рун гаснет, оставляя узкую щель.",
              "gmSummary": "Игрок частично повторил PatternMemory последовательность.",
              "responseFragment": {
                "response": "Проход открывается ненадолго.",
                "experienceGained": 10
              }
            },
            {
              "outcomeId": "alarm",
              "title": "Рунная тревога",
              "finalNarrative": "Ошибочный знак будит защитный контур.",
              "gmSummary": "Игрок провалил PatternMemory последовательность.",
              "responseFragment": {
                "response": "Замок отталкивает вас вспышкой.",
                "currentPoiseChange": -10
              }
            }
          ]
        }
        """)!.AsObject();

        var check = offer["chapters"]![0]!["actions"]![0]!["check"]!.AsObject();
        var config = check["config"]!.AsObject();
        switch (mutation)
        {
            case "missingConfig":
                check.Remove("config");
                break;
            case "emptyAlphabet":
                config["alphabet"] = new JsonArray();
                break;
            case "duplicateToken":
                config["alphabet"] = new JsonArray("q", "q");
                break;
            case "unsupportedToken":
                config["alphabet"] = new JsonArray("q", "enter");
                break;
            case "shortSequence":
                config["sequenceLength"] = 1;
                break;
            case "longSequence":
                config["sequenceLength"] = 13;
                break;
            case "shortReveal":
                config["revealMs"] = 100;
                break;
            case "longReveal":
                config["revealMs"] = 25000;
                break;
            case "shortInputTimeout":
                config["inputTimeoutMs"] = 500;
                break;
            case "sequenceImpossibleTimeout":
                config["sequenceLength"] = 8;
                config["inputTimeoutMs"] = 1500;
                break;
            case "negativeMistakes":
                config["allowedMistakes"] = -1;
                break;
            case "failureImpossibleMistakes":
                config["sequenceLength"] = 4;
                config["allowedMistakes"] = 4;
                break;
        }

        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, offer.ToJsonString());
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
