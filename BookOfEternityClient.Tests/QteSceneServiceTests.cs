using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QteSceneServiceTests : IDisposable
{
    private const string QteNormalizerBackupDirectory = "game_state/control/qte_normalizer_backups";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteSceneService _service;

    public QteSceneServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-qte-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new QteSceneService(
            _fs,
            new GameSettings(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<QteSceneService>.Instance);
    }

    [Theory]
    [InlineData('q', "q")]
    [InlineData('Q', "q")]
    [InlineData('й', "q")]
    [InlineData('Й', "q")]
    [InlineData('ц', "w")]
    [InlineData('Ц', "w")]
    [InlineData('у', "e")]
    [InlineData('У', "e")]
    [InlineData('ф', "a")]
    [InlineData('Ф', "a")]
    [InlineData('ы', "s")]
    [InlineData('Ы', "s")]
    [InlineData('в', "d")]
    [InlineData('В', "d")]
    public void QteKeyInput_NormalizesConsoleFallbackCharacters(char input, string expectedToken)
    {
        Assert.Equal(expectedToken, QteKeyInput.NormalizeCharacter(input));
        Assert.Equal(expectedToken, QteKeyInput.NormalizeConsoleInput(new ConsoleKeyInfo(input, 0, false, false, false)));
    }

    [Theory]
    [InlineData(ConsoleKey.Q, "Q / Й")]
    [InlineData(ConsoleKey.W, "W / Ц")]
    [InlineData(ConsoleKey.E, "E / У")]
    [InlineData(ConsoleKey.A, "A / Ф")]
    [InlineData(ConsoleKey.S, "S / Ы")]
    [InlineData(ConsoleKey.D, "D / В")]
    [InlineData(ConsoleKey.Spacebar, "Space")]
    public void QteKeyInput_FormatsPhysicalKeyLabelsWithRuFallback(ConsoleKey key, string expectedLabel)
    {
        Assert.Equal(expectedLabel, QteKeyInput.FormatPromptLabel(key));
    }

    [Theory]
    [InlineData('й', ConsoleKey.Q)]
    [InlineData('ц', ConsoleKey.W)]
    [InlineData('у', ConsoleKey.E)]
    [InlineData('ф', ConsoleKey.A)]
    [InlineData('ы', ConsoleKey.S)]
    [InlineData('в', ConsoleKey.D)]
    [InlineData(' ', ConsoleKey.Spacebar)]
    public void QteKeyInput_MatchesConsoleFallbackInputToExpectedPhysicalKey(char input, ConsoleKey expectedKey)
    {
        var keyInfo = new ConsoleKeyInfo(input, 0, false, false, false);

        Assert.True(QteKeyInput.MatchesConsoleKey(keyInfo, expectedKey));
    }

    [Fact]
    public void QteKeyInput_LeavesUnsupportedCharactersUnmatched()
    {
        Assert.Null(QteKeyInput.NormalizeCharacter('ж'));
        Assert.False(QteKeyInput.MatchesConsoleKey(new ConsoleKeyInfo('ж', 0, false, false, false), ConsoleKey.Q));
    }

    [Fact]
    public void MashInputGrade_ResolvesSuccessPartialAndFailFromMatchingPressCounts()
    {
        Assert.Equal(
            "success",
            ResolveMashInputGrade(["space"], successTarget: 5, partialTarget: 3, RepeatKey(ConsoleKey.Spacebar, 5)));
        Assert.Equal(
            "partial",
            ResolveMashInputGrade(["space"], successTarget: 5, partialTarget: 3, RepeatKey(ConsoleKey.Spacebar, 3)));
        Assert.Equal(
            "fail",
            ResolveMashInputGrade(["space"], successTarget: 5, partialTarget: 3, RepeatKey(ConsoleKey.Spacebar, 2)));
    }

    [Fact]
    public void MashInputGrade_EscapeCancelsAsFail()
    {
        var inputs = new[]
        {
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false),
            new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false),
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false),
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false)
        };

        Assert.Equal(
            "fail",
            ResolveMashInputGrade(["space"], successTarget: 3, partialTarget: 1, inputs));
    }

    [Fact]
    public void MashInputGrade_CountsRuFallbackOnlyForConfiguredQteKeys()
    {
        var inputs = new[]
        {
            new ConsoleKeyInfo('й', 0, false, false, false),
            new ConsoleKeyInfo('ц', 0, false, false, false),
            new ConsoleKeyInfo('q', 0, false, false, false),
            new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false)
        };

        Assert.Equal(
            "success",
            ResolveMashInputGrade(["q"], successTarget: 2, partialTarget: 1, inputs));
    }

    [Fact]
    public void MashInputEffectiveTarget_IsMonotonicForStatTierAndDifficulty()
    {
        var lowStatTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 3, statTier: -2);
        var highStatTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 3, statTier: 3);
        var easyDifficultyTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 1, statTier: 0);
        var hardDifficultyTarget = ComputeMashInputEffectiveTargetPresses(12, baseDifficulty: 5, statTier: 0);

        Assert.True(highStatTarget <= lowStatTarget);
        Assert.True(hardDifficultyTarget >= easyDifficultyTarget);
        Assert.Equal(6, ComputeMashInputPartialTargetPresses(successTarget: 12, partialThreshold: 0.5));
    }

    [Fact]
    public void PatternMemoryGrade_ResolvesSuccessPartialAndFailFromSequenceMatch()
    {
        var sequence = new[] { "q", "w", "e", "space" };

        Assert.Equal(
            "success",
            ResolvePatternMemoryGrade(sequence, allowedMistakes: 1, [
                Key(ConsoleKey.Q),
                Key(ConsoleKey.W),
                Key(ConsoleKey.E),
                Key(ConsoleKey.Spacebar)
            ]));
        Assert.Equal(
            "partial",
            ResolvePatternMemoryGrade(sequence, allowedMistakes: 1, [
                Key(ConsoleKey.Q),
                Key(ConsoleKey.D),
                Key(ConsoleKey.E),
                Key(ConsoleKey.Spacebar)
            ]));
        Assert.Equal(
            "fail",
            ResolvePatternMemoryGrade(sequence, allowedMistakes: 1, [
                Key(ConsoleKey.Q),
                Key(ConsoleKey.D),
                Key(ConsoleKey.A),
                Key(ConsoleKey.Spacebar)
            ]));
    }

    [Fact]
    public void PatternMemoryGrade_TimeoutResolvesAsFail()
    {
        Assert.Equal(
            "fail",
            ResolvePatternMemoryGrade(
                ["q", "w"],
                allowedMistakes: 0,
                [Key(ConsoleKey.Q), Key(ConsoleKey.W)],
                timedOut: true));
    }

    [Fact]
    public void PatternMemoryGrade_EscapeCancelsAsFail()
    {
        var inputs = new[]
        {
            Key(ConsoleKey.Q),
            new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false),
            Key(ConsoleKey.W)
        };

        Assert.Equal("fail", ResolvePatternMemoryGrade(["q", "w"], allowedMistakes: 1, inputs));
    }

    [Fact]
    public void PatternMemoryGrade_UsesRuFallbackOnlyForConfiguredQteKeys()
    {
        Assert.Equal(
            "success",
            ResolvePatternMemoryGrade(
                ["q", "space"],
                allowedMistakes: 0,
                [
                    new ConsoleKeyInfo('й', 0, false, false, false),
                    Key(ConsoleKey.Spacebar)
                ]));
        Assert.Equal(
            "fail",
            ResolvePatternMemoryGrade(
                ["q", "q"],
                allowedMistakes: 0,
                [
                    new ConsoleKeyInfo('ц', 0, false, false, false),
                    Key(ConsoleKey.Q)
                ]));
    }

    [Fact]
    public void PatternMemoryEffectiveRequirement_IsMonotonicForStatTierAndDifficulty()
    {
        var lowStat = ComputePatternMemoryEffectiveRequirement(
            sequenceLength: 6,
            revealMs: 2500,
            inputTimeoutMs: 6000,
            allowedMistakes: 1,
            baseDifficulty: 3,
            statTier: -2);
        var highStat = ComputePatternMemoryEffectiveRequirement(
            sequenceLength: 6,
            revealMs: 2500,
            inputTimeoutMs: 6000,
            allowedMistakes: 1,
            baseDifficulty: 3,
            statTier: 3);
        var easyDifficulty = ComputePatternMemoryEffectiveRequirement(
            sequenceLength: 6,
            revealMs: 2500,
            inputTimeoutMs: 6000,
            allowedMistakes: 1,
            baseDifficulty: 1,
            statTier: 0);
        var hardDifficulty = ComputePatternMemoryEffectiveRequirement(
            sequenceLength: 6,
            revealMs: 2500,
            inputTimeoutMs: 6000,
            allowedMistakes: 1,
            baseDifficulty: 5,
            statTier: 0);

        Assert.True(highStat.SequenceLength <= lowStat.SequenceLength);
        Assert.True(highStat.RevealMs >= lowStat.RevealMs);
        Assert.True(highStat.InputTimeoutMs >= lowStat.InputTimeoutMs);
        Assert.True(highStat.AllowedMistakes >= lowStat.AllowedMistakes);
        Assert.True(hardDifficulty.SequenceLength >= easyDifficulty.SequenceLength);
        Assert.True(hardDifficulty.RevealMs <= easyDifficulty.RevealMs);
        Assert.True(hardDifficulty.InputTimeoutMs <= easyDifficulty.InputTimeoutMs);
        Assert.True(hardDifficulty.AllowedMistakes <= easyDifficulty.AllowedMistakes);
    }

    [Fact]
    public void PatternMemorySequenceGeneration_IsDeterministicAndUsesAlphabet()
    {
        var first = GeneratePatternMemorySequence(["q", "w", "space"], sequenceLength: 6, seed: "qte:rune_lock:repeat");
        var second = GeneratePatternMemorySequence(["q", "w", "space"], sequenceLength: 6, seed: "qte:rune_lock:repeat");

        Assert.Equal(first, second);
        Assert.Equal(6, first.Count);
        Assert.All(first, token => Assert.Contains(token, new[] { "q", "w", "space" }));
    }

    [Fact]
    public void RhythmPulseGrade_ResolvesSuccessPartialAndFailFromPulseWindows()
    {
        var pulses = new[] { 500, 1000, 1500, 2000 };

        Assert.Equal(
            "success",
            ResolveRhythmPulseGrade(
                pulses,
                hitWindowMs: 80,
                allowedMisses: 1,
                [
                    RhythmInput(500),
                    RhythmInput(930),
                    RhythmInput(1510)
                ]));
        Assert.Equal(
            "partial",
            ResolveRhythmPulseGrade(
                pulses,
                hitWindowMs: 80,
                allowedMisses: 1,
                [
                    RhythmInput(500),
                    RhythmInput(1510)
                ]));
        Assert.Equal(
            "fail",
            ResolveRhythmPulseGrade(
                pulses,
                hitWindowMs: 80,
                allowedMisses: 1,
                [
                    RhythmInput(500)
                ]));
    }

    [Fact]
    public void RhythmPulseGrade_NoMeaningfulInputResolvesAsFail()
    {
        Assert.Equal(
            "fail",
            ResolveRhythmPulseGrade(
                [500, 1000, 1500, 2000],
                hitWindowMs: 80,
                allowedMisses: 1,
                []));
    }

    [Fact]
    public void RhythmPulseGrade_EscapeCancelsAsFail()
    {
        var inputs = new[]
        {
            RhythmInput(500),
            RhythmInput(610, ConsoleKey.Escape),
            RhythmInput(1000)
        };

        Assert.Equal(
            "fail",
            ResolveRhythmPulseGrade(
                [500, 1000, 1500, 2000],
                hitWindowMs: 80,
                allowedMisses: 1,
                inputs));
    }

    [Fact]
    public void RhythmPulseScheduleVariation_IsDeterministicAndStrictlyIncreasing()
    {
        var steady = GenerateRhythmPulseSchedule(pulseCount: 4, beatIntervalMs: 650, patternVariation: "steady");
        var swing = GenerateRhythmPulseSchedule(pulseCount: 4, beatIntervalMs: 650, patternVariation: "swing");
        var accelerating = GenerateRhythmPulseSchedule(pulseCount: 4, beatIntervalMs: 650, patternVariation: "accelerating");

        Assert.Equal(new[] { 650, 1300, 1950, 2600 }, steady);
        Assert.Equal(steady.Count, swing.Count);
        Assert.Equal(steady.Count, accelerating.Count);
        Assert.NotEqual(steady, swing);
        Assert.NotEqual(steady, accelerating);
        AssertStrictlyIncreasing(swing);
        AssertStrictlyIncreasing(accelerating);
    }

    [Fact]
    public void RhythmPulseEffectiveRequirement_IsMonotonicForStatTierAndDifficulty()
    {
        var lowStat = ComputeRhythmPulseEffectiveRequirement(
            pulseCount: 6,
            beatIntervalMs: 650,
            hitWindowMs: 120,
            allowedMisses: 1,
            baseDifficulty: 3,
            statTier: -2);
        var highStat = ComputeRhythmPulseEffectiveRequirement(
            pulseCount: 6,
            beatIntervalMs: 650,
            hitWindowMs: 120,
            allowedMisses: 1,
            baseDifficulty: 3,
            statTier: 3);
        var easyDifficulty = ComputeRhythmPulseEffectiveRequirement(
            pulseCount: 6,
            beatIntervalMs: 650,
            hitWindowMs: 120,
            allowedMisses: 1,
            baseDifficulty: 1,
            statTier: 0);
        var hardDifficulty = ComputeRhythmPulseEffectiveRequirement(
            pulseCount: 6,
            beatIntervalMs: 650,
            hitWindowMs: 120,
            allowedMisses: 1,
            baseDifficulty: 5,
            statTier: 0);

        Assert.True(highStat.PulseCount <= lowStat.PulseCount);
        Assert.True(highStat.HitWindowMs >= lowStat.HitWindowMs);
        Assert.True(highStat.AllowedMisses >= lowStat.AllowedMisses);
        Assert.True(hardDifficulty.PulseCount >= easyDifficulty.PulseCount);
        Assert.True(hardDifficulty.HitWindowMs <= easyDifficulty.HitWindowMs);
        Assert.True(hardDifficulty.AllowedMisses <= easyDifficulty.AllowedMisses);
    }

    [Fact]
    public void PrecisionChoiceGrade_ResolvesSuccessPartialAndFailFromSelectedChoiceIds()
    {
        var choices = PrecisionChoices();

        Assert.Equal(
            "success",
            ResolvePrecisionChoiceGrade(choices, selectedChoiceId: "open_gate", elapsedMs: 1200, timeoutMs: 6000));
        Assert.Equal(
            "partial",
            ResolvePrecisionChoiceGrade(choices, selectedChoiceId: "narrow_door", elapsedMs: 2000, timeoutMs: 6000));
        Assert.Equal(
            "fail",
            ResolvePrecisionChoiceGrade(choices, selectedChoiceId: "dark_cellar", elapsedMs: 2000, timeoutMs: 6000));
    }

    [Fact]
    public void PrecisionChoiceGrade_TimeoutResolvesConfiguredGradeOrDefaultFail()
    {
        var choices = PrecisionChoices();

        Assert.Equal(
            "partial",
            ResolvePrecisionChoiceGrade(
                choices,
                selectedChoiceId: null,
                elapsedMs: 6000,
                timeoutMs: 6000,
                timeoutGrade: "partial"));
        Assert.Equal(
            "fail",
            ResolvePrecisionChoiceGrade(
                choices,
                selectedChoiceId: null,
                elapsedMs: 6000,
                timeoutMs: 6000));
    }

    [Fact]
    public void PrecisionChoiceGrade_EscapeCancelsAsFail()
    {
        Assert.Equal(
            "fail",
            ResolvePrecisionChoiceGrade(
                PrecisionChoices(),
                selectedChoiceId: "open_gate",
                elapsedMs: 1200,
                timeoutMs: 6000,
                canceled: true));
    }

    [Fact]
    public void PrecisionChoiceGrade_UnknownChoiceResolvesAsFail()
    {
        Assert.Equal(
            "fail",
            ResolvePrecisionChoiceGrade(
                PrecisionChoices(),
                selectedChoiceId: "missing_choice",
                elapsedMs: 1200,
                timeoutMs: 6000));
    }

    [Fact]
    public void PrecisionChoiceEffectiveRequirement_HigherStatDoesNotMakeChoiceHarder()
    {
        var lowStat = ComputePrecisionChoiceEffectiveRequirement(
            timeoutMs: 6000,
            baseDifficulty: 3,
            statTier: -2,
            decoyHintCount: 3);
        var highStat = ComputePrecisionChoiceEffectiveRequirement(
            timeoutMs: 6000,
            baseDifficulty: 3,
            statTier: 3,
            decoyHintCount: 3);

        Assert.True(highStat.TimeoutMs >= lowStat.TimeoutMs);
        Assert.True(highStat.RevealedDecoyHintCount >= lowStat.RevealedDecoyHintCount);
    }

    [Fact]
    public void PrecisionChoiceEffectiveRequirement_HigherDifficultyDoesNotMakeChoiceEasier()
    {
        var easyDifficulty = ComputePrecisionChoiceEffectiveRequirement(
            timeoutMs: 6000,
            baseDifficulty: 1,
            statTier: 0,
            decoyHintCount: 3);
        var hardDifficulty = ComputePrecisionChoiceEffectiveRequirement(
            timeoutMs: 6000,
            baseDifficulty: 5,
            statTier: 0,
            decoyHintCount: 3);

        Assert.True(hardDifficulty.TimeoutMs <= easyDifficulty.TimeoutMs);
        Assert.True(hardDifficulty.RevealedDecoyHintCount <= easyDifficulty.RevealedDecoyHintCount);
        Assert.True(hardDifficulty.TimeoutMs >= 3000);
    }

    [Fact]
    public void StealthNoiseGrade_ResolvesSuccessPartialAndFailFromNoisePressure()
    {
        var effective = StealthNoiseRequirement();

        Assert.Equal(
            "success",
            ResolveStealthNoiseGrade(
                effective,
                [
                    StealthInput(1000),
                    StealthInput(2000),
                    StealthInput(3000),
                    StealthInput(4000)
                ]));
        Assert.Equal(
            "partial",
            ResolveStealthNoiseGrade(
                effective,
                [
                    StealthInput(1500),
                    StealthInput(3000)
                ]));
        Assert.Equal(
            "fail",
            ResolveStealthNoiseGrade(effective, []));
    }

    [Fact]
    public void StealthNoiseGrade_ExcessiveOverThresholdTimeResolvesFail()
    {
        var thresholds = new QteSceneService.StealthNoiseGradeThresholds(
            SuccessMaxNoise: 68,
            SuccessMaxOverThresholdMs: 0,
            PartialMaxNoise: 95,
            PartialMaxOverThresholdMs: 500);
        var effective = new QteSceneService.StealthNoiseEffectiveRequirement(
            DurationMs: 2000,
            StartingNoise: 65,
            DangerThreshold: 70,
            NoiseDriftPerSecond: 10,
            RecoveryPerInput: 1,
            AllowedOverThresholdMs: 500,
            GradeThresholds: thresholds,
            RecoveryKey: "space");

        Assert.Equal("fail", ResolveStealthNoiseGrade(effective, []));
    }

    [Fact]
    public void StealthNoiseGrade_EscapeCancelsAsFail()
    {
        var inputs = new[]
        {
            StealthInput(1000),
            StealthInput(1200, ConsoleKey.Escape),
            StealthInput(2000)
        };

        Assert.Equal("fail", ResolveStealthNoiseGrade(StealthNoiseRequirement(), inputs));
    }

    [Fact]
    public void StealthNoiseGrade_MalformedConfigResolvesAsFail()
    {
        Assert.Equal(
            "fail",
            QteSceneService.ResolveStealthNoiseGrade(
                config: new JsonObject
                {
                    ["durationMs"] = 8000,
                    ["startingNoise"] = 18,
                    ["dangerThreshold"] = 70
                },
                baseDifficulty: 3,
                statTier: 0,
                inputs: []));
    }

    [Fact]
    public void StealthNoiseEffectiveRequirement_HigherStatDoesNotMakeNoiseHarder()
    {
        var lowStat = ComputeStealthNoiseEffectiveRequirement(
            durationMs: 8000,
            startingNoise: 18,
            dangerThreshold: 70,
            noiseDriftPerSecond: 9,
            recoveryPerInput: 12,
            allowedOverThresholdMs: 900,
            gradeThresholds: StealthNoiseThresholds(),
            baseDifficulty: 3,
            statTier: -2);
        var highStat = ComputeStealthNoiseEffectiveRequirement(
            durationMs: 8000,
            startingNoise: 18,
            dangerThreshold: 70,
            noiseDriftPerSecond: 9,
            recoveryPerInput: 12,
            allowedOverThresholdMs: 900,
            gradeThresholds: StealthNoiseThresholds(),
            baseDifficulty: 3,
            statTier: 3);

        Assert.True(highStat.NoiseDriftPerSecond <= lowStat.NoiseDriftPerSecond);
        Assert.True(highStat.RecoveryPerInput >= lowStat.RecoveryPerInput);
        Assert.True(highStat.AllowedOverThresholdMs >= lowStat.AllowedOverThresholdMs);
    }

    [Fact]
    public void StealthNoiseEffectiveRequirement_HigherDifficultyDoesNotMakeNoiseEasier()
    {
        var easyDifficulty = ComputeStealthNoiseEffectiveRequirement(
            durationMs: 8000,
            startingNoise: 18,
            dangerThreshold: 70,
            noiseDriftPerSecond: 9,
            recoveryPerInput: 12,
            allowedOverThresholdMs: 900,
            gradeThresholds: StealthNoiseThresholds(),
            baseDifficulty: 1,
            statTier: 0);
        var hardDifficulty = ComputeStealthNoiseEffectiveRequirement(
            durationMs: 8000,
            startingNoise: 18,
            dangerThreshold: 70,
            noiseDriftPerSecond: 9,
            recoveryPerInput: 12,
            allowedOverThresholdMs: 900,
            gradeThresholds: StealthNoiseThresholds(),
            baseDifficulty: 5,
            statTier: 0);

        Assert.True(hardDifficulty.NoiseDriftPerSecond >= easyDifficulty.NoiseDriftPerSecond);
        Assert.True(hardDifficulty.RecoveryPerInput <= easyDifficulty.RecoveryPerInput);
        Assert.True(hardDifficulty.AllowedOverThresholdMs <= easyDifficulty.AllowedOverThresholdMs);
    }

    [Fact]
    public void LockPinSetGrade_ResolvesCleanPartialAndFailFromPinWindows()
    {
        var effective = LockPinSetRequirement();

        Assert.Equal(
            "success",
            QteSceneService.ResolveLockPinSetGrade(
                effective,
                [
                    LockPinAttempt(1000, 0, 15),
                    LockPinAttempt(2200, 1, 45),
                    LockPinAttempt(4200, 2, 75)
                ]));
        Assert.Equal(
            "partial",
            QteSceneService.ResolveLockPinSetGrade(
                effective,
                [
                    LockPinAttempt(1000, 0, 5),
                    LockPinAttempt(2500, 0, 15),
                    LockPinAttempt(5200, 1, 45),
                    LockPinAttempt(7600, 2, 75)
                ]));
        Assert.Equal(
            "fail",
            QteSceneService.ResolveLockPinSetGrade(
                effective,
                [
                    LockPinAttempt(1000, 0, 15),
                    LockPinAttempt(2200, 1, 45)
                ]));
    }

    [Fact]
    public void LockPinSetGrade_BrokenPickOrExceededMistakesResolvesFail()
    {
        var fragile = LockPinSetRequirement(pickDurability: 2, maxMistakes: 2);
        var strict = LockPinSetRequirement(pickDurability: 5, maxMistakes: 1);

        Assert.Equal(
            "fail",
            QteSceneService.ResolveLockPinSetGrade(
                fragile,
                [
                    LockPinAttempt(1000, 0, 5),
                    LockPinAttempt(1500, 0, 6)
                ]));
        Assert.Equal(
            "fail",
            QteSceneService.ResolveLockPinSetGrade(
                strict,
                [
                    LockPinAttempt(1000, 0, 5),
                    LockPinAttempt(1500, 0, 6)
                ]));
    }

    [Fact]
    public void LockPinSetGrade_TimeoutWithUnopenedPinsResolvesFail()
    {
        Assert.Equal(
            "fail",
            QteSceneService.ResolveLockPinSetGrade(
                LockPinSetRequirement(),
                [
                    LockPinAttempt(1000, 0, 15),
                    LockPinAttempt(2200, 1, 45)
                ],
                timedOut: true));
    }

    [Fact]
    public void LockPinSetGrade_EscapeCancelsAsFail()
    {
        Assert.Equal(
            "fail",
            QteSceneService.ResolveLockPinSetGrade(
                LockPinSetRequirement(),
                [
                    LockPinAttempt(1000, 0, 15, canceled: true),
                    LockPinAttempt(2200, 1, 45)
                ]));
    }

    [Fact]
    public void LockPinSetGrade_MalformedConfigResolvesAsFail()
    {
        Assert.Equal(
            "fail",
            QteSceneService.ResolveLockPinSetGrade(
                config: new JsonObject
                {
                    ["pinCount"] = 4,
                    ["timerMs"] = 12000
                },
                baseDifficulty: 3,
                statTier: 0,
                inputs: []));
    }

    [Fact]
    public void LockPinSetEffectiveRequirement_HigherStatDoesNotMakeLockHarder()
    {
        var lowStat = ComputeLockPinSetEffectiveRequirement(
            baseDifficulty: 3,
            statTier: -2);
        var highStat = ComputeLockPinSetEffectiveRequirement(
            baseDifficulty: 3,
            statTier: 3);

        Assert.True(WindowWidth(highStat.PinWindows[0]) >= WindowWidth(lowStat.PinWindows[0]));
        Assert.True(highStat.TimerMs >= lowStat.TimerMs);
        Assert.True(highStat.PinDriftPerSecond <= lowStat.PinDriftPerSecond);
        Assert.True(highStat.MaxMistakes >= lowStat.MaxMistakes);
    }

    [Fact]
    public void LockPinSetEffectiveRequirement_HigherDifficultyDoesNotMakeLockEasier()
    {
        var easyDifficulty = ComputeLockPinSetEffectiveRequirement(
            baseDifficulty: 1,
            statTier: 0);
        var hardDifficulty = ComputeLockPinSetEffectiveRequirement(
            baseDifficulty: 5,
            statTier: 0);

        Assert.True(WindowWidth(hardDifficulty.PinWindows[0]) <= WindowWidth(easyDifficulty.PinWindows[0]));
        Assert.True(hardDifficulty.TimerMs <= easyDifficulty.TimerMs);
        Assert.True(hardDifficulty.PinDriftPerSecond >= easyDifficulty.PinDriftPerSecond);
        Assert.True(hardDifficulty.MaxMistakes <= easyDifficulty.MaxMistakes);
    }

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_DeletesInvalidJsonRuntimeFile()
    {
        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, "{ invalid json");

        await _service.EnsureRuntimeStateHealthyAsync();

        Assert.False(_fs.FileExists(QteSceneService.QteRuntimePath));
    }

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_RemovesPendingOfferWithoutActiveScene()
    {
        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, """
        {
          "pendingOffer": {
            "qteId": "qte_bridge",
            "title": "Bridge",
            "offerText": "Offer"
          },
          "lastDeclinedQteId": "older_qte"
        }
        """);

        await _service.EnsureRuntimeStateHealthyAsync();

        var json = await _fs.ReadFileAsync(QteSceneService.QteRuntimePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.DoesNotContain("pendingOffer", json!, StringComparison.Ordinal);
        Assert.Contains("lastDeclinedQteId", json!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureRuntimeStateHealthyAsync_ClearsBrokenActiveSceneButPreservesReminder()
    {
        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, """
        {
          "pendingOffer": {
            "qteId": "qte_bridge",
            "title": "Bridge",
            "offerText": "Offer"
          },
          "activeScene": {
            "offer": null,
            "currentChapterId": 42,
            "acceptedAtTurn": "bad"
          },
          "lastResolvedQteSummaryPendingReminder": "QTE summary"
        }
        """);

        await _service.EnsureRuntimeStateHealthyAsync();

        var json = await _fs.ReadFileAsync(QteSceneService.QteRuntimePath);
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.DoesNotContain("activeScene", json!, StringComparison.Ordinal);
        Assert.DoesNotContain("pendingOffer", json!, StringComparison.Ordinal);
        Assert.Contains("lastResolvedQteSummaryPendingReminder", json!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_CapturesBaselineForGuardianProjectNormalization()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 60 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая хранительница."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_existing",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Существующий проект",
                "activeState": "Planning",
                "totalWork": 10,
                "workDone": 2,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 1,
                "stability": 98
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var service = CreateRuntimeCapableService();

        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_done",
            Title = "QTE complete",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "guardianProjectUpdates": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_existing",
                  "workDone": 5,
                  "activeState": "Advancing"
                }
              ]
            }
            """)!.AsObject()
        };

        await service.ApplyTerminalOutcomeStateChangesAsync(outcome);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);

        using var trackerDoc = JsonDocument.Parse(trackerJson!);
        var activeProjects = trackerDoc.RootElement.GetProperty("activeProjects").EnumerateArray().ToList();
        Assert.Single(activeProjects);
        Assert.Equal("proj_existing", activeProjects[0].GetProperty("project").GetProperty("projectId").GetString());
        Assert.Equal(5, activeProjects[0].GetProperty("project").GetProperty("workDone").GetInt32());
        Assert.Equal("Advancing", activeProjects[0].GetProperty("project").GetProperty("activeState").GetString());
        Assert.False(trackerDoc.RootElement.TryGetProperty("guardianProjectUpdates", out _));
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_CapturesWorldEventsBaselineForRivalNormalization()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "completionTurn": 40,
                "projectOutcomeAudit": {
                  "visibleRivalClueBonus": 2
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "bonusLoreUnlocksApplied": 1,
                  "questHookTokensGranted": 1,
                  "questHookTokensSpent": 1,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 2,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "evt_hunter_repeat",
              "eventTitle": "Тайный приказ охотника",
              "summary": "Игрок уже знал об этом следе.",
              "relatedRivalArcId": "arc_hunter_repeat",
              "visibility": "player_known",
              "bonusClueSourceProjectId": "research_major",
              "bonusClueRevealId": "reveal_repeat_evt",
              "bonusClueCost": 1
            }
          ]
        }
        """);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_repeat_clue",
            Title = "Repeat clue",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "UpdateRivalSoulArcs": [
                {
                  "arcId": "arc_hunter_repeat",
                  "scope": "major",
                  "arcType": "hostile_hunt",
                  "status": "rising",
                  "objective": "Find the player",
                  "sponsorGuardianRef": {
                    "mode": "guardianId",
                    "guardianId": "guardian_alpha",
                    "displayName": "Азалия"
                  },
                  "rivalSoul": {
                    "rivalSoulId": "rival_1",
                    "displayNameOrMoniker": "Охотник из тени",
                    "roleSummary": "Охотник rival-Хранителя",
                    "isKnownToPlayer": true
                  },
                  "playerIntersection": {
                    "targetsPlayerDirectly": true,
                    "stakes": "Опасность для героя",
                    "canBecomeSoulQuest": true,
                    "recommendedCounterQuestTone": "urgent"
                  },
                  "milestones": [
                    { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
                  ],
                  "currentStage": 1,
                  "publicSignals": [],
                  "resolution": { "outcome": "ongoing", "notes": "" }
                }
              ],
              "worldEventsLog": [
                {
                  "eventId": "evt_hunter_repeat",
                  "eventTitle": "Тайный приказ охотника",
                  "summary": "Игрок уже знал об этом следе.",
                  "relatedRivalArcId": "arc_hunter_repeat",
                  "visibility": "player_known",
                  "bonusClueSourceProjectId": "research_major",
                  "bonusClueRevealId": "reveal_repeat_evt",
                  "bonusClueCost": 1
                }
              ]
            }
            """)!.AsObject()
        };

        await service.ApplyTerminalOutcomeStateChangesAsync(outcome);

        var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerJson);
        Assert.DoesNotContain("\"visibleRivalClueBudgetSpent\": 1", trackerJson, StringComparison.Ordinal);
        Assert.Contains("\"visibleRivalClueBudgetSpent\": 0", trackerJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_RestoresStateAfterValidationFailure()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", """
        {
          "totalExperience": 10
        }
        """);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid",
            Title = "Invalid outcome",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "playerCharacterNameChange": "Новая личность"
            }
            """)!.AsObject()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));

        var experienceJson = await _fs.ReadFileAsync("game_state/player/experience.json");
        Assert.NotNull(experienceJson);
        using (var experienceDoc = JsonDocument.Parse(experienceJson!))
            Assert.Equal(10, experienceDoc.RootElement.GetProperty("totalExperience").GetInt32());

        Assert.False(_fs.FileExists("game_state/player/transformation.json"));
        Assert.False(_fs.FileExists("output/narrative_response.json"));

        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_RestoresGuardianProjectJournalAfterNormalizationFailure()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 44 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentIncarnation": 3,
          "currentRealm": "Mortal World"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая хранительница."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [],
          "completedProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "research_major",
                "projectType": "lore_research",
                "projectTier": "major",
                "finalState": "Completed",
                "completionTurn": 40,
                "projectOutcomeAudit": {
                  "visibleRivalClueBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "bonusLoreUnlocksApplied": 0,
                  "questHookTokensGranted": 0,
                  "questHookTokensSpent": 0,
                  "specialQuestLineTokensGranted": 0,
                  "specialQuestLineTokensSpent": 0,
                  "visibleRivalClueBudgetGranted": 1,
                  "visibleRivalClueBudgetSpent": 0
                }
              }
            }
          ],
          "temporaryProjectModifiers": []
        }
        """);

        const string originalJournal = """
        {
          "entries": [
            {
              "entryId": "existing_entry",
              "guardianId": "guardian_alpha",
              "projectId": "research_major",
              "eventType": "completed",
              "title": "Старое событие",
              "summary": "Журнал до QTE."
            }
          ]
        }
        """;
        await _fs.WriteFileAtomicAsync(GuardianProjectState.JournalPath, originalJournal);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid_journal_restore",
            Title = "Invalid outcome",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "playerCharacterNameChange": "Новая личность",
              "UpdateRivalSoulArcs": [
                {
                  "arcId": "arc_new_clue",
                  "scope": "major",
                  "arcType": "hostile_hunt",
                  "status": "rising",
                  "objective": "Find the player",
                  "sponsorGuardianRef": {
                    "mode": "guardianId",
                    "guardianId": "guardian_alpha",
                    "displayName": "Азалия"
                  },
                  "rivalSoul": {
                    "rivalSoulId": "rival_1",
                    "displayNameOrMoniker": "Охотник из тени",
                    "roleSummary": "Охотник rival-Хранителя",
                    "isKnownToPlayer": true
                  },
                  "playerIntersection": {
                    "targetsPlayerDirectly": true,
                    "stakes": "Опасность для героя",
                    "canBecomeSoulQuest": true,
                    "recommendedCounterQuestTone": "urgent"
                  },
                  "milestones": [
                    { "stage": 1, "title": "Слух", "summary": "О нём говорят", "visibleToPlayer": true }
                  ],
                  "publicSignals": [
                    {
                      "signalId": "signal_new_clue",
                      "description": "Новый след охотника.",
                      "visibleToPlayer": true,
                      "bonusClueSourceProjectId": "research_major",
                      "bonusClueCost": 1
                    }
                  ],
                  "currentStage": 1,
                  "resolution": { "outcome": "ongoing", "notes": "" }
                }
              ]
            }
            """)!.AsObject()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));

        var journalJson = await _fs.ReadFileAsync(GuardianProjectState.JournalPath);
        Assert.Equal(originalJournal.Replace("\r\n", "\n"), journalJson?.Replace("\r\n", "\n"));
        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task SaveGameAsync_ExcludesQteNormalizerBackupsFromArchive()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        { "guardians": [] }
        """);
        await _fs.WriteFileAtomicAsync($"{QteNormalizerBackupDirectory}/stale/run_backup.json", """
        { "temporary": true }
        """);

        var saveService = await CreateSaveLoadServiceAsync();
        var saved = await saveService.SaveGameAsync("qte_backups", "Regression", "saves/test", 1);

        Assert.True(saved);

        var saveDir = _fs.ResolvePath("saves/test");
        var savePath = Directory.GetFiles(saveDir, "*.zip", SearchOption.TopDirectoryOnly).Single();
        using var archive = ZipFile.OpenRead(savePath);
        Assert.DoesNotContain(archive.Entries, entry =>
            entry.FullName.StartsWith(QteNormalizerBackupDirectory + "/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyTerminalOutcomeStateChangesAsync_RemovesQteBackupRootAfterSuccessfulRun()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        { "turnNumber": 60 }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_alpha",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая хранительница."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 75, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 40, "tier": "Стабильная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guardian_alpha",
              "project": {
                "projectId": "proj_existing",
                "projectType": "abode_expansion",
                "projectTier": "minor",
                "projectMode": "internal",
                "projectName": "Существующий проект",
                "activeState": "Planning",
                "totalWork": 10,
                "workDone": 2,
                "totalStages": 2,
                "currentStage": 0,
                "pressure": 1,
                "stability": 98
              }
            }
          ],
          "completedProjects": [],
          "temporaryProjectModifiers": []
        }
        """);

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_success_cleanup",
            Title = "QTE complete",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "guardianProjectUpdates": [
                {
                  "guardianId": "guardian_alpha",
                  "projectId": "proj_existing",
                  "workDone": 5,
                  "activeState": "Advancing"
                }
              ]
            }
            """)!.AsObject()
        };

        await service.ApplyTerminalOutcomeStateChangesAsync(outcome);

        AssertNoQteBackupArtifacts();
    }

    [Fact]
    public async Task ApplyTerminalOutcomeValidatedStateChangesAsync_PreservesSiblingBackupRunDirectory()
    {
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", """
        {
          "totalExperience": 10
        }
        """);

        var siblingDirectory = _fs.ResolvePath($"{QteNormalizerBackupDirectory}/sibling_run");
        Directory.CreateDirectory(siblingDirectory);
        var siblingFile = Path.Combine(siblingDirectory, "stale_backup.json");
        await File.WriteAllTextAsync(siblingFile, "{ \"temporary\": true }");

        var service = CreateRuntimeCapableService();
        var outcome = new QteSceneService.QteTerminalOutcome
        {
            OutcomeId = "qte_invalid_sibling_cleanup",
            Title = "Invalid outcome",
            FinalNarrative = "Исход применён.",
            GmSummary = "Regression summary.",
            ResponseFragment = JsonNode.Parse("""
            {
              "response": "Исход применён.",
              "experienceGained": 5,
              "playerCharacterNameChange": "Новая личность"
            }
            """)!.AsObject()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyTerminalOutcomeValidatedStateChangesAsync(outcome));

        Assert.True(File.Exists(siblingFile));

        var backupRoot = _fs.ResolvePath(QteNormalizerBackupDirectory);
        Assert.True(Directory.Exists(backupRoot));
        var backupFiles = Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories);
        Assert.Single(backupFiles);
        Assert.Equal("stale_backup.json", Path.GetFileName(backupFiles[0]));
        Assert.Equal("{ \"temporary\": true }", await File.ReadAllTextAsync(backupFiles[0]));
        var runDirectories = Directory.GetDirectories(backupRoot, "*", SearchOption.TopDirectoryOnly);
        Assert.Single(runDirectories);
        Assert.Equal("sibling_run", Path.GetFileName(runDirectories[0]));
    }

    private QteSceneService CreateRuntimeCapableService()
    {
        var settings = new GameSettings();
        return new QteSceneService(
            _fs,
            settings,
            null!,
            null!,
            null!,
            new StateDistributor(_fs, NullLogger<StateDistributor>.Instance),
            new ValidationService(_fs, NullLogger<ValidationService>.Instance),
            new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance),
            new StateManager(_fs, settings, NullLogger<StateManager>.Instance),
            NullLogger<QteSceneService>.Instance);
    }

    private async Task<SaveLoadService> CreateSaveLoadServiceAsync()
    {
        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        await stateManager.RefreshGameStateAsync();
        return new SaveLoadService(_fs, stateManager, NullLogger<SaveLoadService>.Instance);
    }

    private void AssertNoQteBackupArtifacts()
    {
        var backupDirectory = _fs.ResolvePath(QteNormalizerBackupDirectory);
        Assert.False(Directory.Exists(backupDirectory));
    }

    private static ConsoleKeyInfo[] RepeatKey(ConsoleKey key, int count)
    {
        var keyChar = key == ConsoleKey.Spacebar ? ' ' : char.ToLowerInvariant(key.ToString()[0]);
        return Enumerable.Range(0, count)
            .Select(_ => new ConsoleKeyInfo(keyChar, key, false, false, false))
            .ToArray();
    }

    private static ConsoleKeyInfo Key(ConsoleKey key)
    {
        var keyChar = key == ConsoleKey.Spacebar ? ' ' : char.ToLowerInvariant(key.ToString()[0]);
        return new ConsoleKeyInfo(keyChar, key, false, false, false);
    }

    private static string ResolveMashInputGrade(
        string[] acceptedTokens,
        int successTarget,
        int partialTarget,
        ConsoleKeyInfo[] inputs) =>
        QteSceneService.ResolveMashInputGrade(acceptedTokens, successTarget, partialTarget, inputs);

    private static int ComputeMashInputEffectiveTargetPresses(int targetPresses, int baseDifficulty, int statTier) =>
        QteSceneService.ComputeMashInputEffectiveTargetPresses(targetPresses, baseDifficulty, statTier);

    private static int ComputeMashInputPartialTargetPresses(int successTarget, double partialThreshold) =>
        QteSceneService.ComputeMashInputPartialTargetPresses(successTarget, partialThreshold);

    private static string ResolvePatternMemoryGrade(
        string[] expectedSequence,
        int allowedMistakes,
        ConsoleKeyInfo[] inputs,
        bool timedOut = false) =>
        QteSceneService.ResolvePatternMemoryGrade(expectedSequence, allowedMistakes, inputs, timedOut);

    private static QteSceneService.PatternMemoryEffectiveRequirement ComputePatternMemoryEffectiveRequirement(
        int sequenceLength,
        int revealMs,
        int inputTimeoutMs,
        int allowedMistakes,
        int baseDifficulty,
        int statTier) =>
        QteSceneService.ComputePatternMemoryEffectiveRequirement(
            sequenceLength,
            revealMs,
            inputTimeoutMs,
            allowedMistakes,
            baseDifficulty,
            statTier);

    private static IReadOnlyList<string> GeneratePatternMemorySequence(
        string[] alphabet,
        int sequenceLength,
        string seed) =>
        QteSceneService.GeneratePatternMemorySequence(alphabet, sequenceLength, seed);

    private static string ResolveRhythmPulseGrade(
        int[] pulseOffsetsMs,
        int hitWindowMs,
        int allowedMisses,
        QteSceneService.RhythmPulseInput[] inputs) =>
        QteSceneService.ResolveRhythmPulseGrade(pulseOffsetsMs, hitWindowMs, allowedMisses, inputs);

    private static IReadOnlyList<int> GenerateRhythmPulseSchedule(
        int pulseCount,
        int beatIntervalMs,
        string? patternVariation) =>
        QteSceneService.GenerateRhythmPulseSchedule(pulseCount, beatIntervalMs, patternVariation);

    private static QteSceneService.RhythmPulseEffectiveRequirement ComputeRhythmPulseEffectiveRequirement(
        int pulseCount,
        int beatIntervalMs,
        int hitWindowMs,
        int allowedMisses,
        int baseDifficulty,
        int statTier) =>
        QteSceneService.ComputeRhythmPulseEffectiveRequirement(
            pulseCount,
            beatIntervalMs,
            hitWindowMs,
            allowedMisses,
            baseDifficulty,
            statTier);

    private static QteSceneService.PrecisionChoiceChoice[] PrecisionChoices() =>
    [
        new("open_gate", "success"),
        new("narrow_door", "partial"),
        new("dark_cellar", "fail")
    ];

    private static string ResolvePrecisionChoiceGrade(
        IReadOnlyList<QteSceneService.PrecisionChoiceChoice> choices,
        string? selectedChoiceId,
        int elapsedMs,
        int timeoutMs,
        string? timeoutGrade = null,
        bool canceled = false) =>
        QteSceneService.ResolvePrecisionChoiceGrade(
            choices,
            selectedChoiceId,
            elapsedMs,
            timeoutMs,
            timeoutGrade,
            canceled);

    private static QteSceneService.PrecisionChoiceEffectiveRequirement ComputePrecisionChoiceEffectiveRequirement(
        int timeoutMs,
        int baseDifficulty,
        int statTier,
        int decoyHintCount) =>
        QteSceneService.ComputePrecisionChoiceEffectiveRequirement(
            timeoutMs,
            baseDifficulty,
            statTier,
            decoyHintCount);

    private static QteSceneService.RhythmPulseInput RhythmInput(
        int offsetMs,
        ConsoleKey key = ConsoleKey.Spacebar) =>
        new(offsetMs, Key(key));

    private static QteSceneService.StealthNoiseGradeThresholds StealthNoiseThresholds() =>
        new(
            SuccessMaxNoise: 48,
            SuccessMaxOverThresholdMs: 0,
            PartialMaxNoise: 70,
            PartialMaxOverThresholdMs: 900);

    private static QteSceneService.StealthNoiseEffectiveRequirement StealthNoiseRequirement() =>
        new(
            DurationMs: 8000,
            StartingNoise: 18,
            DangerThreshold: 70,
            NoiseDriftPerSecond: 9,
            RecoveryPerInput: 12,
            AllowedOverThresholdMs: 900,
            GradeThresholds: StealthNoiseThresholds(),
            RecoveryKey: "space");

    private static QteSceneService.StealthNoiseInput StealthInput(
        int offsetMs,
        ConsoleKey key = ConsoleKey.Spacebar) =>
        new(offsetMs, Key(key));

    private static string ResolveStealthNoiseGrade(
        QteSceneService.StealthNoiseEffectiveRequirement effective,
        QteSceneService.StealthNoiseInput[] inputs,
        bool canceled = false) =>
        QteSceneService.ResolveStealthNoiseGrade(effective, inputs, canceled);

    private static QteSceneService.StealthNoiseEffectiveRequirement ComputeStealthNoiseEffectiveRequirement(
        int durationMs,
        double startingNoise,
        double dangerThreshold,
        double noiseDriftPerSecond,
        double recoveryPerInput,
        int allowedOverThresholdMs,
        QteSceneService.StealthNoiseGradeThresholds gradeThresholds,
        int baseDifficulty,
        int statTier) =>
        QteSceneService.ComputeStealthNoiseEffectiveRequirement(
            durationMs,
            startingNoise,
            dangerThreshold,
            noiseDriftPerSecond,
            recoveryPerInput,
            allowedOverThresholdMs,
            gradeThresholds,
            baseDifficulty,
            statTier,
            recoveryKey: "space");

    private static QteSceneService.LockPinSetGradeThresholds LockPinSetThresholds() =>
        new(
            SuccessMaxTimeMs: 5000,
            SuccessMaxMistakes: 0,
            PartialMaxTimeMs: 10000,
            PartialMaxMistakes: 2);

    private static QteSceneService.LockPinWindow[] LockPinWindows() =>
    [
        new(Pin: 1, Min: 10, Max: 20, Label: "первый штифт"),
        new(Pin: 2, Min: 40, Max: 50, Label: "второй штифт"),
        new(Pin: 3, Min: 70, Max: 80, Label: "третий штифт")
    ];

    private static QteSceneService.LockPinSetEffectiveRequirement LockPinSetRequirement(
        int pickDurability = 5,
        int maxMistakes = 2) =>
        QteSceneService.ComputeLockPinSetEffectiveRequirement(
            pinCount: 3,
            pinWindows: LockPinWindows(),
            timerMs: 10000,
            pickDurability,
            maxMistakes,
            pinDriftPerSecond: 4,
            gradeThresholds: LockPinSetThresholds(),
            baseDifficulty: 3,
            statTier: 0,
            adjustKey: "q",
            setKey: "space");

    private static QteSceneService.LockPinSetEffectiveRequirement ComputeLockPinSetEffectiveRequirement(
        int baseDifficulty,
        int statTier) =>
        QteSceneService.ComputeLockPinSetEffectiveRequirement(
            pinCount: 3,
            pinWindows: LockPinWindows(),
            timerMs: 10000,
            pickDurability: 5,
            maxMistakes: 2,
            pinDriftPerSecond: 4,
            gradeThresholds: LockPinSetThresholds(),
            baseDifficulty,
            statTier,
            adjustKey: "q",
            setKey: "space");

    private static QteSceneService.LockPinSetInput LockPinAttempt(
        int offsetMs,
        int pinIndex,
        double position,
        bool canceled = false) =>
        new(offsetMs, pinIndex, position, canceled);

    private static double WindowWidth(QteSceneService.LockPinWindow window) => window.Max - window.Min;

    private static void AssertStrictlyIncreasing(IReadOnlyList<int> values)
    {
        for (var i = 1; i < values.Count; i++)
            Assert.True(values[i] > values[i - 1], $"{values[i]} should be greater than {values[i - 1]} at index {i}.");
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
