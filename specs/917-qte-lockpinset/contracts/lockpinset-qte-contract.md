# LockPinSet QTE Contract

Source issue: #917 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/917
Parent epic: #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911

## Check shape

A LockPinSet QTE action uses the existing QTE action/check envelope:

```json
{
  "actionId": "pick_archive_door",
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
      "gradeThresholds": {
        "successMaxTimeMs": 9000,
        "successMaxMistakes": 0,
        "partialMaxTimeMs": 14000,
        "partialMaxMistakes": 2
      }
    }
  },
  "routing": {
    "success": { "nextChapterId": "archive_open_silently" },
    "partial": { "nextChapterId": "archive_open_noisy" },
    "fail": { "terminalOutcomeId": "lockpick_snaps_alarm" }
  }
}
```

## Required fields

- `check.type`: exactly `LockPinSet`.
- `check.baseDifficulty`: existing QTE integer difficulty range `1..5`.
- `check.primaryCharacteristic`: existing canonical lowercase stat id.
- `check.config.pinCount`: integer pin/tumbler count from `2` to `8`.
- `check.config.pinWindows`: array with exactly `pinCount` windows.
- `check.config.pinWindows[].pin`: optional one-based pin number matching its position when present.
- `check.config.pinWindows[].min`: number from `0` to `100`.
- `check.config.pinWindows[].max`: number greater than `min` and no greater than `100`.
- `check.config.timerMs`: integer timer from `1000` to `60000` ms.
- `check.config.pickDurability`: integer from `1` to `20`.
- `check.config.maxMistakes`: integer from `0` to `pickDurability`.
- `check.config.pinDriftPerSecond`: number from `0` to `100`.
- `check.config.gradeThresholds`: object with success and partial boundaries.
- `check.config.gradeThresholds.successMaxTimeMs`: integer `0..timerMs`.
- `check.config.gradeThresholds.successMaxMistakes`: integer `0..maxMistakes`.
- `check.config.gradeThresholds.partialMaxTimeMs`: integer from `successMaxTimeMs` to `timerMs`.
- `check.config.gradeThresholds.partialMaxMistakes`: integer from `successMaxMistakes` to `maxMistakes`.
- `check.config.adjustKey` and `setKey`: optional canonical QTE key tokens; absent means the implementation chooses documented defaults.
- `check.config.pinLabel`, `durabilityLabel`, and `warningLabel`: optional player-facing text. Empty strings are invalid when present.

## Validation rules

Validation must reject:

- missing or non-object `check.config`
- unsupported `pinCount` outside `2..8`
- missing/non-array `pinWindows`
- `pinWindows` length that differs from `pinCount`
- missing, nonnumeric, unordered, zero-width, negative, or over-100 pin-window bounds
- duplicate or out-of-order authored `pin` numbers when present
- missing, non-integer, less-than-1000, or greater-than-60000 `timerMs`
- missing, non-integer, less-than-1, or greater-than-20 `pickDurability`
- missing, negative, or durability-exceeding `maxMistakes`
- missing, negative, or excessive `pinDriftPerSecond`
- missing/non-object `gradeThresholds`
- success thresholds that are harder than partial thresholds
- partial thresholds that cannot be reached before timer/durability failure
- missing `routing.success`, `routing.partial`, or `routing.fail`
- unsupported physical key tokens when a key field is present
- empty player-facing `pinLabel`, `durabilityLabel`, or `warningLabel` when provided

Validation issue messages should name `LockPinSet` and the exact malformed field.

## Local resolution

- The resolver presents visible pin states, target-window feedback, remaining-time cue, pick durability/mistake count, and active control guidance.
- Pin positions use a bounded `0..100` track.
- A pin is considered set/open when its final position falls inside its target window and the player confirms it.
- Mistakes represent confirmations outside the target window, damaging the pick or adding noise.
- The pick breaks when durability reaches zero or mistakes exceed the allowed threshold.
- At the end of the configured timer, all pins must be opened for success or partial; unopened locks fail.
- `success` is selected when all pins are opened within success time and mistake thresholds.
- `partial` is selected when the lock opens but time or mistakes/noise exceed success thresholds while remaining within partial thresholds.
- Otherwise the result is `fail`.
- Escape/cancel resolves `fail` safely.
- The resolver must have deterministic test hooks or pure helper functions that avoid real-time sleeps.

## Difficulty and characteristic

The implementation should use a monotonic adjustment rule equivalent to one of these shapes, keeping tests/docs synchronized with the final choice:

- higher difficulty narrows effective windows, increases drift, reduces timer, or reduces mistake/durability forgiveness;
- higher relevant stat tier widens effective windows, reduces drift, increases timer, or increases durability/mistake forgiveness;
- higher difficulty does not make LockPinSet easier for the same character/config;
- higher relevant characteristic tier does not make LockPinSet harder for the same difficulty/config.

A concrete acceptable formula is:

- `effectiveWindowPadding = statTier - Math.Max(0, baseDifficulty - 3)`, applied symmetrically and clamped so windows remain inside `0..100`;
- `effectivePinDriftPerSecond = pinDriftPerSecond + ((baseDifficulty - 3) * 0.5) - (statTier * 0.25)`, clamped to `0..100`;
- `effectiveTimerMs = timerMs - ((Math.Clamp(baseDifficulty, 1, 5) - 3) * 500) + (statTier * 250)`, clamped to `1000..60000`;
- `effectiveMaxMistakes = maxMistakes - Math.Max(0, Math.Clamp(baseDifficulty, 1, 5) - 3) + Math.Min(2, Math.Max(0, statTier) / 2)`, clamped to `0..pickDurability`.

Codex may refine the exact formula if tests/docs/spec remain monotonic and synchronized.

## Input and accessibility

- The console must show text pin positions/windows and durability/mistake counts, not only color or sound.
- Existing QTE audio cues may play when available, but audio is only an enhancement.
- GM-authored config must not encode keyboard layout or ask the player to switch OS layout.
- If physical keys are exposed, use existing QTE key labels and RU/EN fallback helpers where applicable.
- Dynamic labels, warnings, and narrative text must be escaped before Spectre.Console markup rendering.

## Browser boundary

This issue does not implement full browser interactive LockPinSet. Browser surfaces may expose read-only action metadata if already required by existing QTE DTOs, but React must not duplicate gameplay resolution logic in this slice. Full browser parity remains #918.
