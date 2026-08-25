# Accepted Mechanics Planning and Publication Contract

## 1. One plan

Resource and effect mechanics share one `AcceptedMechanicsPlan`. Raw validation, companion validation, effect planning, publication, and post-validation consume the same plan instance. Random IDs are allocated once; no publication caller independently recomputes arithmetic, ordering, triggers, or identities.

## 2. Input composition

The input composer reads only bounded current and validated pending-turn snapshot state and constructs:

- the exact resource definitions/state/history/owner-authority quartet plus commands;
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

Any failed revalidation clears the validated handoff. Exact cache take consumes it
before the first publication write so neither rollback nor a later commandless
turn can reuse a stale plan.

Validated common-plan, effect-plan, and Mortal-item handoffs belong to the
physical canonical session root, not to one `FileSystemManager` instance. Every
manager for that root holds the same process-local `CanonicalRootIdentity`;
root interning uses the operating system's path comparison semantics, and the
weak registry does not keep an otherwise unused root alive. The registry exposes
only typed operations that require both the calling manager and its exact active
canonical write lease. It never returns a cache or root-owned mutable state to a
caller.

The root-owned handoff state is fenced by the exact persisted session-generation
value plus a monotonic in-process generation revision. That revision advances
only after a successful generation write or rollback deletion. Any difference in
either component replaces all three handoffs together. Consequently a publisher
may outlive the manager that built its plan, two different roots cannot share
authority, and a `G1 -> G2 -> G1` value cycle cannot resurrect the original G1
cache state. Generation I/O and registry access always occur while the same
canonical write lease remains held.

Every publisher receives one non-null discriminated
`AcceptedMechanicsNormalizationPreflight`:

```text
NoPlan
Validated(Plan, Binding, SnapshotBeforeImages)
```

`NoPlan` proves only that no common plan was available at initial preflight and
cannot authorize mechanics publication or item identity allocation. If plan or
command authority appears afterward, the cache is invalidated and the operation
fails. `Validated` carries the exact plan instance, its complete
`AcceptedMechanicsPlanBinding`, and the two accepted-snapshot byte before-images
captured during initial preflight. A nullable/optional plan, a later fresh cache
peek, or a caller-supplied bypass cannot substitute for this handoff.

## 5. Touched paths

The plan lists every resource canonical path, effect carrier/index path, owner companion, pending path, command path, and output companion it may create, replace, or delete. A path omitted from `TouchedPaths` cannot be written. Prior absence is an explicit before-image.

## 6. Initial normalization preflight

Under the owning canonical write lease and before any location, transfer, item,
or domain normalization, `Validated` initial preflight:

1. re-read every protected current path using duplicate-safe parsing;
2. distinguish missing file from present JSON `null`, empty, whitespace, malformed, or wrong-root file;
3. compare exact before-images and canonical authority fingerprints;
4. rebuild only read-only final owner/source/target catalogs required to prove the cached after-state still binds;
5. validate resource state/history agreement, effect carrier/index agreement, and every planned source/target/mechanics binding;
6. prove that the pending snapshot manifest and detached accepted authority bind the exact plan session, request, turn, and snapshot token;
7. capture immutable exact-byte before-images for those two files as `SnapshotBeforeImages`;
8. invalidate the cache and abort with zero writes on any difference.

The full plan before-image set is checked here exactly once. Normalizers may then
consume plan-owned transient before-images, so the final take gate does not
re-read that complete set.

## 7. Mortal item normalization authority

Still before location, transfer, or domain normalization, the accepted-turn item
authority cache is locked and copied into one immutable
`MortalItemAcceptedTurnNormalizationSnapshot` containing the exact session ID,
snapshot token, accepted turn, and creation-ref-to-permanent-item-ID allocation
map. The copy is accepted only when it matches the common plan binding.

Every item normalization receives exactly one typed mode:

```text
Validated(snapshot) | NoAcceptedBinding | ClientOwnedBootstrap
```

