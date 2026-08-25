# Data Model: Complete Effect Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
**Date**: 2026-08-14

## Authority Overview

```text
validated source entity ── activeEffectDefinitions[] / closed adapter
                                  │
                                  v
effectChanges[] / effectEventReports[] ──> subordinate EffectAcceptedTurnPlan
                                  │
                                  v
                         AcceptedMechanicsPlan
                         │        │        │
                         │        │        ├──> bounded pending / terminal receipt evidence
                         │        ├──> canonical resource state + history
                         ├──> owner carriers + effect identity index
                         └──> effect-owned companions / accepted projections
```

Static source definitions and active instances are separate entities. The source owns what may be applied; the client owns active identity, resolution of stacking/lifetime, transition history, and publication. Every active instance appears in exactly one logical owner carrier and exactly one identity-index entry.

## 1. GM Command Staging

### 1.1 Effect Command Root

Transient path: `game_state/effects/effect_commands.json`

```json
{
  "effectChanges": [],
  "effectResolutionReceipts": [],
  "effectEventReports": []
}
```

Rules:

- The root is created only by response-field distribution.
- It is raw accepted-turn input, not canonical state.
- The common accepted-mechanics publisher consumes it and removes it in the same lease-bound publication as every effect/resource after-image.
- A pre-existing command root outside the active accepted response is stale/untrusted and fails closed.
- Legacy `playerActiveEffectsChanges` and `NPCEffectChanges` are not aliases.

### 1.2 Effect Change

Common fields:

| Field | Type | Rule |
| --- | --- | --- |
| `operation` | string | Exact `apply`, `dispel`, or `remove` |
| `target` | object | Exact target selector from the target catalog |
| `eventRef` | object | Exact accepted event/turn/exchange evidence; not a client transition ID. For multiple common effect commands, use `turn_<turn>` for entry 1 and `turn_<turn>_effect_<ordinal>` for later one-based entries. |
| `reason` | string | Non-empty GM-readable reason; never mechanical authority by itself |

`apply` adds:

| Field | Type | Rule |
| --- | --- | --- |
| `source` | object | Exact source kind, source ID, and `definitionKey` |
| `parameters` | object or null | Only source-declared keys inside source-declared bounds |

`apply` MUST omit `effectId`, `currentStacks`, remaining lifetime, transition IDs, receipts, and post-state.

`dispel` and `remove` add:

| Field | Type | Rule |
| --- | --- | --- |
| `effectId` | string | Exact existing opaque effect ID from technical GM context |
| `authority` | object | Exact cure, dispel, cleanup, source-loss, wound, quest, scene, or lifecycle evidence permitted by the active instance |

`dispel` represents a counteraction allowed by the effect. `remove` represents an exact accepted lifecycle/source/cure/cleanup event. Neither command may edit the active instance directly.

### 1.3 Target Selector

```json
{
  "kind": "player|npc|combatant|guardian|resident|radiant_actor|afterlife_actor|spiritual_conflict_side",
  "targetId": "exact-existing-id"
}
```

