# Phase 0 Research: Complete Wound Materialization and Healing

**Feature**: `1536-complete-wound-materialization`  
**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)  
**Research date**: 2026-08-26

This document resolves the technical choices required to implement the approved
specification. It is intentionally a direct replacement of the loose pre-alpha wound
shape; no legacy save migration or compatibility reader is required.

## R-001: One common kernel with realm adapters

**Decision**: Introduce one closed wound contract, identity/history authority, and
accepted-turn planner. Put setting-specific occurrence, location, treatment, recovery,
and consequence validation behind Mortal and afterlife adapters.

**Rationale**: Stable identity, replay protection, archive rules, effect ownership,
atomicity, and player projection are invariant. Mortal wounds need freely authored
world-specific care, while spiritual wounds need closed strain, art, and safe-cycle
rules. Sharing only the invariant kernel avoids both duplicated lifecycle code and a
false universal damage model.

**Alternatives rejected**:

- Two independent systems: duplicates identity, history, rollback, and UI semantics.
- Model wounds as effects: makes effect removal capable of silently healing a wound.
- One universal treatment/damage table: erases the setting-specific Mortal design.

## R-002: Extend the accepted-mechanics transaction

**Decision**: Compose wounds as a typed input/output inside
`AcceptedMechanicsPlanningContext` and `AcceptedMechanicsPlan`. Use a staged
`WoundAcceptedTurnPlanner.Prepare -> EffectAcceptedTurnPlanner ->
WoundAcceptedTurnPlanner.Finalize` contour: preparation validates opportunity and
owner authority and exports a complete bounded wound source-definition graph plus only
the directly requested root applications as a typed batch of correlation refs; the
effect planner remains the sole allocator of opaque permanent effect IDs, materializes
those roots under distinct derived operation events, and returns the exact result map;
finalization proves exact reciprocal root links and consequence budgets. Reaction-only
descendant definitions persist in the graph without an application ref or preallocated
identity and are resolved later through the indexed exact wound source. A valid plan carries
wound carrier after-images, identity-index after-image, append-only history after-image,
wound-owned effect changes, resource mutations, scheduler outcomes, and pending/receipt
state under one input fingerprint and one canonical write lease.

**Rationale**: `AcceptedMechanicsPlanner`, its validated handoff/cache, exact
before-images, and `CanonicalStateNormalizer.AcceptedMechanics` already provide the
atomic boundary for resources and effects. A parallel wound normalizer would introduce
write-order races and partial commits. The staged contour resolves the otherwise cyclic
dependency between wound source allocation and effect-link validation, while all
after-images still publish together.

The handoff uses a per-source `SourceExportFingerprint`, then distinct
`WoundPreparationFingerprint`, `EffectInputFingerprint`,
`EffectAcceptedTurnPlanFingerprint`, and `WoundFinalPlanFingerprint` stage seals. The
common `AcceptedMechanicsPlan.PreparedPlanFingerprint` is computed only after semantic
agreement and seals every stage, exact subordinate result map and after-image, final
wound binding, resource result, path set, pending/scheduler state, and output binding.
It is never accepted as a caller-supplied constructor value; the cache independently
recomputes it from the detached plan before validation and on take/peek. This makes
cross-plan mixing of otherwise valid stage objects fail closed. A newly created wound
is selected by same-turn `sourceRef`, never by its not-yet-published permanent source ID.
Permanent effect IDs are random inside the cached effect plan, as required by #1535, and
are never deterministically derived from GM input.

Each earlier consumer also recomputes its predecessor seal from detached payload rather
than trusting a fingerprint property: composer for source export/wound preparation,
effect planner for effect input, and wound finalizer for the effect plan.

