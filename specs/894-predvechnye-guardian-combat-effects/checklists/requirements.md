# Requirements Checklist: Predvechnye Guardian special-art combat niches (#894)

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/894

## Completeness

- [x] Source issue #894 is linked in `spec.md`, `plan.md`, `tasks.md`, and the contract/design map.
- [x] Dependencies #898, #897, and #895 are identified as already closed before implementation.
- [x] Non-closing follow-up #896 is identified and kept out of PR closing keywords.
- [x] All ten target Guardian dossiers are enumerated.
- [x] Acceptance criteria include explicit combat-effect clauses, uniqueness, trigger/target, legal axis/payoff, limit/counterplay, Saref/story preservation, and spoiler safety.
- [x] Verification commands include focused source guard tests, afterlife docs/examples tests, test-project build, diff check, and static scan.

## Ambiguity review

- [x] `Боевой эффект:` is defined as dossier authoring guidance, not a new runtime JSON field.
- [x] Questline docs are consistency-only for this issue; no mandatory rewrite unless dossier text contradicts them.
- [x] Broad worked examples/regression coverage remain #896.
- [x] The design map gives each Guardian a distinct ordinary-combat niche and preferred legal #897/#898 axis.

## Implementation readiness

- [x] Baseline focused test gate was run before implementation: 115 passed, 0 failed, 0 skipped.
- [x] TDD RED step is defined for Codex: add source guard first, run it before dossier edits, then update dossiers.
- [x] Review and merge remain owned by Hermes after Codex completion.
