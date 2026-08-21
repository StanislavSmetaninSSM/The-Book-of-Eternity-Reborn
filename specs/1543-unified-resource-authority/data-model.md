# Data Model: Unified Resource Authority

**Feature**: [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543)

**Blocked consumer**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535), Effect Task 9

## 1. Canonical roots

The feature owns four JSON roots:

| Root | Ownership | Lifecycle |
| --- | --- | --- |
| `resource_definitions.json` | Client-sealed canonical authority | Created at bootstrap or bounded setting expansion; ordinary turns cannot rewrite definitions |
| `resource_state.json` | Client-owned live canonical authority | Replaced only by one accepted mechanics plan |
| `resource_history.json` | Client-owned immutable replay evidence | Append-only within the supported schema; no lossy compaction in #1543 |
| `resource_commands.json` | GM-authored transient input | Staged by response mapping and deleted only after successful atomic publication |

All roots are strict JSON objects. Duplicate properties at any depth, unknown fields in closed records, invalid JSON, a JSON `null` root, scalar/array roots, empty files, and whitespace-only files are invalid. A missing canonical root is permitted only for a specifically authorized pristine bootstrap transition; otherwise it is an incompatible current save.

## 2. Shared scalar rules

- Canonical numbers are JSON numbers parsed as exact .NET `decimal`; arithmetic proof decomposes them into signed integer coefficient plus scale so intermediate comparisons never inherit `decimal` scale reduction.
- Exponent or precision forms are accepted only when they round-trip to the same exact decimal value.
- `integer` definitions require `decimal.Truncate(value) == value`.
- `quantum` is positive. A value is aligned when `(value - minimum) / quantum` is integral.
- `amount` is strictly positive and quantum-aligned for the target definition.
- No NaN, infinity, binary floating-point tolerance, implicit rounding, or string-encoded number is accepted.
- Exact identifiers are non-empty, already trimmed, case-sensitive, normalization-stable, free of control/format/line-separator characters, and unique by the repository's Unicode-confusable key.
- Realm tokens are `mortal_world`, `chaos_sea`, or `shining_abode`.

Initial technical limits are closed constants, not GM-authored settings:

| Limit | Value |
| --- | ---: |
| Definitions per catalog | 256 |
| Live state entries | 20,000 |
| Capacity transitions per turn | 256 |
| Ordinary/internal mutations per turn before trigger expansion | 512 |
| Effect/resource graph nodes per turn | 1,024 |
| Effect/resource graph dependency depth | 32 |
| Pending resource-resolution requests per turn | 64 |

History is not truncated by these limits. A separate tracked feature is required before any compaction policy is introduced.

## 3. Resource definition catalog

```json
{
  "schemaVersion": 1,
  "definitions": []
}
```

### 3.1 ResourceDefinition

```json
{
  "resourceKey": "health",
  "definitionVersion": 1,
  "displayName": "Здоровье",
  "numericKind": "integer",
  "unit": "percent_point",
  "quantum": 1,
  "minimumPolicy": {
    "kind": "definition_fixed",
    "value": 0
  },
  "capacityPolicy": {
    "kind": "registered_formula",
    "formulaKey": "mortal_health_capacity_v1"
  },
  "initializationPolicy": {
    "kind": "maximum"
  },
  "allowedOwnerKinds": ["player", "npc", "combatant", "combat_group_member", "vehicle"],
  "allowedOperations": ["damage", "restore"],
  "defaultFloorPolicy": "clamp_to_minimum",
  "defaultCapPolicy": "clamp_to_maximum",
  "visibility": "player_visible",
  "materialization": {
    "schemaVersion": 1,
    "definitionId": "resource_definition_<opaque>",
    "seal": "resource_definition_seal_<opaque>",
    "createdAtTurn": 1,
    "createdEventRef": "bootstrap_1"
  }
}
```

Required fields are exactly those shown.

### 3.2 Definition fields

