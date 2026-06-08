# Requirements Checklist: Special-art combat-effect examples and regression coverage (#896)

**Feature**: `specs/896-special-art-effect-coverage`
**Source issue**: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/896
**Created**: 2026-06-08

## Completeness

- [x] Source GitHub issue #896 is linked in spec, plan, and tasks.
- [x] Dependencies #898, #897, #895, and #894 are treated as already-closed authority on `main`.
- [x] Scope is bounded to examples, GM-facing docs, and regression/source-guard coverage.
- [x] Runtime contract reshaping is explicitly out of scope.
- [x] Required worked examples are stated: one player-owned learned art and one non-player Guardian/opposition art.
- [x] Required coverage includes at least two #894 Guardian arts.
- [x] Verification commands are listed with non-zero baseline counts.

## Clarity

- [x] Acceptance criteria distinguish base operation behavior from unique `combatEffect` payoff.
- [x] `specialArtAudit.effectNote` expectations are explicit.
- [x] Player-safe/no-spoiler constraints are explicit.
- [x] Legal #897/#898 axes/payoffs are listed.
- [x] Out-of-scope related issues are not described with risky GitHub closing phrases.

## Testability

- [x] RED/GREEN source-guard workflow is required.
- [x] Focused docs/examples tests are named.
- [x] Test-project build, diff hygiene, and static added-line scan are required.
- [x] Independent review and Hermes-owned PR/merge/closure are required.
