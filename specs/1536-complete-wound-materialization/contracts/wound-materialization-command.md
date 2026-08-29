# Contract: Wound Opportunity and Materialization Command

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Purpose

This contract separates the client's authority to prove that an event can produce a
wound from the GM's authority to decide whether an ordinary wound occurs and author its
fiction. It applies to Mortal and afterlife wound creation/worsening.

## Producers

A registered adapter may create an opportunity only from an accepted, exact event:

- Mortal combat, QTE, trap, check, hazard, item/skill/effect trigger, or explicit
  narrative injury event; item/skill/effect triggers enter the closed `formal` producer
  family and never introduce an eighth adapter kind;
- spiritual-conflict strain transition or explicit re-trauma action;
- a pre-materialized source whose contract guarantees a wound.

The owning producer seals owner, realm, domain, event/source evidence, legal location
profile, and maximum severity into the accepted occurrence. For narrative injuries, the
producer may admit a GM-authored causal description only while accepting the upstream
typed narrative result; it reduces that result to the same bounded `narrative`
occurrence envelope. The later wound-opportunity ingress can only correlate with that
row and cannot author or change its profile, outcome, or maximum.

Producer adapters return immutable occurrence candidates without writing. The common
accepted planner for the source-result transaction is the sole publisher to the pending
occurrence root. The opportunity is not exposed until a subsequent active pending-turn
snapshot seals that root's exact bytes. The later decision transaction is the sole
publisher of receipt and occurrence-consumption after-images.

For Mortal adapters the source-shaped ingress is only a correlation projection. It
must resolve one exact client-owned accepted-occurrence row from the active signed
pending-turn snapshot. The occurrence authority, not the ingress, supplies the adapter
kind, owner, source coordinates/state, bounded causal profile, audited outcome, legal
location kinds, readable cause, and hard severity maximum. Current source presence,
combat membership, an active hazard, sealed dice, or narrative prose alone proves no
occurrence. The seven closed version-1 kinds are `formal`, `qte`, `combat`, `trap`,
`check`, `hazard`, and `narrative`; all use the same occurrence authority envelope and
kind-specific canonical evidence checks.

The closed correlation root contains only `schemaVersion`, `adapterKind`,
`acceptedEventOrdinal`, `opportunityRef`, `owner`, `domain`, `profileKey`, `source`,
`outcome`, `safeContext`, and an optional complete `worseningTarget`. Absence means
create; explicit null is invalid. In particular, `hardMaximumSeverityRank`, nullable
minimum/guarantee, source or decision binding, accepted-event kind/ID/ref/seals,
occurrence/producer IDs and fingerprints, receipts, transitions, proposals, anchors,
and after-images are forbidden. The hard maximum and guarantee are derived only from
the signed occurrence.

The accepted wound binding seals the complete ordered accepted-event set reconstructed
from the immutable accepted response and the occurrence authority. The occurrence
selects one exact zero-based ordinal inside that set. Initial composition and accepted-
turn validation reconstruct the set independently; an added, removed, replaced, or
reordered sibling invalidates the wound command. The historical set is first recomposed
under the occurrence's exact source session/request/snapshot-token/turn and compared to
its stored seal. The same ordered coordinate vector is then rebound to the active
decision snapshot: selected evidence is derived again, while generic sibling seals are
allowed to change only through that binding. `worseningTarget` is absent for create
and is one explicit exact active-wound/cause coordinate for worsen; it is never inferred.
The occurrence persists the original ordered 1-160 event authorities and their
recomputed set fingerprint, plus one stable source `producerOperationKey` and exact
candidate ordinal/count for the complete harmful source-result batch. Pure append logic
sorts by the sealed ordinal. Exact source retry reuses a pending occurrence or resolves
its durable consumed receipt, while changed semantics under that key conflict. A
harmless typed result creates no candidate or public opportunity.
The producer key identifies the batch and therefore repeats across its rows; each row is
identified by `(producerOperationKey, producerCandidateOrdinal)`. Every pending or
consumed row in that group must agree on source session/request/snapshot token/turn,
count, adapter, source-result authority/fingerprint, and no ordinal may exist in both
roots.

