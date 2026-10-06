# T041-RUN-FENCE: original main owner and participating writes

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Base: accepted `14888663d608355098cc1a329d97ab61de0e6016` on
`codex/1553-load-filesystem`. Design only; implementation is not authorized by this
checkpoint. US4 / FR-009/012/013/014/015; full SC-004 remains open.

## Purpose and decisions

Before a normal game-writing CLI can execute, bind the existing persistent-main
record to the **original terminal owner**, publish transitions durably, and enforce
the same admission at actual recovery and held-lease writes. Preserve the selected
root-wide worker refusal and its narrower original cleanup purposes independently.
This coordinates cooperating game components; it does not protect saves from their
player, constrain arbitrary filesystem writes by an external CLI, or add a service.

Recommended: consume schema1 `GmSessionRunRecord`, transitions and slot condition,
with one persistence adapter/coordinator and the existing filesystem gates. A new
combined main/worker event journal duplicates accepted contracts and is rejected.
A status-file/PID-only startup check is smaller but misses recovery, held leases,
old input and independent workers, so it cannot implement this requirement.

Reuse accepted [main record](recovery/gm-run-record-qualification.json),
[R1](recovery/worker-ledger-r1.json), [R2](recovery/worker-restart-r2.json),
[R3](recovery/worker-restart-r3.json),
[input](recovery/gm-input-transaction-qualification.json), and
[terminal](recovery/owned-main-terminal-neutral.json) evidence. No unchanged audit,
test, native probe, runtime edit or catalog edit is performed for this design.

## Source map: missing connections at the accepted base

Paths below are repository-relative; exact source blobs/hashes and reused carriers
are recorded in [checkpoint](recovery/main-run-fence-design.json). Ranges identify
integration boundaries, not a new whole-file correctness audit.

