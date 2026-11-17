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
