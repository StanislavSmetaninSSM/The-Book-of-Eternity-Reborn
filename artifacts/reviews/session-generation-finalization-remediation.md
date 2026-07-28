# Architectural review: session-generation finalization remediation

Issue: `#1500`
Review mode: research only; no production or test changes
Snapshot: `79b236e75ee0e643a2e98f09680094ac27d31834`, dirty worktree, 2026-07-26 10:53 +10:00

## Verdict

The finding is valid and remains a blocker.

`FileSystemManager.CanonicalWriteLease` linearizes one canonical operation, but it
does not bind a multi-step `GameEngine` continuation to the session generation
that produced the accepted GM result. After the last repair-specific generation
check releases its canonical lease, `SaveLoadService.LoadGameAsync` can rotate
the generation and install a replacement session. The old continuation can then
acquire a new, otherwise valid canonical lease and mutate the replacement.

The partial `SessionLifecycleLease` implementation now present in the dirty
worktree gives Load and Clear a second lock, but `GameEngine` does not currently
use it. The new contention tests can therefore pass while the reported race
still exists. Holding that lease over a complete GM wait or repair loop would
not be a correct completion: it would make Load/New Game wait for up to the
terminal timeout, or fail after the lifecycle lock's current roughly ten-second
retry window, instead of promptly terminating the old operation with typed
`SessionReplaced`.

The minimum robust harness fix is:

1. Bind each logical turn/session operation to one immutable generation.
2. Enforce that binding inside every canonical mutation's existing canonical
   lease, after load/worker recovery and before the first mutation.
3. Poll the same binding during long terminal and repair waits.
4. Propagate a core typed `SessionReplacedException` through the entire logical
   caller, never through the ordinary cancellation/rollback path.
5. Use `SessionLifecycleLease`, if retained, only as a short multi-write
   quiescence gate. It is not the stale-writer authority.

This closes the race without holding the non-reentrant canonical lock, or a
replacement-blocking lifecycle lock, across service calls and waits.

## Evidence

### Canonical lease is operation-local

`FileSystemManager.AcquireCanonicalWriteLeaseAsync` currently:

- opens `.boe_runtime/locks/canonical-write.lock` with `FileShare.None`;
- recovers an interrupted load;
- recovers an interrupted worker apply;
- returns the lease.

Each public writer acquires a new lease. Once a repair helper releases its
lease, nothing records that subsequent writers belong to the old generation.
Recovery is correctly placed at lease acquisition, but there is no
generation-bound authorization check for ordinary `GameEngine` writers.

Relevant snapshot locations:

- `BookOfEternityClient/Core/FileSystemManager.cs:474`
- recovery at `FileSystemManager.cs:502-503`
- generation read/check/rotate near `FileSystemManager.cs:1086`
- public mutation entry points near `FileSystemManager.cs:203-458`

### Load is atomic only with respect to an individual canonical lease

`SaveLoadService.LoadGameAsync` stages the archive outside the canonical lock,
then:

1. invokes `BeforeLoadLeaseAcquisitionAsync`;
2. acquires the new lifecycle lease;
3. acquires the canonical lease;
4. rotates the external session generation;
5. journals and swaps the live session;
6. refreshes runtime state and commits under that canonical lease.

Relevant snapshot location:
`BookOfEternityClient/Services/SaveLoadService.cs:215-258`.

That ordering protects the swap itself. It cannot identify a writer that starts
after the swap unless the writer presents the generation it was originally
authorized to mutate.

### Concrete race

One valid interleaving is:

1. Old operation `O(G1)` completes repair.
2. `AppendClearedValidationRepairTrajectoryAsync(..., G1)` and
   `DeleteValidationRepairFilesForSessionAsync(G1)` check `G1` under a
   canonical lease and release it.
3. Load acquires lifecycle then canonical, rotates to `G2`, installs the
   replacement, and releases both leases.
4. `O(G1)` resumes at
   `_progressionSchedule.ApplyAcceptedTurnOutcomeAsync(...)`.
