# Native Windows HTTP diagnosis — T054-WINDOWS-HTTP

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Owner approved separate diagnosis and a minimal causally established fix on 2026-10-08.
Branch `codex/1553-windows-http-20261008` was published at base
`5ff403ed998f7e04dadf6179fba614252bcfd2cf` before changes.

Relay runtime remains frozen at0067874e; its independently reviewed Windows
checkpoint5ff403ed restored29 changed files and14 evidence payloads from GitHub.
Linux regression is parent-owned and must retain its actual source revision.

## Accepted scope and plan

- [ ] Compare an empty root, an ordinary newly initialized game and a separate
  copy of the original owned synthetic Save/Load/restart root. No user data.
- [ ] Capture actual server exceptions/stacks through the existing ILoggerFactory
  on the real LocalWebUiHost, with actual loopback Kestrel and original DI.
  Diagnostic provider only; no replacement services or response rewriting.
- [ ] Establish the smallest causal failing regression before runtime changes.
  A malformed fixture alone does not justify a product fix or an empty200.
- [ ] Make only a proven correction, select affected catalog categories, preserve
  parallel/repeated HTTP reads, Save/Load/restart and normal owned shutdown.
- [ ] Publish bounded WIP before long checks/review, obtain independent Astra
  XHigh review, verify exact remote SHA and fresh GitHub-only source recovery.

Original evidence: three500 responses (/api/session,/api/game-screen,
/api/audio/settings) under parallel browser/HTTP reads after synthetic Save/Load
and restart. Serialized audio returned200. No stack or clean-game comparison was
available, so the cause is unproven. Preserve that distinction.

No model requests, installs, WSL, new network permissions, security/auth changes,
CI enabling, merge, force push, branch deletion or issue closure. Bind loopback
only; retain ownership and await each diagnostic host's StopAsync/DisposeAsync.
No game/GM schema or prompt change is planned; reassess if a proven cause requires it.

## Checkpoint

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
