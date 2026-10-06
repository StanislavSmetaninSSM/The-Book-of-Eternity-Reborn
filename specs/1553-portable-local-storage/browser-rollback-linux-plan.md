# T031 browser rollback Linux implementation plan

> Execute inline with Superpowers TDD/debugging; root is sole writer. Independent
> actual Sol6.1/xhigh plan review precedes implementation, then source/evidence review.

Goal: the real BrowserLocalWriteCoordinator stages, records, commits, rolls back and
cleans its existing transaction on Linux using the accepted trusted-local-player
contract. Base `4d456d5d5e12289d6fc97e5b2e7a0c523207bc08`, branch
`codex/1553-load-filesystem`, [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553),
US2/US3/FR-001–008/011/015. [Spec](spec.md), constitution and existing F2/F3/M1 evidence.
No gameplay/GM-authored field or rule changes; no prompt/example migration required.

## Source diagnosis and selected approach

`BrowserLocalWriteCoordinator.ExecuteAtomicCoreAsync` acquires the real local UI token
under RunBoundTransactionAsync, then calls ExplorerLocalTurnRollbackArtifacts staging.
FileSystemManager.TrustedLocalStorage.UsesTrustedLocalWriter excludes both its rollback
root and any MutationIntentRecorder. Backup/manifest writes and recorded writes
therefore reach Windows descriptor publication. The recorder and restore additionally
require physical FileIdentity; Linux pre-recovery rejects this root as legacy before
the original handler. Marker/cleanup writes still use physical helpers after recorder
removal. This is the actual remaining consumer, not a fixture-only availability test.

Reuse the same browser transaction root, manifest name, markers, backup membership,
original lease/guard and existing TrustedLocalFilePublication journal. Add no second
browser coordinator, delivery/recovery journal, protective service or anti-player model.
Introduce schema7 for the Linux trusted-local browser route, explicitly distinguish it
from schema6 physical receipts. Preserve the Windows schema6 handler/source route and
old journal semantics; Linux must never reinterpret schema6/older retained evidence.
Blindly allowing the existing recorder or synthesizing physical identities is rejected.
A whole new prepared browser API would miss existing dynamic callbacks and is rejected.

Schema7 binds existing generation, declared members, exact before bytes/absence and
hashes, cumulative durable authorized post-image hashes/deletion intent. Before each
member mutation, the original recorder publishes its intent; known before or declared
post bytes can restore baseline, unknown bytes preserve evidence and block continuation.
Repeated writes and a failed later write cannot discard earlier mutation authority.
Ordinary hardlink aliases retain their bytes; links/types/out-of-root paths still refuse.
Schema7 cleanup intent uses the existing cleanup filename with a typed payload carrying
schema, original generation, transaction root/scope/creation binding and outcome. It
survives manifest removal and permits only the remaining declared cleanup. Windows
keeps its empty original intent; Linux rejects and retains that old empty orphan. This
distinction is required for fresh-manager cleanup without reinterpreting old evidence.

## Files and interfaces

- `Services/ExplorerLocalTurnRollbackArtifacts.cs` becomes partial with only narrow
  Linux dispatch; focused `ExplorerLocalTurnRollbackArtifacts.TrustedLocalBrowser.cs`
  owns schema7 staging/recorder/validation/rollback/commit/cleanup. Existing Windows
  original handler is preserved. Current browser root is not a general write permit.
- `Core/FileSystemManager.TrustedLocalStorage.cs`, focused browser storage partial and
  `FileSystemManager.cs`: an internal original-active-lease browser scope enables its
  artifact writes and typed trusted-local recorder. Ordinary writes call recorder
  intent at the shared publication boundary for Write/Append/CAS/Delete, before the
  existing publisher. Owned artifacts bypass recording to avoid recursive manifest
  writes. Recorder-free cleanup remains in this scope.
  Reads/writes/recovery validate main + worker + generation; never mint from JSON/PID.
- Recovery below the already-acquired original main/worker/canonical lease preflights
  supported browser evidence before mutation. Unsupported old/unknown evidence still
  refuses. Resolve any existing trusted-local publication first, then the supported
  browser manifest; otherwise a pending member publication could undo browser rollback.
  Closing SessionFinalization remains no-recovery and cannot bypass this restriction.
- `WebUi/BrowserLocalWriteCoordinator.cs`: preserve established typed outcome through
  UI release and SessionOperationContext finalization. Known rollback is RolledBack;
  incomplete rollback is Uncertain; durable commit plus cleanup/close debt remains
  Committed with follow-up. Preserve Success/IsBlocked/message consumer compatibility.
  Carry the exact result through both ExecuteAtomicAsync and Within under enclosing
  RunBoundTransactionAsync; supply captured MainOperationOutcome instead of default
  Completed. Closing failure must retain established Committed/RolledBack/Uncertain
  and attach debt; it must not replace a settled decision or report Uncertain as Completed.
