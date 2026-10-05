# Worker durable restart and fencing design

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553),
T041-WORKER-RESTART-DESIGN / US4 / FR-012/014/015.
Base: `30ab292d46f6a8fc88b03ee8f3d0731523788e8d`, accepted bounded synthetic POOL B.
Status: design WIP for independent Sol6.1/xhigh review; implementation requires the
parent's separate launch instruction. This document is the architectural brief and
sequential plan; existing Spec Kit spec/plan/tasks remain the durable source of scope.
Use Superpowers executing-plans/TDD for each later authorized slice, sole writer and
independent review. No new runtime, tests, probes, dependency or permission changes here.

## Intent and preserved scope

A fresh application process must not forget an earlier worker and regain its slot,
repeat Release, accept a stale result, or publish into a replaced generation merely
because in-memory objects disappeared. Preserve the exact existing owner/evidence
while alive; after its loss retain durable blocking evidence without pretending to
reconstruct pidfd, Job handle, output tasks or publication permit from JSON.

The connection remains explicitly injected and bound to isolated synthetic fixture
roots. Public Linux WorkerRelease stays closed. Main PTY, live GM/provider, real saves,
main-run integration, systemd implementation and Windows runtime qualification are
outside these slices. No same-PID reconstruction/signals, namespace/root/cgroup changes,
new service, manual force-clear, or cold automatic result acceptance.

## Verified source map at the base

| Boundary | Actual source and finding | Design consequence |
|---|---|---|
| Main record | `Services/GmRuntime/GmSessionRunRecord{,Codec}.cs`, `GmSessionRunAdmission.cs`: pure schema1,64KiB, strict identity/positive epoch, cold nonterminal -> Uncertain; no production references outside these files | Preserve accepted90 tests and types. Main slot never represents worker inventory. |
| Main stop transition | `GmSessionRunTransitions.ConfirmStopped` accepts trusted same-identity complete stop or separately authenticated reboot | Do not reuse this transition for absorbing worker Uncertain. A codec does not authenticate either observation. |
| Worker ownership | `GmWorkerExecutionAuthority.cs`: original task bytes/model digest, live completion, scoped stop, outputs and publication permit are in memory | A worker-specific durable envelope is required; persisted facts cannot recreate `Allows` authority. |
| Capacity | `GmWorkerBridgePool.AcquireWorkerSlotAsync`: static per-root/worker semaphore; `GmWorkerQuarantineReaper`: in-memory entries/default global capacity32 | Restart must reconstruct reservations or block admission before allocating fresh capacity. No fake `IGmWorkerQuarantineOwner`. |
| Task reservation | `GmWorkerBridgePool.TryReserveTaskAsync`: canonical lease, current generation, create-only exact task bytes | Preserve this check; runtime metadata is outside replaceable task/proposal trees. |
| Identity/launch | Native and Windows owned launch allocate `_run` internally; workspace allocates a random leaf before preparation | Future durable intent must be allocated before any helper/host start and bound to that same actual owner before effects. |
| Release | `GmWorkerProcessHostLaunch.ReleaseAsync`: control gate, original Ready identity liveness, framed write; `_released` is in-memory after write | Durable ReleaseIntent must precede the one write; failure/ack loss cannot authorize retransmission. |
| Canonical lease | `FileSystemManager.AcquireCanonicalWriteLeaseCoreAsync`: FileShare.None original lock handle, then legacy/local recovery before return | A fence checked only after ordinary lease acquisition is too late. Future injected check belongs after original lock validation and before every recovery writer. |
| Generation | `.boe_runtime/session-generation/current.json`; lifecycle lock -> replacement write lease; ordinary Store checks exact generation under canonical lease | Generation observation cannot mint a new generation. Hold the canonical lease through the final Release send or publication boundary. |
| Proposal | `GmWorkerProposalStore.PublishBundleAsync`: stage, lease, generation/task bytes, create-only bundle move, derived inbox/audit | Durable publication intent/commit bookkeeping must surround the real bundle boundary under the same lease, not before unrelated awaits. |
| Quarantine | `GmWorkerQuarantinedExecution` retains phase state; receipt comes after validated stop/outputs and workspace deletion, before runtime-authority/slot disposal | Existing receipt is cleanup audit only. It cannot authenticate a cold stop or allocate a new live owner. |
| Runtime path | `GmWorkerExecutionWorkspace.ResolveRuntimeRoot`: env/configurable external base plus root hash; `.boe_runtime` is rooted at `FileSystemManager.BasePath` | Durable admission root must not move when workspace environment changes; record its exact original workspace binding. |
| Diagnostics | Inbox service reads saved proposals; general FileSystemManager reads can invoke publication recovery | Durable diagnostics must use a strict nonmutating reader and never re-enter automatic recovery. Saved bundle visibility is not automatic acceptance. |

