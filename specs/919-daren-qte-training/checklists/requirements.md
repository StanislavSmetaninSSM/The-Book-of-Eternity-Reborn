# Requirements Checklist: Daren QTE Training Showcase

Source issue: [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Spec Completeness

- [x] Source GitHub issue is linked in `spec.md`, `plan.md`, `tasks.md`, and the contract file.
- [x] Spec Kit use is justified by player-facing QTE route, profile reward, New Game grant, validation, browser/console parity, docs, examples, and multi-session scope.
- [x] User scenarios are independently testable and ordered by priority.
- [x] Ending names, thresholds, and Ink Feather reward amounts are exact.
- [x] Permanent profile ownership and default storage boundary are defined.
- [x] New Game exactly-once grant behavior and forbidden grant surfaces are defined.
- [x] Console/browser parity and React-vs-C# authority boundaries are defined.
- [x] GM-facing docs/examples/source guard requirements are defined.
- [x] Out-of-scope boundaries are explicit.

## Acceptance Mapping

- [x] Training mode is discoverable without entering/damaging a normal campaign.
- [x] Scenario is presented as QTE showcase/training mini-game.
- [x] Daren, gadgets, manor, and staff are original project content.
- [x] Scenario covers all existing v1 QTE types and all landed v2 QTE types.
- [x] Scenario has multiple endings from bad to best.
- [x] Completion writes a persistent best-tier achievement/unlock.
- [x] New Game grants best-tier Ink Feather bonus once per new session.
- [x] Same/worse replays do not duplicate, stack, or downgrade.
- [x] New Game reward copy names the Daren tier source.
- [x] Reincarnation and in-session lifecycle flows do not grant the bonus.
- [x] Validation/normalizer/profile checks protect against corruption, duplication, and downgrade bugs.
- [x] GM-facing docs/examples explain Daren vs normal GM-authored QTE offers.
- [x] E2E/integration-style deterministic tests are required for completion paths, ending tiers, and New Game rewards.

## Pre-Implementation Gate

- [x] Current baseline before spec artifact edits: focused C# QTE/browser/docs tests passed 268/268.
- [x] Current baseline before spec artifact edits: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed; Vitest player-facing slice passed 72/72 and Vite build succeeded.
- [ ] Spec Kit prerequisite helper resolves `specs/919-daren-qte-training` after tasks are committed.
- [ ] RED tests/source guards have been written and observed failing before production implementation.

## Completion Gate

- [ ] Focused C# Daren/QTE/New Game/docs/browser tests pass.
- [ ] Frontend verification passes.
- [ ] Client build passes.
- [ ] Spec Kit prerequisites resolve the active feature directory.
- [ ] Diff hygiene and added-line static scan pass.
- [ ] Independent review approves or all critical/important findings are fixed and re-reviewed.
- [ ] PR is squash-merged into `main` and #919 is closed with evidence.
