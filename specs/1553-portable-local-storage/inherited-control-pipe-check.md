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
