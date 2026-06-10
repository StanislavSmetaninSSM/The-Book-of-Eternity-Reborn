import type {
  QteChargeReleaseCheckConfigDto,
  QteLockPinSetCheckConfigDto,
  QteLockPinWindowDto,
  QteMashInputCheckConfigDto,
  QtePrecisionChoiceOptionDto,
  QteRhythmPulseCheckConfigDto,
  QteStealthNoiseCheckConfigDto,
  QteTimingBarCheckConfigDto
} from '../api/contracts';

export type QteMiniGameGrade = 'success' | 'partial' | 'fail';

export interface QteLockPinAttempt {
  offsetMs: number;
  pinIndex: number;
  position: number;
  canceled?: boolean;
}

export function resolveTimingBarGrade(
  config: Pick<QteTimingBarCheckConfigDto, 'successStart' | 'successWidth' | 'partialStart' | 'partialWidth'>,
  position: number
): QteMiniGameGrade {
  if (position >= config.successStart && position < config.successStart + config.successWidth) {
    return 'success';
  }

  return position >= config.partialStart && position < config.partialStart + config.partialWidth ? 'partial' : 'fail';
}

export function resolvePromptChainGrade(
  expectedSequence: readonly string[],
  enteredSequence: readonly string[],
  allowedMistakes: number
): QteMiniGameGrade {
  return resolveTokenSequenceGrade(expectedSequence, enteredSequence, allowedMistakes);
}

export function resolveBalanceMeterGrade(safeTicks: number, totalTicks: number): QteMiniGameGrade {
  if (totalTicks <= 0) {
    return 'fail';
  }

  const ratio = safeTicks / totalTicks;
  if (ratio >= 0.7) {
    return 'success';
  }

  return ratio >= 0.45 ? 'partial' : 'fail';
}

export function resolveChargeReleaseGrade(
  config: Pick<QteChargeReleaseCheckConfigDto, 'targetStart' | 'targetWidth' | 'partialPadding'>,
  charge: number
): QteMiniGameGrade {
  const successEnd = config.targetStart + config.targetWidth;
  if (charge >= config.targetStart && charge <= successEnd) {
    return 'success';
  }

  const partialStart = Math.max(0, config.targetStart - config.partialPadding);
  const partialEnd = Math.min(100, successEnd + config.partialPadding);
  return charge >= partialStart && charge <= partialEnd ? 'partial' : 'fail';
}

export function resolveMashInputGrade(
  config: Pick<QteMashInputCheckConfigDto, 'successTarget' | 'partialTarget'>,
  matchedPresses: number
): QteMiniGameGrade {
  if (matchedPresses >= config.successTarget) {
    return 'success';
  }

  return matchedPresses >= config.partialTarget ? 'partial' : 'fail';
}

export function resolvePatternMemoryGrade(
  expectedSequence: readonly string[],
  enteredSequence: readonly string[],
  allowedMistakes: number,
  timedOut = false
): QteMiniGameGrade {
  if (timedOut) {
    return 'fail';
  }

  return resolveTokenSequenceGrade(expectedSequence, enteredSequence, allowedMistakes);
}

export function resolveRhythmPulseGrade(
  config: Pick<QteRhythmPulseCheckConfigDto, 'pulseOffsetsMs' | 'hitWindowMs' | 'allowedMisses'>,
  hitOffsetsMs: readonly number[]
): QteMiniGameGrade {
  if (config.pulseOffsetsMs.length === 0) {
    return 'fail';
  }

  const matched = new Set<number>();
  for (const hitOffset of [...hitOffsetsMs].sort((left, right) => left - right)) {
    let bestIndex = -1;
    let bestDistance = config.hitWindowMs + 1;
    config.pulseOffsetsMs.forEach((pulseOffset, index) => {
      if (matched.has(index)) {
        return;
      }

      const distance = Math.abs(hitOffset - pulseOffset);
      if (distance <= config.hitWindowMs && distance < bestDistance) {
        bestIndex = index;
        bestDistance = distance;
      }
    });

    if (bestIndex >= 0) {
      matched.add(bestIndex);
    }
  }

  const misses = config.pulseOffsetsMs.length - matched.size;
  const allowedMisses = clamp(config.allowedMisses, 0, config.pulseOffsetsMs.length - 1);
  if (misses <= allowedMisses) {
    return 'success';
  }

  const partialTarget = Math.max(1, Math.ceil(config.pulseOffsetsMs.length / 2));
  return matched.size >= partialTarget ? 'partial' : 'fail';
}