5. That service acquires a fresh canonical lease. Recovery succeeds, but no
   expected generation is supplied, so it updates the replacement's
   `progression_schedule.json`.
6. The old continuation can then rotate pending state, normalize UI artifacts,
   append story, increment the in-memory turn, consume pending state, autosave,
   or delete replacement control files.

The immediate seam is
`BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:128-145`.
The final repair cleanup is generation-aware; progression application at line
144 is not.

### Repair loops can adopt the replacement generation

`ValidateAcceptedTurnOutcomeWithRepairLoopAsync` captures the current generation
separately at each critical repair branch. `ValidateCurrentGameStateOrShowErrorsAsync`
does the same for each ordinary repair iteration. If Load lands between
iterations, a later iteration can capture `G2` and dispatch a repair against the
replacement rather than aborting `O(G1)`.

Relevant locations:

- `GameEngine.ValidationAndRepair.cs:178`
- `GameEngine.ValidationAndRepair.cs:243`
- `GameEngine.ValidationAndRepair.cs:283`
- `GameEngine.ValidationAndRepair.cs:313`
- post-materialization loop at `GameEngine.ValidationAndRepair.cs:373-385`

Generation must be captured once by the logical operation and passed through
all retries. A repair attempt must never recapture "whatever is current now."

### `WaitForGmResponse` has a long unguarded tail

After accepted-turn validation, `WaitForGmResponse` performs response reads,
marker cleanup, pending-state rotation, normalization, post-materialization
repair, backup cleanup, turn increment, story append, progression, life and
ascension transitions, QTE handling, autosave, and terminal cleanup.

Relevant locations:
`BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:34-191`.

`ProcessPlayerTurn` contains a parallel finalization pipeline at approximately
`GameEngine.TurnLifecycle.cs:1151-1285`; fixing only the named private wait
method would leave the ordinary turn path vulnerable.

### `WaitForGmResponseRaw` is not the ownership boundary

`WaitForGmResponseRaw` returns before accepted validation and finalization. Its
life-evaluation caller continues at
`GameEngine.TurnLifecycle.cs:1999-2049`.

The enclosing `CheckLifeTransitions` has a generic catch at lines `2061-2068`.
It currently logs to `error_log.txt` and converts the failure into a boolean.
A lifecycle exception thrown by the raw continuation would therefore be
swallowed unless there is an explicit typed rethrow before this catch. The
scope must cover `CheckLifeTransitions`, not only `WaitForGmResponseRaw`.

### Terminal wait is generation-blind

`WaitForTerminalSignalAsync` polls ready files every 500 ms and converts
`OperationCanceledException` into `TerminalSignalWaitOutcome.Cancelled`
(`GameEngine.TurnLifecycle.cs:316-409`).

After Load it can:

- wait until the full terminal timeout for an obsolete request;
- accept a ready file belonging to the replacement;
- enter the existing Cancelled branch, which deletes files and restores an old
  rollback snapshot into the replacement.

Session replacement must be a separate typed outcome. It must not derive from
`OperationCanceledException` and must not return `Cancelled` or `false`.

### Broad catches and direct filesystem writes bypass propagation

At least these paths require remediation:

- `StoryService.AppendTurnAsync` and `AppendMarkerAsync` catch all exceptions
  (`StoryService.cs:40-90`).
- `StateManager.RefreshGameStateAsync` catches broad exceptions.
- `CleanupPendingTurnSnapshotAsync` falls back to direct
  `Directory.Delete`/`File.Delete`
  (`GameEngine.SessionAndSnapshots.cs:807-883`).
- `AppendErrorLogEntry` directly calls `File.AppendAllText`
  (`GameEngine.TurnLifecycle.cs:3518-3528`).

The fallback deletes do not pass through either canonical recovery or a
generation fence. The generic life-transition catch can also append an old
error into the replacement. These are part of the same correctness boundary,
not incidental cleanup.

## New Game and Clear

`ClearGameStateAsync` now acquires lifecycle then canonical, rotates the
generation, and clears state. The primitive itself has the right lock order.
It discards the newly generated token and releases lifecycle immediately.