All C# paths above are under `BookOfEternityClient/`. Pure main record qualification:
[accepted proof](recovery/gm-run-record-qualification.json). Pool proof:
[accepted proof](recovery/worker-pool-qualification.json). No previously passing cohort
is rerun for this document-only investigation.

## Approaches

1. **Recommended: durable worker ledger plus conservative cold admission.** One
   cooperating pool coordinator owns a root lifetime lock; short serialized ledger
   updates record intent before effects. Lost live authority remains blocked. This
   gives useful crash safety without adding a daemon or transferring kernel handles.
2. Authenticated reconnectable native supervisor with durable terminal receipts could
   later recover some abandoned executions. It changes helper lifetime, authentication,
   ownership transfer and status protocol; it is a separate design/qualification, not
   a shortcut for this plan. Current EOF cleanup does not deliver cold proof.
3. Persist the main record or infer stop from audit/PID/output files: rejected. It
   loses independent workers, cannot authenticate stop and can erase sticky uncertainty.

## Worker identity and storage contract

Introduce separate internal `GmWorkerRunIdentity`: normalized trusted root key,
positive root-wide Int64 epoch, random RunId, existing generation, WorkerId, TaskId,
exact reserved task SHA256, backend and guarantee, host-instance token. Native boot
identity may be recorded diagnostically; changed boot never clears worker Uncertain.
Target OS chooses root comparison; all other fields are exact. Epoch never wraps or
resets on generation replacement. No PID, launch nonce, credentials, environment or
provider content becomes durable authority. Keep main schema1 unchanged.

Use fixed `<BasePath>/.boe_runtime/worker-runs-v1/` independent of workspace env settings:
`owner.lock` (long-lived coordinator ownership), `journal.lock` (short updates),
`state.json` (schema1 root binding, sequence, epoch high-water and up to32 retained
execution reservations), and `retired/<RunId>.json` (create-only exact final tombstone).
Maximum record64KiB, state4MiB, strict UTF-8/JSON, duplicate/unknown keys rejected;
no unbounded allocation on read. Unknown version, malformed/missing required record,
nonordinary path, wrong root, sequence/epoch overflow or inconsistent archive -> Blocked.
File names alone never grant identity. No garbage collection or archive pruning here.
A terminal archive is written before removing its reservation from state; crash between
those steps only retains capacity. Exact archive retry is idempotent; conflict blocks.

First initialization is an explicit create-only operation under the original locks
on a trusted empty namespace. A missing state inside an initialized/nonempty namespace
is not fresh bootstrap. No recovery by resetting epoch, deleting markers or choosing
an older backup. Atomic replacement/CAS compares exact previous bytes plus sequence
under the retained journal lock; unchanged writes are idempotent. A failed/ambiguous
write preserves old/new/temp evidence and blocks side effects. This is a cooperating
application fence under the accepted trusted-local-player model, not protection from
an owner maliciously editing/removing the entire runtime root.