An apply command selects exactly one of `targetId` (pre-existing/effective permanent target) or `targetRef` (an exact same-turn temporary reference exported by the target's accepted plan). A new anonymous Mortal combatant carries `combatantRef`; the cached common plan allocates its client-owned stable `combatantId`, rewrites the combatant, and resolves a same-turn `targetRef` to that result. A same-turn named combat representation instead carries only `npcRef`, which must resolve through accepted NPC materialization to canonical `NPCId`; it does not receive a second combat-local identity and effects target the exact NPC authority. Canonical active instances always store `targetId`, never either raw ref. For `spiritual_conflict_side`, the selector resolves inside one exact active conflict and side/participant authority. No names, indices, case variants, aliases, or historical IDs are accepted.

### 1.4 Source Selector

```json
{
  "kind": "skill|spiritual_art|item|wound|quest|location|hazard|faction|world_event|fate_card|combat_action",
  "sourceId": "exact-id",
  "definitionKey": "exact-definition-key"
}
```

For a client-assigned same-turn source identity, the raw selector instead uses:

```json
{
  "kind": "item|location",
  "sourceRef": "exact-same-turn-ref",
  "definitionKey": "exact-definition-key"
}
```

The raw selector contains exactly one of `sourceId` or `sourceRef`. `sourceId`
selects a validated pre-turn identity or a validated same-turn owner whose
effective identity is already stable. `sourceRef` selects an exact temporary
reference exported by another accepted plan, such as a new item or location
whose permanent ID is client-assigned. The parent entity supplies realm and
owner authority. `definitionKey` selects one exact materializable definition
inside that source. Canonical active instances always store the resolved
`sourceId`, never `sourceRef`.

### 1.5 Accepted Event Report

`effectEventReports[]` contains closed GM-authored evidence for a registered
story-event adapter. It is not lifecycle authority by itself. The client binds
the evidence to sealed accepted-turn inputs, selects one exact eligible active
effect and source-owned trigger, and creates the internal lifecycle event.

The first registered report is `owner_critical_failure` in `mortal_world` for
`player/player_current`. Its `mortal_action_roll` evidence carries exact
`rollMode`, leading sealed `diceIndexes`, `selectedIndex`, `selectedValue=1`,
`originalOutcome=critical_failure`, `resolvedOutcome=failure`, and a readable
reason. Advantage modes select the maximum sealed die, disadvantage modes the
minimum, and ties use the lower index. The GM never provides `effectId`,
`triggerId`, remaining uses, or post-state. For multiple independent matching
effects, selection is deterministic by creation turn then ordinal effect ID.

## 2. Static Active-Effect Definition

Source-owning entities may contain a closed optional `activeEffectDefinitions[]`. Existing source shapes may instead be adapted only when they already prove all equivalent information.

```json
{
  "schemaVersion": 1,
  "definitionKey": "bleeding-consequence",
  "display": {
    "name": "Кровотечение",
    "description": "Рана продолжает отнимать силы.",
    "category": "debuff",
    "visibility": "visible"
  },
  "allowedRealms": ["mortal_world"],
  "allowedTargetKinds": ["player", "npc", "combatant"],
  "components": [],
  "parameterBounds": {},
  "stacking": {},
  "lifetime": {},
  "triggers": [],
  "removal": {},
  "links": []
}
```

### 2.1 Definition invariants

- `schemaVersion` is exactly the current supported version.
- `definitionKey` is non-empty, trimmed, exact, and unique/confusable-unique inside the source.
- Display fields are non-empty; visibility is `visible`, `hidden`, or `gm_only`.
- Category is `buff`, `debuff`, `condition`, `environmental`, or `mixed`.
- Realms and target kinds are non-empty closed sets.
- At least one registered component template exists.
- Every parameter bound names a component parameter and supplies exact numeric, enum, identity, or boolean bounds.
- Stack, lifetime, trigger, removal, and link policy is complete before application.
- Source definitions never contain permanent `effectId`, active remaining state, current stacks, terminal history, or client receipts.

## 3. Canonical Active Effect Instance

```json
{
  "schemaVersion": 1,
  "entityKind": "active_effect",
  "effectId": "effect_<opaque>",
  "state": "active",
  "realm": "mortal_world",
  "target": {
    "kind": "player",
    "targetId": "player_current"
  },
  "display": {
    "name": "Кровотечение",
    "description": "Рана продолжает отнимать силы.",
    "category": "debuff",
    "visibility": "visible",
    "sourceLabel": "Рваная рана"
  },
  "source": {
    "kind": "wound",
    "sourceId": "wound_exact",
    "definitionKey": "bleeding-consequence"
  },
  "components": [],
  "lifetime": {},
  "stacking": {},
  "triggers": [],
  "removal": {},
  "links": [],
  "chronology": {
    "createdAtTurn": 42,
    "createdEventRef": "turn_42:wound_opened",
    "lastTransitionId": "effect_transition_<opaque>",
    "lastTransitionTurn": 42
  }
}
```

### 3.1 Root fields

| Field | Rule |
| --- | --- |
| `schemaVersion` | Exact current integer |
| `entityKind` | Exact `active_effect` |
| `effectId` | Client-generated opaque permanent identity |
| `state` | `active` or `suspended`; terminal instances are removed from carriers |
| `realm` | `mortal_world`, `chaos_sea`, or `shining_abode` |
| `target` | Exact selector agreeing with the logical carrier |
| `display` | Player-readable semantics and visibility |
| `source` | Immutable exact source binding |
| `components` | Non-empty ordered registered instances |
| `lifetime` | One closed current lifetime state |
| `stacking` | Source-authorized policy and current result |
| `triggers` | Ordered declared event subscriptions |
| `removal` | Closed allowed removal/counteraction authority |
| `links` | Exact companion/source relationships |
| `chronology` | Minimal current transition evidence; full history remains client-owned |

Unknown root fields are rejected. Extension mechanics require a newly registered component profile, not an arbitrary root property.

## 4. Registered Mechanical Components

Each component has:

```json
{
  "componentId": "component_001",
  "profile": "characteristic_modifier",
  "priority": 100,
  "payload": {}
}
```

`componentId` is unique/confusable-unique within the effect and stable for its lifetime. `priority` is a bounded integer from the source definition. Components execute in ascending declared priority, then ordinal effect and component identity.

### 4.1 Profile payloads

| Profile | Required payload authority |
| --- | --- |
| `characteristic_modifier` | Exact registered characteristic, `flat` or `percent`, finite non-zero value, optional closed cap |
| `roll_modifier` | Non-empty registered check/operation set and exact `advantage` or `disadvantage` contribution |
| `resistance_modifier` | Exact registered damage/resource type, `flat` or `percent`, finite non-zero value, bounded cap |
| `periodic_damage` | Exact target resource, finite positive amount, damage type, declared trigger/interval, legal floor behavior |
| `periodic_restore` | Exact target resource, finite positive amount, declared trigger/interval, legal cap behavior |
| `action_control` | Exact registered action/operation plus `grant`, `restrict`, `forbid`, or bounded `cost_modifier` |
| `event_reaction` | Exact event type; `apply_definition`, `trigger_component`, `bounded_receipt`, `event_outcome`, `suspend`, or `remove`; exact same-source definition/component/outcome reference; dependency edge; per-component expansion budget |
| `wound_consequence` | Exact wound link plus registered symptom/consequence semantics; no wound mutation permission |
| `afterlife_combat_condition` | Existing condition kind, target side/actor, affected operations, legal axes, counterplay, payoff, and finite exchange/scene semantics |

Profile payload schemas are closed. Optional display text never substitutes for a required mechanical field.

`characteristic_modifier.characteristic` uses the exact twelve-value Block 5 runtime
catalog: `strength`, `dexterity`, `constitution`, `intelligence`, `wisdom`, `faith`,
`attractiveness`, `trade`, `persuasion`, `perception`, `luck`, and `speed`. Its optional
`cap` clamps the source-resolved numeric `value` before aggregation. For one accepted
player snapshot, flat values are summed with base and static permanent bonuses,
percentage values are then summed into one multiplier, and the final characteristic is
floored once. A finite intermediate/final aggregate outside `Int32` rejects the complete
active-effect characteristic projection; it is never saturated or partially applied.
Static `structuredBonuses` and `activeEffectDefinitions` remain separate source authority
and are never counted as live effect instances.

The internal computed characteristic value retains every accepted mechanic for GM/QTE
resolution. A separate client-owned `playerVisibleModifiedCharacteristics` projection is
derived from visible effects only. Hidden/GM-only components, their attribution rows, and
their numeric implications are absent from ordinary console/browser stats. Missing or
wrong-typed safe projection data never falls back to internal `modifiedCharacteristics`.
This field is derived client state, not a GM-authored response contract.

## 5. Stacking State

```json
{
  "stackKey": "bleeding",
  "policy": "stack",
  "maxStacks": 3,
  "currentStacks": 1,
  "refreshMode": null,
  "mergeRule": null
}
```

Rules by policy:

- `independent`: each accepted application gets a new identity; source-definition `maxStacks` bounds simultaneous identities at the logical coordinate, while every canonical instance stores `currentStacks=1` and `maxStacks=1`.
- `stack`: preserve identity and increase `currentStacks` to the source-owned maximum.
- `refresh`: preserve identity/count and use exact `reset` or bounded `extend` lifetime behavior.
- `replace`: terminate the old instance as `replaced` and create a new identity in one transition. The explicit incoming replacement policy may supersede one prior valid policy at the same coordinate; it does not require the retired instance to have been created by a `replace` definition.
- `merge`: preserve identity and combine only profile-declared fields through a deterministic reducer registered by every component profile in the source definition; a globally named reducer does not authorize an incompatible profile.

Logical stack coordinate:

```text
(realm, target.kind, target.targetId, source.kind, source.sourceId, stackKey)
```

For `independent`, multiple active entries may share the coordinate only when the source explicitly authorizes the independent policy and their count does not exceed source-definition `maxStacks`. Other policies permit at most one active entry per coordinate. A source-declared `component_response` at a stack boundary is executed only by the registered deterministic trigger/component phase; the stack reducer never invents an equivalent response.

## 6. Lifetime State

Every active instance has exactly one mode and only the fields legal for that mode.

| Mode | Current evidence | Completion |
| --- | --- | --- |
| `turns` | `remainingTurns` positive integer; exact owner-turn phase | Reaches zero after one governed advancement |
| `uses` | `remainingUses` positive integer; exact consuming trigger set | Reaches zero after governed consumptions |
| `until_time` | Non-negative integer `deadline` derived from exact `world_time.currentTimeInMinutes` plus positive source duration | Current accepted time reaches/passes deadline |
| `scene` | Exact `sceneId`/conflict ID | Exact scene closes or changes per source rule |
| `source_bound` | Exact source/link plus `onSourceLoss` | Source is no longer active/attached/equipped/maintained |
| `condition_bound` | Registered condition key and exact operands | Condition evaluates false in composed state |
| `permanent` | Explicit source permission; no remaining sentinel | Only an allowed dispel/cure/source transition closes it |
| `manual` | Non-empty registered removal authorities | One exact allowed authority closes it |

An optional `displayText` may explain the lifetime in-world but cannot drive completion.

For `until_time`, the source definition declares exact
`timeAuthority=world_time.currentTimeInMinutes` and a positive `duration`. The
client reads the accepted world-time value, computes the deadline with checked
`Int64` arithmetic, and stores only that canonical integer deadline. An exact
same-turn `setWorldTime.currentTimeInMinutes` overrides a retained root value;
an override without that exact number cannot fall back to stale time for a new
effect. A missing authority, GM-authored deadline, overflow, string timestamp,
or display phrase cannot become time authority.

For `manual`, the non-empty source-owned `lifetime.authorities[]` is itself an
exact removal-authority catalog. `removal.manualAuthorities[]` may add other
source-declared manual routes but is not a required duplicate of the lifetime
catalog.

The common Task 8 reducer supports all eight modes, but it never invents missing
runtime context. The current Mortal adapter supplies owner-turn and exact
world-time events. It also converts a closed
`effectEventReports[].owner_critical_failure` report with sealed d20 evidence
into one exact Fate Shield use event. Other uses plus scene/condition modes
require an owning accepted adapter to provide exact event, scene, or condition
evidence; none is inferred from prose.

For `source_bound`, `activePredicate` is an exact closed token. The registry is:

| Predicate | Compatible source kinds | Satisfied by |
| --- | --- | --- |
| `active` | Every registered source kind | The exact owning contract says the source is current and non-terminal; examples include an active quest/event, an unhealed wound, a current location/hazard, a carried item, or an unlocked learned owner. |
| `carried` | `item` only | The exact item is in the accepted player-inventory carrier and `MortalItemLocalActionPolicy` classifies it as carried by the player. |
| `equipped` | `item` only | The exact carried item ID or accepted same-turn creation reference is present in the validated equipment authority. |
| `unlocked` | `skill`, `spiritual_art`, `fate_card`, `combat_action` only | The exact owner exists in its accepted learned/unlocked contour and is active under that contour's own status rules. Afterlife Fate Cards use profile `status`; NPC Fate Cards use their validated `isUnlocked` field. |

Unknown tokens, source-kind-incompatible tokens, and predicates not satisfied by the composed accepted owner state fail closed even when no apply command references the definition. Status spelling/casing follows the owning validator; the effect adapter does not invent a second status grammar.

## 7. Trigger and Removal Models

### 7.1 Trigger

```json
{
  "triggerId": "on_owner_turn_end",
  "eventType": "owner_turn_end",
  "priority": 100,
  "componentIds": ["component_001"],
  "consumeUses": false,
  "resolutionMode": "deterministic"
}
```

- `eventType` comes from a closed accepted-event catalog.
- `componentIds` resolve inside the same instance.
- `resolutionMode` is `deterministic` or `bounded_receipt`.
- `event_reaction` binds its owning trigger mode to its result:
  `bounded_receipt` uses bounded resolution; every other result is
  deterministic. `apply_definition` owns closed downstream parameters;
  component results resolve one exact periodic component in the same instance;
  `event_outcome` resolves one exact source-owned transition from the closed
  client registry and does not replace ordinary lifetime advancement.
- Reaction dependency is exactly `before_current_event`, `after_component`, or
  `after_current_event`. The middle form names one exact predecessor selected
  by the same trigger and runs only when that predecessor mutation applied.
- `trigger_component` and `bounded_receipt` always use `after_component`:
  resource-component reactions are explicit causal edges, while an
  unconditional resource component is selected directly by its trigger.
- Direct/reaction dispatch is unique per component. Component dependencies and
  same-source downstream effect definitions form finite acyclic graphs.
- `maxExpansion` from 1 through 64 is enforced per reaction component across
  the whole accepted transition, including resource-derived reactions; a
  separate whole-transition ceiling of 64 prevents aggregate expansion.
- A downstream definition sharing the current logical stack coordinate must
  explicitly use `replace`; otherwise source validation rejects the graph. That
  incoming policy may supersede exactly one prior valid identity even when the
  retired definition used another policy; multiple prior identities remain
  ambiguous and fail closed.
- A client-derived child effect records optional exact
  `chronology.causalEventRef`. It is immutable causality/replay evidence and is
  absent from every GM-authored command/report.

### 7.1.1 Accepted Effect Boundary Transcript

Resource-event reactions use one immutable transcript owned by the common
accepted-mechanics planner. Pure candidate discovery remains separate from
accepted activation, exact applied-component evidence, and released reaction.

```text
EffectEventBoundaryStamp {
  boundaryOrdinal,
  parentBoundaryOrdinal?,
  producerOperationKey,
  eventKind,
  producerEventRef,
  producerTransitionId,
  producerExecutionSequence,
  producerMechanicsOrdinal,
  openMechanicsOrdinal,
  candidateBatchFingerprint
}

EffectEventBoundaryCloseStamp {
  boundary,
  mechanicsOrdinal
}

EffectBoundaryCausalClosureStamp {
  boundary,
  runtimeOperationIds[],
  replayStableOperationKeys[],
  replayStableFingerprint
}

AcceptedEffectBoundaryActivation {
  boundary,
  acceptedActivation,
  immutableCandidate,
  candidateFingerprint,
  mechanicsOrdinal
}

RejectedEffectBoundaryActivation {
  boundary,
  immutableCandidate,
  candidateFingerprint,
  reason
}

AppliedEffectComponentEvidence {
  boundary,
  activationCandidateIdentity,
  mutationOperationKey,
  componentId,
  transitionId,
  appliedAmount,
  mechanicsOrdinal
}

ReleasedEffectReaction {
  boundary,
  acceptedActivationTranscriptStamp,
  reactionExecution,
  releaseStage,
  mechanicsOrdinal
}

AcceptedResourceMutationEvidence {
  mutationOperationKey,
  transition,
  executionKind: applied | replay,
  mechanicsOrdinal
}

EffectTerminalAvailabilityReservation {
  boundary,
  acceptedActivationTranscriptStamp,
  kind: LastUse | AfterCurrentReaction,
  reactionFingerprint?,
  mechanicsOrdinal
}

AcceptedEffectBoundaryTranscript {
  acceptedPlanAuthority,
  boundaries[],
  boundaryCloses[],
  causalClosures[],
  acceptedActivations[],
  rejectedActivations[],
  appliedComponentEvidence[],
  resourceMutationEvidence[],
  releasedReactions[],
  terminalAvailabilityReservations[],
  expansionUsage,
  useProjectionOrdinal,
  pendingFrontierBoundaryOrdinal?,
  fingerprint
}
```

`mechanicsOrdinal` is a planner-owned monotonic causal order and is not the
resource scheduler's execution sequence. An actual event freezes its candidate
batch before release. Boundaries form a forest rather than a global stack:
unrelated actual events are roots, while a resource event produced inside a
selected causal lane names that exact open boundary as its parent. A boundary
closes only after its full causal closure is complete, it owns no unresolved
pending output, and every child has closed; eligible leaves close inner-first.
The immutable full closure remains sealed even when some prerequisites completed
before a nested boundary opened, while its runtime remaining set begins as
`fullClosure - alreadyCompletedOperations`.

The arbiter freezes and accepts a whole candidate batch before any output is
opened. A last-use activation, or an accepted unconditional
`after_current_event` reaction that will suspend/remove an effect or apply a
`replace` definition to one exact pre-reaction stack target, records an exact
target-effect terminal-availability reservation after that frozen acceptance
batch and before later boundaries are considered. For `replace`, the target is
derived from `(realm,target kind/id,source kind/id,stackKey)` and deliberately
excludes `definitionKey`; it may therefore differ from the reaction-owning
effect. The reservation makes that exact effect unavailable to later boundaries
without revoking already accepted siblings.
Same-batch rejection after a final use is valid only when the rejected candidate
sorts after that exact accepted activation; terminal reaction precedence remains
`remove > suspend > active` during the final fold.

One unresolved causally complete leaf may become the pending frontier. Its exact
ancestor chain remains open and suspended, unrelated branches must drain and
close, `after_current_event` is withheld for the whole open chain, and no use or
lifetime projection is emitted. Multiple unrelated ready pending leaves reject
as ambiguous. A complete transcript has every boundary explicitly closed and
exactly one terminal use/lifetime projection. The builder is permanently sealed
by its first freeze.

Runtime operation IDs remain exact evidence in the full transcript fingerprint,
but the rolling prefix used by pending discovery/replay hashes canonical resource
operation keys for a closed causal batch. Allocator-owned transition and operation
IDs therefore cannot change a legitimate continuation fingerprint. A terminal
reaction changes eligibility only for later boundaries, while already accepted
sibling outputs complete. A reaction-created or replacement effect first becomes
eligible in the next accepted mechanics transition. Discovery and actual replay
must reproduce the same transcript prefix; carrier materialization and
use/lifetime projection occur once after the terminal actual pass. If an accepted
candidate's child mutation is skipped, clamped to no change, or replayed, only
`after_component` outputs that require exact state-changing predecessor evidence
are suppressed. Unconditional `before_current_event` and `after_current_event`
reactions still release, and an accepted consuming activation still spends its
single use.

Every replacement execution carries a client-derived frozen target identity;
the GM never authors it. Actual release immediately makes that target
unavailable to later or nested boundaries, and final folding must prove that
one released reaction event produced the target's exact `replace` transition.
Before any reaction-owned ID or transition allocation, one indexed linear
preflight resolves the canonical downstream definition, groups every released
`apply_definition` by the full stack coordinate, and simulates each coordinate
in `MechanicsOrdinal` order. It rejects a preceding non-replace application
that would occupy frozen absence, create ambiguous occupancy, or carry a known
incompatible stack policy into a later replacement; a compatible
identity-preserving sibling remains legal. Each replace then has exactly one
runtime occupancy state:
`FrozenExact(EffectReplayIdentity)`, `FrozenAbsent`, or
`PriorSelfReplacementResult(eventRef)`. Multiple releases against frozen
absence fail closed. Multiple releases against one frozen typed target also
fail closed unless all are consuming activations from that same typed target in
one accepted boundary; that sole self-replacement fold binds every later member
to the exact typed result created by its predecessor. Runtime application
rechecks the selected occupant before scheduler mutation, and final validation
proves every create/replace pair rather than grouping by raw effect ID and
checking only the earliest event.
When accepted consuming activations belong to the retired target, projection
validates their immutable `N, N-1, ...` budgets and inserts all `consume`
transitions in activation order immediately before that single authoritative
`replace`. The old identity remains `replaced` even on final use. Intermediate
and final replacement identities retain their source-created lifetimes
untouched. This ordering is accepted-use evidence, not retroactive eligibility
for any replacement.

Resource-event routing is additive: the outer producer/event requirement is
appended to every already selected component requirement. In particular, an
`after_component` child retains both its exact predecessor requirement and the
outer resource-event requirement; the wrapper never replaces either edge.

### 7.1.2 Deferred QTE Resource-Event Continuation

Accepting a Mortal QTE offer captures a client-owned immutable continuation for
the exact future terminal resource event. It binds the session and session
generation, accepted source turn, canonical offer fingerprint, byte/existence-
exact pre-terminal roots, source and target authority, trigger candidate index,
pending causal state, and the complete semantic turn authority needed by the
common planner. The runtime stores only a continuation reference/fingerprint
and receipt-wait status in addition to ordinary active-scene progress; it never
duplicates or replaces this sealed authority. The GM authors none of it.

```text
QteDeferredEffectContinuation {
  schemaVersion,
  continuationId,
  sessionId,
  sessionGeneration,
  acceptedSourceTurn,
  qteId,
  offerFingerprint,
  runtimeBeforeFingerprint,
  sealedRootBindings[]: { path, existed, payloadBase64?, sha256 },
  sourceAuthorityBinding,
  targetAuthorityBinding,
  triggerCandidateBinding,
  pendingCausalBinding,
  semanticTurnFingerprint,
  acceptedMechanicsAuthority: { kind: qte_continuation, continuationId, sourceFingerprint },
  identityLedger[]: { semanticKey, identity },
  state: armed | terminal_selected | awaiting_receipt | terminal,
  selectedTerminalBinding?,
  currentWave?: { requestId, waveId, ordinal, safePacketFingerprint },
  resolvedWaveBindings[]: {
    requestId,
    waveId,
    ordinal,
    pendingStateFingerprint,
    receiptsFingerprint,
    fingerprint
  },
  terminalFingerprint?,
  authorityFingerprint
}
```

The continuation uses the current schema only. Every identity reachable from
any sealed terminal branch is allocated once by semantic key at acceptance and
persists in `identityLedger`; a process restart or complete-graph replay cannot
allocate a replacement. Terminal selection and every bounded replay consume
that acceptance-time ledger read-only; they never replace it with a
selection-time ledger. `sealedRootBindings` retain exact existence, bytes, and
hash evidence required both to rehydrate typed planning input and to prove the
final transaction still starts from the accepted authority.

`selectedTerminalBinding` is client-written once and fixes chapter/action/grade,
terminal outcome ordinal/id, positional resource commands, and their exact
producer event identities. A browser retry, console resume, or process restart
reuses that binding and never asks the mini-game to choose a second result.

When the selected terminal branch emits `resource_damaged` or
`resource_depleted`, the client revalidates its positional QTE command and
consumes that exact continuation. It supplies the preserved candidates,
resolver, and work evidence to `AcceptedMechanicsPlanner`; it never scans live
post-turn carriers to invent a new plan. Deterministic reactions, bounded pending
waves, use consumption, downstream effects, and terminal cleanup therefore keep
the same causal transcript and replay rules as an ordinary accepted turn.

The terminal publication is one transaction over the resource quartet, effect
carriers/index/history, pending and reaction outputs, QTE history, and runtime
closure. Exact retry reuses terminal evidence and applies neither damage nor a
trigger twice. Missing, stale, cross-session, reordered, tampered, or
fingerprint-mismatched continuation authority fails before any write. There is
no migration, live rebuild, or random/bootstrap fallback for this GM-authored
deferred path.

If the graph reaches a bounded receipt, the client publishes only the standard
safe pending packet plus `terminal_selected -> awaiting_receipt` continuation
state; it publishes no resource/effect after-image and does not append terminal
QTE history or close the runtime. Each receipt wave replays the same complete
semantic terminal candidate with immutable earlier-wave bindings. The final
wave atomically publishes all resource/effect after-images, terminal QTE history,
runtime closure, `continuation.state=terminal`, and deletion of the active
request/response/ready transport. A conflicting receipt or terminal replay
produces no partial publication.

Bounded QTE receipt transport is separate from an ordinary player turn:

```text
game_state/control/qte_deferred_effect_continuation.json
input/qte_effect_resolution_request.json
output/qte_effect_resolution_receipts.json
ready/qte_effect_resolution_complete.json
```

The request is written from `currentWave` and contains only
`requestKind=qte_deferred_effect_resolution`, exact session/generation/
continuation/request/wave/ordinal/source-turn/QTE correlation, and the standard
safe pending packet. The response is a closed object containing the same
correlation and `effectResolutionReceipts[]` only; mechanics commands,
narrative, post-state, and extra fields are forbidden. The daemon/helper writes
the ready marker last without requiring an ordinary pending-turn snapshot. The
resume handler validates current session generation, every correlation field,
the pending fingerprint, the exact receipt set, all prior wave bindings, and
the sealed before-images. Stale transport is quarantined or removed without
changing continuation/mechanics state. Resume performs no ordinary turn
increment, story/progression replay, or validation-repair cycle.

Every `resolvedWaveBindings` entry is a closed six-field object with a
contiguous ordinal, globally unique request/wave identifiers inside the
continuation, and an exact recomputed binding fingerprint. If `currentWave` is
present, its ordinal equals the resolved binding count and its identifiers do
not reuse any prior wave. Any extra field, gap, duplicate, or fingerprint
mismatch fails before a write.

### 7.2 Removal

```json
{
  "dispelCategories": ["physical_treatment"],
  "cureKinds": ["stop_bleeding"],
  "onSourceLoss": "expire",
  "onConditionLoss": null,
  "manualAuthorities": []
}
```

Every listed value is source-owned and registered. `remove`/`dispel` authority must match one listed route and the exact source/target/effect. Effect removal never grants permission to change the linked wound or other source.

## 8. Links

```json
{
  "kind": "wound|skill|spiritual_art|item|quest|location|hazard|faction|world_event|fate_card|combat",
  "targetId": "exact-id",
  "role": "source|condition|context|cleanup_companion"
}
```

Links are exact, realm-compatible, unique/confusable-unique within the effect, and validated against composed authority. The `wound` link is mandatory for `wound_consequence` and does not transfer wound-write authority.

## 9. Owner Carriers

### 9.1 Mortal player

Path: `game_state/player/effects.json`

```json
{
  "schemaVersion": 1,
  "activeEffects": []
}
```

### 9.2 Named NPCs

Path: `game_state/npcs/npc_effects.json`

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "NPCId": "exact-npc-id",
      "activeEffects": []
    }
  ]
}
```

There is at most one entry per exact/confusable NPC ID. Wound state may share the physical file only if its existing canonical subtree is preserved byte-semantically by the common accepted-mechanics publisher while applying the effect carrier after-image.

### 9.3 Mortal combatants

Paths: `game_state/combat/enemies.json` and `game_state/combat/allies.json`.

Each anonymous accepted combatant has one client-owned stable `combatantId`. New anonymous rows use one exact/confusable-unique `combatantRef`, which is consumed during accepted planning and never remains canonical. Named combat representations use the one existing NPC identity: same-turn rows submit only exact `npcRef`, canonical rows contain only exact `NPCId`, and neither form may also carry `combatantRef`/`combatantId`. Named effects live in the NPC effect carrier and target kind `npc`; anonymous effects remain in the combat row. Existing `activeBuffs` and `activeDebuffs` remain presentation/category-separated canonical arrays containing complete anonymous-combatant instances. Their union must agree with the identity authority, and no effect may occur in both arrays.

### 9.4 Persistent afterlife actors

Path: `game_state/meta/afterlife_entity_profiles.json`.

Applicable accepted profiles (`player_soul`, `guardian`, `resident`, `shining_resident`, `shining_faction_head`, `radiant_actor`, and other authorized profile kinds) carry `activeEffects[]`. Profile identity and actor binding remain owned by the existing actor materialization authority.

### 9.5 Afterlife spiritual conflict

Path: `game_state/meta/afterlife_spiritual_conflict_state.json`.

`activeConflict.combatConditions[]` keeps its specialized schema and receives the common permanent `effectId`, exact source/target binding, stack/lifecycle transition evidence, and identity-index entry through the adapter. It is not duplicated in `activeEffects[]`.

## 10. Effect Identity Index

Client-owned path: `game_state/effects/effect_identity_index.json`

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "effectId": "effect_<opaque>",
      "state": "active|suspended|expired|dispelled|removed|replaced",
      "realm": "mortal_world",
      "owner": {
        "kind": "player",
        "ownerId": "player_current",
        "carrierPath": "game_state/player/effects.json",
        "collection": "activeEffects"
      },
      "target": {},
      "source": {},
      "stackCoordinate": {},
      "createdAtTurn": 42,
      "transitions": []
    }
  ]
}
```

