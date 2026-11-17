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