Direct-root materialization agreement uses no universal numeric power. Prepare derives
`ExpectedComponentCount` and `ExpectedMaterializationFingerprint`; the effect result
derives `ComponentCount` and `MaterializationFingerprint` from the actual created active
effect, and Finalize recomputes that value from the detached effect after-image. The
version-1 fingerprint domain is
`book_of_eternity.wound.effect_materialization`. Its UTF-8 length-prefixed SHA-256 input
is domain/version, exact source realm/kind/id/definition key, effect schema version,
recursively canonical compact application parameters, component count, and every
fully bound component's original ordinal plus recursively canonical compact JSON.
Object keys sort ordinally while component array order remains semantic. The digest is
internal handoff authority, not canonical wound state or player output. Catalog
validation remains the heterogeneous severity/power authority.

**Alternatives rejected**:

- A standalone wound normalizer after effects: cannot prove bidirectional ownership
  before effect publication.
- Direct mutation in `ValidationService`: validation must not write canonical state.
- Best-effort multi-file writes outside the current lease: violates rollback and retry
  requirements.
- A decimal power agreement: no canonical producer exists for heterogeneous numeric,
  string, cadence/resource-relative, restriction, and reaction profiles, so any mapping
  would be lossy and arbitrary.
- A per-profile power-evidence union in the effect handoff: duplicates T019 policy and
  contextual evidence in T020 while still only echoing Prepare's validation; exact bound
  materialization equality is the narrower cross-stage authority.

## R-003: Dedicated identity/history roots and owner-appropriate carriers

**Decision**: Add these common roots:

- `game_state/wounds/wound_identity_index.json`
- `game_state/wounds/wound_history.json`
- `game_state/wounds/wound_commands.json`
- `game_state/control/pending_wound_resolutions.json` when a bounded GM decision is
  still required.

Active wounds remain in owner carriers:

- Mortal player: `game_state/player/wounds.json`;
- named NPCs: new `game_state/npcs/npc_wounds.json`, never `npc_effects.json`;
- anonymous Mortal combatants: `activeWounds[]` inside the exact existing
  `game_state/combat/enemies.json` or `allies.json` combatant;
- afterlife player and persistent actors: `activeWounds[]` in their exact accepted
  profile in `game_state/meta/afterlife_entity_profiles.json`; Shining roster roles
  remain in `game_state/meta/guardian_abode_residents.json` and bind to those profiles
  by exact actor identity;
- the active spiritual-conflict root references an opportunity/result, not a duplicate
  persistent wound.

**Rationale**: This mirrors the proven effect-carrier approach while removing the
current NPC wound/effect entanglement. The index proves one occurrence per active
identity. The history root preserves immutable transition and terminal replay evidence
without turning every owner file into an ever-growing log.

**Alternatives rejected**:

- One global mutable wound array: creates a hot file and weakens owner lifecycle rules.
- History embedded only in active wounds: healed removal would erase replay authority.
- Continue writing NPC wounds to `npc_effects.json`: violates wound/effect independence.

## R-004: Client-owned identity and exact owner coordinates

**Decision**: Reuse the exact-identifier, confusable rejection, owner-resolution, and
promotion patterns established by effects/resources. The GM supplies a temporary
semantic wound proposal reference only. The client allocates `woundId`, transition IDs,
attempt IDs, course/cycle keys, ordinals, fingerprints, receipts, and history rows. The
effect planner, not the wound planner, allocates all permanent effect IDs.

Supported owner kinds are closed: Mortal player, named NPC, Mortal combatant or group
member, player soul, Guardian, Shining resident, radiant actor, and another accepted
afterlife profile. Realm and owner coordinates must match the sealed event and linked
effect target. Anonymous combatant wounds move only through the existing accepted
combatant-persistence transition.

The closed effect-target adapter maps `player_soul` to #1535 `player`,
`combatant_member` to `combatant`, and every other wound owner to its identically named
effect target. It chooses `targetRef` only for an accepted same-turn target and
`targetId` for an already stable target.

