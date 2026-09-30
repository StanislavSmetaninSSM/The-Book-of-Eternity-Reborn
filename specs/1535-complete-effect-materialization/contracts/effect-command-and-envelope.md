# Contract: Effect Commands and Canonical Envelope

**Feature**: [Complete Effect Materialization](../spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## 1. Scope

This contract defines the only common GM route for applying, dispelling, or removing an active runtime effect and the required canonical instance shape. Static source definitions remain separate authority.

## 2. Response Fields

```json
{
  "effectChanges": [],
  "effectResolutionReceipts": [],
  "effectEventReports": []
}
```

- All three fields are optional arrays in one accepted response.
- The distributor stages them under one transient command root.
- The normalizer consumes the command root; none remains in canonical owner state.
- `playerActiveEffectsChanges` and `NPCEffectChanges` are invalid current-schema fields.
- Non-empty `activeBuffs`, `activeDebuffs`, `activeEffects`, or `combatConditions` in a raw creation/update are not alternate effect-application commands.

## 3. Apply Command

```json
{
  "operation": "apply",
  "target": {
    "kind": "player",
    "targetId": "player_current"
  },
  "source": {
    "kind": "wound",
    "sourceId": "wound_torn_side",
    "definitionKey": "bleeding-consequence"
  },
  "parameters": {
    "damagePerTurn": 3
  },
  "eventRef": {
    "kind": "accepted_turn",
    "authorityId": "turn_42"
  },
  "reason": "Рана снова открылась."
}
```

Rules:

1. `operation`, `target`, `source`, `eventRef`, and `reason` are required.
2. `parameters` is absent/null or a closed object allowed by the exact source definition.
3. `effectId`, post-stack count, remaining lifetime, client transition/receipt/index fields, carrier path, and final component payload are forbidden.
4. The source determines display, component templates, bounds, stack, lifetime, trigger, removal, and visibility policy.
5. An instantaneous source action without a declared active-instance definition produces no active effect.
6. A target selector contains exactly one of `targetId` or same-turn `targetRef`. `targetRef` must resolve through an accepted target plan and is replaced by the client-owned permanent target ID in the canonical instance.
7. The first `effectChanges[]` entry uses accepted-turn authority `turn_<turn>`.
   Each additional entry uses the exact one-based ordinal authority
   `turn_<turn>_effect_<ordinal>` (for example `turn_42_effect_2`). The
   client maps these bounded public authorities to unique internal event
   evidence; arbitrary suffixes and reuse by two entries fail closed.

## 4. Dispel and Remove Commands

```json
{
  "operation": "dispel",
  "effectId": "effect_<opaque-from-technical-context>",
  "target": {
    "kind": "player",
    "targetId": "player_current"
  },
  "authority": {
    "kind": "physical_treatment",
    "authorityId": "treatment_turn_43"
  },
  "eventRef": {
    "kind": "accepted_turn",
    "authorityId": "turn_43"
  },
  "reason": "Кровотечение остановлено."
}
```

- `effectId` must resolve to exactly one active/suspended effect at the exact target.
- `dispel` authority must match a declared dispel/counterplay category.
- `remove` authority must match a declared cure, cleanup, source-loss, condition-loss, scene, quest, wound, or lifecycle route.
- Neither operation edits the effect or its source; the client calculates terminal state and cleanup.
- A wound-derived effect command never grants wound-write authority.

## 5. Accepted Event Reports

`effectEventReports[]` is the bounded route by which the GM reports audited
story events whose exact lifecycle reaction is owned by an already-materialized
effect. A report never names `effectId`, `triggerId`, remaining lifetime, or a
post-state. The client validates the report against sealed accepted-turn
evidence, selects the exact eligible effect and trigger, and derives the
lifecycle transition.

The first registered report adapter is the Mortal Fate Shield critical-failure
reaction:

```json
{
  "eventType": "owner_critical_failure",
  "target": { "kind": "player", "targetId": "player_current" },
  "evidence": {
    "kind": "mortal_action_roll",
    "rollMode": "normal",
    "diceIndexes": [0],
    "selectedIndex": 0,
    "selectedValue": 1,
    "originalOutcome": "critical_failure",
    "resolvedOutcome": "failure"
  },
  "reason": "Щит Судьбы смягчает критический провал."
}
```

Rules:

1. The report object and nested objects are closed; every shown field is required.
2. `rollMode` is `normal`, `advantage`, `great_advantage`, `disadvantage`, or `dire_disadvantage`.
3. `diceIndexes` is the exact leading sealed d20 pool (`[0]`, `[0,1]`, or `[0,1,2]`), and `selectedIndex` follows the corresponding max/min rule with the lower index winning a tie.
4. `selectedValue` must equal the sealed selected die and exactly `1`; the report proves `critical_failure -> failure` and cannot invent or improve another outcome.
5. At most one `owner_critical_failure` report is accepted for one Mortal action. If several independent Fate Shields exist, the client consumes exactly the oldest eligible instance (creation turn, then ordinal `effectId`).
6. Missing shield authority, stale/wrong dice, wrong realm/target/outcome, duplicate reports, or any client-owned selector fails the complete transition before publication.

## 6. Canonical Active Effect

Required root fields and their exact order-independent meaning:

```text
schemaVersion, entityKind, effectId, state, realm, target, display, source,
components, lifetime, stacking, triggers, removal, links, chronology
```

### 6.1 Closed values

- `entityKind`: `active_effect`
- `state`: `active`, `suspended`
- `realm`: `mortal_world`, `chaos_sea`, `shining_abode`
- target kind: `player`, `npc`, `combatant`, `guardian`, `resident`, `radiant_actor`, `afterlife_actor`, `spiritual_conflict_side`
- display category: `buff`, `debuff`, `condition`, `environmental`, `mixed`
- visibility: `visible`, `hidden`, `gm_only`

### 6.2 Required semantic sections

| Section | Required checks |
| --- | --- |
| Identity | Current schema, client ID, active/suspended state |
| Realm/target | Exact target agrees with logical carrier and accepted realm |
| Display | Non-empty name/description; closed category/visibility |
| Source | Exact kind/source ID/definition key resolves to accepted definition |
| Components | Non-empty, unique component IDs, registered closed payloads |
| Lifetime | Exactly one closed mode and legal current evidence |
| Stacking | Exact source policy/key/max/current state |
| Triggers | Unique trigger IDs, legal events/components/priorities |
| Removal | Exact source-authorized closure/counteraction routes |
| Links | Exact unique companion/source/context links |
| Chronology | Positive accepted turns and matching latest client transition; optional client-derived `causalEventRef` on reaction-created instances |

Unknown root or section fields are errors. Registered component profiles own their own exact payload fields.
`causalEventRef` is immutable client-owned lineage evidence. It may be derived
from a sealed lifecycle/resource event but never appears in `effectChanges[]`,
`effectResolutionReceipts[]`, or `effectEventReports[]`.

## 7. Profile Examples

### 7.1 Characteristic modifier

```json
{
  "componentId": "component_001",
  "profile": "characteristic_modifier",
  "priority": 100,
  "payload": {
    "characteristic": "dexterity",
    "operation": "flat",
    "value": -2
  }
}
```

### 7.2 Periodic damage

```json
{
  "componentId": "component_001",
  "profile": "periodic_damage",
  "priority": 100,
  "payload": {
    "resource": "health",
    "amount": 3,
    "damageType": "bleeding",
    "floorPolicy": "registered_resource_floor"
  }
}
```

The matching trigger declares when this component executes. Display prose does not determine the amount, target resource, or interval.

## 8. Rejection Classes

Fail before publication for:

- missing/duplicate/unknown fields or wrong types;
- any GM-authored client identity/history/receipt/index field;
- incomplete source/target/display/component/lifetime/stack/removal section;
- unknown profile, event, action, characteristic, resource, or realm token;
- out-of-bound or non-finite numbers;
- pseudo-mechanics only in `description`, `reason`, or custom JSON;
- non-empty unsupported legacy effect carrier;
- direct canonical instance mutation without a matching accepted operation.

## 9. Positive Contract Example

The example is valid only when `wound_torn_side` exists, owns `bleeding-consequence`, permits the player target and parameter value, and the accepted event is current. The client creates the effect ID, resolves stacking/lifetime, writes the canonical instance/index, and never changes the wound.

## 10. Negative Contract Example

```json
{
  "operation": "apply",
  "effectId": "bleeding-forever",
  "target": "Игрок",
  "description": "-3 здоровья каждый ход",
  "duration": 999
}
```

This fails because identity, target, source definition, component, stack policy, lifetime, removal authority, and event evidence are missing or unauthorized. No field may be inferred from the prose.

## 2026-09-05 — exact skill scope extension from #1536

[#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
amends the completed common effect envelope so every `roll_modifier` uses this
three-field closed payload shape:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "skill", "skillId": "skill_lockpicking" }
}
```

The only closed scope variants are `{ "kind": "all" }` and exactly
`{ "kind": "skill", "skillId": "<canonical-id>" }`. Focused scope is legal only
for exactly `operations: ["skill_check"]`; names, aliases, arrays, multiple IDs,
missing scope, and extra fields reject. The client binds a focused selector to one
current usable canonical skill of the exact target before accepted publication.
Historical #1535 tasks remain complete; all new implementation, repair, cutover, and
verification work belongs to #1536.
