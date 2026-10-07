# Bounded lifecycle diagnosis — causal gate before another live attempt

Source #1553. Owner requested whole-chain systematic diagnosis on 2026-10-07,
read-only Astra/xhigh review, sole Sol6.1/xhigh writer. Source/evidence checkpoint
`ed02178b8eb2684f318e94fb42ec7d707b70e507`; runtime remains `1c64050b`.
No new implementation, tests, CLI startup or provider request in this diagnosis.
Actual provider requests and accepted game turns remain **0**. Historical
Uncertain records are retained; ECHILD does not authorize continuation.

## Actual attempts and first supported boundary

`root` denotes a separate disposable game. These rows count attempts, not passed
cohorts. Earlier diagnostic startup and real-game attempts have different goals.

| Attempt / executed source | First supported failure or result | Delta entering that attempt | Logical / physical cleanup |
| --- | --- | --- | --- |
| Codex diagnostic Q1, `13cdd9ae` carrier | TERM=dumb prompt; readiness blocked, no answer/request | Original configured M1 route; no renderer change | Original Stopped ACK, EOF/termios; ECHILD/0 emergency |
| Codex next screen, live-Q1 receipt | After one authorized y, SQLite14; state-home mount read-only before model. Driver aborted before intended stop | Only authorized prompt answer | Retained Uncertain; no original ACK/termios; ECHILD/1 emergency, not logical success |
| OpenCode startup, `8e21eb78` | Persistent mini output observed; parser/profile readiness unsupported | Official workspace install 1.18.35, separate free-model profile | Original Stopped ACK/EOF/termios; ECHILD/0 emergency |
| OpenCode draft observation, `fcef74fc` | Driver rejected intermediate startup frame before paste | Read-only standard editor witness prepared | Original Stopped ACK/EOF/termios; ECHILD/0 emergency |
| OpenCode draft observation, `1823aa50` | Actual 4,919-byte Unicode/multiline editor witness; no submit/provider | Wait for completed pinned startup frame | Original Stopped ACK/EOF/termios; ECHILD/0 emergency |
| ordinary root og1, `325b32fd` | Client CPR/observation timeout before NewGame; no CLI | First real ordinary one-turn driver | Client exit−2/EOF/termios; ECHILD/0 emergency; no main created |
| ordinary root og2, `fc1dda09` | NewGame/cancel/derived CLI Ready succeed; Continue fails original admission before player prompt | Client stdin stays PTY, stdout captured; no fabricated CPR response | Original Stopped ACK/EOF/termios; ECHILD/0 emergency |
| ordinary root og3, `e4a39313` | Same Continue failure, after five-method controlled admission GREEN | Actual consumer wrappers qualified; full Continue still uncovered | Original Stopped ACK/EOF/termios; ECHILD/0 emergency |
| ordinary root og4, `d8492fd1` | Reaches player UI; one action fails client preflight before request. Causal tests later prove current-bootstrap and consumed-snapshot gaps | Full Continue participating wrapper qualified on actual shipped console | Early daemon CtrlC interrupted caller admission before main shutdown; retained Uncertain, ECHILD/0 emergency |
| ordinary root og5, `19a8e7ea` | NewGame/cancel succeed; durable Running then first status bootstrap-lost/TerminalUncertain before Ready/daemon/action. **Initiating exception unknown** | Current bootstrap/cancel fixes; qualified main-first cleanup; fresh byte-verified package | One original shutdown refused; durable Uncertain, bridge EOF/termios unconfirmed; ECHILD/0 emergency |

Receipts: [Codex diagnostic](codex-q1-handoff.md), [Codex continuation](codex-live-q1-q2-handoff.md),
[three OpenCode observations](recovery/evidence/opencode-q1/), and actual
[ordinary attempts](recovery/evidence/opencode-live/).

## Controlled evidence; do not substitute it for game acceptance

- Accepted connected parser/T042 block: `2e9f66e0`, 20/20; strict pinned mini
  subset, real pipe loop and external draft witness. No post-model/general TUI claim.
- Five real console admission consumers: `9e4b20dc`, 1/1; full Continue was not
  covered, as the next actual attempt demonstrated.
- Full shipped Continue: causal RED `91b48442` identifies missing-read recovery
  outside participation; minimal whole BuildGameResponse wrapper at `e825d3c3`,
  1/1 GREEN, player prompt/normal exit, 13 original ClosedObserved pins and Stopped.
