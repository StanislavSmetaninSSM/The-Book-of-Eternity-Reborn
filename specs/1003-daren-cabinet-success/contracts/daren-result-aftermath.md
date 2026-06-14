# Contract: Daren Cabinet Lock Success Literary Aftermath

## Source

- Issue: [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003)
- Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Scene prerequisite: [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974)
- Siblings intentionally preserved: [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004) and [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005)
- Downstream completed results intentionally preserved: [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Authored Surface

`BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the sole shared C# authority for the Daren QTE showcase route. #1003 may change only the `success` result prose for:

- Chapter / beat: `lock_pick` / "Замок кабинета"
- Action: `lock_pick_action`
- Check type: `LockPinSet`
- Result surface: `success`

Console and browser clients must continue to consume this same shared route text.

## Required Invariants

The implementation must preserve:

- Route id, offer title, chapter order, beat ids, and chapter titles.
- `lock_pick_action` id and label.
- `LockPinSet` check type, Dexterity primary characteristic, difficulty, pin windows, timer, pick durability, max mistakes, drift, control keys, labels, and grade thresholds.
- Routing targets for success/partial/fail.
- Success/partial/fail grade identities and score deltas.
- Reward tiers, profile persistence, New Game Ink Feather grants, terminal outcome, endpoints, runtime state, and browser/frontend contracts.
- Existing `partial` and `fail` result prose for `lock_pick_action`.
- Existing `rune_memory_action` result prose from completed downstream siblings #1006/#1007/#1008.

## Literary Result Requirements

The success result prose must:

- Be a substantial Russian dark-fantasy aftermath insert, not a terse result notification.
- Keep Daren as active protagonist and point of view.
- Show a clean/best outcome: the pins settle, the lock opens without noise, the bronze plate or dust keeps no visible scratch, and Daren's competence reduces evidence or alarm risk.
- Use concrete sensory/action detail around pins, pick/tension tool, keyhole/bronze plate, dust, breath, hands/fingers, silence, and cabinet door movement.
- Bridge naturally toward `rune_memory` / "Руны на дверце" and the staff/futlar without rewriting that scene.
- Avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence success text before the prose change and pass after it. The guard should pin both literary criteria and mechanical invariants. Affected Daren/QTE/docs/browser C# tests must also pass locally.

No GM-facing docs/examples update is expected because this is client-owned authored showcase prose and does not add or change a GM-authored capability, command, state field, validation rule, pending/control surface, response field, or runtime contract.

## Implementation Evidence

- RED: `DarenLockPickSuccess_ReadsAsCleanCabinetAftermathWithoutMechanicDrift` failed against the old one-sentence success string with 60 passed / 1 failed / 0 skipped / 61 total.
- Authored success aftermath measures 1,909 characters / 16 sentences / 4 `Дарен` mentions / 298 words.
- GREEN focused Daren tests passed 61 / failed 0 / skipped 0 / total 61.
- Affected Daren/QTE/docs/browser C# slice passed 330 / failed 0 / skipped 0 / total 330.
- Post-commit `git diff --check origin/main...HEAD` passed with exit code 0; post-commit code-focused added-line static scan returned `NO_MATCHES`.
- Scope inspection confirmed `lock_pick_action` partial/fail prose, downstream `rune_memory_action` prose, mechanics/config/routing/score deltas/rewards, frontend/browser files, runtime state, and GM docs were not changed.
