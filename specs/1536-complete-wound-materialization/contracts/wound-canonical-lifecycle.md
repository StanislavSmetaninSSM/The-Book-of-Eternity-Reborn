# Contract: Canonical Wound Identity, Carriers, and Lifecycle

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Authority boundary

The client owns permanent identity, owner/realm coordinate, index, transition history,
retry keys, ordinals, progress arithmetic, receipts, and terminal status. The GM owns
bounded semantic wound content and allowed narrative choices. Neither side may write a
complete canonical root independently.

Canonical roots and carrier shapes are defined in `../data-model.md`. All roots use
schema version 1 and closed parsing.

## Identity agreement

For each active `woundId` exactly one of these carrier coordinates exists:

- Mortal player wound root;
- dedicated named-NPC wound entry;
- exact Mortal combatant/group-member object;
- exact persistent afterlife actor profile.

The identity index entry and carrier wound must agree on:

- `woundId`, status, domain, owner kind/ID, realm, and carrier path;
- creation event/turn and last transition ordinal;
- canonical semantic fingerprint.

Confusable, case-folded, display-name, array-index, historical, duplicate, and
wrong-realm matches are not fallbacks. A combatant identity may move only through an
existing accepted persistence/promotion transition that explicitly projects the wound
carrier and preserves history continuity.

## Legal transitions

### Create

Requires one unconsumed legal opportunity. Creates one index entry, active carrier
wound, initial history row, complete consequence effect set, and player-visible
notification. A create operation is all-or-nothing.

### Worsen

Requires active severity I-III and a sealed deterioration/re-trauma/same-conflict
opportunity. New severity must be higher and no greater than IV. It resets current-step
recovery, rematerializes the complete consequence set, and appends one history row.

### Complicate

Requires a registered complication transition and exact cause. It may leave severity
unchanged or worsen only when that outcome was sealed. Duplicate complication/operation
keys are rejected.

### Diagnose

Reveals only fields named by one reachable accepted diagnosis path. It cannot reduce
severity, care state, complication state, or resource values. A failed diagnosis may
consume only its declared resources and produces one terminal attempt result.

### Stabilize

Requires a valid route/outcome and active unstabilized wound. It may remove a declared
complication/effect or unlock recovery, but it does not implicitly reduce severity.

### Treat

Requires an exact active wound, fresh target/provider/reachability/consent authority,
complete route or standard spiritual-healing gate, one sealed attempt, and any reserved
resources. Only the declared result band applies.

### Recover

Requires an exact due Mortal clock event or afterlife safe-cycle key. The client
calculates bounded progress once and applies any resulting severity steps. Narrative
text cannot tick recovery.

### Heal

Requires an accepted transition from active severity I. It terminates all and only the
wound's active effects, removes the wound from its carrier, marks the index terminal,
and appends immutable terminal history. Healed wounds do not remain in the active list.

### Legacy and archive

A cosmetic scar/legacy may remain in readable history. Any lasting mechanical modifier
is a new independent effect/skill/trait/other registered entity linked by provenance to
the terminal wound. It does not keep the wound active. Archive changes projection, not
terminal replay evidence.

## Forbidden transitions

- severity V, death-as-wound, or dissipation-as-wound;
- owner/realm retargeting without a typed accepted owner transition;
- physical-to-spiritual or spiritual-to-physical in-place conversion;
- healing by removing or expiring an effect;
- reopening a healed wound;
- a lower severity without accepted treatment/recovery;
- a higher severity without accepted worsening evidence;
- reuse of a terminal opportunity, attempt, course milestone, cycle, or transition key;
- direct GM history/index/progress/receipt edits;
- deletion of terminal history to permit replay.

## History continuity

Every history row contains one globally ordered ordinal and one per-wound transition
ordinal. Its `beforeFingerprint` equals the previous row's `afterFingerprint`; the first
row has a sealed empty/nonexistent before state. The current index/carrier fingerprint
equals the latest row for an active wound. A terminal index entry equals the terminal
row and has no active occurrence.

History presentation is not history authority. Player History may summarize several
technical transitions, but canonical rows remain sufficient to reject duplicates and
prove exact resource/effect/scheduler results.

## Realm lifecycle

- A Mortal physical wound remains Mortal canonical history when the soul moves to the
  afterlife; it is not shown as an active afterlife wound and is not transformed.
- A spiritual wound follows the persistent soul/afterlife actor authority across Chaos
  Sea and Shining Abode only when the owning lifecycle explicitly preserves that actor.
- A terminal Mortal death outcome follows the existing death contract and may archive
  physical wounds; it does not convert them.
- Incarnation/bootstrap rejects stale wrong-realm active wound bindings rather than
  silently migrating them.

## Agreement validation

Pre-plan and post-publication validation independently prove:

1. all roots parse as strict schema version 1;
2. index/carrier uniqueness and agreement;
3. index/history chain and terminal agreement;
4. reciprocal wound/effect links;
5. exact owner/source/target/realm authority;
6. legal treatment/recovery/state transitions;
7. operation-key uniqueness;
8. complete before-image coverage;
9. canonical after-image byte equality after publication.

Any disagreement invalidates the common accepted-mechanics handoff and invokes the
existing snapshot rollback contour.
