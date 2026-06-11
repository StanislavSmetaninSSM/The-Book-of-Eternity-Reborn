# Contract: Daren Scene 04 Literary Page

## Scene Identity

- Beat id: `stealth_crossing`
- Title: `Галерея без звука`
- GitHub issue: #972
- Parent umbrella: #955

## Product Contract

The scene must read as a complete Russian dark-fantasy stealth page focused on Daren crossing the manor gallery without waking the guard or giving the estate a trace toward the cabinet.

## Required Content Signals

The final scene page should include, in natural literary prose:

- Daren as the active point-of-view protagonist.
- Gallery setting: portraits, dust, corridor/frames/walls/floor, strip of light or equivalent confined visibility.
- Guard pressure: sleeping/breathing guard, lantern/light, the risk of one sound waking the guard.
- Body/movement detail: breath, steps, weight transfer, fingers/shoulder/knees/boots/cloak, or similar controlled stealth motion.
- Sound/noise stakes: creak, scrape, breath, floorboard, leather, metal, echo, silence, or similar sonic tension.
- Natural lead-in to the existing stealth/noise QTE action.

## Invariants

The implementation must not change:

- route id or route availability;
- beat id `stealth_crossing`;
- action id for the scene;
- QTE check type/config/characteristics/difficulty;
- routing targets;
- score deltas;
- reward tiers/profile writes/New Game grants;
- browser or console runtime contract;
- endpoint/runtime state shape.

## Verification Contract

The focused test for #972 should fail on the current compact synopsis and pass on the final page. It should use grouped motif checks so one generic token cannot satisfy a multi-part acceptance criterion.
