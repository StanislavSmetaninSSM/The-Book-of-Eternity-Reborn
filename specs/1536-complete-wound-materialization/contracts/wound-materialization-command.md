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
  narrative injury event;
- spiritual-conflict strain transition or explicit re-trauma action;
- a pre-materialized source whose contract guarantees a wound.

The adapter seals owner, realm, domain, event/source evidence, legal location profile,
and maximum severity. Narrative adapters accept a GM-authored causal profile only
inside a closed shape and cannot contradict a sealed harmless/lower event.

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
   of generated effect identity and allocates provisional client identities in memory.
2. It exports exact wound source definitions to `EffectAcceptedTurnInputComposer`.
3. `EffectAcceptedTurnPlanner` validates/materializes the proposed consequence effects.
4. `WoundAcceptedTurnPlanner.Finalize` verifies reciprocal wound/effect identity,
   owner, realm, source, slot, and power agreement.
5. The common accepted-mechanics planner composes resources, scheduler, output, and all
   canonical after-images.
6. One validated handoff publishes or rolls back everything.

The provisional allocation is deterministic for the accepted input and is not visible
or reusable before finalization.

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
- Repair uses the same event, target, roll, maximum, and provisional semantic proposal.
- A changed snapshot or semantic event abandons the pending packet and requires a new
  opportunity.
