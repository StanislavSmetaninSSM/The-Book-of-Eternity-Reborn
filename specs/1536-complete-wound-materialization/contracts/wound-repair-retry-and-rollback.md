# Contract: Wound Validation, Repair, Retry, and Rollback

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Fail-closed validation

Wound validation runs before canonical publication and rejects at least:

- malformed/unknown/duplicate fields or collection-limit violations;
- stale, duplicate, confusable, historical, wrong-owner, wrong-carrier, or cross-realm
  identities;
- missing accepted event/source/target/opportunity authority;
- severity outside the sealed maximum or an omitted guaranteed result;
- invalid location/profile/domain or forbidden physical/spiritual conversion;
- consequence count/power violations or hidden multi-mechanic packing;
- mismatched wound/effect reciprocal link, target, realm, source, or visibility;
- effect operations attempting to mutate a wound;
- incomplete/undiscoverable treatment or diagnosis routes;
- stale item/resource/skill/art/provider/facility/location/quest/effect/consent refs;
- insufficient Spiritual Healing tier for improvement;
- invalid treatment result, state regression, worsening, recovery tick, or healing;
- reuse of an opportunity, attempt, course milestone, cycle, transition, receipt, or
  terminal wound;
- competing after-image producers for one canonical root;
- missing before-image/snapshot/output authority.

An invalid proposal publishes no wound, effect, resource, scheduler, journal, quest,
history, narration, notification, or player projection change.

## Repair packet

`WoundRepairPacketBuilder` produces one bounded harness packet integrated with the
existing `validation_repair_request.json` contour.

```json
{
  "kind": "wound_materialization_repair",
  "candidateKind": "repair_wound",
  "sessionId": "exact-session",
  "requestId": "exact-request",
  "snapshotToken": "exact-snapshot",
  "candidateRef": "safe-pending-ref",
  "semanticFingerprint": "sha256:...",
  "issues": [
    {
      "path": "proposal.severity",
      "code": "wound_severity_above_opportunity",
      "expected": "none through II",
      "actual": "III"
    }
  ],
  "safeContext": {
    "event": "осколок после обвала",
    "target": "игрок",
    "realm": "Смертный мир"
  },
  "preservedProposal": {},
  "requiredResponseShape": {}
}
```

`candidateKind` is one of `construct_wound`, `repair_wound`,
`author_alternative_treatment`, or `narrate_acquisition`; it selects a closed preserved
payload and required response shape. Construction/repair candidates continue to require
one corrected `woundDecisions[]` entry. An alternative-treatment candidate instead
contains only the safe opaque `authoringRequestRef`, readable accepted-evidence context,
non-offending route/path content, and exact semantic issue paths. Its required response
shape is exactly:

```json
{
  "woundTreatmentAuthorings": [
    {
      "authoringRequestRef": "safe-opaque-request-ref",
      "decision": "author",
      "route": {},
      "diagnosisPath": null
    }
  ],
  "response": "complete accepted scene"
}
```

For `decision=decline`, both payloads are null. For `decision=author`, route is one
complete append-only route; `diagnosisPath` is one complete reachable path exactly when
the route is hidden and null otherwise. The retry cannot submit a construction
`woundDecisions[]` entry, alter an existing route/path, or change the bound evidence
event. The client retains before/route/path/authority fingerprints and the operation key
only in the opaque pending authority; none are exposed in the packet.

### Packet privacy

The packet includes only exact offending semantic paths, legal closed shapes/ranges,
safe readable context, and preserved non-offending authored content. It excludes:

- permanent wound/effect/owner/resource identities;
- hidden target/private NPC facts;
- source and resource seals;
- alternative-route before/route/path/authority fingerprints and operation keys;
- unrelated response content;
- permissions to rewrite the accepted event, roll, target, opportunity, or snapshot.

### Bounded repair

- At most 64 candidates per pending wave and repository-standard retry limit.
- A repair submission addresses the exact candidate/receipt once.
- Unchanged valid sibling content remains byte/semantic equivalent.
- A changed semantic authority invalidates the repair packet.
- New unrelated gameplay is not processed as part of a repair turn.
- If the GM cannot provide a complete legal proposal, the safe resolution is to decline
  an ordinary opportunity or cancel/roll back; a guaranteed result remains unresolved
  until repaired.

