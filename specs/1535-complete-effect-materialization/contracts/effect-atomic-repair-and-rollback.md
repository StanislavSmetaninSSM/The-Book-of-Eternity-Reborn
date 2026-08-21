# Contract: Effect Atomicity, Repair, and Rollback

**Feature**: [Complete Effect Materialization](../spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

## 1. Transaction Boundary

One accepted effect transition includes:

- transient `effectChanges[]` and `effectResolutionReceipts[]`;
- every touched active owner carrier;
- `effect_identity_index.json`;
- `pending_effect_resolutions.json`;
- canonical resource definition/state/history after-images and resource-event history produced by effect components;
- effect-owned companion state;
- source/target state changed by other accepted materializers;
- derived mechanics state if persisted;
- player narrative and interface output.

It is published only as one `AcceptedMechanicsPlan` and reuses the accepted-turn/afterlife canonical write lease, before-images, post-check, rollback snapshot, repair loop, session replacement guard, and stale-output detector. `EffectAcceptedTurnPlan` is a subordinate immutable proposal; no effect-specific cache handoff or writer may publish outside the common boundary.

## 2. Acceptance Sequence

1. Validate the pre-turn identity index and every non-empty active carrier.
2. Validate direct-mutation continuity against the rollback snapshot.
3. Compose exact source/target catalogs, including accepted same-turn plan exports.
4. Parse command operations, due events, and pending receipts.
5. Build the subordinate effect plan, allocate IDs once, and bind it into one cached `AcceptedMechanicsPlan` with the common resource reducer, event graph, pending state, and exact authority fingerprints.
6. Validate final resources/history, carriers, index, stacks, trigger graph, pending/terminal receipts, companions, and mechanics snapshot.
7. Capture exact before-images/existence for all touched paths.
8. Take the validated common plan once and publish all plan paths under one canonical write lease.
9. Re-read and validate the complete post-state and output freshness.
10. Commit only after every check succeeds; otherwise restore the full baseline.

## 3. Rollback Requirements

On ordinary operational failure:

- restore every pre-existing tracked file byte-for-byte;
- delete every newly created tracked file;
- restore deleted files byte-for-byte;
- remove transient command, pending, ready, receipt, and stale output artifacts owned by the rejected attempt;
- preserve operator diagnostic reports outside rollback cleanup;
- suppress narration/interface output from the rejected attempt;
- return a controlled failure so the outer lifecycle performs one caller-owned rollback.

`SessionReplacedException` or equivalent stale-session authority must propagate without allowing the old session to write into the replacement session.

## 4. Failure Injection Matrix

Tests inject one failure after each actually planned publication path for a representative composed transition, including:

1. transient command consumption;
2. player effect carrier;
3. NPC effect carrier;
4. enemy/ally combat carrier;
5. afterlife profile carrier;
6. spiritual-conflict carrier;
7. effect identity index;
8. pending bounded resolution;
9. effect-owned companion/derived state;
10. player narrative/interface output;
11. final post-state validation.

Each case compares the entire rollback-tracked set for exact bytes and existence and proves zero stale output.

## 5. Repair Eligibility

A bounded repair packet may be created only when all are true:

- validated rollback capability exists;
- baseline is restored before dispatch;
- one exact actor/source/target/raw coordinate exists;
- the error is one unambiguous GM-owned semantic omission;
- the source definition fixes legal values or exact bounds;
- no client-owned identity/route/history/receipt/index/control target is involved;
- the full rejected response can be coherently resubmitted;
- all raw repair target paths are allowlisted and none is client-owned.

Candidate repairable examples:

- missing readable effect description while exact source definition and mechanics are fixed;
- missing requested parameter whose source supplies a single exact value or narrow enum;
- incomplete declared visible counterplay text whose mechanical authority is already exact.

## 6. Protected Failures

Return no actionable packet for:

- missing, ambiguous, confusable, historical, or cross-realm source/target;
- GM-authored or changed effect/transition/receipt/index identity;
- stack coordinate/policy/count conflict;
- duplicate property or duplicate carrier occurrence;
- malformed client envelope, index, pending request, or repair wrapper;
- lifecycle event replay, stale receipt, trigger cycle, or unbounded expansion;
- direct canonical mutation;
- ambiguous owner or same-turn materialization plan;
- wound retargeting or effect-side wound mutation;
- unavailable/invalid rollback snapshot;
- write/post-check failure whose exact safe semantic correction is not proven.

Protected failure writes one operator-only path-bound diagnostic after/beyond rollback and shows one generic Russian in-world message to the player.

## 7. Packet Shape

An effect packet inside the existing validation repair request includes:

```json
{
  "kind": "effect_materialization_repair",
  "actor": "effect-apply:<exact-command-coordinate>",
  "route": "effectChanges",
  "rawCoordinate": "effectChanges[0].display.description",
  "expectedSource": {},
  "expectedTarget": {},
  "expectedDefinitionKey": "exact-key",
  "expectedEventRef": {},
  "exactFieldCorrections": [],
  "targetFiles": [],
  "fullTurnResubmissionRequired": true,
  "resubmissionObligations": []
}
```

The packet never contains permission to change `effectId`, source/target identity, realm, stack key/policy, max/current stacks, lifetime mode/current state, transition history, receipt/index state, or wound identity.

## 8. Coherent Retry

- Restore the pre-turn baseline before GM/worker dispatch.
- Retain an in-memory obligation describing the complete rejected response paths and exact effect operation actor/source/target/route.
- A ready marker alone cannot pass.
- A partial effect-only response cannot silently discard unrelated NPC, faction, item, quest, afterlife, narrative, or interface changes from the rejected response.
- Compare canonical JSON semantics, not only mtime or byte formatting, when proving resubmission freshness.
- Consume the obligation only after one complete coherent corrected response passes raw and composed validation.

## 9. Player Privacy

Player console/browser text must not include:

- validation, contract, materialization, repair, rollback, backup, harness, bridge, agent, or DTO vocabulary;
- exception messages;
- exact paths, codes, actor IDs, source/target authority, stack keys, receipts, or pending request data.

Technical details go only to logs and operator diagnostic reports. Ordinary failure copy remains stable, generic, and in-world.
