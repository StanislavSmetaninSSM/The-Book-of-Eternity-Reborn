# Contract: Daren Scene Page — #980 Staff Theft

## Purpose

This contract defines the allowed product change for GitHub issue #980: rewrite only the shared Daren QTE route scene `staff_theft` / "Кража посоха" as a substantial Russian dark-fantasy literary page while preserving all gameplay and runtime contracts.

## Source Authority

- Source issue: [#980](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/980)
- Parent umbrella: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Shared route data: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused guards: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`

## In-Scope Surface

- `QteChapter.Narrative` and `DarenShowcaseBeat.PlayerText` for beat id `staff_theft`.
- Focused C# test coverage proving the old synopsis fails and the rewritten page preserves route/action mechanics.
- Spec Kit evidence under `specs/980-daren-staff-theft/`.

## Required Player-Facing Properties

The final scene prose must:

1. Be substantial Russian dark-fantasy prose, not a one/two-sentence synopsis.
2. Keep Daren as the active point-of-view protagonist.
3. Include relic-theft details: staff or staff-case, velvet holders/supports, thin rings/suspension hardware, weight/balance, belt/strap/futlyar securing.
4. Carry forward prior old-lock/scratch, route-choice, alarm/listening-house, or pursuit pressure where relevant.
5. Make noise, trace, evidence, ringing, scrape, scratch, dust, guards, or pursuit stakes visible.
6. Narrow naturally into the existing balance-control action.
7. Avoid default player-facing implementation terminology: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, and `QTE`.

## Preserved Mechanics and Runtime Contracts

The implementation must not change:

- route id `daren_qte_showcase`;
- beat order or beat ids;
- `staff_theft` title unless the issue explicitly requires it;
- `staff_theft_action` id and label;
- QTE check type (`BalanceMeter`), primary characteristic, difficulty, or input semantics;
- routing targets for success/partial/fail;
- score deltas, reward tiers, reward profile persistence, or New Game grants;
- console/browser command, endpoint, DTO, state file, validation, or frontend contracts;
- sibling scenes #981-#983, result/aftermath issues #988-#1014, or parent #955 lifecycle state.

## Verification Contract

Before merge, evidence must show:

- RED focused test failure after adding the #980 guard and before production prose changes.
- GREEN focused `DarenQteShowcaseTests` after the rewrite.
- Affected Daren/QTE/docs/browser slice passes locally.
- Client and test-project builds succeed.
- `git diff --check origin/main...HEAD` is clean.
- Added-line security/static scan has no real findings.
- Independent review approves the literary quality bar and scope boundaries.
