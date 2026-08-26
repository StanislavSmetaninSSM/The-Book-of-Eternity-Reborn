# Contract: Wound Consequences, Effects, and Atomic Composition

**Feature**: `1536-complete-wound-materialization`  
**Depends on**: completed effect materialization [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## Core separation

A wound is independently treatable canonical state. Its directly materialized
consequences and any later reaction descendants are effects whose exact source is that
wound. The effect engine applies mechanics; the wound kernel owns severity, care,
treatment, recovery, healing, the bounded complete source-definition graph, and the
accepted root-materialization bindings.

These statements are invariants:

- removing, dispelling, suppressing, consuming, or expiring an effect does not heal,
  worsen, stabilize, retarget, delete, or reopen the wound;
- healing a wound ends all and only effects owned by that wound;
- unrelated effects survive every wound transition;
- a lasting post-healing mechanic becomes an independent entity with provenance, not a
  wound-owned effect on a terminal wound.

## Source export

During preparation, each provisional wound exports one exact accepted source:

```json
{
  "sourceKind": "wound",
  "sourceId": "client-provisional-wound-id",
  "sourceRef": "response-local-wound-ref",
  "sourceState": "active",
  "materializable": false,
  "realm": "mortal_world",
  "ownerKind": "player",
  "ownerId": "player_current",
  "eventRef": "accepted-event-ref",
  "sourceExportFingerprint": "sha256:...",
  "definitions": ["complete detached #1535 definition graph"],
  "rootApplications": [
    {
      "applicationRef": "response-local-root-application-ref",
      "mechanicsOrdinal": 1,
      "operationOrdinal": 1,
      "definitionKey": "wound_bleeding_root",
      "target": { "kind": "player", "targetId": "player_current" },
      "source": {
        "kind": "wound",
        "sourceRef": "response-local-wound-ref",
        "definitionKey": "wound_bleeding_root"
      },
      "parameters": {},
      "slotBindings": [1],
      "expectedComponentCount": 1,
      "expectedMaterializationFingerprint": "sha256:...",
      "ownershipDomain": "base_wound",
      "causalEventRef": "accepted-event-ref"
    }
  ]
}
```

This export enters the existing accepted-plan effect source catalog. No loose scan of
`playerWoundChanges`, no wound-name inference, and no `duration=999` legacy reference is
accepted.

The complete graph and root-application batch are sealed before the effect planner runs.
No effect ID is allocated by wound preparation. `applicationRef` is an opaque handoff
correlation key, not an effect identity. For a newly created same-turn wound, the exact
#1535 source selector uses `sourceRef`; using its not-yet-published `sourceId` is rejected.
A root's expected component count and
`book_of_eternity.wound.effect_materialization` version-1 fingerprint are internally
derived from its exact source coordinate, schema, parameters, and ordered fully bound
components; they are not caller assertions or effect identities.
A definition referenced only by `apply_definition` has no root application and no
reserved ID. The accepted effect runtime allocates a permanent opaque identity only if
the corresponding root or later reaction is actually materialized. Response-local refs
exist only in the prepared handoff.

`WoundAcceptedTurnPlanner.Prepare` emits an immutable typed
`WoundEffectOperationBatch`; it does not append forgeable GM `effectChanges[]` JSON.
`EffectAcceptedTurnInputComposer` admits that batch beside ordinary GM effect commands,
derives one exact/confusable-unique internal event reference per terminal/root operation
with `CreateWoundEffectOperationEventRef(parentEventRef, mechanicsOrdinal,
operationOrdinal, operationKind)`. The value is a client-owned
`wound_effect:sha256:<canonical-tuple-hash>` namespace and cannot collide with public
`turn_<n>[_effect_<n>]` authorities. The composer passes it to the ordinary effect
planner as the exact root `createdEventRef`; the accepted wound event is separately
passed as exact client-owned `causalEventRef`. Terminal transitions receive the same
two-field separation. Sharing one causal wound event never permits two operations to
share a created/transition event. `EffectAcceptedTurnPlan` alone allocates permanent
effect IDs and returns a detached typed `applicationRef -> effectId` result for every
wound root. Finalization
rejects a missing, duplicate, confusable, non-creating, or extra result and requires
exact agreement for the create transition ID, both event refs, source/target keys, and
carrier coordinate. One wound event may therefore materialize several roots without
reusing one #1535 operation event.

Every canonical/same-turn wound `EffectSourceExport` has
`Materializable = false`. Ordinary GM `effectChanges[].operation=apply` therefore cannot
materialize a wound root or downstream definition by naming its source. The typed wound
batch carries the only one-shot internal root-materialization authority. The effect
planner resolves its exact allowlisted `(SourceExportFingerprint, applicationRef,
definitionKey, target, source selector)` through canonical source binding plus ordinary
parameter/target validation; it never turns the whole wound source into public apply
authority. Later descendants remain reachable only through the sealed #1535 reaction
executor.

After finalization, canonical `consequences.ownedEffectSources` persists the complete
definition graph and exact `{ effectId, definitionKey }` root bindings. Both arrays are
mandatory even when empty; `definitions` and `rootBindings` each have a maximum of five
entries and contain no local refs. The object, arrays, and elements are non-null. A legal
non-mechanical Mortal wound exports a sealed zero-operation batch and empty source graph;
the effect stage returns an empty result without allocating an ID or publishing a changed
effect after-image, and wound finalization persists both empty arrays.
Every graph definition carries the exact wound source link. Every definition without a
direct root binding is reachable from exactly one directly bound definition. A bound
definition may also be an `apply_definition` target; its later effect instance is a
runtime descendant and does not receive another root binding only when that definition
uses the exact legal `replace` policy and belongs to the producer's reconstructed
root-ownership domain. Other stacking policies on a root-bound target and cross-domain
edges fail closed alongside unresolved, orphan unbound, cyclic, cross-wound, duplicate,
or confusable graphs.

The five-definition ceiling follows from slot accounting, not a hidden catalog limit.
An `event_reaction` producer consumes one slot and every flattened non-marker component
of its leaf consumes another. Thus the legal severity-IV maximum with a mechanical
downstream-only leaf is the reaction root, two other one-slot roots, the leaf, and the
one zero-slot marker. Four ordinary one-slot roots plus a reaction and mechanical leaf
already exceed four slots before a marker is considered.

Canonical serialization sorts definitions by exact ordinal `definitionKey`, root
bindings by `effectId` then `definitionKey`, and consequence entries by slot. It uses
the shared recursive canonical object writer for each full definition while preserving
#1535-defined nested array order. The whole wound semantic fingerprint covers all three
collections; canonical state stores no second source fingerprint. The prepared handoff's
exact JSON/API field is `sourceExportFingerprint` / `SourceExportFingerprint`. It seals
the complete canonical source owner metadata, source ID/ref/state, non-materializable
policy, accepted causal wound event, every detached definition, ordered root-lineage
authority rows, and every ordered root application including `applicationRef`, operation
ordinals/kind, exact target and source selector, empty parameters, slot bindings, and
expected component count/materialization fingerprint, and ownership domain. It is
immutable pre-effect plan authority and is not a persisted wound field.

The cross-stage seals are non-interchangeable. `WoundPreparationFingerprint` binds the
accepted wound input, allocated wound/complication/transition IDs, detached drafts,
source-export fingerprints, and terminal intents. `EffectInputFingerprint` additionally
binds the complete typed operation batch and each derived-created/causal event pair.
`EffectAcceptedTurnPlanFingerprint` additionally binds allocated effect/transition IDs,
exact application/termination maps including actual component counts/materialization
fingerprints, and effect carrier/identity before- and after-images from which those
fingerprints are independently recomputed.
`WoundFinalPlanFingerprint` additionally binds the finalized canonical wound roots,
bindings, entries, complications, history/index/pending/scheduler/output after-images,
and intents. Finally, `AcceptedMechanicsPlan.PreparedPlanFingerprint` binds those seals,
the common input and authority fingerprints, all wound/effect/resource after-images and
receipts, touched/consumed paths, pending state, scheduler outcomes, and output bindings.

The common fingerprint is not a constructor argument or caller assertion. The common
planner computes it only after cross-stage semantic agreement; the plan cache
independently recomputes it from the detached plan before validation and again on
take/peek. Mixing otherwise valid exports, result maps, operation events, effect
after-images, or final wound bindings from different plans therefore fails closed.
Every earlier consumer likewise recomputes its predecessor's seal from detached payload:
the effect composer checks source-export and wound-preparation seals, the effect planner
checks the effect-input seal and exact expected/created materialization, and wound
finalization checks the effect-plan seal and recomputes every result fingerprint from its
exact created active-effect after-image. A seal or fingerprint property is never trusted
without payload recomputation.

`complication.ownedEffectIds` is an exact subset of canonical root-binding effect IDs;
the sets are pairwise disjoint between complications. A complication neither lists
reaction descendants nor duplicates consequence slot truth. A reaction-created effect's
create transition records its exact producing effect in `sourceEffectIds`; a direct root
create transition has an empty source set. Ownership indexes read only the new identity's
first `create` transition; `replace`, stack, refresh, and other transition result arrays
are lifecycle evidence, not additional ownership parents.

The planner reconstructs one ephemeral `WoundRootLineageAuthority` from root bindings
and complication ownership: every root is assigned `base_wound` or one exact
`complicationId`, and a reaction child inherits its producer's domain. Ordered authority
rows participate in `SourceExportFingerprint` but are not new canonical wound fields.
Before mutation, stack/refresh/merge/replace behavior between different domains is
rejected because it cannot later be selectively inverted. A root-bound reaction target
must use `replace` within the same domain; the prior target's replacement succession is
not a second causal ownership edge.

Removing a complication starts from its declared roots (including terminal roots),
follows only this validated parent-to-child
identity lineage inside the exact wound source group and persisted definition graph,
and terminates only active/suspended members of that closure. Every group identity is
visited at most once; a cycle, more than one causal `sourceEffectId` on first create,
duplicate same-kind causal evidence, foreign-source edge, missing graph definition, or
cross-domain edge fails before mutation. Legal replace/stack/refresh lifecycle result
edges are ignored by this causal traversal. Full healing instead terminates the entire
exact wound-source group.

Selective complication resolution removes the complication, its declared root bindings,
and every slot entry pointing to those roots, then recomputes `slotsUsed`. It prunes only
definitions no longer reachable from any remaining root binding; a definition still
bound or reachable from another root remains. Any active/suspended effect using a key
that would be pruned is a lineage disagreement and rejects the transition. The global
effect identity history retains terminal provenance. Thus independent dispel/expiry
still retains wound source authority, while an explicit complication-removal transition
produces a clean active wound graph. The remaining graph/slots must still satisfy the
current severity envelope; otherwise the same atomic treatment must include its declared
severity rematerialization or replacement consequence result.

## Effect proposal requirements

Every directly materialized wound-owned root effect must contain:

- exact `source.kind = wound` and matching source ID;
- exact same owner target and realm, unique derived `createdEventRef`, and the wound's
  accepted event as separate `causalEventRef`;
- a registered wound consequence profile and allowed mechanical components;
- a client-allocated effect ID after validation;
- a reciprocal canonical root binding;
- visibility no broader than the source wound/target authority permits.

The owner adapter is closed: wound `player_soul` targets #1535 `player`,
`combatant_member` targets `combatant`, and every other supported wound owner kind maps
to the identically named effect target kind. The adapter uses `targetRef` for an accepted
same-turn target and `targetId` for a stable target. Direct root applications have
exact/confusable-unique logical stack coordinates and must each create a new effect
identity; wound finalization never treats a stack/refresh/merge of another root as a
successful reciprocal binding.

Every wound-owned definition has exact `stacking.maxStacks = 1`, and definition
`stackKey` values are exact/confusable unique across the wound graph. All other
policy/`atMaximum`/refresh/merge fields must satisfy their ordinary #1535 combination
rules; in particular `independent` at its bound uses `atMaximum = no_change`. This keeps
at most one active/suspended instance per wound definition without removing refresh,
replace, merge, or legal root-definition reuse semantics.

Version 1 root applications always use exact empty `{}` parameters and therefore every
root-bound definition has exact empty `parameterBounds`. Authored numeric/scope values
are frozen directly in its components. A downstream-only definition may declare
bounded parameters because the persisted `apply_definition` component also seals the
exact parameters supplied when that reaction is released. Root parameters are not
silently defaulted and are not duplicated in canonical root bindings. The exact `{}`
still remains an explicit materialization-fingerprint input so a future parameterized
root contract cannot silently reuse version-1 digests.

Wound version 1 permits zero or one downstream definition and forbids nested
wound-owned `apply_definition`. When the optional edge exists, that reaction's #1535
`maxExpansion` is exactly `2` (the reaction plus its one reachable definition), while
the wound semantic budget counts one downstream expansion. A graph without a reaction
edge remains valid.

Every slot-consuming root also has one or more reciprocal wound consequence entries.
The sole legal zero-slot root exception is a directly materialized
`wound_consequence` marker. The one permitted marker definition may instead be a later
reaction descendant. A reaction-created instance receives no additional root binding
and no slot, even when its definition is also directly root-bound; its complete
definition is present in the wound graph and its worst-case components were charged to
the originating reaction root before acceptance.

An effect cannot contain a wound transition, wound command, treatment route, recovery
tick, or history mutation.

## Slot accounting

### Mortal wounds

Severity I/II/III/IV permits at most 1/2/3/4 independently understandable mechanical
consequences. Zero mechanical slots is legal only when an active complication or a
care/recovery constraint changes the legal lifecycle; display text alone is invalid.

### Spiritual wounds

Severity I/II/III/IV requires exactly 1/2/3/4 independently understandable legal
afterlife consequences.

### Counting rules

- Technical source linkage, display copy, symptoms, prognosis, and a zero-mechanics
  marker cost zero slots.
- Each independent numeric modifier, roll mode, periodic operation, permission,
  restriction, control, resistance, resource reaction, or other mechanical outcome
  costs at least one slot.
- A profile cannot hide multiple independent mechanics in one object or prose field.
- One effect may occupy more than one slot only when its registered profile explicitly
  declares and validates those independent components; the wound records every slot.
- Slot count is recomputed from parsed mechanical components, never trusted from a GM
  integer.

## Power envelopes

`WoundConsequenceEnvelopeCatalog` owns domain/profile/severity limits. At minimum each
profile seals:

- allowed component kinds and combinations;
- allowed target scope and affected axes;
- magnitude, cadence, duration/use/exchange, and stack limits;
- action/control restriction strength;
- counterplay or safe-exit requirement;
- whether the effect may remain when its visible symptom is suppressed.

“Power” in this section names that heterogeneous catalog policy; it is not a universal
numeric application-result field. Cross-stage equality instead uses the domain/versioned
materialization fingerprint over the exact source coordinate, schema, parameters,
component count, and ordered fully bound components. The effect stage derives it from
the created active effect, and Finalize independently recomputes it from the detached
after-image.

Spiritual profiles use only legal afterlife conflict/profile axes. Mortal profiles use
registered universal effect primitives without introducing a catalog of complete
wounds. Severity III-IV may impose severe action restrictions, but at least one path to
inspect, communicate, ask for help, receive treatment, or exit the condition must
remain.

### Exact version-1 Mortal registry

The usable generic effect profiles are exactly `characteristic_modifier`,
`roll_modifier`, `resistance_modifier`, `periodic_damage`, `periodic_restore`,
`action_control`, and `event_reaction`. `wound_consequence` is a zero-slot marker, and
at most one may appear across the wound-owned effect set. One independently affected
characteristic, roll operation, resistance, periodic resource, action, or worst-case
reaction component consumes one slot.

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

Scalar caps use the same order as runtime application: apply exact-decimal `minimum`,
then exact-decimal `maximum`, and budget the resulting nonzero modifier. A larger finite
raw value may be admitted when the cap makes the runtime modifier legal; a cap may not
amplify it past the severity limit. A finite number outside the exact decimal contract
is rejected and cannot silently remove a component from slot derivation.

Periodic values are quantum-aligned without rounding above the cap and execute at most
once for one accepted source event. Roll array entries consume one slot each. Every
worst-case reaction result is counted before admission. The single wound expansion
ceiling applies only to wound-owned effects; bounded independent siblings are preserved
outside wound slot and expansion budgets. A separate structural work bound permits at
most 64 flattened reaction-expansion rows in each raw effect proposal before ownership
classification; it does not consume the wound-owned semantic ceiling or discard bounded
independent rows. `forbid` may target only
`attack`, `cast`, or `movement`; `defend`, `use_item`, `interact`, and `escape` remain
non-forbiddable safety-capable action keys. Aggregate wound effects must still preserve
inspection, communication, help, treatment, and exit.

### Exact version-1 spiritual registry

| Profile | Axis | Severity envelope |
| --- | --- | --- |
| `spiritual_roll_hindrance` | `rollMode` | one declared operation per slot at I-IV |
| `spiritual_action_cost_burden` | `actionCostAudit` | +1 at I-II, +2 at III, +3 at IV |
| `spiritual_position_burden` | `conflictPosition` | one adverse step at I-II, up to two at III-IV |
| `spiritual_control_burden` | `controlState` | unavailable at I; one adverse step at II-IV |
| `spiritual_strain_burden` | side strain | unavailable at I-II; one extra step at III-IV, capped at `broken` |
| `spiritual_tempo_burden` | `tempoAdvantage` | deny one owner gain per slot at I-IV |
| `spiritual_counter_burden` | `counterPayoff` | reduce one payoff step per slot at I-IV |
| `spiritual_art_restriction` | one standard combat art | unavailable at I-II; restrict at III; forbid at IV |

Every instance targets one declared non-safety operation/family, costs one slot, and is
unique by profile/operation coordinate within the wound. `spiritual_healing`, wound
inspection, communication, help, withdrawal, surrender, negotiation, and the separate
dissipation decision cannot be targeted. Eligible operation keys are exactly
`pressure`, `counter`, `guard`, `maneuver`, `binding`, `break_binding`,
`force_binding`, `force_incarnation`, `incarnation_resistance`,
`champion_coordination`, and `recover_spiritual_power`. These primitives may be combined into unique
GM-authored spiritual wounds; they are not complete-wound templates.

`spiritual_art_restriction` uses the dedicated ten-art combat subset: `pressure`,
`counter`, `guard`, `maneuver`, `binding`, `break_binding`, `force_binding`,
`incarnation_resistance`, `champion_coordination`, and `recover_spiritual_power`.
It does not derive from the extensible standard-art registry and therefore never admits
`force_incarnation`, `spiritual_resilience`, `spiritual_healing`, or later noncombat
arts without an explicit versioned contract change.

The eight identifiers in this registry are also exact first-class #1535 component
profiles. Each has deterministic metadata, only the `profile_specific` merge reducer,
and the same closed
`{ operation, axis, magnitude }` payload accepted here; registry parity is mandatory so
a consequence accepted by the wound envelope cannot be rejected by the common effect
contract. `EffectComponentProfiles` owns profile-invariant shape/type/domain checks,
while the wound envelope alone owns severity availability, exact magnitude for the
accepted severity, slot accounting, coordinate uniqueness, and aggregate safe exits.

The common registry uses two exact operation domains. `O` is `pressure`, `counter`,
`guard`, `maneuver`, `binding`, `break_binding`, `force_binding`,
`force_incarnation`, `incarnation_resistance`, `champion_coordination`, and
`recover_spiritual_power`. `A` is the same set without `force_incarnation`. The common
profile validator admits exactly this machine domain before the wound severity policy
selects the legal member:

| Profile | Operation domain | Exact axis | Exact JSON `magnitude` domain |
| --- | --- | --- | --- |
| `spiritual_roll_hindrance` | `O` | `rollMode` | string `disadvantage` |
| `spiritual_action_cost_burden` | `O` | `actionCostAudit` | integer `1..3` |
| `spiritual_position_burden` | `O` | `conflictPosition` | integer `1..2` |
| `spiritual_control_burden` | `O` | `controlState` | integer `1` |
| `spiritual_strain_burden` | `O` | `sideStrain` | integer `1` |
| `spiritual_tempo_burden` | `O` | `tempoAdvantage` | string `deny_one_gain` |
| `spiritual_counter_burden` | `O` | `counterPayoff` | string `reduce_one_step` |
| `spiritual_art_restriction` | `A` | `artAvailability` | string `restrict` or `forbid` |

An integer magnitude is a JSON number accepted exactly as an integer, not a numeric
string, fractional value, non-finite value, or boolean. String values and all three
payload property names are ordinal and case-sensitive. Missing, extra, null, object,
or array values fail closed; display prose cannot supply a mechanical field.

A #1535 source definition containing any of these profiles is scoped to
`chaos_sea|shining_abode`, a persistent actor target (`player`, `guardian`, `resident`,
`radiant_actor`, or `afterlife_actor`), and exactly one `wound` link with role `source`.
It cannot target `spiritual_conflict_side` and does not inherit the finite-lifetime
adapter imposed on `afterlife_combat_condition`; ordinary #1535 lifetime validation
applies. Visible accepted effects require a safe generic player projection of profile,
operation, axis, and magnitude without leaking internal wound/effect/source identity.

Current-conflict application is a typed derived contribution from exact actor
membership. It never translates the persistent component into
`afterlife_combat_condition`, never adds it to `combatConditions[]`, and never creates a
second canonical wound/effect copy. Conflict closure removes only derived evidence;
the actor-carried wound, definition graph, root binding, and effect remain until their
own accepted lifecycle transition.

## Severity rematerialization

A change of severity does not incrementally patch arbitrary effects. The planner:

1. reads the old complete source graph and the indexed active root/descendant effect set;
2. validates the proposed new complete graph and root set against the new severity;
3. plans terminal transitions for every active/suspended old root and descendant in the
   exact wound source group, even when a definition remains semantically unchanged;
4. applies every new root after that full teardown and requires a fresh effect identity
   result for each application;
5. validates exact reciprocal root links, graph membership, and unrelated-effect preservation;
6. commits wound, index, history, effect carriers/index/history, and output together.

There is never an accepted intermediate state where the wound says severity II while
its consequence set is still the severity IV set.

## Independent effect removal

If a wound-owned effect is independently dispelled or expires while the wound remains
active:

- the effect transitions through the normal effect lifecycle;
- for a directly materialized root, its canonical root binding and reciprocal slots
  remain as known source/consequence authority with terminal or suppressed effect status
  according to the registered profile; a reaction descendant has neither and retains
  only its persisted definition/source authority;
- the wound severity/care/recovery does not change;
- a later wound transition may rematerialize a currently legal effect through a new
  effect identity only when the wound/source rule declares that behavior;
- no missing-effect fallback mutates the wound.

This preserves the possibility of suppressing pain without mending the underlying
injury.

## Accepted-mechanics ordering

The complete plan uses this semantic order:

1. resolve exact owner/event/opportunity and prepare wound sources;
2. resolve effect plan against those sources and the pre-turn carrier catalog;
3. finalize wound/effect reciprocal agreement and transition intent;
4. compose item/resource reservations and mutations;
5. compose scheduler/progression/journal/quest/output outcomes;
6. freeze all after-images, fingerprints, touched/consumed paths, and before-images;
7. publish under one canonical write lease or restore the pending snapshot.

Same-root changes such as afterlife profile wounds, profile effects, art progression,
and scheduler-derived state must be composed through one typed root assembler. Two
whole-root producers for the same path are invalid.

## Atomic failure examples

- Invalid effect magnitude: no wound identity/history is created.
- Valid wound and effects but stale medicine item: no wound/effect/resource change.
- Valid treatment but missing reciprocal effect ID: no severity/resource/output change.
- Crash after writing one root: snapshot recovery restores every touched path and final
  player output.
- Replay of accepted treatment: no new effect transition, charge, history row, or
  notification.

## Saref boundary

`memory_suppression` has an independent Saref source and is never included in a wound's
source graph or root bindings. A distinct trauma-caused memory-loss effect may instead
be explicitly materialized as its own wound-owned definition and direct root when
applicable. Healing a spiritual wound must prove the independent
Saref effect's before/after bytes are unchanged.