**Rationale**: GM-authored permanent IDs or name lookup would permit replay,
cross-realm retargeting, and ambiguity. Existing effect target authority already proves
the necessary owner identities and promotion semantics.

## R-005: A wound opportunity is a sealed maximum, not an automatic injury

**Decision**: Every accepted Mortal producer reduces a harmful exact formal, QTE,
combat, trap, check, hazard, or narrative result to one immutable complete occurrence-
candidate batch; a harmless result produces none. The source-result common accepted
plan checks stable producer operation keys against pending occurrences and durable
consumed receipts, publishes only new candidates to the client-owned pending root in
sealed batch-ordinal order, and the next active pending-turn snapshot seals their bytes
before exposing an opportunity.
Correlation-only source input resolves exactly
one such occurrence; current source state, combat membership, a hazard, dice, or prose
cannot prove that the event occurred. It may repeat only public correlation and never
the hard maximum, guarantee, binding, event authority, receipt, or after-image. The
occurrence seals its exact source snapshot token. The client first verifies the stored
historical event vector under those source coordinates, then rebinds the same complete
ordered coordinate vector to the active decision snapshot during initial composition
and validation. Selected wound semantics remain derived while generic sibling seals are
recomputed. It then derives a
`WoundOpportunity` containing exact event/target/realm/domain evidence, legal severity
range, profile, and any guarantee. Absence of `worseningTarget` means create; worsening
requires one explicit exact active-wound coordinate. The GM may choose none or any
severity up to the maximum for ordinary opportunities. Only a previously materialized
guaranteed trigger requires a wound. The GM also supplies the readable acquisition
narration; the client adds an explicit notification and rejects contradictory
narration/state.

Every accepted `none` or `materialize` decision appends one client-owned receipt and
consumes the occurrence through the same atomic accepted plan. While the same active
pending-turn snapshot is retained, exact cold replay uses its signed pre-consumption
occurrence plus the current append-only receipt/pending partition and emits no command
or transition; changed semantics conflict. A newer snapshot makes the old correlation
stale. A decline has no wound-history transition but remains durably auditable through
its receipt.

**Rationale**: This preserves GM narrative judgment and prevents the client from
inventing wounds. The signed occurrence prevents an available source from being
mistaken for an event that actually happened; the complete event-set seal prevents
sibling-event tampering; the durable receipt closes restart replay even for a decline.
It also makes a guaranteed skill/item/source promise enforceable.

**Alternatives rejected**:

- Always create the computed maximum: turns every eligible transition into deterministic
  injury and breaks training/fictional context.
- Let prose alone decide legality: cannot validate severity, owner, or guaranteed
  outcomes.

## R-006: Closed wound envelope, no wound catalog

**Decision**: Parse a versioned closed proposal containing domain, explicit
`locationProfile`, readable nature/display, severity I-IV, care/recovery state,
complications, consequences, and treatment/diagnosis routes. Validate structure and
registered primitive bounds; do not look up the wound name in a predefined catalog.

`locationProfile.kind` is closed (`anatomical`, `systemic`, `mental`,
`spiritual_axis`, `other`) while its readable locus remains setting-authored. An exact
body-part or spiritual-axis reference is optional and must resolve when present.

**Rationale**: The client needs a finite data language it can validate, while the GM
must remain free to create injuries appropriate to arbitrary Mortal universes and the
fixed afterlife setting.

## R-007: Reuse the effect engine with strict wound ownership

**Decision**: Every mechanical consequence is a normal #1535 effect with
`source.kind=wound` and the exact `woundId`. The wound stores one mandatory bounded
`ownedEffectSources` value: the complete detached definition graph plus separate
`effectId -> definitionKey` bindings for roots materialized directly by a wound
transition. A reaction-only descendant persists as a definition but has no root binding
or preallocated identity. Admission requires exact bidirectional owner/realm/source
agreement. Direct roots have unique stack coordinates and each must produce a new effect
identity. Effect operations cannot mutate wound state. Severity rematerialization first
terminates the complete old active/suspended wound source group and then creates every
new root; full healing finds and terminates all active roots and descendants through an
indexed exact wound-source coordinate.

