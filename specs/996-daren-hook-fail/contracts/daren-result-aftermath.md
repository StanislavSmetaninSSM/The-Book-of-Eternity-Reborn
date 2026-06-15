# Contract: Daren Hook and Line Fail Literary Aftermath

**Feature**: `specs/996-daren-hook-fail/`
**Source issue**: [#996](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/996)
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

Only the fail result prose may change:

- Replace the current fail string `Крюк срывается с края; шум будит двор, и Дарен успевает уйти в тень только после собачьего лая.` with a substantial Russian literary aftermath insert.
- Keep Daren as the active point-of-view protagonist.
- Show dangerous fail consequences: the hook slips or tears free, noise wakes the yard, dog or guard pressure becomes concrete, and Daren continues only with pursuit/evidence pressure.
- Preserve in-world Russian player-facing language. The prose must not mention `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, JSON, files, tests, agents, or implementation details.
- Bridge toward `stealth_crossing` / `Галерея без звука` without rewriting that downstream scene.

## Preservation Contract

The implementation must preserve:

- `gadget_infiltration_action` success prose from #994.
- `gadget_infiltration_action` partial prose from #995.
- Downstream result prose for #997-#1008.
- Route order, action ids, labels, check types, characteristics, difficulty, routing, score deltas, reward/profile behavior, endpoints, runtime state, and frontend/backend boundaries.
- Console/browser parity by keeping the authored result in shared C# route data.

## Verification Contract

The implementation is acceptable only when:

- A new focused test fails against the old fail text before the production prose rewrite.
- The focused Daren filter and affected Daren/QTE/docs/browser C# slice pass after the rewrite.
- `git diff --check origin/main...HEAD` is clean.
- Added-line static scans find no secrets or dangerous code patterns in code/test changes.
- Independent review confirms literary quality, grade semantics, scope control, and Spec Kit alignment before PR/merge.
