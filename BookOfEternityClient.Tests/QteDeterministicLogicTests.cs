using BookOfEternityClient.Services;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QteDeterministicLogicTests
{
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
    public void LockPinSetGrade_AttemptsAfterTimerDoNotOpenLock()
    {
        Assert.Equal(
            "fail",
            QteSceneService.ResolveLockPinSetGrade(
                LockPinSetRequirement(),
                [
                    LockPinAttempt(1000, 0, 15),
                    LockPinAttempt(2200, 1, 45),
                    LockPinAttempt(11000, 2, 75)
                ]));
    }

    [Fact]
    public void LockPinSetLiveAdjustment_CanReachCommittedExampleLowWindow()
    {
        var shiftedAdjust = new ConsoleKeyInfo('Q', ConsoleKey.Q, shift: true, alt: false, control: false);
        var normalAdjust = new ConsoleKeyInfo('q', ConsoleKey.Q, shift: false, alt: false, control: false);
        var position = 50d;

        for (var step = 0; step < 4; step++)
        {
            Assert.True(QteSceneService.TryGetLockPinSetAdjustmentDirection(shiftedAdjust, "q", out var direction));
            position = QteSceneService.ApplyLockPinSetAdjustment(position, direction);
        }

        Assert.InRange(position, 18, 32);
        Assert.True(QteSceneService.TryGetLockPinSetAdjustmentDirection(normalAdjust, "q", out var upwardDirection));
        Assert.True(upwardDirection > 0);
    }

    [Theory]
    [InlineData(4, 9500)]
    [InlineData(5, 9000)]
    public void LockPinSetGrade_HardDifficultyFullTimerPartialThresholdStillResolvesAllGrades(
        int baseDifficulty,
        int expectedEffectiveTimerMs)
    {
        var effective = QteSceneService.ComputeLockPinSetEffectiveRequirement(
            pinCount: 3,
            pinWindows: LockPinWindows(),
            timerMs: 10000,
            pickDurability: 5,
            maxMistakes: 2,
            pinDriftPerSecond: 4,
            gradeThresholds: LockPinSetThresholds(partialMaxTimeMs: 10000),
            baseDifficulty,
            statTier: 0,
            adjustKey: "q",
            setKey: "space");

        Assert.Equal(expectedEffectiveTimerMs, effective.TimerMs);
        Assert.True(effective.GradeThresholds.SuccessMaxTimeMs <= effective.GradeThresholds.PartialMaxTimeMs);
        Assert.True(effective.GradeThresholds.PartialMaxTimeMs <= effective.TimerMs);
        Assert.True(effective.GradeThresholds.SuccessMaxMistakes <= effective.GradeThresholds.PartialMaxMistakes);
        Assert.True(effective.GradeThresholds.PartialMaxMistakes <= effective.MaxMistakes);
        Assert.Equal("success", QteSceneService.ResolveLockPinSetGrade(effective, OpenAllLockPins(effective, 4200)));
        Assert.Equal("partial", QteSceneService.ResolveLockPinSetGrade(effective, OpenAllLockPins(effective, expectedEffectiveTimerMs - 100)));
        Assert.Equal("fail", QteSceneService.ResolveLockPinSetGrade(effective, OpenAllLockPins(effective, expectedEffectiveTimerMs + 100)));
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

    private static QteSceneService.LockPinSetGradeThresholds LockPinSetThresholds(
        int successMaxTimeMs = 5000,
        int successMaxMistakes = 0,
        int partialMaxTimeMs = 10000,
        int partialMaxMistakes = 2) =>
        new(
            successMaxTimeMs,
            successMaxMistakes,
            partialMaxTimeMs,
            partialMaxMistakes);

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

    private static QteSceneService.LockPinSetInput[] OpenAllLockPins(
        QteSceneService.LockPinSetEffectiveRequirement effective,
        int finalOffsetMs)
    {
        var stepMs = Math.Max(1, finalOffsetMs / effective.PinCount);
        return effective.PinWindows
            .Select((window, index) => LockPinAttempt(
                Math.Min(finalOffsetMs, stepMs * (index + 1)),
                index,
                (window.Min + window.Max) / 2d))
            .ToArray();
    }

    private static double WindowWidth(QteSceneService.LockPinWindow window) => window.Max - window.Min;

    private static void AssertStrictlyIncreasing(IReadOnlyList<int> values)
    {
        for (var i = 1; i < values.Count; i++)
            Assert.True(values[i] > values[i - 1], $"{values[i]} should be greater than {values[i - 1]} at index {i}.");
    }
}
