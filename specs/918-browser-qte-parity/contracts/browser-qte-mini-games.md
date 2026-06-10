# Browser QTE Mini-Games Contract

Source issue: [#918 Browser QTE parity](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918)

## Authority boundary

- C# remains the only authority for QTE offer acceptance, action resolution, routing, history, completion, and game-state writes.
- React may calculate a local mini-game outcome grade (`success`, `partial`, `fail`) from player input, but it submits that grade through the existing browser QTE action endpoint.
- React must not write `game_state/`, pending/control files, rewards, score/rank state, achievements, or Daren/practice-mode state.

## Browser action projection

Each `QteWebActionDto` should provide:

- `actionId`: stable action id from the active chapter.
- `label`: player-facing action label.
- `checkType`: one of the supported QTE check types or a future/unknown string.
- `baseDifficulty`: numeric difficulty from the authored check.
- `primaryCharacteristic`: characteristic id/name used by the existing QTE check.
- `requiresSubmittedGrade`: `false` for BranchChoice/direct static choice; `true` for browser mini-games that submit a computed grade.
- `gradeOptions`: existing grade vocabulary for compatibility; not rendered as a default manual selector for supported mini-games.
- `checkConfig`: read-only normalized configuration for the matching mini-game, with unknown/future types represented as unsupported rather than raw debug JSON in default UI.

## Supported check behavior

| Check type | Browser behavior | Submit grade source |
| --- | --- | --- |
| `BranchChoice` | Static player choice button; no grade selector. | Existing authored branch grade via C# resolution. |
| `TimingBar` | Timing/target-zone interaction with keyboard/pointer activation. | Local timing result. |
| `PromptChain` | Sequence prompt input with clear progress. | Local sequence accuracy/time result. |
| `BalanceMeter` | Balance/keep-in-zone interaction. | Local meter stability result. |
| `ChargeRelease` | Hold/release or charge-threshold interaction. | Local release timing result. |
| `MashInput` | Repeated key/button presses within duration. | Local press count vs projected targets. |
| `PatternMemory` | Reveal sequence, then replay sequence. | Local replay correctness/time result. |
| `RhythmPulse` | Beat/pulse hit interaction with non-audio fallback. | Local hit/miss result. |
| `PrecisionChoice` | Timed choice list with correct/partial/fail choice mapping. | Local choice/timeout result. |
| `StealthNoise` | Noise meter/recovery interaction. | Local meter/over-threshold result. |
| `LockPinSet` | Pin positioning/confirm interaction. | Local pin success/mistake/time result. |
| Unknown/future | Player-facing unsupported message. | No default player submit. |

## Non-goals

- No scoring metrics/ranks (#924).
- No Daren showcase route (#919).
- No practice mode (#925).
- No new GM-authored QTE fields.
- No console behavior changes except docs/source guards if browser parity guidance must mention the new surface.

## Verification contract

- Tests must prove supported checks do not render manual grade selectors in default player UI.
- Tests must prove `success`, `partial`, and `fail` can be produced deterministically by mini-game helpers where applicable.
- C# tests must prove DTO projection is sufficient and endpoint semantics remain unchanged.
- Frontend verification must include typecheck, player-facing tests, and build.
