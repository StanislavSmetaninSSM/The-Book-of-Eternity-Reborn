# Unified Resource Materialization and Transition Authority

**Date:** 2026-08-15

**Tracked issue:** [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543)

**Blocked feature:** [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535), Effect Task 9

**Status:** Approved architecture; awaiting review of this written design before implementation planning

## 1. Context

The game currently represents the same mechanical idea through unrelated fields:

- Mortal player health, energy, and poise are percentage strings in `player_status.json`, while separate turn-delta fields live in `status_changes.json`;
- named NPC health is optional NPC-core state;
- individual combatants use `currentHealth` and `currentPoise`, while groups use positional `healthStates[]` values;
- items carry charges, ammunition, durability, or another resource through item-specific fields and commands;
- afterlife conflicts keep action economy and spiritual reserves inside conflict-specific state;
- Effect Materialization defines periodic damage and restoration profiles, but has no single canonical resource authority to mutate.

Adding effect-only field adapters would create another resource implementation. Ordinary combat, items, effects, and afterlife operations could then disagree about ordering, bounds, replay, ownership, or the current value. Issue #1543 therefore blocks Effect Task 9 and establishes the resource foundation first.

This is a breaking technical Pre-Alpha cutover. It does not migrate or interpret old saves.

## 2. Goals

The resource subsystem must provide:

1. one canonical definition catalog for built-in and setting-defined resources;
2. one canonical current-state ledger for every supported resource owner;
3. one immutable transition history for exactly-once and replay protection;
4. one pure planner for ordinary actions, item operations, system rules, afterlife operations, and effect-generated mutations;
5. one atomic publication contour covering resource state, history, commands, pending work, effect companions, and projections;
6. exact decimal arithmetic, quantization, capacity, floor, cap, and operation policies;
7. closed owner identity and source/target authority without aliases, display-name inference, or arbitrary JSON paths;
8. in-memory GM/player projections that do not become a second persisted mechanical authority.

## 3. Scope

### 3.1 Included

- player health, energy, poise, and materialized setting-defined resources such as mana or stamina;
- named NPC resources;
- individual, anonymous, and group-member combatant resources;
- item charges, ammunition, durability, and other bounded item resources;
- afterlife action economy and other spendable spiritual reserves;
- ordinary damage, healing, recovery, costs, spending, restoration, and gain;
- deterministic periodic effect damage/restoration;
- bounded effect receipts that resolve into authorized resource mutations;
- complete owner creation, retirement, combat entry/exit, item movement/removal, and conflict-scope cleanup.

### 3.2 Excluded

Money, currencies, treasuries, faction resource ledgers, and market/accounting balances remain domain-specific transactional systems. They do not enter the damage/restore/spend resource lifecycle in this feature.

## 4. Design Principles

- **One authority:** a resource value exists canonically only in the resource ledger.
- **Definitions and instances are separate:** a definition describes legal mechanics; a state entry describes one owner's current value.
- **No arbitrary paths:** operations address typed resource coordinates, never file paths or JSONPath-like selectors.
- **No implicit aliases:** `stamina` does not silently mean `energy`; a setting must materialize the exact definition and owner state it uses.
- **Plan before write:** reducers produce after-images and issues without mutating files.
- **Whole-transition atomicity:** one invalid mutation blocks every resource/effect/companion write in the accepted turn.
- **Exactly once:** client-owned transition evidence prevents replay across retries and saves.
- **No compatibility layer:** legacy resource fields are removed as authority rather than read, promoted, or synchronized indefinitely.
- **Derived means non-authoritative:** UI and GM context projections are generated in memory from the accepted ledger.

## 5. Canonical Files

```text
game_state/resources/
  resource_definitions.json
  resource_state.json
  resource_history.json
  resource_commands.json
```

`resource_definitions.json`, `resource_state.json`, and `resource_history.json` are protected canonical state. `resource_commands.json` is transient accepted-turn input and must be consumed after successful publication.

The resource paths must participate in validated pending-turn snapshots, canonical write leases, rollback tracking, post-publication validation, save/load copying, and session replacement cleanup.

## 6. Resource Definitions

A definition has one exact, case-sensitive, Unicode-confusable-unique `resourceKey` and a closed shape equivalent to:

