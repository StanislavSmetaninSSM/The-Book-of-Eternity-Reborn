# Data Model: Complete Effect Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
**Date**: 2026-08-14

## Authority Overview

```text
validated source entity ── activeEffectDefinitions[] / closed adapter
                                  │
                                  v
effectChanges[] ──> EffectAcceptedTurnPlan ──> owner active carrier
                             │                       │
                             ├──> effect identity index
                             ├──> pending bounded resolution
                             ├──> effect-owned companions
                             └──> accepted mechanics snapshot / player projection
```

Static source definitions and active instances are separate entities. The source owns what may be applied; the client owns active identity, resolution of stacking/lifetime, transition history, and publication. Every active instance appears in exactly one logical owner carrier and exactly one identity-index entry.

## 1. GM Command Staging

### 1.1 Effect Command Root

Transient path: `game_state/effects/effect_commands.json`

```json
{
  "effectChanges": [],
  "effectResolutionReceipts": []
}
```

Rules:

- The root is created only by response-field distribution.
- It is raw accepted-turn input, not canonical state.
- The effect normalizer consumes it and removes it before accepted publication completes.
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

An apply command selects exactly one of `targetId` (pre-existing/effective permanent target) or `targetRef` (an exact same-turn temporary reference exported by the target's accepted plan). A new raw Mortal combatant carries `combatantRef`; the cached plan allocates its client-owned stable `combatantId`, rewrites the combatant, and resolves a same-turn `targetRef` to that result. Canonical active instances always store `targetId`, never `targetRef`. Named NPC combatants also bind the combat-local anchor to exact NPC authority. For `spiritual_conflict_side`, the selector resolves inside one exact active conflict and side/participant authority. No names, indices, case variants, aliases, or historical IDs are accepted.

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
| `event_reaction` | Exact event type, declared bounded result kind, referenced component/effect definition, and dependency edge |
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
- `replace`: terminate the old instance as `replaced` and create a new identity in one transition.
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
world-time events. Uses await accepted trigger events, and scene/condition modes
require an owning accepted adapter to provide exact scene or condition evidence.

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
- Downstream effect definitions form a finite acyclic graph with an explicit expansion limit.

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

There is at most one entry per exact/confusable NPC ID. Wound state may share the physical file only if its existing canonical subtree is preserved byte-semantically by the effect normalizer.

### 9.3 Mortal combatants

Paths: `game_state/combat/enemies.json` and `game_state/combat/allies.json`.

Each accepted combatant has one client-owned stable `combatantId`. New raw combatants use one exact/confusable-unique `combatantRef`, which is consumed during accepted planning and never remains canonical. Existing `activeBuffs` and `activeDebuffs` remain presentation/category-separated canonical arrays containing complete active instances. Their union must agree with the identity authority, and no effect may occur in both arrays.

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

## 11. Accepted Effect Plan

In-memory only:

| Field | Meaning |
| --- | --- |
| `InputFingerprint` | Accepted raw input plus validated snapshot/session/source/target authority fingerprint |
| `Issues` | Raw, composed, protected, and repairable issues with exact actor/path binding |
| `FinalCarriers` | Complete final JSON for every touched logical owner |
| `FinalIdentityIndex` | Complete final client identity authority |
| `FinalPendingResolutions` | Complete bounded work root or absent state |
| `CompanionWrites` | Exact effect-owned companion changes |
| `MechanicsSnapshot` | Complete accepted derived component set |
| `PlayerProjection` | Safe visible effect data derived from the same accepted set |
| `TouchedPaths` | Exact canonical write/creation/deletion set |

The same plan object is reused by raw validation, effect-aware companion validation, normalization, and post-check. It is invalidated on changed snapshot, session, raw input, source authority, target authority, or relevant same-turn plan.

## 12. Pending Effect Resolution

Client-owned path: `game_state/control/pending_effect_resolutions.json`

```json
{
  "schemaVersion": 1,
  "sessionId": "opaque-session",
  "requests": [
    {
      "requestId": "effect_resolution_<opaque>",
      "effectId": "effect_<opaque>",
      "target": {},
      "source": {},
      "triggerId": "on_owner_turn_end",
      "eventRef": "turn_42:end",
      "allowedResultKinds": ["narrated_no_state_change"],
      "allowedTargetPaths": [],
      "numericBounds": {},
      "requiredCompanions": [],
      "fullTurnResubmissionRequired": true
    }
  ]
}
```

The GM sees a bounded task packet through technical context and returns a corresponding `effectResolutionReceipts[]` command. Missing, stale, extra, partial, cross-target, or out-of-bound receipts fail closed. The request and receipt are internal DTOs and are suppressed as whole shapes from player projections.

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
