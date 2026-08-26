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
owner authority and exports wound effect sources; the effect planner materializes those
sources; finalization proves exact reciprocal links and consequence budgets. A valid plan carries
wound carrier after-images, identity-index after-image, append-only history after-image,
wound-owned effect changes, resource mutations, scheduler outcomes, and pending/receipt
state under one input fingerprint and one canonical write lease.

**Rationale**: `AcceptedMechanicsPlanner`, its validated handoff/cache, exact
before-images, and `CanonicalStateNormalizer.AcceptedMechanics` already provide the
atomic boundary for resources and effects. A parallel wound normalizer would introduce
write-order races and partial commits. The staged contour resolves the otherwise cyclic
dependency between wound source allocation and effect-link validation, while all
after-images still publish together.

**Alternatives rejected**:

- A standalone wound normalizer after effects: cannot prove bidirectional ownership
  before effect publication.
- Direct mutation in `ValidationService`: validation must not write canonical state.
- Best-effort multi-file writes outside the current lease: violates rollback and retry
  requirements.

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
attempt IDs, course/cycle keys, ordinals, fingerprints, receipts, and history rows.

Supported owner kinds are closed: Mortal player, named NPC, Mortal combatant or group
member, player soul, Guardian, Shining resident, radiant actor, and another accepted
afterlife profile. Realm and owner coordinates must match the sealed event and linked
effect target. Anonymous combatant wounds move only through the existing accepted
combatant-persistence transition.

**Rationale**: GM-authored permanent IDs or name lookup would permit replay,
cross-realm retargeting, and ambiguity. Existing effect target authority already proves
the necessary owner identities and promotion semantics.

## R-005: A wound opportunity is a sealed maximum, not an automatic injury

**Decision**: Every mechanically eligible event produces a client-owned
`WoundOpportunity` containing exact event/target/realm/domain evidence, legal severity
range, profile, and any guarantee. The GM may choose none or any severity up to the
maximum for ordinary opportunities. Only a previously materialized guaranteed trigger
requires a wound. The GM also supplies the readable acquisition narration; the client
adds an explicit notification and rejects contradictory narration/state.

**Rationale**: This preserves GM narrative judgment and prevents the client from
inventing wounds. It also makes a guaranteed skill/item/source promise enforceable.

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
`source.kind=wound` and the exact `woundId`; the wound stores the corresponding effect
IDs. Admission requires exact bidirectional owner/realm/source agreement. Effect
operations cannot mutate wound state. Wound transitions rematerialize the owned effect
set atomically; full healing terminates only those effects.

Mortal severity I-IV allows at most 1/2/3/4 independent consequences. Spiritual
severity I-IV requires exactly 1/2/3/4 consequences. Each consequence uses one
registered component/profile and consumes one slot; display text and linkage consume no
slot. Profile-specific severity envelopes cap magnitude, cadence, duration, scope, and
action restriction. The closed version-1 profile list and exact per-severity limits are
normative in `contracts/wound-effects-and-atomicity.md`; they include every permitted
generic Mortal component and eight fixed-setting spiritual axes. A Mortal wound with no
mechanical slot still requires an active complication or care/recovery constraint that
changes its legal lifecycle.

**Rationale**: This preserves one mechanical effect engine while keeping wound
lifecycle authority separate. Exact slot counting blocks a GM from packing several
mechanics into one prose object.

## R-008: Mortal diagnosis and treatment are materialized routes

**Decision**: Require every Mortal wound to have at least one mechanically complete
treatment route. Hidden routes require a reachable diagnosis path. Route-local
requirements are AND; routes are OR alternatives. Reference exact accepted items,
resources, capabilities, skills, providers, facilities, locations, clocks, and
environmental conditions.

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
dual-write, compatibility parsing, or legacy promotion.

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
rows, 128 wound commands per turn, 64 pending resolutions, 32 treatment routes per
wound, 16 requirements per route, 4 consequences, and 32 transition steps per accepted
turn.

**Rationale**: The repository's test runner and mature effect/resource planners already
enforce bounded execution and report artifacts. Explicit limits prevent malicious or
accidental GM payload expansion without constraining ordinary play.

## Resolved research conclusion

All design-critical unknowns are resolved. Implementation can proceed without a
clarification gate. File-level refinements may be made during TDD when nearby code
proves a more appropriate partial-class split, provided the canonical paths, authority
boundaries, approved mechanics, and no-migration decision above remain unchanged.
