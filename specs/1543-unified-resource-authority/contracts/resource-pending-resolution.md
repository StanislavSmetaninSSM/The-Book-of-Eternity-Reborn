# Bounded Resource Resolution Contract

## 1. Eligibility

Deterministic damage, restore, spend, gain, costs, recovery, item use, and registered system outcomes never create pending work.

A story-facing effect may pend only after exact source, target, definition, resource coordinate, bounds, effect trigger, graph prerequisites, and all deterministic siblings are valid. No resource/effect after-image is published while pending.

## 2. Client-owned pending record

The canonical technical request binds:

- schema version 2 plus session/request/turn/event identity;
- resolved effect/source/target/trigger evidence;
- one exact activation identity and trigger-event identity, declared priority,
  causal activation ordinal, consuming flag, and uses-before evidence;
- one nullable stable producer operation/event key for lifecycle versus
  graph-origin activation;
- component identity, optional predecessor component, v6 candidate fingerprint,
  transcript-prefix fingerprint, and non-negative pending-wave ordinal;
- one closed authority binding for the effect, source, target, and resource owner;
- exact protected resource coordinate and operation;
- allowed result kinds and numeric bounds;
- required companions and full-turn resubmission;
- created chronology and state;
- `fullTurnFingerprint`, which seals the stable pre-publication accepted
  mechanics content, internal inputs, and canonical definition/owner/resource/
  effect authority fingerprints while excluding receipt payloads, pending
  self-state, and the transport snapshot token;
- `semanticTurnFingerprint`, which seals the exact session/request/realm/turn,
  accepted events, resource commands, and effect commands with receipt payloads
  removed while likewise excluding the transport snapshot token, so terminal
  replay can recognize the original command envelope after canonical
  after-images and the pending transport generation have legitimately changed.

Both fingerprints are deliberately independent of transport generation. The
current resubmission snapshot and byte/existence manifest are still checked
separately by the accepted-plan binding before either fingerprint is trusted.

The GM-facing projection replaces protected identities with safe in-world labels and instructions. It never exposes paths, owner IDs, transition history, seals, fingerprints, validation codes, rollback evidence, or agent instructions on player surfaces.

Each authority binding is exactly `{ bindingKind, authorityId }`. `bindingKind` is
one of:

- `permanent` — the accepted pre-turn permanent identity is the exact selector;
- `same_turn_ref` — the exact accepted temporary reference is consumed by its
  owning materializer and re-resolved on the full-turn resubmission;
- `accepted_application` — a newly applied effect is bound to its exact accepted
  application event rather than to its still-unpublished random `effectId`.

`effectAuthority` is deliberately narrower than the shared binding catalog. It
MUST be either `accepted_application` with one exact application event or
`permanent` with `authorityId` exactly equal to the request `effectId`.
`same_turn_ref` remains valid for source, target, and resource bindings, but is
invalid for an effect because the accepted application event is the only stable
same-turn effect authority.

Resolved permanent IDs remain sealed evidence in the request, but an
unpublished random ID is never a cross-phase selector. Receipt validation
rebuilds the same accepted turn, resolves every temporary reference or accepted
application again, and binds the restored mutation to that freshly validated
after-image. The client neither predicts nor deterministically derives random
materialization IDs.

## 3. Initial result catalog

`narrated_no_state_change`:

```json
{
  "requestId": "resource_resolution_<opaque>",
  "resultKind": "narrated_no_state_change",
  "reason": "..."
}
```

`resource_delta`:

```json
{
  "requestId": "resource_resolution_<opaque>",
  "resultKind": "resource_delta",
  "amount": 3,
  "reason": "..."
}
```

No other fields are accepted. The request fixes coordinate and operation; the receipt cannot retarget or reverse it.

## 4. Validation

The receipt must match one pending request in the current session and exact
full-turn resubmission, use one allowed result, stay within exact numeric
bounds/quantum, provide the required reason, and include every required
companion. Existing permanent bindings must remain byte-exact; same-turn
bindings must resolve through the same exact accepted refs/application events
to one equivalent source, target, resource coordinate, trigger, and policy.
Extra, missing, stale, partial, conflicting replay, cross-target,
wrong-operation, wrong-realm, ambiguous/unresolved authority, or out-of-bound
data fails closed.

The two turn fingerprints are not interchangeable. Active pending resolution
requires the exact `fullTurnFingerprint`. Terminal replay requires the stored
`semanticTurnFingerprint`; changing, deleting, or adding an original
resource/effect command is a conflicting replay even when session, request,
turn, and receipt identity still match.

The v6 candidate fingerprint is also non-interchangeable with its earlier
domains. It binds replay-stable typed effect authority,
activation/use/producer identity, pending and reaction outputs including typed
replacement targets, and one immutable causal-origin fingerprint covering every
base mutation's full amount/dependency/event/receipt/recovery/constraint
semantics plus every referenced source export's identity, authority
fingerprint, state, same-turn flag, and bound owner. An unbound replay must
match that origin exactly. A bound replay may add only terminal projections
regenerated from typed resolved bindings, and final graph validation must prove
those projections against the actual mutation/source catalogs. A cached
candidate fingerprint without its exact causal-material fingerprint, or any
origin/source/projection drift, fails closed.

## 5. Resolution and causal replay

A valid `resource_delta` becomes one normal `ResourceMutation` with client-owned operation/transition identity and the pending request's authority fingerprint. A no-state-change result records terminal receipt evidence without a mutation.

The receipt is an output replacement for its exact accepted activation, not an
independent root mutation and not an already consumed trigger. On every coherent
resubmission, the client rebuilds the graph from the same immutable effect
snapshot, reaches the exact producer event, replays the saved accepted
activation at its causal ordinal and uses-before value, and only then releases
the receipt mutation or narrated no-change result. Priority, ordinal, budget,
producer key, candidate fingerprint, or transcript-prefix drift fails closed.

Successful publication consumes the pending request and receipt exactly once.
Semantic replay of the complete original resource/effect command set returns
terminal evidence, consumes the transient commands, and performs no second
effect or resource mutation. Conflicting replay is protected.

Terminal evidence retains the complete closed request authority and exact
result as a typed resolved binding. This is required to replay earlier waves
without asking the GM to resubmit their receipts and without deleting the
causal position at which their outputs occur.

If a resolved output emits an event that reaches another accepted bounded
candidate, the client creates the next wave, carries all earlier resolved
bindings forward, and returns to pending with zero mechanics/lifetime
publication. Wave ordering is `waveOrdinal`, then `activationOrdinal`, then
ordinal `componentId`; opaque request IDs are never causal ordering authority.
Finalization occurs only after a full replay produces no new pending outputs.
For each returned safe packet the GM resubmits the same complete semantic turn
with receipts for that packet's current unresolved requests. Earlier-wave
receipts need not be repeated: their typed terminal bindings remain
client-owned replay evidence. An exact non-empty subset of already terminal
receipts is accepted only as terminal no-op evidence; an empty, altered, or
foreign set does not authorize replay.

Schema version 1 pending state and requests produced by an earlier incomplete
candidate-fingerprint domain are explicitly incompatible technical state. They
are rejected rather than migrated, inferred, or silently repaired.

## 6. Failure and cancellation

Malformed or stale receipts do not delete the pending request. Protected failures cannot be repaired by asking the GM to rewrite the request. Explicit cancellation, if later added, requires a separate tracked result kind and contract; #1543 does not infer cancellation from omission.
