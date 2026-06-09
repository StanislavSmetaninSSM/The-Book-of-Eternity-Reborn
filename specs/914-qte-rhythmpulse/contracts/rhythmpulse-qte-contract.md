# RhythmPulse QTE Contract

Source issue: #914 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/914
Parent epic: #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911

## Check shape

A RhythmPulse QTE action uses the existing QTE action/check envelope:

```json
{
  "actionId": "match_ritual_pulse",
  "label": "Подстроиться под ритм печати",
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
    "success": { "nextChapterId": "seal_resonates" },
    "partial": { "nextChapterId": "seal_wavers" },
    "fail": { "terminalOutcomeId": "ritual_breaks" }
  }
}
```

## Required fields

- `check.type`: exactly `RhythmPulse`.
- `check.baseDifficulty`: existing QTE integer difficulty range `1..5`.
- `check.primaryCharacteristic`: existing canonical lowercase stat id.
- `check.config.pulseCount`: integer pulse count from `2` to `16` before effective difficulty/stat adjustment.
- `check.config.beatIntervalMs`: integer interval from `300` to `3000` ms between authored pulses.
- `check.config.hitWindowMs`: integer early/late tolerance from `40` to `1000` ms around each pulse.
- `check.config.allowedMisses`: integer success miss tolerance from `0` to `pulseCount - 1`.
- `check.config.patternVariation`: optional string; absent/null means `steady`.

## Supported variation tokens

- `steady`: each pulse uses the authored beat interval.
- `accelerating`: later pulse intervals shorten deterministically while staying strictly increasing and playable.
- `swing`: intervals alternate long/short around the authored beat interval.

Unsupported strings, arrays, objects, booleans, and numbers are invalid.

## Input and accessibility

- Runtime uses Space as the local pulse key.
- Existing QTE audio cues may play when available, but audio is only an enhancement.
- The console must show visual/textual pulse timing, current pulse/progress, hit/miss counts, and remaining time so the check is not purely audio-dependent.
- GM-authored config must not encode keyboard layout or ask the player to switch OS layout.

## Validation rules

Validation must reject:

- missing or non-object `check.config`
- missing, non-integer, zero, negative, less-than-2, or greater-than-16 `pulseCount`
- missing, non-integer, less-than-300, or greater-than-3000 `beatIntervalMs`
- missing, non-integer, less-than-40, or greater-than-1000 `hitWindowMs`
- `hitWindowMs * 2 >= beatIntervalMs`, because adjacent pulse windows would overlap
- missing, non-integer, negative, or `>= pulseCount` `allowedMisses`
- missing? no; `patternVariation` is optional, but if present it must be a supported string or null
- unsupported or malformed `patternVariation`

Validation issue messages should name `RhythmPulse` and the exact malformed field.

## Local resolution

- The resolver generates a deterministic pulse schedule from effective pulse count, authored beat interval, and variation.
- Each Space press can satisfy at most one unmatched pulse if its elapsed offset is within `hitWindowMs` before or after that pulse.
- A run resolves `success` when missed pulses are within effective `allowedMisses`.
- A run resolves `partial` when it misses the success tolerance but hits at least half of effective pulses.
- Too few hits, no meaningful input by the end of the pattern, malformed config, or cancel resolves `fail`.
- Escape/cancel resolves `fail` safely.
- Non-Space keys do not count and do not crash.
- The resolver must have deterministic test hooks or pure helper functions that avoid real-time sleeps.

## Difficulty and characteristic

The implementation should use a monotonic adjustment rule equivalent to:

- Effective pulse count is `pulseCount + max(0, baseDifficulty - 3) - max(0, statTier / 2)`, clamped to `2..16` and never below authored `pulseCount - 2`.
- Effective hit window is `hitWindowMs - ((baseDifficulty - 3) * 10) + (statTier * 8)`, clamped to `40..1000` and then capped below half the beat interval so windows do not overlap.
- Effective allowed misses is `allowedMisses - max(0, baseDifficulty - 3) + max(0, statTier / 2)`, clamped to `0..effectivePulseCount - 1`.
- Higher `baseDifficulty` does not make RhythmPulse easier for the same character/config.
- Higher relevant characteristic tier does not make RhythmPulse harder for the same difficulty/config.

The adjustment must be covered by deterministic tests. Codex may refine the exact formula if tests/docs/spec remain monotonic and synchronized.

## Browser boundary

This issue does not implement full browser interactive RhythmPulse. Browser surfaces may expose read-only action metadata if already required by existing QTE DTOs, but React must not duplicate gameplay resolution logic in this slice. Full browser parity remains #918.
