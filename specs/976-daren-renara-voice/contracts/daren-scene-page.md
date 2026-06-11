# Contract: Daren Scene 08 Literary Page

## Scene Identity

- Beat id: `ward_steward_parley`
- Title: `Голос Ренары`
- GitHub issue: #976
- Parent umbrella: #955

## Product Contract

The scene must read as a complete Russian dark-fantasy magical-security dialogue page focused on Daren answering Renara Wardova after the rune-memory ward has noticed him. It must be atmospheric, tense, and player-facing while preserving the existing `PrecisionChoice` mechanics.

## Required Content Signals

The final scene page should include, in natural literary prose:

- Daren as the active point-of-view protagonist.
- Renara Wardova as a concrete named magical-security authority, expressed through voice, reflection, ward/seal presence, glass, light, pressure, or comparable embodied signals.
- Real interaction, dialogue, question/answer pressure, or visible conversational duel between Renara and Daren.
- Daren's answer strategy: false seal, inspection, promise, misdirection, or a comparable in-world logic that leads into the existing answer choice.
- Carry-forward from the previous rune-memory scene: extinguished blue runes, glass/case/ward, signal, trace, house watching/listening, or the danger of the house learning his voice.
- A natural narrowing into the existing action of answering Renara without waking the house, and a bridge toward the next physical-pressure/case movement beat.

## Invariants

The implementation must not change:

- route id or route availability;
- beat id `ward_steward_parley`;
- action id or label for the scene;
- QTE check type/config/characteristics/difficulty (`PrecisionChoice`, current dialogue choice config);
- routing targets;
- score deltas;
- reward tiers/profile writes/New Game grants;
- browser or console runtime contract;
- endpoint/runtime state shape.

## Verification Contract

The focused test for #976 should fail on the current compact synopsis and pass on the final page. It should use grouped motif checks so one generic token cannot satisfy a multi-part acceptance criterion, and it should pin the existing `PrecisionChoice` action/config/routing/scoring invariants.

## Local Verification Evidence

- The focused guard was added first and failed RED on the original synopsis: 48 passed / 1 failed / 0 skipped / 49 total.
- After replacing only shared C# route prose for `ward_steward_parley`, focused Daren tests passed: 49 passed / 0 failed / 0 skipped / 49 total.
- The affected Daren/QTE/docs/browser C# slice passed: 318 passed / 0 failed / 0 skipped / 318 total.
- Working-tree added-line production C# forbidden-term scan returned `NO_MATCHES`.
- Hermes-owned independent review/PR/merge/closure remains outside this Codex implementation.
