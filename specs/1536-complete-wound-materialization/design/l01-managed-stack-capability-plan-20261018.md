# L01 managed-stack capability probe and held future observation

Task: #1536 / NI-IOBS-L01. Base `39cafe9e9595730929efb7b7450b4cc260b5bafc`.
Sole writer Root; independent reviewer Astra XHigh. User authorizes cloud inventory,
documentation and an owned synthetic smoke only after independent minimal-plan review.
**S01 FIRST PASS saved at actual4d476b38 after independent SOURCE/PREP ACCEPTd2c716af.
Independent EVIDENCE pending. One-shot permit consumed; gameplay release NONE.**
[Saved original evidence](../recovery/l01-managed-stack-s01-original-evidence-20261018.json).
[Independent PLAN review](../recovery/l01-managed-stack-capability-plan-independent-review-20261018.md).

## Question and available route

Can this unchanged cloud Linux security context obtain and interpret a managed stack
using installed components, without installing anything or probing any game process?
Success requires matching real managed frames to independently known synthetic work,
not merely producing a file or finding a diagnostic socket.

[Read-only inventory](../recovery/l01-managed-stack-capability-inventory-20261018.json)
pins9 critical files and163 already cached .NET8 reference assemblies. Installed:
SDK10.0.402 compiler, .NET8.0.32 runtime/createdump/DAC, SDK DiagnosticsClient
0.2.15.611 targeting netstandard2.0, PowerShell on .NET10.0.13. Reflected public
WriteDump overload accepts CrashReportEnabled=4. Bounded searches did not find
dotnet-dump/stack/trace, ClrMD, TraceEvent, SOS, lldb or gdb; do not install them.
PowerShell's first metadata-only startup failed134 on read-only home cache, before
any attach. Private XDG paths corrected that metadata read to0; no rights changed.

Chosen route: SDK DiagnosticsClient.WriteDump(Normal, private absolute path,
CrashReportEnabled), requesting the target runtime's matching createdump. Its
built-in DAC unwinder and JSON writer can emit managed method names, tokens,
IL offsets, module names and MVIDs. This is a source-supported possibility,
**now demonstrated on the owned synthetic only, pending EVIDENCE; not a general dump analyzer**.
Direct createdump --crashreportonly exists, but is not an automatic fallback.