- Current NewGame/initial cancel: causal RED `f310f39b`; `0d1ed97a` 1/1 GREEN
  after three existing guardian values, current item/wound authority bootstrap and
  consumed snapshot retirement only after confirmed restoration.
- Actual player cancel: `da062f52` one causal manifest RED plus two BOM preparation
  failures; `1c64050b` three new GREEN cases including genuine restore failures
  retaining exact authority/evidence. Attraction retention and three source guards
  also GREEN; stale held-Clear fixture separately corrected/qualified at `8116f7ba`.
- Forced admission CtrlC diagnostics: `8fe993c3`, `e9f74a18`, `759c1618`,
  `b7eee94e`, `190baae0` each execute one failed positive expectation. SIGINT helper
  shield did not establish closure. Later traces prove serverActive precedes caller
  receipt; they do not prove JSON-close failure. Caps/missing phases stay inconclusive.
- Revised main-first stop: `767ef6a7`, 1/1 GREEN, four original pins ClosedObserved,
  full-identity Stopped ACK before daemon CtrlC, exit0/EOF/termios/ECHILD0 emergency.
  Five directly affected helper cases 5/5 GREEN; caught-loss retains Uncertain.
  Independent Sol source/evidence PASS, 89 pins/61 artifacts. No general guarantee
  for every PreparedGrant race. Old forced-CtrlC expectation remains unqualified.

Exact sources/results are linked from [current execution plan](opencode-live-plan.md).
Preparation failures above are not causal REDs or runtime qualification.

## Complete available og5 trace and the missing boundary

All 20 original stored/decompressed artifacts are in
[original-bootstrap-lost](recovery/evidence/opencode-live/original-bootstrap-lost/manifest.json).
Original RPC journal contains first status at 6.610s and sole shutdown at 6.784s;
bridge raw is its 439-byte banner only. No native raw handshake frames or initiating
managed exception were captured. There is no fuller internal trace to supply.

1. Original root `a4bce4411f7c4cf88f77c80f8f03856b`, generation
   `8d44d89c2d2b4039874f56e8f95424f1`, epoch1 has durable Running; original shell
   PID794616 and helper794597. Running is the archived manifest Result.OriginalRunningRecord; the separately
   [final record](recovery/evidence/opencode-live/original-bootstrap-lost/play/.boe_runtime/gm-runs/main.json.gz)
   is Uncertain/nullStopEvidence, not a Running proof.
2. Source publishes Running **before** `prepared.ReleaseAsync`
   ([coordinator](../../BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.cs)).
   Running alone is not release, readiness, or admission authority.
3. Native bootstrap-lost is recvmsg EOF/error with FD open, after held-root B1
   transfer in this case. Valid A1 opens gate then closes this FD immediately
   ([native helper](../../native/linux/boe-lineage-supervisor.c)). This confines the
   observation before accepted release; it does not reveal the initiating cause.
4. Managed first-loss is latched; ordinary release catch latches
   terminal-release-unconfirmed before its CloseBootstrap. This makes a race during
   send/dispose/Started observation a hypothesis, not a verified socket defect.
5. Command/CWD fields are assigned only after LaunchProduction returns successfully.
   Their blank values are a symptom. `RunAsync` catches and discards the actual
   OwnedTerminalStartException chain ([Program](../../BookOfEternityGMBridge/Program.cs)).
6. Refused original shutdown preserves Uncertain. Client exit−2/EOF/termios and
   guardian ECHILD/driver1/emergency0/reaped2/failures0/deadlinefalse are physical
   observations; bridge I/O/disposal/Stopped ACK did not qualify. Elapsed11.823s.

## Working versus failed boundary and budgets

Og5 uses exact fresh-built DLLs from the successful `767ef6a7` production fixture:
678 shipped files / 99,049,670 bytes; both DLLs and all package hashes checked.
Package has no source/TestSupport/compiler, only player .NET8/PowerShell7 prerequisites.
Successful control instantiates BridgeHost through reflection inside TestSupport,
with TestSupport AppContext and redirected host I/O. Og5 uses the actual standalone
ordinary launcher/Bridge AppContext and foreground controlling PTY. This is a
material unqualified composition boundary despite equal DLL bytes. A blindly
reflected mini-profile diagnostic would fail its packaged-editor apphost check
before native launch; it must not be reported as this causal failure.
[Deployment proof](recovery/evidence/opencode-live/og5-preparation/manifest.json).
Native helper source/provenance are unchanged. Positive control uses configured
neutral command, neutral profile and no external-editor configuration. Failed og5
adds ordinary current NewGame, pinned mini/editor child environment, private own
XDG/TMP directories and OpenCode command; full differences must be tested, not
attributed to the model or parser before release.