`InitializeChaosSea` then performs many independent canonical writes:

- soul and guardian state;
- afterlife profile and project state;
- chat, achievements, bootstrap files, and lore;
- settings, rollback/baseline snapshot, and the first turn request.

The clear occurs at
`BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs:1001`; bootstrap
writes continue through approximately line `1168`. Load, a second New Game, or
another Clear can win after line 1001. The first initializer can then populate
the newer session.

Required changes:

- make the lease-aware Clear primitive return the generation created by
  `RotateSessionGeneration`;
- bind the remainder of New Game to that returned generation;
- optionally keep one short lifecycle lease across Clear plus deterministic
  bootstrap/request staging;
- release lifecycle before `WaitForGmResponse`;
- keep the generation-bound operation active through the wait and its
  finalization;
- treat a second Clear/New Game exactly like Load: old bootstrap/wait exits
  through typed `SessionReplaced`, with no cleanup of the winner.

`ClearCurrentWorldLoreAsync` is a same-session mutation and should inherit the
active operation fence; it must not rotate the generation.

## Assessment of the partial `SessionLifecycleLease`

The dirty worktree now contains:

- `SessionLifecycleLease` and
  `TryAcquireSessionLifecycleLeaseAsync(expectedGeneration)`;
- lifecycle-before-canonical ordering in Load and Clear;
- tests that an explicit lifecycle holder blocks Load/Clear and that stale
  acquisition returns `null`.

These are useful primitives, but they are not sufficient acceptance evidence.
They do not prove that:

- a complete old `WaitForGmResponse` cannot write after Load;
- Raw's caller propagates typed replacement;
- a repeated repair does not rebind to the replacement;
- New Game bootstrap is protected after Clear;
- Load remains responsive while an old GM terminal wait is active;
- no fallback delete or broad catch defeats the fence.

The lifecycle lock has a retry budget of `200 * 50 ms`. If held over a GM wait,
repair worker, user prompt, QTE flow, autosave publication, or nested life
evaluation, Load/Clear can time out rather than replace the session. It also
creates a deadlock if any path takes canonical first and lifecycle second while
Load/Clear take lifecycle first and canonical second.

If retained, its contract must be:

- lock order is always `session lifecycle -> canonical`;
- never acquire lifecycle while holding canonical;
- never hold lifecycle over terminal wait, repair wait/process, UI/input,
  daemon call, or nested turn;
- `TryAcquire...` returning `null` maps immediately to typed
  `SessionReplaced`;
- it protects only a short compound phase where atomicity across several
  canonical operations is desirable.

The generation fence remains authoritative even when no lifecycle lease is
held.

## Recommended minimal design

### 1. Core exception

Introduce a core exception, for example:

```csharp
internal class SessionReplacedException : Exception
{
    internal string ExpectedGeneration { get; }
    internal string? ActualGeneration { get; }
}
```

It must not inherit `OperationCanceledException`.

Either replace `GmWorkerSessionReplacedException` with the core type or make the
worker-specific exception derive from it. All game-loop boundaries catch the
core type before broad catches.

### 2. Immutable session operation

Add a root-aware async-flow operation context, for example
`SessionOperationContext.RunBoundAsync(root, generation, body)`.

Required behavior:

- one immutable generation for the complete logical operation;
- nested scopes for the same root and generation reuse the same frame;
- a nested conflicting generation fails closed;
- the frame is mutable only for a sticky `Replaced`/`Closed` flag;
- escaped `Task.Run` continuations retain the frame and cannot write after the
  owning operation closes;
- successful delegate return performs one final durable generation check;
- if a child service catches a writer exception, the sticky frame still makes
  the outer boundary throw `SessionReplacedException`.

The operation context should be keyed by normalized canonical root, not by a
particular `FileSystemManager` object, because tests and separate service graphs
can create multiple manager instances for the same root.

Replacement coordinators need a narrowly scoped internal bypass/purpose:
`LoadGameAsync` and Clear must be able to rotate a generation rather than be
rejected by the old ambient frame. Do not expose a general public "ignore
generation" switch.

