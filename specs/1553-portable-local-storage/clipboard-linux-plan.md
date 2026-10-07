# T050-CLIPBOARD-LINUX implementation plan

> Execute inline as sole writer with Superpowers executing-plans/TDD/debugging;
> independent actual Sol6.1/xhigh reviews are read-only. Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), US5/FR-012/SC-005 and owner authorization.

Accepted base `113edbb00eeeb9172ee3a02e00585f7bde442243`, branch `codex/1553-load-filesystem`.
Goal: reliable bounded Linux clipboard read through existing service and both real
console consumers; preserve manual draft, explicit acceptance and one read per gesture.
No game/GM model/balance/text/bridge paste policy, desktop installation/settings,
actual clipboard data, provider/CLI/browser/saves, systemd or other block changes.

## Source and minimal design

Existing `Services/ClipboardService.cs` has `IClipboardService.TryReadText()` and
`ClipboardReadResult(Success, Text, Error)`. Windows uses Get-Clipboard through
PowerShell; Linux refuses. `Core/TextComposer.cs` swallows failure as empty.
`GameEngine.TurnLifecycle.cs:GetPlayerInput` first uses TextComposer, then its late
clipboard shortcut check can reread successful clipboard text equal to a shortcut.
`UI/SpectreExplorerConsole.Ask` uses the same composer. These are the actual entrypoints.

1. Keep the public service/three existing result properties and normalization;
   add explicit outcome enum: Text/Empty/Unavailable/Error/Timeout/TooLarge/CleanupUncertain.
   Preserve Windows Get-Clipboard command/UTF8/STA adapter; use a bounded runner.
   Select available Windows executable before launching rather than retry an executed read.
2. Linux capability snapshot uses only own PATH and presence of Wayland/X11 session
   variables. Select one existing absolute executable: Wayland wl-paste first, then
   DISPLAY+xclip, then DISPLAY+xsel; a missing candidate may fall through before
   launch. Read-only args: wl-paste --no-newline --type text;
   xclip -selection clipboard -out -target UTF8_STRING; xsel --clipboard --output.
   No watch/write/primary-selection command, no shell concatenation, no tool/version
   probe that reads clipboard, no retry/fallback after a process started.
3. Concurrent bounded stdout/stderr byte reads + exit under one 2s deadline;
   stdout≤1MiB, stderr≤64KiB. Success only exit0+both EOF+strict UTF8 stdout,
   normalized nonempty text. Windows-controlled exit3 means Empty; other errors
   are generic Error (stderr content is not logged/displayed). Linux .NET reaps its
   children independently, so managed Process plus HasExited cannot authorize a PID
   signal. Preflight pidfd on own self before reader creation; after Process.Start,
   acquire a pidfd for the returned PID, then force `HasExited` on a newly created
   `Process.GetCurrentProcess()` (own self only), then check the original managed
   Process.HasExited. In .NET8 this own-self observation creates a Holder and crosses
   the managed child-table lock held across reaping and exit caching, including
   the PID1/original-SIGCHLD-ignored `reapAll` branch. Bare self construction is
   insufficient; explicit HasExited is required. Reject the descriptor if original exit is observed: this
   excludes a descriptor opened on an already recycled PID. Retain a validated
   pidfd as stable incarnation identity; Linux cleanup uses pidfd_send_signal only,
   never Process.Kill/PID/tree fallback. A concurrent exit yields ESRCH, never a
   signal to another process. If acquisition fails after launch, keep original
   process/I/O debt until actual exit; do not signal by PID. Windows retains its
   original Process/OS handle route. Wait/reap within 1s, close/cancel own streams.
   If unconfirmed, retain original
   cleanup reference/debt and refuse a new read until settled. Do not claim ownership
   of compositor/X11 server/external services or arbitrary descendant trees.
4. Composer treats a shortcut as one read request. Failure displays escaped Russian
   message and re-prompts without changing current default/manual multiline draft;
   no empty-return substitution. Success updates local draft/limited escaped preview
   and requires a subsequent manual acceptance (Enter in immediate mode, existing
   two-empty-line terminator in multiline). Manual replacement/clear/direct terminal
   paste remain their existing paths. No adapter/composer invokes bridge/daemon/GM.
   Remove late clipboard reinterpretation/read from GetPlayerInput. Clipboard content
   equal to /paste, /вставить or \\p is accepted as the value, not another read gesture.
5. Prefer a new clipboard-only mode in the existing PortableStorageCrashHost test
   fixture: the public real SystemClipboardService runs under a child-owned PATH/display
   environment containing only own synthetic readers, never a global override.
   This permits causal baseline RED without adding a production constructor first.
   No production test-only executable or second clipboard implementation.
   Fixtures install nothing: own GUID executable names use Python/shebang with
   synthetic bytes, own argument/invocation/PID receipts and independent in-process
   hard lifetime. Real service performs process I/O. Input streams/screens are
   controlled through actual IConsoleInputSource/real GameEngine private entrypoint
   and SpectreExplorerConsole.Ask; no provider or game loop starts.
   A counted synthetic IClipboardService also isolates the old double-read consumer
   RED; GREEN additionally consumes the real public service and synthetic executable.
   ReadLine null/EOF must never count as manual acceptance or multiline terminator;
   raise a typed closed-input/cancellation carrying the local draft, without logging
   its text or saving it to a new journal. Confirmed exited/reaped readers receive no
   later signals; unfinished I/O/original Process stay retained if cleanup is uncertain.

