# Linux inherited-channel stand (WIP)

Base: `fa5d5ec8a20851f6b7401e5e7b27b455c9deb9d7`, branch
`1553-linux-relay-fd-stand`. User authorized a separate isolated launch stand,
then a dedicated issue and full Linux relay implementation if feasible.

This block tests kernel primitives only, not game acceptance or production
NativeLineage admission. The existing supervisor legacy exit and double-fork
fixtures completed with guardian ECHILD and no emergency actions. Ordinary
production terminal launch still fails at AF_UNIX creation. Its separate bridge
NamedPipe listener also needs a legitimate connection design.

Design: an exclusively retained, unreaped direct-child helper owns a gated root,
its pidfd and PTY. Private inherited pipes carry bounded descriptor locators.
The original helper pidfd plus pidfd_getfd duplicates the actual root pidfd and
PTY open file descriptions; numeric locators alone never authorize release.
No ptrace, dumpability, seccomp, capabilities or other security setting changes.
If transfer fails, do not release the root; close its gate and reap it.

Before production adaptation, prove an equally race-free original helper handle
in the managed/native launch boundary. Python's exclusive fork/wait stand does
not prove .NET Process ownership. Preserve native cleanup, partial ownership,
hold deadlines, PTY lifecycle and bridge per-connection pins in later work.

Checks planned: real root pidfd and bidirectional PTY transfer, PTY dimensions,
held-child liveness, rejection of invalid descriptor and injected EPERM/ENOSYS,
no payload execution on refusal, root and helper reaping, ECHILD. Platform
coverage is this machine only; other kernels, Yama/LSM and seccomp policies may
refuse pidfd_getfd. Such refusal must remain a supported fail-closed outcome.

Performance: no cache added. Each test must own a fresh helper/root/PTY;
process authority cannot be reused. Small immutable protocol data is read once.
No game builds or broad test suites are needed for this primitive stand.

Status: source preparation, not yet run/reviewed. No production source modified.

Initial runtime: real transfer/PTY exchange passed on Linux 6.18.57 x86_64;
real EBADF and injected EPERM/ENOSYS refused release and reaped the root/helper.
Independent review then found an unsafe test-harness child exception cleanup
(`kill(0)`), missing acquisition cleanup, non-structured type refusal and a
partial-read assumption. These are corrected in source; new wrong-type,
helper-pidfd-denial and pre-setsid child-failure controls await rerun/re-review.
No production path was changed or qualified. Initial happy-path success does
not qualify the corrected source or its failure paths.

## Accepted primitive block and next native block (#1589)

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1589
Corrected stand ran seven cases successfully. Real transfer, bad descriptor and
wrong descriptor type are distinct from injected EPERM/ENOSYS, helper acquisition
failure and pre-setsid root exception. Root exits were respectively 0, 125 on
refusal, and 126 for injected child exception; helper/root cleanup passed.
Independent source/outcome review accepted this bounded primitive block only.
No group signal or numeric-PID signal after reaping remains in the harness.

Next design: `--terminal-pipe-v1` uses original stdin/stdout. Capture candidate
supervisor pidfd, THEN generate a fresh random challenge; only its exact response
from the original private status channel admits the candidate, before any fork.
Pipe inode equality alone is NOT an identity proof. Candidate failure is handled
by closing original control and joining original Process, never signaling an
unadmitted candidate. One dispatcher consumes both bounded bootstrap and status
frames. Root export pidfd is separately duplicated/pinned until bootstrap ends,
including early root death. Expiry/stop/cancel precede release.

Native initial RED against unchanged helper: it emitted legacy Ready instead of
required authenticated bootstrap. Current narrow native implementation compiles
with the official script; final edit checking expiry before commands is not yet
rebuilt/run. Native happy/cancel tests are prepared, other phase/negative controls
and independent native review remain pending. Managed and bridge paths unchanged.
No live relay or game acceptance is claimed.

