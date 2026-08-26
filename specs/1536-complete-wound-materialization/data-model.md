# Data Model: Complete Wound Materialization and Healing

**Feature**: `1536-complete-wound-materialization`  
**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)  
**Schema policy**: Direct pre-alpha cutover to version 1; no legacy reader or migration.

## 1. Canonical topology

| Root | Authority | Purpose |
| --- | --- | --- |
| `game_state/wounds/wound_identity_index.json` | client-owned | One stable identity and one active carrier occurrence per wound |
| `game_state/wounds/wound_history.json` | client-owned | Append-only accepted transition/replay/terminal evidence |
| `game_state/wounds/wound_commands.json` | client-owned command staging | Exact materialization/treatment/diagnosis/recovery intents for one accepted request |
| `game_state/control/pending_wound_resolutions.json` | client-owned pending | Bounded GM construction/repair work, exact receipts, and immutable authority |
| `game_state/player/wounds.json` | composed client/GM semantic carrier | Mortal player active wounds |
| `game_state/npcs/npc_wounds.json` | composed client/GM semantic carrier | Named Mortal NPC active wounds, separate from effects |
| `game_state/combat/enemies.json` / `allies.json` | existing combatant authority | `activeWounds[]` on exact combatants or group members |
| `game_state/meta/afterlife_entity_profiles.json` | existing afterlife actor authority | `activeWounds[]`, standard arts, and optional healing service per persistent actor |
| `game_state/meta/guardian_abode_residents.json` | existing Shining roster authority | Visible resident primary role, including `healing_support`, bound to an exact actor profile |
| `game_state/meta/afterlife_spiritual_conflict_state.json` | existing conflict authority | Danger mode, sealed wound opportunities/results, and bounded defeat outcome |

Every path read or written by one accepted plan has an exact before-image and is
covered by the pending-turn snapshot. Wound roots join effect, resource, owner,
scheduler, journal, quest, and output roots in the same accepted-mechanics publication.

## 2. Common scalar types

### Exact identifiers

Client IDs and source references use normalized non-empty identifiers, ordinal
comparison, no leading/trailing whitespace, no control/format characters, and no
confusable fallback. The client owns permanent identities.

### Enumerations

| Type | Values |
| --- | --- |
| `realm` | `mortal_world`, `chaos_sea`, `shining_abode` |
| `domain` | `physical`, `spiritual` |
| `severity` | `I`, `II`, `III`, `IV` |
| `lifecycle` | `active`, `healed` |
| `careState` | `fresh`, `untreated`, `stabilized`, `recovering`, `healed` |
| `visibility` | `public`, `known_to_player`, `hidden`, `gm_only` |
| `locationKind` | `anatomical`, `systemic`, `mental`, `spiritual_axis`, `other` |
| `transitionKind` | `create`, `worsen`, `complicate`, `diagnose`, `stabilize`, `treat`, `recover`, `heal`, `legacy`, `archive` |
| `routeMode` | `procedure`, `course`, `guaranteed` |
| `recoveryMode` | `progressive`, `requires_stabilization`, `no_natural_recovery` |
| `conflictDangerMode` | `training`, `controlled`, `hostile`, `annihilation` |
| `compensationKind` | `ink_feathers`, `favor`, `debt`, `quest`, `allegiance`, `free_aid` |

Death and soul dissipation are terminal outcomes in their owning lifecycle contracts,
not wound severities.

## 3. Owner coordinate

```json
{
  "realm": "mortal_world",
  "ownerKind": "npc",
  "ownerId": "client-owned-exact-owner-id",
  "carrierPath": "game_state/npcs/npc_wounds.json"
}
```

Allowed `ownerKind` values are `player`, `npc`, `combatant`, `combatant_member`,
`player_soul`, `guardian`, `resident`, `radiant_actor`, and `afterlife_actor`.
`carrierPath` is derived from the kind/realm and must match it. Afterlife actor kinds
bind to one accepted profile identity. Mortal combatants bind to the exact current
combat root and promotion authority.