### 10.1 Transition entry

```json
{
  "transitionId": "effect_transition_<opaque>",
  "kind": "create|stack|refresh|replace|merge|trigger|consume|suspend|resume|expire|dispel|remove",
  "turn": 42,
  "eventRef": "turn_42:wound_opened",
  "sourceEffectIds": [],
  "resultEffectIds": ["effect_<opaque>"],
  "receiptId": null
}
```

Index rules:

- Root, entries, owner coordinates, stack coordinates, source bindings, and transitions use closed field sets.
- Active/suspended entries have exactly one matching carrier occurrence.
- Terminal entries have zero active carrier occurrences and immutable terminal transition evidence.
- `transitionId`, accepted `eventRef`, and applicable receipt IDs are unique/confusable-unique globally.
- Source, target, realm, owner, stack, and chronology agree with the active instance.
- GM state, repair packets, and player projections never author or expose the index.

## 11. Accepted Mechanics Plan and Effect Subplan

Both are in-memory only. `EffectAcceptedTurnPlan` is a subordinate proposal that
allocates random effect/combat identities once and supplies complete effect
carrier/index after-images to the common planner. It has no independent
publication handoff. `AcceptedMechanicsPlan` is the only publishable plan:

| Field | Meaning |
| --- | --- |
| `InputFingerprint` / `AuthorityFingerprints` | Exact session, request, snapshot, accepted events, commands, pending input, source/target/owner catalogs, carriers, index, ledger, and internal-input binding |
| `EffectPlan` | The exact subordinate effect plan used while constructing this common plan; never a separately consumable publication token |
| resource definition/state/history after-images | Complete canonical common-ledger result |
| effect carrier/index after-images | Complete final JSON for every touched logical owner and the identity authority |
| pending after-images / GM packet | Complete bounded work or terminal receipt evidence and its safe operator projection |
| owner companion after-images/transitions | Exact effect/resource-owned companion changes |
| before-images, touched paths, consumed paths | Exact lease preflight, canonical write/creation/deletion, and command-consumption set |
| resource events / projection input | Accepted common mechanics output used by downstream lifecycle and safe projections |