Accepted-turn validation treats the command opportunity only as comparison input. The
pending occurrence and append-only receipt roots are mandatory signed snapshot paths;
their live bytes must still equal the snapshot before-image. The validator parses both,
checks their union agreement, reconstructs each complete producer batch and active-
snapshot event vector, resolves an explicit worsening target only in the signed carrier,
and composes a fresh opportunity from occurrence-owned fields. Every field and seal must
match the submitted opportunity, and the exact signed prior receipt projection is used
when the command is recomposed. Raw effect-event JSON and the submitted
`inputEvidenceFingerprint` are never evidence at this boundary.

## Client-authored opportunity

```json
{
  "schemaVersion": 1,
  "opportunityId": "client-owned",
  "eventRef": "accepted-event-ref",
  "ownerBinding": "client-owned-exact-binding",
  "realm": "mortal_world",
  "domain": "physical",
  "profileKey": "mortal_narrative_injury_v1",
  "maximumSeverity": "II",
  "guaranteedTrigger": null,
  "safeContext": {
    "target": "вы",
    "cause": "осколок стекла после обвала",
    "allowedLocationKinds": ["anatomical", "systemic", "other"]
  },
  "authorityFingerprint": "sha256:..."
}
```

The pending packet may expose `safeContext`; it never exposes owner IDs, resource
seals, hidden NPC facts, or internal paths not needed for repair.

## GM decision

Ordinary opportunity declined:

```json
{
  "opportunityRef": "client-visible-pending-ref",
  "decision": "none"
}
```

Accepted proposal:

```json
{
  "opportunityRef": "client-visible-pending-ref",
  "decision": "materialize",
  "woundRef": "gm-local-ref",
  "proposal": {
    "classification": {
      "woundType": "Рваная режущая травма",
      "locationProfile": {
        "kind": "anatomical",
        "readableLocus": "левая ладонь"
      }
    },
    "display": {
      "name": "Рваная рана ладони",
      "description": "Края раны расходятся при движении.",
      "visibleSymptoms": ["кровотечение", "боль при хвате"],
      "prognosis": "Требует очистки и закрытия.",
      "visibility": "known_to_player",
      "acquisitionNarration": "Осколок вспарывает ладонь, и пальцы немеют от боли."
    },
    "severity": "II",
    "complications": [],
    "consequenceDefinitions": [],
    "treatment": {},
    "recovery": {}
  }
}
```

`woundRef` is response-local and never becomes canonical identity. The client allocates
`woundId`, effect IDs, complication IDs, transition IDs, ordinals, operation keys, and
receipts only after the whole proposal is valid.

## Required decision rules

1. An ordinary opportunity permits `none` or severity I through its maximum.
2. The GM may select a lower severity than the maximum without explanation to the
   validator.
3. A guaranteed trigger forbids `none` and requires the declared result inside every
   harder realm/mode cap.
4. Contradictory guaranteed source and hard cap invalidate the source/event contract;
   the client does not invent a compromise wound.
5. A training spiritual conflict forbids creation unless it was explicitly escalated
   before the harmful exchange.
6. A side that already received a wound in this spiritual conflict can receive only a
   worsen opportunity for that exact wound.
7. An older active wound requires an explicit accepted re-trauma target.
8. Death and soul dissipation are not wound proposals.

## Completeness validation

Before allocation, the proposal must pass all of these gates:

- exact opportunity/session/request/snapshot/event match;
- exact owner, realm, and domain match;
- severity within legal range;
- closed location and display shape;
- required consequences within count/power envelope;
- exact treatment/recovery completeness for the domain;
- effect source/target admission through staged source export;
- required acquisition narration and safe player notification;
- no GM-authored client fields or unknown properties;
- all collection and text bounds.

No partial identity, wound, effect, resource mutation, history row, scheduler tick, or
player output is published when any gate fails.

## Staged planning handshake

1. `WoundAcceptedTurnPlanner.Prepare` validates opportunity/proposal fields independent
   of generated effect identity and allocates only provisional wound and wound-transition
   identities in memory. It allocates no effect ID.
