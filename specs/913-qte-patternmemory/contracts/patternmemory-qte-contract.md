# PatternMemory QTE Contract

Source issue: #913 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/913
Parent epic: #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911

## Check shape

A PatternMemory QTE action uses the existing QTE action/check envelope:

```json
{
  "actionId": "repeat_rune_pulse",
  "label": "Повторить вспышки рун",
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
    "success": { "nextChapterId": "seal_open" },
    "partial": { "nextChapterId": "seal_flickers" },
    "fail": { "terminalOutcomeId": "alarm" }
  }
}
```

## Required fields

- `check.type`: exactly `PatternMemory`.
- `check.baseDifficulty`: existing QTE integer difficulty range `1..5`.
- `check.primaryCharacteristic`: existing canonical lowercase stat id.
- `check.config.alphabet`: non-empty unique array of canonical QTE key tokens used to generate or display the repeatable sequence.
- `check.config.sequenceLength`: integer sequence length from `2` to `12` before effective difficulty/stat adjustment.
- `check.config.revealMs`: integer reveal-phase duration from `500` to `15000` ms.
- `check.config.inputTimeoutMs`: integer input-phase timeout from `1000` to `30000` ms and large enough for the sequence.
- `check.config.allowedMistakes`: integer mistake tolerance from `0` to `sequenceLength - 1`.

## Supported key tokens

PatternMemory reuses the #920 layout-independent QTE key contract:

- `q`, `w`, `e`, `a`, `s`, `d`, `space`
- Console fallback characters `й`, `ц`, `у`, `ф`, `ы`, `в` match the corresponding physical Latin keys only inside QTE input.
- Prompt labels should use `Q / Й`, `W / Ц`, `E / У`, `A / Ф`, `S / Ы`, `D / В`, and `Space`.

## Validation rules

Validation must reject:

- missing or non-object `check.config`
- missing, empty, duplicate, or unsupported `alphabet`
- missing, non-integer, less-than-2, or greater-than-12 `sequenceLength`
- missing, non-integer, less-than-500, or greater-than-15000 `revealMs`
- missing, non-integer, less-than-1000, greater-than-30000, or sequence-impossible `inputTimeoutMs`
- missing, non-integer, negative, or `>= sequenceLength` `allowedMistakes`
- combinations where effective adjustment would make the check unplayable or failure impossible

Validation issue messages should name `PatternMemory` and the exact malformed field.

## Local resolution

- The reveal phase shows the generated sequence using player-facing key labels, then the input phase collects the player's repeat attempt.
- A perfect repeat resolves `success`.
- An imperfect repeat with mistakes at or below effective tolerance and enough matched symbols to show meaningful progress resolves `partial`.
- Too many mistakes, timeout, empty/no meaningful input, or cancel resolves `fail`.
- Escape/cancel during reveal or input resolves `fail` safely.
- Non-matching keys count as mistakes only during the input phase and do not crash.
- The resolver must have deterministic test hooks or pure helper functions that avoid real-time sleeps.

## Difficulty and characteristic

The implementation should use a monotonic adjustment rule equivalent to:

- Effective sequence length is `sequenceLength + max(0, baseDifficulty - 3) - max(0, statTier / 2)`, rounded/clamped to `2..12` and never below authored `sequenceLength - 2`.
- Effective reveal time is `revealMs - ((baseDifficulty - 3) * 150) + (statTier * 100)`, clamped to `500..15000`.
- Effective input timeout is `inputTimeoutMs - ((baseDifficulty - 3) * 250) + (statTier * 150)`, clamped to `1000..30000` and not below `effectiveSequenceLength * 300` ms.
- Effective allowed mistakes is `allowedMistakes - max(0, baseDifficulty - 3) + max(0, statTier / 2)`, clamped to `0..effectiveSequenceLength - 1`.
- Higher `baseDifficulty` does not make PatternMemory easier for the same character/config.
- Higher relevant characteristic tier does not make PatternMemory harder for the same difficulty/config.

The adjustment must be covered by deterministic tests. Codex may refine the exact formula if tests/docs/spec remain monotonic and synchronized.

## Browser boundary

This issue does not implement full browser interactive PatternMemory. Browser surfaces may expose read-only action metadata if already required by existing QTE DTOs, but React must not duplicate gameplay resolution logic in this slice. Full browser parity remains #918.