| Boundary | Source and present behavior | Required connection |
| --- | --- | --- |
| Main slot | `BookOfEternityClient/Services/GmRuntime/GmSessionRunRecord.cs:5–18,82–122`, `GmSessionRunAdmission.cs:10–55`, `GmSessionRunRecordCodec.cs:26–74` | Pure schema1/64KiB/cold interpretation; no owner, persistence or production fence. Retain these contracts and accepted90 tests. |
| Terminal launch | `Services/GmWorkers/NativeLineageOwner.cs:113–165` under client; `BookOfEternityGMBridge/ConPtySession.cs:32–69` | Native A1 ACK and Windows ResumeThread currently occur inside Start. Split held preparation from single release; preserve original pidfd/master or suspended process/Job. The PID callback is not that authority. |
| Real bridge | `BookOfEternityGMBridge/Program.cs:170–183,387–448,448–611`; `BridgeHost.PromptDispatch.cs` | Constructor/control/status writes precede durable admission; StartShell stops then starts, Attach installs actual pumps/T042, Stop clears after actual disposal. Bind these existing consumers, defer participating canonical writes until admitted, and retain the coordinator until terminal publication ACK. |
| Root and recovery | `BookOfEternityClient/Core/CanonicalRootIdentity.cs`; `Core/FileSystemManager.cs:577–596,3628–3725,3870–3895` | Constructor itself is inert. Canonical lease validates original lock then runs recovery before return. Install a main condition after lock validation **before recovery**, plus held-lease checks; a nullable same-process registration alone is insufficient across processes. |
| Recovery and publication | `Core/FileSystemManager.TrustedLocalStorage.cs:14–21,59–68,173–224`; `FileSystemManager.cs:4183–4208,4774–4794`; `Core/FileSystemManager.ImagePublication.cs`; `Core/FileSystemManager.BackupLifecycle.cs` | Recovery may roll back, promote or clean evidence; publication awaits/retries and existing backup paths reuse leases. Compose main with existing worker and generation gates at every final mutation/recovery boundary. |
| Replacement / clear / load | `Core/FileSystemManager.SessionReplacement.cs:24–87`, `LoadReplacement.cs:26–93`; `FileSystemManager.cs:5876–5888`; `Services/SaveLoadService.Loading.cs:176–224` | Lifecycle → replacement lease; generation and members share existing publication. Load callback is **after** acquisition/recovery, hence cannot be the sole gate. Keep typed load outcomes. Main tombstone is not part of the replaceable image. |
| Live rollback / saves | `Core/GameEngine/GameEngine.TurnLifecycle.cs:332–339`; `GameEngine.SessionAndSnapshots.cs`; `IO/StateDistributor.cs:638–667`; `Services/RealmSegregationAutoRollbackService.cs:199–234`; `SaveLoadService.Creation.cs:251–255,351–365`; `Core/SessionOperationContext.cs:145–163` | Actual restore/delete/report/save/finalization writes use filesystem leases, sometimes an already-held lease. Classify active-run repair separately from quiescent replacement; a pending-turn file is not live owner proof. |
| Worker composition | `Services/GmWorkers/GmWorkerRootContext.cs:89–135`; `Core/FileSystemManager.WorkerCanonicalFence.cs`; `Core/WorkerRunLedgerPersistence.cs:9–13`; `GmWorkerBridgePool.cs:388–425` | R1–R3 inventory/pins/metadata debt and narrow dispatch/cleanup are separate. Public Linux Release remains closed. Main Stopped never removes worker reservations or grants a worker permit. |
| Other processes | `BookOfEternityClient/Launcher/bookofeternity.ps1:306–401,642`; `game_master_daemon.ps1:3716,3807–3830,5867–6031,6523–6586`; `Core/GameEngine/GameEngine.MainMenu.cs:3295–3330` | Status discovers an endpoint, not authority. Real launcher/daemon clients and raw script control writes/deletions must participate before production. T042 keeps its original operation body and ambiguous-outcome behavior. |
| Actual browser Load | `BookOfEternityClient/WebUi/LocalWebUiMainMenuService.cs:55–81` → `BrowserLocalWriteCoordinator.cs:76–136` | The first UI-guard acquisition can recover/write **before** Load; refused/rolled-back guard release takes another recovery lease. Hold quiescent root admission through both, actual Load, and post-load bound menu refresh/finalization. `BrowserLoadStateService` is reconciliation, not this entry. |

## One record, one original authority

Use the already planned `<BasePath>/.boe_runtime/gm-runs/main.json`; no second
history log, worker entry, receipt authority or new state envelope. Add stable
non-inherited `<BasePath>/.boe_runtime/locks/gm-main-owner.lock`, outside the main
initialization namespace. It is never renamed or deleted. The original coordinator
holds its exclusive handle through owner lifetime and metadata debt. An unbound
quiescent consumer must acquire the same original exclusive guard **before**
lifecycle/canonical locks and retain it through its whole operation; probing then
unlocking is insufficient. Visible Stopped bytes do not grant quiescence while the
publishing owner retains an unacknowledged flush/barrier/readback. Same-owner
quiescent access requires its recorded durable ACK, not merely expected bytes.
The existing canonical lock serializes the single-record CAS; a short
in-process coordinator gate freezes transitions, not another durable journal.
Standalone bounded ordinary-file I/O reuses trusted-local scope/publication
primitives **below canonical recovery**; main metadata must never recursively call
the canonical publisher. Publish frozen exact bytes through same-directory staging,
file flush, atomic replacement and the platform directory durability barrier;
acknowledge only after exact readback and required barriers. A platform without a
qualified adapter refuses admission; this is application-process crash ordering,
not power-loss/reboot salvage. Metadata retry repeats only that same frozen write,
never launch, input, stop signal or an unknown command.

