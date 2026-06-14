# Contract: Daren Silent Gallery Fail Literary Aftermath

**Feature**: `specs/999-daren-gallery-fail/`
**Source issue**: [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999)
**Parent**: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## Runtime Contract

No runtime-state, persistence, endpoint, browser DTO, validation, normalizer, pending/control, reward, profile, New Game, or GM-authored contract change is permitted for this feature.

The shared C# route data remains the single authority consumed by both console and browser clients:

- Route: Daren QTE showcase route returned by `QteSceneService.GetDarenShowcaseRoute()`.
- Chapter/beat: `stealth_crossing` / `Галерея без звука`.
- Action: `stealth_crossing_action` / `Пройти галерею без шума`.
- Check: `StealthNoise`, primary characteristic `Characteristics.Dexterity`, base difficulty `3`, existing `DarenStealthNoiseConfig()`.
- Routing: success, partial, and fail all continue to `guard_interrogation`.
- Score deltas: success, partial, and fail deltas remain unchanged.

## Prose Contract

Only the fail result prose may change:

- Replace the current fail string `Доска отвечает резким треском, и Дарен видит, как в дальнем крыле поднимается тревожный фонарь со свидетелем.` with a substantial Russian literary aftermath insert.
- Keep Daren as the active point-of-view protagonist.
- Show dangerous fail consequences: broken silence, floorboard/noise mistake, guard/fonar/witness pressure, evidence or remembered trace, pursuit/alarm escalation, and movement toward the service-door/keykeeper beat under pressure.
- Preserve in-world Russian player-facing language.
- Do not expose `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in the default prose.

## Preservation Boundaries

The implementation must not change:

- `stealth_crossing` scene opening narrative (#972).
- `stealth_crossing_action` success prose (#997).
- `stealth_crossing_action` partial prose (#998).
- Downstream `guard_interrogation_action` result surfaces (#1000/#1001/#1002).
- Downstream `lock_pick_action` result surfaces (#1003/#1004/#1005).
- Downstream `rune_memory_action` result surfaces (#1006/#1007/#1008).
- Any QTE mechanics, check config, route ids, action ids, score deltas, reward tiers, endpoints, runtime state, frontend/browser code, GM prompts, examples, or manifests.

## Verification Contract

- A focused test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` must fail against the old one-sentence fail text before the production prose changes.
- The focused test must assert the result is substantial and dangerous, pin action/config/routing/score invariants, preserve success/partial siblings, and reject player-facing technical terms.
- Focused Daren tests and the affected Daren/QTE/docs/browser C# slice must pass locally before PR/merge.
- `git diff --check origin/main...HEAD` and an added-line static scan must pass before PR/merge.
