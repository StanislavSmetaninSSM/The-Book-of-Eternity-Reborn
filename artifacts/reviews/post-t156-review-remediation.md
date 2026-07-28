# Post-T156 Review Remediation

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500

Exact comparison base: `9a1490146b7cecad6101af1f166bde614050a6a3`

The post-T156 independent review used `gpt-5.6-sol` with high reasoning under
the user constraint active at that time. The final acceptance review must use
`gpt-5.6-sol` with max reasoning.

## Confirmed Findings

### Worker proposal publication could cross a reparse swap

The proposal store previously validated its staging path before publication,
then called `Directory.Move` directly. A concurrent replacement of a staging
ancestor could redirect the move after validation.

The filesystem harness now exposes one purpose-bound canonical operation for
moving a proposal bundle from `.boe_runtime/proposal-staging` into the session.
It revalidates source confinement, source reparse state, destination
confinement, and session generation at the mutation boundary.

RED/GREEN regression:

- `GmWorkerProposalStoreTests.PublishBundleAsync_ParentReplacedAtMoveBoundaryRejectsWithoutEscape`

### Publication cancellation was rechecked after the durable transition

The first move-boundary remediation passed the caller cancellation token into
the final move API. Cancellation at the mutation-boundary hook could therefore
revoke publication after `TryBeginPublication` had already selected the durable
bundle as the authoritative result.

The final move is intentionally non-cancelable after that transition. Staging,
lease acquisition, and every pre-transition path remain cancelable.

RED/GREEN regression:

- `GmWorkerProposalStoreTests.PublishBundleAsync_CancellationAtMoveBoundaryPreservesPublishedOutcome`

### Interrupted direct browser gacha could retain dynamic artifacts

The durable browser rollback manifest covered known canonical files but did not
declare dynamic pending-snapshot and rollback roots created by direct gacha.
Process interruption could therefore leave old-session artifacts behind.

Rollback manifest schema version 2 carries narrowly allowlisted cleanup
directories. Direct gacha declares only its pending-turn snapshot and its own
rollback root. Recovery and in-process rollback restore before-images and
remove those declared roots. Version 1 manifests remain readable.

RED/GREEN regressions:

- `BrowserLocalWriteCoordinatorTests.InterruptedStagedBrowserWrite_RemovesDeclaredRollbackCleanupDirectories`
- `BrowserLocalWriteCoordinatorTests.BrowserWriteRollbackCleanup_RejectsBroadCanonicalDirectory`
- `BrowserAfterlifeWriteServiceTests.TryApplyAsync_GachaDirectPull_DeclaresDynamicRollbackCleanupBeforeMutation`

### Missing rollback before-image was reported as restored

Rollback previously returned success when a required before-image file was
missing. It now fails closed with `FileNotFoundException`, preserving evidence
for recovery instead of claiming a successful restore.

RED/GREEN regression:

- `FileSystemManagerTests.RestoreBackup_MissingBeforeImageFailsClosed`

### Save snapshot reads had a verify/read race

Save enumeration and ZIP publication previously used path-based
`CreateEntryFromFile` after canonical validation. A parent directory could be
replaced between validation and file opening.

Save capture now reads every canonical source through the generation-bound
no-follow filesystem API and writes verified bytes into the archive. Missing,
reparsed, or replacement-session content fails the save before publication.

RED/GREEN regression:

- `SaveLoadServiceTests.SaveGameAsync_ParentReplacedByJunctionBeforeReadFailsClosed`

## Fresh Verification

- Focused review regressions: `7/7` passed.
- Complete proposal-publication store suite: `9/9` passed.
- Adjacent filesystem, worker, browser, afterlife, and save suites: `129/129`
  passed.
- Complete test project: `6580/6580` passed.
- Mandatory afterlife documentation tests: `118/118` passed.
- Release build: `0` warnings and `0` errors.
- PowerShell parsing: `4/4` changed scripts passed.
- JSON parsing: `4/4` changed files passed.
- `git diff --check`: passed.
- Spec Kit analysis: 60 FRs, 22 SCs, and 157 unique tasks with no blocking
  inconsistency, placeholder, traceability gap, or constitution conflict.

## GM Contract Synchronization

These fixes are client-owned harness internals: canonical filesystem movement,
rollback recovery, verified save reads, and fail-closed before-image handling.
They do not change a GM-authored state field, command, pending action type,
response, receipt, report, normalizer side effect, or gameplay schema.
Therefore Mortal World and afterlife prompts, prose documentation, and worked
examples require no content update. The worker source guard was updated because
it directly verifies the changed harness boundary, and the mandatory
documentation suite remains green.

## Remaining Gate

Run a fresh independent `gpt-5.6-sol` / max exact-diff review against the base
above and resolve every Critical or Important finding before integration.