Missing means a verified absent entire `gm-runs` namespace under the original guard
and canonical lease. Only its original successful creator may bootstrap epoch1.
The external stable lock alone is not an initialized main namespace. Once any
`gm-runs` namespace evidence exists, missing/truncated/unreadable `main.json` is blocked,
including an initialization crash. The same live creator can retry its exact
pending bytes; a cold reader cannot guess freshness. A valid Stopped tombstone is
retained forever in this stage; next run uses epoch+1/new RunId, overflow blocks.
No generation replacement, clear, rollback or Load deletes or resets it.

Root comes from resolved `FileSystemManager.BasePath`, generation from its actual
held snapshot, fresh host/run IDs from the original coordinator, BootId from a
trusted platform startup reader (failure refuses, no guessed reboot evidence).
For Linux the planned reader is bounded `/proc/sys/kernel/random/boot_id`;
Windows identity/durability execution remains a later native qualification.
`LinuxSupervisor` remains the schema1 platform family, **not** a systemd guarantee.
Held factories consume the coordinator-allocated RunId; native bootstrap and
terminal stop/status retain that same run, not a second internally generated ID.
Live binding also retains exact `TerminalIdentity` backend/scope and the actual
session object; neither is reconstructed from JSON. Systemd adapts this binding
in its separate required backend stage; no manager is installed here.

Only coordinator-created internal capabilities may call transitions or grant
ActiveRunMutation: bind exact root/run/generation/epoch/host/boot, selected backend,
original session and the specific operation/pin. No caller-supplied run fields,
decoded Running/Stopped record, worker stop, terminal text, PID, root exit or EOF
creates a capability or resumes an old CLI. Cold nonterminal records are observed
as Uncertain without rewriting them; cold Stopped allows only quiescent admission,
subject to workers/storage. No reconnect, reboot transition, force-clear or
automatic command retry is implemented.

## Launch, stop, restart and lock order

The lifetime main-owner guard is acquired before coordinator use and retained
through any uncertainty or metadata debt. It is not acquired opportunistically
under a held canonical lease. Routine order: obtain the original active operation
pin **or** the original quiescent guard before filesystem locks; when needed take
session-lifecycle → canonical-write → short
main transition gate. Existing worker journal/purpose checks stay in their accepted
order below the canonical lease; no worker path waits for a main lock while holding
its journal. Release short gates/canonical lease before supervisor or bridge IPC,
pipe waits, child waits, terminal disposal or pin-drain waits. Lifecycle may span
owned start/stop to serialize replacement, but no pin is awaited while holding it.
Thread the original guard through lifecycle-first APIs and ambient/nested helpers;
never reacquire it while holding lifecycle/canonical. Browser replacement retains
one guard through initial UI lock, Load, generation-bound refresh/finalization and
exact refused/rolled-back UI lock release. It must not unlock between these helpers.

1. **Launch:** close input/automatic readiness; acquire original main coordinator;
   under lifecycle+canonical compose quiescent main, independent worker admission,
   unresolved storage and current generation. Perform admitted existing recovery
   and freeze identity, then durably publish Prepared **before helper/process creation**.
   Release canonical/short gate, retain lifecycle; prepare the original held terminal
   without A1/ResumeThread. Re-enter a narrowly typed **metadata-only/no-recovery**
   canonical lease, recheck exact generation/Prepared bytes and original binding,
   durably publish Running. Drop canonical/short gate; consume one internal release
   capability for A1/ResumeThread, never retry it. Attach actual InputLifetime/output/
   T042 to that original session; only after release is confirmed may live pins and
   input open. Loss/partial start retains original cleanup authority and closes admission.
   Revalidate original held state/AuthorityLost immediately before release, including
   after exact metadata retry: `native/linux/boe-lineage-supervisor.c:228–229,321`
   expires held bootstrap after5000ms. A late Running ACK cannot revive that owner.
