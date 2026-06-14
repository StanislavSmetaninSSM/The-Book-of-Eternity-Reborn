# Contract: Daren Keykeeper Gallery Fail Literary Aftermath

## Source

- Issue: [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002)
- Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Scene prerequisite: [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973)
- Completed keykeeper siblings intentionally preserved: [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000) and [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001)
- Downstream completed results intentionally preserved: [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Authored Surface

`BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the sole shared C# authority for the Daren QTE showcase route. #1002 may change only the `fail` result prose for:

- Chapter / beat: `guard_interrogation` / "Ключник в галерее"
- Action: `guard_interrogation_action`
- Check type: `PrecisionChoice`
- Result surface: `fail`

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
- Existing `partial` result prose for `guard_interrogation_action` completed by #1001.
- Existing `lock_pick_action` result prose from completed downstream siblings #1003/#1004/#1005.
- Existing `rune_memory_action` result prose from completed downstream siblings #1006/#1007/#1008.

## Literary Result Requirements

The fail result prose must:

- Be a substantial Russian dark-fantasy aftermath insert, not a terse result notification.
- Keep Daren as active protagonist and point of view.
- Show a dangerous social failure: Daren's silence, hidden-face attempt, or missing proper answer collapses cover; Лукьян raises the lantern, keys, voice, or witness memory; alarm/evidence/pursuit pressure becomes concrete.
- Use concrete sensory/action detail around Лукьян's keys, lantern, voice, breath, service door, sleeping guard/gallery silence, Mira's missing phrase/authority, Daren's exposed face/voice/body control, and the corridor toward the cabinet.
- Bridge naturally toward `lock_pick` / "Замок кабинета" without rewriting that scene or changing route order.
- Avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence fail text before the prose change and pass after it. The guard should pin both literary criteria and mechanical invariants. Affected Daren/QTE/docs/browser C# tests must also pass locally.

No GM-facing docs/examples update is expected because this is client-owned authored showcase prose and does not add or change a GM-authored capability, command, state field, validation rule, pending/control surface, response field, or runtime contract.

## Setup Evidence

- Baseline focused Daren tests passed 65 / failed 0 / skipped 0 / total 65 before implementation.
- Baseline affected Daren/QTE/docs/browser C# slice passed 334 / failed 0 / skipped 0 / total 334 before implementation.
- Implementation evidence, independent review evidence, PR evidence, and post-merge closure evidence remain Hermes/Codex lifecycle responsibilities for this closure unit.