## 4. Wound identity index

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "woundId": "client-owned",
      "realm": "mortal_world",
      "ownerKind": "player",
      "ownerId": "player_current",
      "carrierPath": "game_state/player/wounds.json",
      "domain": "physical",
      "status": "active",
      "createdAtTurn": 42,
      "createdEventRef": "accepted-event-ref",
      "lastTransitionOrdinal": 3,
      "terminalTransitionId": null,
      "semanticFingerprint": "sha256:..."
    }
  ]
}
```

### Index invariants

- `woundId` is globally unique and appears at most once in active carriers.
- Every active entry resolves to exactly one carrier wound with matching owner, realm,
  domain, chronology, and fingerprint.
- A healed entry resolves to no active carrier and exactly one terminal history row.
- Status changes only `active -> healed`.
- An active identity cannot be retargeted or moved except through a typed accepted
  combatant-persistence transition preserving owner authority.
- Physical and spiritual domains never convert in place.

## 5. Wound history

```json
{
  "schemaVersion": 1,
  "nextOrdinal": 18,
  "transitions": [
    {
      "transitionId": "client-owned",
      "woundId": "client-owned",
      "ordinal": 17,
      "woundTransitionOrdinal": 3,
      "kind": "treat",
      "turn": 45,
      "eventRef": "accepted-event-ref",
      "operationKey": "retry-safe-key",
      "beforeFingerprint": "sha256:...",
      "afterFingerprint": "sha256:...",
      "sourceFingerprint": "sha256:...",
      "attemptId": "client-owned-attempt-id",
      "readableSummary": "Рана очищена и стабилизирована.",
      "terminal": false
    }
  ]
}
```

History is append-only, ordered, and client-authored. `operationKey` is unique for the
semantic event/attempt/course milestone/recovery cycle. The before/after fingerprint
chain must be contiguous for each wound. Terminal `heal`/`archive` evidence prevents a
replay from reopening or reapplying the wound. The player History projection reads a
sanitized subset; internal IDs and fingerprints are never projected.

## 6. Active carrier roots

### Mortal player

```json
{
  "schemaVersion": 1,
  "owner": {
    "realm": "mortal_world",
    "ownerKind": "player",
    "ownerId": "player_current"
  },
  "activeWounds": []
}
```

### Named NPCs

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "npcId": "exact-existing-npc-id",
      "activeWounds": []
    }
  ]
}
```

### Combatants and afterlife profiles

The existing object receives `activeWounds[]`; no parallel name-based object is
created. The wound index stores its exact owner coordinate. Spiritual-conflict
participants use the corresponding persistent afterlife profile; conflict state does
not become a second carrier.

## 7. Active wound envelope

```json
{
  "schemaVersion": 1,
  "woundId": "client-owned",
  "lifecycle": "active",
  "owner": {},
  "origin": {},
  "classification": {},
  "display": {},
  "severity": {},
  "care": {},
  "complications": [],
  "consequences": {},
  "treatment": {},
  "recovery": {},
  "relations": {},
  "lastTransition": {}
}
```

### 7.1 Origin

```json
{
  "eventRef": "accepted-event-ref",
  "sourceKind": "combat_action",
  "sourceId": "exact-materialized-source-id",
  "sourceState": "active",
  "createdAtTurn": 42,
  "createdAtCycleId": null,
  "opportunityId": "client-owned",
  "guaranteedTriggerId": null,
  "readableCause": "Удар осколком стекла во время обвала."
}
```

The exact source adapter proves realm, target, event, and capability state. A guarantee
must have been materialized before the trigger event and be present in the sealed
opportunity.

### 7.2 Classification and location

```json
{
  "domain": "physical",
  "woundType": "Рваная режущая травма",
  "locationProfile": {
    "kind": "anatomical",
    "readableLocus": "левая ладонь и основание большого пальца",
    "authorityKind": "body_part",
    "authorityRef": "left_hand",
    "affectedSide": "left"
  }
}
```

`authorityKind`, `authorityRef`, and `affectedSide` are optional closed adapter fields.
No value is inferred from `woundType`, name, or prose.

### 7.3 Display

```json
{
  "name": "Рваная рана ладони",
  "description": "Края раны расходятся при попытке сжать пальцы.",
  "visibleSymptoms": ["кровотечение", "боль при хвате"],
  "prognosis": "Без очистки вероятно воспаление.",
  "visibility": "known_to_player",
  "acquisitionNarration": "Осколок вспарывает ладонь..."
}
```

All text is bounded untrusted text. Acquisition narration is required on creation and
must make the accepted wound legible. The client also emits a deterministic short
notification.

