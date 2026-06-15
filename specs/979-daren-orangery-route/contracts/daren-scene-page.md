# Contract: Daren Scene Page — #979 Route Decision

## Purpose

This contract defines the allowed product change for GitHub issue #979: rewrite only the shared Daren QTE route scene `route_decision` / "Развилка в оранжерее" as a substantial Russian dark-fantasy literary page while preserving all gameplay and runtime contracts.

## Source Authority

- Source issue: [#979](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/979)
- Parent umbrella: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Shared route data: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused guards: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`

## In-Scope Surface

- `QteChapter.Narrative` and `DarenShowcaseBeat.PlayerText` for beat id `route_decision`.
- Focused C# test coverage proving the old synopsis fails and the rewritten page preserves route/action mechanics.
- Spec Kit evidence under `specs/979-daren-orangery-route/`.

## Required Player-Facing Properties

The final scene prose must:

1. Be substantial Russian dark-fantasy prose, not a one/two-sentence synopsis.
2. Keep Daren as the active point-of-view protagonist.
3. Include orangery/greenhouse setting details such as wet glass, plants, condensation, moon or red alarm light, and enclosed-house pressure.
4. Carry forward prior alarm-pulse/staff-case/pursuit pressure where relevant.
5. Present three concrete exits or routes and make route choice meaningful.
6. Make trace-washing, pursuit misdirection, noise, light, or evidence stakes visible.
7. Narrow naturally into the existing route-choice action.
8. Avoid default player-facing implementation terminology: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, and `QTE`.

## Preserved Mechanics and Runtime Contracts

The implementation must not change:

- route id `daren_qte_showcase`;
- beat order or beat ids;
- `route_decision` title unless the issue explicitly requires it;
- `route_decision_action` id and label;
- QTE check type (`PrecisionChoice`), primary characteristic, difficulty, config choices, or input semantics;
- routing targets for success/partial/fail;
- score deltas, reward tiers, reward profile persistence, or New Game grants;
- console/browser command, endpoint, DTO, state file, validation, or frontend contracts;
- sibling scenes #980-#983, result/aftermath issues #988-#1014, or parent #955 lifecycle state.

## Verification Contract

Before merge, evidence must show:

- RED focused test failure after adding the #979 guard and before production prose changes.
- GREEN focused `DarenQteShowcaseTests` after the rewrite.
- Affected Daren/QTE/docs/browser slice passes locally.
- Client and test-project builds succeed.
- `git diff --check origin/main...HEAD` is clean.
- Added-line security/static scan has no real findings.
- Independent review approves the literary quality bar and scope boundaries.