- `Validated(snapshot)` is the only accepted-turn identity authority. It uses the
  copied allocation map and never peeks the cache again or allocates a replacement.
- `NoAcceptedBinding` allows only non-materializing generic normalization. Raw
  GM-authored item, effect, or owner identity materialization fails closed.
- `ClientOwnedBootstrap` is available only through
  `NormalizeClientOwnedBootstrapAccumulatedStateAsync`. It is the sole route that
  may allocate a random Mortal item identity and must reject any validated common
  plan, pending snapshot manifest, detached accepted authority, pending mechanics
  authority, resource/effect command, or GM-authored materialization surface.

The public generic `NormalizeAccumulatedStateAsync` cannot consume raw
GM-authored identity materialization. Accepted production uses the common-plan
`Validated(snapshot)` route. GM and QTE production call sites cannot invoke the
bootstrap route. A missing, lost, or mismatched item authority snapshot fails
before any transfer or write begins; nullable bindings and boolean
random-allocation switches are not contract states.

Completed #1535 T042a/T047b supply one immutable acceptance-time deferred effect
continuation. QTE raw item/effect/owner materialization outside that sealed
continuation fails closed. The #1543 direct QTE resource producer is not
bootstrap and receives no bootstrap identity authority.

## 8. Final take and atomic publication

While retaining the same lease:

1. immediately before cache take, recompare only the captured exact bytes for the pending snapshot manifest and detached accepted authority;
2. on either byte difference, invalidate the validated cache and abort with zero writes; do not fresh-peek or rebuild publication authority;
3. call `TryTakeValidated(fs, exact Binding)` and require the exact same plan instance; this consumes the handoff before the first publication write;
4. write owner companion after-images required for permanent identities;
5. write resource definitions/state/history/owner authority;
6. write effect carriers/index and pending state;
7. write any other plan-owned companion after-images;
8. delete consumed resource/effect commands and resolved pending requests;
9. validate the complete canonical state;
10. on any failure after take, restore every touched path byte-for-byte or delete it if absent before the turn, keep the consumed cache invalid, and retain operator evidence.

Player narration/interface output cannot be accepted when canonical publication fails.

## 9. Repair

Protected issues include identity, replay, ambiguity, direct mutation, stale input, arithmetic, capacity, owner, source/target, cycle/expansion, pending/receipt, and TOCTOU failures. They do not create actionable GM repair.

Only one exact GM-owned semantic omission may yield a bounded repair request. Repair must name the in-world owner/command semantic, not a path or canonical field, and requires a coherent full-turn resubmission. For `resource_semantic_omission_repair`, root `fullTurnResubmissionRequired=true`; root `requiredResubmissionPaths` contains exactly the changed GM-authored command/output surfaces. Client-owned preparation/publication roots are never replay obligations: `system_mods.json`, `progression_schedule.json`, resource definitions/state/history/owner authority, `pending_effect_resolutions.json`, and `effect_identity_index.json` are restored or republished by the client and cannot be delegated to the GM.

## 10. Validation phases

- Raw accepted-turn validation: command/legacy/direct-mutation/owner/source/event authority and one-plan creation.
- Initial normalization preflight: exact cached plan, all protected before-images/fingerprints, exact session/snapshot binding, and capture of the pending manifest/detached-authority bytes under lease.
- Normalization authority: immutable cache-locked Mortal item allocation snapshot or explicit non-materializing/bootstrap mode before any earlier transfer or write.
- Final publication binding: only the two captured accepted-snapshot byte before-images, followed by exact cache take before the first publication write.
- Canonical post-seal validation: complete definition/state/history/owner/effect/pending agreement and no legacy authority.
- Full game-state validation: domain invariants, realm segregation, docs/example guards where applicable.

## 11. Breaking boundary

This is a breaking Pre-Alpha contract. There is no migration, compatibility
overload, nullable-plan fallback, fresh-peek fallback, legacy promotion,
dual-write path, boolean random-allocation escape hatch, or QTE/GM bootstrap
exception.