```json
{
  "resourceKey": "health",
  "displayName": "Здоровье",
  "numericKind": "integer",
  "unit": "percent",
  "quantum": 1,
  "minimumPolicy": {
    "kind": "definition_fixed",
    "value": 0
  },
  "capacityPolicy": {
    "kind": "definition_fixed",
    "value": 100
  },
  "allowedOwnerKinds": ["player", "npc", "combatant", "combat_group_member"],
  "allowedOperations": ["damage", "restore"],
  "defaultFloorPolicy": "clamp_to_minimum",
  "defaultCapPolicy": "clamp_to_maximum",
  "visibility": "player_visible",
  "materialization": {
    "schemaVersion": 1,
    "seal": "client-owned"
  }
}
```

Closed definition catalogs:

- `numericKind`: `integer` or `decimal`;
- `unit`: registered built-in unit or an exact materialized setting unit;
- capacity kind: `definition_fixed`, `instance_fixed`, or `registered_formula`;
- operation: `damage`, `restore`, `spend`, or `gain`;
- visibility: `player_visible`, `owner_visible`, `gm_only`, or `hidden`;
- floor/cap behavior: an explicit registered policy, never prose.

`registered_formula` points to a closed client implementation. It cannot contain source code, an expression language, a path, or a GM-selected method name.

Built-in definitions are shipped in the active new-game template. A setting-defined resource is accepted only through a dedicated materialization command. The GM may propose its semantic key and bounded policy, but the client validates uniqueness, creates the seal, and publishes the canonical definition. An ordinary turn cannot mutate a sealed definition.

A definition-creation proposal contains the complete public definition fields but omits the client seal, definition chronology, and post-state. It is legal only during an accepted world/bootstrap materialization or an explicit later setting-expansion transition. Updating an existing sealed definition in place is forbidden. A semantic revision requires a new definition version plus an explicit atomic reconciliation plan for every existing state entry; no such revision route is included in issue #1543.

## 7. Resource Owner Identity

Every resource-bearing entity resolves to one stable `resourceOwnerId`:

| Domain owner | Canonical resource owner |
| --- | --- |
| Current player | `player_current` |
| Named Mortal NPC | permanent NPC ID |
| Named NPC represented in combat | the same permanent NPC ID |
| Anonymous individual combatant | client-owned permanent combatant ID |
| Combat group member | client-owned permanent member ID created with the group |
| Inventory or world item | permanent item ID |
| Persistent afterlife actor/profile | permanent actor/profile ID |
| Afterlife conflict participant/side | client-owned scoped owner ID created with the conflict |

The owning actor/item/combat/conflict materializer creates or resolves the resource owner. A raw resource command cannot invent an owner.

Named NPC combat representations reference the NPC resource owner and do not fork health into a combat-local copy. Anonymous combatants remain combat-owned. Group members receive stable member IDs instead of positional array identity. Scoped afterlife owners are terminally retired when their conflict closes; persistent actors retain their persistent resource owner.

## 8. Resource State

`resource_state.json` contains a closed root and one entry per exact coordinate:

```text
realm + ownerKind + resourceOwnerId + resourceKey
```

An entry contains:

```json
{
  "realm": "mortal_world",
  "ownerKind": "player",
  "resourceOwnerId": "player_current",
  "resourceKey": "health",
  "current": 85,
  "maximum": 100,
  "capacityBinding": {
    "kind": "definition_fixed",
    "authorityKey": "health"
  },
  "state": "active",
  "chronology": {
    "createdAtTurn": 1,
    "lastTransitionId": "resource_transition_<opaque>",
    "lastEventRef": "turn_42",
    "lastTransitionTurn": 42
  }
}
```

Rules:

- all arithmetic uses exact `decimal`, never binary floating-point;
- values must be representable by the definition's `numericKind` and `quantum`;
- one coordinate has exactly one state entry;
- current and maximum must satisfy the definition and capacity binding;
- `active` and `suspended` are the only live states;
- terminal owner cleanup removes live state only after immutable terminal history is prepared;
- owner aliases, names, list positions, and file paths are not identity.

## 9. Transition History

`resource_history.json` stores immutable accepted evidence sufficient to detect exact replay and conflicting reuse:

```json
{
  "transitionId": "resource_transition_<opaque>",
  "eventRef": "turn_42_effect_2",
  "originKind": "effect_component",
  "originId": "component_001",
  "operationId": "operation_<opaque>",
  "coordinate": {},
  "operation": "damage",
  "amount": 3,
  "before": 88,
  "after": 85,
  "sourceEvidence": {},
  "receiptId": null,
  "turn": 42
}
```

Transition IDs, operation IDs, and applicable receipt IDs are client-owned and globally exact/confusable-unique. The same accepted event/origin/operation returns the previous result or no-ops. Reuse with different semantics is a protected conflict.

History retention and compaction may be optimized only if replay evidence remains equivalent and is separately specified and tested. This feature does not introduce lossy history cleanup.

## 10. Commands and Mutations

`resource_commands.json` carries transient GM-authored ordinary operations and unsealed materialization proposals. It never carries sealed canonical definitions, current state, maximum state, chronology, transition IDs, after-images, file paths, or receipt authority.

Its accepted root is closed to these optional arrays:

```json
{
  "resourceDefinitionCreations": [],
  "resourceCapacityChanges": [],
  "resourceChanges": []
}
```

- `resourceDefinitionCreations[]` contains complete unsealed proposals and is accepted only by the definition-materialization route described in section 6.
- `resourceCapacityChanges[]` names one exact target/resource and one complete source-authorized capacity transition. It cannot change `current` directly.
- `resourceChanges[]` contains ordinary `damage`, `restore`, `spend`, or `gain` commands with exact target, resource key, positive amount, source evidence, accepted event reference, and reason.

No raw command supplies `resourceOwnerId` for a same-turn owner, operation/transition IDs, phase, priority, floor/cap policy, or final value. The owning target/source plans resolve temporary references, and the client assigns ordering and identity.

Every accepted source of change becomes the same internal record:

```text
ResourceMutation
  originKind
  originId
  operationId
  eventRef
  coordinate
  operation
  amount
  phase
  priority
  policyBinding
  dependencies
```

Ordinary combat, healing, recovery, costs, items, afterlife operations, and effects use separate source adapters only to prove authority and construct this record. They do not implement their own arithmetic or write resource state directly.

Initialization and capacity reconfiguration are not ordinary mutations. They require the owning materialization or a separately authorized capacity transition.

A capacity transition uses a closed current-value disposition: `preserve`, `clamp_to_new_maximum`, or `scale_ratio_exact`. The source contract selects the disposition. `scale_ratio_exact` fails if the result cannot be represented by the target quantum; it never rounds. Reducing capacity below current without an authorized disposition fails before publication.

## 11. Accepted-Turn Order

The composed accepted turn follows this order:

1. compose and validate resource definitions, owner identities, and pre-transition state;
2. resolve and apply direct costs to an in-memory working ledger;
3. resolve and apply direct action, item, system, and afterlife outcomes to that working ledger;
4. emit closed resource events from the exact applied results;
5. reconcile effect bound continuation, removals, and applications;
6. select due scheduled and resource-event triggers and build the finite dependency graph;
7. execute graph nodes in stable `phase`, `priority`, `originId`, and `operationId` order, applying each resource node to the same working ledger before its downstream dependents;
8. advance effect uses/time/scene lifetime and terminal cleanup;
9. build complete resource definitions/state/history, effect carriers/index, pending state, and derived projections;
10. validate the complete after-state;
11. reacquire and compare every protected before-image/fingerprint under the canonical write lease;
12. publish all paths atomically and consume transient commands;
13. post-validate; on failure, restore every touched path byte-for-byte and by prior existence.

The closed resource phases are `direct_cost`, `direct_outcome`, `registered_system_outcome`, and `effect_trigger`. An adapter assigns the phase from its registered source route; the GM cannot choose it. Applied resource results emit only registered events such as `resource_damaged`, `resource_restored`, `resource_spent`, `resource_gained`, `resource_depleted`, or `resource_filled`.

Mutations are not summed before application because ordered floor/cap behavior is observable. A clamp is legal only when the governing definition/source policy explicitly declares it. Otherwise an out-of-range transition fails closed.

One invalid mutation, missing owner, unknown resource, quantum mismatch, overflow, stale event, ambiguous source, cycle, expansion overflow, or changed before-image blocks the entire accepted transition.

## 12. Planner Boundaries

