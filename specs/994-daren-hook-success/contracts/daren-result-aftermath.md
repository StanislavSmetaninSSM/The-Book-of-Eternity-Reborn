# Contract: Daren Hook and Line Success Literary Aftermath

**Feature**: `specs/994-daren-hook-success/`
**Source issue**: [#994](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/994)
**Parent**: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## Runtime Contract

No runtime-state, persistence, endpoint, browser DTO, validation, normalizer, pending/control, reward, profile, New Game, or GM-authored contract change is permitted for this feature.

The shared C# route data remains the single authority consumed by both console and browser clients:

- Route: Daren QTE showcase route returned by `QteSceneService.GetDarenShowcaseRoute()`.
- Chapter/beat: `gadget_infiltration` / `Крюк и леска`.
- Action: `gadget_infiltration_action` / `Запустить складной крюк`.
- Check: `ChargeRelease`, primary characteristic `Characteristics.Dexterity`, base difficulty `3`, existing action config.
- Routing: success, partial, and fail all continue to `stealth_crossing`.
- Score deltas: success, partial, and fail deltas remain unchanged.

## Prose Contract

Only the success result prose may change:

- Replace the current success string `Крюк ложится на балкон мягко, и Дарен поднимается над двором, пока леска молчит в ладони.` with a substantial Russian literary aftermath insert.
- Keep Daren as the active point-of-view protagonist.
- Show clean success consequences: soft hook catch, controlled line tension, quiet balcony ascent, reduced immediate courtyard risk, no useful witness/evidence/alarm trail, and movement toward the gallery beat with momentum.
- Preserve in-world Russian player-facing language.
- Do not expose `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in the default prose.

## Preservation Boundaries

The implementation must not change:

- `gadget_infiltration` scene opening narrative (#971).
- `gadget_infiltration_action` partial prose (#995).
- `gadget_infiltration_action` fail prose (#996).
- Downstream `stealth_crossing_action` result surfaces (#997/#998/#999).
- Downstream `guard_interrogation_action` result surfaces (#1000/#1001/#1002).
- Downstream `lock_pick_action` result surfaces (#1003/#1004/#1005).
- Downstream `rune_memory_action` result surfaces (#1006/#1007/#1008).
- Any QTE mechanics, check config, route ids, action ids, score deltas, reward tiers, endpoints, runtime state, frontend/browser code, GM prompts, examples, or manifests.

## Verification Contract

- A focused test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` must fail against the old one-sentence success text before the production prose changes.
- The focused test must assert the result is substantial and clean, pin action/config/routing/score invariants, preserve partial/fail siblings, and reject player-facing technical terms.
- Focused Daren tests and the affected Daren/QTE/docs/browser C# slice must pass locally before PR/merge.
- `git diff --check origin/main...HEAD` and an added-line static scan must pass before PR/merge.
