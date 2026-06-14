# Contract: Daren Silent Gallery Success Literary Aftermath

## Source

- Issue: [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997)
- Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Scene prerequisite: [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972)
- Sibling result follow-ups intentionally preserved: [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998) and [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999)
- Downstream completed results intentionally preserved: [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000), [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001), [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Authored Surface

`BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the sole shared C# authority for the Daren QTE showcase route. #997 may change only the `success` result prose for:

- Chapter / beat: `stealth_crossing` / "Галерея без звука"
- Action: `stealth_crossing_action`
- Check type: `StealthNoise`
- Result surface: `success`

Console and browser clients must continue to consume this same shared route text.

## Required Invariants

The implementation must preserve:

- Route id, offer title, chapter order, beat ids, and chapter titles.
- `stealth_crossing_action` id and label.
- `StealthNoise` check type, Dexterity primary characteristic, base difficulty, StealthNoise config, and accepted/noise semantics.
- Routing targets for success/partial/fail.
- Success/partial/fail grade identities and score deltas.
- Reward tiers, profile persistence, New Game Ink Feather grants, terminal outcome, endpoints, runtime state, and browser/frontend contracts.
- Existing `partial` result prose for `stealth_crossing_action` reserved for #998.
- Existing `fail` result prose for `stealth_crossing_action` reserved for #999.
- Existing `guard_interrogation_action` result prose from completed downstream siblings #1000/#1001/#1002.
- Existing `lock_pick_action` result prose from completed downstream siblings #1003/#1004/#1005.
- Existing `rune_memory_action` result prose from completed downstream siblings #1006/#1007/#1008.

## Literary Result Requirements

The success result prose must:

- Be a substantial Russian dark-fantasy aftermath insert, not a terse result notification.
- Keep Daren as active protagonist and point of view.
- Show a clean stealth outcome: Daren reads the gallery's floorboards, dust, portrait glass, curtains, sleeping air, and guard presence; he moves without waking alarm, leaving a clear witness, or producing usable evidence.
- Use concrete sensory/action detail around boards, creaks, dust, portrait frames/glass, breath, hand/boot placement, curtains/doors, and the pressure of the house listening.
- Bridge naturally toward `guard_interrogation` / "Ключник в галерее" and the service-door/keykeeper continuity without rewriting that scene.
- Avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence success text before the prose change and pass after it. The guard should pin both literary criteria and mechanical invariants. Affected Daren/QTE/docs/browser C# tests must also pass locally.

No GM-facing docs/examples update is expected because this is client-owned authored showcase prose and does not add or change a GM-authored capability, command, state field, validation rule, pending/control surface, response field, or runtime contract.

## Setup Evidence

- Baseline focused Daren tests passed 66 / failed 0 / skipped 0 / total 66 before implementation.
- Baseline affected Daren/QTE/docs/browser C# slice passed 335 / failed 0 / skipped 0 / total 335 before implementation.
- Implementation evidence, independent review evidence, PR evidence, and post-merge closure evidence remain Hermes/Codex lifecycle responsibilities for this closure unit.
