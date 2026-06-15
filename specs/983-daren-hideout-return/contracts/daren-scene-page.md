# Contract: Daren Scene Page — #983 Hideout Return

## Purpose

This contract defines the allowed product change for GitHub issue #983: rewrite only the shared Daren QTE route scene `hideout_return` / "Убежище под мостом" as a substantial Russian dark-fantasy literary page while preserving all gameplay and runtime contracts.

## Source Authority

- Source issue: [#983](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/983)
- Parent umbrella: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
- Shared route data: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Focused guards: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`

## In-Scope Surface

- `QteChapter.Narrative` and `DarenShowcaseBeat.PlayerText` for beat id `hideout_return`.
- Focused C# test coverage proving the old synopsis fails and the rewritten page preserves route/action mechanics.
- Spec Kit evidence under `specs/983-daren-hideout-return/`.

## Required Player-Facing Properties

The final scene prose must:

1. Be substantial Russian dark-fantasy prose, not a one/two-sentence synopsis.
2. Keep Daren as the active point-of-view protagonist.
3. Include the under-bridge hideout: bridge or arch, water, wet stone, hidden cache/taynik/stone, and the low shelter where sound is masked or reflected.
4. Carry forward chase pressure: the courtyard-chain route, Orvald/guards/dogs/lanterns/voices, stolen staff/futlyar weight, and traces that can lead to the hideout.
5. Show Daren hiding/sealing the staff and erasing or misdirecting final traces before pursuit reaches the bridge.
6. Narrow naturally into the existing `BranchChoice` action: `Спрятать посох и зачистить след`.
7. Avoid default player-facing implementation terminology: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, and `QTE`.

## Preserved Mechanics and Runtime Contracts

The implementation must not change:

- route id `daren_qte_showcase`;
- beat order or beat ids;
- `hideout_return` title unless the issue explicitly requires it;
- `hideout_return_action` id and label;
- QTE check type (`BranchChoice`), primary characteristic (`Characteristics.Wisdom`), difficulty `3`, or `DarenBranchChoiceConfig("success")`;
- routing targets for success/partial/fail (`TerminalOutcomeId = "daren_hideout_return"`);
- score deltas from `DarenScoreDeltas(hideout: 6, evidence: -3)`, reward tiers, reward profile persistence, or New Game grants;
- terminal outcome id/title/final narrative unless tests reveal a necessary prose-consistency issue tied to #983;
- console/browser command, endpoint, DTO, state file, validation, or frontend contracts;
- sibling/result issues #988-#1014 or parent #955 lifecycle state.

## Verification Contract

Before merge, evidence must show:

- RED focused test failure after adding the #983 guard and before production prose changes.
- GREEN focused `DarenQteShowcaseTests` after the rewrite.
- Affected Daren/QTE/docs/browser slice passes locally.
- Client and test-project builds succeed.
- `git diff --check origin/main...HEAD` is clean.
- Added-line security/static scan has no real findings.
- Independent review approves the literary quality bar and scope boundaries.
