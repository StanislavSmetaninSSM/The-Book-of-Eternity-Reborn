# Contract: Daren Scene 06 Literary Page

## Scene Identity

- Beat id: `lock_pick`
- Title: `Замок кабинета`
- GitHub issue: #974
- Parent umbrella: #955

## Product Contract

The scene must read as a complete Russian dark-fantasy burglary page focused on Daren opening the old cabinet-office door lock after passing the keykeeper/gallery pressure. It must be tactile, tense, and player-facing while preserving the existing `LockPinSet` mechanics.

## Required Content Signals

The final scene page should include, in natural literary prose:

- Daren as the active point-of-view protagonist.
- The cabinet-office door, old lock, keyhole, plate, pins, pick, or similar concrete mechanism details.
- Daren's body control: hands, breath, hearing, stillness, pulse, posture, or comparable craft signals.
- Stealth/evidence stakes: noise, scratch marks, disturbed dust, guards/witnesses, or a trail that could lead to the staff.
- A natural narrowing from observation and pressure into the existing action of setting the lock pins.

## Invariants

The implementation must not change:

- route id or route availability;
- beat id `lock_pick`;
- action id or label for the scene;
- QTE check type/config/characteristics/difficulty (`LockPinSet`, Dexterity, current config);
- routing targets;
- score deltas;
- reward tiers/profile writes/New Game grants;
- browser or console runtime contract;
- endpoint/runtime state shape.

## Verification Contract

The focused test for #974 should fail on the current compact synopsis and pass on the final page. It should use grouped motif checks so one generic token cannot satisfy a multi-part acceptance criterion.