### 3. Canonical mutation seam

Change the existing canonical acquisition path, rather than acquiring an outer
canonical lease around `GameEngine`:

```text
acquire canonical file lock
recover interrupted Load
recover interrupted worker apply
read ambient expected generation for this root
compare expected/current while the same lease is held
on mismatch: mark scope replaced, dispose lease, throw typed exception
on match: return this lease to the existing writer
```

The compare must occur after recovery. This preserves FR-039 and prevents a
stale operation from bypassing an unfinished load journal. Check and mutation
must use the same lease; a pre-call `IsCurrentSessionGeneration` check would
reintroduce the same TOCTOU window.

Lease-taking internal overloads should carry or validate the authorization
established at acquisition. Load/Clear use explicit replacement authority.

This central seam covers existing public writes, deletes, backup restore and
cleanup, compare-exchange, schedule/story writes, and autosave snapshot
acquisition without threading a generation parameter through every service.

### 4. Bind complete logical callers

Capture/bind once at the earliest canonical staging boundary:

- ordinary `ProcessPlayerTurn`, including request staging and finalization;
- `WaitForGmResponse` when used as a standalone transition wait;
- all of `CheckLifeTransitions`, including the Raw continuation;
- incarnation handling, including writes after `WaitForGmResponse`;
- New Game, using the generation returned by Clear.

`WaitForGmResponse` and `WaitForGmResponseRaw` should require an active bound
operation or an explicit token. They must not silently capture a new generation
mid-flow.

### 5. Repair and terminal loops

- Replace every per-attempt `CaptureCurrentSessionGenerationAsync` with the
  operation's immutable generation.
- Call a durable `ThrowIfCurrentSessionReplacedAsync` before each repair
  dispatch and after each repair result.
- Poll the same check in `WaitForTerminalSignalAsync`, at the existing 500 ms
  cadence, before inspecting ready files.
- Check once more immediately after terminal wait completion, before resolving
  terminal artifacts.
- An in-process cancellation token may wake waits sooner, but durable
  generation comparison remains authority for cross-process Load/Clear.
- Publish in-process replacement cancellation only after Load/Clear releases
  canonical/lifecycle locks. Use asynchronous continuations. Never invoke
  cancellation callbacks while holding either lock.

If Load fails after rotation and rolls disk state back, do not restore the old
generation. Rotation is conservative invalidation: old operations must still
abort.

### 6. Typed unwind and runtime rebind

The typed catch must:

- run outside the bound old-operation scope;
- perform no old rollback, cleanup, trajectory, audit, ready, or error-log
  write;
- refresh `StateManager`;
- reset `_gameLoop` from the replacement state's session/turn;
- clear old `_lastResponse`, pending image, pending-memory, and transition
  flags;
- return to the appropriate menu/game-loop state.

Add an explicit `catch (SessionReplacedException) { throw; }` before the generic
catch in `CheckLifeTransitions`. Apply the same ordering to other broad catches
on the logical path. The sticky operation frame is still needed because leaf
services such as `StoryService` currently suppress failures.

### 7. Remove canonical bypasses

Move pending-snapshot fallback tree deletion into a
`FileSystemManager` lease-aware API that validates paths and does not follow
reparse points. Route `AppendErrorLogEntry` through a fenced API or keep the log
outside replaceable canonical state; in either case typed replacement must
bypass the generic logging path.

## Deadlock rules

The following are hard constraints:

1. Do not hold `CanonicalWriteLease` around `WaitForGmResponse`, validation,
   repair, or a service graph. It is non-reentrant; nested public writers will
   contend with the holder and time out.
2. If lifecycle remains, acquire only `lifecycle -> canonical`, never the
   reverse.
3. Do not call test hooks, cancellation callbacks, logging callbacks, UI, worker
   processes, archive code, or user input while either lock is held.
4. Dispose the canonical lease before throwing `SessionReplacedException`.
5. Perform replacement refresh/rebind only after the old scope and all locks
   are released.
