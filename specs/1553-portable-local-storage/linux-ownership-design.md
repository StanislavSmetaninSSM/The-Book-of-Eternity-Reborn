# Linux ownership: primary systemd and ordinary-lineage fallback

## Approved two-backend Linux ownership — 2026-10-05 15:12 UTC

Owner decision: primary existing **systemd user manager**, plus a **native ordinary-lineage fallback** for cloud/similar environments. This explicitly supersedes the earlier unconditional complete-descendant requirement only for the declared fallback scope. It is a product guarantee change, not permission to bypass environment security. Source base `2defe92cd8b7d313d07b059db76b73e97905f66f`, same branch/sole writer. First implementation is T041-FALLBACK-NATIVE synthetic helper qualification only; production pool/main Release wiring requires the next handoff/authorization.

- `systemd-user` declares the owned transient unit/cgroup boundary. Prefer it in Auto only when an already-running accessible user manager supplies the required transient-unit, stop and authoritative empty-boundary capabilities. No root, new persistent service, enable/autostart, cgroup delegation or policy changes. Positive native systemd qualification is absent here; mocks cannot establish it.
- `native-lineage` declares **ordinary descendants remaining within tracked lineage in the same PID namespace**, including ordinary double-fork, setsid and process-group changes. It does not cover external brokers/services, descendants outside the tracked lineage or unrestricted namespace migration. Those absences need not be universally proved on every ordinary launch. Detected escape/scope breach, lost authority, timeout, incomplete cleanup, owner loss or restart ambiguity is `Uncertain`; preserve quarantine and slot and do not automatically accept the task result.
- Default `Auto`: prefer available systemd-user; if unavailable before any launch, select native-lineage explicitly after its prerequisites pass. Explicit `SystemdUser` never silently downgrades. Explicit `NativeLineage` declares its limited scope. After a launch may have happened, backend switching is forbidden; uncertain startup retires through the original authority. Neither backend claims external delegated work. Windows Job behavior is unchanged.
- Every readiness/status/stop-evidence record exposes backend, guarantee scope, run identity, state, reason, whether managed authority is retained and whether scoped cleanup actually completed. `StoppedWithinScope` is never an Accepted proposal or durable run/fence. Consumers must preserve the scope and typed uncertainty; an old unqualified bool must not erase them. Source guards for existing production gates remain; new tests must assert the revised two-mode contract without enabling Linux Release.

## Portable own-child discovery revision — design PASS, 2026-10-05

Continuation from `c70a8c02515f115ef220c9b806b10005f88e368e`. Owner authorized
read-only proc metadata discovery, independent Sol6.1/XHigh algorithm/fixture
review, then staged native implementation. The ordinary same-namespace guarantee,
systemd primary, typed uncertainty, Windows Job and closed production gates remain.
Missing proc-children is ENOENT, not a security-refusal conclusion. The failed19case
run and19PID1zombies remain historical failures; never signal PID1 or claim them
reaped. This revision replaces only the proc-children discovery dependency and
makes fixture emergency cleanup independent of that discovery implementation.

**Observed without child/process probe:** `/proc` directory enumeration and own
`/proc/self/stat`/`status`/namespace metadata are readable. getpid, self link and
stat PID agree; NSpid contains exactly that one value. Only self metadata and the
count of numeric directory entries were retained; no other task metadata was read.
This establishes availability of the proposed inputs, not descendant qualification.

### Discovery and identity invariant

1. Open a retained read-only CLOEXEC proc directory; verify procfs with fstatfs.
   Before Ready require bounded self stat/status reads, matching own PID/PPID,
   exactly one NSpid equal to getpid, and the live self PID namespace identity.
   The one-element NSpid requirement rejects an ancestor proc mount's different
   PID coordinate system without touching PID1, changing mounts or translating
   numbers speculatively. SIGCHLD remains SIG_DFL without SA_NOCLDWAIT; helper is
   single-threaded and its loop is the only wait/reap owner. No fork occurs before
   admission. Keep the existing held-child bootstrap/pidfd gate before worker exec.
2. Enumerate only numeric `/proc` entries and read a bounded stat prefix. Consume
   PID, parent PID and state only. Skip `comm` structurally using the final closing
   parenthesis in the bounded record, so spaces/parentheses/newlines in names cannot
   shift PPID. Do not read environ, cmdline, maps, filesystem roots or file handles.
   No raw stat/comm or metadata of unrelated tasks goes to logs/artifacts. The
   unavoidable observation of numeric PID/PPID while filtering is expressly scoped
   to this worklist construction; no control operation follows on nonchildren.
3. A candidate is admitted only if the kernel stat PID equals the numeric entry
   and stat PPID equals this still-live helper in the verified PID coordinate
   system. That proves it is a current direct child, including adopted descendants.
   Before ANY next reap, acquire its pidfd. Such a child cannot have its PID reused
   between that parent check and pidfd acquisition: it remains our unreaped child,
   SIGCHLD is not ignored, and no other thread reaps it. Do not use starttime or a
   PID snapshot as authority. An old retired PID number can be considered again
   only after a fresh direct-parent check, never by replaying the old identity.
