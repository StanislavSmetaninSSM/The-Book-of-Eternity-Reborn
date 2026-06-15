# Contract: Daren Mira Whisper Success Literary Aftermath

**Feature**: `specs/991-daren-mira-success/`
**Source issue**: [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991)
**Parent**: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## Runtime Contract

No runtime-state, persistence, endpoint, browser DTO, validation, normalizer, pending/control, reward, profile, New Game, or GM-authored contract change is permitted for this feature.

The shared C# route data remains the single authority consumed by both console and browser clients:

- Route: Daren QTE showcase route returned by `QteSceneService.GetDarenShowcaseRoute()`.
- Chapter/beat: `informant_parley` / `Шёпот Миры`.
- Action: `informant_parley_action` / `Ответить Мире Ночной Нити`.
- Check: `PrecisionChoice`, primary characteristic `Characteristics.Wisdom`, base difficulty `2`, existing `DarenDialoguePrecisionChoiceConfig` choices.
- Correct success choice: `old_captain_shift` / `Назвать смену караула` / grade `success`.
- Other choices: `pay_for_rumor` remains `partial`; `threaten_contact` remains `fail`.
- Routing: success, partial, and fail all continue to `gadget_infiltration`.
- Score deltas: success, partial, and fail deltas remain unchanged.

## Prose Contract

Only the success result prose may change:

- Replace the current success string `Мира Ночная Нить принимает точный пароль Дарена и шепчет, что Лукьян дремлет у галереи, а Орвальд ведёт погоню сам.` with a substantial Russian literary aftermath insert.
- Keep Daren as the active point-of-view protagonist.
- Show clean success consequences: Daren answers Mira's trust test precisely, Mira accepts the answer without raising alarm, source-exposure risk decreases, Лукьян/Орвальд information is delivered in-world, and Daren leaves toward the hook-and-line beat with momentum.
- Preserve in-world Russian player-facing language.
- Do not expose `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, or implementation terminology in the default prose.

## Preservation Boundaries

The implementation must not change:

- `informant_parley` scene opening narrative (#970).
- `informant_parley_action` partial prose (#992).
- `informant_parley_action` fail prose (#993).
- Previous `approach_manor_action` result surfaces (#988/#989/#990).
- Downstream `gadget_infiltration_action` result surfaces (#994/#995/#996).
- Downstream `stealth_crossing_action` result surfaces (#997/#998/#999).
- Downstream `guard_interrogation_action` result surfaces (#1000/#1001/#1002).
- Downstream `lock_pick_action` result surfaces (#1003/#1004/#1005).
- Downstream `rune_memory_action` result surfaces (#1006/#1007/#1008).
- Any QTE mechanics, check config, route ids, action ids, choice ids/grades, score deltas, reward tiers, endpoints, runtime state, frontend/browser code, GM prompts, examples, or manifests.

## Verification Contract

- A focused test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` must fail against the old one-sentence success text before the production prose changes.
- The focused test must assert the result is substantial and clean, pin action/config/choice/routing/score invariants, preserve partial/fail siblings, preserve previous/downstream surfaces, and reject player-facing technical terms.
- Focused Daren tests and the affected Daren/QTE/docs/browser C# slice must pass locally before PR/merge.
- `git diff --check origin/main...HEAD` and an added-line static scan must pass before PR/merge.
