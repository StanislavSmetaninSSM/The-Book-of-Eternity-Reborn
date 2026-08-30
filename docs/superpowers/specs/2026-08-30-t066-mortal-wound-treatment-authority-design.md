# T066 Mortal Wound Treatment Authority Design

**Tracked work:** GitHub issue #1536, Spec Kit task T066

**Depends on:** completed T060 transient requirement resolver and T065 treatment contract

**Defers to:** T067 resolution/history orchestration, T068 resource reservations, T069 deterioration, and T070 publication

## Goal

Create one lease-bound, immutable authority layer for Mortal wound treatment attempts. It must prove the current wound and actor selection plus every common or course-milestone requirement without accepting caller-authored JSON, IDs, fingerprints, success flags, or mutation authority. Canonical skill capability proof is required only for guaranteed/export paths; canonical world-time and course authority are required only for course paths.

T066 must preserve the completed three-argument `MortalWoundTreatmentAuthority.ResolveRequirements(route, context, snapshot)` surface and its resolved-row shape. The new layer composes trusted inputs for that resolver and records enough typed evidence for later request/history replay; it does not resolve dice, reserve resources, apply outcomes, or publish state.

## Selected architecture

Use layered immutable authorities around the frozen T060 resolver surface. Its
three-argument API, result shape, diagnostics, precedence, and fingerprints remain
unchanged, while its existing per-kind evaluation internals are extracted once so both
that adapter and the new typed witnesses use the same evaluator and cumulative ledger.

The rejected alternatives are:

1. Putting accepted-state reads, canonical skills, course witnesses, and fingerprints into the existing `MortalWoundTreatmentAuthority.cs`. This reduces file count but turns the already large transient resolver into a second canonical-state and lifecycle owner.
2. Generalizing all skill materialization under #1533 and wiring the T070 publication transaction first. This could eventually share more code, but it expands T066 across two later dependency boundaries and makes wound treatment wait on unrelated skill publication work.

The selected design keeps each trust transition explicit and independently testable:

1. canonical skill extension validation and source selection;
2. accepted-state export under one canonical lease;
3. deterministic attempt/time/course coordinates;
4. scoped requirement witnesses and bundles;
5. current and final-composed capability proof export.

## Components

### Canonical skill capability contract

Add one shared strict parser/catalog for the optional `mortalWoundTreatmentCapabilities[]` extension on current player and NPC active/passive skill rows. Player and NPC validators, the skill normalizer, accepted-state composition, and the proof exporter must use the same contract.

Ordinary skills without the extension remain unchanged and may retain their pre-#1533 shape. An extension-bearing skill must have a permanent `skillId`; that ID is exact and case/Unicode-confusable unique across the actor's current active and passive skills, including siblings without the extension. Capability refs are likewise exact/confusable unique across both skill kinds for that actor.

Version 1 accepts only `woundDomain=physical`, severity ranks 1-4, the closed operation-limit shape, the registered wound complication kinds, checked signed 32-bit limits, and the existing aggregate operation/legacy bounds. It rejects unknown fields and an all-zero capability. Normalization preserves a valid extension exactly; it does not infer, repair, rename, or synthesize one.

The exact selected extension-bearing skill is also the only source of the unchanged-shape T060 `actors[].capabilities[]` row. The composer projects only `capabilityRef`, diagnostic `displayName`, `lifecycle`, and `active`; it never projects guarantee limits or proof fingerprints into T060.

### Lease-bound accepted state

Add `MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(FileSystemManager, CanonicalWriteLease, MortalWoundTreatmentAuthority.Context, string woundId)` as the sole treatment-admission boundary.

Under the supplied live canonical lease it must:

- validate the parsed Mortal selection against current player/NPC/combat actor identity and location-source integrity, plus accepted promotion authority when such evidence is present, while retaining current reachability, consent, availability, and co-presence as predicate evidence rather than requiring every value to be positive;
- derive the current `WoundAcceptedTurnBinding` and accepted events from production pending-turn/registry authority rather than accepting either from the caller;
- parse the selected wound through its current carrier, identity index, and complete history;
- read canonical world time, player/NPC skill roots, accepted effect mechanics, and all eleven T060 requirement sources;
- compose detached T060 `Context` and `Snapshot` values, including the ordinary capability row projected from the exact selected canonical skill;
- retain the detached typed T065 `MortalWoundTreatmentDefinition` selected from the
  current wound so later coordinate/course factories never reparse a raw route;
- seal semantic source, context, wound, history, and accepted-state fingerprints.

The result contains exactly `IsValid`, frozen issues, and nullable authority. The authority has no public constructor and exposes no `JsonNode`, `JsonElement`, `JsonDocument`, dice collection, mutation, reservation, absolute-root identity, or caller fingerprint. Runtime root/revision data may remain private admission state; durable fingerprints use stable semantic coordinates so a cold copy to another root remains replayable.

