# Native Windows HTTP diagnosis — T054-WINDOWS-HTTP

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Owner approved separate diagnosis and a minimal causally established fix on 2026-10-08.
Branch `codex/1553-windows-http-20261008` was published at base
`5ff403ed998f7e04dadf6179fba614252bcfd2cf` before changes.

Relay runtime remains frozen at0067874e. Its final Windows carrier
19d79b26afe3a46c488794b6a6d010badab44909 restored29 changed files/14 payloads,
clean, from GitHub. Windows24/24 and separate parent-accepted Linux32/32 qualify
that relay slice; [Linux carrier100939f9](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/100939f9e07e66d6933af6712e87a86af2206c8e/specs/1553-portable-local-storage/relay-linux-regression-20261008.md).

## Verified Windows HTTP result

Frozen runtime/tests: `3d205d317244b977460fc5537f66fa35e8bc567b`.
Native HOME-PC Windows11/.NET8; no WSL. Targeted fresh build0errors/22warnings,
33/33 PASS (9 admission +2 lifetime +10 Load refresh +4 required generation
+8 settings), no skips/duplicates/timeout, original owned/runtime cleanup true.
All Load cases retained after category split; unchanged5minute budgets.
[Original evidence and hashes](recovery/windows-http-20261008/manifest.json).

| Real Kestrel probe | GET responses | Warnings/exceptions |
|---|---|---|
| Fresh empty root | 12x200,3xexpected game-screen404 | 30 validation warnings,0 exceptions |
| Separate synthetic copy | 15x200 | 0 |
| Ordinary NewGame, before/after SaveLoad | 20x200 | 0 |
| Fresh process on the ordinary root after Load | 15x200 | 0 |

Both actual Save and Load returned200/Committed, continuationBlocked=false;
Load included menu/session/game/audio/settings. Game/audio/settings payload
hashes matched all three corresponding restart reads. All four probes record
StopAsync/DisposeAsync true, Fatalnull, tool wrapper exit0. The probe, integration
and ordinary CLI product DLLs were byte-equal before the later catalog build;
see the receipt for the hash and distinction between raw and executor evidence.

Actual browser on the ordinary CLI host: separate new Chrome profile, launcher
with HTTP Probe Soul and settings with11 controls rendered, all five state APIs
plus metadata returned200, JavaScript/transport errors0, Chrome exit0. **Overall
browser smoke remains FAIL** because `/favicon.ico` returned404. Its original
failure, images and helper are preserved, with no weakened oracle or rerun.
The CLI was stopped by Ctrl+C through its retained PTY; PowerShell wrapper exit1
means pipeline cancellation. Product exit code was not captured. Both observed
host/shell PIDs were absent afterwards: cleanup evidence, not product exit0.
The four independent real-Kestrel probes provide normal host-shutdown evidence.

Discovery-only catalog:439 categories/11204 methods/files, valid,0 tests executed,
clean metadata carrierc5e3680e. Unit project/reference build for discovery:
0errors/41warnings; integration was freshly built for the33-case run. No repeat
of passing runtime selections. Whole-branch PlanOnly at clean carrier ee1c2fa5:
10 descriptors/72 cases planned,0 executed, no timeout, owned/runtime cleanup
true. This is inventory evidence, not72 passing tests. Final recovery follows.

Independent actual Astra XHigh source, causal RED,33-case original TRX evidence,
prior-Linux coverage and all native/browser raw records reviewed: no product
blocker. Final metadata carrier still requires exact remote/readback/GitHub-only
recovery before writer delivery. Linux HTTP execution is separate/pending here;
the [exact independent recipe](windows-http-linux-reproduction.md) is published.

Limits: queued reads reached29.93seconds; no latency improvement is claimed.
No active-GM Load continuation, provider/model turn, physical audio/clipboard,
whole-game qualification, CI enablement, merge or issue closure. The favicon404
is a separate frontend issue. This host-scheduling change preserves all original
storage/owner/generation guards, responses and finite retries; no game/GM-authored
schema, rule, prompt or example changed or requires synchronization.

## Accepted scope and plan

- [x] Compare an empty root, an ordinary newly initialized game and a separate
  copy of the original owned synthetic Save/Load/restart root. No user data.
- [x] Capture actual server exceptions/stacks through the existing ILoggerFactory
  on the real LocalWebUiHost, with actual loopback Kestrel and original DI.
  Diagnostic provider only; no replacement services or response rewriting.
- [x] Establish the smallest causal failing regression before runtime changes.
  A malformed fixture alone does not justify a product fix or an empty200.
- [x] Make only a proven correction, select affected catalog categories, preserve
  parallel/repeated HTTP reads, Save/Load/restart and normal owned shutdown.
- [x] Publish bounded WIP before long checks/review, obtain independent Astra
  XHigh review, verify exact remote SHA and fresh GitHub-only source recovery.

Original evidence: three500 responses (/api/session,/api/game-screen,
/api/audio/settings) under parallel browser/HTTP reads after synthetic Save/Load
and restart. Serialized audio returned200. No stack or clean-game comparison was
available, so the cause is unproven. Preserve that distinction.

No model requests, installs, WSL, new network permissions, security/auth changes,
CI enabling, merge, force push, branch deletion or issue closure. Bind loopback
only; retain ownership and await each diagnostic host's StopAsync/DisposeAsync.
No game/GM schema or prompt change is planned; reassess if a proven cause requires it.

