# Contract: Mortal Wound Diagnosis, Treatment, and Recovery

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Design boundary

Mortal wounds are authored for the current universe. The client validates a closed
mechanical language but does not provide a catalog of wound names, anatomy, medicines,
or complete cures. Antibiotics and bandages can be valid in one setting; healing
crystals and crystalline dust can be valid in another.

## Complete Mortal wound requirement

At creation, every Mortal wound must include:

- explicit physical/systemic/mental/other location profile and readable nature;
- severity I-IV and consequence set within its budget;
- visible/hidden symptoms, prognosis, and complication facts;
- at least one mechanically complete treatment route;
- a natural recovery policy and optional deterioration policy;
- reachable diagnosis evidence for every hidden route.

The client never derives any of these fields from the wound name or narrative prose.

## Treatment routes

All requirements in one route are conjunctive. Separate routes are alternatives.
Routes may be added later only through an accepted evidence-backed
`author_alternative_treatment` transition; prior history is immutable.

### Procedure

A procedure declares exact requirements, resource consumption policy, one registered
check formula, and ordered outcome bands before the roll is consumed.

Example shape:

```json
{
  "routeId": "clean_and_suture",
  "displayName": "Очистить и ушить рану",
  "mode": "procedure",
  "requirements": [
    { "kind": "item_quantity", "itemRef": "sterile_thread", "quantity": 1 },
    { "kind": "item_quantity", "itemRef": "antiseptic", "quantity": 1 },
    { "kind": "skill_tier", "capabilityRef": "field_medicine", "minimumTier": 2 },
    { "kind": "facility", "facilityRef": "clean_work_surface" }
  ],
  "resourcePolicy": {
    "consumeOn": ["success", "partial_success", "failed_attempt"],
    "refundOn": ["cancelled", "validation_failed", "rolled_back"]
  },
  "resolution": {
    "formulaKey": "mortal_wound_procedure_v1",
    "difficulty": 15,
    "rollSource": "accepted_d20"
  },
  "outcomes": [
    { "minimumMargin": 5, "result": ["stabilize", "reduce_one"] },
    { "minimumMargin": 0, "result": ["stabilize", "add_recovery:1"] },
    { "minimumMargin": -4, "result": ["no_improvement"] },
    { "maximumMargin": -5, "result": ["add_complication:irritation"] }
  ]
}
```

References above are illustrative semantic refs. They must resolve through exact item,
capability, actor, and location authority, not through display strings.

### Course

A course declares ordered doses/time milestones, exact resources per milestone,
permitted gaps, interruption behavior, and final transition. Starting a course creates
one client course identity. Each milestone has a unique operation key and cannot be
consumed twice.

```json
{
  "mode": "course",
  "course": {
    "milestones": [
      { "ordinal": 1, "afterSeconds": 0, "requirements": [], "result": "dose_recorded" },
      { "ordinal": 2, "afterSeconds": 28800, "requirements": [], "result": "dose_recorded" },
      { "ordinal": 3, "afterSeconds": 57600, "requirements": [], "result": "heal" }
    ],
    "maximumGapSeconds": 36000,
    "interruptionResult": "course_failed_no_regression"
  }
}
```

Elapsed time is game-world authority, not wall-clock time. Missing a step applies only
the declared interruption result and cannot consume future doses.

### Guaranteed route

A guaranteed route has no ad hoc GM success flag. It binds an exact already
materialized capability whose contract declares the wound domain/severity/result and
whose current state is valid.

```json
{
  "mode": "guaranteed",
  "capabilityRef": "exact-materialized-healing-source",
  "guaranteedResult": ["remove_complication:infection", "heal"]
}
```

The source may still require items, charges, a provider, facility, consent, or elapsed
time. If its authority becomes stale before commit, the route fails closed.

## Diagnosis paths and hidden routes

A hidden route is legal only when at least one diagnosis path can become reachable
through current canonical gameplay. The reachability proof may refer to a visible clue,
skill, provider, item, facility, location, or quest state. These are invalid:

- a hidden route revealed only by itself;
- a private GM fact with no attainable discovery path;
- a display-name-only item/provider/location reference;
- a path requiring an impossible or wrong-realm capability.

Diagnosis reveals only its declared facts. Tier/skill failure may return a readable
need for better expertise without leaking the hidden route.

## Exact requirements

The final attempt revalidates:

- item/resource identity, quantity, ownership, availability, and reservation;
- skill or capability identity/tier and active state;
- healer/provider identity, consent, reachability, and realm;
- facility/location identity and target presence;
- quest/effect/environment state;
- ordered prior milestones and game-time evidence;
- wound identity, severity, care state, complication set, and route version.

If any reference is stale, nothing is consumed. Removing a referenced item, skill,
effect, or facility during an active course uses the route's explicit interruption
policy or fails closed; no implicit migration is performed.

## Resource and item handling

The treatment service emits typed resource/item intents into the common
accepted-mechanics plan. It never writes inventory or resource ledgers directly.

1. Resolve exact requirements against the fresh snapshot.
2. Reserve the declared quantities.
3. Resolve the sealed check/course/guarantee.
4. Select one predeclared outcome and consumption policy.
5. Compose wound/effect/resource/item/history/output after-images.
6. Commit once; release/restore reservation on cancel, rejection, or rollback.

A valid failed procedure may consume supplies only when `consumeOn` says so. A repair
round is not a new attempt and never consumes a second set.

## Natural recovery

Each wound selects exactly one mode:

- `progressive`: a declared valid time/event cadence adds bounded progress;
- `requires_stabilization`: the cadence is blocked until stabilization;
- `no_natural_recovery`: only treatment improves the wound.

The policy declares clock source, cadence, threshold per step, blockers, overflow,
and allowed result. Optional deterioration declares exact unmet conditions, grace
period/cadence, and bounded complication/severity/death-contour result. The client owns
tick keys and progress arithmetic.

Prose such as "через несколько дней стало лучше" is not recovery evidence. A repeated
turn or clock event cannot tick twice.

## Treatment of another entity

The guided action lists Self first, then visible reachable targets. It never accepts a
raw NPC name or opaque ID. A conscious capable target must consent outside conflict. A
helpless friendly target may be treated when relationship/conflict authority permits.
An unwilling target requires a legal conflict action.

The selected hidden target binding is revalidated before resource reservation and once
again at commit. Duplicate visible names receive readable context without exposing
technical identity.

## Worked cross-setting acceptance pair

The implementation fixtures must prove at least two unrelated wounds without a wound
catalog:

1. Post-apocalyptic wound: contaminated laceration; bandage/antiseptic/antibiotic
   course or skilled debridement route.
2. Magical-world wound: crystalline burn; resonant crystal dust and a tuned healing
   focus or specialist ritual route.

Both use the same closed requirement/outcome primitives, exact authority, resource
transaction, history, and UI projection, while their names, fiction, items, skills,
and treatment logic are independently authored.