`AcceptedTurnAuthorityRegistry` stores the lease/root-generation-scoped accepted-state
entry using the same guarded slot pattern as existing common/effect/wound authorities.
The exporter first composes and validates a complete candidate from the current signed
snapshot, binding/events, wound/carrier/index/history, clock, effect, item/resource,
actor/location, and skill source semantics. Only then may the registry compare the
candidate's complete typed semantic key and authority fingerprint with its cached entry.
An exact retry reuses equivalent sealed state; changed live revision or any candidate
semantic fingerprint replaces/invalidates the entry instead of returning stale
authority. A cache hit is never decided from only context/wound IDs and never skips live
candidate validation.

Malformed, ambiguous, or structurally incomplete canonical selection evidence fails closed. A structurally valid negative predicate such as withdrawn consent, insufficient quantity, or an unreachable provider remains detached snapshot evidence for requirement classification; it is not itself an accepted-state parser error. Tests may complete source fixtures when they currently omit required location, actor lifecycle, or co-presence roots; production must not default or infer those facts.

### Attempt, time, and course authorities

Add the T066-owned immutable coordinate and course types described by the feature data model. A narrow partial `MortalWoundTreatmentPlanner` shell may own only `CreateAttemptCoordinates`; all attempt resolution remains T067.

`CreateAttemptCoordinates` selects the route from the sealed wound, resolves the accepted event exactly once, validates current wound/history agreement, validates and seals the supplied operation key, and derives `AttemptId` and `CoordinatesFingerprint` from the complete semantic coordinate set. Durable operation/attempt/course/event reuse and collision detection remains T067-B history/replay authority. The factory accepts no attempt ID, course ID, band, result, die, or terminal flag.

`MortalWoundGameTimeAuthority.Create` reads only the accepted state's canonical clock.
`MortalWoundCourseModeAuthority.Create` selects the typed
`MortalWoundCourseRouteDefinition` from the accepted state's detached T065 treatment
definition and uses `route.Resolution.MaximumGapMinutes`; it owns checked first-course
start/window construction, the immutable starting-wound authority, due/deadline bounds,
and exact course-coordinate binding. It also proves agreement between that typed route
and the raw T060 route used by requirement evaluation. T067-B later reconstructs
continuation mode from durable history and orchestrates it; T066 does not parse or
persist treatment commands/history.

This resolves the apparent task split as follows: T066 supplies the complete types, first-course construction, pure coordinate validation, and the requirement classifier for any valid supplied course-mode authority. T067-B supplies continuation-history reconstruction and high-level request/resolution flow.

### Scoped requirement authority bundle

Add `MortalWoundTreatmentRequirementAuthorityBundle` with these factories:

- `CreateForProcedure(acceptedState, coordinates, before)`;
- `CreateForGuaranteed(acceptedState, coordinates, before)`;
- `CreateForCourseMilestone(acceptedState, coordinates, before, history, courseMode)`.

Every factory selects its route and scope internally. No overload accepts a requirement
array, context/snapshot, resolved row, witness, or fingerprint.

Extract the existing T060 per-kind selection and predicate logic behind one internal
structured evaluator. Its observation result distinguishes `Satisfied`, trusted
`Unsatisfied`, and `InvalidAuthority`, and contains the exact mechanical fields needed
by the unchanged resolved-row writer plus the closed success/failure observation. The
legacy three-argument T060 method is a strict adapter over that evaluator: it preserves
its current all-or-fail result, diagnostics, issue precedence, row shape, and result
fingerprints byte-for-byte. It is not a second implementation.

Evaluation accepts an internal batch of ordered scopes and owns one cumulative
item/resource ledger for the whole batch. Procedure and guaranteed factories evaluate
their single common scope. Course classification evaluates common and exact milestone
scopes in one batch, so cumulative quantity cannot be reset or overbooked at the scope
boundary. Witnesses are built directly from the same typed observations; they never try
to reconstruct missing mechanical evidence from the lossy public T060 result.

Procedure and guaranteed factories require a fully satisfied common scope. Course classification evaluates the common scope and exact milestone scope separately. Each authored local requirement index appears once as either a binding or a failure witness. A successful binding retains the unchanged T060 resolved row plus a kind-complete immutable success witness; a failure retains a closed kind/reason-specific observation sufficient to distinguish trusted predicate loss from broken authority.

Only a valid, unambiguous current observation that no longer satisfies a predicate yields `Unsatisfied`. Proven absence of the exact referenced row inside a complete valid canonical source set is such an observation and emits `authority_absent`. A missing/corrupt authority root, cross-realm or ambiguous candidate, malformed state, changed coordinates or route/history/course seals, or unrecomputable T060 row yields `InvalidAuthority`. `InvalidAuthority` has no bundle. `Satisfied` and `Unsatisfied` return a complete sealed bundle; T067 later decides whether a trusted course loss produces interruption.

