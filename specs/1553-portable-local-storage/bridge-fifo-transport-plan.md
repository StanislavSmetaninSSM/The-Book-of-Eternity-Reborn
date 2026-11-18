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

## WIP: deployment-only post-turn finally markers, 2026-11-18

#1589 candidate `tests/fixtures/DaemonObservation/instrument-finally.py` adds only
inert diagnostic marker blocks to a fresh deployed daemon copy. Every original
anchor must occur exactly once; removing all inserted blocks must recover the
original bytes exactly. The ledger records original/instrumented/patcher hashes,
anchor identities and marker names. A single immutable instrumented script uses
`BOE_DAEMON_FINALLY_TRACE` only as an absolute phase-specific output path, allowing
distinct p01/p02 traces without further script changes. Direct .NET writes mark
loaded, inner-finally entry/exit, watcher cleanup before/after, both final-status
branches before/after, outer-finally entry/exit and guard Dispose before/after.
Only marker I/O has an added catch; it emits
`BOE_DAEMON_FINALLY_DIAGNOSTIC_INVALID` to stderr. Existing operations, branches,
error handling, guards and lifecycle order are unchanged. Invalid/missing marker
evidence cannot establish a successful cleanup.

Source-only controls passed 4/4 in 0.084 seconds against the actual daemon source:
LF/CRLF byte restoration, reinstrumentation refusal, missing/duplicate-anchor
refusal and terminal-suffix refusal. PowerShell AST parsing of the patched copy
reported no syntax errors. No daemon/game execution, package rebuild or deployment
has occurred; independent source review and remote save precede any controlled
run. Instrumented success would be diagnostic evidence only, not pristine
production acceptance. A subsequent pristine same-world restart and normal stop
remain required. The earlier real ml7 post-turn unsettled state stays unexplained;
inert finally controls and no-turn idle settlement do not reclassify it.

Optimization: patching reads immutable source once and creates one instrumented
copy reusable across phases. Marker output is phase-local and uncapped; no owner
state is cached and no broad test suite or new runtime framework is introduced.