Linux durable publication must flush the new file, atomically install the same-directory
name and synchronize the parent directory before acknowledgement. File flush alone
is insufficient for directory-entry durability ([fsync manual](https://man7.org/linux/man-pages/man2/fsync.2.html)).
Reuse applicable trusted-local path/byte primitives, but do not call their canonical
recovery API recursively to write the fence. Missing durability/lock capability fails
closed. Windows needs its own tested adapter before any native qualification; do not
infer a portable directory-flush guarantee from `FileStream.Flush(true)`.
Existing root locks establish cooperating exclusion, not stop evidence. Linux locks
can survive exec through inherited descriptors ([flock manual](https://man7.org/linux/man-pages/man2/flock.2.html));
qualify that new lifetime/journal descriptors never enter helper/host/worker inheritance.
No network filesystem, power-cut or storage-controller reliability claim follows
ordinary-file or process-termination tests.

## Lifecycle and uncertainty

The durable state records intent and progress, not process authority:
`Prepared -> ReleaseIntent -> Released -> StopValidated -> PublicationIntent -> Published
-> CleanupPending -> Retired`. A no-result path may skip publication after validated
stop/output. `AbortedBeforeLaunch` is permitted only while the same live coordinator
can prove Start was never attempted; cold Prepared cannot use that shortcut.
Every nonterminal cold observation becomes effective Uncertain, including a durable
StopValidated/Published progress record. Explicit Uncertain is absorbing in all
planned slices. A reboot, late ECHILD, new generation, PID absence, cleanup receipt,
existing bundle or acquired owner.lock cannot clear it. Disk observation stays
nonmutating; a startup refusal need not rewrite the original evidence.

The ledger stores monotonic phase flags and fixed receipt/proposal identifiers/digests
needed to recognize exact retries, plus the original workspace/runtime binding. It
never deserializes a live success permit. Retired means live stop+outputs and required
cleanup/audit phases completed without ever latching Uncertain; it is not permission
to replay that task or automatically apply its proposal after restart. Preserve all
canonical accepted bytes if the application died after bundle publication. A stale or
unknown task is not retried with a new epoch merely to evade its tombstone.

A same-process post-stop filesystem/receipt failure keeps the original phase owner
and may retry exactly as in B. A process death loses that live owner: the same durable
CleanupPending now blocks; do not manufacture a reaper owner from saved paths.
Persist Retired/archive before releasing the worker slot or quarantine reservation.
If retirement publication fails, capacity stays retained. Tombstones survive session
replacement and never delete retained workspaces. Failed uncertainty persistence
cannot silently leave a usable coordinator; its live gate closes while the prior
nonterminal record still makes the next process refuse admission.

## Fences, lock order and actual pool connection

One root coordinator is shared by cooperating pool instances; a second process must
fail admission while its non-inherited owner.lock remains held. Reacquiring that lock
after death is only exclusion, never proof of old process-tree stop. Do not release
it while any live execution/quarantine operation can still mutate ledger or publish.

Lock order: optional session lifecycle -> canonical write lock -> journal lock;
never acquire canonical/lifecycle while holding journal. Root owner.lock is acquired
at coordinator admission before these operations and retained; no hot path reacquires
it under a canonical lease. Slot waits occur outside canonical/journal locks. Calls
already holding the canonical lease pass it explicitly instead of reacquiring it.
No managed `WaitAsync` timeout abandons an underlying ledger or pipe write and then
releases its fence; original I/O must settle, or ownership remains retained/blocked.

For the later explicitly injected fixture contour:
- Split canonical lock acquisition from recovery just enough to invoke a typed worker
  admission predicate after exact lock-handle validation and before legacy/local
  recovery. Check both newly acquired and already-held lease mutation paths. Diagnostic
  ledger reads do not call that acquisition. Default/public behavior remains unchanged
  until a separately approved rollout; this is not a whole-root/main-writer fence.
- Admission binds coordinator, root, current existing generation and exact task. It
  reserves durable capacity/epoch and Prepared before any helper/host start. Workspace
  leaf and RunId are chosen first, then passed through sealed internal preparation
  tokens so actual owners use the same identity; do not expose arbitrary success hooks.
- Under the current canonical lease and short journal transaction, persist ReleaseIntent,
  recheck exact original live owner and generation, then perform at most one Release
  write while the lease remains held. A partial write/failed acknowledgement remains
  uncertain and is never retransmitted. Close new admission before cleanup. Release
  linearizes at this send; later generation replacement can invalidate detached work,
  but cannot cause an old result to publish.
- After original correlated success, scoped stop and actual output settlement, enter
  Store through a sealed live execution permit. Under its original canonical lease,
  revalidate durable epoch/run/root/task and current generation after all awaited
  preparation, then record PublicationIntent and perform the existing create-only
  bundle publication. Commit result/digests before reporting success to both consumers.
  On ambiguous commit persistence preserve bundle/evidence and suppress new success;
  never undo accepted bytes merely to make metadata look clean.
- Reaper retry keeps the same ledger binding/phase owner. Cleanup-confirmed audit or
  fallback receipt is still required before terminal archival and capacity release.
  Neither missing main record nor main Stopped skips any worker condition.

R2/R3 must exercise all relevant pre-recovery/held-lease routes in the injected contour.
Until those routes are actually connected and qualified, documentation must say
"durable metadata prerequisite", not "restart-safe pool" or "all writers fenced".

## Sequential implementation plan (not authorized yet)

### R1 — worker durable ledger prerequisite; first implementable slice

Goal: a real disk-backed, exact-identity worker ledger whose cold reader and concurrent
writers cannot forget ambiguous reservations. It does not launch processes or connect
production pool admission.

Files: create `Services/GmWorkers/GmWorkerRunRecord.cs`, `GmWorkerRunRecordCodec.cs`,
`GmWorkerRunLedger.cs`; isolate native storage details in
`Core/WorkerRunLedgerPersistence.cs`. Tests: `BookOfEternityClient.Tests/GmWorkerRunLedgerTests.cs`
and a finite TestSupport child entrypoint for real cold reads/crash cuts. Retain main
codec/admission files unchanged. All service/Core paths are under BookOfEternityClient.

Interfaces: `ObserveAsync(WorkerLedgerTarget, CancellationToken) -> Task<WorkerLedgerObservation>`
never creates/repairs files; `OpenCoordinatorAsync(...) -> Task<WorkerLedgerCoordinator>`
retains original root locks; `InitializeAsync(...)`, `CompareExchangeAsync(expectedBytes,
desiredSnapshot, CancellationToken)` and `ArchiveRetiredAsync(exactRecordBytes, ...)`
return explicit Applied/AlreadyExact/Blocked outcomes. Only a coordinator can mutate;
returned observations never implement a lease/permit. Concrete record fields/bounds,
identity comparison and cold semantics are defined above; unsupported platform durability
returns Blocked before publication. Cancellation after mutation starts retains/settles
original I/O before reporting its result.

- [ ] Add a coherent `worker-run-ledger` category and explicit selection for this slice.
  Write cold tests for Prepared/ReleaseIntent/Published/CleanupPending/Uncertain;
  each blocks, preserves exact evidence and cannot return a success permit.
- [ ] Add pure and real-file tests: wrong root/generation/epoch, malformed/duplicate/
  oversize JSON, epoch overflow, two-process same-root contender, exact CAS conflict,
  missing initialized state, env workspace-base change, nonordinary paths, cancellation
  around publication, exact terminal archive retry/conflict and descriptor inheritance.
- [ ] Add finite child-process abrupt-exit cuts before/after temp flush, rename and
  parent sync; fresh child sees the exact old or new snapshot or explicit Blocked,
  never false Missing/retired. Preserve outside sentinels. Injected syscall failures
  test refusal paths; they are not a power-loss qualification.
- [ ] Publish scaffold and PlanOnly; distinguish build preparation failure from causal
  assertions. Minimal implementation then focused GREEN through `scripts/test-csharp.ps1`.
  No pool Release, native descendants, main-record90 or old passing cohorts in R1.
- [ ] Verify catalog discovery, exact source/commands/counts/cleanup, separate
  Sol6.1/xhigh review and GitHub restoration. Handoff as a prerequisite only.

### R2 — actual synthetic pool admission, intent and cold refusal

Depends on R1 and the product admission choice below. Modify explicit native fixture
admission/coordinator, pool, workspace preallocation, OwnedLaunch/native launcher RunId
binding and the minimal FileSystemManager pre-recovery hook. Preserve default public
Linux refusal and original Windows Job lifecycle. Add `GmWorkerRestartAdmissionTests`
under its own selected category with actual pool/independent guardian and finite actors.

- [ ] Causal test: kill the original application after durable Prepared / after real
  helper bind / before Release; fresh actual pool must preserve capacity/evidence and
  send zero Release, with guardian cleanup independent of its decision.
- [ ] Connect one shared coordinator, exact preallocated identity and pre-recovery
  admission. Prove a held old lock blocks a second app, PID reuse/boot change do not
  clear evidence, terminal old epochs cannot restart same task, and valid fresh roots
  still reach the existing synthetic path only through explicit injection.
- [ ] Add generation replacement at queued Release, during prepare and immediately
  before send; old-generation dispatch is rejected at its held-lease boundary.
- [ ] Review/qualify this admission slice before attempting publication recovery.

### R3 — one-send/publication fences and retirement

Depends on R2. Modify ProcessHost Release, real pool/Store, ExecutionAuthority and
QuarantinedExecution through typed permits; preserve actual proposal bytes, create-only
publication, generation and B cleanup order. Add narrow `GmWorkerRestartFenceTests`.

- [ ] Causal Release write/ack-loss case proves fresh restart never repeats a send.
  Old coordinator/epoch and same-ID changed task/proposal cannot publish.
- [ ] Crash cuts after actual Completed, stop, output settlement, before/after bundle
  move, before derived inbox/audit, before/after terminal archive and capacity release.
  Fresh process accepts no old result; canonical committed bytes and uncertain
  workspace remain unchanged; cleanup retries cannot double-release capacity.
- [ ] Prove unbound acquisition is rejected before any recovery writer and already-held
  leases recheck the same fence. Race replacement/publication under original lock;
  exactly one generation ordering wins, stale paths never mutate the new generation.
- [ ] Preserve B live happy/content paths and only affected uncertainty/receipt/consumer
  cases; do not rerun all127 solely because a later slice uses the same pool.
- [ ] Final source-specific native process-crash packet, independent review, normal
  publication/readback/fresh restore. Stop before public rollout or autonomous salvage.

## Product decisions versus technical choices

No new product decision blocks R1, which is inert outside synthetic tests.
Before R2, ask the parent to choose restart admission after a valid unresolved worker:
**recommended first bound: block new synthetic pool dispatch for that root**, while
preserving per-worker/quarantine reservation counts and diagnostic access. Allowing
unrelated workers to continue while reconstructing only affected capacity is a useful
later product policy, but requires a reviewed complete-inventory/limit-change contract.
Malformed or incomplete inventory blocks the whole pool in either policy.

Automatic recovery of lost authority, manual force-clear, reboot-based quarantine
release and auto-replay/application of a saved proposal are not technical defaults;
they require separate owner product decisions and separate authority designs. This
plan supplies none. The initial qualification target is application-process crash
and restart on supported local filesystems; storage flush ordering is designed, but
power-cut/reboot durability and Windows native acceptance require their own evidence
before a broader product claim. This limitation does not weaken any cold refusal.

## Design verification / delivery

Source inspection only, zero tests/native probes. Self-check: main/worker identities
remain separate; all uncertainty paths retain evidence; no recorded observation grants
live authority; source bootstrap cannot recover before admission; lock hierarchy is
acyclic; every side-effect cut has an owned crash test in R1–R3. Keep current catalog
and CI selection unchanged until an implementation slice adds actual tests. Source
hashes, independent design verdict and fresh GitHub restoration go in the existing
plan checkpoint. Parent separately authorizes any implementation after reading this
plan and the specific R1 handoff.
