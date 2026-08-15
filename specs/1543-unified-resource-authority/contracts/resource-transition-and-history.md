# Resource Transition and History Contract

## 1. Pure reducer boundary

The reducer consumes one already-authorized `ResourceMutation`, an immutable working ledger view, and the plan-local history working set owned by `AcceptedMechanicsPlanner`. It does not read files, resolve GM selectors, invoke effects, write state, log, render UI, or create repair packets.

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

The planner first constructs the complete finite dependency graph, executes the closed phase order, and assigns each stored transition one non-negative client-owned `executionSequence` unique within its turn. Canonical history orders by `turn`, then `executionSequence`; phase, priority, origin, operation, event, and transition identities are deterministic consistency tie-breakers only. Within a ready set: ascending registered priority, then exact `originId`, then client-owned `operationId`. Dependencies must already be satisfied. The sequence is protected replay semantics and must be identical on retry.

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

If a matching history entry has identical requested amount, policy/source binding, phase, priority, and receipt semantics, return it as `semantic_replay` without changing state/history or emitting another trigger event. Newly allocated transition/operation IDs do not participate in replay matching. Any semantic mismatch is `resource_transition_conflicting_replay` and blocks the complete plan.

`semantic_replay` is never stored as a second history row. Canonical stored outcomes are only `applied`, `clamped_minimum`, and `clamped_maximum`.

## 6. Capacity transition

Capacity transitions are reduced separately before their dependent ordinary mutations.

- initialize: resolve maximum and initial current from sealed definition/owner authority; the history parser itself proves `minimum`, `maximum`, and `fixed` policies, while the composed reducer must additionally prove the typed input and fingerprint of `registered_formula`;
- preserve: reject if current exceeds new maximum;
- clamp: set current to the new maximum only when source policy authorizes it;
- scale: compute `current * newMaximum / oldMaximum` exactly and reject zero denominator/inexact quantum;
- suspend/resume: change lifecycle without numeric mutation;
- retire: append terminal evidence and remove live state.

Every accepted capacity/lifecycle transition has client-owned transition evidence and exact replay behavior.

Every history row stores complete nullable `beforeState` and `afterState` snapshots containing current, maximum, capacity binding, and lifecycle plus an explicit nullable `capacityDisposition`. `initialize` is `null -> active` with `initialize_from_definition`; its current value must agree with the locally decidable sealed initialization policy, and a registered formula remains subject to the later composed typed-authority check. `retire` is `live -> null` with exact `owner_lifecycle` evidence and a null disposition. Reconfigure must actually change maximum or capacity binding and must declare exactly one of `preserve`, `clamp_to_new_maximum`, or `scale_ratio_exact`. Preserve keeps current exact; clamp yields the exact lesser of prior current and new maximum; ratio scaling must satisfy exact cross multiplication and target quantum with no rounding. Ordinary and lifecycle operations require a null disposition. This prevents a maximum, formula input fingerprint, lifecycle change, or arbitrary current rewrite from hiding behind a nominal reconfigure row.

Arithmetic proof uses exact decimal coefficient/scale integers. Addition and subtraction first compute the mathematical result and then require that it fits the supported 96-bit, scale-28 `decimal` representation; checked `decimal` operations alone are insufficient because they may reduce scale without throwing. Every ordinary transition proves both the full requested candidate and the separately applied after-state. A clamp is legal only when the exact requested candidate is representable and crosses the relevant bound, while the exact applied delta reaches that bound; `appliedAmount: 0` cannot hide an unrepresentable or overflowing request. Ratio equality uses `BigInteger` cross-products and quantum alignment uses exact coefficient subtraction/modulo, so a difference below the representable product scale cannot be rounded away.

The canonical `ResourceHistoryState` is immutable. One accepted mechanics plan creates exactly one isolated `ResourceHistoryWorkingSet` from that validated baseline. The working set indexes exact/confusable transition, operation, receipt, coordinate, and replay identities plus the latest transition per coordinate once; each accepted mutation performs only incremental replay/continuity checks and appends to that isolated set. After all phases and graph nodes succeed, `Freeze()` performs the complete invariant validation, canonical sort, immutable index construction, and history fingerprint exactly once. Any issue discards the working set and produces no after-image.

`ResourceHistoryState.Append` is a single-entry validation/construction helper, not the planner hot path. Production planning must not rebuild, re-sort, or re-fingerprint the full untruncated history after each mutation.

## 7. History immutability

History is append-only and canonically ordered. Direct edit, deletion, truncation, reorder with semantic change, duplicate/confusable ID, wrong coordinate, before/after discontinuity, stale chronology, or inconsistent replay evidence is protected.

The first transition for a coordinate is `initialize`; every next `beforeState` equals the prior `afterState`; no transition follows `retire`. The state entry snapshot and chronology must equal the first/latest accepted transitions for that coordinate. Retirement history remains after live state removal, and a nonterminal history chain cannot exist without its live state.

## 8. Trigger graph

Applied events can satisfy registered effect triggers. The cross-domain planner builds a finite graph before executing effect-trigger nodes. Duplicate node identity, missing dependency, cycle, more than 1,024 nodes, or dependency depth above 32 fails with zero after-images.

Each downstream mutation re-enters the same reducer and may emit further registered events. No trigger executes twice for the same accepted event/trigger/operation identity.