4. For verified children only, compare observable PID namespace identity to the
   retained own namespace. Mismatch is scope-breach/Uncertain. If namespace metadata
   disappears for a child already terminal, observe terminal status without reaping
   first (waitid WNOWAIT) instead of declaring a false breach. Other errors on
   known-owned metadata/pidfd/signal/wait latch Uncertain and retain cleanup.
5. ENOENT/ESRCH for an unbound directory entry are ordinary disappearance races;
   skip and rescan. EACCES/EPERM are respected: no privilege retry, alternate mount
   or reading more private metadata. An unreadable unclassified entry grants no
   authority and is skipped; a live hidden in-scope child still prevents ECHILD,
   so timeout remains Uncertain. A refusal for a known-owned candidate latches
   Uncertain immediately. Global proc/self/read-directory failure is a capability
   failure, never a silently empty inventory or a stop proof.
6. Bound each scan slice (128 numeric entries) and each stat read (4096bytes), keep
   the directory cursor between iterations and begin a fresh pass at EOF. Bound
   held live pidfds (128). Capacity failure latches Uncertain but continues retiring
   held children and reaping so later passes can acquire freed slots. Full/empty
   snapshots have no terminal meaning. Forks, exits, reuse, root-first exit and late
   adoption are caught by repeated passes; no completeness claim comes from proc.
7. TERM each owned handle once; KILL after the global grace. Sole waitpid __WALL
   handles actual terminal states and releases each retired handle. Only sealed
   launch + actual ECHILD + no uncertainty latch produce StoppedWithinScope.
   Deadlines/errors never drop authority; late cleanup stays Uncertain. This is
   exactly the already approved ordinary-lineage scope, not an external-work proof.

