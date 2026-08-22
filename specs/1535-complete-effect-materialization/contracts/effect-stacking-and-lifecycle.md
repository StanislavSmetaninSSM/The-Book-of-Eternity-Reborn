# Contract: Effect Stacking and Lifecycle

**Feature**: [Complete Effect Materialization](../spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## 1. Stack Coordinate

```text
(realm, target.kind, target.targetId, source.kind, source.sourceId, stackKey)
```

`stackKey`, policy, maximum, refresh behavior, replacement semantics, and merge function come from the exact source definition. The GM supplies none of the derived post-state.

## 2. Closed Policies

### 2.1 `independent`

- Each accepted application creates a new effect identity.
- Each instance has `currentStacks=1`, `maxStacks=1`.
- Source-definition `maxStacks` is the bounded number of simultaneous independent identities at the logical coordinate; it is not copied into an individual instance.
- Reapplication at that bound follows the exact source `atMaximum` policy. A component response is legal only after its registered trigger/component execution path is available; otherwise it fails closed before publication.
- Event replay never creates an additional instance.

### 2.2 `stack`

- One active identity exists per logical coordinate.
- Reapplication increments count up to exact `maxStacks`.
- At maximum, the source declares `no_change`, bounded lifetime refresh, or a registered component response.
- `component_response` belongs to the deterministic trigger/component phase and cannot be approximated by changing stacks or lifetime in the stack reducer.
- Component scaling by stacks is profile-declared; prose does not scale mechanics.

### 2.3 `refresh`

- Preserve effect identity and current stack count.
- Source declares `reset` or bounded `extend` for the lifetime mode.
- The GM cannot submit the remaining value or choose a different mode.

### 2.4 `replace`

- Terminate the old entry as `replaced` and remove it from the active carrier.
- Create one new identity and create transition atomically.
- Old terminal history prevents replay.

### 2.5 `merge`

- Preserve one active identity.
- Merge only source-declared component fields using registered deterministic rules.
- The selected reducer must be registered by every component profile in the definition; a globally known reducer is not sufficient authority for an incompatible profile.
- Reject incompatible component sets, unknown fields, overflow, non-finite values, or an absent merge rule.
- `profile_specific` execution remains part of the registered component phase; the common stack reducer fails closed until that profile-owned reducer is available.

## 3. Closed Lifetime Modes

### 3.1 Turns

- Positive `remainingTurns` only.
- Source declares whether owner-turn start or owner-turn end advances it.
- One accepted phase advances it once; retries do not decrement again.

### 3.2 Uses

- Positive `remainingUses` only.
- Only declared trigger events consume a use.
- One event consumes at most one use unless the source explicitly declares a bounded amount.

### 3.3 Exact time

- The source declares a positive `duration` and exact `timeAuthority=world_time.currentTimeInMinutes`.
- The client reads the accepted non-negative `world_time.currentTimeInMinutes` value and computes `deadline=currentTime+duration` with checked `Int64` arithmetic.
- An exact same-turn `setWorldTime.currentTimeInMinutes` overrides a retained root value. If the override omits that exact numeric authority, a new `until_time` application fails closed instead of deriving a deadline from stale time.
- The canonical active instance stores only the resulting non-negative integer `deadline`; the GM never authors that deadline.
- Display phrases may accompany but never replace the deadline.
- Expire when accepted time is equal to or later than the deadline.

### 3.4 Scene

- Binds to one exact accepted scene/conflict ID.
- Source declares whether exit suspends or expires; ordinary scene effects do not move to another scene.

### 3.5 Source-bound

- Binds to one exact source/link and one exact predicate from the closed registry `active|carried|equipped|unlocked`.
- `active` is valid for every registered source kind; `carried` and `equipped` are item-only; `unlocked` is limited to skill, spiritual art, Fate Card, and combat action.
- Predicate satisfaction is derived from the exact accepted owner/equipment state described by the source/target authority contract; prose and duplicate status inference are forbidden.
- On source loss, source declares `suspend` or `expire`.
- Rebinding to another source is forbidden.

### 3.6 Condition-bound

- Uses a registered mechanically evaluable condition and exact operands.
- Free-form text is display only.
- Condition false applies the exact declared suspend/expire transition.

### 3.7 Permanent

- Allowed only by an exact source definition.
- Has no numeric sentinel or arbitrary remaining field.
- Must retain at least one legal closure/counteraction route unless the source contract explicitly defines truly irreversible state.

### 3.8 Manual

- Allowed only with non-empty registered cure/dispel/removal authorities.
- `lifetime.authorities[]` is source-owned terminal authority and is honored by exact `remove`; it need not be duplicated into `removal.manualAuthorities[]`.
- A generic GM reason is insufficient authority.

Task 8 provides pure reducers for every lifetime mode. Production adapters in
this slice emit the accepted Mortal owner-turn phase and exact world-time
authority. The first trigger-driven adapter accepts one closed
`effectEventReports[].owner_critical_failure` with sealed Mortal d20 evidence,
then derives one exact Fate Shield trigger/use without accepting an effect or
trigger selector from the GM. Other trigger-driven uses plus scene/condition
contexts remain unavailable unless an owning accepted adapter supplies their
exact client-validated context; missing context fails closed and is never
inferred from GM prose.

## 4. Scheduler Phase Order

For every accepted Mortal turn or afterlife exchange:

1. Validate/reconcile source-bound and condition-bound continuation.
2. Apply accepted removals and dispels.
3. Resolve new applications and stack policy outcomes.
4. Select due periodic/event triggers.
5. Execute deterministic components or validate exact bounded receipts.
6. Consume uses and advance turn/time/scene lifetime exactly once.
7. Expire completed instances and clean effect-owned companions.
8. Recompute the complete accepted mechanics snapshot and player projection.

Inside a phase: ascending source-declared priority, then ordinal `effectId`, then ordinal `componentId`/`triggerId`.

## 5. Same-Transition Conflicts

| Conflict | Required result |
| --- | --- |
| Remove and apply same stack coordinate | Remove first, then source policy decides whether application creates a new identity |
| Source disappears and same source reapplies | Continuation/loss resolves before application; no implicit rebinding |
| Lifetime reaches terminal while use-trigger fires | Trigger execution and use consumption follow phase order; terminal cleanup occurs once afterward |
| Refresh and expiry in same accepted event | Application phase precedes lifetime advancement; source refresh rule is applied, then one legal advancement |
| Wound treatment and direct effect removal | Both require exact authorities; effect terminal transition occurs once and wound transition remains independently owned |
| Multiple replacements | Stable operation ordering permits one deterministic winner or rejects a source-defined conflict |

## 6. Trigger Graph

- Nodes are exact `(effect definition, component, trigger)` declarations.
- Edges are declared downstream effect/component operations.
- The planner rejects direct or indirect cycles before mutation.
- `event_reaction` has exactly one result kind: `apply_definition`,
  `trigger_component`, `bounded_receipt`, `event_outcome`, `suspend`, or
  `remove`.
- `apply_definition` resolves one same-source definition and closed parameters;
  `trigger_component` and `bounded_receipt` resolve one periodic component in
  the same definition; `event_outcome` resolves one exact transition from the
  closed client-owned outcome registry. No display name or dynamically named
  mechanic resolves.
- The dependency is exactly `before_current_event`, `after_component`, or
  `after_current_event`. `after_component` binds one predecessor selected by
  the same trigger and executes only after that exact mutation applied.
- `trigger_component` and `bounded_receipt` must use `after_component`. An
  unconditional resource component is selected directly by its trigger, so a
  reaction-routed resource mutation always has an explicit causal predecessor.
- One trigger cannot dispatch the same component both directly and through a
  reaction. Component dependencies and downstream definitions must each form
  finite acyclic graphs.
- `maxExpansion` is a source-owned per-reaction-component execution budget for
  the whole accepted transition. The client preserves its usage across the
  pre-resource and post-resource phases. A separate hard ceiling of 64 applies
  to all reaction executions in the accepted transition.
- For a reaction emitted by a common-resource event,
  `before_current_event` follows the resource mutation that emitted the event
  and precedes only the remaining continuation of that derived event.
- A downstream application on the reacting effect's stack coordinate is legal
  only when the downstream definition explicitly uses `replace`.
- Unknown result kinds or dynamically named effect definitions are forbidden.

## 7. Bounded Resolution

`resolutionMode=bounded_receipt` creates an exact pending request only after all deterministic preconditions pass. The request fixes:

- effect/source/target/trigger/event;
- allowed result kinds;
- canonical target paths;
- deterministic values or numeric bounds;
- required companions;
- full-turn resubmission requirements.

When a bounded reaction uses `dependency=after_component`, the request retains
the exact predecessor component. A dependent `resource_delta` joins the common
resource graph only after the predecessor receipt also resolves to an applied
mutation. `narrated_no_state_change`, replay, skipped work, or any resolution
that produces no predecessor transition never authorizes the dependent
mutation.

The accepted receipt must match all fields and current session/turn. It is consumed exactly once and receives terminal replay evidence even when the narrative result changes no canonical mechanic.

## 8. Idempotency Keys

Each plan records client-owned transition identity plus the accepted event reference. Repeated input is classified as:

- same plan/same event: return existing result/no-op;
- same event with different operation: protected conflict;
- stale terminal receipt/event: protected replay;
- new accepted event: ordinary transition eligibility.

## 9. Wound Lifecycle Boundary

- An effect may observe an exact wound continuation or accepted wound transition.
- Effect expiry/removal never writes the wound carrier.
- A wound transition may supply source-loss or condition-change authority to the effect plan.
- Missing/ambiguous wound authority blocks both effect creation and repair retargeting.

## 10. Realm Transition

- Ordinary active effects cannot change realm or target owner.
- Source policy may declare `suspend` or `expire` on a specific realm exit.
- Resumption requires the exact same source/target binding and a registered policy.
- Cross-life persistence requires a future explicitly registered soul-bound profile; descriptive lore is not enough.
