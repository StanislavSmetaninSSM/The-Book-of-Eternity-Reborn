# StealthNoise QTE Contract

Source issue: #916 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/916
Parent epic: #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911

## Check shape

A StealthNoise QTE action uses the existing QTE action/check envelope:

```json
{
  "actionId": "cross_creaking_floor",
  "label": "Пересечь скрипучий настил",
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
      "gradeThresholds": {
        "successMaxNoise": 48,
        "successMaxOverThresholdMs": 0,
        "partialMaxNoise": 70,
        "partialMaxOverThresholdMs": 900
      },
      "warningLabel": "Доски начинают отвечать резким скрипом."
    }
  },
  "routing": {
    "success": { "nextChapterId": "silent_hall" },
    "partial": { "nextChapterId": "guard_stirs" },
    "fail": { "terminalOutcomeId": "alarm_raised" }
  }
}
```

## Required fields

- `check.type`: exactly `StealthNoise`.
- `check.baseDifficulty`: existing QTE integer difficulty range `1..5`.
- `check.primaryCharacteristic`: existing canonical lowercase stat id.
- `check.config.durationMs`: integer duration from `1000` to `30000` ms.
- `check.config.startingNoise`: integer or number from `0` to `100`, not above `dangerThreshold`.
- `check.config.dangerThreshold`: integer or number from `1` to `100`.
- `check.config.noiseDriftPerSecond`: positive number from `1` to `100`.
- `check.config.recoveryPerInput`: positive number from `1` to `100`.
- `check.config.allowedOverThresholdMs`: integer from `0` to `durationMs`.
- `check.config.gradeThresholds`: object with success and partial boundaries.
- `check.config.gradeThresholds.successMaxNoise`: number `0..dangerThreshold`.
- `check.config.gradeThresholds.successMaxOverThresholdMs`: integer `0..allowedOverThresholdMs`.
- `check.config.gradeThresholds.partialMaxNoise`: number from `successMaxNoise` to `100`.
- `check.config.gradeThresholds.partialMaxOverThresholdMs`: integer from `successMaxOverThresholdMs` to `durationMs`.
- `check.config.recoveryKey`: optional canonical QTE key token; absent means the implementation chooses a documented default.
- `check.config.recoveryLabel` and `warningLabel`: optional player-facing text. Empty strings are invalid when present.

## Validation rules

Validation must reject:

- missing or non-object `check.config`
- missing, non-integer, less-than-1000, or greater-than-30000 `durationMs`
- missing, negative, greater-than-100, or above-threshold `startingNoise`
- missing, non-positive, or greater-than-100 `dangerThreshold`
- missing, non-positive, or excessive `noiseDriftPerSecond`
- missing, non-positive, or excessive `recoveryPerInput`
- missing, negative, or duration-exceeding `allowedOverThresholdMs`
- missing/non-object `gradeThresholds`
- success thresholds that are harder than partial thresholds
- partial thresholds that cannot be reached before failure or that exceed the meter/duration range
- unsupported `recoveryKey` tokens when the implementation supports a player input token
- empty player-facing `recoveryLabel` or `warningLabel` when provided

Validation issue messages should name `StealthNoise` and the exact malformed field.

## Local resolution

- The resolver presents a visible noise meter, danger threshold, remaining-time cue, recovery input, and warning state.
- Noise starts at `startingNoise` and increases by `noiseDriftPerSecond` as elapsed time advances.
- Player recovery inputs reduce noise by `recoveryPerInput`, clamped to `0..100`.
- The resolver accumulates time spent above `dangerThreshold`.
- At the end of the configured duration, final noise and accumulated over-threshold time are compared to `gradeThresholds`.
- `success` is selected when final noise and over-threshold time are within success thresholds.
- `partial` is selected when success is missed but both values remain within partial thresholds.
- Otherwise the result is `fail`.
- Exceeding a terminal failure condition before duration may resolve early as `fail` if the implementation can prove the partial thresholds are already impossible.
- Escape/cancel resolves `fail` safely.
- The resolver must have deterministic test hooks or pure helper functions that avoid real-time sleeps.

## Difficulty and characteristic

The implementation should use a monotonic adjustment rule equivalent to one of these shapes, keeping tests/docs synchronized with the final choice:

- higher difficulty increases effective drift and/or lowers `allowedOverThresholdMs`;
- higher relevant stat tier increases recovery strength and/or raises over-threshold allowance;
- higher difficulty does not make StealthNoise easier for the same character/config;
- higher relevant characteristic tier does not make StealthNoise harder for the same difficulty/config.

A concrete acceptable formula is:

- `effectiveDrift = noiseDriftPerSecond + ((baseDifficulty - 3) * 1.5) - (statTier * 0.5)`, clamped to `1..100`;
- `effectiveRecovery = recoveryPerInput + statTier - Math.Max(0, baseDifficulty - 3)`, clamped to `1..100`;
- `effectiveAllowedOverThresholdMs = allowedOverThresholdMs - ((baseDifficulty - 3) * 100) + (statTier * 100)`, clamped to `0..durationMs`.

Codex may refine the exact formula if tests/docs/spec remain monotonic and synchronized.

## Input and accessibility

- The console must show text meter values, not only color or sound.
- Existing QTE audio cues may play when available, but audio is only an enhancement.
- GM-authored config must not encode keyboard layout or ask the player to switch OS layout.
- If a recovery key is exposed, use existing QTE key labels and RU/EN fallback helpers where applicable.
- Dynamic labels, warnings, and narrative text must be escaped before Spectre.Console markup rendering.

## Browser boundary

This issue does not implement full browser interactive StealthNoise. Browser surfaces may expose read-only action metadata if already required by existing QTE DTOs, but React must not duplicate gameplay resolution logic in this slice. Full browser parity remains #918.