- `resourceKey`: semantic key. Exact/confusable-unique across all versions in #1543.
- `definitionVersion`: exactly `1`; in-place revision is unsupported.
- `displayName`: non-empty safe player/GM label. It is not identity.
- `numericKind`: `integer` or `decimal`.
- `unit`: one registered built-in token or an exact setting-defined token accepted with the definition. It has display meaning only and does not select arithmetic.
- `quantum`: positive exact decimal.
- `minimumPolicy`: `definition_fixed` with exact `value` in #1543.
- `capacityPolicy`: one of the variants below.
- `initializationPolicy`: one of the variants below. It determines current value for an absent coordinate; the GM never authors current state directly.
- `allowedOwnerKinds`: non-empty, exact, duplicate-free subset of the closed owner-kind catalog.
- `allowedOperations`: non-empty, exact, duplicate-free subset of `damage`, `restore`, `spend`, `gain`.
- `defaultFloorPolicy`: `reject_below_minimum` or `clamp_to_minimum`.
- `defaultCapPolicy`: `reject_above_maximum` or `clamp_to_maximum`.
- `visibility`: `player_visible`, `owner_visible`, `gm_only`, or `hidden`.
- `materialization`: complete client-owned envelope; forbidden in raw proposals.

### 3.3 CapacityPolicy

`definition_fixed`:

```json
{ "kind": "definition_fixed", "value": 100 }
```

`instance_fixed`:

```json
{ "kind": "instance_fixed" }
```

The authorized owner/capacity transition supplies an exact maximum for each state entry.

`registered_formula`:

```json
{
  "kind": "registered_formula",
  "formulaKey": "afterlife_spiritual_action_points_v1"
}
```

The formula catalog is closed in client code. No expression, JSON path, source code, method name, or arbitrary parameter bag is allowed.

Version 1 registers exactly these capacity functions:

| Formula key | Closed typed inputs |
| --- | --- |
| `mortal_health_capacity_v1` | player permanent Strength/Constitution, or a validated materialized maximum for npc/combatant/group-member/vehicle |
| `mortal_energy_capacity_v1` | player permanent Constitution/Intelligence/Wisdom/Faith, or a validated materialized maximum for npc/combatant |
| `mortal_poise_capacity_v1` | player permanent Strength/Constitution/Intelligence/Wisdom, or a validated materialized maximum for combatant/group-member |
| `afterlife_spiritual_action_points_v1` | actor Spirit Focus tier, or exact conflict-side ID plus its validated maximum |
| `afterlife_return_gacha_attempts_v1` | Guardian reputation/Abode Power/founder bonus/return-cycle ID, or Shining radiance tier/return-cycle ID |

Every typed input includes an exact SHA-256 owner-authority fingerprint. The resolved capacity fingerprint additionally binds the sealed definition ID/seal, exact owner coordinate, formula key, every consumed scalar, and result. `instance_fixed` similarly binds the exact accepted capacity-authority fingerprint. A changed definition, owner authority, return cycle, characteristic, tier, or accepted maximum is stale input; it is not silently reused.

The three Mortal formulas preserve the existing mechanics rather than forcing a universal maximum of 100. Player maxima use the current exact equations: health `100 + Constitution*2 + Strength`, energy `100 + trunc(Constitution*0.75) + trunc(Intelligence*0.75) + trunc(Wisdom*0.75) + trunc(Faith*0.75)`, and poise `100 + trunc(Strength*1.5) + trunc(Constitution*1.5) + trunc(Intelligence*1.5) + trunc(Wisdom*1.5)`. Other allowed owners use their validated materialization maximum through the same formula key; the GM cannot choose a method or inject a path/expression.

### 3.4 InitializationPolicy

Closed variants:

```json
{ "kind": "minimum" }
{ "kind": "maximum" }
{ "kind": "fixed", "value": 3 }
{ "kind": "registered_formula", "formulaKey": "afterlife_return_gacha_attempts_v1" }
```

The result must satisfy numeric kind, quantum, minimum, and resolved maximum. A formula reads only the validated owner/context snapshot declared by its registration. Initialization is resolved separately from capacity and its fingerprint binds the resolved-capacity fingerprint, maximum, sealed initialization policy, typed formula authority when used, and exact result.

### 3.5 Built-in definitions

The active new-game template includes at least:

| Key | Owners | Operations | Capacity source |
| --- | --- | --- | --- |
| `health` | player, npc, combatant, combat_group_member, vehicle | damage, restore | registered `mortal_health_capacity_v1` typed by owner |
| `energy` | player, npc, combatant | spend, gain | registered `mortal_energy_capacity_v1` typed by owner |
| `poise` | player, combatant, combat_group_member | damage, restore | registered `mortal_poise_capacity_v1` typed by owner |
| `durability` | item | damage, restore | instance fixed |
| `charges` | item | spend, gain | instance fixed |
| `ammunition` | item | spend, gain | instance fixed |
| `spiritual_action_points` | afterlife_actor, afterlife_conflict_side | spend, gain | registered `afterlife_spiritual_action_points_v1` with actor/conflict-side typed inputs |
| `gacha_attempts` | afterlife_actor, afterlife_scope | spend, gain | registered `afterlife_return_gacha_attempts_v1` with Guardian/Shining typed inputs |
| `blessing_rerolls` | afterlife_actor | spend, gain | instance fixed |