Raw effect validation may retain its validated subordinate result only long
enough for the resource validator to build the common plan. Normalization takes
and consumes exactly one cached `AcceptedMechanicsPlan`; no effect-only cache API
may authorize writes. Any change to session, request, snapshot, accepted input,
source/target/owner authority, carrier/index, resource state/history, pending
state, internal inputs, or exact before-images invalidates publication.

## 12. Bounded Resource Resolution for Effects

Client-owned path: `game_state/control/pending_effect_resolutions.json`

```json
{
  "schemaVersion": 2,
  "sessionId": "opaque-session",
  "requests": [
    {
      "requestId": "effect_resolution_<opaque>",
      "acceptedRequestId": "request_42",
      "requestTurn": 42,
      "eventRef": "turn_42:end",
      "effectAuthority": {
        "bindingKind": "permanent|accepted_application",
        "authorityId": "..."
      },
      "source": {},
      "sourceAuthority": {},
      "target": {},
      "targetAuthority": {},
      "triggerId": "on_owner_turn_end",
      "causalAuthority": {
        "effectId": "effect_<opaque>",
        "triggerId": "on_owner_turn_end",
        "activationEventRef": "effect_event_<opaque>",
        "triggerEventRef": "turn_42:end",
        "priority": 10,
        "activationOrdinal": 0,
        "consumesUse": true,
        "usesBefore": 1,
        "resourceProducerOperationKey": null,
        "componentId": "periodic_damage_001",
        "afterComponentId": null,
        "candidateFingerprint": "<sha256>",
        "transcriptPrefixFingerprint": "<sha256>",
        "waveOrdinal": 0
      },
      "coordinate": {},
      "resourceAuthority": {},
      "operation": "damage|restore",
      "allowedResults": [
        { "resultKind": "narrated_no_state_change" },
        {
          "resultKind": "resource_delta",
          "operation": "damage|restore",
          "minimumAmount": 0,
          "maximumAmount": 3
        }
      ],
      "requiredCompanions": [],
      "fullTurnResubmissionRequired": true,
      "state": "pending",
      "projection": {}
    }
  ],
  "terminalReceipts": []
}
```

