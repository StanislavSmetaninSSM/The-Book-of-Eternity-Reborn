# Retained spiritual source continuation implementation

> Required execution skill: superpowers:subagent-driven-development. Use the existing isolated 1536 worktree, TDD and independent review.

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536.
Spec: specs/1536-complete-wound-materialization/spec.md and plan.md.
Tracked work: T081-B2C-J2-C2 in tasks.md. C2 remains open after this bounded unit.

Implement the actual signed source owner continuation, explicit missing-audit live
mode, and discardable lease-bound preparation. A failed or discarded candidate
must preserve accepted source references, checked exchanges and dice claims.
Changing the signed original or acquiring a replacement owner permanently revokes
the previous owner. Ordinary acquisition remains strict about incomplete audits.

The complete proposed code and tests are in the adjacent patch. Its eleven final
postimages combine source-transaction-proposed under the C2 planning workspace
with the phase 1 ResourceMaterialization.cs source-owner revocation reset delta.
The three original phase patches in that workspace define the intermediate TDD
states: source-continuation-proposal, source-missing-audit-proposal and
source-transaction-proposal. Do not apply the combined patch before OLD controls.

## Constraints and interfaces

- Keep existing C1 and applied retained resource continuation changes intact.
- Signed PendingTurnSnapshotReader acquisition and shared conflict validation
  remain the evidence producers. Detached JSON and resource batches are not
  source admission or current-generation authority.
- ContinueAsync keeps exact owner identity and known checked source references.
- BeginLiveSpiritualWoundSourceSessionAsync is explicit; default Begin/Continue
  cannot admit a partial action-cost audit.
- PrepareContinuationAsync returns an opaque PreparedContinuation ticket with
  private constructor, exact owner, revision, retained prefix and active lease.
- BuildPreparedResourceBatches reads that ticket's actual prospective capture.
- CommitPreparedContinuation commits once. Foreign, stale, used or wrong-lease
  tickets fail. Discard does not mutate the original source owner.
- No source seal, generation receipt, common publication or ordinary runtime
  cutover is added. D/E, T083 and terminal producers stay explicit integration work.
- No index changes, commits, pushes or issue closure in this execution unit.

## Task 1: Signed retained continuation

- [x] Pin byte-exact preimages and missing files before edits.
- [x] Apply only SpiritualSourceContinuation.Old.cs; run its OLD-compatible
  signed owner replacement control using Focused Integration.
- [x] Apply source-continuation-proposal production and remaining test postimages.
- [x] Run SourceContinuation_ and SourceOwner_ in separate bounded focused families; fix actual defects
  without weakening ownership and retained-prefix assertions.

## Task 2: Explicit pending action-cost evidence

- [x] Apply SpiritualSourceMissingAudit.Old.cs alone and run its semantic OLD
  control: the existing strict path cannot retain an incomplete source owner.
- [x] Apply source-missing-audit-proposal production and remaining tests.
- [x] Run SourceMissingAudit_ plus SourceContinuation_ in separate bounded families. Retain the passed phase1 SourceOwner_ baseline without a duplicate run. Missing is absent expected
  evidence; malformed values and invented zero remain errors. Known sides stay
  immutable; partial validation claims no source or dice.

## Task 3: Source prepare/commit transaction

- [x] Apply SpiritualSourceTransaction.Old.cs alone and run semantic OLD control:
  immediate continuation must fail the assertion that preparation is discardable.
- [x] Apply source-transaction-proposal production and remaining tests.
- [x] Run SourceTransaction_, SourceMissingAudit_ and SourceContinuation_ as
  separate bounded Integration selections. SourceOwner_59 is the once-only phase1
  baseline; rerun it only if a new failure or relevant change requires it.
- [x] Refresh the adjacent patch to the actual final eleven-file unit.
- [x] Independent review must inspect the unit diff and actual RED/GREEN evidence.
- [ ] Parent selects one Fast checkpoint after integration changes; do not run
  duplicate Fast or PreMerge controls within individual phase tasks.

Use PowerShell 7, the retained enter-net8.ps1 helper and scripts/test-csharp.ps1
with -Lane Focused -FocusedProject Integration -Filter. Never run concurrent builds
against this checkout. A compile failure is not semantic RED evidence.

This unit adds internal source preparation only and leaves ordinary acquisition
strict, with no active GM authoring or publication contract change. Thus no GM
example update is needed for this isolated unit. Integrated C2 still requires its
prompts, matrix, worked examples, manifest and source guards before completion.

Execution ruling: the initial combined SourceContinuation_/SourceOwner_ selection
discovered 75 filesystem-backed rows and hit the five-minute lane deadline after
a successful 73-second build; it produced no completed TRX. An isolated unchanged
continuation then passed (one four-second test, 25-second lane). Keep the normal
five-minute bound and run each family separately; preserve the timeout as failed
lane evidence, not a test-failure or acceptance claim.
