# Contract: Daren Rune Memory Partial Literary Aftermath

## Source

- Issue: [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)
- Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Scene prerequisite: [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975)
- Completed sibling intentionally preserved: [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006)
- Remaining sibling intentionally out of scope: [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Authored Surface

`BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the sole shared C# authority for the Daren QTE showcase route. #1007 may change only the `partial` result prose for:

- Chapter / beat: `rune_memory` / "Руны на дверце"
- Action: `rune_memory_action`
- Check type: `PatternMemory`
- Result surface: `partial`

Console and browser clients must continue to consume this same shared route text.

## Required Invariants

The implementation must preserve:

- Route id, offer title, chapter order, beat ids, and chapter titles.
- `rune_memory_action` id and label.
- `PatternMemory` check type, Perception primary characteristic, difficulty, and pattern/sequence config semantics.
- Routing targets for success/partial/fail.
- Success/partial/fail grade identities and score deltas.
- Reward tiers, profile persistence, New Game Ink Feather grants, terminal outcome, endpoints, runtime state, and browser/frontend contracts.
- Existing `success` and `fail` result prose for `rune_memory_action`.

## Literary Result Requirements

The partial result prose must:

- Be a substantial Russian dark-fantasy aftermath insert, not a terse result notification.
- Keep Daren as active protagonist and point of view.
- Show a mixed outcome: the ward pattern is held well enough to open the way, but a cracked rune, scar in glass, delayed alarm, evidence trace, physical cost, or later house/Renara consequence remains.
- Use concrete sensory/action detail around runed glass, protective symbols, cold light, dust/stone/metal, breathing, hands, silence, and the listening house.
- Bridge naturally toward `ward_steward_parley` / "Голос Ренары" without rewriting that scene.
- Avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence partial text before the prose change and pass after it. The guard should pin both literary criteria and mechanical invariants. Affected Daren/QTE/docs/browser C# tests must also pass locally.

No GM-facing docs/examples update is expected because this is client-owned authored showcase prose and does not add or change a GM-authored capability, command, state field, validation rule, pending/control surface, response field, or runtime contract.