6. A timeout watchdog in tests is not synchronization. Use explicit
   `TaskCompletionSource` barriers with
   `RunContinuationsAsynchronously`.

## Exact deterministic test hooks

Add one optional internal test hook object to `GameEngine`, not production
delays:

```csharp
internal enum SessionFinalizationCheckpoint
{
    TerminalWaitStarted,
    ValidationRepairClearedBeforeProgressionApply,
    AcceptedOutcomeValidatedBeforeMaterialization,
    RuntimeNormalizedBeforePostMaterializationValidation,
    PostMaterializedStateValidatedBeforeFinalWrites,
    RawAcceptedOutcomeValidatedBeforeLifeEvaluationFinalWrites,
    NewGameClearedBeforeBootstrapWrites,
    NewGameBootstrapStagedBeforeGmWait
}

internal sealed class GameEngineSessionFinalizationHooks
{
    internal Func<SessionFinalizationCheckpoint, Task>? AtCheckpointAsync { get; init; }
}
```

Invoke checkpoints outside canonical/lifecycle leases. Each test uses two TCS
barriers: `checkpointReached` and `releaseOldFlow`.

Existing `SaveLoadServiceHooks.BeforeLoadLeaseAcquisitionAsync` is sufficient to
coordinate Load before it enters replacement locks. Existing
`FileSystemManagerHooks.CanonicalWriteLockContendedAsync` and
`SessionLifecycleLockContendedAsync` are sufficient for lock-order tests. Do
not add a production hook that waits while a canonical lease is held.

Add a test helper that snapshots every regular file under `game_session` as
`relative path -> SHA-256`. In replacement tests:

1. await full Load/Clear completion;
2. capture the replacement tree;
3. release the old continuation;
4. assert the tree is byte-identical.

This catches stale deletes as well as stale writes and avoids asserting only
one convenient sentinel.

## Deterministic RED test matrix

### P0: direct writer authority

1. `SessionBoundWriter_LoadRotatesGeneration_ThrowsBeforeReplacementMutation`

   Bind `G1`, complete a real `LoadGameAsync` that installs `G2`, then invoke a
   public canonical writer in the old flow. Expect core
   `SessionReplacedException`; replacement tree remains byte-identical.

2. `SessionBoundWriter_ClearRotatesGeneration_ThrowsBeforeFreshSessionMutation`

   Bind `G1`, complete `ClearGameStateAsync`, seed a `G2` sentinel, then release
   the old writer. Expect typed replacement and unchanged `G2`.

3. `CanonicalAcquire_RecoversInterruptedLoadBeforeCheckingBoundGeneration`

   Arrange an active load journal and an old bound writer. Acquisition must
   finish/fail recovery first, then reject the stale generation, with no target
   mutation. This locks in FR-039 ordering.

4. `BoundPublicWriter_UsesSingleCanonicalLeaseWithoutSelfContention`

   Execute a normal writer under a bound operation and assert it completes with
   no canonical contention callback. This prevents an implementation that
   grabs an outer lease and then calls a public writer.

5. `BoundOperation_LeafServiceSwallowsTypedWriteFailure_OuterBoundaryStillThrows`

   Force replacement immediately before `StoryService.AppendTurnAsync`.
   Although the current leaf catch logs and returns, the sticky operation
   boundary must throw typed replacement and preserve the replacement tree.

### P0: full GameEngine finalization

6. `WaitForGmResponse_LoadAfterRepairCleanupBeforeProgressionApply_AbortsOldFinalizer`

   Produce one successful validation-repair cycle. Pause at
   `ValidationRepairClearedBeforeProgressionApply`, complete Load, snapshot the
   replacement, release the old flow. Expect typed replacement. Assert no old
   progression update, story append, pending rotation, rollback, cleanup,
   ready deletion, trajectory append, or turn increment survives.

7. `WaitForGmResponse_LoadAfterAcceptedValidationWithoutRepair_AbortsOldFinalizer`

   Pause at `AcceptedOutcomeValidatedBeforeMaterialization` with a clean
   accepted result. This proves the fix is operation-owned and not dependent on
   having entered a repair path.