### 7.4 Severity

```json
{
  "value": "II",
  "rank": 2,
  "maximumAtCreation": "II",
  "lastChangeEventRef": "accepted-event-ref"
}
```

`rank` is the derived numeric representation 1-4 and must agree with `value`. A
transition supplies explicit before/after severity; impossible improvement/worsening
and severity V fail closed.

### 7.5 Care

```json
{
  "state": "untreated",
  "stabilizedAtTurn": null,
  "activeCourseId": null,
  "lastAttemptId": null
}
```

Legal state progression is bounded by the transition table below. A complication can
be treated without silently changing severity or care state unless the route's sealed
outcome declares both transitions.

### 7.6 Complication

```json
{
  "complicationId": "client-owned-within-wound",
  "kind": "infection",
  "state": "active",
  "displayName": "Начавшееся воспаление",
  "treatmentDifficultyModifier": 2,
  "ownedEffectIds": ["client-owned-effect-id"],
  "visibility": "known_to_player"
}
```

Complication `kind` is one of `bleeding`, `infection`, `pain`, `impairment`,
`systemic_instability`, `spiritual_instability`, or `other`; this is a mechanical
primitive list, not a complete wound catalog. `treatmentDifficultyModifier` is an
integer 0-4 and participates only where the selected treatment resolver declares it.
Genre-specific manifestation remains in bounded display/prognosis text, and any
mechanical result still uses a registered consequence effect.

### 7.7 Consequences

```json
{
  "slotBudget": 2,
  "slotsUsed": 2,
  "entries": [
    {
      "slot": 1,
      "profileKey": "periodic_damage",
      "effectId": "client-owned-effect-id",
      "readableSummary": "Рана продолжает кровоточить."
    },
    {
      "slot": 2,
      "profileKey": "action_control",
      "effectId": "client-owned-effect-id",
      "readableSummary": "Хват левой рукой затруднён."
    }
  ]
}
```

Each entry maps to exactly one active #1535 effect whose exact source is this wound.
The effect may contain only the component composition allowed by the registered wound
consequence profile at the current severity. Mortal `slotsUsed <= rank`; spiritual
`slotsUsed == rank`. A Mortal wound with zero mechanical slots is valid only when it
has an active complication or a care/recovery constraint whose presence changes a
legal lifecycle transition; display text alone is never sufficient. Severity change
replaces the whole owned set atomically.

### 7.7.1 Closed Mortal component envelopes

Allowed generic effect profiles are `characteristic_modifier`, `roll_modifier`,
`resistance_modifier`, `periodic_damage`, `periodic_restore`, `action_control`, and
`event_reaction`; `wound_consequence` is a zero-slot source/display marker. One
independent characteristic, roll operation, resistance, periodic resource operation,
action, or worst-case reaction result consumes one slot.

| Per-slot limit | I | II | III | IV |
| --- | ---: | ---: | ---: | ---: |
| Absolute flat characteristic/resistance modifier | 1 | 2 | 3 | 4 |
| Absolute percent characteristic/resistance modifier | 5% | 10% | 20% | 30% |
| Periodic amount / accepted exact resource maximum | 5% | 10% | 20% | 30% |
| Absolute action cost modifier | 1 | 2 | 3 | 4 |
| `grant` | one action/slot | one | one | one |
| `restrict` | one action/slot | one | one | one |
| `forbid` | none | none | one non-safety action/slot | one non-safety action/slot |
| Reaction definition expansion | none | none | one fully budgeted | one fully budgeted |

Periodic values are quantum-aligned without exceeding the cap and execute no more than
once per accepted source event. Every worst-case spawned reaction component consumes
its own slot. `forbid` can target only `attack`, `cast`, or `movement`; `defend`,
`use_item`, `interact`, and `escape` cannot be forbidden. Aggregate restrictions
preserve inspection, communication, help, treatment, and exit.

### 7.7.2 Closed spiritual consequence profiles