- [Official client sample](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostics-client-library#write-a-core-dump)
  establishes the public Normal dump request; local reflection establishes this ABI.
- [Runtime8.0.32 unwinder](https://github.com/dotnet/runtime/blob/v8.0.32/src/coreclr/debug/createdump/threadinfo.cpp)
  and [JSON writer](https://github.com/dotnet/runtime/blob/v8.0.32/src/coreclr/debug/createdump/crashreportwriter.cpp)
  describe managed frames and names; bool/hex fields are serialized as strings.
- [Runtime collection](https://github.com/dotnet/runtime/blob/v8.0.32/src/coreclr/debug/createdump/createdumpunix.cpp)
  suspends threads and later resumes them. Capture perturbs its target.
- [Official permissions FAQ](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/faq-dumps)
  describes ptrace/proc access and possible seccomp/LSM denial. Here UID1000,
  CapEff0, NoNewPrivs1, Seccomp2; Yama/LSM setting files are unavailable. These
  observations alone prove neither permission nor denial. No manual/system security
  setting changes. The normal runtime API itself authorizes its specific createdump
  child with PR_SET_PTRACER before that child uses ptrace/proc; this route is not
  ptrace-free. See [runtime PAL](https://github.com/dotnet/runtime/blob/v8.0.32/src/coreclr/pal/src/thread/process.cpp#L2353).
  We do not invoke prctl, grant ANY ptracer, change sysctl/setcap/seccomp/LSM policy,
  or use direct attach. If normal runtime authorization is denied, retain that failure.
- [Diagnostic-port contract](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostic-port)
  requires same-user access and a matching TMPDIR; use a new private namespace.

## Minimum synthetic S01, after plan review

1. Author only an external, task-local helper and tiny synthetic C# program. No
   production/test project/catalog/selection/budget/assertion changes. No game
   assembly references, fixtures or saves. Public controller has no arbitrary-PID
   attach mode: it launches its own synthetic target and internal collector.
2. Compile the synthetic program directly with installed csc and cached net8 refs,
   without restore/tool installation/network. Runtimeconfig pins8.0.32, roll-forward
   disabled. Record compiler command, native exit, exact source/reference/tool/output
   hashes and fresh build identity. This is helper preparation, zero game tests.
3. Save/push/recover helper source and preparation before independent SOURCE/PREP
   review. Only after acceptance admit one named S01. No implicit fallback/retry.
4. Use the unused inventory-planned short `/tmp/l01ms-<16hex>` root,
   private TMPDIR/XDG/output folders and a sanitized explicit child environment with
   no credentials or game paths. Preflight requires the worst-case Unix socket path
   below108 bytes including its numeric PID/starttick budget (currently92 bytes
   excluding NUL), avoiding a known long-TMPDIR risk before launch. The superseded
   long planned root was never created or used. Do not reuse prior L01/M01 namespaces. Target has
   two NoInlining/NoOptimization known call chains: main waits on private stdin in
   KnownWaitLeaf/Outer; one worker executes bounded KnownCpuLeaf/Outer. It reports
   nonce, PID/kernel starttick, native thread IDs and readiness after real CPU work.
   A private stop token ends it; a60-second self deadline is only a backup while its
   threads run. It cannot fire while createdump has suspended those threads.
5. Controller records its spawned ownership, target command/nonce, PID/starttick,
   same UID and exact private diagnostic socket. Recheck immediately before the
   one request; a missing/exited/replaced identity is a HOLD, never a PID guess.
   A private PowerShell collector loads only pinned existing SDK dependencies and
   calls the public API once. An external controller enforces its20-second deadline
   independently of suspended target/collector code. This is a synthetic tooling bound,
   not a game timeout change. No process-wide endpoint enumeration or unrelated PID.
6. Drain original stdout/stderr concurrently. On return, parse the original JSON:
   at least the two independently reported native thread IDs must have the known
   managed leaf/outer frames in order, nonzero method tokens, expected module name
   and actual compiled MVID. Decode names/tokens/offsets into readable stacks.
   Match those frames to source and observed known waiting/CPU work. File existence,
   native0, guessed names from logs, or native-only frames alone cannot pass S01.
7. Send stop token and require target0, join/EOF and bounded owned cleanup. On a
   failure, preserve the first result, stop only identity-verified own processes,
   and independently diagnose. External watchdog cleanup cannot rely on the suspended
   target self deadline. Reviewed implementation ruling: the external supervisor may
   first terminate its exact collector (IPC client, not the tracer), retaining the
   target pidfd, then immediately account for and terminate the exact createdump
   child if necessary before stopping the target and joining/draining EOF. Collector
   exit alone never proves ptracer exit or target resumption.
   Any createdump descendant cleanup must first prove
   target parentage, executable, private output argument and PID/starttick; otherwise
   report the blocker, never signal by name. No global kill/process scan.
8. Preserve raw JSON, command/stdout/stderr, timing, identities, compile/ABI and
   interpretation evidence; save/push/recover before independent EVIDENCE. Core
   minidump stays private with size/hash (no full-core recovery claim); durable raw
   stack JSON is the interpreted artifact. Do not upload a memory core to GitHub.
   No game qualification count changes, even if S01 passes.

If API/dependency loading, IPC, ptrace/proc, DAC or naming fails: retain exact native
status/HRESULT/errno and failing stage. No privilege escalation, setcap/sysctl/prctl
changes, install, direct-createdump fallback, repeated smoke or gameplay run is
automatically authorized. Record what access/component is missing and what is unproved.

## One future distinguishing custody-to-accepted observation — PROPOSAL ONLY

After S01 and independent evidence review, prepare a separate source/PREP/ownership
plan for exactly one existing diagnostic Fact
`GuaranteedTreatment_DiagnoseCurrentResumeAfterInterfaceMemberProcessLoss` (cut1).
It is diagnostic, outside the original five-case qualification. No launch is released
here; original crash5 and all previous outcomes remain frozen.

Prerequisite currently missing: JointInterfaceCrash.cs:128,144–150 reads child stdout
with ReadToEndAsync and publishes it after join. No safe live custody/accepted signal
is exposed to an external observer. A separately authorized/reviewed minimal test-side
tee or equivalent owned delivery must preserve exact raw stdout/join/EOF and primary
errors. Do not read/steal the parent pipe, add polling or assume a live artifact exists.
This is future proposal work, not an S01 or game-source change released here.

Proposed observation, conditional on that prerequisite: retain existing scalar diagnostic output and untouched220s
resumer/900s category. On the exact owned resumer's
`finalization-return/MortalJointCustodyCaptureCompletedBeforePreparation`, arm one
timer for60s after that observed return. If accepted-outcome/late return or exit
arrives first, cancel and record NOT_OBSERVED (no second target or earlier retry).
If still between custody and accepted, revalidate root/nonce/PID/starttick, source,
runtime/DAC and private socket, then request exactly one Normal+crash-report snapshot.
One request is not periodic stack/CPU sampling. Measure request/suspend interval and
observer elapsed/CPU cost; retain all original events and timeout outcome. Never
pause/reset/extend the220s clock or convert a perturbed timeout into a baseline result.
The future external observer needs separate implementation/review; do not silently
reuse the exhausted diagnostic2 sampler or old environment shell.

Expected discriminators from all named managed thread stacks:

| Observation | Supported conclusion | Still unproved |
| --- | --- | --- |
| Validation/normalization/JSON/fingerprint/file traversal frames under the selected resume call | Specific managed operation was active at capture; map module/MVID/token/IL to unchanged source | Hot loop, useful progress, total cost or remaining work from one snapshot |
| Managed lock/semaphore/task wait chain, with other managed workers visible | Named wait site and possible executing counterpart | Lock ownership/deadlock/async causality without heap/task/lock evidence |
| Native-only/missing/truncated frames, IPC/permission/DAC failure | Exact diagnostic limitation | Managed cause; do not infer idle or CPU exhaustion |
| Accepted/exit before trigger | This attempt did not capture the blocked interval | Explanation of earlier failures or crash5 qualification |

Use source-bound names plus existing scalar ordering to narrow the next causal
question. A snapshot cannot prove a finite completion bound, justify larger budgets,
or certify all five cases. If causality still needs heap/async-state decoding and no
installed analyzer suffices, stop with that precise tooling/evidence gap.

## Platform holds and completion boundary

| Platform | Current qualification / next step | Release now |
| --- | --- | --- |
| Cloud Linux | L0152/57 at original provenance; crash5 unqualified. S01 FIRST PASS saved, independent EVIDENCE pending; no personal Linux is available | NONE; own synthetic permit consumed, gameplay held |
| Windows | User may later qualify the five scenarios separately. Current identity helper reads Linux `/proc/<pid>/stat`, so Windows needs a separately reviewed native identity/ownership preparation while retaining strong assertions | NONE; do not request Windows, start tests, retry artifact403 or use CI |

Windows success would be separate platform evidence and would not prove cloud Linux
completion or explain this blocker. Windows is not an automatic remedy or dependency
of this cloud inquiry. Keep its future-five-case hold distinct from existing403.

M01 remains separately accepted. L0152/57, seven original fixture failures and all
later preserved outcomes, old66529 UNKNOWN/unavailable/recoveryClaim=false, frozen
roots and exhausted diagnostic2 permit stay unchanged. E01/T148/Phase10/fullNI/HOT/
F1/M3/T197 open/incomplete; A3 NO-PROCEED. This block may complete only capability
evidence plus a reviewed future-observation proposal, never a gameplay release.
