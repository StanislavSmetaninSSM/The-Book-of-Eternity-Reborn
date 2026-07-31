using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
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

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_AcceptsValidRhythmPulseConfig()
    {
        await WriteRhythmPulseOfferAsync();

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "qte_invalid_check_type", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_rhythm_pulse_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_AcceptsValidPrecisionChoiceConfig()
    {
        await WritePrecisionChoiceOfferAsync();

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "qte_invalid_check_type", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_precision_choice_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_AcceptsValidStealthNoiseConfig()
    {
        await WriteStealthNoiseOfferAsync();

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "qte_invalid_check_type", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_stealth_noise_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_AcceptsValidLockPinSetConfig()
    {
        await WriteLockPinSetOfferAsync();

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "qte_invalid_check_type", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_lock_pin_set_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_AcceptsValidScoreModel()
    {
        await WriteScoredQteOfferAsync();

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("qte_score_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Theory]
    [InlineData("duplicateMetricId", "qte_score_metric_duplicate")]
    [InlineData("invalidMetricId", "qte_score_metric_id_invalid")]
    [InlineData("invalidMetricBounds", "qte_score_metric_invalid_bounds")]
    [InlineData("initialOutsideBounds", "qte_score_metric_initial_out_of_bounds")]
    [InlineData("invalidVisibility", "qte_score_metric_visibility_invalid")]
    [InlineData("unknownDeltaMetric", "qte_score_delta_unknown_metric")]
    [InlineData("invalidGradeDeltaKey", "qte_score_delta_invalid_grade")]
    [InlineData("invalidDeltaValue", "qte_score_delta_invalid_delta")]
    [InlineData("unknownThresholdMetric", "qte_score_rank_threshold_unknown_metric")]
    [InlineData("impossibleThreshold", "qte_score_rank_threshold_unsatisfiable")]
    [InlineData("duplicateRankId", "qte_score_rank_duplicate")]
    [InlineData("missingFallbackRank", "qte_score_rank_missing_fallback")]
    [InlineData("duplicateRankOrderId", "qte_score_rank_order_duplicate")]
    [InlineData("unknownRankOrderId", "qte_score_rank_order_unknown_rank")]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsMalformedScoreModel(string mutation, string expectedCode)
    {
        await WriteScoredQteOfferAsync(mutation);

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("scoreModel", StringComparison.Ordinal));
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

    [Theory]
    [InlineData("missingConfig", "qte_rhythm_pulse_config_missing")]
    [InlineData("zeroPulseCount", "qte_rhythm_pulse_pulse_count_out_of_range")]
    [InlineData("negativePulseCount", "qte_rhythm_pulse_pulse_count_out_of_range")]
    [InlineData("tooFastBeatInterval", "qte_rhythm_pulse_beat_interval_ms_out_of_range")]
    [InlineData("tooSlowBeatInterval", "qte_rhythm_pulse_beat_interval_ms_out_of_range")]
    [InlineData("zeroHitWindow", "qte_rhythm_pulse_hit_window_ms_out_of_range")]
    [InlineData("overlappingHitWindow", "qte_rhythm_pulse_hit_window_ms_overlaps")]
    [InlineData("negativeMisses", "qte_rhythm_pulse_allowed_misses_out_of_range")]
    [InlineData("failureImpossibleMisses", "qte_rhythm_pulse_allowed_misses_out_of_range")]
    [InlineData("unsupportedPatternVariation", "qte_rhythm_pulse_pattern_variation_invalid")]
    [InlineData("malformedPatternVariation", "qte_rhythm_pulse_pattern_variation_invalid")]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsMalformedRhythmPulseConfig(
        string mutation,
        string expectedCode)
    {
        await WriteRhythmPulseOfferAsync(mutation);

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missingConfig", "qte_precision_choice_config_missing")]
    [InlineData("missingChoices", "qte_precision_choice_choices_missing")]
    [InlineData("singleChoice", "qte_precision_choice_choices_out_of_range")]
    [InlineData("tooManyChoices", "qte_precision_choice_choices_out_of_range")]
    [InlineData("nonObjectChoice", "qte_precision_choice_choice_invalid")]
    [InlineData("missingChoiceId", "qte_precision_choice_choice_id_missing")]
    [InlineData("duplicateChoiceId", "qte_precision_choice_choice_id_duplicate")]
    [InlineData("missingChoiceLabel", "qte_precision_choice_choice_label_missing")]
    [InlineData("invalidChoiceGrade", "qte_precision_choice_choice_grade_invalid")]
    [InlineData("missingCorrectChoiceId", "qte_precision_choice_correct_choice_missing")]
    [InlineData("unknownCorrectChoiceId", "qte_precision_choice_correct_choice_unknown")]
    [InlineData("correctChoiceNotSuccess", "qte_precision_choice_correct_choice_not_success")]
    [InlineData("missingTimeout", "qte_precision_choice_timeout_missing")]
    [InlineData("stringTimeout", "qte_precision_choice_timeout_invalid")]
    [InlineData("tooShortTimeout", "qte_precision_choice_timeout_out_of_range")]
    [InlineData("tooLongTimeout", "qte_precision_choice_timeout_out_of_range")]
    [InlineData("timeoutSuccess", "qte_precision_choice_timeout_grade_invalid")]
    [InlineData("timeoutUnknown", "qte_precision_choice_timeout_grade_invalid")]
    [InlineData("nonArrayDecoyHints", "qte_precision_choice_decoy_hints_invalid")]
    [InlineData("unknownDecoyHint", "qte_precision_choice_decoy_hint_unknown_choice")]
    [InlineData("successDecoyHint", "qte_precision_choice_decoy_hint_success_choice")]
    [InlineData("emptyDecoyHint", "qte_precision_choice_decoy_hint_invalid")]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsMalformedPrecisionChoiceConfig(
        string mutation,
        string expectedCode)
    {
        await WritePrecisionChoiceOfferAsync(mutation);

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missingConfig", "qte_stealth_noise_config_missing")]
    [InlineData("nonObjectConfig", "qte_stealth_noise_config_missing")]
    [InlineData("missingDuration", "qte_stealth_noise_duration_missing")]
    [InlineData("tooShortDuration", "qte_stealth_noise_duration_out_of_range")]
    [InlineData("tooLongDuration", "qte_stealth_noise_duration_out_of_range")]
    [InlineData("negativeStartingNoise", "qte_stealth_noise_starting_noise_out_of_range")]
    [InlineData("startingNoiseAboveThreshold", "qte_stealth_noise_starting_noise_above_threshold")]
    [InlineData("missingDangerThreshold", "qte_stealth_noise_danger_threshold_missing")]
    [InlineData("zeroDangerThreshold", "qte_stealth_noise_danger_threshold_out_of_range")]
    [InlineData("dangerThresholdTooHigh", "qte_stealth_noise_danger_threshold_out_of_range")]
    [InlineData("zeroDrift", "qte_stealth_noise_drift_out_of_range")]
    [InlineData("excessiveDrift", "qte_stealth_noise_drift_out_of_range")]
    [InlineData("zeroRecovery", "qte_stealth_noise_recovery_out_of_range")]
    [InlineData("excessiveRecovery", "qte_stealth_noise_recovery_out_of_range")]
    [InlineData("negativeAllowance", "qte_stealth_noise_allowed_over_threshold_out_of_range")]
    [InlineData("allowanceExceedsDuration", "qte_stealth_noise_allowed_over_threshold_out_of_range")]
    [InlineData("missingGradeThresholds", "qte_stealth_noise_grade_thresholds_missing")]
    [InlineData("missingSuccessMaxNoise", "qte_stealth_noise_grade_success_max_noise_missing")]
    [InlineData("partialNoiseBelowSuccess", "qte_stealth_noise_grade_noise_not_monotonic")]
    [InlineData("partialOverBelowSuccess", "qte_stealth_noise_grade_over_threshold_not_monotonic")]
    [InlineData("emptyRecoveryLabel", "qte_stealth_noise_recovery_label_invalid")]
    [InlineData("unsupportedRecoveryKey", "qte_stealth_noise_recovery_key_invalid")]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsMalformedStealthNoiseConfig(
        string mutation,
        string expectedCode)
    {
        await WriteStealthNoiseOfferAsync(mutation);

        var issues = await _validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missingConfig", "qte_lock_pin_set_config_missing")]
    [InlineData("nonObjectConfig", "qte_lock_pin_set_config_missing")]
    [InlineData("pinCountTooLow", "qte_lock_pin_set_pin_count_out_of_range")]
    [InlineData("pinCountTooHigh", "qte_lock_pin_set_pin_count_out_of_range")]
    [InlineData("missingPinWindows", "qte_lock_pin_set_pin_windows_missing")]
    [InlineData("wrongPinWindowCount", "qte_lock_pin_set_pin_windows_count_mismatch")]
    [InlineData("nonObjectPinWindow", "qte_lock_pin_set_pin_window_invalid")]
    [InlineData("unorderedPinWindow", "qte_lock_pin_set_pin_window_bounds_invalid")]
    [InlineData("outOfRangePinWindow", "qte_lock_pin_set_pin_window_bounds_out_of_range")]
    [InlineData("pinNumberMismatch", "qte_lock_pin_set_pin_window_pin_mismatch")]
    [InlineData("tooShortTimer", "qte_lock_pin_set_timer_out_of_range")]
    [InlineData("tooLongTimer", "qte_lock_pin_set_timer_out_of_range")]
    [InlineData("zeroDurability", "qte_lock_pin_set_durability_out_of_range")]
    [InlineData("excessiveDurability", "qte_lock_pin_set_durability_out_of_range")]
    [InlineData("negativeMistakes", "qte_lock_pin_set_max_mistakes_out_of_range")]
    [InlineData("mistakesExceedDurability", "qte_lock_pin_set_max_mistakes_out_of_range")]
    [InlineData("negativeDrift", "qte_lock_pin_set_drift_out_of_range")]
    [InlineData("excessiveDrift", "qte_lock_pin_set_drift_out_of_range")]
    [InlineData("missingGradeThresholds", "qte_lock_pin_set_grade_thresholds_missing")]
    [InlineData("missingSuccessTime", "qte_lock_pin_set_grade_success_time_missing")]
    [InlineData("successTimeExceedsTimer", "qte_lock_pin_set_grade_success_time_out_of_range")]
    [InlineData("partialTimeBelowSuccess", "qte_lock_pin_set_grade_time_not_monotonic")]
    [InlineData("partialMistakesBelowSuccess", "qte_lock_pin_set_grade_mistakes_not_monotonic")]
    [InlineData("unsupportedAdjustKey", "qte_lock_pin_set_adjust_key_invalid")]
    [InlineData("unsupportedSetKey", "qte_lock_pin_set_set_key_invalid")]
    [InlineData("sameAdjustAndSetKey", "qte_lock_pin_set_keys_not_distinct")]
    [InlineData("emptyPinLabel", "qte_lock_pin_set_pin_label_invalid")]
    [InlineData("emptyDurabilityLabel", "qte_lock_pin_set_durability_label_invalid")]
    [InlineData("emptyWarningLabel", "qte_lock_pin_set_warning_label_invalid")]
    [InlineData("missingRoutingPartial", "qte_missing_required_branch")]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsMalformedLockPinSetConfig(
        string mutation,
        string expectedCode)
    {
        await WriteLockPinSetOfferAsync(mutation);

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

    private async Task WriteScoredQteOfferAsync(string? mutation = null)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var offer = JsonNode.Parse("""
        {
          "qteId": "qte_scored_manor",
          "title": "Тихое проникновение",
          "offerText": "Нужно пройти двор, собрать улики и уйти до тревоги.",
          "introNarrative": "Фонари качаются над мокрым двором усадьбы.",
          "startChapterId": "yard",
          "scoreModel": {
            "metrics": [
              { "id": "stealth", "label": "Скрытность", "initial": 50, "min": 0, "max": 100, "visibility": "always" },
              { "id": "evidence", "label": "Улики", "initial": 0, "min": 0, "max": 100, "visibility": "final" },
              { "id": "alarm", "label": "Тревога", "initial": 10, "min": 0, "max": 100, "visibility": "always" }
            ],
            "rankOrder": ["best", "good", "partial", "bad"],
            "ranks": [
              {
                "id": "best",
                "label": "Безупречный исход",
                "summary": "Усадьба осталась спокойной, а улики собраны чисто.",
                "allOf": [
                  { "metric": "stealth", "op": ">=", "value": 85 },
                  { "metric": "alarm", "op": "<=", "value": 20 }
                ]
              },
              {
                "id": "good",
                "label": "Удачный исход",
                "summary": "Цель достигнута, тревога осталась управляемой.",
                "allOf": [
                  { "metric": "stealth", "op": ">=", "value": 55 },
                  { "metric": "alarm", "op": "<=", "value": 50 }
                ]
              },
              {
                "id": "partial",
                "label": "Неровный исход",
                "summary": "Победа есть, но следы заметны.",
                "allOf": [
                  { "metric": "stealth", "op": ">=", "value": 20 }
                ]
              },
              {
                "id": "bad",
                "label": "Провальный исход",
                "summary": "Сцена завершилась тяжёлыми последствиями.",
                "fallback": true
              }
            ]
          },
          "chapters": [
            {
              "chapterId": "yard",
              "title": "Двор",
              "narrative": "Патруль разворачивается у ворот.",
              "actions": [
                {
                  "actionId": "cross_yard",
                  "label": "Пройти между фонарями",
                  "check": {
                    "type": "BranchChoice",
                    "baseDifficulty": 2,
                    "primaryCharacteristic": "dexterity",
                    "config": { "choiceGrade": "success" }
                  },
                  "scoreDeltas": {
                    "success": [
                      { "metric": "stealth", "delta": 25 },
                      { "metric": "alarm", "delta": -10 }
                    ],
                    "partial": [
                      { "metric": "stealth", "delta": -5 },
                      { "metric": "evidence", "delta": 5 }
                    ],
                    "fail": [
                      { "metric": "stealth", "delta": -20 },
                      { "metric": "alarm", "delta": 30 }
                    ]
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "clean_exit" },
                    "partial": { "terminalOutcomeId": "narrow_exit" },
                    "fail": { "terminalOutcomeId": "caught" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "clean_exit",
              "title": "Чистый уход",
              "finalNarrative": "Вы исчезаете до смены караула.",
              "gmSummary": "Игрок прошёл scored QTE с успехом.",
              "responseFragment": {
                "response": "Вы уходите из усадьбы с уликами.",
                "experienceGained": 25
              }
            },
            {
              "outcomeId": "narrow_exit",
              "title": "Уход с помехами",
              "finalNarrative": "Вы выбираетесь через мокрый сад.",
              "gmSummary": "Игрок прошёл scored QTE частично.",
              "responseFragment": {
                "response": "Вы уходите, но оставляете следы.",
                "experienceGained": 5
              }
            },
            {
              "outcomeId": "caught",
              "title": "Поднятая тревога",
              "finalNarrative": "Патруль замечает движение.",
              "gmSummary": "Игрок провалил scored QTE.",
              "responseFragment": {
                "response": "Тревога поднята."
              }
            }
          ]
        }
        """)!.AsObject();

        var scoreModel = offer["scoreModel"]!.AsObject();
        var metrics = scoreModel["metrics"]!.AsArray();
        var ranks = scoreModel["ranks"]!.AsArray();
        var action = offer["chapters"]![0]!["actions"]![0]!.AsObject();
        var scoreDeltas = action["scoreDeltas"]!.AsObject();

        switch (mutation)
        {
            case "duplicateMetricId":
                metrics.Add(JsonNode.Parse("""
                { "id": "stealth", "label": "Повтор", "initial": 0, "min": 0, "max": 10, "visibility": "always" }
                """));
                break;
            case "invalidMetricId":
                metrics[0]!["id"] = "bad id";
                break;
            case "invalidMetricBounds":
                metrics[0]!["min"] = 100;
                metrics[0]!["max"] = 0;
                break;
            case "initialOutsideBounds":
                metrics[0]!["initial"] = 120;
                break;
            case "invalidVisibility":
                metrics[0]!["visibility"] = "debug";
                break;
            case "unknownDeltaMetric":
                scoreDeltas["success"]![0]!["metric"] = "ghost";
                break;
            case "invalidGradeDeltaKey":
                scoreDeltas["critical"] = new JsonArray(JsonNode.Parse("""{ "metric": "stealth", "delta": 1 }"""));
                break;
            case "invalidDeltaValue":
                scoreDeltas["success"]![0]!["delta"] = "many";
                break;
            case "unknownThresholdMetric":
                ranks[0]!["allOf"]![0]!["metric"] = "ghost";
                break;
            case "impossibleThreshold":
                ranks[0]!["allOf"]![0]!["op"] = ">";
                ranks[0]!["allOf"]![0]!["value"] = 100;
                break;
            case "duplicateRankId":
                ranks.Add(JsonNode.Parse("""
                { "id": "good", "label": "Повтор", "summary": "Повтор.", "allOf": [{ "metric": "stealth", "op": ">=", "value": 1 }] }
                """));
                break;
            case "missingFallbackRank":
                ranks[3]!.AsObject().Remove("fallback");
                ranks[3]!["allOf"] = new JsonArray(JsonNode.Parse("""{ "metric": "stealth", "op": ">=", "value": 0 }"""));
                break;
            case "duplicateRankOrderId":
                scoreModel["rankOrder"] = new JsonArray("best", "good", "good", "partial", "bad");
                break;
            case "unknownRankOrderId":
                scoreModel["rankOrder"] = new JsonArray("best", "legendary", "good", "partial", "bad");
                break;
        }

        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, offer.ToJsonString());
    }

    private async Task WriteRhythmPulseOfferAsync(string? mutation = null)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var offer = JsonNode.Parse("""
        {
          "qteId": "qte_rhythm_pulse_test",
          "title": "Ритм печати",
          "offerText": "Нужно подстроить дыхание под пульсацию магической печати.",
          "introNarrative": "Свет на камнях вспыхивает короткими ударами, задавая опасный ритм.",
          "startChapterId": "seal_pulse",
          "chapters": [
            {
              "chapterId": "seal_pulse",
              "title": "Пульсация резонанса",
              "narrative": "Каждая вспышка открывает короткое окно для движения.",
              "actions": [
                {
                  "actionId": "match_pulse",
                  "label": "Подстроиться под ритм",
                  "check": {
                    "type": "RhythmPulse",
                    "baseDifficulty": 3,
                    "primaryCharacteristic": "perception",
                    "config": {
                      "pulseCount": 4,
                      "beatIntervalMs": 650,
                      "hitWindowMs": 120,
                      "allowedMisses": 1,
                      "patternVariation": "steady"
                    }
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "resonance_matched" },
                    "partial": { "terminalOutcomeId": "resonance_wavers" },
                    "fail": { "terminalOutcomeId": "resonance_breaks" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "resonance_matched",
              "title": "Ритм совпал",
              "finalNarrative": "Печать принимает ровный пульс и пропускает героя.",
              "gmSummary": "Игрок успешно прошёл RhythmPulse последовательность.",
              "responseFragment": {
                "response": "Вы входите в резонанс с печатью.",
                "experienceGained": 40
              }
            },
            {
              "outcomeId": "resonance_wavers",
              "title": "Ритм дрогнул",
              "finalNarrative": "Печать пропускает героя рывком, но резонанс обжигает ладони.",
              "gmSummary": "Игрок частично прошёл RhythmPulse последовательность.",
              "responseFragment": {
                "response": "Вы проходите через нестабильный ритм.",
                "experienceGained": 10
              }
            },
            {
              "outcomeId": "resonance_breaks",
              "title": "Ритм сорван",
              "finalNarrative": "Печать сбивает шаг и отбрасывает героя от прохода.",
              "gmSummary": "Игрок провалил RhythmPulse последовательность.",
              "responseFragment": {
                "response": "Печать отбрасывает вас вспышкой.",
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
            case "zeroPulseCount":
                config["pulseCount"] = 0;
                break;
            case "negativePulseCount":
                config["pulseCount"] = -2;
                break;
            case "tooFastBeatInterval":
                config["beatIntervalMs"] = 100;
                break;
            case "tooSlowBeatInterval":
                config["beatIntervalMs"] = 5000;
                break;
            case "zeroHitWindow":
                config["hitWindowMs"] = 0;
                break;
            case "overlappingHitWindow":
                config["beatIntervalMs"] = 500;
                config["hitWindowMs"] = 250;
                break;
            case "negativeMisses":
                config["allowedMisses"] = -1;
                break;
            case "failureImpossibleMisses":
                config["pulseCount"] = 4;
                config["allowedMisses"] = 4;
                break;
            case "unsupportedPatternVariation":
                config["patternVariation"] = "random";
                break;
            case "malformedPatternVariation":
                config["patternVariation"] = new JsonObject { ["mode"] = "steady" };
                break;
        }

        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, offer.ToJsonString());
    }

    private async Task WritePrecisionChoiceOfferAsync(string? mutation = null)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var offer = JsonNode.Parse("""
        {
          "qteId": "qte_precision_choice_test",
          "title": "Погоня у складов",
          "offerText": "Нужно выбрать проход, пока преследователи сужают кольцо.",
          "introNarrative": "За спиной лязгают цепи, впереди три тёмных переулка.",
          "startChapterId": "warehouse_chase",
          "chapters": [
            {
              "chapterId": "warehouse_chase",
              "title": "Три прохода",
              "narrative": "Один путь ведёт к открытой воде, остальные заводят в ловушку.",
              "actions": [
                {
                  "actionId": "choose_alley",
                  "label": "Выбрать проход",
                  "check": {
                    "type": "PrecisionChoice",
                    "baseDifficulty": 3,
                    "primaryCharacteristic": "perception",
                    "config": {
                      "timeoutMs": 6000,
                      "timeoutGrade": "fail",
                      "correctChoiceId": "salt_wind",
                      "choices": [
                        {
                          "id": "salt_wind",
                          "label": "Проход, откуда тянет солью и ветром",
                          "description": "Сквозняк указывает на открытую набережную.",
                          "grade": "success",
                          "hint": "Пыль уходит внутрь прохода."
                        },
                        {
                          "id": "red_lantern",
                          "label": "Арка под красным фонарём",
                          "description": "Свет обещает укрытие, но двор слишком тихий.",
                          "grade": "partial",
                          "hint": "Фонарь отвлекает преследователей, но двор узкий."
                        },
                        {
                          "id": "dry_well",
                          "label": "Лестница к сухому колодцу",
                          "grade": "fail"
                        }
                      ],
                      "decoyHints": [
                        {
                          "choiceId": "red_lantern",
                          "hint": "Фонарь выглядит безопасным, но тень за ним не движется."
                        }
                      ]
                    }
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "escape_clean" },
                    "partial": { "terminalOutcomeId": "escape_scraped" },
                    "fail": { "terminalOutcomeId": "caught" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "escape_clean",
              "title": "Чистый отрыв",
              "finalNarrative": "Солёный ветер выводит героя к воде.",
              "gmSummary": "Игрок выбрал правильный PrecisionChoice проход.",
              "responseFragment": {
                "response": "Вы отрываетесь от преследователей.",
                "experienceGained": 40
              }
            },
            {
              "outcomeId": "escape_scraped",
              "title": "Узкий двор",
              "finalNarrative": "Герой теряет время, но всё же перелезает через низкую стену.",
              "gmSummary": "Игрок получил частичный PrecisionChoice исход.",
              "responseFragment": {
                "response": "Вы уходите с царапинами и потерей темпа.",
                "experienceGained": 10
              }
            },
            {
              "outcomeId": "caught",
              "title": "Кольцо сомкнулось",
              "finalNarrative": "Погоня загоняет героя в тупик.",
              "gmSummary": "Игрок провалил PrecisionChoice выбор.",
              "responseFragment": {
                "response": "Вы попадаете в засаду.",
                "currentPoiseChange": -10
              }
            }
          ]
        }
        """)!.AsObject();

        var check = offer["chapters"]![0]!["actions"]![0]!["check"]!.AsObject();
        var config = check["config"]!.AsObject();
        var choices = config["choices"]!.AsArray();
        switch (mutation)
        {
            case "missingConfig":
                check.Remove("config");
                break;
            case "missingChoices":
                config.Remove("choices");
                break;
            case "singleChoice":
                config["choices"] = new JsonArray(choices[0]!.DeepClone());
                break;
            case "tooManyChoices":
                config["choices"] = new JsonArray(
                    choices[0]!.DeepClone(),
                    choices[1]!.DeepClone(),
                    choices[2]!.DeepClone(),
                    JsonNode.Parse("""{ "id": "c4", "label": "Вариант 4", "grade": "fail" }"""),
                    JsonNode.Parse("""{ "id": "c5", "label": "Вариант 5", "grade": "fail" }"""),
                    JsonNode.Parse("""{ "id": "c6", "label": "Вариант 6", "grade": "fail" }"""),
                    JsonNode.Parse("""{ "id": "c7", "label": "Вариант 7", "grade": "fail" }"""),
                    JsonNode.Parse("""{ "id": "c8", "label": "Вариант 8", "grade": "fail" }"""),
                    JsonNode.Parse("""{ "id": "c9", "label": "Вариант 9", "grade": "fail" }"""));
                break;
            case "nonObjectChoice":
                choices[1] = "red_lantern";
                break;
            case "missingChoiceId":
                choices[1]!.AsObject().Remove("id");
                break;
            case "duplicateChoiceId":
                choices[1]!["id"] = "salt_wind";
                break;
            case "missingChoiceLabel":
                choices[1]!.AsObject().Remove("label");
                break;
            case "invalidChoiceGrade":
                choices[1]!["grade"] = "near";
                break;
            case "missingCorrectChoiceId":
                config.Remove("correctChoiceId");
                break;
            case "unknownCorrectChoiceId":
                config["correctChoiceId"] = "missing_path";
                break;
            case "correctChoiceNotSuccess":
                config["correctChoiceId"] = "red_lantern";
                break;
            case "missingTimeout":
                config.Remove("timeoutMs");
                break;
            case "stringTimeout":
                config["timeoutMs"] = "soon";
                break;
            case "tooShortTimeout":
                config["timeoutMs"] = 500;
                break;
            case "tooLongTimeout":
                config["timeoutMs"] = 45000;
                break;
            case "timeoutSuccess":
                config["timeoutGrade"] = "success";
                break;
            case "timeoutUnknown":
                config["timeoutGrade"] = "near";
                break;
            case "nonArrayDecoyHints":
                config["decoyHints"] = new JsonObject { ["red_lantern"] = "Ложная подсказка" };
                break;
            case "unknownDecoyHint":
                config["decoyHints"] = new JsonArray(new JsonObject
                {
                    ["choiceId"] = "missing_path",
                    ["hint"] = "Кажется безопасным."
                });
                break;
            case "successDecoyHint":
                config["decoyHints"] = new JsonArray(new JsonObject
                {
                    ["choiceId"] = "salt_wind",
                    ["hint"] = "Правильный путь не должен быть decoyHint."
                });
                break;
            case "emptyDecoyHint":
                config["decoyHints"] = new JsonArray(new JsonObject
                {
                    ["choiceId"] = "red_lantern",
                    ["hint"] = " "
                });
                break;
        }

        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, offer.ToJsonString());
    }

    private async Task WriteStealthNoiseOfferAsync(string? mutation = null)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var offer = JsonNode.Parse("""
        {
          "qteId": "qte_stealth_noise_test",
          "title": "Тихий коридор",
          "offerText": "Нужно пройти мимо спящего стражника, удерживая шум ниже опасной черты.",
          "introNarrative": "Пол под ногами скрипит, а стражник дышит совсем рядом.",
          "startChapterId": "silent_hall",
          "chapters": [
            {
              "chapterId": "silent_hall",
              "title": "Скрипучий настил",
              "narrative": "Каждый шаг наращивает шум, и нужно вовремя замирать.",
              "actions": [
                {
                  "actionId": "cross_floor",
                  "label": "Пересечь настил",
                  "check": {
                    "type": "StealthNoise",
                    "baseDifficulty": 3,
                    "primaryCharacteristic": "dexterity",
                    "config": {
                      "durationMs": 8000,
                      "startingNoise": 18,
                      "dangerThreshold": 70,
                      "noiseDriftPerSecond": 9,
                      "recoveryPerInput": 12,
                      "allowedOverThresholdMs": 900,
                      "recoveryKey": "space",
                      "recoveryLabel": "замереть и распределить вес",
                      "warningLabel": "Доски начинают отвечать резким скрипом.",
                      "gradeThresholds": {
                        "successMaxNoise": 48,
                        "successMaxOverThresholdMs": 0,
                        "partialMaxNoise": 70,
                        "partialMaxOverThresholdMs": 900
                      }
                    }
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "silent_passage" },
                    "partial": { "terminalOutcomeId": "guard_stirs" },
                    "fail": { "terminalOutcomeId": "alarm" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "silent_passage",
              "title": "Беззвучный проход",
              "finalNarrative": "Герой проходит настил, не разбудив стражу.",
              "gmSummary": "Игрок удержал StealthNoise ниже порога и прошёл тихо.",
              "responseFragment": {
                "response": "Вы исчезаете в дальнем коридоре.",
                "experienceGained": 40
              }
            },
            {
              "outcomeId": "guard_stirs",
              "title": "Стражник заворочался",
              "finalNarrative": "Один скрип заставляет стражника приподняться, но тревога ещё не поднята.",
              "gmSummary": "Игрок получил частичный StealthNoise исход: шум был опасен, но не сорвал сцену.",
              "responseFragment": {
                "response": "Вы проходите, оставляя за собой настороженную тишину.",
                "experienceGained": 10
              }
            },
            {
              "outcomeId": "alarm",
              "title": "Скрип поднял тревогу",
              "finalNarrative": "Пол отвечает резким треском, и стражник вскакивает.",
              "gmSummary": "Игрок провалил StealthNoise или отменил QTE; сцену нужно продолжать с поднятой тревогой.",
              "responseFragment": {
                "response": "Стража просыпается и перекрывает коридор.",
                "currentPoiseChange": -10
              }
            }
          ]
        }
        """)!.AsObject();

        var check = offer["chapters"]![0]!["actions"]![0]!["check"]!.AsObject();
        var config = check["config"]!.AsObject();
        var gradeThresholds = config["gradeThresholds"]!.AsObject();
        switch (mutation)
        {
            case "missingConfig":
                check.Remove("config");
                break;
            case "nonObjectConfig":
                check["config"] = "quiet";
                break;
            case "missingDuration":
                config.Remove("durationMs");
                break;
            case "tooShortDuration":
                config["durationMs"] = 500;
                break;
            case "tooLongDuration":
                config["durationMs"] = 45000;
                break;
            case "negativeStartingNoise":
                config["startingNoise"] = -1;
                break;
            case "startingNoiseAboveThreshold":
                config["startingNoise"] = 80;
                break;
            case "missingDangerThreshold":
                config.Remove("dangerThreshold");
                break;
            case "zeroDangerThreshold":
                config["dangerThreshold"] = 0;
                break;
            case "dangerThresholdTooHigh":
                config["dangerThreshold"] = 101;
                break;
            case "zeroDrift":
                config["noiseDriftPerSecond"] = 0;
                break;
            case "excessiveDrift":
                config["noiseDriftPerSecond"] = 101;
                break;
            case "zeroRecovery":
                config["recoveryPerInput"] = 0;
                break;
            case "excessiveRecovery":
                config["recoveryPerInput"] = 101;
                break;
            case "negativeAllowance":
                config["allowedOverThresholdMs"] = -1;
                break;
            case "allowanceExceedsDuration":
                config["allowedOverThresholdMs"] = 9000;
                break;
            case "missingGradeThresholds":
                config.Remove("gradeThresholds");
                break;
            case "missingSuccessMaxNoise":
                gradeThresholds.Remove("successMaxNoise");
                break;
            case "partialNoiseBelowSuccess":
                gradeThresholds["partialMaxNoise"] = 40;
                break;
            case "partialOverBelowSuccess":
                gradeThresholds["successMaxOverThresholdMs"] = 500;
                gradeThresholds["partialMaxOverThresholdMs"] = 100;
                break;
            case "emptyRecoveryLabel":
                config["recoveryLabel"] = " ";
                break;
            case "unsupportedRecoveryKey":
                config["recoveryKey"] = "enter";
                break;
        }

        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, offer.ToJsonString());
    }

    private async Task WriteLockPinSetOfferAsync(string? mutation = null)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", """
        {
          "qteEventsEnabled": true
        }
        """);

        var offer = JsonNode.Parse("""
        {
          "qteId": "qte_lock_pin_set_test",
          "title": "Архивный замок",
          "offerText": "Нужно выставить штифты отмычкой, пока патруль не вернулся.",
          "introNarrative": "Внутри замка сухо щёлкают старые латунные штифты.",
          "startChapterId": "archive_lock",
          "chapters": [
            {
              "chapterId": "archive_lock",
              "title": "Латунные штифты",
              "narrative": "Каждый штифт нужно поставить в своё окно без лишнего скрежета.",
              "actions": [
                {
                  "actionId": "pick_archive_lock",
                  "label": "Вскрыть архивный замок",
                  "check": {
                    "type": "LockPinSet",
                    "baseDifficulty": 3,
                    "primaryCharacteristic": "dexterity",
                    "config": {
                      "pinCount": 4,
                      "pinWindows": [
                        { "pin": 1, "min": 18, "max": 32, "label": "первый штифт" },
                        { "pin": 2, "min": 42, "max": 55, "label": "второй штифт" },
                        { "pin": 3, "min": 58, "max": 70, "label": "третий штифт" },
                        { "pin": 4, "min": 75, "max": 88, "label": "последний штифт" }
                      ],
                      "timerMs": 14000,
                      "pickDurability": 5,
                      "maxMistakes": 2,
                      "pinDriftPerSecond": 3,
                      "adjustKey": "q",
                      "setKey": "space",
                      "pinLabel": "штифт",
                      "durabilityLabel": "отмычка скрипит в пальцах",
                      "warningLabel": "Отмычка начинает гнуться.",
                      "gradeThresholds": {
                        "successMaxTimeMs": 9000,
                        "successMaxMistakes": 0,
                        "partialMaxTimeMs": 14000,
                        "partialMaxMistakes": 2
                      }
                    }
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "archive_open_silently" },
                    "partial": { "terminalOutcomeId": "archive_open_noisy" },
                    "fail": { "terminalOutcomeId": "lockpick_alarm" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "archive_open_silently",
              "title": "Замок открыт тихо",
              "finalNarrative": "Штифты становятся ровно, и дверь в архив мягко отходит.",
              "gmSummary": "Игрок чисто прошёл LockPinSet и открыл замок без шума.",
              "responseFragment": {
                "response": "Архив открывается без тревоги.",
                "experienceGained": 40
              }
            },
            {
              "outcomeId": "archive_open_noisy",
              "title": "Замок поддался со скрежетом",
              "finalNarrative": "Дверь открыта, но металл громко цепляет накладку.",
              "gmSummary": "Игрок получил частичный LockPinSet исход: замок открыт, но шум или задержка осложняет сцену.",
              "responseFragment": {
                "response": "Вы открываете архив, оставляя за собой резкий металлический звук.",
                "experienceGained": 10
              }
            },
            {
              "outcomeId": "lockpick_alarm",
              "title": "Отмычка сорвалась",
              "finalNarrative": "Отмычка ломается, и за дверью звенит тревожная пластина.",
              "gmSummary": "Игрок провалил LockPinSet или отменил QTE; сцену нужно продолжать с шумом или тревогой.",
              "responseFragment": {
                "response": "Замок клинит, а коридор отвечает тревожным звоном.",
                "currentPoiseChange": -10
              }
            }
          ]
        }
        """)!.AsObject();

        var action = offer["chapters"]![0]!["actions"]![0]!.AsObject();
        var check = action["check"]!.AsObject();
        var config = check["config"]!.AsObject();
        var windows = config["pinWindows"]!.AsArray();
        var gradeThresholds = config["gradeThresholds"]!.AsObject();
        switch (mutation)
        {
            case "missingConfig":
                check.Remove("config");
                break;
            case "nonObjectConfig":
                check["config"] = "pins";
                break;
            case "pinCountTooLow":
                config["pinCount"] = 1;
                break;
            case "pinCountTooHigh":
                config["pinCount"] = 9;
                break;
            case "missingPinWindows":
                config.Remove("pinWindows");
                break;
            case "wrongPinWindowCount":
                windows.RemoveAt(3);
                break;
            case "nonObjectPinWindow":
                windows[1] = "second pin";
                break;
            case "unorderedPinWindow":
                windows[0]!["min"] = 32;
                windows[0]!["max"] = 18;
                break;
            case "outOfRangePinWindow":
                windows[0]!["min"] = -1;
                break;
            case "pinNumberMismatch":
                windows[1]!["pin"] = 4;
                break;
            case "tooShortTimer":
                config["timerMs"] = 500;
                break;
            case "tooLongTimer":
                config["timerMs"] = 90000;
                break;
            case "zeroDurability":
                config["pickDurability"] = 0;
                break;
            case "excessiveDurability":
                config["pickDurability"] = 21;
                break;
            case "negativeMistakes":
                config["maxMistakes"] = -1;
                break;
            case "mistakesExceedDurability":
                config["pickDurability"] = 2;
                config["maxMistakes"] = 3;
                break;
            case "negativeDrift":
                config["pinDriftPerSecond"] = -0.5;
                break;
            case "excessiveDrift":
                config["pinDriftPerSecond"] = 101;
                break;
            case "missingGradeThresholds":
                config.Remove("gradeThresholds");
                break;
            case "missingSuccessTime":
                gradeThresholds.Remove("successMaxTimeMs");
                break;
            case "successTimeExceedsTimer":
                gradeThresholds["successMaxTimeMs"] = 15000;
                break;
            case "partialTimeBelowSuccess":
                gradeThresholds["successMaxTimeMs"] = 9000;
                gradeThresholds["partialMaxTimeMs"] = 8000;
                break;
            case "partialMistakesBelowSuccess":
                gradeThresholds["successMaxMistakes"] = 2;
                gradeThresholds["partialMaxMistakes"] = 1;
                break;
            case "unsupportedAdjustKey":
                config["adjustKey"] = "enter";
                break;
            case "unsupportedSetKey":
                config["setKey"] = "enter";
                break;
            case "sameAdjustAndSetKey":
                config["setKey"] = "q";
                break;
            case "emptyPinLabel":
                config["pinLabel"] = " ";
                break;
            case "emptyDurabilityLabel":
                config["durabilityLabel"] = " ";
                break;
            case "emptyWarningLabel":
                config["warningLabel"] = " ";
                break;
            case "missingRoutingPartial":
                action["routing"]!.AsObject().Remove("partial");
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
