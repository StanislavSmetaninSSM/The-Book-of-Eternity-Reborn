# Worker durable restart and fencing design

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553),
T041-WORKER-RESTART-DESIGN / US4 / FR-012/014/015.
Base: `30ab292d46f6a8fc88b03ee8f3d0731523788e8d`, accepted bounded synthetic POOL B.
Status: independent actual gpt-6.1-sol/xhigh DESIGN PASS at
`e90d5e4dbae51f6d9ba4601bdba3a060f29720dc`, no unresolved findings;
R1 accepted at `25c16dcb513de8b09fc67059465d9dcfb6b96224`; parent now authorizes R2
with owner-selected root-wide new-job refusal for unresolved workers (2026-10-06).
R3 still requires a separate launch instruction. This document is the architectural brief and
sequential plan; existing Spec Kit spec/plan/tasks remain the durable source of scope.
Use Superpowers executing-plans/TDD for each later authorized slice, sole writer and
independent review. Current technical R2 refinements and execution checkpoints live at
the start of plan.md; no dependency or permission changes are authorized.

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
resets on generation replacement. High-water allocates identities, not invalidation:
each active permit must match its own exact retained entry/epoch and current live
coordinator; it need not equal the latest high-water. Concurrent valid workers with
epochs N and N+1 remain valid independently. A new generation never rewrites an older
entry's identity or clears its reservation. No PID, launch nonce, credentials, environment or
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
on a trusted empty namespace. Create/open the stable owner.lock with exclusive access,
then journal.lock in that order; never delete/rename a lock file. Remember whether this
exact coordinator created the namespace and lock entries. Only that original creation
attempt may initialize; any existing namespace/lock without state after a previous
crash blocks. Qualification cuts include directory creation, each lock creation and
initial state install, not only later replacements. Synchronize new ancestor directory
entries before reporting initialization durable. A missing state inside an initialized/nonempty namespace
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
`Prepared -> LaunchIntent -> ReleaseIntent -> Released -> StopValidated -> PublicationIntent
-> Published -> CleanupPending -> Retired`. LaunchIntent is durable before any Start
attempt, including helper creation; StopValidated requires the original matching scoped
stop AND actual owned-output settlement. There is no edge from Uncertain to any other
state. Same-phase exact retries cannot change identity or completed proof. A no-result path may skip publication after validated
stop/output. `AbortedBeforeLaunch` is permitted only from Prepared while the same live coordinator
retains its original unconsumed launch intent and can prove Start was never attempted; cold Prepared cannot use that shortcut.
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
Retirement uses a frozen exact commit plan and an explicit RetirementCommitPending state.
Complete all fallible process/channel/workspace-deletion phases first, retaining original
workspace/runtime authority. Existing B requires cleanup-confirmed audit/fallback only
if the execution entered quarantine; ordinary successful cleanup does not gain a new
audit event. Freeze that requirement, stable event ID, original publication/cleanup facts
and terminal bytes before the terminal commit. Durable retirement requires the exact
archive and committed removal of its active reservation. The live owner additionally
requires acknowledgement before releasing retained runtime authority, slot or
reservation. All work that can change physical cleanup, required audit or terminal
evidence completes before durable retirement. Later local handle/bookkeeping disposal
failures retain the original cleanup owner, coordinator lease and live slot/reservation
until settled, without changing committed bytes, frozen audit requirements or the
original publication permit. A candidate archive alone never frees capacity.

A definite pre-commit failure may transfer ordinary cleanup to quarantine while it
still owns runtime authority, complete the now-required original audit/fallback and
then create its terminal plan. An ambiguous/installed commit or lost ACK remains
RetirementCommitPending with the same original owner, frozen bytes and requirements; it cannot
be reclassified into a different terminal receipt plan. Observe/retry that exact
publication only, no repeat Release/import or slot reuse. Distinguish this from
PublicationCommitPending: an ambiguous Published commit cannot mint a success permit.
A retirement-only failure after an already acknowledged durable Published commit
preserves the original live publication permit and B's CleanupDeferred result behavior;
it does not newly authorize or replay result acceptance. Freeze terminal bytes and the
conditional audit requirement through lost ACK and any post-commit resource-disposal
retry, without creating a new receipt requirement for that frozen commit. Cold
nonterminal/pending reservations block even if a matching candidate archive exists.
A cold reader checks exact durable state/archive consistency; it cannot know whether
the former owner received an acknowledgement. Fully committed retirement with no
active/pending reservation may admit a new task even after a lost live acknowledgement;
it is admission evidence only and never recreates a result permit. Metadata
CommitPending after already-validated stop is distinct from absorbing
execution Uncertain; it cannot conceal a process/output uncertainty. Tests must prove
this distinction and all lost-ACK cuts. Tombstones survive session
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
- R2 refinement after source review: legacy B and durable R2 modes cannot share a
  fixture root. Both cooperate through the same original non-inherited owner.lock
  and immutable create-only mode binding, checked before capacity/Start. A healthy
  R2 inventory cannot let an unrecorded legacy worker enter. Initial attach races,
  same-process shared instances and retained legacy quarantine use the same root
  registration/lock. Existing missing/partial/conflicting mode fails closed; no mode
  upgrade or reset. Legacy metadata is exclusion only and never durable stop/result
  authority. Distinct legacy roots preserve B behavior. R2 tests both process orders.
