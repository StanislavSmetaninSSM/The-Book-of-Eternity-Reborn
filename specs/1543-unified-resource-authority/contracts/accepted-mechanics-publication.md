# Accepted Mechanics Planning and Publication Contract

## 1. One plan

Resource and effect mechanics share one `AcceptedMechanicsPlan`. Raw validation, companion validation, effect planning, publication, and post-validation consume the same plan instance. Random IDs are allocated once; no publication caller independently recomputes arithmetic, ordering, triggers, or identities.

## 2. Input composition

The input composer reads only bounded current and validated pending-turn snapshot state and constructs:

- resource definitions/state/history/commands;
- exact owner authority and same-turn owner after-images;
- ordinary/local/system internal mutations;
- effect commands, sources, targets, carriers, identity index, and due lifecycle events;
- pending resource-resolution requests and receipts;
- session/request/snapshot/turn/event authority;
- exact before-images/fingerprints for every authority and potential write.

Wrong-root, malformed, duplicate-property, missing-required, legacy, and mixed-realm input is represented as issues, never as absent pristine state.

## 3. Planning order

The planner performs the exact sequence in [data-model.md §9.3](../data-model.md#93-planning-sequence). Resource results are visible to later nodes only inside the immutable working plan. No file changes while planning.

The result is either one complete plan or bounded issues with zero after-images. Partial plan publication is impossible.

## 4. Cache binding

The validated cache key covers:

- session ID, request ID, snapshot token, realm, turn, accepted events;
- full canonical fingerprints for definition, owner, state, history, effect source, target, carrier, index, and pending catalogs;
- exact resource/effect command roots and validated internal mutations;
- same-turn owner/materialization plan fingerprints;
- exact protected before-images for every potentially touched path.

Any failed revalidation clears the validated handoff. A successful publication consumes it so a later commandless turn cannot reuse a stale plan.

## 5. Touched paths

The plan lists every resource canonical path, effect carrier/index path, owner companion, pending path, command path, and output companion it may create, replace, or delete. A path omitted from `TouchedPaths` cannot be written. Prior absence is an explicit before-image.

## 6. Lease preflight

Under one canonical write lease, immediately before the first write:

1. re-read every protected current path using duplicate-safe parsing;
2. distinguish missing file from present JSON `null`, empty, whitespace, malformed, or wrong-root file;
3. compare exact before-images and canonical authority fingerprints;
4. rebuild only read-only final owner/source/target catalogs required to prove the cached after-state still binds;
5. validate resource state/history agreement, effect carrier/index agreement, and every planned source/target/mechanics binding;
6. abort with zero writes on any difference.

## 7. Atomic publication

While retaining the same lease:

1. write owner companion after-images required for permanent identities;
2. write resource definitions/state/history;
3. write effect carriers/index and pending state;
4. write any other plan-owned companion after-images;
5. delete consumed resource/effect commands and resolved pending requests;
6. validate the complete canonical state;
7. on success, consume the validated plan cache;
8. on any failure, restore every touched path byte-for-byte or delete it if absent before the turn, then retain operator evidence.

Player narration/interface output cannot be accepted when canonical publication fails.

## 8. Repair

Protected issues include identity, replay, ambiguity, direct mutation, stale input, arithmetic, capacity, owner, source/target, cycle/expansion, pending/receipt, and TOCTOU failures. They do not create actionable GM repair.

Only one exact GM-owned semantic omission may yield a bounded repair request. Repair must name the in-world owner/command semantic, not a path or canonical field, and requires a coherent full-turn resubmission.

## 9. Validation phases

- Raw accepted-turn validation: command/legacy/direct-mutation/owner/source/event authority and one-plan creation.
- Pre-publication binding: exact cached plan plus current bytes/fingerprints under lease.
- Canonical post-seal validation: complete definition/state/history/owner/effect/pending agreement and no legacy authority.
- Full game-state validation: domain invariants, realm segregation, docs/example guards where applicable.