The arithmetic boundary is a pure `ResourceMutationReducer`. It applies one already-authorized mutation to an immutable working ledger and returns the new ledger, result event, transition evidence, and issues. It knows nothing about effects, files, prompts, or source-specific commands.

The cross-domain `AcceptedMechanicsPlanner` owns phase traversal. It composes direct operations, calls the resource reducer, feeds the resulting registered events to the effect trigger graph, applies downstream mutations, and returns either:

- one complete accepted mechanics plan containing final resource state/history, effect state/index, transition IDs, pending state, touched paths, source bindings, and projection inputs; or
- a bounded list of validation issues and zero after-images.

Both layers perform no writes, logging, UI work, or GM repair. A separate input composer performs bounded file reads before planning; file/cache/publication orchestration remains outside the pure reducers.

The accepted mechanics plan is cached under the validated session, snapshot token, full definition/owner/state/history/source/target fingerprints, accepted events, commands, and internal mutations. Any input change invalidates the handoff. Publication cannot independently regenerate identities or recompute a different plan.

## 13. Bounded Resolution

Deterministic damage, restore, spend, and gain never create GM pending work.

A story-facing effect may create a client-owned pending request only after every deterministic prerequisite passes. The canonical pending record binds session, turn, event, effect/source/target/trigger identity, allowed results, numeric bounds, companions, and full-turn resubmission requirements.

The GM projection exposes:

```text
requestId
story-facing source/target context
allowed result kinds
typed resource coordinate labels
numeric bounds
required companions
full-turn resubmission instruction
```

It does not expose file paths, resource ledger routes, effect identity internals, transition history, seals, or player-private/hidden authority.

The receipt returns only:

```text
requestId
resultKind
allowed operands
narrative reason
```

The client resolves all protected binding through the pending request. Initially registered bounded result kinds are `narrated_no_state_change` and a bounded `resource_delta` whose coordinate and operation were fixed by the request. Extra, missing, stale, partial, cross-target, wrong-operation, or out-of-bound data fails closed.

Canonical resource/effect state does not change while the request is pending. A valid receipt requires one coherent full-turn resubmission, becomes an ordinary `ResourceMutation`, and is consumed exactly once with terminal replay evidence.

## 14. Domain Cutover

### 14.1 Mortal player

Player health, energy, poise, and setting-defined resources move to the ledger. Existing percentage and delta fields cease to be inputs or persisted mechanics. Player status, sidebars, stats, combat context, and GM context read a safe in-memory resource projection.

### 14.2 NPCs and combatants

Named NPCs retain one resource owner inside and outside combat. Anonymous combatants use their combatant owner. Group materialization assigns stable member IDs and member resources. Combat arrays reference resource owners rather than carrying authoritative current values.

Combat entry, update, defeat, exit, and named-NPC reconciliation are resource-plan operations, not ad hoc field copying.

### 14.3 Items

Item charges, ammunition, durability, and bounded reserves become item-owned resource entries. Item movement does not move or duplicate the resource entry because identity remains the item ID. Item destruction or terminal consumption closes its resources atomically with the item transition.

### 14.4 Afterlife

Persistent afterlife actors use persistent resource owners. Conflict action economy and other temporary reserves use scoped owners. Conflict start creates them, exchange operations mutate them, and conflict close retires them.

Specialized spiritual-combat axes and conditions remain specialized mechanics; only genuinely spendable/restorable quantities enter the resource ledger. Currency, treasury, and faction accounting remain outside it.

### 14.5 Effects

Effect Task 9 resumes only after the preceding owners and ordinary operations use the resource planner. `periodic_damage` and `periodic_restore` emit mutations against exact target coordinates. Unsupported target/resource combinations are impossible when the source definition is accepted because definition/owner capability is checked at application and again at execution.

## 15. Projections and Privacy

`ResourceProjectionService` is the only ordinary read surface for UI and GM context. It reads an accepted definitions/state/history snapshot and produces:

- localized names and units;
- current/maximum/percentage display values;
- owner-visible resource summaries;
- safe recent deltas when player-visible;
- action availability derived from current authority.

It never exposes internal owner IDs when a player-safe selector is sufficient, transition IDs, event refs, source authority, fingerprints, paths, pending DTOs, receipts, validation codes, or hidden/GM-only resources.

