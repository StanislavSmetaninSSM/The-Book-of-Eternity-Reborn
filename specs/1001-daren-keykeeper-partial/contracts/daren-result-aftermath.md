# Contract: Daren Keykeeper Gallery Partial Literary Aftermath

## Source

- Issue: [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)
- Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Scene prerequisite: [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973)
- Completed success sibling intentionally preserved: [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000)
- Remaining fail sibling intentionally preserved: [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002)
- Downstream completed results intentionally preserved: [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Authored Surface

`BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the sole shared C# authority for the Daren QTE showcase route. #1001 may change only the `partial` result prose for:

- Chapter / beat: `guard_interrogation` / "Ключник в галерее"
- Action: `guard_interrogation_action`
- Check type: `PrecisionChoice`
- Result surface: `partial`

Console and browser clients must continue to consume this same shared route text.

## Required Invariants

The implementation must preserve:

- Route id, offer title, chapter order, beat ids, and chapter titles.
- `guard_interrogation_action` id and label.
- `PrecisionChoice` check type, Persuasion primary characteristic, difficulty, dialogue choice ids/labels/grade mapping/hints, and accepted answer semantics.
- Routing targets for success/partial/fail.
- Success/partial/fail grade identities and score deltas.
- Reward tiers, profile persistence, New Game Ink Feather grants, terminal outcome, endpoints, runtime state, and browser/frontend contracts.
- Existing `success` result prose for `guard_interrogation_action` completed by #1000.
- Existing `fail` result prose for `guard_interrogation_action` reserved for #1002.
- Existing `lock_pick_action` result prose from completed downstream siblings #1003/#1004/#1005.
- Existing `rune_memory_action` result prose from completed downstream siblings #1006/#1007/#1008.

## Literary Result Requirements

The partial result prose must:

- Be a substantial Russian dark-fantasy aftermath insert, not a terse result notification.
- Keep Daren as active protagonist and point of view.
- Show a mixed social outcome: Daren's plausible order, cover story, or imperfect social proof gets him past Лукьян and through the service door, but a remembered face/cloak/detail, journal/log suspicion, delayed doubt, or witness/evidence pressure remains.
- Use concrete sensory/action detail around Лукьян's keys, lantern, voice, breath, service door, sleeping guard/gallery silence, Mira's phrase/authority, Daren's face/voice/body control, and the corridor toward the cabinet.
- Bridge naturally toward `lock_pick` / "Замок кабинета" without rewriting that scene.
- Avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence partial text before the prose change and pass after it. The guard should pin both literary criteria and mechanical invariants. Affected Daren/QTE/docs/browser C# tests must also pass locally.

No GM-facing docs/examples update is expected because this is client-owned authored showcase prose and does not add or change a GM-authored capability, command, state field, validation rule, pending/control surface, response field, or runtime contract.

## Setup Evidence

- Baseline focused Daren tests passed 64 / failed 0 / skipped 0 / total 64 before implementation.
- Baseline affected Daren/QTE/docs/browser C# slice passed 333 / failed 0 / skipped 0 / total 333 before implementation.
- Implementation evidence, independent review evidence, PR evidence, and post-merge closure evidence remain Hermes/Codex lifecycle responsibilities for this closure unit.