8. `AcceptedValidation_LoadAfterFirstRepairAttempt_DoesNotRebindSecondAttempt`

   Pause after the first repair result, complete Load with a replacement that
   would otherwise trigger another repair, then continue. Expect typed
   replacement and exactly one old repair dispatch. No replacement
   `validation_repair_request.json`, worker task, ready, audit, or trajectory
   record may appear.

9. `WaitForGmResponse_LoadAfterNormalization_DoesNotRepairReplacementState`

   Pause at
   `RuntimeNormalizedBeforePostMaterializationValidation`, install a deliberately
   invalid replacement, then continue. The old post-materialization loop must
   throw before validation dispatch; it must not "repair" the replacement.

10. `WaitForGmResponse_LoadAfterPostMaterializationValidation_DoesNotFinalizeOldTurn`

    Pause at `PostMaterializedStateValidatedBeforeFinalWrites`, complete Load,
    then verify the exact replacement tree and in-memory game-loop rebind.

11. `ProcessPlayerTurn_LoadAfterAcceptedValidation_AbortsDuplicateFinalizer`

    Exercise the ordinary `ProcessPlayerTurn` pipeline, not the private wait
    helper. This prevents a fix that protects only transition turns.

### P0: Raw, cancellation, and catches

12. `LifeTransitionRaw_LoadAfterAcceptedValidation_PropagatesSessionReplaced`

    Invoke all of `CheckLifeTransitions`; pause at
    `RawAcceptedOutcomeValidatedBeforeLifeEvaluationFinalWrites`, complete Load,
    then continue. Expect typed exception to escape the generic catch. Assert no
    replacement story, rewards, afterlife-return activation, terminal cleanup,
    or `error_log.txt` append.

13. `TerminalWait_SessionRotation_CompletesPromptlyWithTypedSessionReplaced`

    Start a wait with no ready signal, await `TerminalWaitStarted`, complete
    Load or Clear, and require typed completion within a watchdog of a few
    seconds. Assert the replacement input/ready/snapshot files remain intact.
    The result must not be `Cancelled` and rollback must not run.

14. `LoadGameAsync_ActiveTerminalWaitIsNotBlockedByLongLifecycleLease`

    With the old wait active, Load must complete without waiting for GM timeout
    or the lifecycle lock retry budget. This catches accidental lifecycle lease
    ownership across `WaitForTerminalSignalAsync`.

15. `FailedLoadAfterGenerationRotation_StillInvalidatesOldOperation`

    Inject a post-rotation load failure that successfully restores disk/runtime
    bytes. Load returns failure, but the old `G1` operation must still receive
    typed replacement and must not resume writing the restored state.

### P0: New Game and Clear

16. `InitializeChaosSea_LoadWinsAfterClearBeforeBootstrap_AbortsOldNewGame`

    Pause at `NewGameClearedBeforeBootstrapWrites`, complete Load, snapshot the
    replacement, release initialization. Expect typed replacement; no old soul,
    guardian, chat, lore, baseline snapshot, or first request is written.

17. `InitializeChaosSea_SecondClearWins_AbortsFirstBootstrap`

    Pause the first New Game after its Clear, complete a second Clear/New Game
    generation, seed a sentinel, then resume the first. The first initializer
    must fail typed without modifying the winner.

18. `NewGameBootstrap_LoadAfterRequestStaging_AbortsWaitWithoutCleaningReplacement`

    Pause at `NewGameBootstrapStagedBeforeGmWait`, complete Load, then enter the
    wait. It must terminate typed and must not delete or roll back the loaded
    session.

### P1: lifecycle lock discipline

19. `LoadAndFinalizer_CompetingLockOrder_CompletesWithoutDeadlock`

    Hold canonical explicitly, start Load so it owns/waits in lifecycle-first
    order, then start stale finalizer acquisition. Release canonical and assert
    Load completes first; finalizer then observes stale generation. Use TCS
    contention callbacks and a watchdog, not `Task.Delay`.