| Profile | Axis | I | II | III | IV |
| --- | --- | --- | --- | --- | --- |
| `spiritual_roll_hindrance` | `rollMode` | one operation | one | one | one |
| `spiritual_action_cost_burden` | `actionCostAudit` | +1 | +1 | +2 | +3 |
| `spiritual_position_burden` | `conflictPosition` | one step | one | up to two | up to two |
| `spiritual_control_burden` | `controlState` | forbidden | one step | one | one |
| `spiritual_strain_burden` | side strain | forbidden | forbidden | one extra step | one extra step |
| `spiritual_tempo_burden` | `tempoAdvantage` | deny one gain | same | same | same |
| `spiritual_counter_burden` | `counterPayoff` | reduce one step | same | same | same |
| `spiritual_art_restriction` | one standard combat art | forbidden | forbidden | restrict | forbid |

Each row instance targets one declared non-safety operation/family and consumes one
slot. Duplicate profile/operation coordinates are invalid. Healing, inspection,
communication, help, withdrawal, surrender, negotiation, and dissipation choice cannot
be targeted. Eligible operation keys are exactly `pressure`, `counter`, `guard`,
`maneuver`, `binding`, `break_binding`, `force_binding`, `force_incarnation`,
`incarnation_resistance`, `champion_coordination`, and `recover_spiritual_power`.
Spiritual severity requires exactly `rank` legal entries.

### 7.8 Treatment

```json
{
  "diagnosisPaths": [],
  "routes": [],
  "knownRouteIds": [],
  "completedRouteIds": []
}
```

At least one route must be mechanically complete for every active Mortal wound.
Spiritual wounds use the standard healing resolver rather than bespoke routes, but may
include visible provider/access facts in projection.

### 7.9 Recovery

```json
{
  "mode": "requires_stabilization",
  "clockKind": "mortal_world_time",
  "cadence": 86400,
  "currentStepProgress": 0,
  "currentStepThreshold": 3,
  "lastTickKey": null,
  "blockers": ["not_stabilized"],
  "carryOverflow": true,
  "deteriorationPolicy": null
}
```

For spiritual wounds `clockKind=afterlife_safe_cycle`, cadence is one safe cycle,
threshold derives from current severity, and progress per cycle derives from the
owner's accepted Spiritual Healing tier.

### 7.10 Relations and last transition

```json
{
  "relations": {
    "priorWoundId": null,
    "legacyRefs": [],
    "independentEffectRefs": []
  },
  "lastTransition": {
    "transitionId": "client-owned",
    "ordinal": 1,
    "turn": 42,
    "kind": "create"
  }
}
```

Relations are provenance only. They grant no cross-entity lifecycle authority.

## 8. Wound opportunity

```json
{
  "schemaVersion": 1,
  "opportunityId": "client-owned",
  "sessionId": "exact-session",
  "requestId": "exact-request",
  "snapshotToken": "exact-snapshot",
  "eventRef": "accepted-event-ref",
  "owner": {},
  "domain": "spiritual",
  "profileKey": "afterlife_strain_transition_v1",
  "minimumSeverity": null,
  "maximumSeverity": "II",
  "guaranteedTrigger": null,
  "inputEvidence": {},
  "authorityFingerprint": "sha256:..."
}
```

`minimumSeverity` is null for ordinary opportunities. A guaranteed trigger supplies
its exact required result but remains subject to hard realm/mode caps; contradictory
source contracts fail before asking the GM to invent a compromise. The GM response is
one of:

```json
{ "opportunityRef": "...", "decision": "none" }
```

or

```json
{
  "opportunityRef": "...",
  "decision": "materialize",
  "woundRef": "gm-local-ref",
  "proposal": {}
}
```

The client allocates permanent identities after complete validation.

## 9. Treatment route model

### Diagnosis path

```json
{
  "diagnosisPathId": "gm-local-stable-within-wound",
  "visibility": "known_to_player",
  "requirements": [],
  "check": {},
  "reveals": ["route:clean_and_suture", "complication:infection"],
  "failurePolicy": "no_reveal"
}
```

A hidden route must be revealed by at least one diagnosis path whose requirements are
reachable from current canonical world capabilities, locations, quests, or provider
access. Cyclic hidden-only revelation is invalid.

### Route

```json
{
  "routeId": "clean_and_suture",
  "displayName": "Очистить и ушить рану",
  "visibility": "known_to_player",
  "mode": "procedure",
  "requirements": [],
  "resourcePolicy": {},
  "resolution": {},
  "outcomes": [],
  "interruption": null
}
```

### Requirement union

