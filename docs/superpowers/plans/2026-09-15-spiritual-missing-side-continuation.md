# Retained missing action-cost continuation

> Required execution method: superpowers:subagent-driven-development, TDD and independent review.

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536.
Spec/plan/tasks: specs/1536-complete-wound-materialization/, T081-B2C-J2-C2.

Continue the same live resource iterator when an actual producer supplies an
absent action-cost side. Keep known-side transitions, IDs, source fingerprints,
history, accepted activations and use budgets unchanged. Explicit zero closes
the side without inventing a mutation. Missing evidence remains pending.

The adjacent complete patch contains six final postimages, generated from
missing-side-producer.proposed.cs, missing-side-owner.proposed.cs and
missing-side-tests.proposed.cs in the C2 planning workspace. Regenerate after
other receipt edits: the generator rejects stale shared-file postimages.

## Corrected design

- Only real TryCreate outputs carry raw producer provenance. Share a single
  inaccessible frozen context per composition, with a per-batch index.
- Remapped/merged execution batches carry no raw producer provenance. The
  session separately retains the active raw batch and active execution batch.
- Exact missing-slot completion keeps all already observed evidence immutable.
  A newly supplied nonzero side receives a deterministic alias derived from its
  actual completed export; earlier source fingerprints are not overwritten.
- PrepareMissingAuditSide produces an exact session-retained preparation with
  current wait, raw/execution batches, checked completion, alias version and
  checked source-to-alias relation. Construction alone is not authority.
- CommitMissingAuditSide consumes only that exact current preparation, once.
  Invalid or stale input leaves the wait reusable and allocates no resource IDs.
- The common signed owner must retain this actual preparation under its source
  ticket. This lower-resource API alone does not admit a wound source/generation.
- After graph advancement fails, publication must be prevented by the common
  owner. E reconstruction must replay staged completion images and aliases.

## Execution

- [x] Inspect the corrected complete proposal and preserve the original rejected
  review as history. Pin six-file preimages; do not touch index or HEAD.
- [x] Apply only the OLD-compatible missing-side test file.
- [x] Run MissingSideContinuation_RetainsKnownMutationAndRemapsNextDependency
  in Focused Integration. Existing real pending assertions precede missing API RED.
- [x] Apply the five production files, preserving C1 and receipt/source work.
- [x] Run MissingSideContinuation_ plus relevant SpiritualExchangeInterval_
  controls in bounded Focused Integration; split families if required by limits.
- [x] Verify forged dependency maps and merged provenance laundering reject,
  repeated nonzero fills and later remapped pending batches continue, preparation
  foreign/stale/reuse fails, and contexts are shared rather than copied per batch.
- [x] Independently review actual diff and RED/GREEN evidence; refresh companion.

Use PowerShell 7 and scripts/test-csharp.ps1 with -Lane Focused
-FocusedProject Integration. Serialize checkout builds. Parent owns meaningful
Fast and later integrated verification; no per-unit duplicate Fast or PreMerge.
No stage, commit, push, C2 closure or ordinary runtime cutover from this unit.

No GM-authoring entry point changes in this isolated resource unit, so no new GM
example is required here. The shared zero receipt correction and integrated C2
separately require their matrix, worked examples, manifests and source guards.
