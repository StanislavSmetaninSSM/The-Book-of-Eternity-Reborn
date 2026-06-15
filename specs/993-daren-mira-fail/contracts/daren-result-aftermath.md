# Daren Result Aftermath Contract: Mira Whisper Fail

Source issue: [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993)

## Contract Surface

- Route: `daren_qte_showcase`
- Beat/chapter: `informant_parley` / "Шёпот Миры"
- Action: `informant_parley_action` / "Ответить Мире Ночной Нити"
- Result grade: `fail`
- Shared authority: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Consumer surfaces: console and browser both consume the same C# route data.

## Allowed Change

Replace only the `fail` result prose string for `informant_parley_action` with a substantial Russian dark-fantasy aftermath insert.

## Required Narrative Signals

The fail result must show:

- Daren remains the active point-of-view protagonist.
- Mira / Ночная Нить remains personified through body language, green ribbon or knife-hand detail, caution, anger, or witness pressure.
- Daren's threat or failed social pressure collapses trust instead of buying information.
- Useful information is lost, poisoned, withheld, or converted into risk.
- Source-exposure, witness, guard, pursuit, rumor, or evidence pressure becomes concrete.
- Wet awning / social atmosphere remains part of the aftermath.
- Daren's breath, voice, shoulders, hands, or body control show the consequence of the failed exchange.
- The prose bridges toward the wall / folded hook / line / `gadget_infiltration_action` beat without changing route ordering.

## Forbidden Drift

The implementation must not change:

- route id, beat ids, beat order, title, action id, or action label;
- QTE type `PrecisionChoice`, characteristic, difficulty, or choice ids/labels/grades;
- success/partial/fail grade identities;
- routing targets or terminal behavior;
- score deltas, reward tiers, persistent profile, New Game grants, or reward services;
- frontend/browser DTOs, endpoints, runtime state, save/session behavior, or GM-facing contracts;
- #991 success, #992 partial, #988-#990 previous, or #994-#1008 downstream result surfaces.

## Verification Authority

- Focused `DarenQteShowcaseTests` guard is the primary product-regression test.
- Affected Daren/QTE/docs/browser slice checks route and shared-surface drift.
- Spec Kit tasks/checklists record evidence but do not replace tests or GitHub issue/PR readback.
- Hermes owns final independent review, PR, merge, issue closure, label transition, and cleanup.
