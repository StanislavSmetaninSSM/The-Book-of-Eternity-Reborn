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
chain must be contiguous for each wound. Exactly one terminal `heal` evidence row
prevents a replay from reopening or reapplying the wound. Later `legacy` and `archive`
rows are nonterminal audit/projection records that preserve the sealed terminal wound
fingerprint. The player History projection reads a sanitized subset; internal IDs and
fingerprints are never projected.

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

`rank` is the derived numeric representation 1-4 and must agree with `value`.
`maximumAtCreation` preserves the maximum severity sealed by the originating
opportunity; it may be higher than the severity selected by the GM and never changes
on later transitions. A transition supplies explicit before/after severity; impossible
improvement/worsening and severity V fail closed.

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

The nested #1535 definition body in this overview is abbreviated with empty objects and
arrays for readability; canonical state contains every required validated field and a
non-empty registered component set.

```json
{
  "slotBudget": 2,
  "slotsUsed": 2,
  "ownedEffectSources": {
    "definitions": [
      {
        "schemaVersion": 1,
        "definitionKey": "wound_bleeding_root",
        "display": {},
        "allowedRealms": ["mortal_world"],
        "allowedTargetKinds": ["player"],
        "components": [],
        "parameterBounds": {},
        "stacking": {},
        "lifetime": {
          "mode": "source_bound",
          "activePredicate": "active",
          "onSourceLoss": "expire"
        },
        "triggers": [],
        "removal": {},
        "links": [
          { "kind": "wound", "targetId": "client-owned-wound-id", "role": "source" }
        ]
      }
    ],
    "rootBindings": [
      {
        "effectId": "client-owned-effect-id",
        "definitionKey": "wound_bleeding_root"
      }
    ]
  },
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

`ownedEffectSources` is mandatory in schema version 1, including when both arrays are
empty. This is a direct technical cutover: the client does not accept or migrate a
slot-only wound shape. The object itself, either array, and every array element are
non-null. A legal non-mechanical Mortal wound persists exact empty arrays and produces a
detached zero-operation typed effect batch; it finalizes with an empty result map, no
allocated effect identity, and no changed effect after-image.

`definitions` is the complete detached #1535 source-definition graph owned by this
wound, not merely definitions that have an active effect instance now. Every definition
has an exact/confusable-unique `definitionKey`, the exact wound source link shown above,
the wound's realm/target authority, and one legal #1535 lifetime admitted by the wound
severity envelope. A `source_bound` lifetime, when selected, must bind the exact wound;
other accepted lifetimes may expire or suspend an effect without changing the wound. Every
`apply_definition` edge resolves inside this array. Every definition without a direct
root binding is reachable from exactly one directly bound definition; orphan unbound
definitions fail closed. A directly bound definition may also be an
`apply_definition` target permitted by #1535 only when that target uses the exact legal
`replace` stacking policy and belongs to the same reconstructed root-ownership domain as
the producing definition. That later application retires the prior target identity when
one is present, receives a fresh runtime effect identity, and does not create an
additional canonical root binding for the descendant instance. `independent`, `stack`,
`refresh`, or `merge` on a root-bound reaction target is invalid. Cycles, cross-wound
links, cross-ownership-domain edges, and unresolved edges fail closed. The graph
contains at most five definitions.

Every wound-owned definition has an exact/confusable-unique `stacking.stackKey` within
the complete graph and exact `stacking.maxStacks = 1`. Remaining stacking fields obey
the ordinary #1535 policy combinations (`independent` uses `atMaximum = no_change`).
This bounds the active/suspended group to at most one instance per definition while
retaining legal refresh, replace, merge, and reaction reuse behavior.

This bound is semantic, not arbitrary: every complete definition has at least one
component. The `event_reaction` producer consumes one consequence slot and every
flattened non-marker component of its reachable leaf consumes its own slot. Therefore a
severity-IV wound with one mechanical downstream-only definition can have at most the
reaction root, two other one-slot mechanical roots, that leaf, and one zero-slot marker:
five definitions total. Four ordinary one-slot roots plus a reaction producer and a
mechanical leaf require at least six definitions and at least six slots before any
marker, so they fail the severity envelope rather than expanding the graph bound. If the
leaf is the sole zero-slot marker, no separate marker root is legal. Nested wound-owned
reaction expansion is forbidden by the version-1 envelope. A zero-edge graph is legal;
at most one `apply_definition` edge and reachable leaf are permitted. When present, that
edge has exact #1535 `maxExpansion = 2`: one reaction plus one reachable leaf
definition. This remains one semantic wound expansion.

`rootBindings` contains only effects materialized directly by wound creation,
complication, or severity rematerialization. Effect IDs and bound definition keys are
both exact/confusable-unique; every binding maps one client-owned `effectId` to one
present graph definition. An unbound downstream definition referenced through
`apply_definition` has no root binding and receives no reserved effect ID; the normal
effect runtime allocates its identity only when the reaction is accepted. Response-local
`LocalWoundRef` and root `applicationRef` values never enter canonical wound state. The
wound planner never allocates a permanent effect ID; the accepted effect plan returns
the exact `applicationRef -> effectId` results used by finalization. There are at most
five root bindings.

An ephemeral `WoundRootLineageAuthority` is reconstructed from `rootBindings` and the
active complications' pairwise-disjoint `ownedEffectIds`. Each root is assigned exactly
one ownership domain: `base_wound` or its exact `complicationId`. Same-turn root
applications carry the corresponding domain before effect IDs exist. The authority is
not a new persisted wound field, but its ordered rows participate in the accepted source
export seal. A reaction child inherits its producer's domain. Before mutation, the
effect planner rejects stack/refresh/merge/replace behavior whose producer and incumbent
belong to different domains; #1535 has no inverse contribution capable of undoing half
of such a cross-domain mutation during later selective complication removal.

Every root-bound definition has exact empty `{}` `parameterBounds` and its root
application uses exact empty parameters; authored values are frozen in the definition's
components. An unbound downstream definition may expose bounded parameters only when
the persisted `apply_definition` component carries the exact future application
parameters. A bound definition reused as a reaction target therefore also receives
empty parameters.

Canonical wound serialization writes definitions in exact ordinal `definitionKey`
order, root bindings by `effectId` and then `definitionKey`, and consequence entries by
slot. Complete definitions use the shared recursive canonical object writer; nested
arrays retain their #1535-defined semantic order. The whole-wound semantic fingerprint
covers the full graph, root bindings, and entries. Canonical state persists no separate
source fingerprint; prepared accepted-turn plans carry their own immutable
`SourceExportFingerprint`.

Every `complication.ownedEffectIds` value resolves to a root binding; complications do
not list reaction-descendant IDs or duplicate slot truth, and their owned root sets are
pairwise disjoint. A direct root create transition has empty `sourceEffectIds`; a
reaction-created child's first `create` transition records exactly its producing effect
ID. Later replace/stack/refresh transition arrays are not ownership-parent edges.
Removing a complication
starts from its declared roots even when a root is terminal, traverses only validated
first-`create` causal parent-to-child identity edges inside the same exact wound source,
definition graph, and ownership domain, and terminates active/suspended members of that
closure. Every source-group identity is visited at most once; cycles, more than one
causal `sourceEffectId` on a child's first `create`, duplicate same-kind causal evidence,
foreign-source edges, or graph-missing definition keys fail closed. A legal replacement
may separately record old-to-new succession in `replace` transition evidence; that
lifecycle edge is not a second causal ownership parent and is ignored by selective
ownership traversal. Other roots/descendants owned by the still-active wound remain
unchanged.

After selective complication resolution, the complication, its declared root bindings,
and all slot entries pointing to those roots are absent and `slotsUsed` is recomputed.
Definitions are pruned only when no remaining root binding can reach them; a still-bound
or still-reachable definition remains. If an active/suspended same-source effect uses a
definition that would be pruned, the transition fails as a lineage disagreement. The
global effect identity index/history retains terminal provenance, while the active wound
contains no stale complication mechanics. The remaining graph and recomputed slots must
pass the current severity envelope or be paired with a legal same-plan severity/
replacement-consequence transition.

Each slot entry maps to exactly one root binding whose effect's exact source is this
wound. One root may own several slots. A directly materialized `wound_consequence`
marker is the sole legal root binding with zero slots. A marker may instead be a
reaction-only descendant; either way there is at most one marker definition across the
whole graph. Any later reaction-created instance has no reciprocal slot or additional
root binding: its worst-case
mechanical components were already charged to the originating reaction root. All other
roots have at least one slot. The effect may contain only the component composition
allowed by the registered wound consequence profile at the current severity. Mortal
`slotsUsed <= rank`; spiritual `slotsUsed == rank`. A Mortal wound with zero mechanical
slots is valid only when it has an active complication or a care/recovery constraint
whose presence changes a legal lifecycle transition; display text alone is never
sufficient. Direct roots have exact/confusable-unique logical stack coordinates and each
must create a new effect identity. Severity change first terminates the whole old active
wound-source group and then replaces the complete graph and root-binding set atomically;
it does not preserve effect identity for unchanged definitions.

### 7.7.1 Closed Mortal component envelopes

Allowed generic effect profiles are `characteristic_modifier`, `roll_modifier`,
`resistance_modifier`, `periodic_damage`, `periodic_restore`, `action_control`, and
`event_reaction`; `wound_consequence` is a zero-slot source/display marker, with at
most one marker across the wound-owned effect set. One independent characteristic,
roll operation, resistance, periodic resource operation, action, or worst-case reaction
result consumes one slot.

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

For scalar modifiers, a non-null cap is evaluated exactly as runtime evaluates it:
apply `minimum`, then `maximum`, and compare the resulting nonzero modifier to the
severity limit. Both endpoints use the exact decimal contract. A larger finite raw
value is legal when that runtime result is inside the envelope; an unrepresentable raw
number fails closed and never disappears from slot accounting.

Periodic values are quantum-aligned without exceeding the cap and execute no more than
once per accepted source event. Every worst-case spawned reaction component consumes
its own slot. The one-expansion-per-wound limit counts only wound-owned expansions;
bounded independent effect siblings remain outside wound slot and expansion budgets.
Before ownership classification, each raw effect proposal has a separate structural
work bound of 64 flattened reaction-expansion rows. That bound does not turn into a
wound budget and does not discard bounded independent rows.
`forbid` can target only `attack`, `cast`, or `movement`; `defend`,
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
The art-restriction subset is the ten combat arts above excluding
`force_incarnation`; it is a dedicated closed operation set rather than a projection of
the extensible standard-art registry, so later noncombat and healing arts remain
ineligible.
Spiritual severity requires exactly `rank` legal entries.

All eight profile identifiers above are deterministic registered #1535 component
primitives with only the `profile_specific` merge reducer and the exact closed payload
`{ operation, axis, magnitude }`. The common registry owns
closed payload/type/domain validation and deterministic execution/projection metadata;
the wound envelope owns the severity table, exact magnitude at that severity, slot
budget, coordinate uniqueness, and safe-exit checks. They are not represented by
`afterlife_combat_condition`. The exact ordinal operation set, fixed axis, and JSON
magnitude domain for each profile are defined by the common-registry table in
`contracts/wound-effects-and-atomicity.md`; no name, prose, numeric-string, fractional,
null, object, or array inference is permitted.

A definition containing one of these profiles has only afterlife allowed realms and a
non-empty subset of persistent actor target kinds: `player` (for `player_soul` in an
afterlife realm), `guardian`, `resident`, `radiant_actor`, or `afterlife_actor`. It
forbids `spiritual_conflict_side`, contains exactly one wound link with role `source`,
and otherwise uses an ordinary valid #1535 lifetime. The normal persistent form is
`source_bound` with `activePredicate = active` and `onSourceLoss = expire`; independent
effect expiry or suppression does not heal the wound or erase its canonical source
graph/root binding.

The payload remains owner-relative in canonical state. A typed current-conflict
projector resolves the exact persistent target actor to exactly one accepted conflict
participant and side, then derives a private `SpiritualWoundConflictContribution`.
`actionCostAudit` resolves to its player/opposition branch and `sideStrain` resolves to
`playerSideStrain`/`oppositionSideStrain`; other axes retain their registered name.
Absent, duplicate, wrong-realm, or side-ambiguous membership fails closed. The derived
contribution is never stored as an effect, wound, or combat condition, and
`combatConditions[]` remains byte-identical when only wound contributions change.

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

### Accepted wound-effect handoff

The in-memory handoff is closed and typed:

```text
WoundPreparedPlan
  WoundPreparationFingerprint
  EffectOperationBatches[]