For `effectAuthority`, `permanent` requires `authorityId == effectId`, while a
same-turn effect uses the exact `accepted_application` event. The broader
`same_turn_ref` binding remains available to source, target, and resource
authorities only; it is rejected for the effect field.

This is the common `ResourcePendingResolutionState`, not a second effect-owned
mechanics authority. The protected record additionally seals exact
session/request, source/target/resource binding, policy/full-turn/replay
fingerprints, causal activation/producer/component evidence, wave, and
chronology. A same-turn unpublished effect binds through its
accepted application event rather than through a predictable or durable raw ID.

The GM sees only the bounded safe projection and returns a corresponding
`effectResolutionReceipts[]` command with `requestId`, one allowed result,
optional bounded amount, and narrative reason. Missing, stale, extra, partial,
cross-target, wrong-operation, ambiguous-ref, conflicting-replay, or
out-of-bound receipts fail closed. The entire original turn must be resubmitted;
no resource/effect mechanics publish before a valid receipt. Successful
publication moves the receipt to immutable terminal evidence and consumes both
pending request and transient commands exactly once. The authoritative closed
shape and privacy rules live in
[`resource-pending-resolution.md`](../1543-unified-resource-authority/contracts/resource-pending-resolution.md).

Receipt output is replayed only when the original activation is reached; a
graph-origin activation is never pre-seeded. Terminal evidence retains the full
closed request authority as a typed resolved binding. If that output creates a
new bounded candidate, the next wave carries every prior binding and again
publishes no mechanics until a full causal replay reaches a terminal wave with
no new pending output. Version 1 technical pending state is rejected without
migration.

