# Bounded receipt zero consistency

> Required execution method: superpowers:subagent-driven-development, TDD, systematic debugging and independent review.

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536.
Spec: specs/1536-complete-wound-materialization/spec.md; tracked parent
T081-B2C-J2-C2, explicit subtask T081-B2C-J2-C2-Z0 in the companion tasks delta.

The pending producer advertises an inclusive minimum of zero, but a valid zero
receipt currently becomes a forbidden zero-request ordinary mutation. Preserve
the accepted zero receipt and consumed use, while emitting no resource mutation
or event. Component-dependent work must not run without its required applied
component. Existing positive requests clamped to zero keep their ordinary
transition and primary event behavior.

The adjacent complete patch contains fifteen production/test/doc postimages.
Its source is the retained-resource SDD directory's
2026-09-15-zero-delta-consistency.patch and zero-delta-proposal.md.
Preserve the parent's C2 status paragraph in tasks.md when applying its new row.

## Execution and evidence

- [ ] Pin actual preimages for this correction, distinct from the original six
  receipt preimages. Apply its explicit tracked task record first.
- [ ] Existing live semantic RED is already recorded at focused163259: narrated
  passes and valid delta0 fails resource_mutation_amount_invalid. Preserve it.
- [ ] Apply new unit and integration tests only. Verify new pending-resolver and
  real fixed validator/normalizer zero controls fail semantically before changing
  production. Include the retained afterComponent zero control where useful.
- [ ] Apply the two shared production changes and synchronized Mortal/afterlife
  documentation, worked examples, manifest and guards.
- [ ] Run the focused pending-resolver tests and relevant documentation guards
  in the Fast project, keeping Integration filters separate.
- [ ] Run fixed bounded zero, live continuation/afterComponent and positive-
  request/clamped-zero controls in Focused Integration. Preserve exact terminal
  receipt, accepted request IDs, spent uses, unchanged state and absent phantom
  transitions/reactions; verify dependency completion rather than hiding pending.
- [ ] Run Focused AfterlifeDocumentationCoverageTests and conditional
  FullValidation because the afterlife documentation/examples boundary changed.
- [ ] Independently review the actual correction diff and observed RED/GREEN
  evidence. Refresh the adjacent patch from pinned preimages.
- [ ] Parent runs one Fast checkpoint after the relevant C2 changes settle; no
  duplicate per-worker Fast and no PreMerge outside actual integration/merge.

Use PowerShell 7, the existing enter-net8.ps1 helper and scripts/test-csharp.ps1.
Do not overlap builds or edit shared code while another worker compiles it.
Do not widen the resource reducer's positive-request contract. Malformed/out-of-
bound receipts remain errors; a zero receipt is not missing evidence.
No index changes, commits, pushes, publication or C2/issue closure in this unit.

GM synchronization is required because this is shared active receipt behavior.
The patch updates both Mortal and afterlife worked examples, the afterlife matrix,
the shared design, manifests and source guards. Verify launcher/GM prompt entry
points still direct agents to these existing guides; record any no-update rationale.