Mana, stamina, and setting-specific equivalents are not implicit aliases. They exist only after an exact definition is materialized.

## 4. Resource owner authority

### 4.1 ResourceOwnerKey

```text
ResourceOwnerKey(
  Realm: string,
  OwnerKind: string,
  ResourceOwnerId: string)
```

Closed owner kinds:

- `player`
- `npc`
- `combatant`
- `combat_group_member`
- `vehicle`
- `item`
- `afterlife_actor`
- `afterlife_conflict_side`
- `afterlife_scope`

### 4.2 Stable owner mapping

| Domain entity | Owner key |
| --- | --- |
| Current Mortal player | `mortal_world/player/player_current` |
| Named Mortal NPC | `mortal_world/npc/<permanent NPCId>` |
| Named NPC combat representation | Same `mortal_world/npc/<permanent NPCId>` owner; combat row contains only binding |
| Anonymous individual combatant | `mortal_world/combatant/<client combatantId>` |
| Combat group member | `mortal_world/combat_group_member/<client memberId>` |
| Vehicle | `mortal_world/vehicle/<permanent vehicleId>` |
| Mortal item | `mortal_world/item/<permanent itemId>` |
| Persistent afterlife profile | `<current realm>/afterlife_actor/<permanent actor/profile id>` |
| Afterlife conflict side | `<realm>/afterlife_conflict_side/<client conflict-scoped id>` |
| Shining/return-cycle scoped reserve | `<realm>/afterlife_scope/<client scope id>` |

Names, aliases, array positions, paths, and indexes are never owner identity.

### 4.3 ResourceOwnerExport

```text
ResourceOwnerExport(
  Key: ResourceOwnerKey,
  State: active | suspended | terminal,
  SameTurn: bool,
  OwnerRef: string?,
  BoundNpcId: string?,
  Capabilities: IReadOnlySet<string>,
  RealmIndependentResourceCapabilities: IReadOnlySet<string>,
  AuthorityFingerprint: string)
```

`OwnerRef` is accepted only in the same turn and is consumed when the owning materializer publishes the permanent identity. `Capabilities` contains exact resource keys allowed by the validated owner/source contract. `RealmIndependentResourceCapabilities` is a client-sealed, duplicate/confusable-free subset of `Capabilities`; an unknown, unexported, ambiguous, or raw-authored exception is invalid. A definition's allowed owner kind, the owner's capability, and the shared per-capability activity resolver must all authorize initialization/mutation.

For the persistent `player_soul` owner, realm binding and resource activity are separate. Ordinary capabilities follow the active/suspended realm binding. A sealed realm-independent capability remains active while the owner persists across realms. Version 1 marks only `blessing_rerolls` realm-independent; `spiritual_action_points` remains realm-bound. The exact subset participates in the owner fingerprint and is consumed by owner resolution, lifecycle planning, and canonical state agreement.

### 4.4 Common combat identity

New individual combatants use `combatantRef` and omit `combatantId`; every accepted ref is rewritten to one client-owned permanent combatant ID even when no effect or resource command targets it. New group members use `memberRef` and omit `memberId`; every accepted member ref is rewritten similarly. Canonical combat state forbids residual refs and preserves client IDs exactly.

Named combatants may carry `NPCId`; it must resolve exactly to composed NPC authority. They do not receive a separate health resource coordinate. Anonymous combatants must not claim `NPCId`.

## 5. Resource state ledger

```json
{
  "schemaVersion": 1,
  "entries": []
}
```

### 5.1 ResourceStateEntry

```json
{
  "realm": "mortal_world",
  "ownerKind": "player",
  "resourceOwnerId": "player_current",
  "resourceKey": "health",
  "current": 85,
  "maximum": 100,
  "capacityBinding": {
    "kind": "registered_formula",
    "authorityKey": "mortal_health_capacity_v1",
    "authorityFingerprint": "sha256:<64 lowercase hex>"
  },
  "state": "active",
  "chronology": {
    "createdAtTurn": 1,
    "createdEventRef": "bootstrap_1",
    "lastTransitionId": "resource_transition_<opaque>",
    "lastEventRef": "turn_42",
    "lastTransitionTurn": 42
  }
}
```

