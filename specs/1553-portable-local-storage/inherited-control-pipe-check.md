# Inherited byte-control pipe check (#1589)

WIP, uncompiled and unrun. This follows the first five managed owner observations
at ac6d714; it is not bridge or live GM acceptance.

Extract the existing inherited BaseStream operations into one tiny typed owner
of the same retained PipeStream. No added serialization, buffer, flush or retry:
NativeLineageOwner retains the phase, authority, deadline and gate. Abort closes
the pipe without flushing a StreamWriter. Finite/socket routes are unchanged.

One real anonymous-pipe test fills the actual kernel capacity with filler, proves
that a new A is pending, cancels and awaits that task before draining, then aborts
and reads exact filler plus EOF. It owns/closes the duplicate writer handles and
checks abort idempotence and rejection after abort. This tests raw cancellation
and absence of abort append; it is not causal RED for the historical StreamWriter
char-buffer/flush interleaving. No previous RED for this new coverage is claimed.

The existing narrow category owns this added case; its responsibility is updated.
All current managed cases must run against the extracted control, not old output.
Each kernel pipe and original owner is fresh. No cache or speedup claim is useful
for this transient authority; stable toolchain/package data continues to be reused.
Independent source/outcome review and actual runtime evidence remain pending.

Independent review found a partial-initialization blocker in saved43533cc:
control creation preceded original observation tasks, while abort dereferenced
a possibly absent control. A typed failure-before-control-assignment hook and
actual guarded-helper test are now prepared to expose that exact error without
reflection. The test requires the original exception/owner, no admitted root or
payload, native cleanup and independent ECHILD. The defect is deliberately not
fixed in this test-first checkpoint; expected causal RED is a masked failure.
All seven selected cases are still uncompiled/unrun after these additions.

The typed hook now captures the exact original owner instance; the retention
oracle requires ReferenceEquals with the exception owner. Its diagnostic
reference may attempt cleanup after a RED but never satisfies the main oracle.
This corrects the earlier type-only assertion before runtime execution.

## Actual partial-initialization RED and narrow correction

Saved65ea3487 built with0errors/96warnings in96.14s. Official PlanOnly selected
four methods/seven cases, no execution,9.057s. Actual official run completed
6PASS/1FAIL, zero skips/duplicates,41.599s and complete runner cleanup. The new
full-pipe cancellation/abort control PASSED, as did the five prior managed cases.
The exact partial-init failure was NullReferenceException in null-control abort,
masking the injected sentinel; diagnostic Stop also hit missing observation tasks.
The independent guardian still reported ECHILD/zero emergency signals, but the
driver failed and this is NOT successful owner retention or cleanup acceptance.
TRX:20261117-110701-092-5-22cab0477afe4a728ed17b46e9c718d5-categories.

The narrow production correction now starts original process exit/status/error
observations before the fallible control setup, and uses original raw-pipe close
when no control object was assigned. Tests are unchanged. This correction is
UNCOMPILED/UNRUN pending fresh build and the same seven cases. No raw diagnostics
are committed; no broad suite, permission change or additional execution route.

Source re-review found no blocker to GREEN. Its nonblocking observation is also
addressed: when inherited abort already closed the original control pipe, stop
does not try sending S through an absent/closed control; it observes the original
process/status retirement. Tests remain unchanged, this final correction unrun.

## Same-case GREEN observed, 2026-11-17

Saved bffce3062ed89967f9873d5d37511ff31f69108a built successfully:0errors,
19warnings,56.39seconds (unchanged dependent test sources reused by the normal
incremental build, no copied cross-checkout outputs). The same seven official
cases completed7/7PASS, no skips/duplicates,45.776seconds runner wall, complete
runner-owned cleanup. TRX identity:
20261117-111533-641-5-de54343dc745441cba81938245368c09-categories.
All six guarded scenarios reported ECHILD, driver0, zero emergency signals,
zero failures and no deadline. Partial-init now retained the exact original
owner and sentinel, no root was admitted, and native cleanup completed while
uncertainty stayed explicit. The seventh case was the independent raw-pipe
cancellation/abort check. Independent outcome review is pending; this does not
qualify descriptor-transfer refusal, abort-timeout retry, bridge or live GM.

Independent outcome review accepted this bounded seven-case block after checking
source blobs against bffce306, build/run logs, TRX and all six guardian/scenario
results. It did not rerun runtime. No historical buffered-writer RED, complete
protocol-negative matrix, bridge admission or live-game acceptance is claimed.

## Abort timeout control WIP

Adds one existing-production behavior check: hold the original control lease,
cancel ReleaseTerminalAsync before A, observe its two-second abort gate timeout,
reject disposal of the uncertain original owner, then release only the fixture
lease and observe cleanup through that same owner. This is a cleanup observation
retry, not a second A or a claim that abort closure itself retried. No production
change. Eight selected cases are UNCOMPILED/UNRUN. Fresh original native owner
is required; no authority reuse/cache. Existing category owns the wildcard test.

Independent design inspection identified the stronger oracle: native retirement
alone does not close the original control handle. The same test now observes the
original SafePipeHandle through a read-only typed snapshot (no duplicate/raw PID,
no new cleanup API). It requires abort requested but neither closure claim nor
actual closure while the gate is held, and both confirmed and actual original
handle closure after same-owner Stop. Current Stop does not retry this closure;
expected causal RED is the final handle assertion. Only observation seams added,
production cleanup behavior unchanged; fresh build/execution remain UNRUN.

## Abort control causal RED and correction WIP

Saved b42afa6f built with 0 errors/96 warnings in 102.19s. Official PlanOnly
selected eight cases in 9.036s; the actual unchanged selection completed
7 PASS / 1 FAIL in 34.871s, no duplicates/skips, runner cleanup complete.
The new case reached the exact final assertion: same-owner stop did not close
the original aborted control handle. Native cleanup was already complete and
guardian ECHILD/emergency0, demonstrating why those facts alone were insufficient.
TRX run: 20261117-112920-835-100-ade4a64e373749b3b316b3b28713f373-categories.

Correction retries only bounded closure for an already-aborted inherited owner
inside existing StopAndObserve, outside its one-shot S branch. It never retries
L/A, never sends S behind unresolved abort or after closed control, and preserves
sticky uncertainty. Tests unchanged. This correction is UNCOMPILED/UNRUN and
awaits independent source review. No caching of fresh handles/authority; existing
immutable native preparation remains unchanged through this RED/GREEN pair.