2. **Stop:** close new pins and revoke the exact T042/input binding immediately.
   Under a short metadata-only canonical lease publish Stopping (or Uncertain
   if already uncertain) **before pin drain**; the original main-owner guard and
   coordinator serialize this initial transition, without taking lifecycle yet.
   This durable publication is the cross-process ordinary-write revocation point.
   An earlier held lease may settle its current decision before that boundary;
   delayed pipe notifications cannot authorize a later lease after it.
   Release canonical/short gate, drain admitted pins, then take lifecycle for
   original terminal retirement. Wait for neither pins nor IPC under filesystem
   locks. Unresolved record publication/drain blocks retirement; independent
   original scope cleanup can still run without any canonical authority.
   Stop via the same session,
   join actual input/output/root/resize tasks and terminal disposal. TerminalStopEvidence
   alone is insufficient: require exact identity, scoped empty, complete cleanup,
   no retained/lost authority and no late I/O/disposal uncertainty. Then under the
   retained lifecycle+metadata-only lease publish Stopped with sealed original proof.
   Only its durable ACK allows coordinator/binding retirement. If metadata alone
   is pending after handles closed, retain lock, proof and logical owner; retry exact
   publication only. Explicit terminal/owner uncertainty stays absorbing; finishing
   cleanup never upgrades it to Stopped. No canonical mutation bypass for this cleanup.
3. **Restart:** exact durable Stopped and settled old input/pins/I/O precede a fresh
   epoch/run, current generation and new owner. Main.json is never deleted. Queued
   old T042 operations are retired, never redirected to new input. NotLoaded or
   RolledBack Load does not resurrect a stopped old CLI; Committed Load rebinds only
   its established generation and may later start a fresh run. Uncertain or blocked
   follow-up prevents continuation. No restart when durable stop is ambiguous.

The metadata-only lease can read the exact generation/main record and publish only
main metadata; it cannot recover, mutate canonical members, alter generation or
borrow a worker cleanup purpose. It can record this main owner's stop even when a
separate worker is unresolved; that never makes the overall root quiescent.
An original-pin **closing lease** after persisted Stopping is distinct: bounded
no-recovery read of its exact root/run/generation for SessionOperationContext closing,
not an ActiveRunMutation grant. It cannot perform new writes, generic recovery or
invent/replace generation. Settle an already-linearized storage decision under its
original still-held lease before the revocation boundary; preserve Committed,
RolledBack, Uncertain and cleanup-debt classifications. After lease disposal, closing
reports that result/failure without repeating effects. Unresolved storage debt stays
an independent root condition. Local cancellation must not abort required settlement
and manufacture a pin that can never close; unknown completion still refuses drain.

## Actual mutation admission, including separate client processes

All filesystem instances probe the fixed main namespace on lease acquisition and
at participating held-lease boundaries, whether or not an in-process context exists.
Share original main context through `CanonicalRootIdentity` in one process, retain
it with a lease pin, and reject incompatible rebinding. Initial unbound access on a
verified absent namespace follows current game behavior; existing nonterminal
evidence requires original live authority. The conjunction is **main condition AND
existing worker-purpose/inventory condition AND generation/storage condition**.

| Operation | Required main condition and preserved behavior |
| --- | --- |
| Normal turn, accepted publication, in-generation rollback/repair, save snapshot/retention, bound finalization | Running + original active operation capability; current generation and exact held lease at each writer. A copied identity is insufficient. Existing rollback/receipt/storage decision rules continue; no new snapshots or log. |
| Replacement, clear, selected Load, generation-changing recovery | Quiescent main + selected worker condition + lifecycle/replacement lease. Refuse while old main is active; optional UI stop must occur first outside filesystem locks. Gate before acquisition recovery, before live capture and at actual publication/delete boundary. |
| Recovery without generation change | May run under the same original Running capability and generation; otherwise quiescent. Inspect retained intent without writing first. Unknown or generation-changing evidence requires quiescence; block before its first mutation, not after a partly recovered tree. |
| Worker task/audit/publication | Main condition additionally applies, without broadening existing exact worker purposes or recreating accepted permits. Worker state still owns its reservation/slot. Original worker process/workspace cleanup remains independently possible without canonical writes. |
| Diagnostics | Strict bounded no-recovery reader and live volatile pipe status. Existing `ReadFileAsync`, save-list and status helpers that can acquire a recovery lease are not automatically diagnostic. Stale canonical status never grants authority. |

