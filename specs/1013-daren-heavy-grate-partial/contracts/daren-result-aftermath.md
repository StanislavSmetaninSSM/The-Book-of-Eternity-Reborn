# Contract: Daren Heavy-Grate Partial Result Aftermath

## Scope

This contract applies only to the shared C# Daren QTE route data for issue #1013:

- Chapter/beat: `physical_pressure` / "Тяжёлая решётка"
- Action: `physical_pressure_action` / "Удержать тяжёлую решётку"
- Result surface: `partial`

It does not change route ids, command surfaces, browser endpoints, persistence, reward/profile writes, GM-facing contracts, or frontend runtime behavior.

## Authored Prose Requirements

The partial result text must:

1. Be Russian, in-world, player-facing dark-fantasy prose.
2. Read as a substantial post-QTE aftermath insert, not as a one-sentence result notification.
3. Keep Daren as the active POV protagonist.
4. Show the mixed outcome: the staff/posoh is freed, but the grate hits or catches Daren and leaves a visible cost, delay, trace, doubt, noise, wound, pursuit/evidence risk, or later consequence.
5. Use concrete physical/sensory details: iron weight, shoulder/ribs/hands/breath, stone niche, case/staff, oil/metal/stone sounds, and the listening house.
6. Bridge toward the next alarm-pulse corridor beat without rewriting `timed_rhythm` or altering route order.
7. Avoid default player-facing implementation terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, raw ids, or test/agent language.

## Invariants

The implementation must preserve:

- route id and Daren showcase availability;
- beat id `physical_pressure` and title "Тяжёлая решётка";
- action id `physical_pressure_action` and label "Удержать тяжёлую решётку";
- check type `MashInput`, Strength characteristic, base difficulty, and config shape;
- routing targets for success/partial/fail;
- score deltas and reward/profile/New Game behavior;
- success and fail result texts except for a documented minimal connective fix required by tests;
- shared C# route authority consumed by both console and browser;
- absence of new state files, endpoints, frontend-only story forks, or GM-authored contract surfaces.

## Verification Contract

A compliant change includes:

- a RED/GREEN focused guard in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` for `physical_pressure_action` partial aftermath;
- focused Daren tests passing;
- affected Daren/QTE/docs/browser C# slice passing;
- client and test-project builds passing;
- `git diff --check origin/main...HEAD` passing;
- added-line static scan over non-Spec changed files showing no secrets/injection/eval/deserialization/SQL-formatting findings;
- diff review showing only expected code/test/Spec Kit files changed.