Native source at `64147efd75efd97198147aea2bb55433dcecf226` passed the first
happy/cancel-held pair through the actual compiled helper: terminal exchange
and scoped retirement, or cancellation before execution with root exit125.
Review found pre-authentication stop/cancel was consumed as malformed challenge;
current source moves S/C/U handling before the challenge parser. Fourteen native
cases now cover fragmented/invalid/oversized/replayed challenge, early launch,
owner EOF, both deadlines, held-root death, duplicate launch and cancellation.
Current expanded source/test delta is UNRUN and awaiting independent review.
Further review correction: native harness now closes local descriptors/streams
in an inner finally even if close/wait fails; unresolved owned cleanup is an
explicit error, never an emergency helper kill or PASS. Eighteen planned cases
also include premature/duplicate release, late A and EOF while held. Duplicate A
cannot undo an already legitimate first release; its expected result is uncertain
scoped cleanup, not a guarantee the first permitted payload never executed.

Outcome review did NOT accept the first 18-case result: its kill-held trace
contained Started although no A was sent. Marker absence alone missed this.
A new no-Started-before-release assertion produced causal RED against that binary.
Current narrow fix suppresses Started for terminal-pipe mode unless bootstrap
phase4 (release) was reached. Root-death cleanup remains unchanged. Corrected
source/test is UNRUN. The earlier 18 expected exits are not a native acceptance.

## Native bounded acceptance

Runtime source `04e55596f64df902a5233eb3877af992dea4e232`: all eighteen
corrected native cases passed. In particular kill-held now emits Ready, Stopping,
StoppedWithinScope, never Started. Official native compile passed with warnings
as errors. C SHA256 `e05ec9d94ebda400c7ede8b7442660a279888a7c2092c7cf99d045b77641d896`
matched build provenance and the fetched Git blob. Legacy exit/double-fork were
rerun against that binary: guardian ECHILD, no emergency actions; 0.022/0.730s.
Independent source/outcome review accepted the bounded native block after
checking the causal RED and final source/evidence join. It did not rerun runtime.

Positive old socket bootstrap remains unqualified in this environment; its
actual EPERM path still returns version2 Uncertain with cleanupComplete and no
root launch. No security policy was changed. No authority cache was introduced.

Next gate: managed candidate pidfd capture followed by fresh nonce on original
exclusive Process channels, strict one-reader dispatch and handle transfer,
actual owned-terminal lifecycle/cleanup tests. Then bridge per-peer transport
for independent CLI clients. Bridge currently has no exact-PID peer credential
check; do not confuse its live connection pins with worker socket PID authority.
Real launcher/maintained relay/live GM acceptance and cleanup remain required.

Managed test-first gate prepared: a narrow category reuses the existing real
owned-terminal/independent-guardian journey (Unicode input, resize, canonical
EOF and scoped cleanup), additionally requiring the inherited transport was
actually used. The current capability observation is false and transport remains
unwired; expected RED is the still-blocked socket launch, not a feature PASS.
Fresh unit/bridge/test-support restore succeeded from shared offline package
DATA. No bin/obj or ownership objects were copied. Build/run pending.

## Managed implementation draft (separate checkout)

The original relay-fd checkout remains frozen for the pending test-first save.
Implementation continues separately in relay-managed, derived from saved native
source plus those same test-first files; no competing remote ref write is made.
Managed RED was 1/1 SocketException Permission denied, official runner 12.991s,
independent guardian ECHILD with no emergency signals. No payload was launched.

Draft uses creation-only EPERM/EACCES fallback before Process.Start, retained
supervisor pidfd, post-capture nonce, strict bootstrap/status phases and actual
root/PTY transfer. PTY uses master-specific TIOCGPTN, not isatty alone. Abort is
async and bounded: failure retains uncertainty and prevents disposal; successful
release keeps stdin for scoped stop. The original pidfd is retained to disposal.
Finite workers and bridge are unchanged. Draft is UNCOMPILED/UNRUN.