A structurally valid row with retired lifecycle or `active=false` is current negative
predicate evidence and maps to the closed `retired` or `inactive` failure reason; it is
not confused with a stale lease, snapshot, or carried seal, which is invalid authority.

Extract or add one internal T060 resolved-row fingerprint writer so direct resolution,
witness reconstruction, future history parsing, and bundle sealing recompute the same
bytes. The writer and structured evaluator are the only per-kind mechanics path. Do not
change the T060 public result shape or add a second resolver dialect.

For ordinal 1 with no active course, trusted `Unsatisfied` returns its complete bundle
but leaves `InterruptionReason` null; T067 rejects that course start. A reason is present
only for a validated continuation whose sealed starting authority agrees with the
already-active course. There `deadline_exceeded` dominates
`requirements_unsatisfied`; T067-B remains responsible for accepting the interruption.

### Canonical capability proof

Add `MortalWoundTreatmentCapabilityAuthority.ExportCurrent(acceptedState, coordinates, capabilityRef, actorRole)`.

The exporter derives the owner from the exact role binding, permits only canonical player/NPC skill sources, selects exactly one current active/passive skill plus capability, and emits an immutable detached proof containing only the closed fields frozen in `data-model.md`. A provider-owned player/NPC capability may treat a combatant or combatant member. `actorRole=target` requires the target to be a canonical player/NPC owner first, using the existing accepted persistence/promotion path when it began as a combat coordinate; accepted-state export does not require promotion for every combat target. Display text is excluded from semantic fingerprints. Mechanical source changes alter the source/proof fingerprint; display-name changes do not grant or change authority.

Add the five-argument `ExportForPublication` seam in T066. It reads the selected final
skill root from a validated `AcceptedMechanicsPlan` only when that root is touched,
otherwise from the lease-bound current root. If the selected path is declared in
`TouchedPaths`, the exact validated root must exist in `OwnerCompanionAfterImages`;
missing, duplicate, foreign, or malformed after-image authority is
`InvalidAuthority`, never a fallback to current state. It revalidates the selected
publication source against the baseline source semantics sealed by `acceptedState` and
`coordinates`, rejecting mechanical drift with
`mortal_wound_treatment_capability_publication_mismatch`. T070 separately compares the
returned proof with the request's sealed `ModeAuthority`.

T066 owns the five-argument API and revalidation algorithm. Existing end-to-end publication tests remain intentionally RED until T070 supplies the validated treatment plan. Do not add a detached or test-built plan; add an independent T066 control only if an already-existing production builder is demonstrably usable without taking T067, T068, or T070 ownership.

## Error and fingerprint rules

- All public-internal entry points return closed typed result objects with frozen issue arrays and nullable authority; malformed state must not throw.
- Exact identifiers use the shared `ExactIdentifierConfusableKey`; no display-name lookup or case-insensitive fallback is authority.
- Fingerprint writers are versioned, ordered, and use checked numeric conversion. Display text, absolute filesystem root, and process-local registry identity are excluded.
- Detached outputs retain no mutable JSON or caller-owned collections.
- Canonical validation failure never becomes a merely unsatisfied treatment predicate.
- No migration, compatibility reader, raw write intent, GM-authored proof, or inferred capability is introduced.

## Test strategy and ownership

The first exact RED is:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~BookOfEternityClient.Tests.MortalWoundTreatmentResolverTests.AcceptedStateExport_UsesParsedSelectionAndCanonicalRootsToDeriveItsProductionBinding"
```

The broader canonical capability baseline currently executes 74 rows: 6 infrastructure controls pass and 68 expected future rows fail in `20260830-141038-238-38192-00d173f07cbb4c6cac51784766b64c43-focused`.

Implementation proceeds through independent RED-to-GREEN slices:

1. strict player/NPC extension validation, cross-active/passive identity, and preservation;
2. accepted-state export and canonical source-fault rejection;
3. attempt coordinates, canonical time, and first-course authority;
4. shared structured T060 evaluation/ledger and fingerprint reuse, common success
   witnesses, course failure witnesses, and bundle factories;
5. current capability proof export;
6. publication revalidation seam without T070 transaction ownership.

At every checkpoint retain the completed T060 authority class green, then run the smallest affected T066 method filters. Run one meaningful Fast after the completed T066 aggregate checkpoint. T067/T068/T069/T070 RED tests must be listed by owner rather than weakened, skipped, or relabeled as T066 success.

## Documentation boundary

T066 adds client-owned canonical validation and authority surfaces but no reachable player command, GM response field, pending/control file, player projection, or publication path. Full Mortal GM guidance and worked examples remain T072-T074. Chaos Sea and Shining Abode contracts are unchanged, so afterlife matrix/example/manifest files require no T066 update.