20. `SessionLifecycleLease_IsNotHeldAcrossRepairOrTerminalWait`

    At both wait checkpoints, a competing lifecycle acquisition must complete
    promptly. This is a direct regression test against broad lease scope.

21. `SessionFence_AppliesAcrossFileSystemManagerInstancesForSameRoot`

    Bind with one manager and write with another manager for the same root after
    rotation. The old operation must still be rejected.

### Source guards

Add source/architecture guards that require:

- `WaitForTerminalSignalAsync` receives or reads a bound operation generation;
- no repair `while` loop calls `CaptureCurrentSessionGenerationAsync`;
- the named game-loop entry points run under a session operation boundary;
- `SessionReplacedException` is rethrown before broad catches;
- no direct recursive delete exists in pending-turn cleanup;
- Load/Clear are the only generation-rotation authorities;
- no lifecycle acquisition occurs while a canonical lease is in lexical scope.

## Why the existing lifecycle tests are not acceptance tests

The current RED/partial tests:

- `TryAcquireSessionLifecycleLeaseAsync_StaleGenerationReturnsNull`;
- `ClearGameStateAsync_WaitsForActiveSessionLifecycleLease`;
- `LoadGameAsync_WaitsForSessionLifecycleLeaseBeforeReplacingLiveSession`;

verify the new lock primitive in isolation. They do not reproduce the finding.
All three can pass while `WaitForGmResponse` writes replacement
`progression_schedule.json` immediately after Load. Keep them only as primitive
tests if the lifecycle gate remains, and add the full-flow tests above.

The current tests also use fixed delays in some contention assertions. Replace
those delays with hook/TCS barriers so a slow CI worker cannot convert a race
test into a false pass or false failure.

## Rejected remediations

- **One more generation check after repair:** still has check/write TOCTOU.
- **Check only before progression apply:** later story, cleanup, autosave, Raw,
  and New Game writes remain exposed.
- **Capture generation on every retry:** explicitly allows adoption of the
  replacement.
- **Return `false`/Cancelled on replacement:** existing branches delete files
  and restore old backups into the winner.
- **Use `OperationCanceledException` for replacement:** it is already converted
  to ordinary user cancellation.
- **Hold canonical lease over finalization:** nested writers self-deadlock on the
  non-reentrant file lock.
- **Hold lifecycle lease over the complete Wait/repair:** Load/New Game becomes
  unresponsive or times out; cross-process cancellation cannot safely break the
  holder.
- **Rely on browser write coordination:** it is advisory and does not protect
  direct `SaveLoadService`, CLI, tests, another process, or cleanup races.
- **Fix only worker apply/repair leaf methods:** the stale continuation is the
  caller after those methods return.

## Documentation and task impact

This is a client-owned harness synchronization change. It does not add or
change a GM-authored command, pending/control schema, afterlife action type,
receipt, or player-facing contract. No update is required to
`Afterlife_Contract_Matrix.md`, GM examples, manifests, or afterlife
documentation coverage tests solely for this remediation.

The Spec Kit artifacts for issue `#1500` do require an update because the
current completed wording in FR-039/FR-042, T115, and T121 overstates the
end-to-end guarantee. The implementation task should explicitly cover:

- immutable operation generation;
- canonical writer fence after recovery;
- terminal wait and Raw propagation;
- New Game/Clear bootstrap ownership;
- full-flow Load/Clear RED evidence.

## Acceptance criteria

The finding can be closed only when all of the following are demonstrated:

- Load and Clear can win while an old terminal/repair wait is active.
- Every old canonical mutation after rotation fails under the same lease before
  touching live state.
- The replacement tree is byte-identical after the old continuation unwinds.
- Raw and all broad-catch callers propagate core typed `SessionReplaced`.
- No old rollback, cleanup, audit, trajectory, ready, story, progression,
  autosave, or error-log action targets the winner.
- Repair retries never recapture the replacement generation.
- New Game owns the generation returned by Clear through bootstrap and wait.
- Lock-order tests complete without deadlock, and no long wait owns lifecycle
  or canonical lease.
