# MashInput QTE Contract

Source issue: #912 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/912

## Check shape

A MashInput QTE action uses the existing QTE action/check envelope:

```json
{
  "actionId": "force_door",
  "label": "Толкать дверь всем весом",
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
    "success": { "nextChapterId": "door_open" },
    "partial": { "nextChapterId": "door_stuck" },
    "fail": { "terminalOutcomeId": "caught" }
  }
}
```

## Required fields

- `check.type`: exactly `MashInput`.
- `check.baseDifficulty`: existing QTE integer difficulty range `1..5`.
- `check.primaryCharacteristic`: existing canonical lowercase stat id.
- `check.config.keys`: non-empty array of canonical QTE key tokens.
- `check.config.durationMs`: integer duration from 750 to 10000 for the local rapid-input window.
- `check.config.targetPresses`: integer from 1 to 80 required for success before difficulty/stat adjustment; it must also be possible for `durationMs` at 12 presses per second.
- `check.config.partialThreshold`: numeric ratio greater than `0` and less than or equal to `1`; reaching this ratio of the effective success target resolves `partial`.

## Supported key tokens

MashInput reuses the #920 layout-independent QTE key contract:

- `q`, `w`, `e`, `a`, `s`, `d`, `space`
- Console fallback characters `й`, `ц`, `у`, `ф`, `ы`, `в` match the corresponding physical Latin keys only inside QTE input.
- Prompt labels should use `Q / Й`, `W / Ц`, `E / У`, `A / Ф`, `S / Ы`, `D / В`, and `Space`.

## Validation rules

Validation must reject:

- missing or non-object `check.config`
- missing, empty, duplicate, or unsupported `keys`
- missing, non-integer, less-than-750, or greater-than-10000 `durationMs`
- missing, non-integer, zero, negative, greater-than-80, or physically impossible `targetPresses`
- missing, non-number, zero, negative, or greater-than-one `partialThreshold`
- thresholds that would require more presses for partial than for success after rounding rules are applied

The current feasibility cap is `floor(durationMs / 1000 * 12)` matching presses. For example, a 1000 ms MashInput may not require 40 presses.

Validation issue messages should name `MashInput` and the exact malformed field.

## Local resolution

- Matching key press count at or above the effective success target resolves `success`.
- Matching key press count at or above the effective partial target but below success resolves `partial`.
- Matching key press count below partial resolves `fail`.
- Escape/cancel resolves `fail` safely.
- Non-matching keys do not count and do not crash.
- The resolver must have deterministic test hooks or pure helper functions that avoid real-time sleeps.

## Difficulty and characteristic

The implementation uses this monotonic adjustment rule:

- Effective success target is `targetPresses + (baseDifficulty - 3) - statTier`, clamped to `1..80`.
- The existing QTE stat tier resolver maps the relevant `primaryCharacteristic` to `-2..3`.
- Higher `baseDifficulty` does not make MashInput easier for the same character/config.
- Higher relevant characteristic tier does not make MashInput harder for the same difficulty/config.
- Effective partial target is `ceil(effectiveSuccessTarget * partialThreshold)`, clamped to `1..effectiveSuccessTarget`.

The adjustment changes effective target presses and is covered by deterministic tests.

## Browser boundary

This issue does not implement full browser interactive MashInput. Browser surfaces may expose read-only action metadata if already required by existing QTE DTOs, but React must not duplicate gameplay resolution logic in this slice. Full browser parity remains #918.
