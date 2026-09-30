# Contract: Canonical Wound Identity, Carriers, and Lifecycle

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Authority boundary

The client owns permanent identity, owner/realm coordinate, index, transition history,
retry keys, ordinals, progress arithmetic, receipts, and terminal status. The GM owns
bounded semantic wound content and allowed narrative choices. Neither side may write a
complete canonical root independently.

Canonical roots and carrier shapes are defined in `../data-model.md`. All roots use
schema version 1 and closed parsing. Every wound carries mandatory
`consequences.ownedEffectSources` with a complete bounded #1535 definition graph and a
separate exact root-binding set. The client rejects the former slot-only development
shape; there is no migration or compatibility reader.

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
wound, initial history row, complete consequence source graph, directly materialized
root effect set, and player-visible notification. Reaction-only descendant definitions
receive no effect identity until their accepted trigger. Wound preparation allocates no
effect identity: the subordinate effect plan allocates every opaque root ID and returns
an exact typed application-result map before the wound is finalized. A create operation
is all-or-nothing.

### Worsen

Requires active severity I-III and a sealed deterioration/re-trauma/same-conflict
opportunity. New severity must be higher and no greater than IV. It resets current-step
recovery, terminates every active/suspended old root and descendant through the exact
wound-source index, then rematerializes every new root with a fresh effect identity and
appends one history row. This is a full source-group teardown, not an identity-preserving
definition diff; an unchanged definition does not retain its former runtime instance.

### Complicate

Requires a registered complication transition and exact cause. It may leave severity
unchanged or worsen only when that outcome was sealed. Duplicate complication/operation
keys are rejected.

### Diagnose

Requires one reachable path and client-sealed `success` or `failure` attempt evidence.
Success reveals the complete ordered fact set named by that path. Failure reveals no
fact and changes no known route or complication visibility. Neither result can reduce
severity, change care/recovery/consequence mechanics, rewrite display text, or mutate
resources directly. A failed diagnosis may consume only resources declared through
the common accepted-mechanics plan and still produces one terminal retry-safe attempt.

### Author alternative treatment

Requires an active Mortal wound, one fresh pre-response authoring request bound to the
current before fingerprint and accepted event, and post-response accepted-transition
evidence bound to that request, the complete new route, and optional diagnosis path.
It appends exactly one route and one history row. A
visible route also becomes known; a hidden route requires one atomically appended
diagnosis path that passes both the intra-wound discovery fixed point and fresh world
reachability proof. Existing routes, paths, known/completed route order, and prior
history are immutable. The transition cannot treat, stabilize, worsen, or heal.

### Stabilize

Requires a valid route/outcome and active unstabilized wound. It may remove a declared
complication/effect or unlock recovery, but it does not implicitly reduce severity.

### Treat

Requires an exact active wound, fresh target/provider/reachability/consent authority,
complete route or standard spiritual-healing gate, one sealed attempt, and any reserved
resources. Only the declared result band/milestone/guaranteed outcome applies. Every
accepted treatment result is terminal for its retry-safe attempt, including declared no
improvement, harmful failure, or course interruption; that does not set the wound
history row's lifecycle `terminal` flag. Procedure natural criticals run only after hard
authority gates. A course interrupts only on a trustworthy unsatisfied live predicate
or exceeded deadline; invalid authority rejects. Guaranteed routes require both the
unchanged successful capability requirement and a separate canonical current player/NPC
skill proof with permanent source identity and matching operation limits. Closed typed
treatment operations may stage the working severity down to I and then request a
separate follow-up heal; they cannot heal directly from a higher working severity. A new
complication carries its complete validated complication proposal: its consequence graph
is either exact empty for an effectless complication or one complete validated effect
draft. Lasting legacies occur only as complete declarations on that follow-up heal.

### Recover

Requires an exact due Mortal clock event or afterlife safe-cycle key. The client
calculates bounded progress once and applies any resulting severity steps. Narrative
text cannot tick recovery.

### Heal

Requires an accepted transition from active severity I, including a severity-I
after-image staged by an immediately preceding accepted treatment result. It terminates all and only the
wound's active root and reaction-descendant effects by the exact
`(realm, wound, woundId, definitionKey)` index grouped by wound source, removes the wound
from its carrier, marks the index terminal, and appends immutable terminal history.
An active source entry whose definition key is absent from the persisted graph rejects
the transaction. Healed wounds do not remain in the active list.

Selective complication cleanup is narrower than healing. Complication root sets are
pairwise disjoint. The planner reconstructs each root's scalar ownership domain as
`base_wound` or the exact owning `complicationId`; reaction descendants inherit their
producer's domain, and cross-domain stack/refresh/merge/replace is rejected before
mutation. Cleanup begins even from a terminal declared root and follows only exact first-
`create` reaction-parent lineage (`sourceEffectIds`) inside the same wound source,
definition graph, and ownership domain, visiting each group identity at most once.
Replacement succession remains separate lifecycle evidence and is not an ownership
parent. A same-definition first-create generation edge points from a retired canonical
root to its severity-rematerialized successor and is classified separately; selective
cleanup starts from the exact current root and does not treat that edge as a reaction.
It terminates only the active/suspended descendants of those roots; unrelated
wound roots survive.
The resolved complication, its declared root bindings, and reciprocal slots leave the
active wound; `slotsUsed` is recomputed. Only definitions no longer reachable from any
remaining root are pruned, and any active effect that would lose its definition rejects
the transition. Terminal provenance remains in the global effect identity history.

### Legacy and archive

A cosmetic scar/legacy may remain in readable history. Version 1 represents every
lasting mechanical modifier as a new independent #1535 effect, never as an open
skill/trait/other payload. Its client-only, non-GM-materializable
`sourceKind=wound_legacy`/`sourceId=legacyId` authority is reconstructed from the exact
typed nonterminal `legacy` history row linked to the terminal wound. It does not keep the
wound active and is not included in active-wound effect cleanup. Later dispel/removal may
end that effect but cannot erase the legacy row, source provenance, terminal replay
evidence, or cosmetic history. Archive changes projection, not any of those authorities.

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
4. complete wound definition graph, root bindings, pairwise-disjoint complication root
   ownership, reconstructed ownership domains, source and first-create causal-parent
   indexes, slot-consuming roots, the optional zero-slot marker, and reaction-descendant
   membership;
5. exact owner/source/target/realm authority;
6. legal treatment/recovery/state transitions;
7. independent exact/confusable uniqueness and semantic agreement for every operation
   key, every non-null treatment attempt ID, and every `(courseId, milestoneOrdinal)`;
8. complete before-image coverage;
9. canonical after-image byte equality after publication.

Any disagreement invalidates the common accepted-mechanics handoff and invokes the
existing snapshot rollback contour.