Wound source exports set `Materializable = false`, so ordinary GM `effectChanges[]`
cannot apply graph definitions. The sealed typed batch is a one-shot internal allowlist
for exact roots; downstream definitions remain reaction-only. Every graph definition
has a unique stack key and exact `maxStacks = 1`, with the remaining #1535 stacking
combination intact, bounding simultaneous source-group membership without discarding
refresh/replace/merge semantics.

Version 1 permits zero or one leaf `apply_definition` expansion and no nested wound
expansion. When present, its #1535 `maxExpansion` is exactly 2 (the reaction plus the
reachable leaf), while it consumes one wound expansion budget. A root-bound reaction
target must use the exact legal `replace` policy and share the producer's reconstructed
root-ownership domain. The planner derives each root's ephemeral domain as `base_wound`
or its exact `complicationId`; descendants inherit their producer's domain, and
cross-domain stack/refresh/merge/replace is rejected before mutation. A reaction-created
identity records the producing effect in its first create transition's
`sourceEffectIds`; replacement succession remains separate non-ownership lifecycle
evidence. Complication-owned root sets are pairwise disjoint; selective cleanup traverses
only that same-source, same-domain first-create lineage from declared roots, including
terminal roots, and visits each source-group identity once.
It removes the active complication, its root bindings and reciprocal slots, recomputes
slot use, and prunes only definitions unreachable from every remaining root. Terminal
provenance remains in the global effect identity history; an active effect that would
lose its source definition rejects the transition.

Mortal severity I-IV allows at most 1/2/3/4 independent consequences. Spiritual
severity I-IV requires exactly 1/2/3/4 consequences. Each consequence uses one
registered component/profile and consumes one slot; display text and linkage consume no
slot. Profile-specific severity envelopes cap magnitude, cadence, duration, scope, and
action restriction. The closed version-1 profile list and exact per-severity limits are
normative in `contracts/wound-effects-and-atomicity.md`; they include every permitted
generic Mortal component and eight fixed-setting spiritual axes. A Mortal wound with no
mechanical slot still requires an active complication or care/recovery constraint that
changes its legal lifecycle.

The eight spiritual axes are registered directly as deterministic first-class #1535
component profiles with only `profile_specific` merge and a closed
`{ operation, axis, magnitude }` payload. They are persistent
actor effects in afterlife realms, require an exact wound source link, and cannot target
`spiritual_conflict_side`. They are deliberately distinct from
`afterlife_combat_condition`, whose payload, finite conflict lifetime, side carrier,
and `combatConditions[]` projection describe a transient conflict condition. Runtime
conflict mechanics later derive a typed owner-to-current-side contribution without
creating another canonical condition or changing the persistent payload.

**Rationale**: This preserves one mechanical effect engine while keeping wound
lifecycle authority separate. Persisting only slot effect IDs would lose a legal
zero-slot marker and the definitions needed by later `apply_definition` reactions;
preallocating every descendant would instead create effects before their trigger. The
two-layer graph/root model avoids both failures. Exact slot counting blocks a GM from
packing several mechanics into one prose object.

## R-008: Mortal diagnosis and treatment are materialized routes

**Decision**: Require every Mortal wound to have at least one mechanically complete
treatment route. Hidden routes require a reachable diagnosis path. Route-local
requirements are AND; routes are OR alternatives. Reference exact accepted items,
resources, capabilities, skills, providers, facilities, locations, clocks, and
environmental conditions.

The intra-wound discovery graph is explicit rather than inferred from prose or world
names. Diagnosis paths carry typed `requiresKnownFacts[]` and `reveals[]`; the client
computes a least fixed point seeded by already known routes and public/player-known
complications. Hidden paths require a non-empty satisfied prerequisite, GM-only paths
never prove player reachability, and self/unseeded cycles fail closed. Exact external
world reachability is a separate fresh authority proof so structural parsing does not
scan mutable canonical roots.

