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

- Canonical numbers are JSON numbers parsed as exact .NET `decimal`.
- Exponent or precision forms are accepted only when they round-trip to the same exact decimal value.
- `integer` definitions require `decimal.Truncate(value) == value`.
- `quantum` is positive. A value is aligned when `(value - minimum) / quantum` is integral.
- `amount` is strictly positive and quantum-aligned for the target definition.
- No NaN, infinity, binary floating-point tolerance, implicit rounding, or string-encoded number is accepted.
- Exact identifiers are non-empty, already trimmed, case-sensitive, normalization-stable, and unique by the repository's Unicode-confusable key.
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
    "kind": "definition_fixed",
    "value": 100
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
  "formulaKey": "afterlife_spirit_focus_action_points_v1"
}
```

The formula catalog is closed in client code. No expression, JSON path, source code, method name, or arbitrary parameter bag is allowed.

### 3.4 InitializationPolicy

Closed variants:

```json
{ "kind": "minimum" }
{ "kind": "maximum" }
{ "kind": "fixed", "value": 3 }
{ "kind": "registered_formula", "formulaKey": "guardian_return_charges_initial_v1" }
```

The result must satisfy numeric kind, quantum, minimum, and resolved maximum. A formula reads only the validated owner/context snapshot declared by its registration.

### 3.5 Built-in definitions

The active new-game template includes at least:

| Key | Owners | Operations | Capacity source |
| --- | --- | --- | --- |
| `health` | player, npc, combatant, combat_group_member, vehicle | damage, restore | definition fixed 100 unless a separately registered owner policy is selected |
| `energy` | player, npc, combatant | spend, gain | definition fixed 100 |
| `poise` | player, combatant, combat_group_member | damage, restore | definition fixed 100 |
| `durability` | item | damage, restore | instance fixed |
| `charges` | item | spend, gain | instance fixed |
| `ammunition` | item | spend, gain | instance fixed |
| `spiritual_action_points` | afterlife_actor, afterlife_conflict_side | spend, gain | registered spirit-focus formula or authorized instance fixed for opposition |
| `gacha_attempts` | afterlife_actor, afterlife_scope | spend, gain | registered per-return formula |
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
  AuthorityFingerprint: string)
```

`OwnerRef` is accepted only in the same turn and is consumed when the owning materializer publishes the permanent identity. `Capabilities` contains exact resource keys allowed by the validated owner/source contract. A definition's allowed owner kind and the owner's capability must both authorize initialization/mutation.

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
    "kind": "definition_fixed",
    "authorityKey": "health",
    "authorityFingerprint": "<sha256>"
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
  "coordinate": {
    "realm": "mortal_world",
    "ownerKind": "player",
    "resourceOwnerId": "player_current",
    "resourceKey": "health"
  },
  "operation": "damage",
  "requestedAmount": 3,
  "appliedAmount": 3,
  "outcome": "applied",
  "before": 88,
  "after": 85,
  "sourceEvidence": {
    "sourceKind": "effect_component",
    "sourceId": "component_001",
    "authorityFingerprint": "<sha256>"
  },
  "receiptId": null,
  "turn": 42
}
```

Closed `outcome` values are `applied`, `clamped_minimum`, `clamped_maximum`, and `semantic_replay`. A replay does not append a second entry; the previous entry is returned to the planner as the result.

Replay identity is the exact tuple:

```text
eventRef + originKind + originId + coordinate + operation
```

If the tuple exists and amount/source/policy semantics match, it is an exact replay. If any semantic differs, it is a protected conflicting replay.

History entries are immutable and canonically sorted by turn, eventRef, phase, priority, originId, operationId. Existing entries may never be removed, reordered semantically, or rewritten by accepted input.

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

### 8.2 ResourceMutationResult

```text
ResourceMutationResult(
  WorkingLedger: ResourceWorkingLedger?,
  Event: ResourceAppliedEvent?,
  Transition: ResourceTransitionEntry?,
  ReplayTransition: ResourceTransitionEntry?,
  Issues: IReadOnlyList<ValidationIssue>)
```

Success has one updated ledger plus an applied or exact-replay result. Failure has no after-state.

### 8.3 Resource events

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
5. apply registered system outcomes;
6. emit resource events after each actual result;
7. reconcile effect continuation/removal/application;
8. construct finite due trigger graph;
9. apply effect-triggered mutations and emit downstream events;
10. advance effect lifetime and terminal cleanup;
11. validate complete resource/effect/pending/owner after-state;
12. return plan or issues, never partial after-images.

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

Item `durability`/`maxDurability` and the `item_resources.json` current/max resource authority are removed. Item definitions/materialization declare resource capabilities/initial capacity through the accepted resource owner plan; inventory and NPC item resource commands become common resource changes. Item movement preserves the same coordinate; destruction/terminal consumption retires it atomically.

### 12.5 Afterlife

`activeConflict.actionEconomy.*.current/max`, Guardian/Shining `chargesUsedThisReturn/chargesPerReturn`, and numeric blessing reroll counters cease to be persisted mechanical values. Their owning states retain references/audit/display companions only when needed. Capacity formulas bind spirit focus, reputation/return-cycle authority, or accepted entitlement creation. Currencies and faction accounting remain untouched.

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
