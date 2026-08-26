# Contract: Wound Consequences, Effects, and Atomic Composition

**Feature**: `1536-complete-wound-materialization`  
**Depends on**: completed effect materialization [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## Core separation

A wound is independently treatable canonical state. A wound consequence is an active
effect whose exact source is that wound. The effect engine applies mechanics; the wound
kernel owns severity, care, treatment, recovery, and healing.

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
  "sourceState": "active",
  "realm": "mortal_world",
  "ownerKind": "player",
  "ownerId": "player_current",
  "eventRef": "accepted-event-ref",
  "semanticFingerprint": "sha256:..."
}
```

This export enters the existing accepted-plan effect source catalog. No loose scan of
`playerWoundChanges`, no wound-name inference, and no `duration=999` legacy reference is
accepted.

## Effect proposal requirements

Every wound-owned effect must contain:

- exact `source.kind = wound` and matching source ID;
- exact same owner target, realm, and accepted event;
- a registered wound consequence profile and allowed mechanical components;
- a client-allocated effect ID after validation;
- a reciprocal wound consequence slot;
- visibility no broader than the source wound/target authority permits.

An effect cannot contain a wound transition, wound command, treatment route, recovery
tick, or history mutation.

## Slot accounting

### Mortal wounds

Severity I/II/III/IV permits at most 1/2/3/4 independently understandable mechanical
consequences. Zero is legal when the authored injury is still complete and readable.

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

Spiritual profiles use only legal afterlife conflict/profile axes. Mortal profiles use
registered universal effect primitives without introducing a catalog of complete
wounds. Severity III-IV may impose severe action restrictions, but at least one path to
inspect, communicate, ask for help, receive treatment, or exit the condition must
remain.

## Severity rematerialization

A change of severity does not incrementally patch arbitrary effects. The planner:

1. reads the old complete wound-owned effect set;
2. validates the proposed new complete set against the new severity;
3. plans terminal transitions for removed/replaced old effects;
4. plans apply/update operations for the new set;
5. validates exact reciprocal links and unrelated-effect preservation;
6. commits wound, index, history, effect carriers/index/history, and output together.

There is never an accepted intermediate state where the wound says severity II while
its consequence set is still the severity IV set.

## Independent effect removal

If a wound-owned effect is independently dispelled or expires while the wound remains
active:

- the effect transitions through the normal effect lifecycle;
- the reciprocal wound slot remains as a known consequence definition with terminal or
  suppressed effect status, according to its registered profile;
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
owned effect IDs unless a distinct trauma-caused memory-loss effect was explicitly
materialized from that wound. Healing a spiritual wound must prove the independent
Saref effect's before/after bytes are unchanged.
