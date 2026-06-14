# Contract: Daren Rune Memory Fail Literary Aftermath

## Source

- Issue: [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)
- Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Scene prerequisite: [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975)
- Completed siblings intentionally preserved: [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006) and [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)

## Authored Surface

`BookOfEternityClient/Services/QteSceneService.Daren.cs` remains the sole shared C# authority for the Daren QTE showcase route. #1008 may change only the `fail` result prose for:

- Chapter / beat: `rune_memory` / "Руны на дверце"
- Action: `rune_memory_action`
- Check type: `PatternMemory`
- Result surface: `fail`

Console and browser clients must continue to consume this same shared route text.

## Required Invariants

The implementation must preserve:

- Route id, offer title, chapter order, beat ids, and chapter titles.
- `rune_memory_action` id and label.
- `PatternMemory` check type, Perception primary characteristic, difficulty, and pattern/sequence config semantics.
- Routing targets for success/partial/fail.
- Success/partial/fail grade identities and score deltas.
- Reward tiers, profile persistence, New Game Ink Feather grants, terminal outcome, endpoints, runtime state, and browser/frontend contracts.
- Existing `success` and `partial` result prose for `rune_memory_action`.

## Literary Result Requirements

The fail result prose must:

- Be a substantial Russian dark-fantasy aftermath insert, not a terse result notification.
- Keep Daren as active protagonist and point of view.
- Show a failed/dangerous outcome: the ward pattern rejects or marks him, the house remembers his touch/name/heat, and concrete alarm, evidence, witness, Renara, or pursuit pressure escalates.
- Use concrete sensory/action detail around runed glass, protective symbols, cold or violent blue light, dust/stone/metal, breathing, hands, silence, and the listening house.
- Bridge naturally toward `ward_steward_parley` / "Голос Ренары" without rewriting that scene.
- Avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence fail text before the prose change and pass after it. The guard should pin both literary criteria and mechanical invariants. Affected Daren/QTE/docs/browser C# tests must also pass locally.

No GM-facing docs/examples update is expected because this is client-owned authored showcase prose and does not add or change a GM-authored capability, command, state field, validation rule, pending/control surface, response field, or runtime contract.

## Verification Evidence

- Focused RED: 59 passed / 1 failed / 0 skipped / 60 total, with the new fail guard rejecting the retired one-sentence text.
- Focused GREEN: 60 passed / 0 failed / 0 skipped / 60 total after the fail prose rewrite.
- Affected Daren/QTE/docs/browser slice: 329 passed / 0 failed / 0 skipped / 329 total.
- Client and test-project builds: 0 warnings / 0 errors.
- Added-line source/test static scan: `NO_MATCHES`.
- Scope check: success/partial result prose, route/action/check/config/routing/scoring/rewards, endpoints, runtime state, frontend/browser files, and GM-facing docs/examples were not changed.