For real separate clients, extend the **existing real pipe loop** with a typed,
bounded begin/finish operation-pin exchange. The live owner mints an opaque pin on
an original control connection; capability factories are internal and bind the
returned full identity plus original connection. Request fields and a status-file
pipe name alone do not mint it. Acquire this pin before filesystem locks; no RPC
while holding a canonical/worker journal lock. The client checks exact persisted
Running bytes and generation at pre-recovery and held writes; notifications are
only an acceleration of closure, not atomic cross-process permission. The pin
spans publication **and SessionOperationContext finalization**. Stop first commits
Stopping under the canonical lock, so a later ordinary lease refuses even with
delayed notification. The narrow closing lease above lets the original pin finish.

F2 must implement a bounded typed state machine: owner PreparedGrant → Active →
Closing → ClosedObserved, with Unresolved on lost authority before observed close.
Client Active → Closing → **irreversibly locally closed** after operation,
finalization and every lease disposal, then sends one immutable terminal close frame
on the original connection (pin/run identity, typed outcome/closing failure). Owner
retires the pin only on receipt/validation of that exact frame, not on WriteAsync
success or sending its reply. Loss **before owner receipt** retains Unresolved and
blocks drain; loss **after receipt** leaves client continuation failure while owner
already knows no more effects are possible. Querying that original close identity
may report existing state; it never repeats the game operation or revives the local
capability. There is no mutually acknowledged last message or cold exactly-once
claim. Bound concurrent pins at32 per root, one active per original connection;
unknown/evicted status refuses continuation. This is a controlled technical budget,
not final live-client UX. PID/EOF/client exit never substitutes for the close frame.

Revocation of ordinary writes linearizes at the persisted transition; an earlier
held lease settles its decision before it. Stop cannot claim quiescence until every
original pin is ClosedObserved. These
pins coordinate cooperating callers, not hostile clients. Daemon terminal input
uses its actual T042 dispatch binding; daemon canonical control writes/deletions
must use the same participating C# mutation adapter/pin, not add a PowerShell lock
or infer permission from gm_bridge_status.json. Defer bridge canonical status writes
until admitted; on Uncertain expose volatile diagnostics and keep old status merely
as discovery. This connection is a separate slice, not a neutral-test substitute.

## Smallest implementation sequence; no implementation in this checkpoint

> For agentic workers: use Superpowers executing-plans/TDD after explicit slice
> authorization; read this design and the existing plan/tasks. Sole writer and
> independent actual Sol6.1/xhigh review; no Astra required.

**F1 — connected neutral durable owner/fence (first implementable slice).**
Modify `NativeLineageOwner` terminal-only preparation/release, `ConPtySession` source
adapter, existing BridgeHost start/input/stop and root/lease boundaries. Add internal
`GmSessionRunPersistence`, `GmSessionRunCoordinator` and `FileSystemManager.MainRunFence.cs`,
consuming schema1, exact source transitions and actual original terminal, not a new
ledger. Isolated fixture root must contain its own `game_session` under fresh BasePath;
current neutral scratch must never derive a canonical root from shared `/tmp`/parent.
Keep that originally admitted fixture root across tested epochs; only its internal
fixture admission can mint a fresh single-use launch capability for the same fixed
CLI, rather than reusing a consumed launch or accepting arbitrary paths/commands.
Use actual FileSystemManager APIs in same-process controlled consumers and a separate
unbound child reader to prove cold refusal; public launch remains closed.

User-observable result: the same neutral process stays interactive, its unresolved
run blocks cooperating clear/load/recovery, and only confirmed durable stop permits
replacement or a fresh run. No usable game-writing launch is exposed.