- Split canonical lock acquisition from recovery just enough to invoke a typed worker
  admission predicate after exact lock-handle validation and before legacy/local
  recovery. The typed purposes are ColdAdmission, TaskReservation, Release,
  ProposalPublication, ConfirmedCleanupAudit and DiagnosticObservation; a Boolean
  allow-anything hook is forbidden. Cold nonterminal or explicit Uncertain inventory
  blocks recovery and dispatch/publication. Warm reservation/Release/publication needs
  the current coordinator, exact applicable entry and generation; those purposes
  cannot authorize a cleanup receipt. ConfirmedCleanupAudit needs the original live
  positive stop/output authority and only its required exact audit operation; if the
  root inventory blocks canonical recovery, retain capacity and propagate the refusal,
  without inventing a new fallback for arbitrary I/O failures. Ledger uncertainty/
  CommitPending persistence uses its standalone journal path, never canonical recovery.
  DiagnosticObservation is standalone read-only, with no recovery side effects.
  Check both newly acquired and already-held lease mutation paths. Diagnostic
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
  fallback receipt remains required only after quarantine transfer; preserve the normal
  no-receipt path. Apply the frozen terminal commit protocol above before releasing
  retained runtime authority or capacity.
  Neither missing main record nor main Stopped skips any worker condition.

R2/R3 must exercise all relevant pre-recovery/held-lease routes in the injected contour.
Until those routes are actually connected and qualified, documentation must say
"durable metadata prerequisite", not "restart-safe pool" or "all writers fenced".

## Sequential implementation plan (R1 completed; R2 authorized; R3 not authorized)

### R1 — completed worker durable ledger prerequisite

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
retains original root locks. Expose typed operations: Initialize, Prepare, PlanLaunch,
PlanRelease, RecordReleased, RecordValidatedStopAndOutputs, PlanPublication,
RecordPublication, PlanCleanup, MarkUncertain and Retire. Each consumes the same opaque
live coordinator/entry handle plus its exact expected sequence, returns an explicit
Applied/AlreadyExact/Blocked/CommitPending outcome, and enforces the closed transition
table above. Generic exact-byte CAS remains private to storage and cannot choose a
phase or bypass validation. Cold observations and arbitrary decoded snapshots cannot
be passed as live entry handles or stop/retirement witnesses.

R1 implements initialization, Prepared/LaunchIntent/Uncertain, exact live prelaunch
abort, strict serialization/cold interpretation and private atomic storage. Started-run
stop/publication/retirement operations stay unavailable until R2 connects sealed
witnesses from the original `GmWorkerExecutionAuthority`; never add a production
"trusted=true" or arbitrary evidence callback. R1 may read seeded valid terminal
fixtures to test syntax/admission policy, but cannot mint real started-run retirement.
The writer rejects Uncertain->Retired, cold handle reuse, changed identities, epoch
regression/overflow and archival/removal without its required phase witness.
Terminal candidate bytes are separate from committed state; exact archive retry cannot
turn an active entry into a terminal one. R1's archive mutation proof is limited to
its live no-launch abort path; actual stopped-run retirement is R2's responsibility.
Unsupported platform durability returns Blocked before publication. Cancellation after
mutation starts retains/settles original I/O before reporting its result.

- [x] Add a coherent `worker-run-ledger` category and explicit selection for this slice.
  Write cold tests for Prepared/ReleaseIntent/Published/CleanupPending/Uncertain;
  each blocks, preserves exact evidence and cannot return a success permit.
- [x] Add pure and real-file tests: wrong root/generation/epoch, malformed/duplicate/
  oversize JSON, epoch overflow, two-process same-root contender, exact CAS conflict,
  missing initialized state, env workspace-base change, nonordinary paths, cancellation
  around publication, exact terminal archive retry/conflict and descriptor inheritance.
- [x] Add finite child-process abrupt-exit cuts before/after temp flush, rename and
  parent sync; fresh child sees the exact old or new snapshot or explicit Blocked,
  never false Missing/retired. Preserve outside sentinels. Injected syscall failures
  test refusal paths; they are not a power-loss qualification.
- [x] Publish scaffold and PlanOnly; distinguish build preparation failure from causal
  assertions. Minimal implementation then focused GREEN through `scripts/test-csharp.ps1`.
  No pool Release, native descendants, main-record90 or old passing cohorts in R1.
- [x] Verify catalog discovery, exact source/commands/counts/cleanup, separate
  Sol6.1/xhigh review and GitHub restoration. Handoff as a prerequisite only.

Qualification: independent actual gpt-6.1-sol/xhigh R1 PASS at `c7e801f1`;
[bounded source-specific evidence](recovery/worker-ledger-r1.json). The later R2 policy
choice below does not enlarge R1's qualification.

### R2 — atomic connection of the complete synthetic lifecycle