Diagnosis attempt evidence has one sealed `success|failure` result. Success reveals the
complete declared fact set; failure reveals none and remains terminal/retry-safe. A
later cure uses its own `author_alternative_treatment` transition, appending exactly one
complete route plus a required reachable path when hidden and one new history row. It
never reuses `diagnose`, replaces prior route definitions, or rewrites old history.

Support three closed modes:

- `procedure`: one sealed check and bounded result bands;
- `course`: ordered doses/milestones with declared interruption behavior;
- `guaranteed`: deterministic result backed by an already materialized capability.

The common planner reserves resources and commits consumption only with the accepted
attempt. A valid failed attempt may consume only its predeclared resources. Mortal
natural recovery uses `progressive`, `requires_stabilization`, or
`no_natural_recovery`, plus an optional deterministic deterioration policy.

**Rationale**: A ready-made medicine list cannot work across arbitrary universes.
Materialized routes preserve setting freedom while making resource use, retry, and
progression mechanically enforceable.

## R-009: Spiritual wounds attach to strain transitions

**Decision**: Extend the existing spiritual-conflict state with a declared danger mode
(`training`, `controlled`, `hostile`, `annihilation`) and per-side wound opportunity.
Calculate maximum severity from:

```text
traumaPressure = harmfulMargin
               + 2 * (appliedArtTier - targetResilienceTier)
               + 2 * (newStrainRank - 1)
               + 3 * extraJumpSteps

< 8  => none
8-12 => I
13-17 => II
18-22 => III
>=23 => IV
```

Then apply the destination-strain and danger-mode hard caps. Natural 1/20 do not raise
the wound maximum. Training forbids a wound absent an explicit earlier escalation;
controlled caps at II. Destination caps are exactly `clear -> none`, `strained -> I`,
`fractured -> II`, `overwhelmed -> III`, and `broken -> IV`. One new wound per side per conflict is allowed; later accepted
opportunities may worsen that wound. An older wound changes only through explicit
re-trauma.

**Rationale**: Strain is the existing afterlife harm contour. Basing the opportunity on
its accepted transition avoids introducing HP or automatic post-battle fatigue and
keeps training safe by default.

## R-010: Defeat and soul dissipation stay separate

**Decision**: Every non-training defeat produces one bounded anti-repeat outcome even
when the GM declines a wound. Soul dissipation remains a separate optional winner
choice available only under existing annihilation/authority rules; it is never wound
severity V and never automatic.

**Rationale**: Without a bounded defeat outcome, the loser can immediately repeat the
same aggression. Making injury or dissipation compulsory would contradict the approved
conflict model.

## R-011: Standard spiritual arts and active-healing resolver

**Decision**: Add visible standard arts `spiritual_resilience` / `Духовная стойкость`
(passive) and `spiritual_healing` / `Духовное исцеление` (active), tiers 0-V, to the
existing afterlife art/progression authority. Tier 0 diagnoses but cannot reduce a
wound. A healer may actively reduce a wound only when healing tier `H >= W`.

Resolve the accepted check as `d20 + 2H + validatedModifiers` against
`10 + 2W + complications`:

- margin at least 8 or natural 20: reduce two severities, bounded at healed;
- margin 0 through 7: reduce one severity;
- margin -1 through -4: add one recovery point;
- margin at most -5 or natural 1: no active improvement.

The tier gate precedes natural-20 handling. In conflict the action costs base 5
spiritual action points, uses ordinary validated reductions, has a floor of 2, and is
counterable.

**Rationale**: One final resolver prevents command, roleplay, self, and NPC healing
from drifting. Tier 0 keeps the system discoverable without granting free treatment.

## R-012: Safe-cycle healing and universal natural recovery