Each requirement has `kind`, exact reference/evidence fields, and a closed predicate.
Allowed kinds are:

- `item_quantity` and `resource_quantity`;
- `skill_tier`, `spiritual_art_tier`, and `source_capability`;
- `provider`, `consent`, and `relationship`;
- `facility`, `location`, and `reachable`;
- `quest_state` and `effect_state`;
- `elapsed_time`, `environment`, and `ordered_prior_step`.

All requirements within one route are AND. Different routes are OR. References are
re-resolved against the fresh snapshot immediately before commit. Removing a referenced
capability while a route/course is active fails closed unless the route declared an
interruption transition.

### Resource policy

```json
{
  "reserveBeforeResolution": true,
  "consumeOn": ["success", "partial_success", "failed_attempt"],
  "refundOn": ["cancelled", "validation_failed", "rolled_back"],
  "mutations": []
}
```

Resource/item mutations use their established typed authorities. A route cannot write
inventory or resource ledger values directly.

### Resolution and outcome bands

`procedure` binds a registered roll/check formula and closed ordered bands.
`course` binds an exact dose/milestone sequence and does not roll unless a declared
milestone uses a registered procedure. `guaranteed` binds a proven source capability.

Outcomes may compose only explicit bounded transitions: add/remove a complication,
stabilize, add recovery progress, reduce severity by one/two, heal, add an independent
legacy, or apply a declared deterioration consequence. No outcome may reopen a healed
wound or change owner/domain.

## 10. Spiritual conflict additions

### Conflict danger envelope

```json
{
  "dangerMode": "hostile",
  "escalatedFromTraining": false,
  "annihilationAuthority": null,
  "sideWoundState": {
    "player": {
      "newWoundId": null,
      "opportunityCount": 0
    },
    "opposition": {
      "newWoundId": null,
      "opportunityCount": 0
    }
  }
}
```

Each accepted strain transition records immutable audit inputs:
`harmfulMargin`, `appliedArtTier`, `targetResilienceTier`, previous/new strain,
extra jump steps, raw trauma pressure, formula severity, destination cap, danger cap,
and final maximum. These are client-computed or copied from validated conflict
authority; the GM cannot author them.

The destination cap is exactly `clear -> none`, `strained -> I`, `fractured -> II`,
`overwhelmed -> III`, and `broken -> IV`. A multi-rank jump uses the accepted final
destination and separately records `extraJumpSteps`; no caller substitutes a looser
table.

If `newWoundId` is already set, a later eligible result may produce a worsen
opportunity for that identity, not another create opportunity. Existing older wounds
require an explicit `retraumaWoundRef` in the accepted action.

### Defeat outcome

```json
{
  "outcomeId": "client-owned",
  "kind": "retreat_lock",
  "targetSide": "opposition",
  "scope": "same_conflict_goal",
  "expiresAtCycle": 73,
  "sourceEventRef": "accepted-resolution-event"
}
```

The registered bounded kind may vary by legal resolution, but non-training defeat must
prevent immediate repetition of the same aggression. Soul dissipation remains in the
existing independent terminal authority.

## 11. Standard spiritual arts

Every persistent afterlife combat profile exposes:

```json
{
  "standardArts": {
    "spiritual_resilience": { "tier": 0, "experience": 0 },
    "spiritual_healing": { "tier": 0, "experience": 0 }
  }
}
```

Tier is 0-5 and experience/progression follows the existing accepted afterlife entity
progression pipeline. `spiritual_resilience` supplies the target tier used by the wound
opportunity. `spiritual_healing` supplies diagnosis, active-healing gate/check, natural
recovery rate, and service capability. An omitted tier in a complete current profile is
invalid after cutover; current bootstrap/profile fixtures receive tier 0 explicitly.

## 12. Spiritual healing attempt

```json
{
  "attemptId": "client-owned",
  "operationKey": "retry-safe-key",
  "woundId": "exact-active-wound",
  "target": {},
  "healer": {},
  "healingTier": 3,
  "woundSeverity": "II",
  "mode": "safe_cycle",
  "roll": 14,
  "validatedModifiers": 0,
  "complicationModifier": 1,
  "total": 20,
  "difficulty": 15,
  "margin": 5,
  "resultBand": "reduce_one",
  "cycleId": "client-owned-safe-cycle",
  "compensationReceiptId": null
}
```