## Historical checkpoints (current qualification above)

Diagnostic harness builds with0errors/3existing warnings. Actual probes at
source406423ae use fresh product assembly and built frontend assets from the
original6381 checkout; no product changes. Both hosts stopped/disposed normally.
[Evidence](recovery/windows-http-20261008/manifest.json).

Fresh empty root beforeSaveLoad: first parallel batch game-screen500; other four
200. Second batch returns expected404 for no game, others200. All serial reads
are successful (game-screen404 expected). Actual Kestrel exception is IOException
opening gm-main-owner.lock, from MainAdmission through ReadFileAsync and dashboard
soul read. Serial menu9380ms/game-screen10598ms; requests contend with a legitimate
long participating main-menu operation/full validation.

Separate copied synthetic SaveLoad root: first batch5/5 status200; second batch
session/game-screen/client-settings500,menu/audio200; same original-lock IOException.
Thus the symptom is not confined to damaged save data. No lock timeout or authority
checks have been changed. Independent narrow design review is in progress.

Ordinary actual console NewGame created another owned root, initial request observed,
Escape cancelled before any CLI/model, pending request absent, ordinary menu exit0
(UI/exit observed in tool transcript). Its HTTP comparison also reproduces the
same lock IOException: first batch game-screen500, second session/game-screen/
settings500; all serial reads200. Both StopAsync/DisposeAsync confirmed. No user data.

Independent actual Astra XHigh source/design recommendation: host-local async
admission for state API handlers, with real matched endpoint boundaries rather
than brittle path comparisons. All existing filesystem/main/worker/generation
guards and finite retries remain unchanged. Queue waits use RequestAborted;
acquisition releases in finally. Completion/cancel of Load must bypass: actual
load retains original admission while awaiting client application ACK. Static
root/files, media/audio file GET and pure command metadata bypass. Media generation
stays gated because its existing bound operation spans its network stage; this
does not optimize that latency. Explorer/QTE interactions return between answers.

Nine new real-host tests are prepared before runtime edits: causal hold past the
unchanged physical retry budget, cancellation, handler-error release, Load bypass
including route casing/trailing slash, static/metadata/audio file bypass and
independently held physical guard refusal. Narrow affected settings and Load
refresh categories accompany GREEN. No true live-GM continuation claim on Windows.
The measured10–14s is whole-request duration; validation's individual share was
not separately profiled. Shared StateManager corruption was not demonstrated.

RED at9dfa154d: fresh integration build0errors;9/9 completed,8PASS/1FAIL, no skips,
timeout or duplicates, owned cleanup complete. The causal test fails on198
physical guard contentions (expected0); its held request released before the
last retry actually failed, so that particular test returned200. The actual500
RED evidence is the three native probes above. Preserve this distinction rather
than recasting the unit/integration assertion as a500 observation.

Candidate implementation adds one host-owned IEndpointFilter/SemaphoreSlim for32
state endpoints; original five bypass API routes remain outside the route group.
No endpoint payload, filesystem guard, retry or relay source changed. Queue wait
uses request cancellation, and only an acquired permit is released in finally.
GREEN and independent implementation/evidence review remain pending.

First candidate7e4d66df: all9 admission cases PASS. The14-case existing Load
descriptor exceeded its own5minute budget; no TRX/completed-case denominator was
produced for it, and settings8 never ran. Whole runner wall7m34s, complete original
owned/runtime cleanup. The runner's error text names its17minute aggregate budget,
but the actual failing descriptor budget is5minutes. No hang or root cause for
this incomplete category has been established; an owned fresh test root was still
created near the cutoff. Split the four expensive required-generation full
Save/Load scenarios into their own5minute category; retain all14 cases and their
independent fixtures, no budget increase or silent omission.

Source review raised a semaphore-disposal race with a handler surviving aborted
Kestrel shutdown. Before correction, add an actual host/DI/endpoint-filter
shutdown regression and exact cancelled-invocation entry/permit assertions.
At b238e4ea both lifetime tests completed: cancellation PASS; aborted shutdown
FAIL with actual ObjectDisposedException from SemaphoreSlim.Release in the real
admission filter. No skips, duplicates or timeout; owned cleanup complete.
The correction removes IDisposable from the admission filter. Its managed-only
semaphore never creates WaitHandle and remains alive with the in-flight handler;
DI teardown cannot dispose it before the handler's finally. Final GREEN pending.

Final targeted Windows GREEN at clean source3d205d317244b977460fc5537f66fa35e8bc567b:
33/33 completed PASS, no skips/duplicates/timeout; original owned/runtime cleanup
complete. Fresh integration build0errors/22warnings; wall9m25s. Descriptor counts
9+2+10+4+8 are intact; both split Load categories finish within their unchanged
5minute budgets. Independent lifetime/source review PASS. Actual native probes,
SaveLoad/restart/browser and final evidence review remain pending.

[Prior Linux coverage audit and exact RED/GREEN reproduction](windows-http-linux-reproduction.md)
distinguish existing short overlapping Load HTTP scenarios from missing parallel
startup state reads. No Windows-only cause or Linux HTTP GREEN is claimed.