Identity is the exact `ResourceCoordinate`:

```text
ResourceCoordinate(Realm, OwnerKind, ResourceOwnerId, ResourceKey)
```

The root is sorted canonically by realm, owner kind, owner ID, then resource key for stable serialization. Duplicate or confusable coordinates are invalid. `current` and `maximum` obey the definition. `state` is `active` or `suspended`. Suspended state remains canonical but cannot be mutated unless the registered source route explicitly resumes it in the same plan.

### 5.2 CapacityBinding variants

- `definition_fixed`: `authorityKey` is the resource key; maximum equals definition value.
- `instance_fixed`: `authorityKey` identifies the accepted capacity source transition; maximum equals its exact value.
- `registered_formula`: `authorityKey` is the registered formula key and `authorityFingerprint` binds the exact validated owner inputs used to derive maximum.

Every variant includes a client-computed `authorityFingerprint`. Raw commands cannot submit it.

## 6. Transition history

```json
{
  "schemaVersion": 1,
  "entries": []
}
```

### 6.1 ResourceTransitionEntry

```json
{
  "transitionId": "resource_transition_<opaque>",
  "operationId": "resource_operation_<opaque>",
  "eventRef": "turn_42_effect_2",
  "originKind": "effect_component",
  "originId": "component_001",
  "phase": "effect_trigger",
  "priority": 200,
  "executionSequence": 7,
  "coordinate": {
    "realm": "mortal_world",
    "ownerKind": "player",
    "resourceOwnerId": "player_current",
    "resourceKey": "health"
  },
  "operation": "damage",
  "capacityDisposition": null,
  "requestedAmount": 3,
  "appliedAmount": 3,
  "outcome": "applied",
  "beforeState": {
    "current": 88,
    "maximum": 100,
    "capacityBinding": {
      "kind": "registered_formula",
      "authorityKey": "mortal_health_capacity_v1",
      "authorityFingerprint": "sha256:<64 lowercase hex>"
    },
    "state": "active"
  },
  "afterState": {
    "current": 85,
    "maximum": 100,
    "capacityBinding": {
      "kind": "registered_formula",
      "authorityKey": "mortal_health_capacity_v1",
      "authorityFingerprint": "sha256:<64 lowercase hex>"
    },
    "state": "active"
  },
  "sourceEvidence": {
    "sourceKind": "effect_component",
    "sourceId": "component_001",
    "authorityFingerprint": "sha256:<64 lowercase hex>"
  },
  "policyFingerprint": "sha256:<64 lowercase hex>",
  "receiptId": null,
  "turn": 42
}
```

`beforeState` and `afterState` are complete immutable snapshots of current, maximum, capacity binding, and lifecycle. `initialize` requires absent `beforeState`, an active `afterState`, and `capacityDisposition: initialize_from_definition`; the history parser proves `minimum`, `maximum`, and `fixed` initialization directly from the sealed definition, while `registered_formula` is additionally recomputed from typed composed owner authority by the reducer. `retire` requires a live `beforeState`, absent `afterState`, exact `owner_lifecycle` source evidence, and a null disposition. Reconfigure requires an actual maximum or capacity-binding change plus exactly one explicit disposition: `preserve`, `clamp_to_new_maximum`, or `scale_ratio_exact`. Preserve keeps current exact; clamp produces `min(before.current, after.maximum)` with exact applied/clamped evidence; ratio scaling must satisfy exact coefficient/scale cross multiplication and the target quantum without rounding. Ordinary arithmetic separately proves the complete requested candidate and the applied candidate: both must be representable, the requested candidate must cross the relevant bound for a clamp, and the applied candidate must equal the after-state at that bound. A checked operation that silently reduces scale is invalid even when `appliedAmount` is zero. Suspend, resume, retire, and ordinary mutations require a null disposition.

Stored transition operations are `initialize`, `reconfigure`, `suspend`, `resume`, `retire`, `damage`, `restore`, `spend`, and `gain`. Stored `outcome` values are only `applied`, `clamped_minimum`, and `clamped_maximum`. Every row carries the explicit nullable `capacityDisposition`; it is protected replay evidence and prevents capacity transitions from laundering an arbitrary current value. `semantic_replay` is an in-memory reducer result: it returns the previous entry and never appends a second history row. `executionSequence` is a non-negative client-owned identity of the transition's actual position within its turn; it is unique within that turn and is protected replay semantics.