2. It exports the complete detached wound source-definition graph and an immutable typed
   `WoundEffectOperationBatch` of exact direct root applications. Each root has only a
   response-local `applicationRef`; a reaction-only descendant has neither an
   application nor a reserved identity. The source export is `Materializable = false`
   for ordinary GM effect commands; the typed batch is the sole direct-root allowlist.
   Each root also carries an internally derived expected component count and versioned
   fingerprint of its exact source coordinate, schema, parameters, and ordered fully
   bound components.
3. `EffectAcceptedTurnInputComposer` resolves the closed owner-to-target mapping,
   requires `sourceRef` for a new same-turn wound (`sourceId` for an existing stable
   wound), and derives a distinct internal operation event for each batch member from
   the sealed wound event and mechanics ordinal. That derived value becomes the exact
   created/transition event; the shared accepted wound event remains separate causal
   chronology.
4. `EffectAcceptedTurnPlanner` validates the whole graph, materializes only the proposed
   roots, remains the sole allocator of random opaque permanent `effectId` values, and
   returns a detached typed result mapping every `applicationRef` to the identity it
   created. The result's actual component count and materialization fingerprint are
   derived from that exact created active effect. A non-creating stack/refresh/merge
   result is invalid for a wound root.
5. `WoundAcceptedTurnPlanner.Finalize` verifies the result set, create disposition and
   transition, both event refs, reciprocal root identity, complete graph membership/
   linkage, owner, realm, source, target, carrier, ordered slot, component-count, and
   exact bound-materialization-fingerprint agreement, independently recomputing the
   latter from the created effect after-image, then persists the graph and root bindings
   in the wound.
6. The common accepted-mechanics planner composes resources, scheduler, output, and all
   canonical after-images.
7. One validated handoff publishes or rolls back everything.

The prepared source export and operation batch are immutable and fingerprinted, while
the permanent effect IDs remain random and opaque exactly as required by #1535.
Non-interchangeable wound-preparation, effect-input, effect-plan, and wound-final seals
culminate in one internally computed common prepared-plan fingerprint over all exact
results and after-images. The cache independently recomputes it; no caller may assert a
replacement seal or mix valid pieces from different plans. Reusing the same cached
common plan reuses its allocated results; rebuilding after a changed session/snapshot/
authority creates a different plan. `LocalWoundRef` and
`applicationRef` remain handoff-only coordinates and are never persisted in canonical
wound or effect state.

## Acquisition output

An accepted wound must be visible in the GM-authored scene text and in a deterministic
client notification:

```text
Получена рана: Рваная рана ладони (II). Подробнее: /раны
```

or

```text
Получена духовная рана: Трещина в узоре воли (II). Подробнее: /раны
```

If narration omits or contradicts the accepted injury, the bounded repair packet asks
only for corrected acquisition narration while preserving the valid proposal.

## Replay and repair

- `opportunityId + eventRef + owner + decision` participates in the operation key.
- The same accepted opportunity cannot allocate a second wound.
- A consumed decline cannot later be replayed as an injury without a new event.
- Every accepted decision, including `none`, appends one sealed client-owned opportunity
  decision receipt through the same common publication transaction. A decline does not
  require or synthesize a wound-history transition.
- The receipt binds the opportunity authority, signed turn/snapshot, selected event and
  semantic fingerprint, decision fingerprint, and operation key. Exact cold replay
  while the same active snapshot retains its pre-consumption occurrence returns no
  command/transition; changed semantics conflict; a newer snapshot makes the old
  correlation stale, and only a newly sealed occurrence creates a distinct opportunity.
- A `none` receipt has no wound/transition coordinate and no wound-history row. A
  `materialize` receipt names one exact wound and `create|worsen` transition and agrees
  with that history row on transition/wound/turn/event/operation and opportunity source
  fingerprint.
- Every receipt retains the consumed occurrence's source session/request/snapshot
  token/turn,
  producer operation key and candidate ordinal/count, plus source-result/candidate/
  occurrence fingerprints. The source-result publisher reads both roots before append,
  so exact retry after consumption is still a no-op and changed source semantics still
  conflict.
- Repair uses the same event, target, roll, maximum, and provisional semantic proposal.
- A changed snapshot or semantic event abandons the pending packet and requires a new
  opportunity.