Independent actual Sol6.1/XHigh review PASS at `4882e3b3eb17511b73c570111e410dc791389bc6`, no P1/P2 proof gaps. This source-grounded inference still needs actual native qualification. [stat PID/PPID layout](https://man7.org/linux/man-pages/man5/proc_pid_stat.5.html),
[unreaped-child pidfd preconditions](https://man7.org/linux/man-pages/man2/pidfd_open.2.html),
[proc PID namespace view](https://man7.org/linux/man-pages/man7/pid_namespaces.7.html),
[NSpid](https://man7.org/linux/man-pages/man5/proc_pid_status.5.html) and
[wait semantics](https://man7.org/linux/man-pages/man2/waitpid.2.html) distinguish
metadata discovery from retained kernel authority and terminal reaping.

### Independent fixture cleanup and staged verification

Guardian uses **no child enumeration/proc scan**. It preflights only its own clock,
subreaper/SIGCHLD, pidfd and report/lifetime resources before any fork; known helper
and optional sibling sentinel are gated direct forks with retained pidfds. It
exclusively waits/reaps all its children, including later adopted fixture children,
until actual ECHILD. On helper error/deadline it retires only those held known
pidfds. Unknown adopted fixture children have independently armed7s default,
unblocked SIGALRM in EVERY fork branch before blocking work; they self-expire while
guardian retains reap authority. It never exits on a deadline/error while children
remain. This is a bounded trusted synthetic-fixture mechanism, not a generic CLI
cleanup guarantee and not production stop evidence. Unexpected adopted children
count as emergency cleanup; positive helper cases require zero and finish before
self-expiry. A controlled guardian crash-helper request tests this independent
cleanup path: held helper pidfd killed, synthetic root self-expiry and actual reap,
explicit emergency count and no helper terminal proof. The real helper cancels any
alarm inherited across exec beforeReady; only synthetic workerforks arm expiry.
Sentinel nonterminal proof uses held-pidfd poll readiness, never signal0 alone.

After independent design PASS, first run a new bootstrap category containing only
(1) one real controlled root start/stop/reap (conservative helper still Uncertain),
and (2) the controlled helper-loss/guardian-reap scenario. No old19case replay.
Only after actual cleanup of these1–2 scenarios passes add a focused descendant
causal RED against the root-only baseline (child self-expiry is not scoped success),
then the minimal proc worklist/pidfd retirement GREEN. Later expand coherent narrow
owners for doublefork/setsid, names with brackets/spaces, late adoption/root-first,
spawn-during-stop, ignored TERM, cancellation/owner loss/timeout and sentinel scope.
Unchanged pure/previous cohorts remain unrun. Every stage retains native build
provenance, actual case counts/terminal records and independent guardian cleanup.

## Native fallback algorithm — independent design review PASS

A single-thread C helper owns one launch, inherited private owner-command stdin and
status stdout. It is never loaded/forked from CLR. `native/linux/boe-lineage-supervisor.c`
is built from repo source by `scripts/build-linux-supervisor.ps1`; fixture child code
lives separately under `tests/fixtures/LinuxLineage/`. No privileged installation.
The managed tests invoke the actual built executable, retaining source/binary hashes,
compiler version/flags and complete bounded transcripts. No generic shell command,
provider, user save, canonical write or production process-pool entry is exercised.

1. Before Ready, restore SIGCHLD=SIG_DFL without SA_NOCLDWAIT, clear inherited signal
   mask, establish/verify PR_SET_CHILD_SUBREAPER, verify own proc-child discovery and
   pidfd capability. No other thread/signal handler reaps. Use monotonic deadlines.
   Ready carries `native-lineage` / `ordinary-same-namespace-lineage`; it launches
   nothing. Commands are fixed single bytes, no buffered/unbounded request payload.
2. One Start command forks a child behind an internal bootstrap pipe. Open its pidfd
   while it is the helper's unreaped direct child; only then release exec. Child
   closes/replaces owner stdin and status stdout, closes internal ends, restores
   normal signal disposition/mask, and owns a new process group. All helper pidfds
   and exec-error/control pipes are CLOEXEC. Preserve inherited credentials, groups,
   environment, cwd and proc view; no namespace/mount/credential change. Child stdout
   goes to the caller's stderr stream in this non-PTY prototype. Exec-error pipe
   distinguishes exec failure from successful start; authority exists before exec.
3. EOF/owner loss, Cancel, Stop, root exit or authority error seals launch permanently
   and enters stop. SIGTERM/SIGINT to helper only request its owned cleanup; signal
   handlers set flags. Owner EOF is detected even if a child lives. Status output is
   nonblocking/bounded; broken/backpressured output latches Uncertain and triggers
   cleanup, never blocks the reaper. No signal is sent by untrusted numeric PID.
4. During retirement repeatedly read only `/proc/self/task/<self>/children` as a
   **worklist**, never a proof of completeness. Children may be omitted during exit;
   retry until exclusive wait reports ECHILD. Before any reap, open pidfds for listed
   unreaped direct children. Exclusive ownership of reaping prevents their PID reuse
   in that interval. Verify known child membership/namespace where observable; any
   unexpected failure/scope mismatch latches Uncertain. Descendants orphaned by
   stopped parents are adopted by this subreaper within the declared same-namespace
   scope. No scanning/signal to unrelated system PIDs or broad process-group kill.
5. Send TERM once per acquired pidfd, then KILL after the configured grace. Repeated
   adoption rounds catch double-fork/setsid and forking while parents stop. Retain
   each handle until actual wait/reap; SIGKILL send, pidfd readiness, empty snapshot
   and root exit are not stop success. Reap with exclusive `waitpid(-1,__WALL|WNOHANG)`;
   stopped/traced notifications are not terminal; failed syscalls retain uncertainty.
   Terminal child entries are retired without reacquiring their numeric PID; stale
   pidfds cannot target a reused process. Bound the live handle inventory; exhaustion
   latches uncertainty and preserves cleanup authority, never truncates a proof.
6. Only sealed launch plus actual ECHILD, with no uncertainty latch, yields
   `StoppedWithinScope`. It says nothing about out-of-contract external work. Root
   exit status/reason remains separate from stop evidence; no Accepted field exists.
   Timeout reports `Uncertain` once and retains helper/reaping authority until actual
   cleanup. Cleanup may later finish, but the uncertainty latch is not silently
   converted to successful task delivery. Owner loss, malformed command or an explicit
   observable out-of-scope notice likewise remain uncertain. Abrupt helper death or
   restart cannot reconstruct authority from PIDs; the future consumer retains slot
   and quarantine. The helper is not promised to survive its own SIGKILL.

Default protective observations: TERM grace250ms, stop5s; tests use explicit shorter
budgets, with a stop deadline shorter than TERM grace to prove timeout while a known
child still exists. No exit/drop of authority merely to fit that deadline. Per-test
synthetic children also self-expire and tests wait for actual helper/child cleanup.
The fixture harness must preserve its own emergency cleanup authority; a missing
helper terminal report is a test failure, not permission to kill arbitrary PIDs.
The per-test external native guardian establishes its own subreaper and holds the
unreaped helper pidfd. Its20s deadline starts emergency retirement, never authority
disposal; it waits until actual ECHILD. A positive case must finish before the7s
fixture alarm and report zero emergency helper-lineage cleanup. An intentionally
separate guardian-owned sentinel is accounted independently, never signalled by
the helper. Guardian closes duplicate owner/status FDs before fixture inheritance.
Helper/guardian ignore SIGPIPE (worker restores default), so broken output reaches
cleanup. Test closed output after Start and a genuinely full status pipe before
Ready. A final Uncertain cleanup update may say cleanupComplete=true after ECHILD,
without clearing uncertainty. No permanent resources, global handlers/shared
mutable fixture or native installation.

Before Ready the helper closes all inherited nonstandard FDs with close_range;
this bounded build therefore requires that syscall (Linux5.9+) and glibc pidfd
wrappers. No fallback after a denied prerequisite. It probes only its own pidfd,
not another process. Fixed commands: L=start, S=stop, C=cancel, U=observed scope
breach; malformed/repeated launch seals Uncertain. Run ID is1..64 ASCII alphanumeric
or hyphen. No dynamic payload parser. First package is linux-x64 built on this
Debian/glibc toolchain; other architectures/libc/distribution claims remain open.

Protocol v1 fixed metadata: `runId`, `backend`, `guarantee`, `state`, `reason`,
`cleanupComplete`, `authorityRetained`, root exit information and syscall error code
when applicable. States Ready/Started/Stopping/StoppedWithinScope/Uncertain have
fixed semantics; parse strictly and correlate the run. No command text, environment
values or payload content in errors. A scoped terminal result never substitutes for
fence/canonical acceptance/whole-application qualification. Future systemd adapter
uses the same semantic record with its distinct scope, after its own native proof.

## Selection and first bounded TDD plan

Tracked T041-BACKENDS-DESIGN and T041-FALLBACK-NATIVE, US4/FR-012/013/014/015.
Alternatives considered: retain unavailable universal namespace gate (does not meet
new approved fallback requirement); root/process-group-only killing (misses ordinary
setsid/double-fork); selected single-thread subreaper/pidfd worklist plus exclusive
reap for the narrower declared contract. Namespaces remain optional future research.

- First commit the reviewed docs. Add native build/package script, per-test native
  fixture and narrow `linux-fallback-supervisor` owner through the canonical C# runner.
  Actual native build is part of test preparation; compile/missing-binary errors are
  preparation failures, never causal RED. Record compiler/source/binary provenance.
- Bootstrap a conservative baseline that really forks/execs and owns/reaps a synthetic
  root, reports its limited/uncertain outcome and does not yet claim descendant stop.
  Positive root startup/control/cleanup passes; missing descendant retirement and
  false/absent scoped-success cases provide behavioral RED. Fixture self-expiry and
  owned cleanup prevent leaked test children. Do not fabricate a source-era defect.
- Add iterative adopted-child pidfd retirement and typed protocol handling. GREEN
  must include ordinary children, double-fork/setsid in same namespace, TERM ignored,
  forking during stop, root-exits-first, cancellation before/after Start, owner-channel
  loss, retained timeout, stale pidfd, exec failure, observable out-of-scope notice
  and an unrelated synthetic sentinel unchanged. Source-linked fixture PID/exit
  records prove actual descendants existed and were reaped; positive cases are
  required. Pure selection/outcome contract checks may accompany this owner.
- Run `scripts/test-csharp.ps1 -Category linux-fallback-supervisor -Parallelism 1`
  with fresh managed/native builds; PlanOnly for exact selection, discovery-only
  catalog audit. Do not replay prior IPC/FRAME/ENV/workspace/receipt/output/input or
  full categories. Native tests fail explicitly off Linux; no fake OS-skip PASS.
- Independent actual Sol6.1/XHigh design review before helper implementation, then
  implementation/evidence review after RED/GREEN. Commit/push WIP non-force, read
  remote SHA/bytes, restore fresh from GitHub. Stop at handoff before production
  pool/main Release wiring. Primary systemd positive native execution stays open.

## API grounding and compatibility

[Subreaper](https://man7.org/linux/man-pages/man2/PR_SET_CHILD_SUBREAPER.2const.html)
provides ordinary orphan adoption; it is not universal namespace containment.
[pidfd_open](https://man7.org/linux/man-pages/man2/pidfd_open.2.html) documents the
unreaped-child/no-other-reaper PID stability preconditions; [pidfd signals](https://man7.org/linux/man-pages/man2/pidfd_send_signal.2.html)
retain identity after numeric PID reuse. [wait](https://man7.org/linux/man-pages/man2/waitpid.2.html)
and [proc children](https://man7.org/linux/man-pages/man5/proc_tid_children.5.html)
separate actual reaping from a racy worklist. The helper's loop is a design inference
under the new limited contract and still requires native tests, not proof by citation.
Systemd user support needs separately qualified transient-unit ownership and stop/
empty-cgroup observation; do not start or configure the absent manager here.

## Historical universal-boundary analysis (superseded only as stated above)

The following original analysis records why subreaper alone could not satisfy the
old unrestricted guarantee. Its namespace-only preference and old no-implementation
instructions do not override the approved two-mode contract or current bounded task.
Its kernel counterexample and environment evidence remain valid scope limitations.

## Root assessment — backend choice deferred, 2026-10-05

Namespace ownership remains a pending kernel-stop candidate, not the approved sole Linux backend. Do not implement namespace bootstrap now or count negative-only unavailable-route tests as progress toward a qualified backend. Ordinary namespace use by a CLI is legitimate behavior, not an adversarial exception. Compare a systemd user/cgroup backend later without changing security or delegation; preserve the same complete-stop requirement whichever backend is selected.

Compatibility obligations extend beyond making map files writable. The unprivileged single-ID UID/GID map path does not preserve every identity visible to an arbitrary CLI; supplementary groups can remain kernel credentials while their unmapped user-visible IDs become overflow values. GID mapping also constrains setgroups. These are product compatibility issues, not solved by preserving one effective UID: [kernel user_namespace.c](https://github.com/torvalds/linux/blob/v6.18/kernel/user_namespace.c), [groups.c](https://github.com/torvalds/linux/blob/v6.18/kernel/groups.c). An inherited outer proc mount exposes another PID coordinate system and cannot silently stand in for a compatible CLI proc view: [pid_namespaces(7)](https://man7.org/linux/man-pages/man7/pid_namespaces.7.html). Distribution policy also matters: [Ubuntu24.04 release notes](https://discourse.ubuntu.com/t/ubuntu-24-04-lts-noble-numbat-release-notes/39890) describe AppArmor-mediated unprivileged namespace/capability restrictions. This Debian probe establishes no Ubuntu support.

New visible CLI restrictions or system dependencies require an owner decision. No such change, security bypass or additional namespace probe is authorized by the receipt slice below. Earlier proposed namespace slices remain deferred.

## Recommendation and environment decision

Keep Linux production Release closed. Prefer a fresh per-run **kernel PID
namespace**, supervised by a separate native monitor, for the next ownership
prototype. A process group, proc snapshot or ordinary subreaper is insufficient
for the required unrestricted descendant boundary. The namespace architecture
has a kernel completion condition, but its faithful bootstrap and host
integration are not implemented or qualified here.

Two bounded, ordinary-permission probes passed: subreaper/pidfd/owned-child
wait, and one `unshare(CLONE_NEWUSER | CLONE_NEWPID)` call. The second made no
child, UID/GID mapping, mount or exec. It proves namespace creation permission,
not PID1 operation, teardown or CLI compatibility. The normal UID/GID map paths
and cgroup2 mount are read-only. A mapped namespace backend is therefore
**BLOCKED on that normal mapping route in this environment**; no write was
attempted and no write errno is claimed. No remount, alternate proc mount,
privilege retry, cgroup delegation or security change is proposed as a workaround.

This is enough to complete analysis without waking the user. Parent assessment
of this design and prerequisites precedes any runtime implementation. No new
secrets, persistent permissions, network policy or paid service are needed for
the work completed here.

## Existing contract and implementation seams

Source audit at the exact base:

| Source | Existing behavior and required consequence |
| --- | --- |
| `GmWorkerProcessTree.cs` | `Attach(Process)` accepts Windows only. An unnamed Job has KILL_ON_JOB_CLOSE, no breakaway flag, assignment before Release, explicit TerminateJobObject, then root exit **and ActiveProcesses=0**. Linux must supply a complete boundary, not reinterpret root exit. |
| `GmWorkerBridgePool.cs` | Workspace → neutral managed host → Attach → authenticated Ready → Release. Completion/import waits for `StopAndWaitAsync`. Linux ownership must be established before the first potentially executing worker. |
| `GmWorkerProcessHost.cs` | Both pipe peers are authenticated against the direct managed host's `Process.Id` and effective UID before payload. Host starts worker only after Release. Completed and OutputDrained do not prove descendant death; host waits indefinitely afterward. |
| Pool unattached fallback | `Kill(entireProcessTree:true)` plus root exit is not a Linux complete-tree proof. Once a new helper may have spawned anything, it must transfer its exact ownership authority to cleanup/quarantine; never fall through to that root-only fallback. |
| `GmWorkerQuarantineReaper.cs` | Death confirmation gates disposal, detached evidence removal, terminal receipt and slot release. Failed confirmation retains authority/workspace/slot and retries. Preserve that contract. |

Windows default child Job membership covers ordinary CreateProcess descendants,
including later generations; breakaway and externally delegated processes are
separate limits. Its last-handle cleanup is a useful failure guarantee, but this
Linux proposal is not yet an equivalent qualified implementation. See
[Microsoft Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).

FR-009/012/014/015 remain unchanged. Accepted application outcomes require the
existing admission/generation/publication checks plus confirmed stop; a stop
receipt alone does not accept a proposal. Missing/invalid proof, timeout or
lost ownership remains Uncertain and retains quarantine. The persistent-main
run-record component does not fence this independent worker ownership slot.

## Why the simpler candidates fail

- `setsid`, double fork and process-group changes defeat group-only ownership;
  a PID list has fork/exit/reuse races. `/proc/.../children` is only a possible
  worklist and can omit live children during changes. See
  [proc children](https://man7.org/linux/man-pages/man5/proc_tid_children.5.html).
- A correctly isolated subreaper with SIGCHLD=SIG_DFL, no SA_NOCLDWAIT, one
  exclusive reaper, sealed launch and all-class `wait*(__WALL)` can reason about
  its ordinary ancestry. `WNOHANG` returning zero means children remain; pidfd
  ESRCH proves only that target. See [subreaper](https://man7.org/linux/man-pages/man2/PR_SET_CHILD_SUBREAPER.2const.html)
  and [wait](https://man7.org/linux/man-pages/man2/wait.2.html).
- It cannot unconditionally prove arbitrary CLI descendants gone. An owned
  process can fork into an existing descendant PID namespace whose init is
  external to the owned ancestry. After intermediate parents exit, later
  children can reparent to that init, leaving the outer subreaper with ECHILD.
  Kernel `find_new_reaper` stays at the exiting parent's PID-namespace level.
  A late namespace snapshot cannot repair this proof. CapEff=0 or Seccomp=2 is
  not evidence of an immutable prohibition on that path. See
  [Linux v6.18 exit.c](https://github.com/torvalds/linux/blob/v6.18/kernel/exit.c).
- cgroup v2 could provide a different kernel boundary (`cgroup.kill` and
  population observation with membership control), but no writable delegation
  is available/authorized here. No cgroup operation was attempted. See
  [kernel cgroup v2 documentation](https://docs.kernel.org/admin-guide/cgroup-v2.html).

## Proposed kernel boundary and proof

```text
application owner
  └─ native monitor (outside the new PID/user namespaces)
       └─ native namespace init (PID 1 in a fresh per-run PID namespace)
            └─ existing managed hidden host
                 └─ worker CLI and descendants, including nested PID namespaces
```

The native monitor is a small single-thread program, not a fork callback inside
the multithreaded .NET process. Prefer atomic native clone/clone3 with
CLONE_NEWUSER, CLONE_NEWPID and CLONE_PIDFD plus SIGCHLD; that exact creation
path is **unprobed**. No automatic alternate syscall/flags after denial. A
separately reviewed unreaped-child binding could be considered later, not
silently substituted. Monitor owns the exact init process pidfd and exclusively
waits/reaps it. No PIDFD_THREAD, PID-name reacquisition or broad signal target.
See [clone](https://man7.org/linux/man-pages/man2/clone.2.html),
[pidfd_open](https://man7.org/linux/man-pages/man2/pidfd_open.2.html) and
[pidfd_send_signal](https://man7.org/linux/man-pages/man2/pidfd_send_signal.2.html).

The fresh namespace contains the entire worker lineage from its first fork.
Descendants cannot move themselves or future children into ancestor/sibling
PID namespaces; entering deeper namespaces still leaves them in the outer run
boundary. When init dies, the kernel disables new PID allocation, kills the
namespace tasks, and waits for their removal, including externally parented
zombies. The external monitor's **terminal wait/reap of that exact init child**
is the proposed completion proof. Signal delivery, pidfd readability alone,
EOF, PTRACE_EVENT_EXIT, host exit, or a user-space ECHILD frame are not substitutes.
This inference is grounded in [PID namespace semantics](https://man7.org/linux/man-pages/man7/pid_namespaces.7.html)
and [v6.18 zap_pid_ns_processes](https://github.com/torvalds/linux/blob/v6.18/kernel/pid_namespace.c).
It is not native qualification of this 6.18.44 environment or all supported kernels.

All worker descendants must originate inside this namespace. Even an already-
running neutral managed host cannot be transplanted into it. Linux needs an
explicit launch/ownership seam creating monitor → fresh init → managed host
before Ready; preserve the existing Windows Attach path. No later migration
can establish that history. Namespace inode numbers or PID
numbers alone are not durable identity. External pre-existing brokers/daemons
asked by a CLI to write are outside both the ordinary Job lineage and this
namespace. Such provider behavior needs its own owned-service contract or must
block execution; this design does not silently narrow the arbitrary-CLI product
requirement or promise containment of hostile same-user outsiders.

## Startup, identity and lifecycle

1. Application starts only the trusted native monitor. Before it may create the
   run boundary, bind its live instance using private IPC, kernel peer identity
   and a fresh challenge. Process.Id alone after a possible exit is insufficient;
   do not assume .NET Process.SafeHandle is a Linux pidfd. Ownership transfer must
   remain recoverable on every failure after a helper can create descendants.
2. Monitor creates the init child atomically with a pidfd and explicit bounded
   bootstrap barrier. It preserves the unreaped child identity and checks that
   this is a new PID namespace with native init PID1. Establish requested UID/GID
   maps and applicable per-userns setgroups ordering before any managed host.
   Preserve intended effective IDs, groups and filesystem access; no overflow
   UID65534 fallback. No setuid helper, privileged service or root requirement.
3. Native init stays simple and single-threaded, resets SIGCHLD correctly, owns
   reaping, and forks only the existing neutral managed host. All control/lifetime
   FDs are closed or CLOEXEC in host/worker as appropriate. Worker must never hold
   the owner's monitor-liveness endpoint. Root/monitor loss before Ready seals
   startup; it cannot be interpreted as authorization to launch.
4. Distinguish monitor identity, namespace-init identity and managed-host identity.
   Existing two-channel SO_PEERCRED checks must still authenticate the **actual
   managed host in the application's PID/UID coordinate system**. A possible
   native binding is init-owned unreaped host + transferred pidfd + authenticated
   outer identity announcement and host challenge. The exact translation/FD
   protocol remains a first-slice design/test obligation; no unchecked numeric
   PID, namespace-local PID or monitor Process.Id may replace it. Keep the current
   strict managed framing/Launch/environment reconstruction, not a second native
   payload parser. See [Unix credentials/FD transfer](https://man7.org/linux/man-pages/man7/unix.7.html).
5. Only after every binding, mapping and ownership barrier passes may the existing
   Ready/Release protocol proceed in a later separately authorized integration.
   This design turn and proposed first slice never send production Release.
6. Stop seals launch permanently. A cooperative grace period may be bounded, but
   the final operation targets the held init pidfd with SIGKILL and waits for its
   terminal status. Ignored TERM, detached sessions, double forks and concurrent
   fork attempts do not weaken the proposed kernel boundary. Stop uses its own
   bounded deadline, not an already-cancelled caller token. A blocked task,
   external tracer or unreaped externally parented zombie can delay completion:
   timeout is Uncertain, not success or permission to delete/reuse evidence.
7. A live monitor reports proof only after successful terminal wait, bound to
   root key, generation/session epoch, execution/run ID, backend, monitor instance,
   init binding and stop attempt. A strict codec alone is not authentication.
   Planned monitor shutdown after acknowledged proof is distinct from unexpected
   monitor loss before proof. Existing root-first Windows wait ordering cannot
   simply be reused for a monitor which must remain alive to deliver proof.
8. If the application dies while monitor survives, non-inherited owner-channel
   EOF triggers seal/kill/reap. Init should also have a carefully ordered native
   parent-death SIGKILL mechanism against monitor loss; registration race,
   parent-thread identity and credential-transition clearing must be handled
   before forking host. This is a secondary cleanup mechanism, not proof. See
   [PDEATHSIG limits](https://man7.org/linux/man-pages/man2/PR_SET_PDEATHSIG.2const.html).
   Init crash with a live monitor can still produce terminal kernel proof;
   monitor crash without retained trustworthy proof remains Uncertain even if
   the kernel probably cleaned descendants.
9. Cold restart needs authenticated reconnection to the same live authority or
   a separately designed durable monotonic complete-stop receipt. PID absence,
   stale numeric identity, missing channel or an unauthenticated file cannot
   supply it. Until that work is qualified, retain quarantine and block reuse.
   Do not connect the persistent-main codec as a substitute worker fence.

## Dependencies and remaining compatibility questions

Available: Debian13.6 x86_64/kernel6.18.44, GCC14.2.0, glibc2.41, pidfd headers,
.NET SDK10.0.401/runtime8.0.31 and PowerShell7.6.6. Exact observed metadata is in
the [evidence packet](recovery/evidence/worker-ownership-design/manifest.json).
No package installation was needed for this turn.

New user namespaces initially have no UID/GID maps. This environment permits
creation but exposes their normal mapping interface through read-only `/proc`.
Faithful ID/group behavior is therefore not established. The map requirement
and exec-capability behavior come from [user namespaces](https://man7.org/linux/man-pages/man7/user_namespaces.7.html);
creation permission is governed by [unshare](https://man7.org/linux/man-pages/man2/unshare.2.html).
No source-backed argument yet shows that an unmapped namespace meets the product
contract. Do not infer that creation PASS solves bootstrap.

The inherited outer `/proc` has a different PID view from namespace getpid;
.NET diagnostics, Process APIs, arbitrary CLI `/proc` usage and cross-boundary
peer identity must be qualified. Do not silently remount proc or add a mount/
network/filesystem sandbox. HOME, selected environment, working directory and
local authentication/socket behavior must retain the existing product contract.
Do not pass newly gained namespace capabilities to the CLI by accident. Required
capability dropping, credential/group fidelity, signal setup and authentication
form one pre-host bootstrap gate; they remain unimplemented.

Future build needs a reviewed native C asset, architecture-specific build and
packaging, deterministic provenance and explicit unavailable-backend failure.
Ship the helper with the product; users must not need a compiler. Kernel/syscall,
libc and architecture support limits must be selected and tested before claiming
portability (pidfd waitid needs Linux5.4; clone3/filters remain separate checks).
No permanent daemon, cgroup delegation, kernel module, seccomp change, secret or
new network access is part of this candidate.

## Two small next slices, conditional on parent assessment

**Slice A — native bootstrap and pre-Release authority only.** Settle and test
the exact monitor/init/host identity protocol, availability rejection and
ownership transfer first. Add a narrow catalog owner through `scripts/test-csharp.ps1`
for causal RED then GREEN: missing helper, unavailable mapping/creation path,
wrong PID/UID/instance, malformed/replayed control, early EOF, owner loss, and
cleanup retaining uncertainty. Native children are bounded synthetic fixtures;
the managed neutral host may reach Ready only once mapped identity prerequisites
are satisfied. No worker CLI, production Release or canonical mutation. In this
environment, the mapped-bootstrap success cases remain BLOCKED until an ordinary,
authorized compatible route exists; testing rejection is not positive ownership
qualification. Do not enlarge this slice into a general native command parser.

**Slice B — kernel terminal-stop proof, synthetic trees only.** After bootstrap
is valid, add exact native synthetic descendants and the lifecycle tests below,
again causal RED/GREEN via a dedicated narrow category. Bound children, time and
memory; native fixture has independent cleanup authority and self-expiry where
applicable. Test pure decision logic separately from actual syscalls. Complete
kernel proof and review may qualify that bounded backend; production pool wiring,
durable restart receipts, quarantine publication, PTY and real GM remain separate
tracked work, not automatic follow-ons inside this task.

| Scenario | Required observation / decision |
| --- | --- |
| Before Ready/Release; attach failure at each barrier | Zero CLI starts; exact authority retained after any possible fork; no root-only fallback. |
| Worker/host exit first, late descendant/output holder | Completed/EOF/root exit cannot confirm; terminal init wait must occur. |
| setsid + double fork + orphan + ignored TERM | All owned processes stop under namespace teardown; actual terminal proof and fixture cleanup. |
| Spawn during stop; nested namespace; CLONE_PARENT/no-SIGCHLD variants | Sealed launch and kernel namespace teardown contain them; no proc-snapshot proof. |
| Same-number PID/replayed identity/stale pidfd | Never signal foreign incarnation or accept stale proof; stale-fd ESRCH alone is not tree proof. |
| Zombie, external tracer, simulated blocked terminal wait | Bounded timeout retains Uncertain/workspace/slot; retries only through same valid authority. |
| Owner death; monitor death; init crash; host crash | Check each actor separately; cleanup attempt and proof availability are separate assertions. |
| Cancellation/timeout races with Ready, Release, Completed and stop | No release after sealing; no caller-cancelled stop short circuit; no premature acceptance. |
| Restart, missing/forged/wrong-generation receipt | Preserve uncertainty; no quarantine deletion or new writer without independent valid stop authority. |
| UID/GID/groups, env/cwd/HOME, two-channel peer PID, inherited FDs/proc | Preserve actual CLI/host contract; negative admission if prerequisites cannot be met. |

No such tree matrix case has run in this design turn. Only the two minimal
capability probes below ran. Existing IPC34, ENV16, workspace47, FRAME49 and
run-record90 were not repeated. No test catalog/selection or application/test
source changed; that previous selection is not evidence for this analysis.

## Evidence and review

The [machine-readable result](recovery/worker-ownership-design.json) records
commands, exact source hashes, environment, proof limits and review status.
The [packet](recovery/evidence/worker-ownership-design/manifest.json) retains
both C probe sources, exact logs/results and Spec Kit prerequisite result;
compiled binaries are excluded. Probe1 built and exited0, reaped its one own
child after pidfd SIGKILL, observed expected stale-fd ESRCH(3) and ECHILD(10).
Probe2 built and exited0 with one unshare call and zero children. Neither result
is TDD GREEN, process-tree qualification or application execution.

Analysis-only documentation has no GM-authored field, gameplay or runtime
behavior change; Mortal World/afterlife prompts/examples and source guards need
no update for this task. Operational documentation and guards must be updated
with any eventual new runtime capability. No Fast/PreMerge/full-suite, real GM,
canonical writes, HOME-PC changes or unrelated checkout edits occurred.

Independent review verified the exact source/remote/tree, clean fresh restoration,
all16 changed file bytes,11 unchanged inputs and10 artifact hashes, including
probe log/result/source consistency. No independent runtime execution or native
ownership acceptance is claimed. [Review/restoration evidence](recovery/evidence/worker-ownership-design-review/manifest.json).
Final verdict-carrier publication and another empty-directory restore are
reported in the handoff, without a self-referential commit identifier.

## Current two-mode design review

Independent actual gpt-6.1-sol/xhigh reviewed five docs at `a3e48f3a636bfd6aca86171b2f0a41ae2b93812e` relative to `2defe92cd8b7d313d07b059db76b73e97905f66f`: PASS, no P1/P2 findings. Guardian/deadline/SIGPIPE/late-Uncertain obligations above are included. Read-only review and diff-check; no runtime qualification.