Tool semantics: [wl-clipboard upstream](https://github.com/bugaevc/wl-clipboard),
[xclip man](https://github.com/astrand/xclip/blob/master/xclip.1),
[xsel man](https://github.com/kfish/xsel/blob/master/xsel.1x). Native read tools are
foreground readers; use no copy/watch/input mode. This does not qualify their actual
desktop protocols here. No tools or DISPLAY/WAYLAND_DISPLAY were present in the
read-only environment inventory; no user buffer command ran.

## Bounded RED→GREEN sequence

- [x] Design checkpoint ordinary commit/push + remote/readback; independent Sol review.
- [x] First RED: new Linux service fixture reaches real TryReadText and fails old
  Windows-only refusal; real GetPlayerInput counts two reads for literal shortcut,
  and real Ask/composer shows missing failure/premature clipboard acceptance.
  Preparation failures are separate; do not treat compilation failure as RED.
- [x] Implement minimal adapter/result/runner and consumers, then GREEN same cases;
  checkpoint source before expanding failure/limit matrix.
- [x] Sequential bounded matrices: each tool/prelaunch selection/no session or tool,
  text/Unicode/multiline/invalid UTF8/empty/exit error, stdout/stderr flood, timeout,
  startup failure, scoped cleanup; actual turn/Ask/multiline draft/default on failure,
  literal shortcut/one process/manual acceptance and no replay. Own fixtures never
  invoke real clipboard programs or read/mutate user clipboard; no child descendants.
- [x] Existing affected normalization/composer/direct-paste tests only; narrow source
  guard PlayerInput_MustExposeClipboardPasteShortcut (which currently requires the
  late ResolveClipboardPlayerInput helper) must be adapted and selected. Create two narrow
  categories `clipboard-linux-adapter`, `clipboard-console-consumers`; separate exact
  affected regression category if required, no console-explorer/e2e broad cohort.
  Update catalog structurally and current selection. All execution only via
  scripts/test-csharp.ps1; PlanOnly/ValidateCatalog discovery is not executed tests.
- [x] Source review; evidence receipts include exact source/hash/commands/executed
  counts, fixture PID/arguments/deadline/cleanup, SDK/runtime/PowerShell provenance.
  Catalog validation, independent evidence/metadata review, final remote readback,
  fresh GitHub-only source restoration, handoff and stop.

## Qualification limits and decisions

Observed now: Linux x86_64, SDK10.0.401, runtime8.0.31 (10.0.12 also installed),
PowerShell7.5.4. These were freshly read, not assumed. Native compiler is not used
by this block. Disk preparation removed only the previous own verified113edbb0 clone;
its restore proof/logs remain. Existing feature artifacts/constitution and project
Spec Kit skills apply; no global specify install/init or environment changes.

Synthetic adapter/consumer wiring PASS will not be positive Wayland/X11 desktop,
Windows clipboard/ConPTY/Job, live timed console/game, full T050 or game qualification.
Actual desktop/backend/device permission availability remains a separate environment
check using owner-approved synthetic content later. No new product choice identified:
manual acceptance/no automatic submit and draft retention follow this task's request.

## Execution ledger

Independent actual Sol6.1/xhigh `/root/clipboard_linux_design_review` PASS at
`02639ebbe21c15619e1714fc3c63ce3e7991dcfb`; EOF, exact late-helper source guard and
no signal after reap details carried above. Narrow fixture-seam amendment replaces
the proposed internal constructor with the existing test-host public-service route;
no runtime/test edits or execution yet. Amendment review precedes execution.

Source review at `30027e4555ab297108c7f69d313910e2b9ef9bca` identified the managed
Linux Kill race. This bounded identity amendment precedes its implementation and
requires independent Sol approval. Primary [.NET Process source](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Unix.cs)
and [exclusive child reaping/cache source](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/ProcessWaitState.Unix.cs)
support this correction; no runtime source patch, privilege or signal-permission
change is proposed. Own synthetic timeout/flood readers have independent8s
lifetime, so test cleanup remains bounded if pidfd capture is refused.
For the negative capture/debt case only, an internal instance constructor injects
the capture function (no public/global setting). It receives the real started
Process and may return no descriptor; all ordinary fixtures use the unchanged
public constructor. The controlled child then proves two CleanupUncertain results
with only one reader, retained original Process, actual natural exit, and a later
explicit gesture that settles the debt and launches once. This seam cannot claim
kernel race reproduction; stable identity remains a source-backed guarantee.
The self-observation barrier depends on the pinned .NET8 Process/Holder/reaper
implementation, not a public guarantee that Process retains an unreaped child.
Future runtime changes require rechecking that dependency. Test-host cleanup sends
no numeric PID signal and uses its existing independent12s lifetime; unconfirmed
exit retains original host/root and reports failure/debt, not successful cleanup.

Execution checkpoint `b5255b1fb96e405eb1ed195c6437b16ad5788246`: final38new+12affected distinctPASS; catalog valid0executed. Source reviewer Sol PASS after stablepidfd and causal fixture corrections. Initial3causalRED and intermediate1culture-sensitive test assertion failure are preserved, not preparation failures. See [handoff](clipboard-linux-handoff.md) and [qualification](recovery/clipboard-linux-qualification.json). Independent actual Sol evidence/metadata PASS at61a548ab; candidate GitHub-only restore20,755files verified. Final carrier metadata receives exact-tip push/readback/fresh restore before writer closure.

Final bounded verdict: actual separate Sol design/source/evidence/metadata PASS; evidence and metadata reviewed at `61a548ab5def2ab5fb8136b8215918e7dce54b2f`. [Candidate proof](recovery/clipboard-linux-candidate-restore.json) records20,755byte comparisons, clean/fsck,14source/51artifact pins,0tests. Runtime/tests remain b5255b1f. Final exact-tip writer closure follows carrier publication; no further runtime execution/stage.