export function resolvePrecisionChoiceGrade(
  choices: readonly Pick<QtePrecisionChoiceOptionDto, 'id' | 'grade'>[],
  selectedChoiceId: string | null,
  timedOut: boolean,
  timeoutGrade: string = 'fail'
): QteMiniGameGrade {
  if (timedOut || !selectedChoiceId) {
    return timeoutGrade.trim().toLowerCase() === 'partial' ? 'partial' : 'fail';
  }

  const choice = choices.find((item) => item.id === selectedChoiceId);
  return choice ? normalizeGrade(choice.grade) : 'fail';
}

export function resolveStealthNoiseGrade(
  config: Pick<QteStealthNoiseCheckConfigDto, 'durationMs' | 'startingNoise' | 'dangerThreshold' | 'noiseDriftPerSecond' | 'recoveryPerInput' | 'allowedOverThresholdMs' | 'gradeThresholds'>,
  recoveryOffsetsMs: readonly number[],
  canceled = false
): QteMiniGameGrade {
  if (canceled) {
    return 'fail';
  }

  const sample = sampleStealthNoise(config, recoveryOffsetsMs, config.durationMs);
  const thresholds = config.gradeThresholds;
  const successOverThresholdLimit = Math.min(config.allowedOverThresholdMs, thresholds.successMaxOverThresholdMs);
  if (sample.noise <= thresholds.successMaxNoise && sample.overThresholdMs <= successOverThresholdLimit) {
    return 'success';
  }

  const partialOverThresholdLimit = Math.min(config.allowedOverThresholdMs, thresholds.partialMaxOverThresholdMs);
  return sample.noise <= thresholds.partialMaxNoise && sample.overThresholdMs <= partialOverThresholdLimit ? 'partial' : 'fail';
}

export function resolveLockPinSetGrade(
  config: Pick<QteLockPinSetCheckConfigDto, 'pinCount' | 'pinWindows' | 'timerMs' | 'pickDurability' | 'maxMistakes' | 'gradeThresholds'>,
  attempts: readonly QteLockPinAttempt[],
  canceled = false
): QteMiniGameGrade {
  if (canceled || config.pinCount <= 0 || config.pinWindows.length < config.pinCount) {
    return 'fail';
  }

  const opened = Array.from({ length: config.pinCount }, () => false);
  let mistakes = 0;
  let durabilityRemaining = config.pickDurability;
  let openedAtMs: number | null = null;

  for (const attempt of [...attempts].sort((left, right) => left.offsetMs - right.offsetMs)) {
    if (attempt.canceled) {
      return 'fail';
    }

    if (attempt.offsetMs < 0) {
      continue;
    }

    if (attempt.offsetMs > config.timerMs) {
      break;
    }

    if (attempt.pinIndex < 0 || attempt.pinIndex >= config.pinCount) {
      mistakes++;
      durabilityRemaining--;
    } else if (!opened[attempt.pinIndex] && isLockPinAttemptInsideWindow(config.pinWindows[attempt.pinIndex], attempt.position)) {
      opened[attempt.pinIndex] = true;
      if (opened.every(Boolean)) {
        openedAtMs = attempt.offsetMs;
        break;
      }
    } else if (!opened[attempt.pinIndex]) {
      mistakes++;
      durabilityRemaining--;
    }

    if (durabilityRemaining <= 0 || mistakes > config.maxMistakes) {
      return 'fail';
    }
  }

  if (!opened.every(Boolean) || openedAtMs === null) {
    return 'fail';
  }

  const thresholds = config.gradeThresholds;
  if (openedAtMs <= thresholds.successMaxTimeMs && mistakes <= thresholds.successMaxMistakes) {
    return 'success';
  }

  return openedAtMs <= thresholds.partialMaxTimeMs && mistakes <= thresholds.partialMaxMistakes ? 'partial' : 'fail';
}