- [ ] Add focused causal RED for current release-before-record and recovery-before-
      admission/held-lease write behavior, using real neutral owner and controlled
      storage; preparation failure counts separately. Introduce only coherent new
      owner/persistence and mutation-fence category owners structurally.
- [ ] GREEN proves Prepared before actual process creation, Running before exact
      A1/Resume boundary, original capability (forged/copied identity denied), two
      inputs same process, input retirement, stop+actual I/O/disposal+Stopped ACK,
      epoch advancement, expired held-bootstrap refusal after late metadata ACK,
      visible-Stopped/pending-ACK refusal and absorbing uncertainty.
- [ ] Through real lease/writers prove refusal before recovery side effects,
      held-lease revocation before a new mutation without aborting its already
      linearized storage decision, generation-changing versus
      in-generation recovery, clear/load/rollback/save boundaries, preserved tombstone,
      separate worker refusal and original narrow cleanup purpose. Fault frozen
      metadata writes/readback, release ACK, late I/O and scoped stop; own guardian
      must reach ECHILD even when the application correctly retains Uncertain.
- [ ] Run only new categories and specifically affected old methods via
      `scripts/test-csharp.ps1`; source-select root/lease/load/rollback and terminal
      regressions, not full90 record/R1–R3/input cohorts. Independent source/evidence
      review, normal checkpoint pushes/readback and fresh GitHub-only restore.

**F2 — real client/daemon pins and mutation consumers.** Consumes F1 coordinator
and no-recovery filesystem gate. Implement actual pipe begin/finish/revocation,
retained unresolved pins, C# client binding through SessionOperationContext and
real launcher/daemon/QTE/repair/status consumers. RED→GREEN: stop races a held
client lease/delayed notification, loss before and after observed close, stale
run/epoch/generation, no-recovery finalization/rollback, both UI Load entrypoints
including browser guard acquisition/release/refresh, and raw daemon writes.
Inert real PowerShell functions,
controlled processes/screens; no live provider. Same original T042 dispatcher.

**F3 — process-crash and replacement qualification.** Consumes F1/F2 connected
path. Sequential bounded crash points before/after Prepared, held creation,
Running/release, mutation-pin completion, scoped stop/disposal and Stopped ACK.
New process reads exact main record nonmutatingly; nonterminal is cold Uncertain,
Stopped admits only fresh run and worker/storage conjunction. No old input replay,
salvage, reboot clear or unknown command retry. Test exact canonical bytes and
typed Committed/RolledBack/Uncertain load/rollback outcomes with isolated fixtures.

Primary existing-manager **systemd terminal adapter/qualification remains a separate
required backend stage**; no manager here, no mock-based positive claim. Production
main admission + ordinary launcher/live CLI acceptance follow F1–F3 and qualification
of the selected actual backend. Enabled production workers require their own later
admission; default disabled helpers do not block a no-worker GM path. Native Windows
execution, arbitrary VT/TUI, real saves and cold guarantees remain unqualified.

## Product questions and checkpoint gate

No new product decision blocks F1: root-wide cold refusal, original scoped evidence,
preserved drafts and no ambiguous replay are already chosen. Backend mapping,
single-record CAS, pins and narrow metadata lease are technical choices to review.
Later live UX must decide whether a user-requested Load/restart offers to stop the
current CLI and start a fresh one, or requests explicit stop/start actions. Either
keeps the same low-level refusal and never resumes the old run. This decision does
not authorize automatic maintenance, CLI command replay or additional save security.

Publish this design with aligned spec/plan/tasks/status and exact source manifest;
obtain independent Sol6.1/xhigh review and remote byte readback. Handoff **before F1**.
GM prompts/examples need no change now: this is a client-owned planning contract,
with no gameplay or operational runtime change. Connected execution must synchronize
relevant operational documentation/source guards when its behavior changes.