Replay identity is the exact tuple:

```text
eventRef + originKind + originId + coordinate + operation
```

If the tuple exists and requested amount, source evidence, policy fingerprint, phase, priority, and receipt semantics match, it is an exact replay. Newly allocated transition/operation IDs are not part of replay matching. If any protected semantic differs, it is a conflicting replay.

History entries are immutable and canonically sorted by turn and `executionSequence`, then phase, priority, originId, operationId, eventRef, and transitionId as deterministic consistency tie-breakers. The sequence is assigned only after the complete phase/DAG order is known; event ordinal and lexical identities can never reorder an accepted dependency chain. Existing entries may never be removed, reordered semantically, or rewritten by accepted input.

For every coordinate, the first transition is `initialize`, each next `beforeState` equals the prior `afterState`, and no transition follows `retire`. Every nonterminal history chain has exactly one live ledger entry whose snapshot and chronology equal the chain's first/latest transitions; a terminal chain has none.

### 6.1 Plan-local history working set

Persisted history remains immutable and untruncated. During one accepted turn, `AcceptedMechanicsPlanner` creates one isolated `ResourceHistoryWorkingSet` from the validated `ResourceHistoryState`. It reuses baseline indexes for transition/operation/receipt uniqueness, confusable identities, replay keys, coordinates, and per-coordinate tails, and incrementally indexes newly accepted transitions. Exact replay can therefore see both baseline and earlier same-turn transitions without reparsing or rebuilding the full history.

The working set is never published or shared between plans. A failed mutation or graph blocks the whole plan and the set is discarded. On successful completion, one `Freeze()` call performs full cross-entry validation, canonical ordering, immutable index construction, and fingerprinting and produces the only `ResourceHistoryState` after-image. The ordinary reducer/planner path must not call the baseline state's whole-history `Append` helper once per mutation.

## 7. Transient command root

```json
{
  "resourceDefinitionCreations": [],
  "resourceCapacityChanges": [],
  "resourceChanges": []
}
```

All arrays are optional; an absent array means no commands of that type. A receipts-only effect command and a resource-command root with no resource changes remain valid no-ops if every present property is valid.

### 7.1 ResourceDefinitionCreation

```json
{
  "definitionRef": "mana_v1",
  "definition": {
    "resourceKey": "mana",
    "definitionVersion": 1,
    "displayName": "Мана",
    "numericKind": "integer",
    "unit": "point",
    "quantum": 1,
    "minimumPolicy": { "kind": "definition_fixed", "value": 0 },
    "capacityPolicy": { "kind": "instance_fixed" },
    "initializationPolicy": { "kind": "maximum" },
    "allowedOwnerKinds": ["player"],
    "allowedOperations": ["spend", "gain"],
    "defaultFloorPolicy": "reject_below_minimum",
    "defaultCapPolicy": "clamp_to_maximum",
    "visibility": "player_visible"
  },
  "eventRef": "turn_1",
  "reason": "Пробуждение дара"
}
```

`definitionRef` is exact/confusable-unique in the accepted turn and is never canonical. The route is valid only during accepted bootstrap/setting expansion. The raw definition omits materialization evidence.

### 7.2 ResourceCapacityChange

```json
{
  "operation": "initialize",
  "target": {
    "kind": "player",
    "targetId": "player_current"
  },
  "resourceKey": "mana",
  "capacity": {
    "kind": "instance_fixed",
    "maximum": 40
  },
  "currentDisposition": "initialize_from_definition",
  "source": {
    "kind": "setting_materialization",
    "sourceId": "mana_v1"
  },
  "eventRef": "turn_1",
  "reason": "Начальный запас маны"
}
```

Closed `operation` values: `initialize`, `reconfigure`, `suspend`, `resume`, `retire`.

Closed dispositions:

- initialize: `initialize_from_definition` only;
- reconfigure: `preserve`, `clamp_to_new_maximum`, `scale_ratio_exact`;
- suspend/resume/retire: disposition omitted.

The `capacity` object matches the definition policy. Same-turn targets use `targetRef`, never a GM-authored permanent ID. Retire requires owning terminal lifecycle evidence and appends terminal history before removing live state.

### 7.3 ResourceChange

```json
{
  "operation": "damage",
  "target": {
    "kind": "player",
    "targetId": "player_current"
  },
  "resourceKey": "health",
  "amount": 13,
  "source": {
    "kind": "combat_outcome",
    "sourceId": "exchange_7"
  },
  "eventRef": "turn_42",
  "reason": "Удар клинком"
}
```

