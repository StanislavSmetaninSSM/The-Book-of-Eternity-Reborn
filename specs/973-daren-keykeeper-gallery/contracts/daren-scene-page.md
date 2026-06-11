# Contract: Daren Scene 05 Literary Page

## Scene Identity

- Beat id: `guard_interrogation`
- Title: `Ключник в галерее`
- GitHub issue: #973
- Parent umbrella: #955

## Product Contract

The scene must read as a complete Russian dark-fantasy social-pressure page focused on Daren being stopped by Lукьян Седой Ключник near the service door after crossing the gallery.

## Required Content Signals

The final scene page should include, in natural literary prose:

- Daren as the active point-of-view protagonist.
- Service door / gallery continuation after the stealth crossing.
- Lукьян Седой Ключник as a personified NPC, not just a named obstacle.
- Lantern/key-ring/body-language details: keys, rings, old hands, face, posture, voice, or similar concrete signals.
- Suspicion/question/social pressure between Lукьян and Daren.
- Real dialogue or a visible exchange that leads into the existing choice/action.
- Stakes: whether Lукьян becomes a witness, raises alarm, or remains a mistaken shadow.

## Invariants

The implementation must not change:

- route id or route availability;
- beat id `guard_interrogation`;
- action id for the scene;
- QTE check type/config/characteristics/difficulty;
- routing targets;
- score deltas;
- reward tiers/profile writes/New Game grants;
- browser or console runtime contract;
- endpoint/runtime state shape.

## Verification Contract

The focused test for #973 should fail on the current compact synopsis and pass on the final page. It should use grouped motif checks so one generic token cannot satisfy a multi-part acceptance criterion.
