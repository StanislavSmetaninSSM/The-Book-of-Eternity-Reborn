# Contract: Daren Scene 10 Shared Literary Page

Source issue: [#978](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/978). Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955).

## Shared Route Contract

- Scene id: `timed_rhythm`.
- Title: `Пульс сигнализации`.
- Shared source: `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Console/browser parity: console and browser consume the same `DarenShowcaseBeat.PlayerText` / `QteChapter.Narrative`; no browser-only or console-only fork is allowed.

## QTE Action Contract To Preserve

- Action id: `timed_rhythm_action`.
- Label: `Двигаться между ударами кристалла`.
- Check type: `RhythmPulse`.
- Primary characteristic: `Characteristics.Speed`.
- Base difficulty: `3`.
- Config remains equivalent to `DarenRhythmPulseConfig()`:
  - `pulseCount = 5`
  - `beatIntervalMs = 640`
  - `hitWindowMs = 125`
  - `allowedMisses = 1`
  - `patternVariation = "swing"`
- Routing remains to `route_decision` for success, partial, and fail.
- Score deltas remain equivalent to `DarenScoreDeltas(stealth: 4, pursuit: 2)`.
- Reward tiers, permanent reward profile, New Game grants, route id, endpoints, runtime state, frontend files, and GM-facing contracts remain unchanged.

## Prose Contract

The new prose must be an in-world Russian dark-fantasy page, not a mechanical summary. It must include:

- Daren as the active point-of-view protagonist.
- Corridor/signal-crystal setting with red pulses/light and shadows on floor/walls.
- Daren's movement, breath, boot/step/body timing, shadow control, and intent.
- Continuity from the previous staff-case/heavy-grate pressure, such as pain, blood, posoh/staff burden, quiet wing, or awakened-house danger.
- Silence/noise/alarm/trace/guard stakes that naturally lead into the existing `RhythmPulse` action.

The prose must not expose `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, score/debug framing, file paths, or implementation terminology in default player-facing text.

## Test Contract

Add or update a focused C# guard that proves:

- the old compact synopsis fails before the prose change;
- the final prose is at least 1500 characters, at least 12 sentences, and mentions Daren at least 5 times;
- grouped motif checks cover every conceptual group above;
- action id, label, check type, Speed characteristic, base difficulty, config, routing, and score deltas did not drift;
- default player-facing technical terms remain absent.
