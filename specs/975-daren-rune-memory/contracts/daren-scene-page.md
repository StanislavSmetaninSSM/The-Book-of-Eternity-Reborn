# Contract: Daren Scene 07 Literary Page

## Scene Identity

- Beat id: `rune_memory`
- Title: `Руны на дверце`
- GitHub issue: #975
- Parent umbrella: #955

## Product Contract

The scene must read as a complete Russian dark-fantasy magical-security page focused on Daren reading and memorizing the rune pattern on the case door before the theft escalates. It must be atmospheric, tense, and player-facing while preserving the existing `PatternMemory` mechanics.

## Required Content Signals

The final scene page should include, in natural literary prose:

- Daren as the active point-of-view protagonist.
- The case door, glass, blue runes, magical ward/lock, or similar concrete magical-security details.
- Daren's body and memory craft: eyes, breath, pulse, stillness, finger/hand restraint, counting, pattern retention, or comparable craft signals.
- Surveillance/alarm stakes: the house seeming to watch him, a ward waking, a signal before the theft, guards/witnesses, or a magical trace that could betray the route to the staff.
- A natural narrowing from observation and pressure into the existing action of repeating the rune pattern.

## Invariants

The implementation must not change:

- route id or route availability;
- beat id `rune_memory`;
- action id or label for the scene;
- QTE check type/config/characteristics/difficulty (`PatternMemory`, current config);
- routing targets;
- score deltas;
- reward tiers/profile writes/New Game grants;
- browser or console runtime contract;
- endpoint/runtime state shape.

## Verification Contract

The focused test for #975 should fail on the current compact synopsis and pass on the final page. It should use grouped motif checks so one generic token cannot satisfy a multi-part acceptance criterion.

## Local Verification Evidence

- `DarenRuneMemory_ReadsAsRuneWardMemoryPageWithoutMechanicDrift` was added before the prose rewrite and failed on the compact-synopsis page-length assertion: 47 passed / 1 failed / 0 skipped / 48 total.
- After replacing the shared `rune_memory` prose, focused Daren tests passed: 48 passed / 0 failed / 0 skipped / 48 total.
- The focused test pins the unchanged action id, label, `PatternMemory` check, `Perception` characteristic, base difficulty, pattern config, routing to `ward_steward_parley`, and score deltas.
- Added production prose lines scanned clean for forbidden default-player technical terms: `NO_MATCHES`.
