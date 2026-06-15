# Contract: Daren Scene Page — #982 Courtyard Chain

## Purpose

This contract defines the allowed product change for GitHub issue #982: rewrite only the shared Daren QTE route scene `chase_chain` / "Цепочка дворов" as a substantial Russian dark-fantasy literary page while preserving all gameplay and runtime contracts.

## Source Authority

- Source issue: [#982](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/982)
- Parent umbrella: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Shared route data: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused guards: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`

## In-Scope Surface

- `QteChapter.Narrative` and `DarenShowcaseBeat.PlayerText` for beat id `chase_chain`.
- Focused C# test coverage proving the old synopsis fails and the rewritten page preserves route/action mechanics.
- Spec Kit evidence under `specs/982-daren-chase-chain/`.

## Required Player-Facing Properties

The final scene prose must:

1. Be substantial Russian dark-fantasy prose, not a one/two-sentence synopsis.
2. Keep Daren as the active point-of-view protagonist.
3. Include the courtyard-chain route: rear yard, low wall, cart or wagon, dark alley, wet stone or mud, lanterns/guard lines, and bridgeward direction.
4. Carry forward route-choice and first-dash pressure: orangerie/servant gate/arch context where relevant, pursuit control, stolen staff/futlyar balance, and trace/noise/evidence risk.
5. Include pursuit pressure from Captain Orvald Shpil, guards, voices, lanterns, dogs, or visible tracking pressure where relevant.
6. Narrow naturally into the existing `PromptChain` action: Daren must repeat the exact sequence of jumps, turns, and route memory before pursuit reads his route to the bridge.
7. Avoid default player-facing implementation terminology: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, and `QTE`.

## Preserved Mechanics and Runtime Contracts

The implementation must not change:

- route id `daren_qte_showcase`;
- beat order or beat ids;
- `chase_chain` title unless the issue explicitly requires it;
- `chase_chain_action` id and label;
- QTE check type (`PromptChain`), primary characteristic, difficulty, or input semantics;
- routing targets for success/partial/fail (`hideout_return`);
- score deltas from `DarenScoreDeltas(pursuit: 4, evidence: -2)`, reward tiers, reward profile persistence, or New Game grants;
- console/browser command, endpoint, DTO, state file, validation, or frontend contracts;
- sibling scene #983, result/aftermath issues #988-#1014, or parent #955 lifecycle state.

## Verification Contract

Before merge, evidence must show:

- RED focused test failure after adding the #982 guard and before production prose changes.
- GREEN focused `DarenQteShowcaseTests` after the rewrite.
- Affected Daren/QTE/docs/browser slice passes locally.
- Client and test-project builds succeed.
- `git diff --check origin/main...HEAD` is clean.
- Added-line security/static scan has no real findings.
- Independent review approves the literary quality bar and scope boundaries.
