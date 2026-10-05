# Linux complete-descendant ownership — T041-OWNERSHIP-DESIGN

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Exact source base: `31de2e33a6f44001fee9c0c3b6c4e61c8d3aab0e`.
2026-10-05; design and capability evidence only. Implementation is not authorized
by this document. Independent actual gpt-6.1-sol/xhigh design/evidence PASS at reviewed
`39df05024cecefe7266f41cec1b772895155e576`; no actionable findings.

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