Authorized from accepted R1 and the owner-selected root-wide policy below. Connect durable admission,
pre-recovery checks, launch/Release/publication intent and terminal retirement as one
bounded implementation. Never enable durable pool admission while later lifecycle
stages still use the unfenced B route. Default public Linux admission stays closed.
Files: pool, native fixture admission, workspace preallocation, OwnedLaunch/native
RunId binding, ProcessHost, ExecutionAuthority, real Store and QuarantinedExecution;
minimal injected FileSystemManager pre-recovery/held-lease predicates. Do not broaden
Windows Job semantics or claim native Windows execution. Test entrypoint:
`BookOfEternityClient.Tests/GmWorkerRestartAdmissionTests.cs` plus finite TestSupport
restart driver under the existing independent guardian.

- [ ] First causal RED: preserve a nonterminal intent from an abruptly stopped first
  app; a fresh real pool must not allocate fresh capacity, perform recovery writes,
  send Release or accept the saved result. Separate preparation errors from the RED.
- [ ] Thread the same sealed entry handle through preparation, original owner binding,
  one Release, Store and cleanup. Connect every typed operation/fence listed above
  before lifting the internal scaffold refusal. Preallocate identity before Start.
- [ ] One positive actual-pool happy/content result must stop and settle outputs,
  publish under the current generation, finish conditional cleanup, commit terminal
  state and release capacity exactly once. Fresh restart may admit a NEW task after
  this fully committed terminal record, never replay the old task identity.
- [ ] Prove the retained owner lock blocks a second process and a queued old-generation
  Release refuses under the original canonical lease. Parallel workers with different
  epochs remain valid; new high-water alone must not invalidate an earlier live entry.
- [ ] Focused GREEN and independent review of this complete connected path precede R3.
  No intermediate build is qualified as restart-safe merely because metadata exists.

### R3 — gradual crash/restart and retirement qualification

Depends on positive R2 cleanup. Add narrow `GmWorkerRestartFenceTests` and only required
negative/observation hooks. Change runtime only to fix causal defects found by this
matrix; do not introduce reconnect or new cold cleanup authority.

- [ ] Extend one bounded cohort at a time: crash after Prepared, helper bind, Release
  intent/write/ack, Completed, scoped stop and output settlement. Fresh process blocks
  every nonterminal case and sends zero repeated Release; guardian proves independent
  physical cleanup without clearing ledger uncertainty.
- [ ] Cut before/after bundle move, derived inbox/audit, terminal candidate archive,
  exact commit/ack and capacity release. Preserve committed canonical bytes, frozen
  terminal bytes/conditional audit requirements and uncertain workspace. Publication
  commit ambiguity grants no success; retirement-only ambiguity preserves an already
  acknowledged live publication permit with CleanupDeferred. Cold active/pending
  inventory retains capacity despite an archive; consistent fully committed retirement
  may admit a new task without recreating a result permit. No duplicate import or
  tombstone mutation on retry.
- [ ] Race generation replacement with queued Release and Store publication. Prove
  all unbound/recovery entrypoints block before mutation, and already-held leases
  recheck the same binding after awaits. Old epoch/coordinator/same-ID changed body
  cannot borrow a live permit; existing original journal recovery conflicts still block.
- [ ] Crash with active parallel workers, valid and corrupt inventory, changed profile
  limits, changed runtime base and lost owner lock. Preserve all reservations; no
  per-root inventory claims to replace the existing process-wide reaper capacity bound.
- [ ] Qualify selected B uncertainty/receipt/consumer regressions only when changed.
  Do not replay all127, main-record90 or unrelated passing cohorts by default.
- [ ] Preserve exact source/commands/counts/cleanup, catalog discovery, separate
  Sol6.1/xhigh final review, non-force publication/readback and fresh GitHub restore.
  No public rollout, reboot-based clear or autonomous salvage follows R3.

## Product decisions versus technical choices

No new product decision blocks R1, which is inert outside synthetic tests.
On 2026-10-06 the owner explicitly chose the recommended R2 policy, relayed by the
parent with the accepted R1 SHA: **block new synthetic pool dispatch for that root
when an unresolved worker remains**, while preserving per-worker/quarantine
reservation counts and diagnostic access. This is not a ban on legitimate cleanup
and does not relax absorbing Uncertain. Allowing
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

Source inspection only, zero tests/native probes. [Design evidence](recovery/worker-restart-design.json)
records exact input hashes, the independent verdict and a fresh GitHub-only candidate
restoration of all7,043 tracked files. Only five Spec Kit documents changed from the
accepted B source before this evidence/status carrier. Self-check: main/worker identities
remain separate; all uncertainty paths retain evidence; no recorded observation grants
live authority; source bootstrap cannot recover before admission; lock hierarchy is
acyclic; every side-effect cut has an owned crash test in R1–R3. Keep current catalog
and CI selection unchanged until an implementation slice adds actual tests. Source
hashes, independent design verdict and fresh GitHub restoration go in the existing
plan checkpoint. Parent separately authorizes any implementation after reading this
plan and the specific R1 handoff.