## Cache and handoff authority

Wound preparation/final plans are generation-scoped in
`AcceptedTurnAuthorityRegistry` and invalidated with effect/common plan authority. The
cache fingerprint includes:

- session/request/snapshot/realm/turn;
- accepted event and opportunity roots;
- wound command/proposal/pending/receipt inputs;
- owner/carrier/index/history roots;
- effect source/target/carrier/index roots;
- resource/item/capability/provider/location/quest authority;
- progression schedule/cycle state;
- player output/narration authority;
- every exact before-image.

A cache hit is legal only for exact equality. Any mismatch discards the plan rather
than partially recomputing it against stale provisional identities.

The input/cache fingerprint above is distinct from the prepared-result seal. Internally
computed `WoundPreparationFingerprint`, `EffectInputFingerprint`,
`EffectAcceptedTurnPlanFingerprint`, and `WoundFinalPlanFingerprint` values bind each
staged handoff. `AcceptedMechanicsPlan.PreparedPlanFingerprint` then binds those seals,
all subordinate result maps and after-images, final wound root bindings, resources,
paths, pending/scheduler state, and output. It is not accepted from a constructor caller;
the cache independently recomputes it from detached plan data during validation and
take/peek. A cross-plan source export, operation event, result, after-image, or final
binding therefore invalidates the whole handoff.

## Retry identities

| Operation | Unique replay coordinate |
| --- | --- |
| opportunity decision | opportunity + event + owner + decision |
| creation/worsening | event + source + target + wound/provisional ref |
| diagnosis attempt | attempt ID + wound + diagnosis path + sealed check result |
| alternative-route authoring | authoring request + wound + evidence event + operation key |
| procedure/healing attempt | attempt ID + wound + healer + target + sealed roll |
| course milestone | course ID + milestone ordinal + due event |
| natural recovery | wound + exact clock/cycle key |
| provider payment | attempt ID + sealed quote/compensation agreement |
| effect transition | wound transition + consequence slot + effect operation |
| history row | wound + transition ordinal + operation key |

Replay returns the already accepted semantic result where a user-facing receipt is
needed, but it creates no second state mutation, roll, charge, cycle, effect,
notification, or history row.

## Snapshot coverage

The pending-turn snapshot and common plan before-images cover every touched path,
including:

- all wound carriers, index, history, commands, pending occurrence/resolution roots, and
  append-only opportunity-decision receipts;
- effect carriers, definitions/source authority, identity/history, and pending root;
- resource definitions/state/history/owner authority/commands/pending;
- Mortal inventory/item identity and affected characteristics;
- actor/profile/Guardian/Shining roster and provider state;
- spiritual-conflict state and progression schedule/control/report;
- referenced locations, quests, journals, and accepted agreements;
- final game response, player output, and GM debug/repair output used by the turn.

The exact set is derived from the plan, not a fixed partial allowlist. Arbitrary paths
remain forbidden.

## Publication and rollback

1. Acquire the owning canonical write lease.
2. Revalidate plan handoff and exact snapshot before-images.
3. Take the validated plan exactly once.
4. Compose typed same-root after-images and deterministic write order.
5. Write canonical files atomically under the lease.
6. Delete only declared consumed/pending paths.
7. Read back and validate exact after-images/cross-root agreement.
8. Accept the turn only after all validation succeeds.

On any exception, mismatch, crash recovery, or rejected post-validation, invalidate all
handoffs and restore the complete pending snapshot. No per-file success is considered a
committed wound transaction.

## Required failure tests

Tests must inject failure before/after each stage and prove byte/existence restoration:

- provisional wound identity allocation;
- effect plan creation/finalization;
- resource/item reservation and provider charge;
- wound carrier/index/history write;
- same-root afterlife profile composition;
- progression-cycle outcome;
- history/receipt append;
- player response/notification publication;
- pending root consumption and post-publication agreement.

Every test also asserts no duplicate result after exact replay.
