import { describe, expect, it } from 'vitest';
import {
  resolveBalanceMeterGrade,
  resolveChargeReleaseGrade,
  resolveLockPinSetGrade,
  resolveMashInputGrade,
  resolvePatternMemoryGrade,
  resolvePrecisionChoiceGrade,
  resolvePromptChainGrade,
  resolveRhythmPulseGrade,
  resolveStealthNoiseGrade,
  resolveTimingBarGrade
} from '../src/qte/qteGradeHelpers';

describe('browser QTE mini-game grade helpers #918', () => {
  it('resolves TimingBar grades from marker position and zones', () => {
    const config = { width: 32, successStart: 12, successWidth: 8, partialStart: 9, partialWidth: 14 };

    expect(resolveTimingBarGrade(config, 15)).toBe('success');
    expect(resolveTimingBarGrade(config, 9)).toBe('partial');
    expect(resolveTimingBarGrade(config, 1)).toBe('fail');
  });

  it('resolves PromptChain grades from sequence accuracy', () => {
    expect(resolvePromptChainGrade(['q', 'w', 'space'], ['q', 'w', 'space'], 1)).toBe('success');
    expect(resolvePromptChainGrade(['q', 'w', 'space'], ['q', 'a', 'space'], 1)).toBe('partial');
    expect(resolvePromptChainGrade(['q', 'w', 'space'], ['a', 's', 'd'], 1)).toBe('fail');
  });

  it('resolves BalanceMeter grades from safe tick ratio', () => {
    expect(resolveBalanceMeterGrade(8, 10)).toBe('success');
    expect(resolveBalanceMeterGrade(5, 10)).toBe('partial');
    expect(resolveBalanceMeterGrade(3, 10)).toBe('fail');
  });

  it('resolves ChargeRelease grades from released charge value', () => {
    const config = { targetStart: 45, targetWidth: 16, partialPadding: 10 };

    expect(resolveChargeReleaseGrade(config, 52)).toBe('success');
    expect(resolveChargeReleaseGrade(config, 38)).toBe('partial');
    expect(resolveChargeReleaseGrade(config, 90)).toBe('fail');
  });

  it('resolves MashInput grades from matching press count', () => {
    expect(resolveMashInputGrade({ successTarget: 6, partialTarget: 3 }, 6)).toBe('success');
    expect(resolveMashInputGrade({ successTarget: 6, partialTarget: 3 }, 3)).toBe('partial');
    expect(resolveMashInputGrade({ successTarget: 6, partialTarget: 3 }, 2)).toBe('fail');
  });

  it('resolves PatternMemory grades from replayed tokens', () => {
    expect(resolvePatternMemoryGrade(['q', 'w', 'space'], ['q', 'w', 'space'], 1)).toBe('success');
    expect(resolvePatternMemoryGrade(['q', 'w', 'space'], ['q', 'a', 'space'], 1)).toBe('partial');
    expect(resolvePatternMemoryGrade(['q', 'w', 'space'], ['a', 's', 'd'], 1)).toBe('fail');
    expect(resolvePatternMemoryGrade(['q', 'w', 'space'], ['q', 'w'], 1, true)).toBe('fail');
  });

  it('resolves RhythmPulse grades from hit windows', () => {
    const config = { pulseOffsetsMs: [500, 1000, 1500, 2000], hitWindowMs: 80, allowedMisses: 1 };

    expect(resolveRhythmPulseGrade(config, [500, 940, 1510])).toBe('success');
    expect(resolveRhythmPulseGrade(config, [500, 1510])).toBe('partial');
    expect(resolveRhythmPulseGrade(config, [500])).toBe('fail');
  });

  it('resolves PrecisionChoice grades from selected choice and timeout', () => {
    const choices = [
      { id: 'open_gate', grade: 'success' },
      { id: 'narrow_door', grade: 'partial' },
      { id: 'dark_cellar', grade: 'fail' }
    ];

    expect(resolvePrecisionChoiceGrade(choices, 'open_gate', false, 'partial')).toBe('success');
    expect(resolvePrecisionChoiceGrade(choices, 'narrow_door', false, 'partial')).toBe('partial');
    expect(resolvePrecisionChoiceGrade(choices, 'missing', false, 'partial')).toBe('fail');
    expect(resolvePrecisionChoiceGrade(choices, null, true, 'partial')).toBe('partial');
  });

  it('resolves StealthNoise grades from noise pressure samples', () => {
    const config = {
      durationMs: 6000,
      startingNoise: 10,
      dangerThreshold: 70,
      noiseDriftPerSecond: 9,
      recoveryPerInput: 12,
      allowedOverThresholdMs: 800,
      gradeThresholds: {
        successMaxNoise: 45,
        successMaxOverThresholdMs: 0,
        partialMaxNoise: 75,
        partialMaxOverThresholdMs: 800
      }
    };

    expect(resolveStealthNoiseGrade(config, [1000, 2000, 3000, 4000])).toBe('success');
    expect(resolveStealthNoiseGrade(config, [3000])).toBe('partial');
    expect(resolveStealthNoiseGrade(config, [])).toBe('partial');
    expect(resolveStealthNoiseGrade({ ...config, noiseDriftPerSecond: 18 }, [])).toBe('fail');
    expect(resolveStealthNoiseGrade({ ...config, noiseDriftPerSecond: 1 }, [])).toBe('success');
  });

  it('resolves LockPinSet grades from pin attempts', () => {
    const config = {
      pinCount: 2,
      pinWindows: [
        { pin: 1, min: 20, max: 30, label: 'первый штифт' },
        { pin: 2, min: 60, max: 70, label: 'второй штифт' }
      ],
      timerMs: 9000,
      pickDurability: 4,
      maxMistakes: 2,
      gradeThresholds: {
        successMaxTimeMs: 3000,
        successMaxMistakes: 0,
        partialMaxTimeMs: 8000,
        partialMaxMistakes: 2
      }
    };

    expect(resolveLockPinSetGrade(config, [
      { offsetMs: 1000, pinIndex: 0, position: 25 },
      { offsetMs: 2500, pinIndex: 1, position: 65 }
    ])).toBe('success');
    expect(resolveLockPinSetGrade(config, [
      { offsetMs: 1000, pinIndex: 0, position: 10 },
      { offsetMs: 3000, pinIndex: 0, position: 25 },
      { offsetMs: 7000, pinIndex: 1, position: 65 }
    ])).toBe('partial');
    expect(resolveLockPinSetGrade(config, [
      { offsetMs: 1000, pinIndex: 0, position: 10 },
      { offsetMs: 2000, pinIndex: 0, position: 11 },
      { offsetMs: 3000, pinIndex: 0, position: 12 }
    ])).toBe('fail');
  });
});