### Resolver rules

1. Revalidate exact active wound, healer/target/realm/reachability/consent.
2. Reject severity reduction if healer tier is below wound rank; diagnosis may still
   return readable insufficiency.
3. In conflict, prove a legal counterable action and final action-point cost 2-5.
4. Outside conflict, allocate exactly one safe cycle and enforce one active attempt for
   this wound in that cycle.
5. Seal or client-generate the d20 through the existing roll authority; do not accept a
   replacement during repair/retry.
6. Apply the result band, then ordinary safe-cycle recovery exactly once when relevant.
7. Publish attempt, wound/effect/resource/scheduler/history/output after-images together.

An insufficient-tier healer cannot use natural 20 to bypass the gate. Natural 1 means
no active improvement even when numeric total would otherwise pass.

## 13. Natural spiritual recovery

For one accepted safe cycle:

```text
pointsAdded = 1 + currentSpiritualHealingTier
threshold(I)   = 2
threshold(II)  = 4
threshold(III) = 6
threshold(IV)  = 8
```

When progress reaches the current step threshold, severity falls one step or I heals;
subtract the threshold and continue with overflow. One cycle may cross several steps
only when its bounded points allow it. `lastTickKey` prevents double application.
Worsening resets current-step progress to zero. Recovery is suppressed during unsafe
or active-conflict time and resumes on a later safe cycle.

Examples:

- Tier 0, IV: 8+6+4+2 = 20 safe cycles to full healing.
- Tier V, IV: 6 points/cycle crosses total 20 points in 4 safe cycles.

## 14. Healing service profile

```json
{
  "providerId": "exact-afterlife-actor-id",
  "realm": "chaos_sea",
  "locationRef": "elyara_lazaret",
  "visibility": "public",
  "availability": "available",
  "minimumRelationship": null,
  "priceMultiplierPercent": 100,
  "compensationKinds": ["ink_feathers", "favor", "quest", "free_aid"],
  "accessConditions": []
}
```

Capability is proven by the actor's `spiritual_healing` tier, not by this profile.
Multiplier is an integer 50-200. Quote is
`ceil(basePrice[severity] * multiplier / 100)` and is sealed before confirmation.
Currency receipt is produced by the existing Ink Feather authority. A negotiated
non-currency agreement is an accepted exact contract/quest/debt/favor reference and
feeds the same attempt resolver.

### Elyara invariant

The built-in `elyara` profile is normalized/validated to:

- `spiritual_healing.tier = 5` and cannot be downgraded;
- fixed discoverable Lazaret location;
- public available service, multiplier 100;
- supported negotiated compensation from her accepted character contract.

### Shining faction invariant

Each accepted Shining faction has at least one visible roster resident in
`game_state/meta/guardian_abode_residents.json` whose
`primaryRole.key = healing_support` and whose `spiritual_healing.tier` is 1-5. The
resident's exact actor identity resolves to its afterlife profile. That profile may
omit `healingServiceProfile` or declare restricted access. The role is ordinary
visible roster information.

## 15. Wound command staging

```json
{
  "schemaVersion": 1,
  "sessionId": "exact-session",
  "requestId": "exact-request",
  "snapshotToken": "exact-snapshot",
  "commands": [
    {
      "commandRef": "client-owned",
      "kind": "treat",
      "targetBinding": "client-owned-hidden-selection",
      "woundId": "exact-active-wound",
      "routeId": null,
      "providerBinding": null,
      "quotedCompensation": null,
      "authorityFingerprint": "sha256:..."
    }
  ]
}
```

Allowed command kinds are `materialize`, `worsen`, `diagnose`, `stabilize`, `treat`,
`recover`, and `heal`. Commands are one-shot, consumed only by the matching accepted
plan, and absent in ordinary read-only `/раны` queries. The GM cannot write the command
root.

## 16. Pending wound resolution

The pending root stores a bounded wave of at most 64 candidates and their exact safe
authority. It distinguishes `construct_wound`, `repair_wound`,
`author_alternative_treatment`, and `narrate_acquisition`. A candidate contains:

- safe readable event/target/realm context;
- allowed decision (`none` or materialize) and severity range;
- closed domain/profile/location/complication/consequence/treatment schema;
- immutable event/target/roll/guarantee/attempt/cycle fingerprints;
- offending semantic paths and expected bounds for repair;
- preserved non-offending proposal fields;
- receipt identity and transcript prefix.