Parent: Running10s, Ready15s, work210s/cleanup270s, original shutdown RPC12s,
EOF5s. Independent live guardian300s; controlled default30s. Native initial
bootstrap5s/held-phase5s; managed initial bootstrap5s/held deadline5s/Started wait5s;
scoped stop2.5s plus100ms TERM grace, managed stop/output observation5s each;
main pin drain5s. Limits are unchanged. Actual phase timestamps are missing;
no exhaustion has been demonstrated. No budget increase or scope weakening proposed.

## Review gate and next sequence

Read-only `/root/bootstrap_lifecycle_astra_review` (Astra/xhigh) reviewed the
entire source/evidence/deployment chain, seeking minimal cause or unnecessary
coupling, without new architecture or ownership relaxation. Source/evidence review
of successful bounded launches is reused. [Final diagnostic verdict](recovery/bootstrap-lifecycle-review.json): failure
localized before accepted A1, initiating cause unknown; no runtime correction or
live retry supported. No live attempt until causal conclusion.

If cause is established: one narrow actual-boundary RED, one minimal correction,
independent review and GREEN, ordinary checkpoint/readback, then one fresh game
attempt. If evidence is insufficient: reviewer must name one missing observation
and one controlled inert experiment preserving actual standalone Bridge AppContext;
no speculative patch or repeated CLI startup.

Read-only Astra interim: all20og5 artifact hashes and all678package bytes match;
all61controlledartifacts checked. First-loss latching narrows the send/dispose/
Started race hypothesis. Official .NET8.0.31 CloseAsIs invokes TryUnblockSocket
only while the SafeHandle is unreleased; Shutdown(Both) is not an unconditional
Seqpacket disposal behavior. No actual invocation in og5 is proved. Publicsource
URLs/hashes retained in recovery/bootstrap-dotnet-source-pins.json, source bytes
locally read-only under /workspace/qualification-1553-bootstrap-diagnosis/.
Root og4/og5 and historical Codex Uncertain metadata remain intact. Systemd draft
63e99009 stays deferred; primary systemd/nativeWindows/fullclientVT/cold are open.

Final source inventory: /usr/bin/strace exists. No trace/probe/process/test was
executed after the owner pause, and no instrumentation/fix has been implemented.
The next experiment must preserve actual standalone application directory and
original helper/PTY/guardian scopes; public syscall evidence can reduce invasive
diagnostic changes, but complete exception capture remains a required boundary.
Checkpoint is diagnostic-only; bounded live gameplay remains open, not accepted.

Independent Sol metadata/current-tip restoration PASS at6bbb376e: all22355
trackedfiles/363042586bytes, exacttree/remote/clean/fullfsck0, noalternates, explicit
depth1currenttreeonly. [Archived candidate proof](recovery/bootstrap-diagnosis-restoration.json).
Final metadata carrier gets separate exact remote/readback and fresh GitHub-only
restoration before writer handoff; no runtime/fixtures/tests changed by this closure.


## First authorized inert standalone trace — cause not reproduced

Source421d01c6, actual standalone relocated ordinary Bridge/AppContext and mini/editor/current NewGame+initialcancel, exception-onlypostfailure logging, strace6.13. Fixedneutral executable for pre-exec question; no Ready/daemon/input/provider. Complete A1send35 at1791379111.360604, managedclose87 at.360935, nativeA1recv35 at.366256, no shutdownsyscall. Original58c1b462f4fb43c28257ad02eed0c31f fullidentityStoppedACK/record and tracer/controlPTY EOF/termios; guardianECHILD/driver0/emergency0/reaped1/failures0/deadlinefalse, elapsed21.616s. All12artifacts and677actualpackage files sourcepinned. [Trace manifest](recovery/evidence/opencode-live/standalone-inert-trace/manifest.json).

This healthy execution does not reproduceog5, prove its initiatingcause, qualify a runtime correction, or permit a realCLI retry before causal gate. Tracer scheduling effect retained. Independent Astra feasibility/Solsource PASS; nextcausal observation under read-onlyreview. No historicalUncertain changes. Provider/game turns0.