Allowed fields are exactly `operation`, `target`, `resourceKey`, `amount`, `source`, `eventRef`, and `reason`. The source kind catalog is adapter-owned. GM input cannot set phase, priority, IDs, policies, current/maximum values, after-images, or paths.

## 8. Internal mutation and reducer result

### 8.1 ResourceMutation

```text
ResourceMutation(
  OriginKind: string,
  OriginId: string,
  OperationId: string,
  EventRef: string,
  Coordinate: ResourceCoordinate,
  Operation: damage | restore | spend | gain,
  Amount: decimal,
  Phase: direct_cost | direct_outcome | registered_system_outcome | effect_trigger,
  Priority: int,
  PolicyBinding: ResourcePolicyBinding,
  Dependencies: IReadOnlyList<ResourceOperationKey>,
  SourceEvidence: ResourceSourceEvidence,
  ReceiptId: string?)
```

`OperationId`, phase, priority, policies, dependencies, and source evidence are client-derived. An adapter cannot write files or apply arithmetic.

A registered-system adapter may declare a closed client-owned derived amount policy instead of a GM-authored amount. The version-1 loss-recovery policy derives an exact integral percentage from the net direct-phase loss on one coordinate, floors once through coefficient/scale arithmetic, and yields no mutation when the result is zero. Replay reuses the immutable stored requested amount; it never recalculates from a later ledger or a changed owner surface.

### 8.2 Registered system outcome draft

```text
ResourceRegisteredSystemOutcomeDraft(
  Fingerprint: string,
  SourceExports: IReadOnlyList<ResourceMutationSourceExport>,
  Mutations: IReadOnlyList<ResourceMutationIntent>,
  ExpectedBeforeImages: IReadOnlyDictionary<string, CanonicalBeforeImage>,
  Project: ResourcePlanningResult -> OwnerCompanionAfterImages | Issues)
```

The draft is immutable and performs no I/O. Before planning, it contributes only exact source authority and mutations to the same four-phase reducer. After that reducer succeeds, it projects its companion after-images from the exact applied/replay transitions produced by that same result. Those companion paths and exact expected pre-publication before-images become part of the one `AcceptedMechanicsPlan`; path conflicts or projection issues discard the complete plan. A registered outcome cannot invoke a second reducer, directly change resource state/history, or publish independently.

### 8.3 ResourceMutationResult

```text
ResourceMutationResult(
  WorkingLedger: ResourceWorkingLedger?,
  Event: ResourceAppliedEvent?,
  Transition: ResourceTransitionEntry?,
  ReplayTransition: ResourceTransitionEntry?,
  Issues: IReadOnlyList<ValidationIssue>)
```

Success has one updated ledger plus an applied or exact-replay result. Failure has no after-state.

### 8.4 Resource events

Closed events:

- `resource_damaged`
- `resource_restored`
- `resource_spent`
- `resource_gained`
- `resource_depleted`
- `resource_filled`

Each event contains the exact operation identity, coordinate, before/after, applied amount, turn, and source fingerprint. Depleted/filled are emitted only on boundary crossing, not whenever an entry remains at a boundary.

## 9. Accepted mechanics graph and plan

### 9.1 AcceptedMechanicsInput

Contains:

- session, request, snapshot token, realm, turn, and accepted-event authority;
- parsed definition/state/history roots;
- exact pre-turn owner catalog plus validated same-turn owner plans;
- resource commands and registered internal mutations from ordinary local/system routes;
- effect source/target/carrier/index state and effect commands;
- pending-resolution state and receipts;
- exact protected before-images/fingerprints.

No input reader mutates state.

### 9.2 AcceptedMechanicsPlan

```text
AcceptedMechanicsPlan(
  Fingerprint: string,
  DefinitionAfterImage: JsonObject,
  StateAfterImage: JsonObject,
  HistoryAfterImage: JsonObject,
  EffectCarrierAfterImages: IReadOnlyDictionary<string, JsonObject>,
  EffectIdentityAfterImage: JsonObject,
  PendingAfterImages: IReadOnlyDictionary<string, JsonObject?>,
  OwnerCompanionAfterImages: IReadOnlyDictionary<string, JsonObject>,
  ConsumedPaths: IReadOnlyList<string>,
  TouchedPaths: IReadOnlyList<string>,
  BeforeImages: IReadOnlyDictionary<string, CanonicalBeforeImage>,
  AuthorityFingerprints: AcceptedMechanicsAuthorityFingerprints,
  ResourceEvents: IReadOnlyList<ResourceAppliedEvent>,
  ProjectionInput: ResourceProjectionInput)
```