function resolveTokenSequenceGrade(
  expectedSequence: readonly string[],
  enteredSequence: readonly string[],
  allowedMistakes: number
): QteMiniGameGrade {
  if (expectedSequence.length === 0 || enteredSequence.length < expectedSequence.length) {
    return 'fail';
  }

  let matches = 0;
  let mistakes = 0;
  for (let index = 0; index < expectedSequence.length; index++) {
    if (normalizeToken(enteredSequence[index]) === normalizeToken(expectedSequence[index])) {
      matches++;
    } else {
      mistakes++;
    }
  }

  if (mistakes === 0 && matches === expectedSequence.length) {
    return 'success';
  }

  const effectiveAllowedMistakes = clamp(allowedMistakes, 0, Math.max(0, expectedSequence.length - 1));
  const partialTarget = Math.max(1, Math.ceil(expectedSequence.length / 2));
  return mistakes <= effectiveAllowedMistakes && matches >= partialTarget ? 'partial' : 'fail';
}

function sampleStealthNoise(
  config: Pick<QteStealthNoiseCheckConfigDto, 'durationMs' | 'startingNoise' | 'dangerThreshold' | 'noiseDriftPerSecond' | 'recoveryPerInput'>,
  recoveryOffsetsMs: readonly number[],
  elapsedMs: number
): { noise: number; overThresholdMs: number } {
  const endMs = clamp(elapsedMs, 0, config.durationMs);
  let noise = clamp(config.startingNoise, 0, 100);
  let currentOffsetMs = 0;
  let overThresholdMs = 0;

  for (const offsetMs of recoveryOffsetsMs.filter((offset) => offset >= 0 && offset <= endMs).sort((left, right) => left - right)) {
    const advanced = advanceStealthNoise(config, noise, offsetMs - currentOffsetMs);
    noise = advanced.noise;
    overThresholdMs += advanced.overThresholdMs;
    currentOffsetMs = offsetMs;
    noise = clamp(noise - config.recoveryPerInput, 0, 100);
  }

  const advanced = advanceStealthNoise(config, noise, endMs - currentOffsetMs);
  return {
    noise: advanced.noise,
    overThresholdMs: Math.round(overThresholdMs + advanced.overThresholdMs)
  };
}

function advanceStealthNoise(
  config: Pick<QteStealthNoiseCheckConfigDto, 'dangerThreshold' | 'noiseDriftPerSecond'>,
  startNoise: number,
  deltaMs: number
): { noise: number; overThresholdMs: number } {
  if (deltaMs <= 0) {
    return { noise: clamp(startNoise, 0, 100), overThresholdMs: 0 };
  }

  const endNoise = clamp(startNoise + (config.noiseDriftPerSecond * deltaMs / 1000), 0, 100);
  const overThresholdMs = estimateOverThresholdMs(config.dangerThreshold, startNoise, endNoise, deltaMs);
  return { noise: endNoise, overThresholdMs };
}

function estimateOverThresholdMs(dangerThreshold: number, startNoise: number, endNoise: number, deltaMs: number): number {
  if (startNoise > dangerThreshold && endNoise > dangerThreshold) {
    return deltaMs;
  }

  if (startNoise <= dangerThreshold && endNoise <= dangerThreshold) {
    return 0;
  }

  const span = endNoise - startNoise;
  if (span <= 0) {
    return startNoise > dangerThreshold ? deltaMs : 0;
  }

  const crossingRatio = clamp((dangerThreshold - startNoise) / span, 0, 1);
  return deltaMs * (1 - crossingRatio);
}

function isLockPinAttemptInsideWindow(window: QteLockPinWindowDto, position: number): boolean {
  return position >= window.min && position <= window.max;
}

function normalizeGrade(value: string): QteMiniGameGrade {
  return value === 'success' || value === 'partial' ? value : 'fail';
}

function normalizeToken(value: string | undefined): string {
  return (value ?? '').trim().toLowerCase();
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
