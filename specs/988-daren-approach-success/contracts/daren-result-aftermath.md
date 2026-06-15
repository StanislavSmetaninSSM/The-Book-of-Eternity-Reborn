# Daren Result Aftermath Contract: Approach Manor Success

Source issue: [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988)

## Contract Surface

- Route: `daren_qte_showcase`
- Beat/chapter: `approach_manor` / "Подступ к поместью"
- Action: `approach_manor_action` / "Выбрать тень у старой липы"
- Result grade: `success`
- Shared authority: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Consumer surfaces: console and browser both consume the same C# route data.

## Allowed Change

Replace only the `success` result prose string for `approach_manor_action` with a substantial Russian dark-fantasy aftermath insert.

## Required Narrative Signals

The success result must show:

- Daren remains the active point-of-view protagonist.
- The old linden / manor wall / blind lantern gap setting remains concrete.
- Daren's choice of shadow, step timing, body control, breath, hands, or listening demonstrates competence.
- Patrol, guard, lantern, dog, witness, or alarm pressure is avoided or softened by the clean result.
- Evidence/trace/noise risk is reduced rather than escalated.
- Atmosphere remains in-world: wet night, stone, garden, wall, leaves, shadow, or comparable approach details.
- The prose bridges toward the next Mira / `informant_parley_action` beat without changing route ordering.

## Forbidden Drift

The implementation must not change:

- route id, beat ids, beat order, title, action id, or action label;
- QTE type `BranchChoice`, choice ids/labels/grades, or routing targets;
- success/partial/fail grade identities;
- score deltas, reward tiers, persistent profile, New Game grants, or reward services;
- frontend/browser DTOs, endpoints, runtime state, save/session behavior, or GM-facing contracts;
- #989 partial, #990 fail, or #991-#1008 downstream result surfaces.

## Verification Authority

- Focused `DarenQteShowcaseTests` guard is the primary product-regression test.
- Affected Daren/QTE/docs/browser slice checks route and shared-surface drift.
- Spec Kit tasks/checklists record evidence but do not replace tests or GitHub issue/PR readback.
- Hermes owns final independent review, PR, merge, issue closure, label transition, and cleanup.