Channel premise was checked against upstream .NET8
[Process.Unix.cs](https://raw.githubusercontent.com/dotnet/runtime/v8.0.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Unix.cs)
and [pal_process.c](https://raw.githubusercontent.com/dotnet/runtime/v8.0.0/src/native/libs/System.Native/pal_process.c):
redirects use CLOEXEC pipes and the parent closes child-side copies. The checked
native helper does not fork before L. This is an ownership construction for the
trusted launch, not authentication against hostile same-UID/same-process code.
Actual managed lifecycle and adversarial observation tests remain required.

Early independent managed review found unbounded L/A control-gate waits. Draft
now has an inherited-only linked cancellation/deadline path covering gate,
write and flush, with authority/phase/deadline checked again after admission to
the gate. Failure retains uncertainty and never retries the command. Four
new guarded-process probes hold the gate with an actual held original root:
L/A cancellation and expiry must finish before gate release, leave the held
phase unchanged and never execute the payload; original scoped cleanup and
outer ECHILD remain required. The L probe is blocked-writer coverage, not a
second lawful native launch. These new tests are UNCOMPILED/UNRUN, and no
causal boundedness RED/GREEN is claimed yet. Abort-timeout/retry and descriptor
transfer rejection coverage remain outstanding.

Second independent early review caught StreamWriter cancellation buffering:
closing that writer could flush an unconfirmed A after refusal. Inherited mode
now exclusively writes original StandardInput.BaseStream bytes, including
challenge, launch, release and stop; abort disposes that unbuffered pipe without
writer flush. Old finite/socket routes retain their original writer API. The
review also rejected new reflection probes under AGENTS; draft replaces them
with an explicit typed gate lease that cannot mutate phase/deadlines, plus typed
command/capability observations. Expiry probes now wait the real held deadline,
not a shortened reflected field. Source review and a deterministic interrupted
write/abort control remain pending; no runtime success is claimed.

## First managed inherited run, 2026-11-17

Saved source ac6d71414cef54579517aa314f237d995a46c426 (independent recovery
branch 1589-relay-frozen-recovery-20261117) built with zero errors, 96 warnings
in 106.55 seconds. Fresh offline restore and builds did not reuse bin/obj from
another checkout. Official PlanOnly selected exactly two methods/five cases,
zero execution, 8.196 seconds. The official NoBuild run completed 5/5 PASS,
zero skips/duplicates, 47.183 seconds runner wall and complete runner cleanup.

The actual original terminal used inherited bootstrap, exchanged two Unicode
inputs, resized to 31x93, observed canonical EOF while root remained alive and
returned StoppedWithinScope. Four actual-held-root control gate cases (L/A,
cancel/expiry) cancelled before releasing the control gate, preserved held
phase, did not create the exec marker and retained uncertainty while proving
native cleanup. Every independent guardian reported ECHILD, driver exit zero,
zero emergency signals/failures and no deadline. Expiry also exercises the real
native five-second held timeout; it is not an isolated managed-timer proof.
L is blocked-writer coverage, not a second lawful native launch admission.

Raw diagnostics stay external under managed-first. TRX identity:
20261117-104851-884-5-c87f37b33c2f476cb74143f30975fe0b-categories.
This is the first passing managed observation, not final source/outcome
acceptance. Earlier socket-creation RED remains distinct. These four gate tests
have no claimed prior boundedness RED. Raw full-pipe cancellation/nonflushing
abort, failed transfer/partial admission, independent review, bridge transport
and actual maintained live-GM/game acceptance remain outstanding. No speedup
claim: each actual root/guardian is deliberately fresh and cannot be cached;
immutable toolchain/package DATA is reused, never ownership or authority.

## Bridge consumer integration plan, 2026-11-17 (#1589)

Continuation branch: `1589-linux-relay-integration`, base `82656f1b`.
The user requested full ordinary Linux relay support and delegated engineering
choices; no security relaxation, worker-auth replacement or synthetic live-GM
claim is permitted. Managed eight-case block is independently accepted; FIFO
primitive/peer acceptance is tracked separately and must be imported exactly.

Architecture: a public `BridgeTransportEndpoint(Version, Kind, Address,
TransportRun)` and shared `BridgeTransportConnector.ConnectAsync(endpoint, token)`
in the shipped Client assembly return one live Stream. C# and PowerShell use the
same native implementation; no duplicated P/Invoke or new unowned helper process.
Kinds are named-pipe-v1 and linux-fifo-v1, version 1. Malformed/unknown/conflicting
endpoints fail closed; no client reconnect/fallback. Transport identity does not
replace application run/generation/root, draft challenge, live pins or close ACK.
No legacy saved-state compatibility is needed for ephemeral bridge status.

- [ ] Import accepted FIFO source/tests and merge additive catalog/project entries
  without replacing the inherited-terminal category or existing TestSupport.
- [ ] Add test-first endpoint/connector validation and actual FIFO Stream exchange;
  initially refusal-only connector stub, then real bounded connection. Cover
  unknown/malformed endpoints, stale run, cancellation and exact stream EOF.
  New files: Services/GmRuntime/BridgeTransportConnector.cs and focused tests.
- [ ] Add bridge-owned listener adapter in BookOfEternityGMBridge. A fresh AF_UNIX
  construction probe may select FIFO only for EPERM/EACCES. Successful probe then
  bind/path/listener failure is fatal, not fallback. Create listener and immutable
  endpoint before StartShellAsync; dispatch begins after existing startup boundary.
  Preserve the next named-pipe listener before serving the previous peer.
- [ ] Wire Program server handlers to Stream, shared endpoint into BridgeStatus,
  GmMainOperationClient, GmLoadSessionOperation and DraftObservation. Draft env
  uses the same explicit endpoint despite TMPDIR changes. Existing application
  handshakes/ownership/close ACK remain unchanged. Listener failures must reach
  host lifecycle; shutdown closes admission, cancels/joins handlers and preserves
  existing original-terminal cleanup and status publication authority.
- [ ] Wire Launcher/bookofeternity.ps1 Invoke-BridgeRequest to the shared shipped
  assembly connector, with actual cancellation deadline and joined connect/read/
  write tasks. Never load an assembly path supplied by diagnostic status.
- [ ] Verify actual PowerShell request and two independent clients, main-operation
  close ACK, single-stream Load, Draft under changed TMPDIR, stale status refusal,
  failed startup and shutdown with pending/accepted peers. Use narrowly owned
  categories and independent source/outcome review; no global suite.
- [ ] Run ordinary launcher and maintained gm-relay with live root-authored GM
  responses, inspect materialized game effects and exact owned cleanup. Prepared
  scripted packets do not satisfy this final acceptance; issue remains open.

Performance: protocol DATA is small; existing FIFO buffers reused, no new cache.
Every owner/pipe/pin is fresh. The FIFO admission bound covers pending/ready peers;
existing bridge capacity still limits accepted handlers. Neither is silently
claimed as a global 48-peer limit across both phases. Restore/build serially using
shared immutable toolchain/NuGet; no copying mutable bin/obj between checkouts.
Independent design review identified startup gating, exact fallback boundary and
failure observation as mandatory integration checks. No consumer code changed yet.

Accepted FIFO block imported from 45ba9130 (17/17 same-process/independent-process
controls, source/outcome independently accepted). Ten isolated files retain exact
worker blob identities. Three shared files were merged additively: unit-project
reference, fixture bin/obj ignores and one gm-bridge-fifo-linux category; existing
inherited-terminal category and all unrelated entries retained. Combined checkout
has not been built/run yet; no consumer wiring is included in this import.

## Consumer continuation: shared connector test-first, 2026-11-17

Branch `1589-bridge-consumer-integration-20261117`, base `39985a3f` verified on
GitHub. A fresh isolated worktree restores the combined source. New public
endpoint/connector is initially refusing-only; eight new cases in the existing
FIFO transport category require exact stream exchange/EOF, cancellation before
peer creation and malformed endpoint refusal. UNBUILT/UNRUN at this checkpoint.
No consumer wiring or live-GM success is claimed. Small endpoint DATA is parsed
once per new connection; connection ownership cannot be cached. Existing FIFO
17 cases remain controls because the public connector delegates to that transport.

Connector causal RED: official category selected/completed25, 17 prior FIFO
controls PASS and eight new cases FAIL with refusing-only connector; cleanup
complete, no skips/duplicates, 17.131s runner wall. Build0errors96warnings94.63s.
The implementation now validates exact version/kind/path/run before connection,
delegates FIFO to the accepted transport, disposes failed named-pipe attempts,
and checks caller cancellation before any new peer. No reconnect or fallback
exists in clients. Linux named-pipe absolute paths remain valid so changing a
child TMPDIR cannot retarget its original endpoint. GREEN pending.

Connector GREEN: fresh build0errors96warnings96.62s; official PlanOnly and run
selected/completed25/25 PASS, no skips/duplicates, complete cleanup, 15.653s.
This is only connector transport coverage, not actual bridge/launcher or live GM.
Independent review accepted the FIFO path but requires Linux named descriptors
to be absolute rather than allowing caller-dependent TMPDIR resolution. Two new
review controls cover that refusal and stale prior run while a fresh listener is
alive; these additions are UNBUILT/UNRUN. Named positive needs a socket-capable host.

Review RED: 27 executed,25PASS/2FAIL, complete cleanup. Relative Linux named
endpoint reached cancellation instead of validation: genuine missing refusal.
Stale-run test expected exact IOException but correctly received its NativeFailure
subtype errno2: test-oracle correction only, not a transport defect. Linux named
addresses are now required absolute/canonical; stale test accepts IOException
subtypes without weakening its independent fresh-listener/no-admission assertions.
Fresh GREEN is pending; no actual consumer is switched yet.

Review correction GREEN on source106bdcb3: fresh unit-project build0errors,
59warnings,77.75s. Official category selected/completed27/27 PASS, no skips,
no duplicates, owned cleanup complete,15.685s. Separate PlanOnly exits0 with27
planned and0executed,5.814s. All three changed files read back byte-exactly from
GitHub. Source review accepted Linux absolute-endpoint refusal and stale-run
control; named-pipe positive remains unqualified on this socket-denied host.

Next integration gate is the actual listener/dispatcher, not another synthetic
transport benchmark. Test narrow real EPERM factory selection and exact cleanup;
only constructor EPERM/EACCES can select FIFO, never later bind/listen failure.
Create immutable endpoint before startup, dispatch only after its boundary.
Fatal notification must be independent of Accept/channel drainage: with occupied
handler capacity and queued peer it cancels/joins the first, does not dispatch the
second, preserves the fatal error and always attempts original terminal stop.
Normal app shutdown must retain original pin close ACK and final response before
cancellation. Preserve next named listener before disposing prior accepted peer;
rearm failure disposes both. A typed actual-host guardian test should then cover
public main client grant/activate/CloseObserved plus shipped PowerShell diagnostics
and shutdown. Only after consumer wiring can the maintained live GM relay gate
be attempted. No task closure, merge or live-GM qualification in this checkpoint.

## Listener/consumer integration work in progress, 2026-11-17

Checkpoint3aafe3bf supplied refusing listener/dispatcher/endpoint-codec stubs.
Fresh build succeeded; official45 cases completed27PASS/18FAIL with complete
cleanup. Implementation at81c9188f then completed49 cases:48PASS/1FAIL. The new
actual-host case did not reach product code: its45s guardian argument exceeded
the non-live guardian's30s maximum, causing exit64 and no report. This is a test
preparation error, not causal product RED or a live relay result. Exact new-mode
budgets are corrected to25s fixture,30s guardian and35s outer observation.

Independent review also found throwing cancellation callbacks can hide original
listener failure. A new typed regression requires both fatal and callback errors,
joined original handler and removed transport. The production correction is still
pending. Actual host Program intentionally has descriptor field/property only,
not listener startup/dispatcher wiring, so its first valid execution remains a
required causal integration observation. Source test fixture lifetimes are now
independent of the owner's cancellation path; actual capability tests follow real
named/FIFO selection rather than assuming ordinary Linux denies sockets.

Valid integration RED atf79ec9c1:51 selected/completed,49PASS/2FAIL,cleanupcomplete.
Actual typed BridgeHost reached Running and refused missing transportEndpoint;
throwing-cancellation control lost the original fatal error, as review predicted.
The actual changed-TMPDIR Draft helper passed, as did all prior transport controls.
Program now prepares endpoint before StartShell, switches original handlers to
Stream dispatcher, observes server failure and separately attempts drain, listener
join, original terminal stop and console restoration. Dispatcher retains callback
errors and all peer failures while still joining cleanup. Fresh GREEN pending.

Actual-host GREEN at08eb76eb: fresh build0errors65warnings41.35s; official51/51
PASS, no skips/duplicates,complete cleanup,20.545s. Independent outcome review
read TRX+scenario+guardian: actual FIFO main grant/activation, client-observed
close before independent shipped shutdown, diagnostics, durable original Stopped,
endpoint removal, ECHILD and no emergency signals/deadline all passed. Actual
Draft helper under changed TMPDIR also passed. This does not qualify live GM.

Review found another cancellation boundary: short application operations can wait
on host CTS, while dispatcher fatal initially canceled only its own CTS. New
causal test requires immediate fatal notification before joining a host-bound
handler. Callback exists as an unused stub for first RED. New actual Load transport
coverage preserves its original Stop/Finish stream, with no canonical replacement
claim. Maintained live driver now calls shipped connector via a qualification-only
stdio fixture, uses maintained relay CLI/close, and supplies no game responses;
inert syntax/argv/phase checks passed, actual game run remains pending.

Host-bound fatal RED:53cases52PASS/1FAIL,completecleanup31.001s. Both actual
Main and Load original-stream/launcher scenarios passed, including no-replacement
Load generation preservation. The new independent host-token wait timed out until
fixture cleanup because fatal callback was not wired. Dispatcher now notifies host
on first fatal before waiting for handlers; a separate prepared-listener watcher
starts before terminal startup and is canceled/joined at shutdown. Natural app
shutdown still writes its original stop response before cancellation; no guarantee
of concurrent unobserved close ACK delivery is added. Fresh GREEN pending.

Fatal handoff implementation run:53completed52PASS/1FAIL,including new host-token
regression PASS and Load PASS. Main-only driver failed during premature metadata
observation before any canonical terminal status; it read the legitimate staged
startup namespace, then its cleanup interrupted startup. Guardian still confirmed
ECHILD/no emergencies. This is fixture ordering, not a relaxation request for
metadata admission. Driver now waits for canonical terminalRunId publication
before fresh original record admission; invalid published records still refuse,
without catch/retry. Second requested input-lifetime descriptor was not executed
because the first descriptor failed. Both require the next fresh verification.

Current integration verification: FIFO53/53 PASS. Input-lifetime30/33 PASS;
three source/provenance oracles failed, not runtime cases. Independent review
confirmed both guards still searched a call absent in baseline39985a3; typed
project reference now loads the byte-identical bridge DLL/PDB from test output.
Guards now bind real Linux/Windows owner origins to shared attachment; provenance
compares both DLL/PDB hashes and reads the loaded PDB's existing source hashes.
No production change or new reflection. Fresh verification pending. These cheap
fresh source/hash checks require no cache; current build provenance cannot reuse
prior acceptance. Live game remains UNRUN.

Oracle correction e421beec: fresh unit build0errors40warnings28.62s and official
input-lifetime33/33 PASS,20.916s,complete cleanup/no duplicates; independent source
review accepted the three guards. FIFO53/53 from unchanged production remains.
First actual maintained relay attempt reached original Running, derived Ready,
and ordinary daemon Waiting without overrides. No game action was submitted:
the test driver awaited an obsolete decorated player prompt while current UI
showed its real plain prompt. Acceptance0; not live-GM qualification. Original
shutdown ACK/Stopped, relay close, helper exit/EOF and guardian ECHILD succeeded,
no emergency or deadline. Source-derived prompt oracle repair precedes a fresh
session attempt; no existing game state or response is reused. Fresh runtime
publication succeeded; immutable package479 files may be reused byte-exactly,
never owner/config/state/queue.

Second fresh live attempt submitted one genuine action but the client rejected
current state before publishing any GM request. Public read-only validation after
original Stopped identified exactly two guardian_relationship_missing_network_edge
errors between the freeform guardian and seeded Elyara. All original shutdown,
relay closure, foreground exit/EOF/termios and guardian ECHILD passed; acceptance0.
This freeform bootstrap defect is not fixed by the transport change. Do not simply
start bridge during initial wait: that wait holds the physical main guard; bridge
before NewGame conflicts with lifecycle generation reset. No race/guard relaxation.
The next narrow relay smoke uses the ordinary selectable Elyara preset: source
confirms its already present guardian/profile prevents adding a second guardian.
Selection is through observed UI, not game-state edits. The existing initial
cancellation remains honest preparation, not initial-NewGame-GM qualification.
Full baseline/accepted ordinary turn still require actual runtime; no success yet.

Third fresh ordinary-preset attempt created the actual turn-1 request after one
real player action, but no GM relay queue request or accepted turn was observed.
Execution recovery then lost the controlling exec session; its original handle
returns Unknown process id. Last streamed RPC evidence is 2026-11-17 21:49:22 UTC;
no final driver result or guardian report exists. Subsequent tool process views
are separate PID namespaces and cannot establish the original processes' death.
Thus cleanup, original Stopped, and completion are UNKNOWN, not successful.
Do not reuse that session, replay its action, or overwrite its diagnostics. A
possible PowerShell event/helper reentry hazard is being checked independently;
it is not established as the cause of this interrupted measurement. A new owned
run must retain incremental raw evidence and its own process observations.

A separate bounded real PowerShell event regression demonstrated helper reentry:
a watcher callback issues sequence2 after reply1 is read but before the outer
PowerShell function compares its mutable sequence. Control succeeds; old callback
fails identity validation. This is not proof of the interrupted run's cause.
Callbacks now only signal; the unchanged serial startup/polling consumers own work.
The ordinary terminal/repair/protocol-failure paths return to polling; arbitrary
repair during unrelated indefinite work is not newly qualified. Initial control
plus all four real watcher callbacks pass with child exit0/EOF/no forced stop.
Acquisition is inside cleanup scope, successful registrations retained immediately;
injected fourth-registration refusal cleans its three earlier subscriptions/jobs
without touching an unrelated subscriber. Fresh C# source-contract run and final
independent review remain pending. No current admission/authority is cached.
The live driver now flushes raw output and immutable correlated RPC events before
finalization, and reads only its own retained process identities/descendants.
Missing/reused process identities remain observational UNKNOWN, never cleanup.
Eight inert driver controls pass. No complete live game turn is claimed yet.

Watcher closeout: independent review accepted runtime, partial acquisition and
failed-cleanup reporting. Initial official source checks were1PASS/3FAIL: two
historical guards selected an existing wrapper instead of Core; new cleanup
extraction wrongly required a following function. Exact Core/current-guard and
last-function boundaries were corrected without production change. Local LF
normalization preserves CRLF portability. Final fresh integration build0errors,
129warnings,60.75s; exact document PDB SHA256 joins current source. Official
four-case category4/4 PASS,11.815s,zero duplicates,complete owned-tree cleanup.
The preceding pre-normalization4/4 run is separate; its PDB did not match the final
one-line change, so it was not reused as current acceptance. No broader suite or
Windows runtime qualification is claimed. Fresh live run d uses478 byte-identical
published files and only the reviewed daemon replacement; no saved game/queue or
ownership copied. Actual live acceptance and original cleanup remain next.

Live d failed after one actual action and zero accepted turns: an old top-level
`for evidence` artifact loop shadowed the new diagnostic sink, so the next real
pump raised AttributeError. This fixture wiring error escaped isolated helper
checks. Its still-pending status reply then mismatched the shutdown correlation;
no retry or fabricated ACK was used. Original logical cleanup is unconfirmed;
guardian reports ECHILD with two emergency signals, not clean completion.
New regression executes the actual module assignment, artifact loop and pump in
one namespace with a real pipe: RED reproduced the collision, GREEN renames only
the loop variable. Process snapshots now preserve verified root rows when child
listing is unavailable and separately record stage/errno/TID; inaccessible children
remain UNKNOWN. All nine inert driver modes pass. No production changes. Next e
reuses only the verified immutable runtime/native/relay; game state is fresh.

Live e: one genuinely authored GM response traversed the maintained relay and
fixed consumer (exit0, child exited, I/O drained); the game accepted exactly the
one submitted action and showed a fresh player prompt. Original turn delivery
was submission-observed with the retained input binding. No repair was requested.
This is real accepted gameplay evidence, not yet a complete lifecycle PASS.
The driver then closed relay execution, exited the client and requested original
shutdown. Shutdown refused original main admission; the record is Uncertain.
Physical terminal stop reported complete, guardian ECHILD/no emergency/no deadline,
but bridge/daemon EOF and logical Stopped ACK were not established by the driver.
Its result is Success=false, driverExit1,228.29s. Preserve that refusal. Investigate
actual client-exit/daemon/owner shutdown ordering before another fresh run; do not
relabel physical cleanup as logical authority or retry this original stop.

## #1589 idle-daemon stop control preparation corrections (WIP, 2026-11-17)

The accepted live relay turn in attempt e does not qualify orderly shutdown:
its original owner remained Uncertain despite physical terminal cleanup. The
bounded idle control preserves the existing original active-pin observation and
both stop orders, using actual typed BridgeHost initialization and the shipped
transport connector. It executes zero game turns and is not live GM acceptance.

Two official attempts failed during preparation, not causal shutdown RED:

1. Client and Bridge no-build publication succeeded, but TestSupport publication
   attempted an MSBuild node named pipe and failed with SocketException 13.
   Publication now uses supported serial `-m:1`; no security setting changed.
2. With serial publication, root `f5ecc247bc82406f84d7816da711b531` reached daemon
   Watching but not Waiting. Python expired at 16.000763 seconds, SignalCount 0;
   no active-pin predicate or original shutdown was reached. Logs show session
   at 17:30:10, backend 17:30:14 and Watching 17:30:16. Guardian reported ECHILD,
   zero emergency signals and driver exit 1. Full startup duration is unknown.

The next fixture-only source block is UNRUN. Python separates 30 seconds startup
(until Waiting) from 20 seconds operation/EOF. C# child observation is 55 seconds,
actual-host deadline 60 seconds with existing five-second final join; guardian
70 seconds and outer observation 75 seconds. Only the two exact daemon control
modes compile the inert neutral CLI with lifetime 65 seconds instead of default
20. Guardian `--live-turn` is only its existing budget-range selector here, not
a game/GM qualification. Production drain remains five seconds, connector RPC
12 seconds, and launcher daemon timeout 30 seconds; none is relaxed.

PTY diagnostics now stream all bytes directly to the raw file without cumulative
record/byte quotas. A rolling marker window detects Watching/Waiting only;
spawn, readiness, signal and EOF timings distinguish preparation from operation.
Python AST parsing and diff whitespace checks passed; no runtime was started.
Next: independent review/checkpoint, fresh unit build, then only the same
`gm-daemon-coordinated-stop` category with existing stop diagnostics enabled.

Optimization: immutable already-built outputs are reused through `--no-build
--no-restore` publication. Serial MSBuild removes unnecessary node IPC, not work
required for evidence. Fresh roots and original authority/pin observations are
mandatory per attempt; mutable admission or shutdown evidence is never cached.
No speedup claim or broad rerun is made. Raw diagnostic artifacts remain outside
Git, including both preparation failures.
