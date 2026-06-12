# Contract: Daren Scene 09 Literary Page

## Scene Identity

- Beat id: `physical_pressure`
- Title: `Тяжёлая решётка`
- GitHub issue: #977
- Parent umbrella: #955

## Product Contract

The scene must read as a complete Russian dark-fantasy physical-pressure page focused on Daren holding the falling heavy grate while the staff case clears the niche. It must preserve the existing `MashInput` mechanics and the shared console/browser route contract.

## Required Content Signals

The final scene page should include, in natural literary prose:

- Daren as the active point-of-view protagonist.
- The cabinet/niche/staff case as a concrete setting immediately after Renara's voice and the rune/glass pressure.
- Heavy grate, iron, weight, mechanism, or comparable physical pressure as the central obstacle.
- Daren's body control: shoulders, hands, palms, fingers, breath, ribs, knees, muscles, pain, or comparable embodied action.
- Silence/noise/alarm stakes: avoiding a crash, wing-wide alarm, guards, house listening, or comparable consequences.
- A natural narrowing into the existing action of holding/lifting the grate until the final inch or until the staff case clears.

## Invariants

The implementation must not change:

- route id or route availability;
- beat id `physical_pressure`;
- title `Тяжёлая решётка`;
- action id or label for the scene;
- QTE check type/config/characteristics/difficulty (`MashInput`, Strength, current config);
- routing targets;
- score deltas;
- reward tiers/profile writes/New Game grants;
- browser or console runtime contract;
- endpoint/runtime state shape;
- frontend code.

## Verification Contract

The focused test for #977 should fail on the current compact synopsis and pass on the final page. It should use grouped motif checks so one generic token cannot satisfy a multi-part acceptance criterion, and it should pin the existing `MashInput` action/config/routing/scoring invariants.

## Local Verification Evidence

- The focused guard was added first and failed RED on the original synopsis: 49 passed / 1 failed / 0 skipped / 50 total.
- After replacing only shared C# route prose for `physical_pressure`, focused Daren tests passed: 50 passed / 0 failed / 0 skipped / 50 total.
- The affected Daren/QTE/docs/browser C# slice passed: 319 passed / 0 failed / 0 skipped / 319 total.
- Client and test-project builds completed with 0 warnings / 0 errors.
- Working-tree added-line static scan excluding Spec Kit docs returned `NO_MATCHES`.
- Hermes-owned independent review/PR/merge/closure remains outside this Codex implementation.