**Decision**: An out-of-conflict healing session advances exactly one safe afterlife
world cycle, consumes no action points, and permits one active attempt per wound per
cycle regardless of healer. Self-healing consumes no item or currency; provider
compensation is separate. A failed session still advances the world and applies only
ordinary natural recovery.

Each safe cycle adds `1 + SpiritualHealingTier` natural recovery points. Thresholds
per severity step are I=2, II=4, III=6, IV=8. Excess progress carries forward;
worsening resets progress for the current step. This applies to players, Guardians,
residents, leaders, and other persistent afterlife actors. Ordinary entity progression
may increase the art tier. The accepted cycle key comes from `ProgressionScheduleService`
and `game_state/control/progression_schedule.json`; wound recovery does not use wall
clock time or overload effect expiry scheduling.

**Rationale**: Every entity has an eventual exit from wounds without requiring a
stronger healer to exist. The world-time cost prevents risk-free sequential self-heal
from being instantaneous or outside play.

## R-013: Capability and public service are separate

**Decision**: Store optional `healingServiceProfile` beside an afterlife actor's
capability. It binds provider, realm/location, visible availability, access conditions,
compensation routes, and a 50%-200% price multiplier. Public command prices for wound
I-IV are 25/50/100/200 Ink Feathers, multiplied and rounded upward to a whole Feather.
The exact quote is sealed before confirmation and charged once per accepted attempt,
including partial/failure; cancel/rollback charges nothing. Accepted roleplay
compensation replaces currency but not the same healing resolver or safe cycle.

Protect built-in Guardian `elyara`: Spiritual Healing V, visible from first Chaos Sea
entry, located in `Лазарет Незаживающего Света`, public at 100%, with negotiated
compensation where her character contract permits it. Every materialized Shining
faction must expose at least one resident whose visible primary role is
`healing_support` and whose tier is I-V; the role does not make service public.
The role is stored in the existing `guardian_abode_residents.json` roster while art
and service capability resolve through the exact afterlife actor profile.

**Rationale**: A healer can be known but unwilling, inaccessible, or faction-bound.
Separating capability from service preserves faction play and avoids a mandatory
universal Shining clinic.

## R-014: One shared command application service

**Decision**: Add `/раны` (`/wounds`) and `/лечить` (`/treat`), with afterlife aliases
`/исцелить` and `/heal`, through one C# query/action service consumed by Spectre.Console
and the browser host. The primary list contains current-realm active wounds only;
History is a separate action. Target selection lists Self first, then visible reachable
entities, binds a hidden exact coordinate, visibly disambiguates duplicate names, and
revalidates reachability/consent/state immediately before commit.

Projection is recursive visibility-aware and removes IDs, seals, hidden symptoms,
unknown routes, private NPC data, and GM/validator terminology. Dynamic text is escaped
for console markup and browser rendering.

**Rationale**: Typed names or opaque IDs are not player-usable and are inconsistent
with established guided entity selection. A common application service gives browser
and console semantic parity.

## R-015: Typed validation, bounded repair, and snapshot rollback

**Decision**: Replace loose `JsonElement[]` wound validation with a dedicated
`WoundMaterializationContract`, typed proposal/command composer, strict parsers, source
and target catalogs, transition planner, plan cache/handoff, and repair packet builder.
Bind the plan to the pending-turn session/request/snapshot, exact before-images, accepted
event, owner, effect source/target, resource authority, scheduler cycle, and command
authority fingerprints.

Repair reports only offending semantic paths, closed legal shapes/ranges, and safe
readable context. It preserves unrelated valid response content. A changed event,
target, roll, or snapshot invalidates the packet. Publication uses the existing
snapshot/write-lease rollback contour across wounds, effects, resources, inventory,
characteristics, quests, scheduler/journals, and final output.

**Rationale**: Prompt-only instructions cannot prevent partial state or replay. The
harness can make invalid state unrepresentable and give the GM a bounded repair loop.

