# C5: original admission through the GM wait

Owner/parent approved this bounded continuation after independent Astra/xhigh
architecture review. Finish the reviewed bounded T070 checkpoint first, then
implement the admission prerequisite and browser wait split as separate coherent
changes. No lifetime implementation yet. Source #1553/T069; actual FAIL at
`cb4934790feb9c32f13aed42597ab8d7aa8e0024`, evidence
[c5-crashcut-original-owner-fail](recovery/linux-game-chains-20261010/c5-crashcut-original-owner-fail/manifest.json).
The agreed cold continuation criterion stays open. Queued crash acceptance and
six factory cold classifications do not qualify this staged real-process cut.

The original pin currently lives through the wait because
[ProcessPlayerTurn](../../BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs)
wraps staging, waiting and finalization in one participating operation. That
binding fences every nested filesystem operation to the same original run and
generation, including closure. SIGKILL loses its immutable close frame;
[RemotePin.Lost](../../BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.PipePins.cs)
keeps the pin counted and closes admission as unresolved. This is a deliberate
guard, not a signal that can be reset with ready/PID/timeout. Finishing the same
relay request later cannot settle the lost client pin. The browser owner is this
ProcessPlayerTurn wrapper, not bootstrap/transition WaitForGmResponse.

The existing architecture supplies bounded participating operations, expected
generation bindings, exact immutable staged request/manifest/authority and
signed pre-turn bytes. A bounded **browser ordinary-turn-only** split can use
those surfaces without a new owner or journal:

0. Capture the full original authenticated `GmSessionRunIdentity` in the original
   browser snapshot manifest during staging: root/run/generation/epoch/backend/
   host/boot. A stored field is a refusal witness, never a grant. Before opening
   a participating connection, compare the retained original expectation with
   nonmutating trusted run evidence; preserve the authenticated original
   handshake. Recheck under the physical lock before storage recovery can write.
   Cover partial publication and missing/downgraded slot with retained browser
   staging artifacts; absent/stopped/replaced owners cannot become quiescent
   fallback. Classification after acquiring a canonical lease is too late.
   Preserve borrowed original owners, typed metadata-only stop and diagnostic
   operations, and the distinct accepted/settled cleanup-only proof contract.
   Do not activate a replacement run and then dispose without its real immutable
   close. This shared admission prerequisite requires a narrow independent review
   and affected admission controls before the lifetime change.
1. Complete original staging participation, including successful immutable close,
   before publishing the UI wait boundary. Keep original action/generation and
   signed request tuple; a failed close retains the slot and blocks continuation.
2. Wait outside participation. Each inspection uses a short participating
   **expected-generation and original-run** operation and physical lease, verifies the exact
   original slot/request/manifest/authority, and closes fully before idle delay.
   Bind health/timeout diagnostics and cancellation decisions to the same tuple;
   they cannot delete evidence or impersonate an actual GM terminal signal.
3. Before processing, enter a fresh expected-generation and original-run operation,
   reread the complete signed binding and captured original one-signal provenance,
   and atomically claim terminalProcessing. Keep the existing full finalization
   owner through validation, publication, history/receipt and closure.
4. Move staged-cold idle delay out of its current participating callback too.
   Keep bootstrap, transitions, ordinary console and original Bridge admission
   unchanged. No Unresolved eviction, automatic replacement or re-dispatch.

The guarantee changes from one continuous client participation to authenticated
short inspections plus one finalization operation. Replacement may happen during
idle; resumption must refuse a different generation/run or substituted artifacts.
Death **during** a bounded operation still loses its original close and remains
unresolved. This reduces exposure; it cannot promise arbitrary-crash recovery.
An actually idle staged crash with the original GM owner alive must continue once;
an active-operation crash must remain a separate honest negative.

The real-Program idle crash fixture needs a deterministic diagnostic boundary:
only after the complete top-level staging await returns successfully, before any
poll starts, an explicit DEBUG-only externally owned nonce/PID/request-bound test
gate may acknowledge the actual original observed close and hold idle. The
`observeOriginalClose` callback itself still precedes await-using disposal, so it
may copy values but must not emit the idle ACK or block. Require non-null actual
TerminalClose, WasRemote/CloseObserved, successful outcome, ClosingFailed=false
and the exact staged tuple/full original identity. Compile the seam out of
Release, bound the gate, disable it in the cold child, and never treat ACK as
production recovery authority. Withhold the authored GM response until crash.
The active-poll death negative separately witnesses its actual acquired original
admission/held lease/reread tuple; the staging close cannot prove an active poll.

Minimal acceptance: causal existing staged FAIL → actual supported idle staged
crash/cold GREEN through React/Program/Bridge/daemon/relay, exact same original
request/action/generation and one accepted history row; normal queued/accepted
cuts; independently owned original stop/EOF/ECHILD. Negative controls: death or
lost close during staging/poll/finalization; generation/owner replacement in idle;
changed request/manifest/authority/snapshot, missing/downgraded proof and partial
publication, absent/stopped owner, borrowed admission and post-activation refusal
with original close discipline; dual, diagnostic, stale or late
terminal signals; cancellation racing completion; late publication/close failure.
Preserve evidence, no later canonical write/replay, and explicit cleanup outcome
in each refusal. Select existing affected console/journal C1 and browser fencing
categories; no aggregate suite or live model.

This is infrastructure required to complete the already agreed cold-flow, not a
new game mechanic or browser topology. The owner/parent approval and independent
architecture review are retained; no repeated permission is needed for this
scope. The only product decision
arises if a broader promise of automatic recovery from **any** active-operation
crash is desired: that needs a different durable owner/settlement design and is
outside this proposal. No such promise or question is assumed here.