## 13. Accepted Mechanics Snapshot

In-memory read authority returned only after complete effect validation:

```text
EffectMechanicsSnapshot
  IsAccepted
  CharacteristicModifiersByTarget
  RollModifiersByTargetAndCheck
  ResistanceModifiersByTargetAndType
  PeriodicOperationsInExecutionOrder
  ActionControlsByTargetAndAction
  AfterlifeAxesByConflictAndSide
  PlayerVisibleEffectsByOwner
```

Consumers do not scan raw carrier aliases or interpret prose. If carrier/index agreement fails, the snapshot is rejected and contributes no mechanical component.

For an active afterlife spiritual conflict, the private GM turn preview projects
only snapshot components whose profile is `afterlife_combat_condition` and whose
exact realm/target is the current conflict side. The bounded projection is
`afterlifeSpiritualConflictPreview.conditionMechanics` with source
`accepted_effect_mechanics_snapshot_v1`; it carries `conditionId`, component
priority, side/actor, kind, affected operations, legal axes, counterplay,
source-owned payoff, stacks, and a visibility flag. It does not reinterpret the
component as a Mortal characteristic modifier. Hidden/GM-only components may
execute in this private GM projection, but their names/descriptions are masked
and they remain absent from the player-visible snapshot audit.

## 14. Player Projection