## R-016: Direct pre-alpha schema cutover

**Decision**: Remove the loose legacy wrapper/object acceptance, old English severity
names, `canBeImprovedBy`, embedded generated-effect payloads, NPC wound writes to the
effect carrier, and removal-on-heal behavior. Update current bootstrap roots, fixtures,
examples, manifests, prompts, and tests to schema version 1. Do not add migration,
dual-write, compatibility parsing, or legacy promotion. A schema-version-1 wound whose
`consequences` omit `ownedEffectSources`, including the former slot-only technical
shape created earlier during #1536 development, is rejected rather than upgraded.

**Rationale**: The project constitution and user explicitly waive pre-release save
compatibility. Carrying both models would substantially weaken the new authority.

## R-017: Saref memory suppression is not a wound

**Decision**: Treat `memory_suppression` as an independent Saref defeat effect and prove
that wound healing leaves it unchanged. Audit its current runtime materialization during
implementation. If it is only audit metadata, create a separate linked issue before
closing #1536; do not expand the wound kernel to absorb it.

**Rationale**: Memory loss caused by a specific trauma may be one wound consequence,
but deliberate Saref suppression has its own source/lifecycle and must not be healed as
collateral damage.

## R-018: Test and performance strategy

**Decision**: Build reusable wound contract/test fixtures, then use TDD in four slices:
kernel; Mortal; spiritual; commands/providers/docs. Start each behavior with the
smallest `Focused` filter. Run one `Fast` control at a meaningful checkpoint, the
conditional afterlife documentation/`FullValidation` controls, and one `PreMerge`
control immediately before merge. Use explicit bounded timeout headroom when a coherent
selection legitimately exceeds its default; do not remove coverage to win seconds.

Add bounded descriptor counts and indexed identity/source/target lookups to prevent
quadratic scans. Suggested version-1 ceilings are 2,000 active wounds, 20,000 history
rows, 20,000 opportunity-decision receipts, 128 wound commands per turn, 64 repair
resolutions, 32 pending Mortal occurrences with 1-160 accepted events each, 32 treatment
routes per wound, 16 requirements per route, 4 consequences, and 32 transition steps per
accepted turn. The complete source graph adds exact derived bounds of five definitions and five
root bindings per wound, 10,000 pre-turn definitions/root bindings across 2,000 active
wounds, and 160 same-turn definitions/root applications across 32 transitions. These
wound partitions are checked before concatenation with independently bounded generic
effect sources.

The five-definition proof charges one slot to the `event_reaction` producer and one to
every flattened non-marker leaf component. A severity-IV wound may therefore contain a
reaction root, two other one-slot roots, one mechanical downstream-only definition, and
one marker (five definitions, four slots). Four ordinary one-slot roots plus a reaction
and mechanical leaf exceeds the slot envelope before the optional marker is considered.
A zero-edge graph remains legal.

Selective lineage work is linear in the already parsed exact source-group membership:
build the first-create causal parent/child index once, enqueue each member at most once,
and reject cycles, more than one causal `sourceEffectId` on first create, duplicate
same-kind causal evidence, foreign-source/domain edges, or definition keys absent from
the persisted graph before mutation. Legal replacement succession is ignored for
ownership traversal. Statistics prove visited identities do not exceed the parsed exact
source-group identity count; no unrelated global effect-history limit is invented inside
#1536.

**Rationale**: The repository's test runner and mature effect/resource planners already
enforce bounded execution and report artifacts. Explicit limits prevent malicious or
accidental GM payload expansion without constraining ordinary play.

## Resolved research conclusion

All design-critical unknowns are resolved. Implementation can proceed without a
clarification gate. File-level refinements may be made during TDD when nearby code
proves a more appropriate partial-class split, provided the canonical paths, authority
boundaries, approved mechanics, and no-migration decision above remain unchanged.