The plan is immutable, uses random client IDs allocated exactly once, and is cached only after the complete raw resource/effect phase succeeds. Any earlier validation failure invalidates the handoff.

### 9.3 Planning sequence

1. compose definitions and owner authority;
2. initialize/reconfigure/suspend/resume/retire resource coordinates;
3. apply direct costs;
4. apply direct outcomes;
5. derive and apply registered system outcomes from the resulting direct-phase loss/state;
6. emit resource events after each actual result;
7. reconcile effect continuation/removal/application;
8. construct finite due trigger graph;
9. apply effect-triggered mutations and emit downstream events;
10. advance effect lifetime and terminal cleanup;
11. validate complete resource/effect/pending/owner after-state;
12. return plan or issues, never partial after-images.

Shining survival is the first production registered-outcome adapter. One newly visible ruinous Mortal event and one exact pending survival blessing contribute three player recovery mutations in `registered_system_outcome`; the same resource result then supplies the restored-amount audit while the draft consumes the blessing and downgrades the exact event in soul/world companion after-images. The former post-publication percentage-restoration path is absent, so runtime re-entry is a byte-exact no-op.

## 10. Pending resource resolution

### 10.1 Pending request

```json
{
  "schemaVersion": 1,
  "requestId": "resource_resolution_<opaque>",
  "sessionId": "...",
  "requestTurn": 42,
  "eventRef": "turn_42_effect_2",
  "effectId": "effect_<opaque>",
  "source": {},
  "target": {},
  "triggerId": "trigger_001",
  "allowedResults": [
    { "resultKind": "narrated_no_state_change" },
    {
      "resultKind": "resource_delta",
      "operation": "damage",
      "minimumAmount": 0,
      "maximumAmount": 5
    }
  ],
  "requiredCompanions": [],
  "fullTurnResubmissionRequired": true,
  "state": "pending",
  "createdAtUtc": "..."
}
```

The player/GM projection never exposes protected IDs, paths, or fingerprints. The technical record binds the exact resource coordinate and policies internally even though the GM packet uses safe labels.

### 10.2 Receipt

```json
{
  "requestId": "resource_resolution_<opaque>",
  "resultKind": "resource_delta",
  "amount": 3,
  "reason": "Яд усилился после описанного исхода"
}
```

`narrated_no_state_change` omits `amount`. No target, resource key, operation, IDs, path, after-state, or arbitrary object is accepted from the receipt.

### 10.3 Pending state transition

```text
absent -> pending -> resolved_and_consumed
                  -> terminal_rejected (operator evidence only)
```

No resource/effect mechanical after-image is published at `pending`. A valid receipt is transformed into a normal mutation during the coherent resubmitted turn. Replay returns terminal evidence without applying another mutation.

## 11. Resource projections

### 11.1 ResourceProjectionEntry

```text
ResourceProjectionEntry(
  SafeOwnerSelector: string?,
  ResourceKey: string,
  DisplayName: string,
  Unit: string,
  Current: decimal,
  Maximum: decimal,
  Percentage: decimal?,
  State: active | suspended,
  RecentVisibleDelta: decimal?,
  AvailableOperations: IReadOnlyList<string>,
  Visibility: player_visible | owner_visible)
```

The projection omits permanent internal owner IDs when a safe selector is available, all transition/event/operation IDs, fingerprints, paths, source evidence, pending DTOs, receipts, issue codes, repair data, and hidden/GM-only entries.

### 11.2 Projection consumers

- Mortal player status derives health/energy/poise fields in memory.
- NPC and combat detail views resolve their exact owner and project accepted resource rows.
- Inventory detail resolves the permanent item ID and projects durability/charges/ammunition.
- Afterlife conflict/profile/gacha views project action points and bounded charges.
- GM context receives safe authoring labels, exact allowed operations/bounds, and temporary refs where necessary, but never canonical after-state authority.

No consumer may read a removed legacy field as fallback.

## 12. Domain cutover rules

### 12.1 Mortal player

Persisted player status retains narrative identity/condition and money only. `healthPercentage`, `energyPercentage`, and `poisePercentage` are removed. `currentHealthChange`, `currentEnergyChange`, and `currentPoiseChange` are removed from accepted response and status-change state. Bootstrap creates the three resource entries.

