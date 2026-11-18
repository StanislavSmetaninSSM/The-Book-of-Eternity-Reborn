# Linux bridge FIFO transport implementation plan

Source: [#1589](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1589).
Base `1d9ae03f13b4a93fd786af96ac09cc4c4f0ba0c5`; isolated branch
`1589-bridge-fifo-recovery2-20261117`. This worker owns only the files listed below.
No native supervisor, NativeLineageOwner, game contracts, bridge dispatcher or
launcher is changed in this bounded reusable-transport block.

## Goal and design

Provide one duplex Stream per independently connecting local bridge client on
Linux without Unix sockets. Existing bridge application-level run/root/generation,
prepared/active pins and exact close acknowledgement remain mandatory above this
transport. Transport READY is not an operation grant. Worker socket PID/UID
identity is outside scope and must not be replaced by this mechanism.

Current bridge NamedPipe listener has no SO_PEERCRED/CurrentUserOnly admission.
This design preserves its trusted-local-user scope; private directories exclude
other UIDs, not hostile processes of the same UID. Nonces correlate channels,
not OS peer credentials. Alternatives: inherited pipes fit owned children but
not later independent PowerShell clients; inherited multiplexing adds logical
channel death/flow control and cannot claim per-peer kernel EOF. Per-peer FIFOs
fit the current independent-client topology with explicit handshake and EOF.

Server creates a unique run directory (0700), retains its parent/run dirfds and
creates rendezvous FIFO (0600). Each client sends one bounded ASCII ticket in
one write, at most 512 bytes. The server admits the ticket before creating its
unique never-reused peer directory and c2s/s2c FIFOs (0600); clients only open
verified entries. This avoids unregistered client-created debris. A bounded
lifetime registry rejects reused nonces and fails closed on exhaustion. The rendezvous has a lifetime RDWR anchor; peer FIFOs
never have dummy RDWR endpoints. A single ticket reader launches at most 48
pending handshakes, independent of accepted Stream consumers. Duplicate in-flight
peer tickets cannot open a second reader/writer for the same pair.

Server opens c2s read-only/nonblocking and s2c write-only/nonblocking; client
opens s2c read-only first, then c2s write-only with bounded ENXIO wait. HELLO,
ACK and READY bind protocol version, run and fresh peer nonce. Startup EOF is
permitted only while bounded handshake waits for the peer writer. After READY,
EOF is terminal transport loss. Server unlinks only its validated known FIFO
entries after ACK, before READY; open handles retain the channel. No retry can
revive a Stream/pin. Client failure and server failure cleanup are idempotent for
ENOENT, use retained dirfds and exact known basenames, and never recurse.

openat/nofollow plus statx verifies type, UID, mode and FIFO link count. Filesystem
entry replacement inside a same-UID private directory is not an adversarial
boundary: fstat-before-unlink is not an atomic compare-and-delete. Mismatches are
refused and left untouched. Directory cleanup removes only empty owned entries.
No global signal handler, ptrace, seccomp, sandbox or permission policy changes.
Broken-reader writes must report failure without killing the bridge; qualify the
normal runtime SIGPIPE behavior or use a scoped safe-I/O primitive before release.

## Files and interfaces

- `Services/GmRuntime/LinuxBridgeFifoNative.cs`: Linux-x64 typed descriptor and
  anchored directory/FIFO primitives; no authority cache.
- `Services/GmRuntime/LinuxBridgeFifoStream.cs`: independently owned read/write
  handles, serialized writes, cancellation, terminal EOF, deterministic disposal.
- `Services/GmRuntime/LinuxBridgeFifoTransport.cs`: `LinuxBridgeFifoEndpoint`,
  listener `Create(parentDirectory)`, `AcceptAsync(token)`, client
  `ConnectAsync(endpoint, token)`, bounded HELLO/ACK/READY and cleanup.
- `BookOfEternityClient.Tests/GmBridgeFifoTransportTests.cs`: actual Linux FIFO
  behavior without expensive game preparation.
- Dedicated category/selection in this branch only, reconciled by parent during
  integration. Bridge/launcher wiring follows as a separate reviewed block.

## Execution and verification

- [ ] Publish this plan and branch before implementation.
- [ ] Write typed public-surface tests and minimal refusing API skeleton; obtain
  causal RED before implementing transport.
- [ ] Implement real duplex connection and EOF/cancellation/cleanup; run targeted
  official runner selection only after fresh serial build.
- [ ] Add concurrency, large framed payload, broken reader, blocked writer,
  handshake loss, stale/wrong endpoint, malformed/replayed ticket and symlink
  refusal controls; record actual failure/cleanup, not merely elapsed timeout.
- [ ] Independent source/outcome review; save complete source and honest findings.
- [ ] Hand exact API, commit and exclusions to parent for bridge integration.

Optimization: retain validated per-operation DATA and the live descriptor owner,
not cached authority. Each peer needs fresh FIFOs and handshake. Reuse bounded
byte buffers for transfer; do not rebuild game state or native tools per case.
No speedup claim until measured; no whole suite or hosted CI dispatch. Raw logs
stay outside Git. Current status: design only, implementation/tests UNRUN.

Initial source preparation: refusing API skeleton plus two expected-success real
FIFO tests (bidirectional bytes/peer EOF and canceled read without byte loss).
The dedicated category and selection are additive. Offline serial restore passed;
compile/runtime are UNRUN. No bridge dispatcher or launcher wiring exists yet.

## Verified initial checkpoint

Recovery commit `f93acc722280b3bd9cc06e9baaae583b8bdf22f4` saves the refusing
skeleton and two expected-success tests. Serial build passed (96 warnings, zero
errors, 103.42 seconds). Initial selection schema typo prevented PlanOnly; after
correction, PlanOnly selected exactly two cases. Official execution produced
causal 2/2 FAIL at refusing Create, 12.4669711 seconds, complete selection and
owned cleanup, no skips or duplicates. Actual transport implementation remains
unqualified. Raw logs are external.

Ruling: server owns all peer creation/cleanup after ticket acceptance. No client
creates paths. Lifetime nonce registry is bounded and exhaustion refuses new
connections; retired nonces are never recreated. CoreCLR8 SIGPIPE ignore is a
runtime prerequisite, verified read-only before native writes; no handler changes.

## Independent pre-build findings and causal probe preparation

The first independent source review found important partial-creation path leaks,
pending-task-fault shutdown cleanup omission, and concurrent stream disposal
completion before actual FD closure. These are not accepted behavior. Five new
expected-success cleanup tests exercise directory/FIFO post-create cuts, listener
post-ticket cut, and an explicitly held pre-close gate. Narrow internal optional
callbacks supply deterministic cuts without reflection or global umask/signal
changes. They do not fix the defects. Compilation/execution of this seven-case
selection is pending; an injected pending-admission ChannelClosedException also requires queue
drain and run removal even when disposal propagates the fault. This is injected
fault coverage, not proof of the precise production scheduling race. Further
transport negative controls remain required. No bridge integration or live-GM claim.

## Seven-case causal cleanup RED and proposed corrections

Saved test-first `b23a73f9d587092773d2a64ee19bfc3306db8544` built with zero errors,
96 warnings in 101.02 seconds. Official PlanOnly selected exactly seven cases.
Execution yielded the two duplex/cancellation cases PASS and all five new cleanup
cases FAIL at the intended assertions: retained created paths, early concurrent
disposal completion, and retained run after injected pending fault. Wall time
12.8100100 seconds, seven cases complete, no skips/duplicates, runner cleanup
complete. This is real FIFO behavior but not independent-process/live-GM proof.

Corrections now record newly-created identity before faultable initialization,
perform descriptor-free anchored cleanup on creation failure, close the whole
listener even if pending admission faults (then rethrow original failure), and
share a disposal completion that includes both closed FDs and joined operations.
Synchronous stream disposal also joins. Unverifiable or changed cleanup targets
remain refused, not recursively deleted. These corrections are UNCOMPILED/UNRUN;
tests are unchanged. Further negative and independent-client controls remain.

## Bounded lifecycle GREEN and next controls

The unchanged seven-case selection passed 7/7 after fresh serial build (zero
errors, 96 warnings, 101.90 seconds). Test duration 319ms; official runner wall
17.6728101 seconds, exact seven cases, no skips/duplicates, complete cleanup.
Thus the five newly demonstrated cleanup defects/assertions are RED→GREEN; this
is not whole transport or live-relay acceptance.

Seven further real-FIFO controls are now prepared: four concurrent 128KiB peers,
pending-read/backpressured-write/write-gate disposal, broken-reader EPIPE in the
current test host, wrong/stale/symlink endpoints, a client lost before HELLO plus
retired-ticket replay, 48 pending handshakes with a 49th ticket, and malformed
rendezvous input. Each explicitly checks path cleanup before fixture deletion.
These additions are UNCOMPILED/UNRUN, with production unchanged. Independent
client-process proof and application pin/launcher/live-GM integration remain open;
the EPIPE case is not claimed as independent-process qualification.

## Fourteen-case bounded transport GREEN

Saved test-only `2ec02c0bc0d484050c4ef1c160b9b9a6fed85d91` built with zero errors,
40 warnings, 32.22 seconds. Official PlanOnly selected 14 cases in 7.3193830s.
Execution passed 14/14, test duration about five seconds (including real five-second
abandoned-handshake expiry), runner wall15.3282362s; complete selection, no skips,
duplicates or timeout, complete runner-owned cleanup. The new controls passed
without further production changes. This qualifies only the named same-process
Linux-x64 tests. Lifetime65536 exhaustion, every handshake crash phase, separate
CLI process and application pin/launcher/live-GM acceptance are not claimed.
Independent bounded source/outcome review is requested before extending the block.

## Independent bounded acceptance and separate-process extension

Independent Astra High review checked the exact source hashes, logs and TRX and
accepted the current standalone same-process Linux-x64 boundary, with no remaining
Critical/Important finding. It did not accept production readiness. Nonblocking
minors: the 48-directory snapshot is not itself an independent proof that ticket49
stays blocked (the reviewed semaphore establishes the source bound); per-byte
handshake overhead remains unmeasured and deferred.

A test-only independent peer executable is now prepared, reusing the exact client
connector through existing TestSupport friendship. Three scenarios cover 128KiB
echo, EPIPE after remote closure with ordinary child exit, and original channel
EOF after deliberate owned-child termination. The official test starts one direct
bounded child using the existing crash-host pattern, drains stdout/stderr, and
joins exit in finally; no production helper, detached process, authentication
change or duplicated native transport. The child launches no descendants. Tests
assert paths removed before fixture deletion. This extension is UNCOMPILED/UNRUN;
application and PowerShell consumer wiring remain entirely unchanged.

## Independent process execution result

Saved fixture `78adafa4b3a0f0c5ad7c98adc8c54f27d4803df1` built with zero errors,
63 warnings, 43.84 seconds. Official PlanOnly selected exactly 17 cases in
7.1023153 seconds. Execution passed 17/17: roughly six seconds of test execution,
23.5662593 seconds runner wall, no skips/duplicates/timeouts, complete selection
and runner-owned cleanup. Three new real child-process scenarios passed: exact
128KiB exchange, EPIPE handled with normal exit0, and server EOF after deliberate
owned-child termination. Child exit and stdout/stderr draining were joined; each
case asserted owned paths removed before fixture cleanup. No production transport
code changed for these tests. Independent extension review remains pending.

This does not qualify application run/root/generation or grant/activate/close-ACK
semantics, PowerShell consumer loading, bridge dispatcher/launcher integration,
worker PID authentication, or a live GM turn. Those remain the parent's next
integration boundary. No merge or issue closure is claimed here.

## Final bounded handoff review

Independent Astra High source/outcome review accepted the standalone Linux-x64
FIFO boundary including all three independent-process cases, with no new
Critical/Important finding. It verified exact source blobs, project wiring, logs
and TRX without rerunning. Descriptor closure was accepted from the unconditional
reviewed finally, not inferred merely from removed paths. The original capacity
snapshot and per-byte parsing observations remain nonblocking deferred minors.

The handoff contains three new production transport files, test-only fixtures,
tests and the dedicated selection. Shared catalog, unit project and ignore-file
changes must be integrated additively; they must not replace another branch's
entries. Transport run identity is distinct from application identity. Internal
48 slots bound pending handshakes plus queued connections only; accepted Stream
count remains the bridge's responsibility. No consumer wiring was changed here.
Ready for a separately reviewed integration block, not merge or issue closure.

## #1589 Linux daemon singleton follow-up, 2026-11-18 (WIP plan)

Live ml4 accepted Mortal turn42 and completely closed its original owner. The
next phase failed before any action because Assert-SingleDaemonInstance treated
old stopped status PID111 as a live original daemon. PID-only lookup is not
identity across reuse/namespaces; PID plus birth also leaves concurrent-start
TOCTOU. These are not grounds to delete status or bypass admission.

Design: every Linux daemon entry uses a retained in-process IDisposable guard
backed by existing trusted filesystem primitives and a distinct fixed
base/.boe_runtime/locks/gm-daemon-instance.lock. Do not hold the main-owner,
session-lifecycle or canonical-write lock for daemon lifetime. Do not use a
child-held lock: child death could release it while the daemon still runs.
The public narrow net8 facade is loaded only on Linux from the trusted shipped
or build code root. All non-Linux branches retain their current behavior and
must not resolve/load this type. Linux requires a Core runtime able to load
net8; unsupported runtimes refuse clearly without PID fallback. Windows runtime
and distributed/network filesystem support are not claimed.

Guard owns one stable local file/parent handle, authenticates path/handle and
uses the existing Linux nonblocking flock pattern; never unlink or replace it.
It grants singleton exclusion only, never generation/publication permission.
Current per-operation root/generation/admission remains authoritative. No cached
liveness result or new cache framework is appropriate for a live owner handle.
Fresh acquisition is bounded; repeated source parsing is unnecessary.

Admission occurs before daemon canonical bootstrap effects and before watchers.
Under the held guard, distinguish atomically fresh first-use from an existing
guard with missing status. Fresh root without old status can initialize. Existing
roots require a well-formed guarded clean-stopped record with exact sessionPath
and owner identity; malformed/null/legacy/running/failed/unknown or lost status
refuses without rewriting it. New guarded incarnation running must be confirmed
through the existing canonical writer before processing starts. A losing or
unadmitted daemon must not publish fatal/stopped over the real owner's status.
Hold the guard through watcher/operation cleanup and final status publication;
failed cleanup or final write must not claim a clean restart. Always release only
one's own retained handle. Previous ml4 status is not migrated manually.

Bounded implementation sequence (one coherent review block):
- [ ] Add causal AST/pwsh test of actual PID-only stale-status refusal and narrow
      guard tests, preserving the failure before the implementation.
- [ ] Add a small public Linux daemon guard facade reusing TrustedLocalFileScope,
      PhysicalFileAuthority/stable-parent and local flock mechanisms; no general
      process manager or main admission API changes.
- [ ] Wire all Linux daemon entry/lifetime paths, initial confirmed running and
      guarded final status; preserve Windows source branch and early failure.
- [ ] Official narrow categories: real same-root cross-process contention and
      restart, distinct roots, status malformed/null/missing/legacy, failed first
      write, stale PID with valid guarded clean stop, wrong session/owner,
      substituted path/handle, startup/watcher cleanup and non-Linux source guard.
      Include affected existing daemon watcher/operation/template consumers.
- [ ] Independent source/outcome review, save all source and precise results.
- [ ] Fresh independent live world with two sequential actual GM turns and full
      original closure each; no continuation token from failed ml4 p02, no raw
      evidence edited, and no implied resolution of unknown ml3/e.

Verification costs and exact category IDs will be recorded with actual results.
Use cheap typed/AST cases and existing harness rather than a whole game for lock
semantics. Rebuild changed owned outputs once after cheap RED; reuse immutable
verified output for the actual live path. No broad suite and no speedup claim.

First RED source checkpoint: explicit refusal facade stub plus11 typed cases and
one actual AST/pwsh singleton case, in new gm-linux-daemon-singleton-data/script
categories. Source is compile-ready but build/execution has not yet occurred;
this is not GREEN, concurrency/lifetime qualification or fixed daemon behavior.
Production daemon remains unchanged at this point.

Source review corrected an important platform assumption: the generic
PhysicalFileAuthority.EnsureHandleMatchesExpectedPath is a Linux no-op; it cannot
supply Linux handle identity proof. The implementation must reuse actual existing
LinuxBridgeFifoNative openat/NOFOLLOW/CLOEXEC directory walk and statx device/inode
identity primitives, with narrow regular-file exclusive creation/flock support,
or expose the existing WorkerRunLedgerPersistence equivalent. Reopen and compare
retained root/parent identities; require a private single-link ordinary guard,
not mode0700 on all historical runtime directories. Preserve trusted same-UID
local scope without claiming hostile-owner or power-loss guarantees. Partial
owner-token write refuses without repair; an error before the first written byte
may leave the old clean pair intact because no new work began. Strict raw status
parsing must reject duplicate/case-colliding keys and wrong types before use.

### Singleton first causal RED and implementation (unverified GREEN)

The automatic-build official attempt stopped during restore before discovery:
0 cases, runner 3.0518261s, complete cleanup; this is not causal RED. A separate
no-restore unit build completed 94.18s, 87 warnings, 0 errors. Official DATA
selection then executed 11/11 FAIL at the explicit refusal stub, 10.5236368s,
complete cleanup. The runner stopped before the script descriptor, so a separate
script selection executed its actual AST-extracted singleton function: 1/1 FAIL
at the old PID-only already-running refusal, 11.1895805s, complete cleanup.

The implementation now retains a Linux-x64 in-process guard and reads prior raw
status through a one-shot callback under flock. Strict duplicate/case-collision,
version/type, session and prior-owner checks precede the new same-handle token.
The native adapter reuses LinuxBridgeFifoNative directory walking/statx and adds
regular-file exclusive creation/flock checks; no Windows PhysicalFileAuthority
no-op is misrepresented as Linux identity validation. Parent/root/file identities
are freshly compared. No PID registry/cache or lifetime main pin is introduced.

A new script helper loads only on Linux/Core with a compatible net8 runtime.
Admission and confirmed running publication precede canonical bootstrap; only
the ordinary admitted status-parent creation may precede the running write.
The outer finally retains the handle through the existing watcher cleanup and
strict final status. Non-Linux singleton code remains the previous branch.
Status and control data bounds are protocol bounds, not raw diagnostic quotas.

The two narrow categories now select 23 cases: 18 guard DATA/process cases and
5 actual-script/source cases. The added 11 cases supplement the initial 12 RED
cases, not retroactive causal RED. They include a real child owner, same-root
contention, EOF release and successor child; strict status shapes; callback under
lock; failed read; actual status write/readback refusal and bootstrap ordering.
These are unbuilt/unrun pending independent review and official GREEN. There is
no live-game result for this change yet. Fresh mutable handle/namespace checks
cannot be cached; one-shot parsed prior status stays within acquisition and is
not retained. No performance improvement or hostile-owner/powerloss guarantee.

### Singleton corrective review RED and revised protocol (GREEN pending)

Initial implementation compiled in 89.33s, 87 warnings, 0 errors; tests were held
for independent review. Review found unconfirmed stopped JSON could authorize
restart; PowerShell string callbacks could coerce missing to empty; changed mode
or hardlink was not rechecked; fatal diagnostics could write after guard loss;
and a wrong session sibling could poison the valid root before refusal.

Corrective build: 27.71s, 42 warnings, 0 errors. Official separate selections:
DATA 21 executed / 17 PASS / 4 FAIL, 10.9888681s; script 11 executed / 7 PASS /
4 FAIL, 13.6948904s. Both complete cleanup. DATA reached mode, hardlink and
unconfirmed-stop failures; its peer failed before ready through the same PS
string-null boundary. Script reached missing-status coercion, wrong-sibling
poison and unguarded fatal write. The terminal-readback scenario was blocked at
initial missing-status coercion, so it did not yet qualify that later branch.

The revised guard writes active(settled=false) on admission, then marks the same
inode settled only after the original canonical final operation and exact
readback/guard validation succeed. This records canonical operation completion,
not process exit/EOF/delivery or powerloss durability. A failed marker write/read
is not caller success and is never retried or rolled back. A later independent
acquisition accepts only a complete exact settled guard/status pair under flock.
Mode/UID/type/single-link are rechecked. The object-returning status callback
preserves actual absence separately from a zero-byte file; session basename is
validated before guard acquisition. Lost guard suppresses canonical fatal/log
publication; stderr and own handle cleanup remain available.

The revised selection expects 34 cases (21 DATA, 13 script), including actual
Start→confirmed stopped→Dispose→actual Start with a new owner, malformed JSON and
UTF8 reader refusal. Positive primitive fixtures now explicitly mark their
fixture-confirmed stop; the unconfirmed-stop negative remains unchanged.
All revised code is unbuilt/unrun pending save, re-review and official GREEN.

### Guard regular-file runtime flag correction (GREEN pending)

Corrected-source unit build completed 93.76s, 88 warnings, 0 errors. Source,
client output and unit output carried byte-identical daemon helper. The malformed
JSON/UTF8 fixture branch was corrected before execution; no production change.
Official DATA executed 21: 20 PASS / 1 FAIL, 11.2230875s; separately script
executed 13: 10 PASS / 3 FAIL, 14.2456923s. Both had complete owned cleanup and
no duplicate IDs. The actual peer and actual Start positives failed on the first
fresh guard readback; the terminal-readback scenario remained blocked before its
terminal phase. Other affected categories remain UNRUN.

A minimal native-open control under the actual PowerShell .NET 11.0.0 runtime
reproduced FileStream Position=0 but Read=0 after a successful write. RandomAccess
also failed on that O_NONBLOCK handle; independent native pread and reopened-file
read verified the exact ABC bytes. Native-open without O_NONBLOCK passed, and the
precise original NONBLOCK→fcntl clear→FileStream control passed n=3/ABC. These are
runtime diagnostic controls, not game or publication acceptance.

The narrow correction preserves NONBLOCK during open/type/owner/link validation,
exclusive flock and named identity checking; only then clears that flag on the
same retained regular-file handle before FileStream. Other flags and CLOEXEC are
unchanged. Errors including EINTR refuse and dispose; no I/O replay or repair.
FIFO opens and general PhysicalFileAuthority are unchanged. The proposed native
record-I/O alternative was discarded before build/publication. Existing actual
peer and helper-start failures provide causal RED for this runtime boundary.
Fresh namespace/ownership/readback checks remain mandatory; no new cache or
performance claim. Corrected flag source awaits save, review and official GREEN.

### Singleton corrected official GREEN (live continuation not yet run)

Source `91513a752fd02e1358e3e472cfc420e1f7dc347a` received independent source
acceptance after the bounded fcntl correction. Serial no-restore unit build:
54.14s, 19 warnings, 0 errors. Official categories
`gm-linux-daemon-singleton-data`, `gm-linux-daemon-singleton-script`,
`gm-bridge-fifo-linux`, `gm-daemon-coordinated-stop`: 106/106 PASS, no skips,
no duplicate IDs, complete owned-tree cleanup, runner 62.0073945s. Exact TRX
counts: new DATA 21, new script 13, existing FIFO 71, original coordinated stop 1.
The previously blocked actual first acquisition, peer successor, clean actual
Start→stopped→restart and terminal-readback refusal now complete successfully.
Independent outcome review confirmed the coordinated actual daemon's 17 original
pins closed and its retained guard/status settled pair agreed. That scenario's
status retains one recovered `Original admitted daemon read refused.` during
close; this is not an error-free-log claim and did not invalidate its assertions.

One subsequent serial no-restore integration build completed 61.68s, 129 warnings,
0 errors. Official `gm-daemon-watcher-serialization` plus
`mortal-skill-template-guidance`: 6/6 PASS across three descriptors, no skips or
duplicate IDs, complete owned-tree cleanup, runner 28.4450367s. The source helper
and client/unit/integration output copies are byte-identical, SHA256
`6dcd9baf80cbb5c8db46efc1769685bfe19e78ac293b4aa5499b43179b8e584a`.
This verifies output delivery, not a newly published runtime package.

Raw build/runner/TRX evidence remains outside Git, with runner logs under
`/workspace/shared/relay-alternative/daemon-singleton-fcntl-*` and corresponding
runner-owned TestResults directories. Results are bounded Linux-x64 evidence;
no Windows/ARM, legacy-status migration, powerloss atomicity, new live Mortal
turn or whole-feature qualification is claimed. No broad suite was run. Fresh
mutable handle and namespace validation remains uncached; only one admission's
prior parsed DATA is reused locally. The correction removes a runtime flag
mismatch, not measured duplicate work, so no speedup claim is made.
Next: independent outcome review, then parent-controlled fresh published-package
live continuation with ordinary stopped status and retained guard, never replay
or migration of earlier failed live requests.

### Local-menu to external-main worker readmission: causal test WIP

Live ml5 phase02 stopped before any game action: daemon restart succeeded, but
client Continue reported `Explicit worker admission is required before canonical
recovery.` The unique throw is the disposed registration check in
GmWorkerRootContext.ValidateCanonical. The same client first shows its menu while
main is stopped; ordinary local admission attaches and then retires the quiescent
worker context. A later external main owns the inventory; that client's new
remote admission observes workers but canonical recovery still consults its
retired local registration. This is a source-based hypothesis, not yet causal
acceptance. Existing world and prior live results remain untouched.

New narrow test `gm-local-remote-worker-readmission` prepares only an empty real
durable inventory, lets a separate shipped consumer execute and close ordinary
local current-session admission, starts the real external production main, then
asks the SAME consumer/FS to enter the same current-session operation remotely.
It checks original close observation and uses existing owner/guardian cleanup.
This tests the actual shared admission/finalization layer, not Continue UI or a
GM turn. No production guards are changed; no reflection or authority injection
is added. Test source awaits remote save, review and official causal RED.

Design remains pending: any fix must retain the original remote pin plus exact
retired durable registration as a lease-local conjunction, preserving current
server worker authority, fresh refusal and special closing semantics. No global
fence deletion, new worker coordinator under the external owner, broad
WasRemote/disposed bypass or cached permission is approved. One tiny empty
inventory avoids costly game setup; mutable authority cannot be cached.

Test-source review corrected the preparation route before execution: the new
mode uses typed BridgeHost.RunAsync and the published transport descriptor,
not the legacy named-pipe-only fixture path. A separate fresh client must succeed
before the warmed client's second entry. Both report exact config/generation
preservation, including the expected refusal path. Original shutdown ACK identity,
durable Stopped identity, host exit0 and owned cleanup are mandatory even on RED.
The retired-context premise is observed as a known-quiescent local entry followed
by RequireOpen refusal, not private-field proof of disposal. No new reflection
or production changes. These test corrections are still unbuilt/unrun.

Initial readmission-test build stopped before execution: 8.49s, 25 warnings,
2 compile errors from a missing Core namespace import in the new typed host
fixture. Zero tests ran; this is preparation failure, not causal RED. The single
missing import is corrected; production guard code remains unchanged.

### Local-to-remote causal RED verified (production correction pending)

After the import correction, serial no-restore unit build completed 37.93s,
67 warnings, 0 errors. Official `gm-local-remote-worker-readmission` executed
1/1 FAIL, runner 17.2829425s, no skip/duplicate, complete owned-tree cleanup.
The fresh client first succeeded against the actual FIFO production owner with
its original close observed. The warmed SAME client then refused with the exact
GmWorkerRootContext.ValidateCanonical line139 disposed-registration exception,
through AcquireCanonicalWriteLeaseCoreAsync line4833. Both config and generation
remained byte-identical, including the failure. The refused remote operation's
own close was observed. Original shutdown returned positive ACK with matching
run identity; durable Stopped retained the entire original identity, host exited0,
and the independent guardian reported ECHILD, zero emergency signals, zero
failures and no deadline. This is causal RED, not a bootstrap or cleanup failure.

Evidence is the retained official runner log `local-remote-red-official.log`
under the shared external diagnostics directory and its test-owned scenario
`ac50d1aa0c4b456b9e8eed4a0ea6bede`. No original live world/request was touched.
Production guards remain unchanged. Next: independently agree the exact
lease-local retired-registration/original-remote-pin conjunction and its negative
controls before implementation; no broad disposed/WasRemote bypass is permitted.

### Exact retired-registration remote conjunction: source WIP

Independent design review accepted the verified causal RED and this bounded fix.
A canonical lease may retain only the exact retired durable root registration,
its exact MainAdmission/MainAccess and original remote client/generation. The
registration must still be the root's current reference, disposed after ordinary
quiescent retirement, with zero clients/executions, no pending/deferred work and
no uncertainty. These DATA predicates alone grant nothing. Existing fresh
MainAdmission remote validation, inventory observation and generation checks
remain authoritative, bracketed by short registration checks without disk/IPC
under the root gate. The lease binding is checked first even if the current
root registration disappears. No retired coordinator is reopened or pinned.

Ordinary permission is limited to SessionMutation/PublicationReadQuiescence with
no worker purpose and no closing frame. A distinct SessionFinalization lease
requires the actually bound closing original and cannot perform recovery or
mutation. MainMetadata, replacement, worker and cleanup authority gain no new
exemption; their existing routes stay intact. References are released with the
lease. No permission cache, last-validated Boolean, root fence deletion or global
registry is added. Existing fresh validation is reused within each synchronous
checkpoint rather than repeating its I/O merely to validate reference DATA.
This is not a measured speedup claim.

Four new cheap predicate cases cover active, uncertain, swapped and foreign-root
registrations without invented remote grants. The same real FIFO causal fixture
adds unbound refusal, wrong expected generation, forbidden replacement and late
ordinary acquisition after BeginClosing. Each owned refusal closes its exact
original operation, and each owned refusal is followed by a distinct successful
ordinary operation on the same main. Canonical config/generation and full original
stop/guardian evidence remain required. New category is
`gm-retired-worker-registration-data`; existing narrow readmission category keeps
its exact Fact. All additions are currently UNBUILT/UNRUN pending source review.

Affected selections require transport inspection, not blind reruns: existing
console runtime/pending fixtures use legacy named-pipe paths and are not being
ported for this correction. Their coverage is not claimed here. New typed/FIFO
readmission and managed registration/absence controls are the primary selection;
existing exact cleanup neighbors are considered separately with the reviewer.
No game world, prior request, or accepted live result was modified or replayed.

### Retired readmission bounded execution; cleanup neighbors under diagnosis

Source checkpoint 5a1ff30f4ea5b1e23b4b8c26dbead72f193ed8ad: serial no-restore
unit build completed 91.25s, 88 warnings, 0 errors. Official new selections
executed 5/5 PASS in 21.6147486s, no skips/duplicate IDs, complete owned cleanup.
The real FIFO same-client local-to-remote path completed, as did all seven
refusal/subsequent-positive evidence rows. Config/generation remained exact;
original full Stopped identity matched Running, host exited0, and guardian
reported driver0, ECHILD, zero emergency signals/failures and no deadline.
The coordinator independently accepted these five results. This is bounded
admission evidence, not a replay of the failed live Mortal phase.

The approved existing unit neighbors then executed 18 cases in 46.3012324s:
15 PASS / 3 FAIL, no skips/duplicates, complete runner-owned cleanup. All seven
absence and four finalization/managed-neighbor cases passed; cleanup admission
passed four of seven. Its publication, publication-expired and publication-retired
rows failed at MainWorkerCleanupScenario.cs:117. Each reached the real publication
cut once, retained its slot and had no cleanup receipt, but the fixture's callback
failure list still contained its initial deferred-cleanup exception. This is an
unresolved neighbor diagnostic, not yet a baseline or regression classification.
No assertion or production guard has been weakened to obtain GREEN.

Execution stopped for causal diagnosis. The two integration cleanup neighbors
remain UNRUN; no live game continuation/retry occurred. Complete raw evidence is
outside Git in retired-readmission-green-build.log, retired-readmission-green-
core-official.log and retired-readmission-green-neighbors-official.log under the
shared external diagnostics directory. The real new scenario is a61a8da58e6b49da
b3658c31b744a989; official neighbor run is 20261117-211546-140-6-96aea338b2424d53992206994651301c-categories.

### Cleanup neighbor oracle correction, test-only WIP

Independent review traced the three failures to an unchanged-source contract
mismatch: GmWorkerQuarantinedExecution retains canonical publication uncertainty
on its original execution authority and deliberately suppresses subsequent
failure callbacks. The old neighbor expected a second diagnostic callback, then
in one path restored audit bytes to force cleanup success. Neither expectation
matches the existing sticky uncertainty contract. These neutral-main NoLaunch
paths do not enter the new retired remote binding. No prior-version runtime
failure is claimed and no production behavior is changed for this correction.

A new cheap typed test uses the actual NoLaunch authority, quarantined execution
and reaper, with one exact required-audit sentinel. Two passes must retain that
same fault, invoke one append and no failure callback, and retain capacity/slot.
It has no filesystem, main, remote client or child process. The three real
publication-cut cases now capture the actual append exception via catch/rethrow,
compare its reference to the original authority fault, and retain the original
unknown audit bytes, journal, slot/capacity and absence of a receipt. Repeated
live and post-withdrawal passes must stop at the sticky fault before another
append. The exact original main pin is separately shown exhausted; retired mode
also requires its actual Stopped main before that pass. No state is restored or
fault cleared to manufacture success. Test-owned resource disposal is separate
from logical debt retirement. Metadata and receipt-retry success paths remain.

The three publication rows are split into
`gm-main-worker-cleanup-publication-sticky-linux`; the existing cleanup category
retains only its four previously passing controls. New tiny proof category is
`gm-worker-quarantine-sticky-audit-data`. Next bounded execution is this one proof,
the three corrected rows and the two still-unrun integration cleanup neighbors.
The previous five new and fifteen unaffected passing cases are not repeated.
Source changes are test/catalog/documentation only, currently UNBUILT/UNRUN;
remote save and independent source review precede execution. Production guards
and all live game worlds remain untouched.

### Retired readmission bounded result: 26 distinct passing cases

Saved test-only correction 735a5f3127d82d3abf8db5ce0ad965d43e9f43e8 passed
independent SOURCE review. Serial unit build: 39.53s, 67 warnings, 0 errors.
Official sticky-audit proof plus the three corrected publication rows executed
4/4 PASS in 21.1342002s, with no skips/duplicates and complete owned cleanup.
All three actual native scenarios reached exactly one publication cut and one
append, retained canonical uncertainty and its evidence, and did not release the
logical slot. Both withdrawal rows retained the same original fault without a
second append or receipt; the retired row separately proved actual main Stopped.
Their guardians all reported driver0, ECHILD, zero emergency signals/failures,
no deadline. This qualifies retention, not successful cleanup of logical debt.

Serial integration build then completed 62.19s, 129 warnings, 0 errors. The two
previously unrun exact cleanup neighbors executed 2/2 PASS in 13.3802089s,
no skips/duplicates, complete owned cleanup. No production source changed during
these test-oracle corrections. The earlier five new cases and fifteen unaffected
passing neighbors were not repeated. Current bounded passing evidence therefore covers
26 distinct cases: 5 + 15 + 1 new proof + 3 corrected rows + 2 integration cases.
This is not one 26-case run; the historical three oracle FAILs above remain part
of the record. Unsupported legacy named-pipe console paths remain UNRUN.

Full logs use retired-readmission-sticky-{build,official}.log and
retired-readmission-integration-{build,official}.log in the shared external
diagnostics directory. Current DLL/PDB hashes, including matching client copies
in unit and integration output, are recorded outside Git in
retired-readmission-current-output-pins.json. This confirms build output identity,
not a new published live runtime package. New source-check guards still require
fresh original permission; no permission cache or skipped canonical fence was
introduced. No live world/request was replayed, no broader suite was run, and
independent outcome review remains the final gate before a fresh live continuation.

### Linux retained bridge status restart: test-first WIP, 2026-11-18

Tracked #1589. ml6 p01 was independently accepted and its original main fully
Stopped. p02 then ended before gameplay in27.199s, zero actions/accepted turns:
launcher printed `GM bridge already running (pid=36).` This is the Start-Bridge
PID-only return, not an actual host owner refusal. Launcher exited0; the outer
phase failed. Guardian ECHILD/driver1/reaped1/no emergency or failures does not
prove normal client closure or a logical Running identity; neither was observed.
No process is killed, status deleted or failed phase replayed to resolve this.

Read-only inventory: Start-Bridge returns on retained helperPid before platform
selection; Read-BridgeStatus/Test-BridgeHelperAlive also filter status on PID.
Invoke-BridgeRequest and daemon Get-GmBridgeStatus have diagnostic PID filters,
but real RPC still uses the typed endpoint. Linux daemon singleton already uses
its held guard; start-daemon has no separate PID singleton gate. Windows window
binding and legacy descendant/shutdown fallback are not this launch route.
Original-main shutdown refuses fallback when its durable expectation exists.
Client harness PID checks diagnose terminal failure, not singleton admission.
These neighboring limitations are inventoried, not claimed fixed.

Approved minimal design: Linux foreground launch reaches existing production
GmMainOwnerGuard and strict durable main record validation instead of allowing
an advisory PID to decide admission. Other platforms retain their current path.
No new singleton lock, ownership cache, deletion, PID kill or ENXIO-is-free rule.
Existing native/main ownership remains the only authority. Its fresh locked
checks cannot be cached; repeated PID advisory reads add no trustworthy DATA.
No performance claim before measurement.

New test-first AST fixture extracts real launcher function bodies into an inert
shipped layout, uses a real Linux PTY and retained helperPid equal to the live
PowerShell fixture PID, and substitutes only the external dotnet leaf. Three
cases require launch admission or exact hidden/redirected foreground refusal;
retained status bytes must stay unchanged. Actual same-root host contention,
original usable RPC/identity/descriptor preservation, matching stop and distinct
same-root restart are prepared separately. All new checks remain UNRUN; causal
RED and independent source review precede production launcher edits.

Causal AST RED on reviewed source84b1: serial unit build39.84s (wall40.230s),
67 warnings, zero errors. An initial `/usr/bin/time` wrapper was unavailable and
stopped before dotnet, zero builds/cases; shell timing then ran the actual build.
Official gm-linux-bridge-restart-dispatch completed3/3 cases: zero PASS, three
FAIL,12.9975617s, no timeout/skips/duplicates, complete owned-tree cleanup.
Lexical fixture-root and actual-terminal preconditions passed. The stale-pid
case printed the original already-running message and failed the explicit
launch-admission assertion. Hidden and redirected cases likewise returned at
that PID gate instead of refusing the missing foreground prerequisite. Thus
these are behavior failures, not parsing/bootstrap failures. Raw evidence is
bridge-restart-red-build-actual.log and bridge-restart-red-official.log outside
Git; runner20261117-215302-137-5-11f4cb0343ec416396e70521d31d3e8c-categories.
The separately prepared actual contender/restart case remains UNRUN, and the
production launcher is unchanged at this checkpoint.

Minimal GREEN source prepared after independently accepted causal RED: only the
Start-Bridge advisory PID block is conditional on not Linux/Core. Linux proceeds
to its unchanged visible/foreground and packaged-DLL checks, then actual host
main-owner admission; no new authority or fallback is introduced. Other
platforms execute the identical PID block before configuration, preserving
Windows Desktop short-circuit compatibility without resolving IsLinux there.
No other PID consumer changed. The retained diagnostic bytes are untouched.
Optimization: this removes untrusted advisory reads from Linux startup; it does
not cache or skip any locked main-owner/durable-state check. No speedup claim.
AST3 plus actual contender/restart1 await post-patch source review/execution;
no broader tests or live game were run for this source checkpoint.

Reviewed source4e1d54 bounded GREEN: official two categories completed4/4 PASS
in23.3187738s, no timeout/skips/duplicate IDs, complete owned-tree cleanup.
The runner serialized exclusive actual-host1 before AST3. C# source/binaries
were unchanged since the causal build, so no unnecessary rebuild was performed;
AST tests explicitly read the current repository launcher source, SHA256
c4ab8244b2b939b78df955ae0656ae15dc588358c82815c6a9890df22f62d6aa.
Host1 is first-run positive coverage, not a claimed prior causal RED.

Actual scenario8c60ce96c8ad4ff1997aded3a01b395a observed39 main-guard contention
callbacks and held-native-root counts [1,0,1]. The refused contender preserved
the original durable Running identity, published descriptor and identity fields;
original status RPC remained usable. Original runc185fe34e9c24c20875dce2e74be34fa,
epoch1, stopped through its exact expected-identity request and matching ACK/full
Stopped proof. Same-config restart established run76bbf104e2f14bc7a8e38bbe7584985b,
epoch2 in the same generation40d95ae925124082b96e56e31c3ac1f0, then independently
closed through its own matching ACK/full Stopped proof. Both host exits were0;
config bytes remained unchanged. The intended contender IOException is retained,
not hidden or claimed an error-free trace. Guardian: driver0, ECHILD, reaped1,
zero emergency signals/failures, no deadline.

Raw bridge-restart-green-official.log and scenario evidence remain outside Git;
runner20261117-215643-834-6-1fb40e9f6fe44b3992db287a69a5afb6-categories.
No broad suite, live action, failed-world replay, status deletion or PID kill was
performed. This bounded startup correction still requires independent outcome
acceptance and exact packaged-script delivery before a fresh live continuation.
Source and build outputs are frozen at this checkpoint.

Qualification boundary: the three BridgeHost instances in the contender test
share one managed test process; the owned native roots are actual. This is not
cross-process bridge contention qualification. Historical AST3 RED remains
recorded above. Current22 DLL/PDB pins are refreshed in external
bridge-restart-current-output-pins.json. The causal unit build includes the new
test/support scenario, while retained integration output still contains the older
helper and is not used as the next package's helper source. Production client
and bridge DLL/PDB remain unchanged. Do not claim all helper copies identical;
there was no integration rebuild for these test-only changes and no C# rebuild
between the causal build and source4e1d54 GREEN4/4. Packaged launcher must be
copied from the reviewed current source, not an older build-output script.

### Daemon terminal-settlement oracle: test-first WIP after ml7

ml7 p01 probe reported Success although final daemon status remained running
and its exact guard remained unsettled. Physical EOF/exit0 is not confirmation
of terminal status publication. This establishes a fixture acceptance gap, not
the cause of daemon finalization failure; no production stop correction is made.
New14 inert cases invoke the actual phase functions from their AST without any
runtime top-level execution: a complete physical/main-close positive plus bad
owner/session/missing/malformed/null/duplicate/running/false/type/original-state
pairs, a lying Success record, and two unchanged saved proofs against changed
current continuation status. No helper refusal stub substitutes for this causal
acceptance behavior. Cases are UNRUN; existing live driver is still unchanged.

Planned correction is read-only original-active identity capture before action,
strict final pair observation after EOF, raw/hash retention even on failure,
and current-pair recheck before continuation writes/processes. It never writes
ownership or requires zero recovered errors. Existing idle-daemon scenario must
also assert original/final pair, not merely record status. Historical results
remain limited to their earlier assertions. Four independent inert PowerShell
finally diagnostics are separate; Ctrl+C timing is a candidate, not established
cause. Fresh owner/state checks are not reusable permission DATA; no cache or
broad gameplay rerun is needed for this acceptance predicate.

Separate gm-linux-powershell-finally-diagnostics selects exactly four inert
Theory cases. Its three-minute protective category budget covers one shared
native preparation (50s bound) plus four25s outer case bounds, not repeated
preparation or game execution. Marker outcomes are diagnostic, not forced to a
predicted Ctrl+C result. Source review also requires a continuation-valid
positive before GREEN so unconditional retained-world refusal cannot satisfy
only the two negative continuation cases; this is not yet added in the frozen
initial causal14 block.

Before the first causal build, reviewer-requested controls extend this frozen
oracle selection to17 cases: unchanged valid continuation must return43,
current guard-only owner replacement must refuse independently of status, and
saved raw/hash tampering must refuse. The unchanged driver currently accepts
both positives and lacks the15 negative gates; expected RED is2PASS/15FAIL,
subject to actual execution rather than asserted as a result. No driver or
production code has changed. The earlier14-case text describes initial source
preparation, not an executed run.

Reviewed sourceae046ad2 executed serially: unit build27.72s (wall28.077s),
42 warnings/zero errors. Official gm-mortal-daemon-settlement-oracle completed
17/17:2PASS/15FAIL,11.0423358s, no timeout/skips/duplicates and complete owned
cleanup. Valid settled and valid retained continuation controls passed; the15
actual-function assertions expose accepted invalid/lying cleanup evidence.
This is causal fixture RED, not an unimplemented parser or preparation failure.
Runner20261117-221724-190-5-197ea7c971174dc882ec1669fdf55bd0-categories;
raw daemon-settlement-red-{build,official}.log remains outside Git.

Separately, official gm-linux-powershell-finally-diagnostics completed4/4 PASS
in12.9764329s with complete owned cleanup, no timeout/skips/duplicates.
All four peers exited0 with EOF/restored termios; each guardian recorded driver0,
ECHILD/reaped1/no emergency/failures/deadline. BOTH direct and nested Ctrl+C
observed child.finally.enter/after-write/exit; nested also observed launcher
finally enter/exit, without launcher.after. Cooperative controls completed body
exit and final markers. Thus this inert experiment does not reproduce ml7 and
does not support a blanket claim that Ctrl+C skips finally. Actual daemon
finalization cause remains unknown; no production fix follows from this alone.
Evidence root powershell-finally/f53d81226f4a4d7884c20e62f4a1b1cd,
runner20261117-221748-951-5-5ce07e5d9b804535bf0a8da67cc8b568-categories,
raw powershell-finally-diagnostic-official.log are retained outside Git.
Driver and production daemon remain unchanged; no live/old-world replay occurred.

Post-RED Python fixture correction is prepared, not yet executed: actual startup
captures raw status/guard bytes and hashes after the daemon Waiting marker and
before Continue/action. Strict duplicate/case-collision-free JSON, exact field
types/version/session/owner and running/active markers are required; continuation
startup cannot reuse its predecessor's owner. After process EOF observation,
raw final status/guard evidence is retained even on read/parse failure and the
same original owner must be stopped/terminalSettled/settled. Both ordinary and
controlled-refusal success require this evidence in addition to their physical
and original-main proofs. No zero-error-counter predicate was added.

Retained-world admission first validates the saved original/final pair and then
reads current final bytes again, requiring byte/hash equality and exact settled
identity before any phase queue/config/process write. Malformed or missing pair
refuses; no canonical/guard writes or status repairs occur. Immutable ml7 probe
is not rewritten. The17 causal tests are unchanged; implementation awaits
independent source review with the separately strengthened idle-daemon fixture.

SOURCE c675ef2c bounded verification: serial no-restore unit build completed
37.98s (wall38.344s),67 warnings,0 errors. Official daemon-settlement oracle
completed17/17 PASS in11.2644542s, no skips/duplicates/timeout and complete
owned cleanup. Both valid physical+logical close and unchanged continuation
remain accepted; all15 invalid/lying predicates now refuse. Runner
20261117-222702-064-5-5e40472e59d8423585e645305b241770-categories. Historical
2PASS/15FAIL remains separate; diagnostic4 was unchanged and not repeated.

The separately strengthened coordinated-stop attempt executed1/1 FAIL in
43.3040282s with runner-owned cleanup complete, no timeout/skips/duplicates.
This is a fixture preparation failure, NOT a causal daemon finalization result:
CaptureDaemonSettlement before-stop used File.ReadAllBytes on the active
exclusively flocked guard; SafeFileHandle.Init refused file sharing before
Ctrl+C or the final settlement assertion. The status raw bytes were retained;
the guard read exception was retained. Sixteen original pins were Observed.
The subsequent host disposal and join failed; guardian recorded driver1,
ECHILD/reaped1/no emergency/failures/deadline. Do not claim logical cleanup.
Runner20261117-222721-397-5-a6a511805f9f4a99934359456652efc3-categories,
scenario production-main/7a2fe8b27a9f472fa9dc524a5c6b5fe3. A separate read-only
post-failure capture retained running/terminalSettled:false status and active
settled:false guard with matching owner aabc42994cae49449be2fb1684a82c7e;
it is not an original successful final observation. Raw logs and capture remain
outside Git under relay-alternative/daemon-settlement-{green-build,
green-official,idle-official}.log and daemon-settlement-idle-after-failure.
No retry, live run, world alteration or production correction followed.

A bounded test-only reader correction is prepared and UNRUN: FileAccess.Read
with FileShare.ReadWrite observes the held daemon guard without requesting the
conflicting managed shared lock. New gm-daemon-settlement-observation selects
one exact Fact (one-minute category): real guard acquisition, exact record and
unchanged-byte reads, original validation, and contender refusal before its
status-reader callback both before and after observation. The explicit normal
MarkSettled operation remains separate from observation. No production lock or
authority behavior changes. The prior coordinated-stop preparation failure
remains recorded; source review and selected verification are still pending.

SOURCE de2ba039 shared-reader candidate was built serially with no restore:
36.85s (wall37.191s),67 warnings,0 errors. The official one-case
 gm-daemon-settlement-observation proof executed1/1 FAIL in11.8538620s,
with complete owned cleanup and no timeout/skips/duplicates. FileShare.ReadWrite
still refused the active flock at FileStream construction (SafeFileHandle.Init),
ReadDaemonObservationBytes line11, first observation at test line27. Thus the
proposed managed sharing-mode correction is disproved on this runtime; it is
not a successful observational proof. No ownership unlock, fallback or retry
was attempted. The strengthened coordinated-stop case was NOT rerun because
its prerequisite failed; oracle17 and diagnostic4 were also not repeated.
Runner20261117-223642-889-6-be1e2e8cbfd8464c8db99ab4b25f373d-categories;
raw daemon-observation-green-{build,official}.log remains outside Git.
Production and all live worlds remain unchanged; no live run occurred.

## WIP: readonly daemon-guard observer candidate, 2026-11-18

#1589, test-only candidate; source review pending and runtime **UNRUN**.
The strengthened idle control's first preparation failed before Ctrl-C because
`File.ReadAllBytes` requested incompatible managed sharing on the held guard
(root `7a2fe8b27a9f472fa9dc524a5c6b5fe3`). The subsequent `FileStream` read with
`FileShare.ReadWrite` was independently disproved by the narrow held-guard test
at source `de2ba039`: 1 executed / 1 FAIL, 11.8538620 seconds, complete runner
cleanup. Neither result is a daemon-stop result; both remain preserved.

The new candidate uses one trusted source-root Python observer shared by the
held-guard proof and actual idle snapshots. It opens only for reading with
NOFOLLOW/NONBLOCK, rejects nonregular inputs, and never requests or releases an
advisory lock. Production guard/API, owner admission and daemon behavior remain
unchanged. The C# caller requires real exit and full stdout/stderr drain within
three seconds, with a separate bounded two-second cleanup; complete raw streams,
script hash and error outcome are retained. Existing strict owner/session/version,
exact guard shape, duplicate rejection and raw-persistence failure gates remain.
The proof still requires original Validate and contender refusal before its
status-reader callback both before and after observation, with unchanged bytes.
No idle rerun, new live phase, retry of ml7 or successful settlement is claimed.

Optimization assessment: the idle observation needs only four small reads across
two lifecycle boundaries. Mutable owner/status bytes cannot be reused between
those boundaries; no cache, heavy package preparation or new general process
framework is added. Existing source/runtime failures and ml7's unconfirmed daemon
settlement are not reclassified by this candidate. Next gate: independent source
review, save, one bounded held-guard proof, then the strengthened idle control only
if that proof succeeds.

SOURCE a42668d443d011bc1f8a17ec6b1fb55d9c76cf5e: serial no-restore unit
build42.43s (wall42.762s),67 warnings,0 errors. Official observational-reader
proof1/1 PASS9.7098909s; only then separately strengthened coordinated-stop
1/1 PASS34.6085241s. Both selections completed with no timeout/skips/duplicates
and complete owned cleanup. The earlier two managed-reader failures remain
recorded. Oracle17 and diagnostic4 were unchanged and not repeated.

Actual idle scenario production-main/b7b508201ac447379f58cf68b0d6d50f captures
the original running/active daemon pair and exact final stopped/terminalSettled
and guard-settled pair for owner9762d92ca1964028ae2bcb1cff42924c, same session.
Final status SHA25613b092416ff345b66cda4c0b11832411b307f3b91b1a3e09aa3206d73fbb5dc4;
final guard SHA2568a4a361c80353c65f4eb7390b5b29197a4632774dba70be7cc966c7e34b42b7f.
All17 original pins were Observed after original shutdown and daemon exit.
Original main d0b76c1052ba44d7ac0f523672dc073f, generation
4e8f836aac964e1b888e2c56219f8090, epoch1 had matching positive ACK and complete
Stopped identity before the daemon Ctrl+C. Daemon exit0/EOF/restored termios,
host exit0; guardian driver0/ECHILD/reaped1/no emergency/failures/deadline.
The daemon retained errorCount1 and recovered `Original admitted daemon read
refused.` during close; this is not an error-free-log claim. This bounded idle
success does not explain or erase the previously unsettled ml7 live result.

Runner proofs:20261117-224521-717-6-9c555599e75545f1873474be8ce1909a-categories
and20261117-224543-026-5-00533099d6f84e3fb485e4b8b76106d6-categories.
Raw daemon-observation-python-{build,proof,idle}.log and all original captures
remain outside Git. No production behavior change or live run occurred in this
block. Outputs are frozen for independent outcome review; relay-only merge
must exclude unverified materialization work and raw diagnostics.

Fresh idle fixture-preparation.json SHA256
99c5851d8bfb4fa33c323b562b346f8bbe8486aff676aa1a41abb425eefbd26d;
neutral CLI source e6fad1618cdbf2255f99a0eee6f88064236f118b77b54282c25fd647067b6616,
compiled binary6fab87e76d57eb09d2164b54438e0cb906de9c963eb628cbc2c313bcdaa13d1f.
Preparation records publisher-only compile and compiler absent from player
startup. The recovered read refusal is the expected closing-fence condition
in this bounded successful stop, not a claim of clean error counters. The
unsettled ml7 result remains unresolved.

## WIP: general relay same-world continuation harness, 2026-11-18

Relay-only candidate based on main `605ec71`; no Mortal seed/exporter/receipt helper
is imported. `live-game-one-turn.py` now has an explicit relay continuation argument
`PHASE --codex-relay PREVIOUS_PROBE`. Initial play remains ordinary Chaos Sea UI
creation with the existing Elyara preset; continuation performs ordinary Continue
in the same world, followed by one newly authored player question and actual GM
response. No spiritual combat or materialization implementation is added.

Before phase effects, a prepared `relay-phase-package.json` must exactly pin
`RuntimePins`, `RelayPins` and `DriverSHA256`; continuation additionally checks the
prior successful physical/logical/daemon settlement, exact retained story, world,
config, runtime, main record and absence of pending work. Only queue/pipe/cwd config
fields change on continuation; other profiles/settings remain. Actual request bytes
are captured once; queue turn/repair witnesses, prompt dispatch, executing response,
consumer exit/drain and exact appended story narrative are joined. Initial/final
daemon owner settlement and all three original peer exits are mandatory. Native
bootstrap path length is checked before effects. This is source WIP, not live
qualification; pristine two-phase restart/stop remains UNRUN.

Actual original driver's AST success predicate causally failed one source-only
assertion: it accepted a result with physical EOF and bridge stop but no daemon
settlement. This is distinct from initial missing-new-function test setup errors.
After the candidate change, nine source-only tests pass, including positive retained
world admission and changed-byte/pending/guardian refusals, stale daemon evidence,
exact story append, config preservation and turn/repair execution correlation.
Official bounded category/build and independent review are pending. Existing
nonrelay/provider-refusal routes are retained and are not newly runtime-qualified.

Optimization: immutable package pins are retained as DATA within a phase; current
world/owner inputs are freshly checked before continuation and never reused as
admission authority. No additional game preparation, exporter or broad suite is
introduced. Raw diagnostic output remains uncapped; old failed worlds are untouched.

Independent source review found two harness defects before live use: a new durable
main could coexist briefly with the predecessor's status endpoint, and a forensic
correlation failure could occur before the fresh player prompt updated cleanup
state. Two actual-AST causal controls failed on those original expressions/blocks.
The startup bound now requires matching new main run and configured pipe before
any RPC; observed prompt plus absent pending work updates the safe exit state
before fallible correlation, without granting accepted-turn qualification. Eleven
source-only controls pass after these corrections (0.048s); official wrapper and
live execution remain UNRUN. Production behavior is unchanged by this test change.

Relay-only candidate build gate: serial offline no-restore Unit build completed
91.24s (84 warnings, 0 errors), Integration 59.76s (124 warnings, 0 errors).
All 3339 compiler-source/project/props/targets hashes stayed unchanged; 28 DLL/PDB
pins were recorded and shared Client/Bridge/TestSupport copies agree. Official
`gm-relay-phase-contract` at cb74a48c passed 1/1 Fact in 10.5950641s, containing
11/11 Python checks (0.032s), with exit0/full streams and complete cleanup.
Independent review accepted both corrected source gates. No live/merge acceptance.
Next bounded composition qualification is three explicit serial groups: transport
(FIFO/pipe grammar/Desktop/inherited terminal), lifecycle (singleton data/scripts,
held observation, launcher and contender restart, coordinated stop), and affected
consumers (retired registration/readmission/sticky cleanup/neighbors/watcher and
existing main template rendering). No aggregate/default suite or hosted CI runs.
Each group retains its category budgets and fresh owned fixtures; current compiled
outputs are reused only with unchanged source pins. Runtime results remain UNRUN.

Relay-only composition runtime gates completed without rebuilding: group A
100/100 PASS (58.0486087s; three descriptors, overlapping FIFO/grammar deduplicated),
group B40/40 PASS (63.7507296s; six descriptors), group C17/17 PASS (62.0030248s;
eight descriptors). All157 distinct cases had complete runner-owned cleanup,
zero skips, duplicate IDs and timeouts. This is the explicit affected-contract
selection above, not a whole suite. Before and after runs, 3339 compiler-input
pins and28 DLL/PDB pins remained exact. Prior phase wrapper1/1 is separate.
B's strengthened idle daemon retained the same owner40415093f105466c9855f40181b51842
through stopped/terminalSettled/guard-settled; recovered errorCount1 is retained.
Original scoped main closure and guardian0/ECHILD/reaped1/no emergency were
observed. B's separate actual contender/restart scenario also succeeded with
its own guardian0. Raw and selection inventories remain external as
relay-only-gate-{a,b,c}.log and corresponding -pins.json. Independent outcome
review is pending; no live-game or merge acceptance follows from these gates.

Fresh pristine rl1 preparation is separate and UNLAUNCHED: no-build/no-restore
publishes from the candidate produce729 runtime files, with8 maintained relay
files and4 source-matched native files. The current daemon and launcher match
source exactly, no finalization instrumentation is active. All28 output and3339
compile-input pins were rechecked. New p01 world contains only _runtime, no
config/game_state/queue; its actual package preflight passed. Source-rooted
launch and private environment are prepared; native bootstrap path is88 bytes.
External preparation manifest SHA256
6d2a00bc4a84cb8a00b149601f50d942db2423d754924782c9c3fbddeb187eee.
Independent preparation review and genuine first/retained second GM responses
are still required. Historical ml7/e uncertainty is not declared repaired.

First pristine rl1 p01 attempt completed32.292s and FAILED before any relay
request: one real player action was entered, zero turns accepted, queue closed
without a request or GM-authored packet. The ordinary client displayed its
pre-turn contract-validation refusal ('Ход не принят'). The detailed issue was
not exposed by the normal AddDebug-only logger; no error_log existed. This is
not successful live relay qualification and no action is replayed.
Nevertheless this original run's physical and logical shutdown completed: client,
bridge and daemon exited0 with EOF/restored terminals; exact original main
c3bbc9945a504a85865cf0ea6dc9c216 received scoped positive ACK/full Stopped.
Original daemon owner8e205159a87c42ab8e79f6ea8c61d867 changed from running/active
to stopped/terminalSettled/guard-settled. Guardian reported driver1 (failed test),
ECHILD, reaped1, zero emergency signals/failures and no deadline. These shutdown
facts do not turn the failed game attempt into PASS. Original exec58553 ended;
world and raw evidence remain untouched. A copied-state diagnostic of the exact
pre-turn validation path is being investigated; no production fix or new launch
is authorized by an assumed cause. Main remains unchanged and merge is blocked.

Bounded post-failure diagnostic completed once: the exact deployed public
ValidateGameStateAsync entry returned exit0 in7.914s, empty stderr, seven
resource_owner_vehicle_receipt_invalid errors. All79 non-runtime files, including
.boe_runtime records, were byte-identical before/after; no initializer, repair,
new game or replay ran. The hardcoded vehicles.json.birthReceipts error path was
misleading: vehicles.json was absent, as in the earlier ordinary Chaos fixtures.
Source inspection found ValidateMetaMiscContract dispatches ValidateVehicleData
for each shared meta/misc object, while ValidateVehicleData unconditionally calls
MortalVehicleBirthReceiptState.ValidateLocal for every JsonObject. Thus unrelated
meta roots are incorrectly required to carry vehicle receipt arrays. Exact main
605ec71 has this same code; the relay-only partition preserved it unchanged.
A narrow canonical-VehiclesPath scope guard exists in the excluded mixed work;
it is not imported wholesale. Causal tests and independent review will qualify
only the minimum existing-validator correction needed to reach a live request.
No vehicle gameplay, spiritual combat or other materialization feature is added.
The original failed world is preserved; no new launch occurred during diagnosis.

Test-first prerequisite source is frozen, production still unchanged. The narrow
vehicle-receipt-validation-scope category owns11 public-API cases: three unrelated
canonical roots, four real canonical vehicle receipt shapes, three ordinary or
command-only responses, and invalid numeric activeVehicleChange preserving its
own rejection. Only the spurious receipt error is excluded for unrelated inputs;
other unrelated validation findings are not treated as whole-state acceptance.
Official causal RED is UNRUN. Fresh tiny independent state is necessary per case;
no authority/data cache or heavy game preparation is introduced.


### #1589 launch prerequisite: canonical vehicle receipt dispatch scope, 2026-11-18

The pristine rl1 attempt stopped before a GM request. A single authorized public
validator diagnostic on the settled world returned seven identical
`resource_owner_vehicle_receipt_invalid` errors; all 79 non-runtime files,
including `.boe_runtime`, stayed byte-identical. This is an inherited main
`605ec71` defect: the shared meta validator required vehicle receipt arrays on
unrelated objects. An absent vehicle root is not itself invalid in Chaos Sea.

Official causal RED at `42b179`: 11 cases, 4 PASS / 7 FAIL, 18.2176326 seconds,
complete cleanup, no skips, duplicates or timeout. Four canonical vehicle shape
controls passed; three unrelated state objects and four response controls failed
only the unwanted receipt requirement. The invalid response command still
produced its own `active_vehicle_change_invalid_type` error.

WIP source fix restricts only `ValidateLocal` to the exact canonical
`StorageTransportMoveService.VehiclesPath` dispatch context. It does not return
from the surrounding method, change command validation, add realm/field guesses,
rewrite bootstrap state, or expand vehicle gameplay. The dispatcher supplies the
actual canonical path for file objects and `response` for response commands.
The unchanged 11 tests are awaiting GREEN; no new live attempt has run.

Optimization: avoid redundant receipt parsing/checks on unrelated meta objects.
This trivial dispatch predicate needs no new cache; mutable validation authority
is still checked by the existing path. No speedup measurement or broader feature
acceptance is claimed. The original rl1 failure and diagnostic evidence remain
outside Git and are not erased by this candidate fix.

Source6e946e8 canonical scope correction passed the same official11 cases:
11/11 PASS17.4027833s, complete owned cleanup, zero skips/duplicates/timeouts.
The earlier4PASS/7FAIL remains causal evidence, not a preparation failure.
Unit rebuild51.30s (17 warnings,0 errors); Integration incremental refresh1.48s
(0 warnings/errors) did not recompile unchanged integration sources. Updated3340
compiler inputs and28 DLL/PDB joins agree across project/unit/integration outputs.
The prior157 unrelated relay composition cases were not repeated for this isolated
validation dispatch correction. Updated output-manifest SHA256
96a3f81e92c72c2de388f5b2f449d8471dc6aeaef6b224d3388da99226349334;
compiler-input manifest85370efbfa582964cdf8e1af86a4406d4a28dfb8e636c1a3e94c22b77411aed1.
Original rl1 runtime/world remain unchanged. Independent outcome review and
fresh actual relay turns are still required; main has not changed.

A second single public-validator diagnostic used the corrected candidate DLL on
unchanged settled rl1 data without replacing its runtime. It returned [] (zero
issues), exit0/empty stderr in7.807s; all79 state/ownership files were unchanged.
The first seven-error capture is retained separately. This confirms the exact
observed pre-turn blocker on the same data, not a replay or accepted game turn.
Fresh rl2 preparation from corrected outputs is UNLAUNCHED:729 runtime files,
8 maintained relay files,4 matched native files,3340 source and28 output joins;
Client827c786e is consistent in all published copies. Package preflight passes,
world contains only _runtime, private environment0700 and native path88 bytes.
External manifest SHA25695c4097e0c398debba84d9cd7bff0a3c245edbc68135c85ba7a5a5abb7f1f305.
A preparation-metadata missing-field error was corrected without rerunning any
game or changing package bytes. Independent preparation review and live phases
remain required; rl1 is not continued and main remains unchanged.

### #1589 rl2 original pidfd observation failure, 2026-11-18, WIP

Independent prerequisite GREEN and package review accepted the preceding bounded
results. The pristine rl2 p01 launch then ran once using the saved 67b379 candidate
and original guardian. It reached a real GM relay request and sent one player
action, but accepted zero turns. Original driver completed in 126.833 seconds
with `Original owner uncertainty`; no second phase was started. The GM authored
no response packet or canonical writes: its later submission was refused as
RelayClosed after the original request disappeared.

The first specific ownership failure is `host-exit-observation-lost` from
NativeLineageOwner observing the original transferred pidfd. The last healthy
RPC and first uncertain RPC are 53 ms apart; uncertainty preceded the fixture's
first cleanup ESC. Subsequent status-publication admission and native-stop
failures are not proof of an earlier publication defect. The saved report does not preserve
native errno/revents or the original exception, so EINTR is only a hypothesis,
not the established cause. No environment restriction or fix is claimed.

The independent guardian finished with ECHILD, driver exit 1, two reaped children,
zero emergency signals/failures/deadline. This physical drain does not qualify
logical cleanup: daemon exit 1, client/bridge exits and foreground EOF unconfirmed,
original owner retained Uncertain, daemon settlement unconfirmed. Preserve this
failed world and do not replay or continue it. Main remains unchanged; merge is
blocked until genuine accepted turns, same-world restart and normal settlement.

Next: inspect the original observation contract and cheaply reproduce its error
classification, preserving exact failure diagnostics before another live run.
No new cache is appropriate for fresh pidfd/ownership observation. Reuse existing
immutable build evidence only where inputs match; do not cache authority. Raw
rl2 logs, queue, request and guardian records remain outside Git. Independent
failure/source investigation is underway; no broad tests or hosted CI ran.

Independent forensic review confirmed the ordering: last certain status111.557s,
first uncertainty111.610s, relay closure111.620s, client cancellation111.621s,
sole shutdown118.625s. No queue reply/response/started/execution exists. The first
recorded stop stage is downstream of RootExited fault; it is not the original
system-call cause. No evidence proves EINTR, kernel termination or an environment
restriction. Because poll timeout is zero, ordinary blocking-poll signal behavior
is not evidence for this failure.

The next bounded implementation is diagnostic-only: retain original poll result,
revents and immediate errno only on negative return, plus the original fault's
type/message/stack. Read only the already-faulted retained RootExited task through
the existing nonce-bound diagnostic path. An instance-scoped typed seam will test
negative/error masks, pending and normal IN/HUP observations without changing
classification, retry policy, handle lifetime or ownership. No new cache, native
probe loop or live replay is justified. Actual implementation and tests are UNRUN.

Diagnostic-only RED source `3be42070` passed independent source review. The original
pidfd wait loop is extracted behind an immutable per-instance poll adapter with
unchanged AddRef/finally, failure classification and delay. The capture stub is
intentionally absent for causal RED; it is not a runtime fix. Official
`gm-host-exit-fault-diagnostics` completed 9/9 cases: 4 PASS, 5 FAIL in11.6885700s,
complete owned cleanup, zero skips/duplicates/timeouts. Five failures are exactly
missing captured fault assertions (four poll failures and disposed-handle AddRef);
three normal IN/HUP controls and nonfault task controls pass. Unit build91.01s,
91.459s wall,84 warnings/0 errors. No Integration rebuild or live run occurred.

GREEN will retain original fault data and wire it to the existing nonce-bound
stop report. Typed fault-to-report serialization plus existing healthy original
owner/nonce controls suffice without adding native authority-injection machinery;
this does not claim a nonnull live export. The historical rl2 errno remains unknown.

### #1589 original host-exit diagnostic GREEN, 2026-11-18, bounded result

Source046de4d9 retains poll failure DATA on the original exception, then captures
only the already-faulted original RootExited task through the existing nonce-bound
report. No retry, liveness decision, handle lifetime, ownership or cleanup rule
changed. Independent source review found and corrected a diagnostic status/fault
snapshot race: snapshot the task status once and capture only if that snapshot is
Faulted. The reviewer accepted that correction. Existing finite report-size and
text bounds remain limits; no nonnull live nonce export is claimed yet.

Fresh Unit build89.88s/90.190s wall,84 warnings/0 errors. Official categories
`gm-host-exit-fault-diagnostics` and `gm-main-stop-first-failure-diagnostics-linux`
selected19 cases, not the initially estimated17 (one existing theory has3 cases).
New diagnostics13/13 PASS; existing category5/6 PASS. Total18/19 in24.1604329s,
complete owned cleanup, zero skips/duplicates/timeouts. The failed unchanged
ActualBridge_DurableOriginalOwnerBeforeInteractiveReleaseAndScopedRetirement
fixture directly uses NamedPipeClientStream and encountered SocketException13
at socket creation in OwnedTerminalScenarioDriver146/201. It had an original
Running owner and then ScopedRetired=true; its guardian reported ECHILD/driver1,
zero emergency signals/failures/deadline. This legacy socket route is not the
new FIFO transport and is not reclassified as PASS. Both actual native nonce
failure-report cases passed with guardian0; three cancellation cases also passed.

The13 include exact poll tuples, normal IN/HUP, disposed AddRef, task-state
controls, typed original-fault-to-real-state-JSON composition, hostile/large fault
incompleteness and once-only pending observation. This qualifies diagnostic DATA
capture, not rl2's unknown errno or complete live restart/settlement. Independent
outcome review is pending. No Integration refresh or new live launch occurred.


Independent outcome review accepted the bounded diagnostic block: both TRX and
all three native scenarios were checked, including actual healthy nonce reports
with RootExitFault:null and guardian0. The legacy NamedPipe failure remains a
distinct unavailable control, not a required fix for this diagnostic-only change.

### #1589 targeted original-owner FIFO observation design, 2026-11-18

Before another game, reuse the real typed consumer fixture for one explicit
120-second original-owner observation. Fake poll tests cannot establish a real
errno; another full game adds unrelated state and GM preparation. The chosen
neutral FIFO path preserves the actual original owner, public operation pin,
shipped diagnostics, scoped shutdown, durable Stopped, endpoint removal and
independent guardian. A separate Fact/class/category avoids silently adding a
long test to the existing FIFO wildcard. No new ownership framework is needed.

Observe the same original RunId and actual FIFO status approximately every5s;
preserve each complete reply and first failure outside Git. Never replace a lost
owner, reuse authority, or turn a failed observation into success after cleanup.
Attempt at most one stop of the captured original owner, never a foreign current
owner. Keep the diagnostic nonce through that attempt. Explicit fixture budgets:
neutral180s, driver160s, guardian170s, outer175s and category4minutes including
native preparation. Existing modes retain their old bounds.

Independent design review accepted this targeted reproduction. Source changes
and execution are UNRUN at this checkpoint. A PASS means only no recurrence in
this120s neutral path, not an rl2 fix, game acceptance or merge readiness. Reuse
existing immutable native/package preparation when its full source identity
matches; fresh owner/endpoint observations cannot be cached. No game fixture or
additional system is introduced.

Soak source c42ab821 is independently reviewed and remains UNRUN. Review added
terminalOwnerRetained to every sample, saved the diagnostic nonce before host
construction, closed the exact owned pin before failure shutdown, and reduced
the work cutoff to140s. Reserved cleanup is5s pin +8s original scoped stop +5s
Dispose observation +5s host join, below170s guardian with7s margin. Any timeout
remains FAIL; timed-out Dispose can continue in the fixture process, and restoring
the nonce may suppress a late export, so absence of such a report proves nothing.
The120s observation holds the original HOST after its operation pin has closed.
Shutdown uses the public connector with captured endpoint and full identity, not
a generic launcher that could reread a different current owner. Existing fixture
modes and the broad FIFO wildcard remain unchanged. Only the separate one-case
`gm-host-exit-original-fifo-soak` category is authorized for this reproduction.

The first soak preparation build failed before any native execution:8.32s,
23 warnings and CS0103 for GmSessionRunPersistence in the new partial file.
Adding only `using BookOfEternityClient.Core;` fixes name resolution; independent
review verified removing that line reproduces the exact previously reviewed
source hash. This is a preparation correction, not a causal runtime RED. The
failed build is retained outside Git; the one actual soak remains UNRUN.

### #1589 original FIFO observation: one PASS, 2026-11-18

Corrected source2f9bf11 built Unit successfully in39.50s/39.756s wall,
65 warnings/0 errors. The sole official `gm-host-exit-original-fifo-soak` case
passed1/1 in133.1912340s with complete owned cleanup, zero skips/duplicates/timeout.
The actual idle observation lasted120.0623264s and retained25 full status replies
for the same original run7613a6757ffd46f2ab8ac9cd9d4a1494, generation
ff09ab46c0024038b494f85a57a2f5d0, epoch1. All observed the actual FIFO endpoint,
retained owner and no uncertainty. The operation pin had already closed.

Direct connector shutdown addressed that original identity and returned the
original scoped-stop acknowledgement, stopped-within-scope/stop-requested,
cleanupComplete=true and authorityRetained=false. Durable original Stopped and
endpoint removal passed. Independent guardian reported ECHILD, driver0, reaped1,
zero emergency signals/failures/deadline. This is a neutral-process result, not
a game turn, a reproduction of rl2's unknown fault, or proof of its repair.

Twenty current output pins and seven source/checkpoint pins were rechecked;
external manifest SHA256e9706bb675b287108d47b218af8c5a9008cabed216fef3280253b427add28556.
No Integration refresh had occurred at this result. Independent outcome review
is pending; next prepare fresh matched game outputs and a new isolated world
with the diagnostic-enabled original observer. Never continue rl2 or infer that
its failure was caused by a timeout merely because this longer observation passed.

Independent soak outcome review accepted all25 samples, full original shutdown
proof, durable identity/OwnedScopeEmpty and guardian evidence. Fresh Integration
build then actually recompiled in59.37s/59.660s wall,124 warnings/0 errors; no
tests ran. Current3344 source inputs and28 output joins match; external source
manifest SHA2563d38a05d5d8ba5849f5ead307ba3243d74b9589cfed60be3457d268e1a322c10,
output manifest fad70d3e8508a05a94173ab0466a7684829410e7bbb91f0581f428bdb2a027bc.

Fresh rl3 p01 is PREPARED, NOT LAUNCHED. It contains729 matching runtime files,
8 maintained relay files,4 native source-matched binaries and6 deployed native
copies; new Clientf79dba9b, Bridge2a091c40 and Support0c65ea09 agree with current
outputs. Source-bound PowerShell and driver2f7aa13b remain unchanged. The world
contains only _runtime; private environment directories0700, no symlinks, native
path88bytes. Original guardian750s/driver720s bounds remain unchanged. Preparation
manifest SHA25698daf799c093106694f23bae346632d90a8d1d000e1083bbc5b7d729bfef3509;
phase package3993cd8b56a87daa6986f2c21c16c709870a484fe6beb8dd1daaeffd8fb58767.
Independent preparation review is pending. Previous worlds remain untouched;
this fresh game will use the diagnostic-enabled observer, without claiming any
production fix for the unknown rl2 failure. Main remains unchanged.

### #1589 rl3 accepted live turn, failed shutdown, 2026-11-18

Independent preparation review accepted the frozen package above. The original
single launch then completed in 223.853 seconds: one genuine action and one
accepted game turn, but overall FAIL. The fresh assistant-authored GM response
was consumed by the real relay, execution exit 0 with child exit and I/O drain.
No second phase or same-world restart was attempted.

The original nonce-bound stop report records pin_drain_timeout with one remaining
operation pin. At that snapshot the owner is closed/uncertain, the stop lifecycle
is absent, and RootExited is still pending. The final report has RootExited
completed and actual native scoped stop proof cleanupComplete=true, but still one
pin and no stop lifecycle, so logical settlement refuses. RootExitFault is null
in both snapshots: this is not evidence of the earlier rl2 pidfd failure.
The cause of the retained pin and faulted registered loop is under investigation;
no production fix or systematic environment restriction is established.

Client exit 0/EOF/termios and RPC-helper exit 0/both EOF were observed. Daemon
exited 1 and its logical settlement is unconfirmed. Bridge exit/EOF/termios were
not confirmed. Guardian separately reports ECHILD, driver 1, reaped 2, zero
emergency signals, failures or deadline. Physical draining does not substitute
for original logical shutdown proof. Raw diagnostics stay external, not in Git.
No repeat launch is authorized by this checkpoint, no game-materialization
qualification is claimed, and main remains unchanged pending reliable relay
shutdown and same-world restart qualification.

### #1589 deterministic prepared-grant stop race: causal RED

Test-only source 0128f3e6 was independently reviewed before execution. A separate
actual FIFO neutral host holds one real Active control pin, receives a second
PreparedGrant, observes same-owner durable Stopping after scoped shutdown, then
sends the exact late activation. Activation is refused and the control pin's
immutable close is acknowledged before observing the sole shutdown response.
The desired clean-stop oracle fails: original shutdown NACK, first failure
pin_drain_timeout with exactly one remaining pin. This proves a stop/admission
race in the current protocol, not the exact historical interleaving in rl3.

Fresh Unit build: 39.59 seconds / 39.978 wall, 66 warnings, zero errors. Sole
official gm-prepared-grant-stop-race: 0 PASS / 1 FAIL, 27.0152095 seconds,
complete runner-owned cleanup, no skips, duplicates, retry or timeout. Original
native case 737624e621fe4430a7531ef978620646 preserves secondary host disposal
and join timeouts; guardian independently reports ECHILD, driver 1, no emergency
signals, failures or deadline. Twenty output pins remain external, manifest
SHA256 99696eeef666eaeb8e34c67a8e68eef7ead8a51b2dd21866b179822deb2c2a5b.

Next minimal correction must distinguish a reservation revoked by the original
Stop while still PreparedGrant from an Active or already Unresolved operation.
Only the same owner lock may order revocation versus activation. No fabricated
ClosedObserved receipt, active lost-close relaxation, retry or authority cache.
Independent RED outcome review and source review of any correction remain
separate gates; no further game launch or main merge has occurred.

### #1589 stop-fenced revocation candidate: two controls PASS, positive unqualified

Source 15f04630 appends RevokedBeforeActivation without renumbering prior states.
The original BeginStop lock revokes only reservations still PreparedGrant; its
snapshot avoids dictionary mutation during bounded terminal-history pruning.
Active and already Unresolved operations keep their original obligations. Lost
without an actual stop-fenced revocation still fails closed; no client admission
comparison was relaxed. Independent source review accepted this narrow change.

Fresh Unit build: 87.02 seconds / 87.365 wall, 85 warnings, zero errors. The sole
three-case official run completed 2 PASS / 1 FAIL in 47.9790236 seconds with
complete runner-owned cleanup and no skips, duplicates or timeout. Both negative
controls passed: pre-stop loss of PreparedGrant or Active retains Unresolved,
original shutdown NACK and durable Uncertain without StopEvidence, while the
original native scoped physical cleanup is proven. Both managed teardown tasks
completed under the shared 18-second bound; no external wait timeout was accepted.

The positive case 7a459f19ea4c439ea3479b0e2953a270 failed its strict metadata
read before establishing the Stopping/late-activation gate. Cleanup did observe
the control close and positive original scoped stop ACK, but this is NOT a
positive GREEN result. The polling observer can overlap the strict namespace
publication interval, which permits only the publisher's own staging file.
Use the existing typed Readback observation boundary instead; do not loosen the
production reader or swallow/retry arbitrary metadata refusals. The exact
historical transient entry was not captured. Output20 manifest SHA256:
d4bb5aad3232331bd6bb8b6b83cfbba4feeb5192c5d081ab5cf5c5b4902df24d.
Raw evidence stays external; no further game launch or merge occurred.

### #1589 prepared-stop positive GREEN after observer correction

The test-only Readback observer in 3cbd212b was independently reviewed. Fresh
Unit build: 38.21 seconds / 38.434 wall, 66 warnings, zero errors. Only the
positive Fact was rerun: 1/1 PASS in 13.0626468 seconds, complete owned cleanup,
no skips, duplicates or timeout. Original case c6d3fb0fa591459ab8bb0e996497e967
proves exact PreparedGrant, same-owner Stopping Readback, late activation refusal,
two unchanged-identity RevokedBeforeActivation replies (not ClosedObserved),
then the separate active control's true immutable close. Original run
a125dd6a445d4594b3d792e89d387631 reaches scoped ACK, full durable Stopped with
OwnedScopeEmpty, actual host exit 0 and endpoint removal. Guardian ECHILD,
driver 0, reaped 1, no emergency signals, failures or deadline.

Independent outcome review accepted this result. Together with the prior two
negative passes, three distinct scenarios are qualified in two runs; no claim
that all three ran together or that historical rl3 causality is established.
Twenty output pins: SHA256
4f491b52ca30aabf5c78e4319671fd5e9c9705e1fa85da2ce1928db6af532a29.
Integration outputs remain old. Before another game, check only the existing
actual daemon coordinated-stop case and exact FIFO Load stop/finish consumer;
the new narrow catalog entry preserves all existing entries and avoids rerunning
71 unrelated transport cases. Full game shutdown and same-world restart remain
unqualified. Main unchanged; raw diagnostics external.

### #1589 affected neighbors accepted; fresh rl4 package

Only the two reviewed neighbors ran: 2/2 PASS in 41.2634243 seconds with complete
owned cleanup, no skips, duplicates or timeout. Load case4714a96362dc4cdda82d4afc17623f0c
retains the actual original FIFO stream through stop/finish without replacement,
then original shutdown/Stopped/endpoint removal. Daemon case78ae0ebf69d24139a7ceae166586dc1a
closes its actual active pin and confirms original Stopped/OwnedScopeEmpty before
the foreground signal; same guard owner reaches stopped/terminalSettled after EOF.
Its final errorCount is 1, not an error-free claim. Both guardians report driver0,
ECHILD, reaped1 and no emergency signals, failures or deadline. Independent outcome
review accepted both bounded results, not a game or same-world restart.

Fresh Integration build recompiled in64.70s/64.931 wall,124 warnings/0 errors,
zero tests. Current3347 source manifest SHA256
fe67df142f2d88a693b4b90db676352bbdb4ad00ffdab4e6ac7b7099158802cd;
28 output joins a08b6f9ac54597a12d82c58bf5c1f4f6181680d5cb18003b01ac241c4c29b393.
Fresh rl4 package independently verified both runtime729 copies, relay8, native4
source/binary provenance, private0700 environment, no symlinks, pristine daemon
and unchanged maintained driver. World initially contained only _runtime.
Preparation manifest37513091eb2cbf67ed6c399d35df147eb854b4c1b9ab6c17b1ae02a5958c726d;
phase package97efb2ca6b7eda30fdec5a35f86e8a9cb3ab36775fa5611a0ccd51b2aee95d31.
The single original guarded p01 launch is now in progress, not accepted yet.
No p02 is permitted without full p01 success and independent guardian proof;
all earlier worlds remain untouched. Main remains unchanged.

### #1589 rl4 p01 live acceptance and cleanup confirmed

The original guarded launch completed in233.079 seconds and was independently
accepted. One genuine action produced request7e71eddbb67b4f37afd7c7a90747fe2d,
turn1, actual request SHA256
b566e488d526114f095000cd65fd0e5abe9db7efee14d3d72486d368f9eb630a.
A new live-authored response4b7f3eea096b2f444dbb65ec386617ca9d2ca51d3c69134e80b86e0b018b8f6f
was published once, executed exit0 with child exit/I/O drain and accepted into
the canonical story with its exact narrative. No repair or replay occurred.

The sole scoped shutdown at231.336s received its original ACK at231.526s.
Run3280804d657a4db79fceb2f438bd260d, generation71afc6e1e3d44666b88adf96e51ea930,
epoch1 reaches full-identity Stopped/OwnedScopeEmpty on disk. The same daemon
owner fa4f4bfe transitions from running/unsettled to stopped/terminalSettled with
guard settlement and errorCount0. Client, bridge and daemon each exit0 with
EOF and restored terminal state; RPC helper exits0/drained. Guardian reports
ECHILD, driver0, reaped1, zero emergency signals/failures/deadline. External
outcome-pins SHA2568de80a2cf3a0d5aa9e620b2dc226f81fe41c2f589f3542d6e72c7b73b3e0f9e8.
This qualifies the first live phase, not same-world restart. Independent review
permits preparing p02 using ordinary Continue of these exact retained bytes.

Merge selection was reconciled against the entire relay-only candidate. The
39 stale #1515 current selections were replaced, without revoking their prior
coverage. All1776 preceding catalog entries retain exact original text/order;
one five-case supported diagnostic category excludes the unavailable legacy
NamedPipe case. Source review corrected the description to preserve valid dotted
Windows pipe names. Official metadata-only PlanOnly exits0 in12.9960057s,
complete cleanup:23 descriptors,101 unique methods,191 planned cases, zero
duplicates and ZERO execution. This is not a fresh191-case pass. The28 current
output pins stayed exact. External PlanOnly pins SHA256
c5a243c34b52697f964a3cb6846568c6867d868e97e34d1032e3cd849d0524cf.
No hosted CI or full suite ran. Main remains unchanged pending p02 and final
source/remote-tree verification; historical rl2/e uncertainty remains separate.

### #1589 rl4 p02 fails before acceptance; merge remains blocked

After independent continuation preflight, ordinary Continue reached a fresh
turn2 request2e637f9d557f4b4ca5f0049432887ab9 in the SAME retained session. One
new live-authored responseaa7b13fbc3910699550547df73e6ee9a7e0fde8a0e093358472f17d7bdaaacc2
was published and executed exit0/drained; no repair or replay. Execution alone
is not acceptance. Original launch completed274.365s, overall FAIL, one action,
zero confirmed accepted turns: daemon exited during current provider wait.

The primary refusal precedes shutdown: daemon turn processing finished around
241.065s, then read admission/completion refused at255.762s; bridge reported
unconfirmed status publication at255.784s and daemon fatal original-connection
admission at255.971s. First client cancellation was later,256.896s; the sole
scoped shutdown261.083s returned NACK266.309s. Pin-drain timeout with one pin is
therefore downstream, not proof of the already-fixed PreparedGrant stop race.
RootExitFault remains null; original native root eventually completes physical
scoped stop, but logical settlement fails. The initiating helper exception and
exact retained pin identity/state are not present in the current public evidence.
No environment restriction or specific new production cause is established.

Daemon exit1/EOF/termios was observed; client and bridge exit/EOF/termios were
not confirmed. Guardian independently reports ECHILD, driver1, reaped2, no
emergency signals/failures/deadline. p02 is not reusable or eligible for replay.
p01 remains a valid earlier acceptance, but same-world continuation acceptance
and reliable completion remain unqualified. Main is unchanged; investigate the
first failure boundary before any new live launch.

Fresh-context independent GPT-6 Astra High final review found no additional
concrete authority/transport regression in the118-path source delta, but marked
one Important diagnostic-policy blocker: new host-fault text truncation and the
existing same-path stderr size drop can discard original evidence. Correct the
existing observational collector with cheap long Unicode message/stack/export
controls, preserving owner/nonce/getter-failure isolation; no logging framework
or ownership relaxation. That later correction will not be claimed as exercised
by the frozen rl4 runs.

Clean remote ca6d9626 restored into a separate directory:3347/3347 tracked source
inputs exact,28/28 current output hashes unchanged,118 changed paths, no new raw
logs/build artifacts or unverified materialization. Of105 initial exclusions,
103 retain main/absence; only the reviewed vehicle predicate prerequisite and
current selection intentionally differ. This is clean-source restoration, NOT
a clean-build run. GitHub compare:34 ahead/0 behind, base and merge-base605ec71.
Exploratory finally diagnostics and unavailable legacy NamedPipe coverage remain
explicitly unqualified; minor EOF whitespace hygiene also remains to correct.

Independent canonical-story readback clarifies p02: turn2 WAS written, with exact
original action and GM narrative, while turn1 bytes remain exact. Input is now
empty. The probe zero means no confirmed client/lifecycle acceptance, not absence
of game effects or successful rollback. Preserve the changed world untouched.

### #1589 diagnostic full-text collection, RED-ready WIP
The fresh merge review identified application diagnostic collection quotas, not transport/protocol payload constraints: HostExitFaultCapture and coordinator text freezing truncate Message/StackTrace at2048/4096, while Bridge export silently omits a report above32768 UTF-8 bytes. This contradicts the current no-quota raw diagnostic policy.
Only behavior-preserving export extraction and tests are prepared. The new internal WriteStopFailureReport(report,TextWriter) retains the original prefix/JsonSerializer/byte-drop/WriteLine and exception isolation. The production caller still resolves the genuine report through the existing nonce/original-owner gate before calling it. A typed synthetic report tests DATA serialization only; it grants no authority and is never supplied to a coordinator. No new reflection.
New gm-stop-diagnostic-full-text-data selects4 cheap cases: separate original-fault long Unicode Message and StackTrace; large Unicode report exact JSON round-trip; rejecting TextWriter isolation. Expected causal result3FAIL/1PASS remains UNRUN. No native/game activity, builds, or runs occurred in this preparation. Existing hostile-getter and nonce/owner controls remain intact. Old quota-specific test expectations deliberately remain until causal RED.
Optimization: reuse already computed exception strings and the existing serializer/stderr route, no new cache or logging framework. Getter/serialization/sink errors remain isolated. No lifecycle, authority, retry, admission or first-failure decision changes are proposed. All source collection and failure constraints remain distinct from diagnostic storage policy.

Coordinator text freezing is exposed internal, body unchanged, for the same two typed rows. Initial HostExit assertions fail first, so coordinator checks remain unreached until GREEN.

### #1589 neutral nested-reader fixture WIP, unrun
Six fixture/wiring sources prepare actual FIFO owner plus shipped PowerShell nested snapshots, exact original close, fresh heartbeat operation and both server ClosedObserved receipts before existing scoped stop. This covers reads, not status writes or the historical p02 initiating cause. No production first-loss instrumentation yet. No new native/game run; source review pending. Quota DATA build overlapped preparation of these sources; resulting build cannot be attributed solely to db155599. Fixture source and emitted PDB must be reconciled before its own run. No new cache: fresh ownership is the tested subject; reuse existing neutral host and shutdown infrastructure.

### #1589 diagnostic causal RED and minimal full-text correction WIP
Official gm-stop-diagnostic-full-text-data:4 completed,3 FAIL/1 PASS,11.6143557s, complete owned cleanup, no skips/duplicates/timeout. Exact failures are absent export above32768bytes and truncation at2048/4096; rejecting sink passed. Build90.38s,85warnings,0errors; exact affected compiled PDB hashes match saved db155599 DATA/capture/export inputs. Coordinator assertions were not reached after earlier HostExit RED, not independently RED-qualified.
Now remove only those diagnostic quotas and retain original exception strings. Existing getter isolation, nonce/original-owner checks, serializer/sink containment and actual refusal are unchanged. Typed hostile coordinator controls add2cases; existing13 host-diagnostic cases retain count with old overflow expectation replaced by second hostile getter. Updated existing assertion helper uses typed collector instead of private reflection and accepts full text. This correction is UNRUN and does not fix p02 lifecycle. Nested fixture cleanup now independently observes exit/pumps/flushes under shared5s and preserves primary/secondary failures; no native execution yet. Minor trailing EOF blank removed. No new cache or logging framework; already computed immutable strings reused, fresh authority unchanged. Raw remains external.

### #1589 full-text GREEN and neutral nested read accepted
At e45056ba, Unit build88.89s/85warnings/0errors. Official gm-stop-diagnostic-full-text-data plus gm-host-exit-fault-diagnostics:19/19 PASS (6+13),13.9391825s, complete owned cleanup, no skips/duplicates/timeout. Exact affected PDB source hashes match frozen source. Both text collectors now execute full Unicode equality checks; hostile getters and sink failure remain contained. Diagnostic quota Important is corrected; this is not a lifecycle fix.
Official gm-main-nested-heartbeat-fifo:1/1 PASS14.2370389s, complete runner cleanup. Native identity f5bd08d02bc24a2f977a881761ecc698 observes actual FIFO, nested original pin3be493 and fresh heartbeat pin199d7 under same owner e57f607678a74c1d8450afbad6483048. Both exact server ClosedObserved replies, helper exit0 and both pumps drained with empty cleanup failures. Independent launcher shutdown ACK, original Stopped and endpoint absence; guardian ECHILD, driver0, reaped1, no emergency/failure/deadline. This qualifies nested reads only, not statuswrite, latency contention or historical p02.
Source corrections independently accepted by fresh-context Astra High reviewer; outcome review pending. No new game launch. Source inspection identifies a testable pre-ready window: remote Active may exist before canonical lease and helper ready, while PowerShell is already timing its10s response wait. Similar canonical retry duration does not prove loss: clean refusal may still close correctly. Need original stage evidence and controlled ordering, not a guessed delay or timeout increase. Historical first fault remains unidentified. Main unchanged.

### #1589 first-operation-failure observation DATA baseline WIP
Prepare one unused typed diagnostic helper and11 small DATA cases under gm-main-first-operation-failure-data. Current Capture deliberately returnsnull as the previous lost-evidence behavior; expected3nonce-refusal PASS/8missing-observation FAIL remain UNRUN. Explicit original owner/pin/close fields are observations, not grants; unknown owner staysnull. Complete Unicode text/stack, precise stages, independently hostile getter capture, sink isolation and changednonce controls. No runtime callers yet, protocol/stdout/timeout/ownership unchanged. Existing nonce and stderr route only, no retention or cacheframework. Earlier quota19/19 and neutralnested1/1 outcomes independently accepted.

First-operation diagnostic DATA RED at66b695c0 completed: Unitbuild91.34s,85warnings,0errors; official11cases3PASS/8FAIL9.9334263s, complete owned cleanup, no skips/duplicates/timeout. All8 fail at absent observation Assert.NotNull, not compile or preparation. Disabled/malformed/stale nonce controls pass. This proves missing DATA capture only, not a historical transport cause. Next implement observer wiring without changing state transitions or timing, then validate exact first-failure stages before another live run.

### #1589 first-operation observation wiring WIP, unrun
Ten source files implement complete nonce-gated observational capture at server original pin failure, client handshake/active close-reply reader, helper command/body/finalization failure, original status publisher and PowerShell firstErrorRecord before secondary status admission. Server immutable pin tuple is copied underownerlock, existing Lost runs before export; original owner is retained rather than inferred fromcurrentstatus. Existing exception text is captured outsideownerlocks; stderr only, no newstorage/retention/cache.
Markers distinguish helper Active, actual canonical-generation lease, ready write and PowerShell replytimeout/stdinclose/beforekill. Managedstderr is buffered by the original PowerShell ReadToEnd and is retrospective, not a live readiness signal. Exact original summary is extracted from separate lines under the unchanged allowlist; duplicates/malformed/embedded summaries refuse classification, fullrawstderr retained. Existing neutralfixture now checks this coexistence. All deadlines, protocol stdout, admission/closure/uncertainty decisions and actual operations are unchanged. No claim of production firstfault qualification or p02 repair; source review and focusedGREEN/nativecontrol pending.

Scoped review rejected two misleading observation boundaries before runtime: helper ready-time projection was labelled TerminalClose, and first open ErrorRecord was exported only after potentially failing Dispose. Both are corrected: separate OriginalCloseProjection/actualTerminalClose, original error before cleanup plus postcleanup raw observation. Diagnostic callback omitted when nonce absent. Capture-entry UTC/monotonic values precede exception string extraction; informational timestamps are not original-event timestamps or authority. Same11 DATA cases now assert projection/actual distinction, absence of actualclose beforecompletion and timestamp bounds. Initial101bd35e build90.55s/85warnings/0errors is superseded without testexecution; re-review and rebuiltGREEN still pending.

### #1589 bounded first-failure observation qualification
Corrected faadf9b0 source independently accepted. Unitbuild92.70s/85warnings/0errors;8 affected compiled PDB source hashes match reviewed inputs. Official11DATA+1neutralnested PASS12/12 in19.0591410s, then existing prepared-stop/loss PASS3/3 in45.8853825s. Both selections complete, no skip/duplicate/timeout, owned cleanup complete. New neutral fixture19809659dd6a4b57b8c45289b6410fc0 executes exact-summary coexistence/refusal controls and real nested helper read/close/freshheartbeat, same scoped cleanup.
Raw producer records independently inspected by coordinator: prepared-loss5e7a5a4d emits activate-read with pre-Lost PreparedGrant0 under original ownerbb39e65b; active-lossdcdda9c4 emits close-read/Active1 owner27daf994; late activation33aadc02 emits activate/RevokedBeforeActivation5 owner4d61790c. Each nonce matches actual scenario, frozen pin/owner agree, original failure preserved. Loss scenarios retain Unresolved/Pins1 and refuse logical stop despite actual physical cleanup; positive originalstop ACK/Stopped remains intact. All four native guardians report ECHILD/driver0/reaped1/emergency0/failures0/no deadline.
This qualifies server failure producer and observational noninterference, not every producer: helper command, client-reader and status-publication actual faults remain source-reviewed/sharedDATA-tested, not runtime-proven. No p02cause or repair claim; no livegame rerun. Next bounded genuine-lease contention fixture releases on actual PowerShell stdinclosed event and distinguishes clean failedclose from late-ready EOF/Unresolved. Competing deadlines are not forced to produce a preferred result. Main remains unchanged.

### #1589 genuine held-lease/ready diagnostic experiment WIP
Fixture-only8-file block/source-reviewed for one bounded attempt. Genuine participating holder acquires original canonicallease; one shippedPowerShell helper opens. Normal release is driven solely by exact nonce/PID/helper-ready timeout then stdinclosed marker, not guessed duration. Finally also releases our lease on every failure. Holder task/actualimmutableclose+originalidentity required independently; extra holder loss cannot be relabelled targetPins1. Retrospective exact targetActive and same-process canonicalacquisition afterrelease required; missing ordering or retry-expiry is inconclusiveFAIL, not a reproducedleak. Exact server query classifiesClosedObserved versusUnresolved; latter reuses unchanged negative-stop evidence predicates.
New mode only:30swork,8s child/holdercleanup,18snegativehostteardown; native65sguardian/60sneutralroot/70souter protecttheattempt. Production deadlines/authority unchanged. Category gm-main-held-ready-fifo selects1Fact/2minutes. Build/executionUNRUN. No new cache: actuallease and currentowner are the experiment; reuse existingneutralhost+negative-stop verifier. No p02causalclaim/mainmerge.

### #1589 held-ready coupled contention reproduced, not a lifecycle fix
Saved 2b146a47 fixture built in39.77s,66warnings,0errors. The single official gm-main-held-ready-fifo experiment completed1/1PASS in41.1600474s, complete runner cleanup, no skips/duplicates/timeout. Native e5d9d9bd2a8144fc828ea81248ce18c5 classified UnresolvedActiveAfterTimeout, not successful game lifecycle. Independent outcome review accepted this bounded evidence.
Actual target Active at07:02:18.747984 precedes PowerShell helper-ready timeout07:02:28.556137; stdin closes28.650839 and releases genuine holder lease28.656219. Same helper acquires canonical lease28.753540 and writesready; original target query isUnresolved4. Distinct original holder close is genuinely observed; child exits and both streams drain with no cleanup failure. Original final record remainsUncertain; negative-stop verifier proves physical cleanup while retaining logical refusal/Pins1. Guardian ECHILD/driver0/reaped1/emergency0/failures0/no deadline.
Crucially, same-owner status publication ALSO fails canonical-write.lock acquisition at28.649605, before stdinclose; existing catch marks ownerUncertain before exporting that observation. This is a coupled contention/readiness failure, not proof that readiness timeout alone causes owner uncertainty. Target close-read failure is observed28.777566. Historical p02 firstcause remains unidentified; this experiment does not retroactively diagnose it or qualify actual game/restart.
Next bounded design must coordinate original-helper cancellation/close acknowledgement and analyze status publication contention separately. EOF/timeouts/exit never count as a successful originalActive close; do not substitute arbitrary longer deadlines, replay, stale authority or ignored publication failures. No production correction yet; no new game launch/mainmerge. Optimization: one actual neutral owner/holder is required for concurrency evidence; immutable DATA reuse cannot replace fresh lease/identity checks. Raw remains local, only compact findings published.

### #1589 bounded contention correction design, RED preparation
Independent Astra High architecture review accepts two narrow test-first boundaries, not implementation or feature acceptance. A: only initial sequence0 readiness timeout before any business command may send one existing sequence1 Cancelled close on retained stdin. Lost stays absorbing; consume the original pending read once, validate the ready original tuple and actual terminal receipt, and still return original timeout without invoking the body. One bounded cancellation phase covers writes, replies and cleanup; timeout/EOF/exit never substitute for receipt. Wrong identity, absent ACK, deadline and non-ready timeout remain negative controls. No reopen/replay or arbitrary extension of normal ready deadline.
B: recognize only actual root-bound FileStream constructor sharing refusal before lease acquisition, after admission/ambient/pin unwind. Require actual prior successful original status publication, reset per Open. Preserve newest pending immutable snapshot; defer only to existing Queue/Seal notifications, no autonomous retry/timer or retained live pin. This best-effort diagnostic snapshot may remain stale until the next update; no eventual delivery promise. Initial failure, unrelated IO, path/hook/post-acquisition/publication/cleanup failure remain fail-closed. Deterministic bookkeeping tests complement real lock-sharing and original-owner publication checks; DATA never grants authority.
Optimization: reuse existing pending latest-snapshot coalescing and original transport/reader, no new cache/scheduler. Original lock, generation, identity, close and publication checks stay fresh. Existing held-ready fixture six compiled PDB source hashes match the frozen experiment inputs. RED preparation and corrective implementation are currently UNRUN; historical p02 remains undiagnosed, main and materialization tasks remain paused.

Boundary A RED source prepared: gm-ready-cancellation-coordination selects one official C# wrapper for five inert owned-stdio peers using shipped PowerShell entrypoints. Correct/wrong-tuple/EOF/drain-expiry cases require exactly one original sequence1 Cancelled frame before EOF; command-in-flight timeout forbids this cleanup classification. Expected oldbehavior4innerFAIL/1innerPASS; official wrapper expected1FAIL, all UNRUN. Stub peer is deliberately not a real owner/receipt authority. No production change. Python owns isolated child groups with26spercase, wrapper140s plus cleanup5s, category3minutes. Source review pending before execution. No gameplay/prompt change; protocolorchestration only.

A causal RED executed at02428aa5: Unitbuild29.93s,42warnings,0errors (compiled wrapper unchanged by later PS assertion correction). Official gm-ready-cancellation-coordination1wrapperFAIL in65.4126599s, no skip/duplicate/timeout, ownedcleanupcomplete. Five inner peers: four fail exactly missing cancellation, each original ledgerEOFBeforeCancellation; command-inflight guardPASS with one exact daemon-snapshotseq1 thenEOF. Ownedpeerwrapper exits1/drains both streams/no cleanupfailure. This proves old orchestration behavior only; no native authority or game claim.
B RED preparation adds unused originalbehavior writer wrapper, false provenance predicate and Refuse DATA decision stub. Thirteen cases:two genuine FileShare.None controls, four preopen/postopen/publication/close failures, onecancel and sixbookkeeping rows. Expected6FAIL/7PASS UNRUN. Generic sharing-shaped exceptions and genuine priorfailure rethrown after a later lock-open must propagate. GREEN requires CURRENT acquisition occurrence proof, not merely same-root typedexception. No publisher integration yet; real owner/epoch/signal/seal qualification still required. No production behavior changed by these unused stubs. No caching of lease/authority, immutable pendingdata only.
