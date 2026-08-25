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
- The exact incoming `replace` policy may supersede one prior valid identity at
  the coordinate even if that identity stored another policy. It never selects
  among multiple prior identities; that coordinate is ambiguous and rejected.

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
- Only an accepted activation of a declared consuming trigger consumes a use.
  Potential graph events, denied candidates, and producer replays that emit no
  event consume nothing.
- One accepted trigger activation consumes exactly one use regardless of how
  many mutation, reaction, or pending outputs it owns. An emitted parent event
  and accepted trigger still consume that use when a child mutation later
  becomes a no-op, exact replay, or dependency skip, and when a bounded receipt
  chooses `narrated_no_state_change`.
- Trigger acceptance is recorded in one causal transcript before output
  evidence is projected. Reaching zero makes the effect terminal immediately;
  every later candidate for that effect, including a non-consuming trigger, is
  denied.
- Canonical `remainingUses` is the single ledger seed. Replayed lifecycle
  activations must prove the exact sequence `N, N-1, ...`; clamping or taking a
  minimum across conflicting budgets is forbidden.
- A released `apply_definition(policy=replace)` carries a client-derived exact
  pre-reaction target at `(realm,target kind/id,source kind/id,stackKey)`;
  `definitionKey` is not part of that coordinate, so the target may differ from
  the reaction owner. Release makes that target unavailable to later or nested
  boundaries, while `after_current_event` reserves it immediately after the
  frozen acceptance batch. Already accepted siblings still complete.
- If accepted consuming activations belong to the retired target, validate
  their immutable `N, N-1, ...` budgets and record every `consume` in activation
  order immediately before that identity's single authoritative `replace`.
  Intermediate and final replacement identities start with their own full
  source-defined lifetimes; accepted uses never transfer to them. On the old
  identity's final use, `replaced` remains the terminal state.

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
4. Materialize pure due/event trigger candidates without publishing outputs or
   changing lifetime.
5. After each actual accepted event, arbitrate its candidates by declared
   priority and stable trigger identity, append the accepted activation
   transcript, and open only accepted outputs.
6. Execute deterministic components or validate exact bounded receipts; record
   applied component evidence separately from trigger acceptance.
7. Project the accepted transcript into uses exactly once and advance
   turn/time/scene lifetime.
8. Expire completed instances and clean effect-owned companions.
9. Recompute the complete accepted mechanics snapshot and player projection.

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
- Static graph expansion materializes immutable candidates for potential
  producer events. It does not reserve uses, publish reactions or pending work,
  or decide which potential boundary event occurred.
- One actual reducer event opens its exact candidate group. The single runtime
  arbiter orders that group by ascending trigger priority, ordinal `triggerId`,
  and ordinal `effectId` as a technical tie-break, then emits an ordered
  accepted-activation transcript. Priority never crosses a producer/event
  causal boundary.
- Candidate, accepted activation, and applied component evidence are distinct
  records. Reaction-only, pending-only, and zero-mutation triggers can be
  accepted; a denied candidate produces no mutation, reaction, pending request,
  identity transition, or reaction-expansion usage.
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
- Acceptance and conditional output release are separate gates. An accepted
  trigger consumes its one use at the parent event, while each
  `after_component` mutation, reaction, or pending output is released only when
  the exact predecessor component has an applied transition. A skipped,
  no-op, or replayed predecessor suppresses that output and its expansion count
  without undoing the accepted activation.
- `before_current_event` and `after_current_event` are unconditional boundary
  reactions. Once their candidate is accepted, a skipped, no-op, or replayed
  child mutation does not suppress them and does not refund the accepted use;
  only an unmet `after_component` predecessor is conditionally withheld.
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
- Each actual resource event freezes its complete candidate batch before any
  reaction output. The common arbiter accepts or denies that batch once; no
  later phase may rediscover candidates or reinterpret deferred reactions as
  accepted authority.
- One immutable boundary transcript assigns a monotonic mechanics ordinal to
  the producer mutation, boundary open, accepted releases, causal descendants,
  boundary close, and final use projection. For every boundary it proves:
  `producer < open < before_current_event < causal continuation and nested
  boundaries < after_current_event < uses/lifetime projection`.
- `after_component` evidence exists only for an exact state-changing mutation
  whose applied amount is nonzero. A skipped, clamped no-op, or replayed
  transition may remain in diagnostic execution evidence but cannot release a
  reaction or consume expansion.
- Wrapping selected work in a resource-event boundary appends the outer
  producer/event requirement without replacing component-local requirements.
  An `after_component` child therefore preserves both the outer producer and
  its exact predecessor requirement; either unmet edge keeps that child
  suppressed.
