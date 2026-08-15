# Resource Transition and History Contract

## 1. Pure reducer boundary

The reducer consumes one already-authorized `ResourceMutation` and an immutable working ledger/history. It does not read files, resolve GM selectors, invoke effects, write state, log, render UI, or create repair packets.

It returns either:

- one updated working ledger, one exact applied event, one immutable transition entry (or one prior replay entry), and zero issues; or
- issues with no after-state.

## 2. Arithmetic

For each operation:

```text
damage: candidate = current - amount
spend:  candidate = current - amount
restore: candidate = current + amount
gain:    candidate = current + amount
```

The definition must allow the operation. `amount`, current, maximum, candidate, and final value must be exact and quantum-aligned. Overflow or inexact representation fails.

If candidate crosses minimum/maximum, the mutation's client-derived policy binding selects reject or the exact registered clamp. The GM cannot select clamp behavior.

Mutations are never combined before reduction.

## 3. Stable order

Closed phases execute in this order:

1. `direct_cost`
2. `direct_outcome`
3. `registered_system_outcome`
4. `effect_trigger`

Within a phase: ascending registered priority, then exact `originId`, then client-owned `operationId`. Dependencies must already be satisfied. The order is recorded in history and must be identical on retry.

## 4. Applied events

Every non-replay applied result emits its operation event plus optional boundary events:

- damage -> `resource_damaged`
- restore -> `resource_restored`
- spend -> `resource_spent`
- gain -> `resource_gained`
- transition from above minimum to minimum -> `resource_depleted`
- transition from below maximum to maximum -> `resource_filled`

Boundary events emit only on crossing/reaching from a different value. Events carry exact operation identity, coordinate, before, after, applied amount, source fingerprint, and turn.

## 5. Replay

Replay key:

```text
eventRef + originKind + originId + coordinate + operation
```

If a matching history entry has identical requested amount, policy/source binding, phase, and priority, return it as `semantic_replay` without changing state/history or emitting another trigger event. Any semantic mismatch is `resource_transition_conflicting_replay` and blocks the complete plan.

## 6. Capacity transition

Capacity transitions are reduced separately before their dependent ordinary mutations.

- initialize: resolve maximum and initial current from sealed definition/owner authority;
- preserve: reject if current exceeds new maximum;
- clamp: set current to the new maximum only when source policy authorizes it;
- scale: compute `current * newMaximum / oldMaximum` exactly and reject zero denominator/inexact quantum;
- suspend/resume: change lifecycle without numeric mutation;
- retire: append terminal evidence and remove live state.

Every accepted capacity/lifecycle transition has client-owned transition evidence and exact replay behavior.

## 7. History immutability

History is append-only and canonically ordered. Direct edit, deletion, truncation, reorder with semantic change, duplicate/confusable ID, wrong coordinate, before/after discontinuity, stale chronology, or inconsistent replay evidence is protected.

The state entry chronology must equal the latest accepted transition for that coordinate. Retirement history remains after live state removal.

## 8. Trigger graph

Applied events can satisfy registered effect triggers. The cross-domain planner builds a finite graph before executing effect-trigger nodes. Duplicate node identity, missing dependency, cycle, more than 1,024 nodes, or dependency depth above 32 fails with zero after-images.

Each downstream mutation re-enters the same reducer and may emit further registered events. No trigger executes twice for the same accepted event/trigger/operation identity.
