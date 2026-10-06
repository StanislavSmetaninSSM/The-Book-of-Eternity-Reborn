# Owned main terminal — design and first-slice implementation plan

> Future implementation uses Superpowers executing-plans and TDD, sole writer,
> separate Sol6.1/xhigh review. This checkpoint authorizes design and the recorded
> primitive probe only; runtime implementation remains pending authorization.

**Issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553
**Task:** T041-OWNED-MAIN-TERMINAL-DESIGN; next T041-OWNED-MAIN-TERMINAL-NEUTRAL.
**Base:** accepted T042 `1bc9d67536dccbcc6672d8e7cd71ad885c2a946c`.
**Goal:** the actual bridge holds one persistent, manually interactive neutral
process on an owned Linux terminal and stops its entire declared lineage precisely.
**Architecture:** one terminal session supplies the original input lifetime, output
pump, resize and root-exit notification; the existing native supervisor retains stop
 authority. Accepted prompt dispatch consumes observations from that same session.
**Stack:** existing .NET8/SDK10, PowerShell7, repo-built Linux C helper, ConPTY/Job.
**Spec:** [spec.md](spec.md), US4/FR-009/012/013/014/015/SC-004,
[accepted roadmap](plan.md#ordinary-linux-console-with-persistent-interactive-gm--design-only-continuation),
[ownership contract](linux-ownership-design.md).

## Constraints and alternatives

Keep native ordinary-same-PID-namespace scope: pidfd identities, one exclusive native
reaper, sealed-launch ECHILD; visibly exclude external services/delegated work/outside
lineage. Existing systemd-user remains primary for the later ordinary launcher;
explicit SystemdUser cannot downgrade. No namespace/root/cgroup/security/settings
requirement. Uncertain startup/stop, timeout or authority loss blocks replacement and
acceptance. EOF, root exit and process-group emptiness never prove owned stop.
Windows remains ConPTY plus Job ownership, not process-tree scanning.

Chosen: extend existing supervisor transport and consume it in BridgeHost. A second
managed PTY reaper would duplicate authority and lose adopted descendants; group kill
misses setsid/double-fork. Waiting for unavailable systemd adds no PTY evidence. Neutral
proof can precede main run fencing; game-writing launch cannot. No public rollout/live
provider/real saves/cold exactly-once/gameplay/GM-model change. This is client-owned
transport; GM-authored prompts/examples need no update. No save protection from the
player. No capability inferred from unavailable-route tests. Sol6.1/xhigh, no Astra.

## Source-backed delta; accepted audits reused

Sources below are at accepted base, pinned by SHA256/blob in
[design evidence](recovery/owned-main-terminal-design.json). No old cohort rerun.

| Boundary | Actual source and conclusion |
| --- | --- |
| Main launch/retirement | `BookOfEternityGMBridge/Program.cs:351–490,1440–1465,1526–1545`: concrete ConPtySession and original BeginInputLifetime/pumps. StopShell clears `_pty` before best-effort Dispose; managed drain retention exists, native proof does not. Root exit must retain captured session/binding until owned retirement. |
| Output/current view | `Program.cs:841–889,926–942,1101`; `BridgeHost.PromptDispatch.cs:12,141–178,225–238`: accepted stateful UTF8/raw pump; output version and Win32 screen reader are separate. Linux reader is empty. Need coherent session-scoped view, not historical tail/injected screen. |
| Windows main owner | `ConPtySession.cs:37–177`: real pseudoconsole, unsuspended CreateProcess, Dispose uses Process.Kill. Existing worker `GmWorkerWindowsOwnedLaunch.cs`/`GmWorkerProcessTree.cs` retain Job authority. Main needs assignment before arbitrary code runs. |
| Native owner/transport | `native/linux/boe-lineage-supervisor.c:259–305,320–411`: held root/pidfd before release; stdin `/dev/null`, setpgid, no PTY/setsid. Reuse numeric-proc discovery/exclusive reap/uncertainty latch; add transport, not another ownership engine. |
| Managed binding | `Services/GmWorkers/GmWorkerNativeLineageLaunch.cs:11–35,87–140` and `GmWorkerNativeDescriptors.cs`: private seqpacket, credentials, transferred descriptors, retained status/owner/drains. Host-v2 is a worker protocol, not a terminal adapter. |
| Admission | `GmWorkerBackendSelector.cs:3–54`: native neutral preflight only, systemd NotImplemented, ordinary Linux WorkerRelease NotQualified. `Services/GmRuntime/GmSessionRunAdmission.cs:10–58` is a slot condition without consumed main launch/persistence fence. Worker permit never grants main/save authority. |

Client paths in the last two rows are relative to `BookOfEternityClient/`.
Reuse [native](recovery/linux-fallback-qualification.json),
[HOST A/B](recovery/worker-host-qualification.json),
[OUTPUT48](recovery/gm-output-qualification.json),
[INPUT37](recovery/gm-input-lifetime-qualification.json),
[T04283](recovery/gm-input-transaction-qualification.json). T042 is bridge46 at773f5e9e
plus daemon37 atdb5864c2, not Linux live main. Runtime sources are unchanged in this
block; only these connected boundaries were re-inspected. Older worker/storage/R3
proof remains evidence, not a new audit/run.

## Necessary primitive probe and API sources

Old audits did not answer controlling-terminal availability. One isolated
[probe](recovery/evidence/t041-owned-terminal-probe/manifest.json) compiled a fixed
throwaway C fixture under the unchanged independent guardian: one driver/eight checks
passed. Own PTY allocation; setsid/TIOCSCTTY(0) and SID/PGID/foreground identity; two
inputs including Unicode; resize/SIGWINCH; canonical VEOF while child stays alive;
exact pidfd TERM/reap; ECHILD; master hangup. EIO(5) appeared after slave holders were
gone. Driver and guardian both ECHILD, no emergency signals/failures/deadlines. Zero
tests/runtime changes/systemd probes/settings changes. Actual GCC14.2.0-19/compiler
hash, SDK10.0.401, installed runtimes8.0.31/10.0.12, PS7.5.4 are recorded; no C# ran.
These are available primitives here, not integrated native backend qualification.

Primary API facts checked 2026-10-06: Linux man-pages
[posix_openpt](https://man7.org/linux/man-pages/man3/posix_openpt.3.html),
[setsid](https://man7.org/linux/man-pages/man2/setsid.2.html),
[TIOCSCTTY](https://man7.org/linux/man-pages/man2/TIOCSCTTY.2const.html),
[TIOCSWINSZ](https://man7.org/linux/man-pages/man2/TIOCSWINSZ.2const.html),
[termios](https://man7.org/linux/man-pages/man3/termios.3.html);
Microsoft [ConPTY creation](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session),
[creation flags](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags),
[Job objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).
Inference from APIs/probe: neutral native PTY needs no environment setup here.
User-manager absence is retained from handoff; no positive systemd claim/setup.

## Minimal connected interface

Internal contracts: `BookOfEternityClient/Services/GmRuntime/IOwnedTerminalSession.cs`;
add only assembly `BookOfEternityGMBridge` friend access in client csproj.
Types: `TerminalSize(int Columns,int Rows)` (1..32767);
`TerminalIdentity(string RunId,string Backend,string Guarantee,int RootPid)`;
`TerminalRootExit(int? ExitCode)` (notification only);
`TerminalStopEvidence(TerminalIdentity Identity,GmWorkerStopState State,string Reason,
bool CleanupComplete,bool AuthorityRetained)`.

```csharp
internal interface IOwnedTerminalSession : IAsyncDisposable
{
    TerminalIdentity Identity { get; }
    Stream InputWriter { get; }
    Stream OutputReader { get; } // merged PTY stdout/stderr, one reader
    Task<TerminalRootExit> RootExited { get; }
    ValueTask ResizeAsync(TerminalSize size, CancellationToken waitToken);
    Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken waitToken);
}
```

`OwnedTerminalSessionFactory.StartNeutralAsync(NeutralTerminalLaunch launch,
CancellationToken waitToken)` returns retained session; partial-start exception carries
that same owner. NeutralTerminalLaunch contains fixed fixture executable/argv, fresh
scratch cwd, size, explicit NativeLineage; no GM launch string/save root/worker Release/
production permit. Future production factory requires authentic consumed main-run
admission before executable release, never an optional/Boolean fence bypass. Neutral
entry is fixture admission, not public arbitrary-command launch or final CLI UX.

BridgeHost stores IOwnedTerminalSession. Original BeginInputLifetime receives its
InputWriter; original keyboard/manual/bootstrap/dispatch keep shared gate/immutable
operations. Original PumpOutputAsync receives its OutputReader. Resize captures the
session and runs outside `_sync`, never on a replacement. RootExited revokes matching
binding and starts same-owner stop even with live descendants. Status identifies actual
backend/scope/uncertainty. No new prompt writer/coordinator.

The one output pump updates `TerminalScreen` with its existing stateful decoder under
`_sync`, publishing `TerminalViewObservation(string BindingId,long Revision,string Text,
bool Reliable)`. `CaptureTerminalView()` is the sole participating dispatch/readiness/
visibility reader. ObservePromptAsync requires matching binding, Reliable and Revision
strictly newer than pre-paste/pre-submit snapshot. `_outputVersion` remains byte-activity
diagnostics, not screen proof. Partial UTF8/control sequences/obsolete binding output
cannot advance a usable view; raw bytes still go unchanged to console. Neutral fixture
uses a documented UTF8/CR/LF/BS/cursor-home/erase-screen VT subset. Unsupported controls
invalidate positive auto readiness until supported full reset/redraw. Arbitrary live
TUI/alternate-screen/width semantics remain unqualified, never guessed. Windows uses
the same session output/view path; transport remains ConPTY.

Accepted DispatchPromptAsync and real pipe loop thus consume actual terminal input
AND output evidence. Native acceptance cannot substitute fake screens, a fixture
arbiter or a second dispatch implementation.

## Adapter lifecycle

Extend original single-thread helper with distinct `--terminal-v1` bootstrap
(T1/P1/B1/A1 run-bound frames, validated descriptor counts; host-v2 unchanged).
Helper allocates O_RDWR/O_NOCTTY/CLOEXEC master, grantpt/unlockpt/slave, initial size,
and one held root; exclusive reaper retains pidfd before release. Transfer master+
root pidfd via existing credential-checked seqpacket. Managed adapter makes
non-inheritable input/output duplicates and ACKs possession; close unused copies.
Child closes master/control/status ends, waits for release, then setsid (no preceding
setpgid), TIOCSCTTY(slave,0), foreground PGID, dup2 slave to0/1/2, restores signals/
mask, execs absolute fixture argv. Parent closes slave after fork. Distinguish prepared
endpoints from successful exec/tty attachment in Ready/Started; retain partial failures.

`Services/GmRuntime/LinuxOwnedTerminalSession.cs` reuses status/stop/retention through
one extracted `Services/GmWorkers/NativeLineageOwner.cs` transport-mode core from
GmWorkerNativeLineageLaunch. No worker admission/ledger supplied to neutral main.
One native launch/discovery/reap engine; no managed PPID walker. Host-v2 still maps to
its original drains/host identity. Existing repo-built package, no PTY dependency.

`LinuxPtyStream` uses nonblocking descriptors, poll+cancellation wake pipe and one
retained reader; no abandoned blocking FileStream read wrapped in WaitAsync. Wake/join
actual operation before closing handles. Writes keep accepted framing/backpressure;
flush is not CLI acceptance. Resize uses TIOCSWINSZ on held master, foreground
SIGWINCH, no guessed group signal. Ctrl-C/EOF are explicit profile/manual input governed
by line discipline, not stop proof. Canonical VEOF can return zero child read without
exit; raw mode may treat byte as data. Master has no pipe input half-close. Closing it
can hang up the attached session, so it is not a graceful EOF API. Established PTY's
expected last-slave EIO is transport end; startup/other I/O errors are faults. Neither
is ownership proof.

Stop: revoke input origin, retain started writes; request original stop independently
of caller wait cancellation; retain session/master/native authority/actual I/O tasks.
Helper TERM/KILLs only held own pidfds, discovers direct/adopted children, exclusively
reaps to ECHILD. Output task has its own retained lifetime so input revocation does
not discard final output; drain to real EOF, then settle managed tasks. Only matching
confirmed scoped stop AND drain allow disposal/binding removal/replacement. Bounded
timeout returns Uncertain retaining same retirement task/owner, no automatic second
session. Uncertainty stays absorbing even if cleanupComplete later becomes true.
Status/cancel/diagnostics remain responsive while native stop/drain is pending.

Windows adapter keeps ConPTY UTF8/pipes/ResizePseudoConsole. Main arbitrary executable:
CREATE_SUSPENDED plus extended startup attributes, retained Job assignment before
ResumeThread/input release, no breakaway; original Job terminate/query-empty evidence.
Assignment/resume/stop errors retain partial resources/Uncertain. No Process.Kill
fallback reporting Job-empty. Neutral Linux slice may add the consumed Windows adapter
and guarded/build checks; Windows native qualification requires a Windows environment.

Future SystemdOwnedTerminalSession owns transient unit and terminal broker inside it,
reuses PTY transport; authentic unit/cgroup empty evidence, not broker-lineage ECHILD,
is its stop authority. Implement where existing user manager is available. Final Auto
can select declared qualified native before launch when primary unavailable, never
after launch. Do not copy worker selector's NotImplemented shortcut into final main Auto.

## First slice: actual bridge with one persistent neutral process

**Visible result:** fixed test CLI displays and accepts two manual commands in one
PID/session, resizes, preserves draft and reports precise scoped stop. Actual prompt
RPC proves paste/fresh view/one submit plus manual takeover/cancel on that same terminal.
No GM or saves. Internal fixture-only `--neutral-terminal` Program entry chooses fixed
fixture/scratch status+config; skips shell bootstrap/CLI/daemon/worker auto-start.
Uses actual BridgeHost lifecycle/pumps/accept loop, not a second host. Normal host stays
Windows-gated on Linux and cannot use neutral admission to release game-writing code.
Retarget bridge csproj net8.0/runtime-guard Windows P/Invoke for actual Linux neutral entry.

**Review focus:** root exits with live PTY holder; stop during blocked I/O; partial or
unsupported screen redraw; stale resize/key/operation; partial launch/lost status.
These are acceptance cases below, not additional product requirements.

### Execution steps (one connected reviewed block, after authorization)

- [ ] Create `BookOfEternityClient.Tests/GmOwnedTerminalSessionTests.cs`,
  `GmOwnedTerminalLinuxTests.cs`, fixed raw-mode CLI
  `tests/fixtures/LinuxTerminal/neutral-cli.c`. Reuse unchanged LinuxHost guardian,
  independent per-test scratch/deadlines. Causal RED: current actual Linux entry cannot
  bind terminal/view. Preparation failure is not causal RED. Managed retirement RED:
  current StopShell must not clear owner/admit replacement on root-exit while descendant/
  output remains. Pin actual executed assertions before modifying behavior.
- [ ] Implement interface/factory, LinuxOwnedTerminalSession/LinuxPtyStream and shared
  NativeLineageOwner; adapt GmWorkerNativeLineageLaunch host-v2 without admission/ledger
  change. Original C helper gains transport/tty setup, same ownership engine. Build
  via scripts/build-linux-supervisor.ps1, preserve source/compiler/binary hashes.
  First GREEN: own-root terminal startup and exact cleanup before descendants expand.
- [ ] Add `BookOfEternityGMBridge/TerminalScreen.cs`, consumed owned ConPTY adapter;
  adapt Program/ConPtySession/csproj/friend access. Wire original lifetime/output/
  keyboard/resize/root-exit/StopShell and retained native stop. Coherent output view
  feeds original PromptDispatch partial and operational readiness consumers; no direct
  Win32 screen dependence there. Preserve identity/no replay/manual takeover/phases.
- [ ] Exercise actual entry/pipe loop/terminal. Controlled console input+size suppliers
  may enter at actual pumps; screen/dispatch cannot be replaced. One foreground owned
  interactive smoke when TTY available; restore captured UI input mode on exit, no
  inherited/system terminal changes. Neutral result remains internal technical proof.
- [ ] Structurally add category owners `gm-owned-terminal-session` (portable consumed
  bridge/interface/view/retirement) and `gm-owned-terminal-native-linux` (real packaged
  PTY/owner/guardian), run only scripts/test-csharp.ps1. Existing output/input/prompt/
  host regressions selected only for contracts actually changed; do not rerun successful
  IPC/ENV/storage/R3 without source cause. Record planned/completed counts/hash/cleanup,
  preparation failures separate, guardian ECHILD/no unexplained emergency signals.
- [ ] Checkpoint/normal push/readback, independent Sol6.1/xhigh source/evidence review,
  fresh GitHub-only restore; handoff/stop before run fence/systemd/live work.

Proposed commands, not executed in this design block:

```powershell
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category gm-owned-terminal-session,gm-owned-terminal-native-linux -PlanOnly
pwsh -NoProfile -File scripts/test-csharp.ps1 -Category gm-owned-terminal-session,gm-owned-terminal-native-linux -Parallelism 1
pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog
```

| Causal acceptance case | Required observation |
| --- | --- |
| PersistentManual_TwoInputs_OneOwnedSession | Same PID/SID/foreground tty, two manual commands/Unicode, original keyboard/write gate/raw output. |
| ResizeAndEof_AreTransportEvents | New size/SIGWINCH; canonical EOF leaves owner active; raw EOF treated per mode. Root exit/transport EOF cannot clear owner. |
| ActualRpc_TwoPrompts_OneBinding | Accepted actual DispatchPromptAsync, real session fresh paste/submission views, one submit each, same input binding. |
| ActualRpc_DraftTakeoverCancel_NoReplay | Draft preserved, pre-submit takeover no submit, missing post-submit observation UnknownOutcome; real status/cancel pipe loop responsive. |
| RootExit_DetachedHolder_ExactScopedStop | Root exits first, bounded setsid/double-fork descendant holds slave/ignores TERM; same owner reaps all/ECHILD, guardian independently empty, unrelated own sentinel survives. |
| StopLateForkAndBackpressure_NoReplacement | Bounded late spawn or output/write holder: replacement waits for matching scope proof AND actual I/O settlement, not EOF/root exit. |
| StopTimeoutOrStatusLoss_RetainsOwner | Deadline/error Uncertain with retained owner/tasks, no restart/dispose/acceptance, diagnostics responsive. Guardian cleanup is not runtime positive stop. |
| StaleBindingAndPartialLaunch_NoCrossSessionActions | Old view/resize/key/request cannot affect replacement; descriptor/exec failures carry owner; host-v2 preserves original semantics. |

## Dependencies, decisions and later sequence

Needed for neutral: existing compiler/package, available PTY, original bridge/T042,
narrow reliable screen. No manager, production pool or save-root admission required.
TTY/session bootstrap/cancellation-safe fd settlement remain implementation work;
probe alone completes neither. No product decision blocks first slice. Neutral argv/
profile/viewport/poll choices are bounded engineering, not final live UX. Before live
qualification resolve unsupported CLI observations while preserving arbitrary configured
CLI/model/manual unknown/trust/update handling; no automatic Ctrl+U/trust/update.

After neutral: (1) existing-manager systemd terminal adapter/native Windows qualification;
(2) authentic durable main owner/run fence at launch and participating write/recovery
operations before game-writing Release; (3) normal Linux launcher/daemon/live configured
CLI acceptance. Enabled helpers additionally need production worker root/ledger
admission; no-worker persistent main does not. Fence is neither save protection from
owner nor cold delivery deduplication. Provider/saves/public rollout remain later.

Status: design candidate, independent Sol6.1/xhigh review and final persistence pending.
