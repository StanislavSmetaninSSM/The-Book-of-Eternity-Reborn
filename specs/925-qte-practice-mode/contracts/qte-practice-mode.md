# Contract: QTE Practice Mode

Source issue: [#925](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925)
Related: #918 browser QTE parity, #920 layout-independent input, #924 scoring, #919 Daren showcase.

## Ownership and authority

QTE Practice Mode is a client-owned training surface. It does not require GM-authored practice scenes and it does not create new canonical campaign progression. The C# client/runtime owns catalog generation, attempt lifecycle, result resolution, no-mutation guarantees, and any local web API write semantics. Browser React may render catalog/attempt/result state and collect mini-game input, but it must not become the authority for campaign or permanent reward mutation.

## Practice catalog

A practice catalog entry has:

- stable QTE type id matching an implemented QTE family;
- player-facing Russian name and short training description;
- supported surfaces (`console`, `browser`, or both);
- instructions shown before timers/action loops start;
- difficulty presets that deterministically generate valid practice configs;
- availability state if a future type is known but not playable.

Playable catalog entries for #925 must cover implemented QTE families:

- `BranchChoice`
- `TimingBar`
- `PromptChain`
- `BalanceMeter`
- `ChargeRelease`
- `MashInput`
- `PatternMemory`
- `RhythmPulse`
- `PrecisionChoice`
- `StealthNoise`
- `LockPinSet`

Unimplemented future types must be hidden or clearly unavailable with no broken start action.

## Practice attempt lifecycle

A practice attempt starts from a catalog entry and difficulty preset. It must:

1. generate a deterministic valid practice QTE config;
2. validate or route through the same QTE implementation family used by normal play;
3. show instructions before the active timer/action phase;
4. resolve to `success`, `partial`, or `fail` through existing QTE result logic;
5. show feedback and allow retry, difficulty/type change, or exit;
6. discard or keep only explicitly local/session training state that has no campaign/reward authority.

Practice attempts must not write:

- ordinary campaign turn state;
- pending campaign/GM actions;
- achievements or profile unlocks;
- Ink Feathers;
- XP or character progression;
- inventory, equipment, quests, readable documents, location storage, or transport state;
- Daren showcase ending/progress/reward state;
- afterlife/Chaos Sea/Shining Abode pending/control files.

## Keyboard and mini-game behavior

Practice Mode must reuse #920 QTE input handling. Canonical QTE key tokens and labels remain scoped to QTE surfaces and must not affect ordinary text input.

Browser practice must reuse #918 mini-game behavior/components where practical and preserve shortcut bubbling guards so frame-level QTE shortcuts do not swallow keyboard activation from buttons or other interactive controls.

## Scoring boundary

If a practice attempt uses #924 scoring/ranks, the score/rank summary is local training feedback only. It must not grant achievements, rewards, Ink Feathers, XP, Daren ending progress, or campaign progression. Player-facing copy must label this boundary clearly.

## Documentation boundary

Docs/help/source guards must explain:

- Practice Mode is for learning QTE mechanics;
- no rewards or story progress are granted;
- practice does not require GM-authored scenes;
- Daren #919 can point stuck players to Practice Mode but Practice Mode does not implement Daren route/rewards;
- console and browser surfaces should remain semantically aligned.
