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
