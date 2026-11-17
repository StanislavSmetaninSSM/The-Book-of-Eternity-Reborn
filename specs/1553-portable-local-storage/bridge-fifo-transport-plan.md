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
