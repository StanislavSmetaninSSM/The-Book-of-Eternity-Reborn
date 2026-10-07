# T050-CLIPBOARD-LINUX — bounded handoff

2026-10-07 UTC · [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)
· branch `codex/1553-load-filesystem` · accepted base `113edbb00eeeb9172ee3a02e00585f7bde442243`.
Runtime/test checkpoint: `b5255b1fb96e405eb1ed195c6437b16ad5788246`.
[Plan](clipboard-linux-plan.md) · [Qualification/14 source pins](recovery/clipboard-linux-qualification.json)
· [durable evidence](recovery/clipboard-linux-evidence).

## Result and limits

Existing `IClipboardService.TryReadText` / `ClipboardReadResult(Success,Text,Error)`
remain; additive Outcome distinguishes Text/Empty/Unavailable/Error/Timeout/
TooLarge/CleanupUncertain. Select one existing tool before launch: own Wayland+
wl-paste, otherwise own DISPLAY+xclip/xsel. Fixed read-only arguments, no shell,
clipboard write/watch/primary selection or retry/fallback after execution.
Windows Get-Clipboard UTF8/STA adapter remains; no native Windows execution.

Concurrent byte streams:2s shared deadline, stdout≤1MiB, stderr≤64KiB; strict
UTF8, actual exit and both EOF before Text. Stderr contents are not exposed.
Cleanup≤1s signals only validated Linux pidfd (Windows original Process/handle);
unconfirmed original exit/I/O retains debt and refuses another reader until settled.
Linux preflight refuses unsupported pidfd capability before reader launch.
The identity proof depends on reviewed .NET8.0.31 reaping/cache/child-table sources:
open fd → explicit new own-self HasExited barrier → original HasExited check.
No PID-reuse race was forced; future runtime changes require rechecking that proof.
The adapter owns only its foreground reader, no compositor/server/descendant tree.

Real `GetPlayerInput` and `SpectreExplorerConsole.Ask` consume shared TextComposer.
One gesture reads once even when returned text equals a shortcut. Clipboard becomes
a local bounded escaped preview/draft; subsequent manual Enter (or existing two
blank multiline terminator) accepts it. Failed reads preserve default/manual
paragraphs and display escaped messages. Explicit null/EOF throws typed cancellation
carrying draft, no value; manual replacement remains manual. Late turn reread is
removed. No composer/service calls bridge/daemon/GM; bridge paste-submit policy,
game rules/economy/text/model/profile command and accepted Load UX are unchanged.

## Execution evidence

All execution used `scripts/test-csharp.ps1`, activated own installed tools,
isolated roots and synthetic clipboard bytes. Public real service runs under
child-owned PATH/display with own readers; two counted synthetic service cases
isolate the original consumer bugs. The negative capture case alone uses an
internal instance capture seam, real reader/process and actual natural exit.
No global environment override, real user clipboard, game loop or provider.
Reader hard lifetime8s; host independent12s; parent deadline15s, no PID signal.

| Run / pinned source | Executed result | Meaning |
|---|---|---|
| Initial PlanOnly /16871c3e | 0 | fresh compilation/discovery, not RED |
| Consumer RED /16871c3e | 2FAIL | duplicate read and hidden failure; adapter not executed |
| Adapter RED /16871c3e | 1FAIL | actual public Linux refusal before reader launch |
| First GREEN /30027e45 | 3PASS | behavior fixed; later source review found Linux Kill race |
| Expanded /00f46cee | 19PASS/1FAIL | noncausal culture-sensitive ESC assertion; corrected to ordinal; adapter16 not executed |
| Identity GREEN /b5255b1f | 38PASS |18adapter+20consumer, all complete/no skip |
| Affected /b5255b1f | 12PASS | normalization,7composer, direct paste,3source guards; same fresh unit build |
| ValidateCatalog /b5255b1f | 0 | valid381categories/11072methods; no stale/unmapped selectors |

Final distinct coverage: **50PASS =38new+12affected**. Historical executions total
76 (72PASS/4FAIL), not76 unique tests;3causal baseline failures and1fixture assertion
failure,0preparation failures. Final runner cleanup complete, no remaining recorded
own reader identities or fixture roots.38final fixture receipts record34reader
processes (negative debt: two reads across three explicit attempts, no second launch
before original exit). Unicode/multiline, literal shortcuts, manual replacement,
EOF after success/failure, empty/error/timeout, no fallback, UTF8 byte limits and
retained cleanup debt are covered at actual consumers/service.

Freshly verified tools: SDK10.0.401, runtime8.0.31 (10.0.12 also installed),
PowerShell7.5.4; no native compiler used. No real clipboard tools or display variables
were present in the read-only own environment inventory. Sources/commands/TRX/logs/
fixture arguments/PID starttime/cleanup and hashes are durable in the qualification.

## Independent review and recovery closure

Actual separate GPT-6.1 Sol/xhigh design PASS at02639eb/29769082 and corrected
identity plan75fed432; source PASS atb5255b1f. Root remains sole writer. Initial
source BLOCK (PID signal race, noncausal fallback and missing byte/debt tests) was
resolved and re-reviewed; no remaining confirmed source defect or essential gap.
Independent evidence/metadata review and fresh GitHub-only restore are pending
at this evidence carrier. Every checkpoint was ordinary pushed with exact remote
SHA and changed-file byte readback. No merge/force/branch deletion/issue closure.

Stop before another block. Positive Wayland/X11 desktop and native Windows remain
unqualified. Unsupported kernel/libc pidfd and other runtime versions are not newly
qualified. Full T050 audio/platform helpers, required unimplemented/unqualified
systemd-user primary, production workers/Q1Q2/live browser/GM/real saves/cold/game
acceptance and historical unpassed Windows-only IDs remain in their existing tasks;
this slice closes none of them and requires no new product decision/access.
