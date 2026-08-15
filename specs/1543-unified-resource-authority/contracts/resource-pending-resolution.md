# Bounded Resource Resolution Contract

## 1. Eligibility

Deterministic damage, restore, spend, gain, costs, recovery, item use, and registered system outcomes never create pending work.

A story-facing effect may pend only after exact source, target, definition, resource coordinate, bounds, effect trigger, graph prerequisites, and all deterministic siblings are valid. No resource/effect after-image is published while pending.

## 2. Client-owned pending record

The canonical technical request binds:

- schema/session/request/turn/event identity;
- effect/source/target/trigger identity;
- exact protected resource coordinate and operation;
- allowed result kinds and numeric bounds;
- required companions and full-turn resubmission;
- created chronology, state, and replay fingerprint.

The GM-facing projection replaces protected identities with safe in-world labels and instructions. It never exposes paths, owner IDs, transition history, seals, fingerprints, validation codes, rollback evidence, or agent instructions on player surfaces.

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

The receipt must match one pending request in the current session and full-turn resubmission, use one allowed result, stay within exact numeric bounds/quantum, provide the required reason, and include every required companion. Extra, missing, stale, partial, replayed, cross-target, wrong-operation, wrong-realm, or out-of-bound data fails closed.

## 5. Resolution

A valid `resource_delta` becomes one normal `ResourceMutation` with client-owned operation/transition identity and the pending request's authority fingerprint. A no-state-change result records terminal receipt evidence without a mutation.

Successful publication consumes the pending request and receipt exactly once. Semantic replay returns terminal evidence without another mutation. Conflicting replay is protected.

## 6. Failure and cancellation

Malformed or stale receipts do not delete the pending request. Protected failures cannot be repaired by asking the GM to rewrite the request. Explicit cancellation, if later added, requires a separate tracked result kind and contract; #1543 does not infer cancellation from omission.
