# Contract: Daren Cabinet Lock Partial Literary Aftermath

## Source

- Issue: [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004)
- Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Scene prerequisite: [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974)
- Completed sibling intentionally preserved: [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)
- Remaining sibling intentionally preserved: [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005)
- Downstream completed results intentionally preserved: [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Authored Surface

`BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the sole shared C# authority for the Daren QTE showcase route. #1004 may change only the `partial` result prose for:

- Chapter / beat: `lock_pick` / "Замок кабинета"
- Action: `lock_pick_action`
- Check type: `LockPinSet`
- Result surface: `partial`

Console and browser clients must continue to consume this same shared route text.

## Required Invariants

The implementation must preserve:

- Route id, offer title, chapter order, beat ids, and chapter titles.
- `lock_pick_action` id and label.
- `LockPinSet` check type, Dexterity primary characteristic, difficulty, pin windows, timer, pick durability, max mistakes, drift, control keys, labels, and grade thresholds.
- Routing targets for success/partial/fail.
- Success/partial/fail grade identities and score deltas.
- Reward tiers, profile persistence, New Game Ink Feather grants, terminal outcome, endpoints, runtime state, and browser/frontend contracts.
- Existing `success` result prose for `lock_pick_action` from #1003.
- Existing `fail` result prose for `lock_pick_action`, owned by #1005.
- Existing `rune_memory_action` result prose from completed downstream siblings #1006/#1007/#1008.

## Literary Result Requirements

The partial result prose must:

- Be a substantial Russian dark-fantasy aftermath insert, not a terse result notification.
- Keep Daren as active protagonist and point of view.
- Show a mixed outcome: the lock/cabinet opens or admits Daren, but an imperfect trace remains visible as scratch, dust disturbance, delay, doubt, wound, or later evidence/alarm pressure.
- Use concrete sensory/action detail around pins, pick/tension tool, keyhole/bronze plate, scratch/dust, breath, hands/fingers, silence or small sound, and cabinet door movement.
- Bridge naturally toward `rune_memory` / "Руны на дверце" and the staff/futlar without rewriting that scene.
- Avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence partial text before the prose change and pass after it. The guard should pin both literary criteria and mechanical invariants. Affected Daren/QTE/docs/browser C# tests must also pass locally.

No GM-facing docs/examples update is expected because this is client-owned authored showcase prose and does not add or change a GM-authored capability, command, state field, validation rule, pending/control surface, response field, or runtime contract.

## Setup Evidence

- Baseline focused Daren tests passed 61 / failed 0 / skipped 0 / total 61 before implementation.
- Baseline affected Daren/QTE/docs/browser C# slice passed 330 / failed 0 / skipped 0 / total 330 before implementation.
- Implementation evidence, independent review evidence, PR evidence, and post-merge closure evidence remain Hermes/Codex lifecycle responsibilities for this closure unit.