WoundEffectOperationBatch
  LocalWoundRef
  EffectSourceExport (Materializable = false)
  RootApplications[]
  TerminalOperations[]
  RootLineageAuthority[]
  SourceExportFingerprint

WoundRootEffectApplication
  ApplicationRef
  MechanicsOrdinal
  OperationOrdinal
  DefinitionKey
  TargetSelector
  SourceSelector
  Parameters = {}
  SlotBindings[]
  ExpectedComponentCount
  ExpectedMaterializationFingerprint
  OwnershipDomain = base_wound | ComplicationId
  CausalEventRef

WoundRootEffectResult
  ApplicationRef
  Disposition = created_new_identity
  EffectId
  CreateTransitionId
  CreatedEventRef
  CausalEventRef
  SourceKey
  TargetKey
  CarrierCoordinate
  Materialization
    SlotBindings[]
    ComponentCount
    MaterializationFingerprint

WoundRootLineageAuthorityRow
  RootSelector = exactly one of ApplicationRef | EffectId
  DefinitionKey
  OwnershipDomain = base_wound | ComplicationId
```

`WoundAcceptedTurnPlanner.Prepare` allocates the wound identity but no effect identity.
For a new same-turn wound, `SourceSelector` contains its exact `sourceRef` and forbids
`sourceId`; an existing stable wound transition uses `sourceId`. The owner adapter maps
`player_soul -> player`, `combatant_member -> combatant`, and all other supported owner
kinds to the identically named #1535 target kind. A same-turn target uses `targetRef`;
an already stable target uses `targetId`.

The wound source is never publicly materializable: ordinary GM `effectChanges[]` cannot
apply any root or downstream definition. Only the fingerprinted typed batch grants
one-shot internal authority for its exact root tuple; later downstream materialization
is authorized only by the existing sealed #1535 reaction executor.

`ApplicationRef` is exact/confusable unique across the accepted turn and is never
canonical state. The effect composer derives a different replay-safe internal event
reference for every terminal and root application through
`CreateWoundEffectOperationEventRef(parentEventRef, mechanicsOrdinal,
operationOrdinal, operationKind)`. Its client-owned value is
`wound_effect:sha256:<canonical-tuple-hash>`, separate from public accepted-turn
authorities. For a root, that derived operation reference becomes exact
`chronology.createdEventRef`; the accepted wound event remains separately preserved as
exact client-owned `chronology.causalEventRef`. A terminal transition likewise uses its
own derived transition event and the same accepted wound event as causal authority. The
two fields may not be swapped, omitted, or collapsed merely because several operations
share one wound event. The effect planner processes terminal operations first,
allocates random opaque IDs only for newly created roots, and returns one detached
result per application. Wound finalization requires exact set/cardinality agreement and
also compares disposition, transition ID, both event refs, source/target keys, and
carrier coordinate, ordered slots, component count, and the materialization fingerprint
before persisting only `{effectId, definitionKey}` root bindings. It independently
recomputes the result fingerprint from the exact created active-effect after-image and
the prepared root parameters; comparing two carried fingerprint strings is insufficient.

`ExpectedMaterializationFingerprint` and result `MaterializationFingerprint` use domain
`book_of_eternity.wound.effect_materialization`, version `1`. In exact order, the
length-prefixed UTF-8 SHA-256 writer hashes domain, fingerprint version, exact source
realm/kind/source ID/definition key, positive definition/effect schema version, recursively
canonical compact parameters, component count, and each fully bound component's
zero-based original ordinal plus recursively canonical compact JSON. Every field is
encoded as `<UTF8-byte-count>:<field>`; output is lowercase `sha256:` plus 64 lowercase
hexadecimal characters. Recursive canonicalization sorts object keys ordinally and
preserves every array order. Version-1 direct-root parameters are exact `{}` but remain
an explicit hash input. The expected value is derived from the detached source
definition; the actual value is derived from the created effect after parameter binding.
It is internal handoff authority and is not persisted in canonical wounds or player
output. The consequence catalog remains the severity/power-policy authority.

`SourceExportFingerprint` is the exact per-source pre-effect seal. It covers source
schema/kind, permanent ID, optional local ref, state, non-materializable policy, realm,
owner, accepted causal event, the recursively canonical complete definitions, ordered
root-lineage-authority rows, and ordered root applications including application refs,
operation ordinals/kinds, target/source selectors, empty parameters, slot bindings, and
expected component counts/materialization fingerprints, and ownership domains. No newly
allocated root-result effect ID is an input to a new root
application or same-turn source export. Existing canonical root effect IDs may appear
only in terminal operations or root-lineage authority and are covered by the
`WoundPreparationFingerprint` or `SourceExportFingerprint`, respectively.

The staged contour uses non-interchangeable, internally computed seals:

1. `WoundPreparationFingerprint` covers the accepted binding/input authority, allocated
   wound/complication/transition IDs, detached wound drafts, local refs, every
   `SourceExportFingerprint`, and the ordered terminal-operation set.
2. `EffectInputFingerprint` covers the complete ordinary effect input plus the typed
   wound batches, their upstream seals, and every derived operation/causal event pair.
3. `EffectAcceptedTurnPlanFingerprint` covers that exact effect input, every allocated
   effect/transition ID, exact application and termination result maps including each
   actual component count/materialization fingerprint, and all effect carrier/identity
   before- and after-images from which that fingerprint can be recomputed.
4. `WoundFinalPlanFingerprint` covers the wound-preparation and effect-plan seals plus
   final canonical wounds, root bindings, entries, complications, identity/history/
   pending/scheduler/output after-images, and exact wound intents.
5. The common `AcceptedMechanicsPlan.PreparedPlanFingerprint` covers the common input
   fingerprint and authority fingerprints, every subordinate seal above, finalized
   wound/effect/resource after-images and receipts, touched/consumed paths, pending
   state, scheduler outcomes, and player-output bindings.

No constructor or caller supplies or self-asserts `PreparedPlanFingerprint`.
`AcceptedMechanicsPlanner` computes it only after semantic cross-stage agreement, and
`AcceptedMechanicsPlanCache` independently recomputes it from the detached returned plan
before validation and again on take/peek for publication. A source export, result map,
derived event, subordinate after-image, or final wound binding taken from another valid
plan therefore invalidates the common plan instead of receiving a fresh caller-chosen
seal.

The same recomputation rule applies at every earlier boundary: the effect composer
recomputes each source-export and wound-preparation seal from detached fields, the effect
planner recomputes its complete input seal and every expected/actual materialization
fingerprint, and wound finalization recomputes the effect-plan seal plus each created
effect's materialization fingerprint before consuming any result. A downstream stage
never treats a fingerprint property alone as proof of its payload.

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
2. Every wound-owned active effect has one matching active wound, exact source
   coordinate, and `definitionKey` in that wound's persisted graph. Directly
   materialized roots have one reciprocal `rootBinding`; slot-consuming roots have one
   or more reciprocal entries. The sole zero-slot marker and later reaction descendants
   do not invent slots.
3. Wound/effect target, realm, source, and visibility authority agree.
4. Effect removal alone never mutates the wound.
5. Full healing uses the indexed exact `(realm, wound, woundId, definitionKey)` source
   coordinates grouped by `(realm, wound, woundId)` and the persisted definition graph
   to remove all and only that wound's active root and descendant effects from active
   carriers.
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
| Persisted wound-owned effect definitions per wound | 5 |
| Persisted wound-owned root effect bindings per wound | 5 |
| Persisted pre-turn wound-owned definitions across 2,000 active wounds | 10,000 |
| Persisted pre-turn wound-owned root bindings across 2,000 active wounds | 10,000 |
| Active/suspended wound-owned effect instances across active wounds | 10,000 |
| Same-turn wound-owned definitions across 32 transitions | 160 |
| Same-turn wound-owned root applications across 32 transitions | 160 |
| Effect proposals supplied to one wound-envelope validation | 128 |
| Components or cadence entries per effect proposal | 64 |
| Flattened reaction-expansion rows per effect proposal (structural work bound) | 64 |
| Wound-owned flattened reaction expansions per wound (semantic budget) | 1 |
| Resource-bound evidence entries per wound envelope | 128 |
| Wound transitions composed per turn | 32 |
| Readable text field | repository-standard bounded GM text limit |

Planning and agreement use indexed `woundId`, owner coordinate, effect ID, operation
key, and route ID maps. Limits fail closed before expensive cross-root composition.