A projected visible effect contains only:

- readable name, description, category, and visible source label;
- complete player-relevant component summaries;
- current/max stacks;
- remaining turns, uses, deadline explanation, scene, source-bound, condition-bound, permanent, or manual explanation;
- available cure, counterplay, or dispel actions;
- allowed readable linked context;
- an opaque UI-local selector only when a player action needs one.

It never contains permanent/internal effect IDs, owner/carrier paths, source authority objects, stack keys, transition/event IDs, receipts, index entries, pending requests, repair/diagnostic wrappers, validation codes, file paths, or agent instructions. Hidden and GM-only effects are omitted from ordinary rows, counts, totals, and action catalogs.

## 15. Lifecycle State Transitions

```text
                          source/condition restored
                ┌────────────────────────────────────┐
                v                                    │
apply ──> active ──> suspended ──────────────────────┘
             │  │
             │  ├── stack / refresh / merge ──> active (same ID)
             │  ├── replace ──> replaced + new active ID
             │  ├── trigger / consume / tick ──> active or terminal
             │  ├── source/condition loss ──> suspended or expired
             │  ├── dispel ──> dispelled
             │  ├── remove ──> removed
             │  └── lifetime complete ──> expired
             └────────────────────────────────────────
```

Terminal states are immutable in the identity index. They cannot return to active; a future valid application creates a new identity except for source-authorized same-instance stack/refresh/merge before terminal transition.

## 16. Composed Validation Invariants

1. Every active/suspended instance has one exact carrier and one matching index entry.
2. Every terminal index entry has no active carrier occurrence.
3. Every source and target resolves exactly in the same composed realm/state.
4. Every source definition, component, lifetime, stack, trigger, removal, and link is closed and complete.
5. No direct GM command carries client identity, history, index, receipt, remaining state, or post-stack result.
6. No duplicate/confusable identity, event, transition, stack conflict, or carrier occurrence exists.
7. Trigger dependencies are finite and acyclic.
8. Derived mechanics are produced only after the entire carrier/index set passes.
9. Wound-linked effect transitions cannot mutate wound state.
10. All touched files, pending state, companions, and player output share one rollback boundary.
11. Missing pristine carriers may initialize empty; non-empty legacy carriers never promote.
12. Static source definitions remain distinct from active state.