### 12.2 NPC and combat

NPC `currentHealthPercentage`/`maxHealthPercentage` are removed. Individual combat `currentHealth`/`currentPoise` and group `healthStates[]` are removed. Combat state retains stable owner bindings and descriptive/action state. Group rows carry stable member records rather than positional health arrays.

### 12.3 Vehicles

Vehicle `currentHealth`/`maxHealth` are removed from `vehicles.json`. A new vehicle exports permanent `vehicleId`, health capability, and capacity authority; accepted damage/restoration uses common mutations. Activation, parking, movement, and use as an item carrier preserve the coordinate. Permanent destruction retires every live vehicle resource atomically. Vehicle display/location/actions remain vehicle-owned companions.

### 12.4 Items

Item `durability`/`maxDurability` and the `item_resources.json` current/max resource authority are removed. Item definitions/materialization declare resource capabilities and instance-fixed initial capacities through the accepted resource owner plan; inventory and NPC item resource commands become common resource changes. Each active item exports `local_item_cost` and `local_item_outcome` sources bound to its exact permanent item owner, so use/repair/fire/reload cannot mutate another item's coordinate. Item movement across inventory, equipment, NPC, location, and offscreen carriers preserves the same coordinate. Split/merge requires an explicit `proportional_exact` disposition for every live resource; destruction/terminal consumption retires every live coordinate and appends terminal history atomically. Player-facing item values and action eligibility are derived later only through `ResourceProjectionService`; neither legacy sidecar nor raw-ledger UI fallback is allowed.

### 12.5 Afterlife

`activeConflict.actionEconomy.*.current/max`, Guardian/Shining `chargesUsedThisReturn/chargesPerReturn`, and numeric blessing reroll counters cease to be persisted mechanical values. Their owning states retain references/audit/display companions only when needed. Capacity formulas bind spirit focus, reputation/return-cycle authority, or accepted entitlement creation. Currencies and faction accounting remain untouched.

Client-owned Shining transitions use a sealed transition kind rather than inferring legality from an after-image. `SynchronizeCurrentShining` is exactly Shining Abode -> Shining Abode; `OrdinaryReentryFromChaosSea` and `AscensionFromChaosSea` are exactly Chaos Sea -> Shining Abode. Ascension additionally requires maximum enlightenment and no pending life-transition command in the same fresh canonical snapshot. One write lease covers every authoritative read, composed profile/binding/resource after-image, before-image comparison, publication, and rollback. Ordinary Shining -> Chaos Sea travel preserves the current Guardian return cycle and spent attempts; only a Mortal-death -> Chaos Sea transition establishes/refills a new incarnation-bound Guardian cycle.

### 12.6 Effects

`periodic_damage` and `periodic_restore` components resolve an exact resource coordinate and emit internal mutations. Resource-event triggers consume registered events from actual results. Story-facing resource receipts use the bounded request model. No effect component reads or writes legacy resource fields.

## 13. Validation invariants

- Every state entry resolves exactly one current definition and owner.
- Every history entry resolves the same coordinate and valid definition policy for its recorded turn semantics.
- Every capacity binding resolves its exact accepted authority.
- Every current value is reproducible from the previous accepted value plus immutable transitions within the current supported history contour.
- Every mutation source and target exists in the composed accepted after-state.
- Every same-turn ref is consumed into one client-owned permanent identity.
- Every command is absent after successful publication.
- No legacy resource authority field exists in active canonical state.
- Resource and effect after-images agree on every resource-linked component, trigger result, pending request, and terminal transition.
- Projection output is derivable from accepted definition/state/history only.

## 14. State transitions

### Definition

```text
unseen proposal -> validated proposal -> sealed active definition
sealed active definition -> unchanged
```

No update/delete transition exists in #1543.

### Resource coordinate

```text
absent -> active
active <-> suspended
active/suspended -> terminal history + absent live state
```

### Mutation

```text
proposed/internal -> source-authorized -> ordered -> applied | semantic replay
any invalid stage -> plan failure with zero after-images
```

### Accepted plan

```text
unvalidated -> validated cached -> lease-preflight-verified -> published -> post-validated -> consumed
                                    \-> stale/rejected -> zero writes
published -> post-validation failure -> byte/existence rollback
```

### Legacy save

```text
legacy resource authority present -> incompatible save failure
```

There is no promotion or migration transition.