- A boundary is frozen: `suspend` or `remove` does not revoke sibling outputs
  already accepted for that same boundary. Its availability change applies to
  every later boundary, and terminal precedence is `remove > suspend > active`.
  Terminal cleanup and use/lifetime projection occur once after all released
  reaction phases.
- After each frozen acceptance batch, the transcript reserves terminal
  availability for an accepted last use and for an accepted unconditional
  `after_current_event` suspend/remove or `apply_definition(policy=replace)`.
  The replacement reservation names the exact frozen pre-reaction target, even
  when it differs from the reaction owner. Reaction reservations block later
  boundaries but preserve every sibling already frozen in the same batch. Only
  a last-use reservation authorizes same-batch rejection, and only for a
  candidate ordered after the accepted final use. Reservations cannot be
  reconstructed during finalization.
- An effect definition created or replaced by a reaction cannot participate in
  the transition that created it. It becomes trigger-eligible only in the next
  accepted mechanics transition, keeping the runtime graph statically bounded.
- Expansion is charged on accepted release, not candidate discovery or trigger
  acceptance: `before_current_event` at boundary open, `after_component` on
  exact applied evidence, and `after_current_event` at boundary close.
- A downstream application on the reacting effect's stack coordinate is legal
  only when the downstream definition explicitly uses `replace`. The retired
  effect need not already carry `replace`; the incoming definition is the exact
  source authority for this atomic policy transition.
- Unknown result kinds or dynamically named effect definitions are forbidden.

### Deferred QTE producer boundary

A Mortal QTE terminal resource transition is a deferred producer, not an
effect-free exception. Offer acceptance must seal the exact session generation,
turn, offer, byte/existence-exact pre-terminal roots, source/target/trigger,
pending-causal, full-semantic-turn authority, and semantic identity ledger
required to continue the common trigger graph.
Terminal resolution may pass only that immutable continuation's candidates,
resolver, and work evidence to the accepted-mechanics planner. Rebuilding from
live post-turn state, allocating identities through a generic normalizer, or
silently applying only the resource transition is forbidden.

The resource transition, accepted trigger transcript, effect after-images,
pending/reaction outputs, QTE history, and runtime closure share one atomic
rollback boundary. Exact replay consumes neither the resource mutation nor the
effect activation twice; stale, missing, cross-session, or tampered continuation
authority produces no writes.

If a bounded reaction is reached, the selected terminal result is sealed once
and the QTE enters `awaiting_receipt`; only the standard safe packet and pending
authority are published. Resource/effect after-images, terminal QTE history, and
runtime closure remain unpublished until the final receipt wave replays the
same complete terminal candidate and produces no further pending work. Earlier
wave bindings remain immutable and client-owned throughout that replay.

Bounded QTE work uses a dedicated `qte_effect_resolution` request, closed
receipt response, ready-last marker, and GameEngine resume handler. It never
uses `turn_request`, fabricates or resurrects a pending-turn snapshot, or enters
the ordinary validation-repair loop. The GM may author only exact bounded
receipts for the published safe packet. Restart/retry reuses the same
continuation, terminal selection, wave correlation, and preallocated semantic
identities. Receipt resume does not advance the ordinary turn or replay story,
progression, offer acceptance, or terminal selection. Final publication also
cleans the active request/response/ready files inside the same rollback
boundary; stale transport causes no mechanics write and leaves the current
continuation resumable.

## 7. Bounded Resolution

`resolutionMode=bounded_receipt` creates an exact pending request only after all deterministic preconditions pass. The request fixes:

- effect/source/target/trigger/event;
- exact activation and trigger-event identities, priority, causal activation
  ordinal, consuming flag and uses-before evidence;
- stable producer operation/event authority for graph-origin requests;
- component and optional predecessor component, candidate fingerprint,
  transcript-prefix fingerprint, and pending-wave ordinal;
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

The accepted receipt must match all fields and current session/turn. It replaces
only the output of the same activation when the complete graph is replayed from
the beginning; it is never inserted as a root or pre-consumed trigger. It is
consumed exactly once and receives terminal replay evidence even when the
narrative result changes no canonical mechanic.

If a receipt-derived mutation reaches another accepted bounded candidate, the
client publishes a next pending wave and still publishes no resource, effect,
or lifetime after-image. Resolved bindings from every prior wave retain the
full immutable request authority and are replayed at their original causal
positions until a complete replay produces no further pending output. Technical
schema version 1 is rejected; no migration or compatibility interpretation is
provided.

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
