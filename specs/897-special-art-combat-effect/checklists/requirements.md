# Requirements Checklist: Structured special-art combatEffect (#897)

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/897

## Scope and Traceability

- [x] Source GitHub issue #897 is linked in spec, plan, tasks, and contract.
- [x] Related issues #895/#894/#896 are identified without making them closing targets for this PR.
- [x] Spec Kit is justified by afterlife contract/validation/docs/examples impact.
- [x] Out-of-scope boundaries exclude full Predvechnye Guardian dossier rewrite (#894) and final broad coverage (#896).

## Contract Requirements

- [x] `effectSummary` and `combatEffect` responsibilities are separate.
- [x] Required `combatEffect` semantics are listed: summary, trigger, mechanical axis, allowed payoff, limit, audit requirement.
- [x] Legal payoff axes are constrained to existing afterlife combat surfaces and #898 vocabulary.
- [x] Backward compatibility for legacy profiles is required.
- [x] Player-facing spoiler/raw-output boundaries are explicit.
- [x] GM prompt/docs/example synchronization is mandatory.

## Verification Requirements

- [x] Baseline command was run and had non-zero test counts: 485 passed, 0 failed, 0 skipped.
- [x] RED/GREEN validation tasks are present.
- [x] RED/GREEN player-facing output tasks are present.
- [x] Documentation/example coverage tasks are present.
- [x] Final verification commands are listed.

## Open Clarifications for Implementation

- [ ] Codex must confirm the exact existing test class names and rendering paths before editing.
- [ ] Codex may refine field names only by updating spec/plan/tasks/contracts/docs/examples/tests consistently.
