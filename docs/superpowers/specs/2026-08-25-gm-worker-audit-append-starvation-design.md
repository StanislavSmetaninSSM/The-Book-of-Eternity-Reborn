# GM Worker Audit Append Starvation Design

**Tracked task:** [GitHub issue #1546](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1546)

**Related contract:** `specs/1500-complete-actor-materialization/spec.md` FR-020e and SC-005

## Problem

`GmWorkerAuditLog.AppendEventAsync` permits many same-process writers to poll the
same cross-process canonical-write file lock concurrently. Each successful
writer reads and atomically rewrites the growing JSONL file. Under load, an
unlucky waiter can exhaust the bounded lock polling window; the audit layer
then intentionally suppresses that telemetry exception, so callers complete
but one event is absent. Historical PreMerge evidence observed 31/32 and 25/32
events with unchanged production and test code.

The fix must satisfy both existing requirements:

- concurrent audit appends preserve every event;
- failure to publish diagnostic telemetry does not roll back or revoke an
  already accepted canonical operation.

## Chosen design

Add one in-process audit-append admission gate to each interned
`CanonicalRootIdentity`. Every `FileSystemManager` for the same physical root
therefore shares the same gate without a permanently growing static path map.

Audit operations that need to acquire their own canonical-write lease enter
the admission gate first, then acquire the existing cross-process canonical
lock, perform the complete read-and-append atomic replacement, release the
canonical lease, and finally release admission. Only one same-root audit writer
can poll the cross-process lock at a time.

Audit operations that receive an already-active canonical-write lease do not
enter admission. Waiting for admission while holding that lease could deadlock
with an admitted audit writer waiting for the same canonical lock. Their caller
already provides serialization and exact authority.

The gate changes only in-process admission. The existing canonical lock remains
the cross-process authority and still protects the entire read/rewrite
operation. Session-generation checks remain inside the canonical lease.

## Covered entry points

The admission boundary applies to every audit operation that acquires its own
lease:

- public best-effort `AppendEventAsync`;
- session-bound `AppendEventIfCurrentSessionAsync`;
- required idempotent `AppendRequiredEventOnceIfCurrentSessionAsync`.

The internal `AppendEventAsync(CanonicalWriteLease, ...)` path remains unchanged
because its caller already owns the canonical lease.

Cancellation-aware entry points pass their cancellation token to admission and
canonical-lock acquisition. Admission is always released in `finally`.
Best-effort paths continue suppressing publication failures; required paths
continue surfacing them. No retry count or timeout is increased.

## Rejected alternatives

1. Retrying the complete append or increasing `CanonicalWriteLockRetryCount`
   leaves non-fair polling intact and only moves the starvation threshold.
2. A global fair scheduler for every canonical write is broader than issue
   #1546, changes lock ordering across the application, and would require a
   separate architecture review and likely a Spec Kit feature.
3. Weakening the 32-event assertion contradicts FR-020e and SC-005.

## Verification design

Add a deterministic unit regression using existing `FileSystemManagerHooks`:

1. The first audit writer acquires the canonical lease and pauses at the
   mutation boundary.
2. Additional writers for the same root start concurrently, using at least two
   `FileSystemManager`/`GmWorkerAuditLog` instances to prove root-shared
   admission.
3. `CanonicalWriteLockContendedAsync` records or fails any same-process audit
   attempt that reaches cross-process polling while the first writer is held.
4. Before the implementation, competing writers reach that hook and the test
   fails with missing events or forbidden lock contention.
5. After the implementation, they wait before the file lock, all unique events
   are present, and same-process canonical-lock contention is zero.

Retain the existing 32-writer 32-unique-event test. Then run the exact new
regression repeatedly, the complete `GmWorkerAuditLogTests` class, the affected
Focused controls from #1535/#1543, and final PreMerge.

## Scope and documentation

This is a narrow harness/runtime reliability repair against the already
documented #1500 contract. It adds no gameplay mechanic, GM-authored command,
state field, afterlife pending/control surface, response, receipt, or player
copy. Mortal World and afterlife prompts, examples, manifests, and contract
matrix require no update. No migration, fallback, dual reader/writer, GitHub
Actions change, or compatibility layer is introduced.