It omits permanent client IDs, hidden target/private NPC data, resource seals, internal
owner coordinates, and unrelated response content. A receipt is accepted once. A
changed snapshot or semantic authority invalidates the pending wave.

## 17. Transition state machine

| Transition | Required before | Legal after | Notes |
| --- | --- | --- | --- |
| `create` | no identity; valid opportunity | active fresh/untreated I-IV | Creates index, wound, history, effects atomically |
| `worsen` | active I-III or same-conflict wound | higher active severity, max IV | Resets current-step recovery; replaces effects |
| `complicate` | active wound | same or declared worse severity | Adds registered complication/effect only once |
| `diagnose` | active, discoverable path | same mechanics plus newly known facts | Never heals by itself |
| `stabilize` | active unstabilized | stabilized | May remove only declared complication/effect |
| `treat` | active; complete route/gate | sealed result | Attempt may fail and still be terminal/charged |
| `recover` | active; due valid clock/cycle | progress or lower severity/healed | Idempotent by tick key |
| `heal` | active severity I with sufficient result | healed and removed from carrier | Ends owned effects; appends terminal history |
| `legacy` | accepted heal or prior healed history | independent entity plus relation | Never keeps wound mechanically active |
| `archive` | healed terminal history | history-only projection | Cannot reopen or delete replay evidence |

No transition may lower severity without a treatment/recovery outcome, increase it
without an eligible event/deterioration/re-trauma, skip owner/realm validation, or
write an effect/resource/history root directly.

## 18. Player projection model

The shared application service returns typed player-facing blocks, not canonical JSON.

### Active list item

- display name and Roman severity;
- readable locus/cause known to the player;
- short visible symptoms/consequences;
- care/recovery summary;
- whether detail/treatment/diagnosis/help actions are currently available.

### Detail

- visible origin, location, symptoms, prognosis, linked effect summaries;
- current care and recovery progress in readable terms;
- known routes, requirements, risks, provider/access/quoted cost when proven;
- guided `Лечить` action.

### History

- healed wound name, readable origin, final outcome, cosmetic scar/legacy summary, and
  readable chronology;
- never present in the primary active list;
- no IDs, fingerprints, hidden routes/symptoms, private NPC facts, or validator terms.

### Target choice

The choice carries hidden exact binding but displays Self first and then reachable
visible entities. Duplicate names receive location/role/appearance context. Confirming
the choice revalidates target, wound, consent, provider, resources, and reachability.

## 19. Cross-root invariants

1. Every active index entry has exactly one active carrier occurrence and vice versa.
2. Every wound-owned active effect has one matching active wound and reciprocal slot;
   every reciprocal slot resolves to that exact effect.
3. Wound/effect target, realm, source, and visibility authority agree.
4. Effect removal alone never mutates the wound.
5. Full healing removes all and only that wound's effects from active carriers.
6. Healed wounds have no active carrier occurrence and remain in terminal history.
7. Independent effects, including Saref `memory_suppression`, survive unrelated wound
   transitions.
8. Every treatment/resource/provider/item/art/location reference resolves in the fresh
   canonical snapshot at commit.
9. Attempt, course milestone, recovery cycle, opportunity, and transition keys are
   globally replay-safe in their scopes.
10. Console and browser projection from the same canonical snapshot has equivalent
    actions, costs, results, and errors.
11. Invalid siblings cause no wound/effect/resource/scheduler/output after-image to
    publish.
12. All canonical text that reaches a player renderer is escaped/sanitized.

## 20. Version-1 bounds

| Collection/value | Limit |
| --- | ---: |
| Active wounds across all carriers | 2,000 |
| History transitions | 20,000 |
| Wound commands per accepted turn | 128 |
| Pending wound candidates | 64 |
| Treatment routes per wound | 32 |
| Diagnosis paths per wound | 32 |
| Requirements per route/path | 16 |
| Complications per wound | 16 |
| Consequences per wound | 4 |
| Wound transitions composed per turn | 32 |
| Readable text field | repository-standard bounded GM text limit |

Planning and agreement use indexed `woundId`, owner coordinate, effect ID, operation
key, and route ID maps. Limits fail closed before expensive cross-root composition.