- The actual `QteWebInteractionService.ResolveDarenShowcaseActionBoundAsync` declares
  the single Daren profile outside game_session, still within BasePath. Include this
  original declared member using its exact registered path and schema7 external intent,
  through `DarenRewardProfileFileStore`'s same original browser scope. Do not silently
  discard this participant or migrate unrelated standalone profile writes. Windows
  Daren deferred physical route stays unchanged; no arbitrary external path capability.
  Use a narrow exact registered-path adapter (ordinary canonical APIs resolve within
  game_session), and qualify the actual QteWebInteractionService reward-producing
  Resolve action as well as direct declared profile publication/rollback.
- Both browser Load entries, original quiescent guard, UI acquisition/release/refresh,
  exact Committed/RolledBack/Uncertain and F2 full same-response refresh bundle remain.
  Select actual backend guard/refresh regression; frontend code is unchanged unless a
  causal consumer failure requires correction, then select only its owning category.

## Execution and verification

- [ ] Plan source/delta/selection independent Sol review and checkpoint push/readback.
- [ ] Add `BrowserRollbackLinuxTests` (isolated bootstrapped roots, actual coordinator)
  and narrow `browser-rollback-linux` category. First three positive-oracle RED cases:
  real existing+absent member commit, exact rollback after callback error, explicit
  held-lease write. Reach actual staging/recorder; assert callback/bytes/outcome, never
  accept unavailable backend as success. Preserve preparation failures separately.
- [ ] Fresh category RED via scripts/test-csharp.ps1, exact SHA/source/command/TRX and
  cleanup. Implement minimal connected scope/schema7/recorder/recovery/cleanup change;
  fresh narrow GREEN and ordinary checkpoint/readback before expansion.
- [ ] Add small qualification block/category `browser-rollback-linux-boundaries` for
  repeated writes/deletes, wrong bytes and retained rollback, committed cleanup debt
  then fresh-manager cleanup, interrupted staged recovery and recovery ordering,
  missing baseline, malformed/stale generation/old-schema retained refusal,
  cleanup cut after manifest removal (typed schema7 orphan clears, old empty orphan refuses),
  directory cleanup/type/link constraints, actual declared Daren file-store commit/
  rollback, local UI other-owner and refreshed original token, held transaction versus
  replacement, main nonterminal/Stopping and worker nonquiescence refusal before effects.
  Explicitly cover Write/Append/CAS/Delete with recording, A→B success followed by
  failed B→C then callback failure restoring A, and finalization faults for all three
  established decisions through both atomic entry routes.
  Faults use controlled filesystem hooks; no providers/process owner probes needed.
- [ ] Select only actually affected old ordinary publication/no-legacy-admission and
  actual browser Load guard/full-refresh methods, inspecting source and catalog before
  recording exact IDs. Do not rerun F1–F3/M1 or broad browser-api-host cohort.
- [ ] Fresh selected GREEN, catalog and exact PlanOnly discovery (zero execution),
  source/manifest/hash/TRX/cleanup map, independent Sol source/evidence/selection review.
- [ ] Reconcile Spec Kit tasks/status and handoff, ordinary push/remote byte readback,
  fresh GitHub-only restore. Stop before another stage; no issue closure or merge.

New categories use protective 3-minute budgets, unmeasured expectedSeconds=null,
integration ownership for the actual coordinator/consumer tests, no frontendBuild unless
frontend source is changed. Runner commands: `pwsh -NoProfile -File scripts/test-csharp.ps1
-Category browser-rollback-linux -Parallelism 1`, then the exact selected second block.
No full suite/Fast/PreMerge/old cohort union. Each mutable root is independent; cleanup
removes only fixture resources after assertions and retains unknown evidence in captured
artifacts. No live browser; automated backend/consumer tests are the accepted boundary.

## Historical four IDs, not passed

These remain the original F2 Linux failures, with zero executions in this new stage;
new Linux cases get distinct IDs and explicit current-source proof:

- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAsync_SessionReplacementWaitsForWholeLegacyTransaction`
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ConcurrentReplacementWaitsForCompleteTransaction`
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_LockReleaseFailureDoesNotRollbackCommittedMutation`
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ExplicitLeaseWritesWithoutAmbientAuthority`

First two originally timed out before callback; last two refused descriptor-bound
create-only publication. They are not native Windows passes. [Original F3 handoff](main-run-fence-f3-handoff.md#four-unavailable-windows-ids-and-real-linux-debt).

No new product decision is identified: this ports existing cooperating transaction
behavior. Load UX is pending, so keep low-level refusal. No live provider/saves/Q1/Q2,
TERM acceptance, settings/auth/network/service changes, native Windows/systemd claim,
cold command exactly-once, reboot salvage or protection from the player.
