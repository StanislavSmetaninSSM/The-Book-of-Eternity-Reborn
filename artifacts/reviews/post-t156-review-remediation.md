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

## Sol/max Follow-up Findings

### Partial browser restore deleted cleanup authority and accepted broad roots

Recovery previously continued into dynamic cleanup after an earlier tracked
restore failed, and any descendant of the browser rollback root could be
declared as cleanup authority. Recovery now stops before cleanup whenever any
canonical or typed external restore fails, retains the manifest and remaining
evidence, and accepts only the pending-turn snapshot plus the exact direct-gacha
rollback root.

RED/GREEN regressions:

- `BrowserLocalWriteCoordinatorTests.InterruptedBrowserRestoreFailure_RetainsEvidenceAndRestoresRemainingFiles`
- `BrowserLocalWriteCoordinatorTests.BrowserWriteRollbackCleanup_RejectsUnownedRollbackSubtree`

### Runtime locks and staging trusted lexical `.boe_runtime` paths

The canonical and lifecycle locks plus proposal/save staging now share one
physical runtime authority root. Existing reparse roots, ancestors, and targets
reject; lock acquisition validates before and after opening; staging
publication and cleanup repeat runtime confinement at their mutation boundary.

RED/GREEN regressions:

- `FileSystemManagerTests.AcquireCanonicalWriteLease_RejectsRuntimeRootReparsePoint`
- `FileSystemManagerTests.AcquireCanonicalWriteLease_RejectsRuntimeLockDirectoryReparsePoint`
- existing proposal publication boundary tests remain green.

### The browser Daren reward profile had only in-process rollback

Browser rollback schema 3 carries a closed typed external entry for the Daren
reward profile with exact-byte SHA-256 evidence. A staged profile write restores
after restart and before either ordinary mutation or session replacement; the
completion path proves the durable manifest exists before the profile write can
finish. Committed transactions preserve the accepted profile.

RED/GREEN regressions:

- `BrowserLocalWriteCoordinatorTests.InterruptedStagedBrowserWrite_RestoresExternalDarenRewardProfile`
- `BrowserLocalWriteCoordinatorTests.SessionReplacementLease_RecoversInterruptedExternalProfileBeforeReplacingSession`
- `BrowserQteGenerationFencingTests.DarenCompletion_StagesExternalProfileRollbackBeforeProfileWriteCompletes`
- existing late-failure and concurrent-replacement Daren tests remain green.

### Save publication and autosave deletion retained destination TOCTOU windows

Save ZIP bytes are now assembled under no-follow `.boe_runtime/save-staging`
and moved into the canonical session through one generation-bound mutation
gate. Autosave enumeration produces canonical relative targets; every deletion
re-enters the no-follow canonical mutation boundary after the deterministic
race hook.

RED/GREEN regressions:

- `SaveLoadServiceTests.SaveGameAsync_SaveDirectoryReplacedAtCommitCannotPublishOutsideSession`
- `SaveLoadServiceTests.AutosaveAsync_DirectoryReplacedAfterEnumerationCannotDeleteOutsideFile`

### Save/load transported browser rollback evidence into another generation

Browser rollback roots are now an ephemeral archive prefix. New saves omit
them, while load removes the same prefix from legacy or crafted staged sessions
before replacement.

RED/GREEN regressions:

- `SaveLoadServiceTests.SaveGameAsync_ExcludesBrowserRollbackTransactions`
- `SaveLoadServiceTests.LoadGameAsync_StripsBrowserRollbackTransactionsFromLegacyArchive`

## Phase 31 Focused Verification

- Filesystem/browser/QTE/save/proposal focused classes: `118/118` passed.
- Mandatory afterlife documentation tests: `118/118` passed.
- Whole-source guard tests: `229/229` passed.
- `git diff --check`: passed; only configured LF-to-CRLF notices were emitted.

## Phase 31 Full Verification Before Re-review

- Complete test project: `6590/6590` passed.
- Release solution build: `0` warnings and `0` errors.
- PowerShell parsing: `4/4` changed scripts passed.
- JSON parsing: `4/4` changed files passed.
- No untracked files remain in the worktree.
- `git diff --check`: passed.
- Spec Kit consistency: `72` explicitly identified functional requirements,
  `23` success criteria, and a continuous `T001` through `T164` task sequence
  with no duplicate IDs, missing task numbers, unresolved placeholders, or
  missing #1500 traceability.

## Pre-Follow-up Verification

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

The Sol/max follow-up remains within the same client-owned boundary. Runtime
locks/staging, browser rollback evidence, the persistent Daren client profile,
and save archive hygiene are not GM-authored output contracts. No Mortal or
afterlife prompt/example prose change is required; Spec Kit and this harness
evidence record the behavior, and the mandatory documentation suite remains
green.

## Remaining Gate

Run a fresh independent `gpt-5.6-sol` / max exact-diff review against the base
above and resolve every Critical or Important finding before integration.
