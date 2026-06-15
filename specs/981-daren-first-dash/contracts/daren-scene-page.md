# Contract: Daren Scene Page — #981 First Dash

## Purpose

This contract defines the allowed product change for GitHub issue #981: rewrite only the shared Daren QTE route scene `pursuit` / "Первый рывок" as a substantial Russian dark-fantasy literary page while preserving all gameplay and runtime contracts.

## Source Authority

- Source issue: [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981)
- Parent umbrella: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Shared route data: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused guards: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`

## In-Scope Surface

- `QteChapter.Narrative` and `DarenShowcaseBeat.PlayerText` for beat id `pursuit`.
- Focused C# test coverage proving the old synopsis fails and the rewritten page preserves route/action mechanics.
- Spec Kit evidence under `specs/981-daren-first-dash/`.

## Required Player-Facing Properties

The final scene prose must:

1. Be substantial Russian dark-fantasy prose, not a one/two-sentence synopsis.
2. Keep Daren as the active point-of-view protagonist.
3. Include first-dash setting details: hall or door behind him, open window or threshold, courtyard/night air, lanterns or guards, and a narrowing escape line.
4. Carry forward staff-theft pressure: stolen staff/futlyar, belt or strap balance, ring/noise/evidence risk, and the waking house or guards.
5. Include pursuit/witness pressure from Captain Orvald Shpil, Lukyan, guards, shouted commands, or visible witness risk where relevant.
6. Narrow naturally into the existing timing/speed action to hit the exact window before pursuit closes.
7. Avoid default player-facing implementation terminology: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, and `QTE`.

## Preserved Mechanics and Runtime Contracts

The implementation must not change:

- route id `daren_qte_showcase`;
- beat order or beat ids;
- `pursuit` title unless the issue explicitly requires it;
- `pursuit_action` id and label;
- QTE check type (`TimingBar`), primary characteristic, difficulty, or input semantics;
- routing targets for success/partial/fail;
- score deltas, reward tiers, reward profile persistence, or New Game grants;
- console/browser command, endpoint, DTO, state file, validation, or frontend contracts;
- sibling scenes #982-#983, result/aftermath issues #988-#1014, or parent #955 lifecycle state.

## Verification Contract

Before merge, evidence must show:

- RED focused test failure after adding the #981 guard and before production prose changes.
- GREEN focused `DarenQteShowcaseTests` after the rewrite.
- Affected Daren/QTE/docs/browser slice passes locally.
- Client and test-project builds succeed.
- `git diff --check origin/main...HEAD` is clean.
- Added-line security/static scan has no real findings.
- Independent review approves the literary quality bar and scope boundaries.