Persisted domain files do not retain derived health/energy/charge mirrors in the final merged feature. Temporary mirrors are allowed only inside the unmerged implementation branch and must be removed before integration.

## 16. Failure and Repair Policy

The following failures are protected and non-actionable for semantic repair: identity ambiguity, duplicate/confusable keys, direct ledger/history mutation, stale event/receipt, source/target mismatch, changed catalog, cycle, expansion overflow, cross-realm owner mismatch, invalid capacity authority, arithmetic overflow, and TOCTOU changes.

A repair packet may address only one unambiguous GM-owned omission in a definition, owner materialization, or ordinary command. It cannot ask the GM to author current values, transition evidence, IDs, after-images, or select a different owner.

Player-facing failures use generic in-world Russian text. Detailed resource coordinates, paths, codes, rollback evidence, pending instructions, and agent guidance remain operator-only.

## 17. No-Migration Policy

- No compatibility readers, legacy promotion, dual-write bridge, or automatic conversion are added.
- Old technical saves with legacy resource authority are rejected as incompatible.
- Active new-game state, templates, examples, fixtures, and test saves switch directly to the ledger.
- The public Pre-Alpha warning already states that save compatibility and operability are not guaranteed before release.

## 18. Implementation Decomposition

Issue #1543 is implemented as one blocking feature with independently verified internal slices:

1. contract/schema and pure definition/state/history validators;
2. pure mutation planner and replay/ordering/bounds arithmetic;
3. resource owner materialization for player, NPC, individual/group combatants, items, and afterlife scopes;
4. ordinary Mortal damage/heal/recovery/cost cutover;
5. item and afterlife resource cutover;
6. UI/GM context projection cutover;
7. Effect Task 9 periodic and bounded-receipt integration;
8. legacy field/validator/writer removal;
9. prompts, examples, manifests, templates, source guards, and final verification.

No partial slice is merged as the completed feature while a second persisted resource authority remains.

## 19. Verification Strategy

Required coverage includes:

- closed schema, duplicate, confusable, unknown-field, and wrong-root cases;
- exact decimal, integer, quantum, minimum, capacity, floor, cap, and overflow boundaries;
- stable mutation ordering and non-commutative boundary cases;
- exact replay, conflicting replay, stale history, cycle, and expansion-limit cases;
- player, named NPC, named NPC in combat, anonymous combatant, group member, item, persistent afterlife actor, and scoped conflict owner cases;
- same-turn owner/definition creation and mutation;
- ordinary damage, healing, recovery, spend, gain, item use, and the equivalent effect-triggered result;
- combat entry/exit, group member defeat, item move/destruction, and conflict close;
- bounded receipt success plus missing, stale, partial, extra, cross-target, wrong-operation, and out-of-bound rejection;
- direct ledger/history mutation and late TOCTOU mutation with zero writes;
- forced write and post-validation failures with byte/existence rollback across every touched path;
- no partial resource/effect/pending/companion publication;
- console/browser/GM-context parity and privacy;
- production validation of Mortal and afterlife prompts, examples, manifests, and active templates.

Implementation runs the smallest relevant Focused RED/GREEN filters, then one meaningful Fast checkpoint. One PreMerge control runs only when #1543 and the resumed #1535 integration are ready to merge.

## 20. Documentation and Tracking

- GitHub issue #1543 owns the resource feature.
- GitHub issue #1535 explicitly records that Effect Task 9 is blocked by #1543.
- A dedicated Spec Kit feature must link #1543 and #1535 in `spec.md`, `plan.md`, and `tasks.md` before implementation.
- GM-facing Mortal and afterlife contracts, examples, manifests, launcher/daemon prompt entrypoints, and documentation/source-guard tests change with the executable cutover.
- The afterlife contract matrix and its coverage tests must change whenever the resource cutover changes afterlife state or GM response surfaces.

## 21. Acceptance Boundary

The resource foundation is complete only when:

1. the ledger is the sole persisted mechanical resource authority;
2. every included existing mechanic uses the same planner;
3. no legacy resource field remains writable or mechanically consumed;
4. all owner lifecycles and resource operations are atomic and replay-safe;
5. Effect Task 9 periodic operations and bounded resource receipts use the same plan;
6. ordinary player/GM projections remain usable without exposing internal authority;
7. all required Focused, Fast, conditional full-validation, and final PreMerge evidence is green.
