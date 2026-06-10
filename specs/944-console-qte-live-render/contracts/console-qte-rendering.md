# Console QTE Rendering Contract for #944

Source issue: [#944](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/944)

## Purpose

This contract describes the client-owned console rendering boundary for timed QTE mini-games. It is not a GM-authored gameplay contract and does not add or change QTE JSON fields.

## Stable Shell

During a timed mini-game, the following elements should remain visually stable instead of being cleared and repainted every tick:

- QTE mini-game title/header.
- Player instructions.
- Panel border/frame.
- QTE RU/EN layout support note.

## Dynamic Body

The following elements may refresh on every tick through an in-place/live renderer target:

- Timer or remaining time.
- Timing-bar marker position.
- Mash input press counters and progress bars.
- Rhythm pulse marker/flash/hit/miss state.
- Prompt-chain current prompt state.
- Balance, charge, noise, pin, and comparable meter/body state.

## Required Rendering Rule

High-frequency QTE mini-game ticks MUST NOT call `AnsiConsole.Clear()` directly or through a helper. The implementation must update a renderable target in place or use a fallback that still avoids full-screen clear-per-tick behavior.

## Allowed Clears

A one-time `AnsiConsole.Clear()` remains allowed for scene transitions outside animation/timer ticks, such as QTE offer/prelude/result screens, menus, and blocking selection prompts.

## Out-of-Scope Contracts

This rendering contract does not alter:

- QTE check type names or config JSON.
- Validation rules.
- Grade/scoring/reward behavior.
- Browser QTE UI.
- GM prompt/rules/example requirements.
- Save files, runtime state schema, pending/control files, afterlife contracts, or campaign progression.

## Verification Expectations

- A RED source guard must fail against the old clear-per-tick implementation.
- The GREEN guard must cover TimingBar, MashInput, RhythmPulse, and at least one newer mini-game family.
- Focused QTE tests and build must pass after implementation.
- If manual visual verification is not possible in the autonomous environment, the final closure report must state the limitation rather than claiming observed flicker-free rendering.
