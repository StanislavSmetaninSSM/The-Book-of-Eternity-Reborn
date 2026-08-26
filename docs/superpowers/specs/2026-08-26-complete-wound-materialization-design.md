# Complete Wound Materialization and Healing Lifecycle Design

**Date**: 2026-08-26

**Source issue**: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

**Depends on**: [GitHub #1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

**Planned Spec Kit feature**: `specs/1536-complete-wound-materialization/`

**Status**: Design approved section by section and written design pre-approved by the user

## Purpose

A wound is not a temporary display effect. It is an independently treatable entity
with an exact owner, cause, severity, manifestation, progression, treatment policy,
linked consequences, healing history, and terminal state. Removing pain, bleeding,
or another linked effect must not silently heal the wound, while healing the wound
must not remove unrelated effects.

The current Mortal wound surface accepts loosely shaped `JsonElement` changes and
does not prove a complete treatment lifecycle. The afterlife spiritual-conflict
model has transient `strain` states but no persistent spiritual injury. These gaps
leave the GM responsible for remembering state transitions, choosing compatible
penalties, consuming resources, preventing duplicate treatment, and keeping
narration synchronized with canonical state.

This design introduces one common wound kernel with separate Mortal and afterlife
policies. It preserves GM authorship of each unique wound while making invalid,
overpowered, contradictory, stale, cross-realm, partially published, or replayed
results impossible to enter canonical state.

## Accepted Product Decisions

### Common authority

1. The GM decides whether an ordinary eligible event creates a wound, its severity
   within the permitted envelope, its name, manifestation, symptoms, prognosis,
   treatment fiction, and final narrative presentation.
2. The client does not invent an ordinary wound or force the GM to choose the
   maximum permitted severity. It validates the proposed wound against sealed event
   evidence, realm policy, severity limits, consequence budget, component-power
   envelopes, treatment completeness, and exact owner/source authority.
3. A pre-materialized source may explicitly guarantee a wound when a sealed trigger
   succeeds. Once that trigger is proven, omitting the promised wound is invalid.
   The source-owned bound and the current realm/conflict hard cap still apply.
4. Wounds are independent canonical entities. Their mechanical consequences are
   separate active effects whose immutable source link resolves to the wound.
5. An effect can never heal, delete, worsen, retarget, or rewrite its source wound.
   Only an accepted wound transition may change wound state.
6. Wound creation, linked effect changes, resource consumption, history, final
   narration, player notification, and derived projections publish atomically.
7. Runtime migration is excluded. This is a technical Pre-Alpha, and existing users
   have been warned that old saves need not remain compatible. Repository fixtures
   and new-game templates move directly to the new contract.

### Severity and terminal state

8. Both realms use four wound severities: I light, II significant, III heavy, and IV
   critical. Death and soul dissipation are separate outcomes, not severity V.
9. Severity changes only through explicit create, worsen, treatment, recovery, or
   healing transitions. Prose and effect removal are never transition authority.
10. A completely healed wound remains as immutable history but is excluded from the
    ordinary active-wound list.
11. Full healing atomically terminates every active effect owned by the wound.
    A permanent mechanical scar, spiritual mark, memory change, or other legacy is a
    new independent entity with a provenance link to the healed wound. A cosmetic
    scar remains narrative/history only.
12. A healed wound is never reopened. A later injury creates a new wound identity
    and may include a historical relation to the earlier wound.

### Spiritual wounds

13. Spiritual wounds are persistent injuries, not automatic post-conflict fatigue.
    An eligible wound opportunity occurs during an accepted harmful `strain`
    transition and reuses the already sealed exchange roll; no second injury die is
    rolled.
14. A spiritual conflict fixes one danger mode at start: `training`, `controlled`,
    `hostile`, or `annihilation`. Escalation is an explicit accepted transition.
15. Training forbids wounds. Controlled conflict caps them at II. Hostile and
    annihilation modes permit I-IV. Annihilation also permits, but never requires,
    soul dissipation as a separately proven winner choice.
16. One side receives at most one newly created spiritual wound per conflict.
    A later accepted wound choice in that conflict may worsen that wound within its
    new envelope but can never create a duplicate conflict wound. The GM may decline
    an ordinary later wound opportunity; a guaranteed trigger follows its source
    contract.
17. Wounds from earlier conflicts remain separate. Re-traumatizing one exact older
    active wound requires an explicit action and causal evidence. A healed wound is
    not reactivated.
18. Ordinary defeat requires a materialized outcome such as retreat, exile,
    surrender, capture/binding, or loss of the objective. That outcome has scope and
    an end condition and prevents immediate repetition of the same aggression.
    Training may end without a punitive outcome.
19. Soul dissipation is always optional, including in annihilation mode. A winner may
    always choose a softer legal outcome.

### Spiritual arts and healing

20. `Духовная стойкость` is a developable passive standard spiritual art.
21. `Духовное исцеление` is a developable active standard spiritual art. Tier 0
    diagnoses but cannot reduce severity; tiers I-IV treat wounds up to the matching
    severity; tier V is the master tier and supports every spiritual severity.
22. Both arts are visible at tier 0 from the first afterlife entry and use the normal
    standard-art unlock and progression systems. Afterlife NPCs use the existing
    deterministic entity progression scheduler rather than a new XP subsystem.
23. In-conflict healing is a counterable spiritual action with base cost 5 OD. The
    ordinary art-cost reducer applies, but final cost cannot fall below 2 OD.
24. Out-of-conflict healing spends no OD. It consumes one ordinary safe afterlife
    cycle, advances the world scheduler, and allows only one session for the same
    wound in the same cycle regardless of healer.
25. Self-healing consumes neither an item nor currency, but still consumes the cycle.
    Repeated attempts are therefore never free rerolls: plans, factions, Guardians,
    Saref, and other scheduled activity continue to progress.
26. Natural spiritual recovery is universal for players and non-player entities.
    It is based on completed safe afterlife cycles, not wall-clock time.
27. A conscious capable target must consent to healing. Treating an unwilling target
    is a conflict action. A helpless friendly target may be treated without waiting
    for an explicit response.
28. A player may diagnose and heal self, another player, a Guardian, a resident, a
    faction leader, or another exact reachable afterlife entity.

### Mortal wounds and treatment

29. There is no catalog of pre-authored physical wounds. The GM constructs every
    Mortal wound for the current universe, including its name, nature, location,
    symptoms, complications, prognosis, effects, and treatment routes.
30. Mortal mechanical consequences are composed from universal registered effect
    primitives such as a characteristic modifier, roll contribution, periodic
    resource change, resistance change, or action control. These primitives are not
    a catalog of wound names, anatomies, medicines, symptoms, or cures.
31. Each Mortal wound includes at least one mechanically complete treatment route.
    A route may be hidden until diagnosis, but every hidden route has a reachable,
    materialized discovery path.
32. Additional evidence-backed treatment routes may be materialized later when a
    player finds a valid world-specific solution.
33. Treatment routes may require exact items/doses, capabilities, skills,
    specialists, facilities, time, order, and environmental conditions. Requirements
    inside one route are conjunctive; separate routes are alternatives.
34. The three resolution modes are `procedure`, `course`, and `guaranteed`.
    Guaranteed treatment is legal only when a pre-materialized source explicitly
    authorizes that exact deterministic result.
35. The client validates, reserves, and consumes exact treatment resources and
    computes the outcome. The GM cannot declare a wound healed merely through prose.
36. The GM selects a bounded natural-recovery policy for each physical wound:
    progressive recovery, recovery only after stabilization, no natural recovery,
    and optional deterministic deterioration where the fiction warrants it.

### Player interaction and providers

37. `/раны` and `/wounds` show the player's active wounds only. Healed wounds are
    available through a separate History action and never mix into the primary list.
38. `/лечить` and `/treat` are the common guided treatment routes. `/исцелить` and
    `/heal` are afterlife aliases. Diagnosis is integrated into this flow.
39. The player never types an entity name or technical ID as a command argument.
    The client presents self first and then reachable visible nearby entities,
    internally binds the selection to an exact hidden identity, disambiguates equal
    names with visible context, and revalidates reachability before commit.
40. Console and browser clients use one command/application service and expose the
    same choices, costs, results, privacy, and history.
41. Ability to heal and provision of a public paid service are separate. Any capable
    reachable entity may agree through roleplay; only an explicit
    `healingServiceProfile` appears in public service listings.
42. Элиара Последней Раны is the guaranteed Chaos Sea healer. Her Spiritual Healing
    tier is fixed at V, her Lazaret service is public from first Chaos Sea entry, and
    ordinary command treatment accepts Ink Feathers. Travel to her advances a safe
    afterlife cycle.
43. Public afterlife command prices per attempt are 25/50/100/200 Ink Feathers for
    severities I/II/III/IV. A provider multiplier may range from 50% through 200%,
    with deterministic upward rounding to a whole Feather. Payment is owed for an
    accepted attempt even on partial success or failure. Roleplay may establish a
    different explicit compensation such as a favor, debt, or quest before using the
    same final treatment handler.
44. There is one Shining Abode containing multiple Shining factions. Every
    materialized Shining faction has at least one visible resident whose primary role
    is `healing_support` ("целитель фракции") and whose Spiritual Healing tier is
    I-V and progresses normally.
45. A Shining faction healer is not automatically a public service. Access may depend
    on membership, reputation, alliance, payment, debt, or negotiation. The design
    does not require one universal public clinic in the late-game Shining Abode.

## Current-State Observations

- `GameResponse.PlayerWoundChanges` and `NPCWoundChanges` are loosely typed
  `JsonElement[]` surfaces. Existing validation proves little beyond readable wound
  names and a few optional legacy fields.
- Mortal player wounds currently live under `game_state/player/wounds.json`, while
  named-NPC wound-like data is entangled with the NPC effect surface. There is no
  complete common identity or transition authority.
- The legacy wound shape includes `woundId`, name, severity, generated effects, and a
  small `Untreated/Stabilized/Recovering/Healed` model, but it does not provide the
  accepted-turn evidence, source authority, replay safety, setting-specific treatment
  contracts, or cross-owner lifecycle required by #1536.
- Effect materialization from #1535 already recognizes `wound` as a source kind and
  enforces the critical separation: wound-derived effects cannot mutate their source
  wound. The new design builds on that authority rather than adding a second effect
  engine.
- Afterlife conflict strain currently follows
  `clear -> strained -> fractured -> overwhelmed -> broken` and is conflict-local.
  It does not persist an injury after conflict resolution.
- Existing `diceAudit.margin` and accepted spiritual-art tiers supply sealed evidence
  for a deterministic maximum spiritual-injury envelope.
- The afterlife progression scheduler already advances player-independent Guardian,
  resident, Shining, and other entity profiles and can develop standard arts by a
  deterministic priority strategy.
- Элиара Последней Раны already exists as an always-available Healing-domain Guardian
  with the canonical Lazaret location. This design strengthens that existing identity
  rather than inventing another compulsory Chaos Sea healer.
- Shining resident roles are already visible player-facing primary roles. Adding
  `healing_support` follows that catalog instead of creating a hidden appointment.
- Saref defeat currently records `memory_suppression` audit data. That state is not a
  spiritual wound merely because it can affect memory.

## Approaches Considered

### A. One common kernel with realm adapters — selected

The common kernel owns identity, owner/realm binding, history, severity, effect links,
transition atomicity, replay safety, archival, and safe projection. The Mortal and
afterlife adapters own occurrence evidence, treatment rules, recovery clocks, and
realm-specific validation.

Advantages:

- Prevents identity, history, retry, effect-link, and archive semantics from drifting.
- Preserves the very different treatment fiction of Mortal worlds and the fixed
  metaphysics of the afterlife.
- Reuses accepted-turn, effect, resource, scheduler, rollback, and target-authority
  infrastructure.
- Makes each adapter independently testable without duplicating the kernel.

Costs:

- Requires explicit adapter boundaries rather than one flat wound schema.
- Requires coordinated atomic plans when wound, effect, resource, scheduler, and
  player-output state all change together.

### B. Fully independent Mortal and afterlife wound systems — rejected

This initially reduces per-file scope but duplicates identity, history, healing,
commands, target selection, retry, and archival logic. The two implementations would
inevitably disagree about what "healed", "same attempt", or "wound-owned effect"
means.

### C. Treat every wound as an active effect — rejected

This simplifies display at the cost of the central gameplay requirement. Dispelling
pain or another symptom could then delete the injury; effect expiry could count as
healing; treatment history and recovery progression would be forced into an effect
lifetime. Wounds remain effect sources, never effect instances.

## Architecture

```text
accepted event / spiritual exchange / treatment request
                         |
                         v
               realm wound opportunity adapter
          (sealed target, cause, hard caps, guarantee)
                         |
                         v
               GM decision and authored proposal
        (no wound, or bounded wound/treatment/narrative)
                         |
                         v
               common WoundAcceptedTurnPlan
          +--------------+---------------+
          |              |               |
          v              v               v
    wound carrier   linked effect plan   resource/scheduler plan
          |              |               |
          +--------------+---------------+
                         v
              identity/history authority
                         |
                         v
              safe console/browser projection
```

The common planner evaluates the proposal against one validated pre-turn snapshot and
the same-turn identities supplied by actor, item/resource, effect, combat, and
afterlife planners. It allocates random permanent identities once and caches the
result for validation, repair, normalization, rollback, and publication.

No downstream reader scans raw GM command arrays for mechanics. Derived wound and
effect projections exist only after the entire composed accepted-turn plan validates.

## Wound Opportunity and GM Authority

An owning event adapter produces a sealed wound-opportunity envelope before a wound
can be materialized. It contains the exact target and realm, causal event/source
evidence, permitted wound domain, maximum severity, whether a wound is forbidden,
optional source-owned guarantee, and the accepted chronology fingerprint.

Formal client-owned events such as combat exchanges, traps, QTEs, checks, and
pre-materialized source triggers compute this envelope from their existing result.
Pure roleplay events have no invented universal damage formula. Their adapter accepts
the GM's explicit causal profile and I-IV proposal, while still validating chronology,
target, realm, closed fields, and any contradiction with sealed mechanical evidence.
If a sealed outcome proves an event harmless, a contradictory critical wound is
rejected. The client never tries to infer genre physics from narrative prose.

For an ordinary opportunity, zero through the maximum severity are legal: zero means
the GM chose no wound. A guaranteed trigger supplies its own minimum/maximum or exact
result and makes omission invalid after the trigger succeeds. Hard realm rules such
as training mode remain authoritative; a source cannot smuggle an injury into a
training conflict without an accepted prior escalation.

## Two-Phase Atomic Publication

Wound-capable turns use a bounded two-phase flow:

1. The GM drafts the exchange/event, proposed outcome, and whether it intends a wound.
2. The client seals the accepted event evidence and computes the wound-opportunity
   envelope. Nothing player-visible publishes yet.
3. If a wound is chosen or guaranteed, the client gives the GM a bounded
   materialization packet containing only the legal target, realm, severity range,
   consequence budget, component envelopes, and required treatment fields.
4. The GM authors the wound, linked consequence definitions, treatment/recovery
   contract, and revised final prose describing how the wound was received.
5. The client builds the composed wound/effect/resource/scheduler/output plan and
   validates it without writing canonical state.
6. A valid plan commits all after-images and player output atomically. An invalid plan
   publishes nothing and returns a bounded repair packet naming only the rejected
   fields and legal ranges.

The final GM pass must make acquisition of the wound legible in the scene. The client
also displays `Получена духовная рана: <name>. Подробнее: /раны` or its Mortal
equivalent, so prose quality never becomes the only discovery mechanism.

## Common Canonical Wound Envelope

Every wound has a closed versioned semantic envelope:

| Section | Required meaning |
| --- | --- |
| Identity | Client-assigned opaque `woundId`, schema version, active or healed lifecycle |
| Owner | Exact realm, owner kind, and permanent/effective owner identity |
| Origin | Exact event/source authority, creation chronology, and optional guaranteed-trigger evidence |
| Classification | `physical` or `spiritual`, explicit location profile, GM-authored readable nature |
| Display | Name, description, visible symptoms, prognosis, and visibility policy |
| Severity | Current I-IV value and explicit previous/next transition evidence |
| Care state | Realm-valid fresh/untreated, stabilized, recovering, or healed state |
| Complications | Structured current complication facts used by treatment and deterioration rules |
| Consequences | Exact wound-owned active effect links and declared slot accounting |
| Treatment | Known/hidden treatment and diagnosis routes with exact requirements and outcomes |
| Recovery | Natural-recovery policy, current-step progress, safe-cycle/time evidence, deterioration policy |
| History | Ordered client-authored create/worsen/stabilize/treat/recover/heal/legacy transitions |
| Relations | Optional prior wound or independent legacy/effect references without shared lifecycle authority |

`locationProfile` is explicit but not a wound or anatomy catalog. It distinguishes an
anatomical, systemic, mental, spiritual-axis, or other locus and carries the exact
readable locus authored for the current creature/world. An exact materialized body
part or spiritual-axis identity may be bound when that world already supplies one;
otherwise the client does not invent it from the wound name.

The GM never supplies permanent IDs, history entries, computed progress, paid-resource
receipts, retry seals, or final transition ordinals.

## Identity, Carriers, and History

Wounds remain in owner-appropriate carriers rather than one global mutable hot file:

- Mortal player wounds remain in the player domain;
- named NPC wounds use a dedicated NPC wound carrier, not the NPC effect carrier;
- anonymous combatant wounds remain bound to the exact combatant authority and move
  only through an existing accepted promotion/persistence transition;
- afterlife player and persistent entity wounds bind to their exact afterlife profile;
- active spiritual-conflict evidence may reference a persistent wound but does not
  duplicate it inside combat conditions.

A common client-owned identity index proves that every active wound occurs in exactly
one carrier and that every linked effect resolves to one active wound owned by the
same target and realm. Terminal history prevents retrying a treatment, healing,
legacy, or archive transition.

The history is append-only. Active carriers hold current semantic state; terminal
history retains enough evidence to reject duplicates without exposing internal seals
to the player or GM. A realm transition never silently converts a physical wound into
a spiritual wound. Any such transformation would require a separately materialized
new spiritual wound and explicit provenance.

## Severity, Consequence Budgets, and Component Power

Severity is a mechanical bound, not merely display text.

### Spiritual consequences

A spiritual wound has exactly one independently understandable mechanical
consequence per severity step: I has one, II has two, III has three, and IV has four.
Each consequence uses a legal afterlife mechanical axis and occupies one slot.
Several restrictions cannot be hidden in one prose description or payload.

The fixed afterlife setting permits a closed set of mechanical axes, but it does not
provide a catalog of complete spiritual wounds. The GM still authors the wound's
identity, manifestation, symptoms, affected axes, and combination of consequences.

A spiritual wound consequence cannot completely remove all access to Spiritual
Healing, retreat, surrender, or negotiation. Heavy wounds may make combat extremely
unattractive without soft-locking the routes that end or remedy the conflict.

### Mortal consequences

Mortal severity supplies a maximum slot budget: I up to one, II up to two, III up to
three, and IV up to four independently understandable consequences. A wound must have
at least one mechanical or explicitly structured treatment-relevant consequence.

There is no list of cuts, fractures, burns, infections, cybernetic failures, crystal
corruption, or other ready-made wounds. The GM freely composes each wound from the
same universal registered effect primitives used elsewhere by the effect engine.
Display text, symptoms, and the technical wound-source marker do not consume slots.
Every actual modifier, damage/restoration behavior, resistance change, roll change,
or action control does. A compound outcome consumes multiple slots.

Slot count alone cannot limit power. The client therefore owns a severity envelope
for each usable effect-component profile. It bounds numeric values, legal operations,
scope, periodic frequency, and whether an absolute restriction is allowed at I-IV.
An absolute action prohibition is restricted to heavy/critical wounds and cannot
remove every route to inspect the wound, communicate, request help, receive treatment,
or otherwise leave the state. Repair packets show the GM the permitted envelope; the
client never silently edits an overpowered component.

### Severity changes

When severity changes, the GM rematerializes the legal current consequence set. The
client atomically terminates/replaces only wound-owned effects and verifies the new
slot and power budget. Unrelated effects survive unchanged. Removing or dispelling a
linked effect alone never changes severity or recovery.

Independent effects such as curses, Fate Card outcomes, oaths, and Saref's deliberate
`memory_suppression` do not consume wound slots. If memory loss is itself a symptom of
the trauma, it may instead occupy one wound slot. One exchange may atomically produce
both kinds, but their lifecycle remains separate.

## Spiritual Conflict Injury Envelope

Strain ranks map to numeric destination ranks:

| Strain | Rank / maximum wound severity |
| --- | ---: |
| `clear` | 0 / no wound |
| `strained` | 1 / I |
| `fractured` | 2 / II |
| `overwhelmed` | 3 / III |
| `broken` | 4 / IV |

For each eligible harmful strain transition, the client calculates target-relative
trauma pressure from the already accepted exchange:

```text
traumaPressure = harmfulMargin
               + 2 * (appliedArtTier - targetResilienceTier)
               + 2 * (newStrainRank - 1)
               + 3 * extraStrainJumpSteps
```

`harmfulMargin` is the non-negative amount by which the accepted result favors the
opponent of the potential target. The calculation does not reinterpret prose or roll
again.

| Pressure | Formula ceiling |
| --- | --- |
| below 8 | no ordinary wound allowed |
| 8-12 | I |
| 13-17 | II |
| 18-22 | III |
| 23 or higher | IV |

The final maximum is the lowest of the formula ceiling, destination-strain ceiling,
and conflict-mode ceiling. Natural 1 and 20 do not independently raise severity.
The GM may choose no wound or any lower severity. A source-owned guaranteed trigger
uses its declared wound result rather than the generic formula, but still cannot
exceed the destination/mode hard cap.

The conflict records whether each side already received its new conflict wound. A
later chosen ordinary wound outcome targets that exact wound and may worsen it within
the new envelope. It cannot allocate a second conflict-wound identity. A deliberate
attack on an older active wound must name the exact authorized target supplied by the
client and prove that specific intent/action.

## Spiritual Defeat and Soul Dissipation

At conflict resolution, a non-training defeat must materialize one exact conflict
outcome with owner, scope, restrictions, and end condition. Examples include forced
retreat from a location, surrender terms, capture/binding, exile, or loss of an
objective. The outcome prevents the defeated side from immediately repeating the
same aggression without first satisfying its end condition.

Soul dissipation is neither a wound severity nor an automatic consequence of
`broken`. It becomes available only when the conflict mode, accepted proof, and
winner authority permit it. The winner explicitly chooses it; choosing retreat,
surrender, binding, or another softer outcome always remains legal.

## Standard Spiritual Arts

Both new arts are ordinary standard arts in the existing tier 0-V framework:

| Art | Tier behavior |
| --- | --- |
| `spiritual_resilience` / Духовная стойкость | Passive; tier enters the trauma-pressure formula and needs no OD action |
| `spiritual_healing` / Духовное исцеление | Tier 0 diagnosis only; tiers I-IV treat matching-or-lower severity; V treats all wounds with maximum art modifier/cost reduction |

They are visible at tier 0 from first afterlife entry and become upgradeable through
the ordinary standard-art unlock/progression contract. Existing player and entity
priority strategies may add them to their normal upgrade order. No new universal XP
pool, Guardian-only exception, or fixed Guardian level is introduced.

## Active Spiritual Healing

Let `H` be the healer's accepted Spiritual Healing tier and `W` the wound's current
severity number. A treatment that can reduce severity is legal only when `H >= W`.
Diagnosis may still explain that a stronger healer is required.

```text
healingTotal = d20 + 2 * H + validatedEffectModifiers
healingDC    = 10 + 2 * W + structuredComplicationModifiers
margin       = healingTotal - healingDC
```

Only registered accepted-effect mechanics contribute modifiers. Complication
modifiers are closed and bounded by the wound contract.

After the tier gate:

| Result | Accepted outcome |
| --- | --- |
| margin >= +8 or natural 20 | Reduce severity by two steps, stopping at healed |
| margin 0 through +7 | Reduce severity by one step |
| margin -1 through -4 | Add exactly one recovery point to the current step |
| margin <= -5 or natural 1 | No active improvement |

A natural 1 is failure and a natural 20 is critical success only after the tier gate.
The client seals the die and attempt fingerprint so replay cannot reroll or double
apply the outcome.

In conflict, the attempt is one ordinary counterable action. Its base cost is 5 OD,
the existing standard-art cost reducer applies, and the final cost is at least 2 OD.
The action is consumed even on failure.

Outside conflict, one selected wound can receive one active session during a safe
cycle. The client resolves the active attempt against the pre-session wound, applies
its result, then advances exactly one world cycle and applies ordinary natural
recovery once. A direct one- or two-step treatment success consumes the old
current-step progress and starts the resulting severity step at zero before that
cycle's natural progress. A partial result adds one point before natural progress. A
failure receives only natural progress. This ordering prevents double-counting while
preserving the agreed cost of leaving ordinary afterlife activity for treatment.

## Natural Spiritual Recovery

Every completed safe afterlife cycle outside an active conflict adds:

```text
naturalRecoveryPoints = 1 + owner's Spiritual Healing tier
```

The cost to leave the current severity is:

| Transition | Points |
| --- | ---: |
| I -> healed | 2 |
| II -> I | 4 |
| III -> II | 6 |
| IV -> III | 8 |

Points beyond a natural-recovery threshold carry into the next step. Worsening resets
the progress of the current step. A tier-0 critical wound therefore heals naturally
in 20 safe cycles; tier V needs four safe cycles. Active healing accelerates this
process but does not create a separate recovery clock.

The scheduler applies the same rule to player souls, Guardians, residents, Shining
leaders, and other authorized afterlife profiles. An entity can improve Spiritual
Healing through existing progression and therefore recover faster later. Active
conflict, explicit unsafe-state blockers, or a terminal entity state prevents a safe
recovery tick. The same cycle identity can never tick twice.

## Mortal Wound Construction

A Mortal wound proposal includes:

- exact accepted cause and owner;
- severity I-IV;
- explicit open-world location profile and readable wound nature;
- visible and hidden symptoms/complications with visibility rules;
- prognosis and deterioration conditions;
- one through the permitted number of wound-owned consequence components;
- at least one complete treatment route;
- a natural-recovery policy;
- final acquisition narration.

Formal combat/QTE/check/trap/mechanical events contribute their owning adapter's
sealed maximum severity. A pure roleplay injury uses the narrative-event adapter:
the GM proposes the causal profile and severity, and the client validates the closed
contract and rejects conflict with any sealed harmless or lower-bound result. There
is deliberately no cross-setting formula that pretends an ogre club, decompression,
nanite infection, and healing-crystal backlash share one damage scale.

The client validates only universal mechanical primitives and bounds. It never
selects a wound from a predefined list or derives the wound's fiction from component
names.

## Mortal Diagnosis and Treatment Routes

Every treatment route has an exact identity within the wound and a closed visibility
state. A route known to the player is projected in `/раны`; an unknown route exposes
only that diagnosis or further evidence is required. A hidden route must bind to at
least one attainable diagnostic path so the GM cannot create an unknowable cure.

Mortal diagnosis uses materialized setting-appropriate skills, tools, procedures,
providers, facilities, clues, and checks. It does not reuse Spiritual Healing. A
successful diagnosis reveals only the facts authorized by that path.

Route requirements may include:

- exact accepted item/resource kinds and quantities;
- required source capabilities or effect definitions;
- skill and difficulty;
- provider identity/kind and consent;
- facility/location authority;
- elapsed game time, dose schedule, or ordered steps;
- environmental conditions and failure/deterioration consequences.

Within a route all requirements must hold. Separate routes are alternatives and may
produce different outcomes or risks.

Resolution modes are:

- `procedure`: one client-resolved skill/tool/severity check using the route's sealed
  outcome bands;
- `course`: deterministic doses and elapsed-time milestones, with interruption and
  missed-step behavior declared before the course starts;
- `guaranteed`: an exact deterministic result authorized by a pre-materialized
  source capability, never by ad hoc GM assertion.

The common accepted-mechanics planner reserves resources before the attempt and
consumes them only in the atomic commit. A valid result may stabilize, remove a
complication, add recovery progress, reduce severity, or heal. Failed validation or
rollback consumes nothing. A valid attempted procedure may consume its declared
resources even when its sealed result fails.

## Mortal Natural Recovery and Deterioration

The GM selects one closed policy per wound:

- `progressive`: valid elapsed-time events add declared bounded recovery progress;
- `requires_stabilization`: no recovery occurs until an accepted stabilization
  transition, after which the declared schedule applies;
- `no_natural_recovery`: only a treatment route can improve the wound;
- optional deterioration: exact unmet conditions and elapsed/event evidence cause a
  deterministic complication or severity transition.

The policy contains the exact clock/event source, cadence, thresholds, blockers, and
result bounds. The client owns ticking and transition history. The GM cannot advance
or worsen a wound twice by repeating prose. A critical wound may deteriorate into a
separate death outcome where the Mortal death contract permits it, but death is never
stored as wound severity V.

## Consent, Reachability, and Target Selection

Treatment always resolves against one exact target supplied by client authority.
A conscious capable entity must consent outside conflict. Treating an unwilling
target requires a legal conflict action. A helpless friendly target can be treated
without an explicit response when the current relationship/conflict authority allows
help.

Player commands never accept a raw name or opaque ID. `/лечить` first displays
`Себя`, then visible reachable nearby entities. Known wound markers appear only when
the player has learned them. Equal names include player-visible disambiguating
context, while the client retains the hidden exact identity. Target reachability,
consent, wound activity, and current severity are checked again immediately before
publication.

## Player Commands and Projection

`/раны` (`/wounds`) defaults to the current realm and active wounds of the player. An
active detail view shows only player-authorized information:

- readable name, severity, origin, location, symptoms, and prognosis;
- linked visible effect summaries;
- current care/recovery state and progress explanation;
- known diagnosis/treatment options and available help;
- a `Лечить` action.

Healed wounds are never present in this primary list. A separate History action shows
readable terminal records without internal IDs, seals, hidden symptoms, GM-only
routes, or private NPC information.

`/лечить` (`/treat`) is the common guided route, with `/исцелить` and `/heal` as
afterlife aliases. For self it offers self-treatment, reachable helpers, and known
public services. For another entity it offers known wounds or a permitted diagnosis
step. Browser and console projections consume the same application service rather
than reimplementing rules.

## Afterlife Healing Services

An entity's Spiritual Healing tier proves capability; it does not automatically make
that entity a public provider. `healingServiceProfile` is an explicit, validated
service contract containing provider/realm/location authority, visible availability,
accepted compensation routes, bounded price multiplier, and access conditions.

The command-price schedule is:

| Severity | Base Ink Feather price |
| --- | ---: |
| I | 25 |
| II | 50 |
| III | 100 |
| IV | 200 |

The multiplier is 50%-200%, and the client rounds the final result upward to the next
whole Feather. The quoted price is sealed before confirmation and paid per accepted
attempt, including partial success and failure. A canceled or rolled-back attempt is
not charged. Light Sparks are not ordinary command payment; they may appear only in
an explicitly negotiated roleplay agreement.

Roleplay may replace currency with a favor, debt, quest, allegiance, or free aid.
That agreement must be materialized and accepted before the same final healing
handler runs. It cannot bypass the healer-tier gate, wound target, roll, attempt seal,
or world-cycle cost.

### Chaos Sea guarantee

The existing Guardian `elyara` / Элиара Последней Раны is fixed as:

- Spiritual Healing tier V, protected from GM downgrade;
- always discoverable and available from first Chaos Sea entry;
- located at `Лазарет Незаживающего Света`;
- public at the ordinary 100% Ink Feather price;
- open to negotiated roleplay compensation where her character/quest contract
  permits it.

The guided route may offer travel to the Lazaret. Travel/treatment advances the
afterlife cycle once through the scheduler; it is not instantaneous world-free UI.
Other Guardians may heal when their own materialized arts and consent permit it.

### Shining Abode factions

There is exactly one Shining Abode. Every materialized Shining faction adds the
visible primary resident role:

```text
healing_support / целитель фракции
```

At least one resident in each faction owns that primary role and has Spiritual
Healing tier I-V. The exact tier reflects the faction and develops through ordinary
entity progression. Multiple healers are legal when justified.

The role is visible whenever the resident/faction roster is visible, like existing
archive, forge, social, resource, and descent roles. It is not specially hidden. It
also does not guarantee a public service: access may require membership, reputation,
alliance, negotiation, payment, debt, or another faction-specific condition. The
player may instead self-heal, recover naturally, seek another entity, earn access, or
return to Elyara.

## Healing Other Entities

Guardians, residents, leaders, and other persistent afterlife entities use the same
wound identities, art-tier gate, active-healing handler, natural recovery scheduler,
and history. They do not remain wounded forever merely because no suitable public
provider exists: universal natural recovery continues on safe cycles, and their
ordinary art progression can shorten recovery.

The player can select and treat a reachable entity through the same hidden-ID target
binding. Conversely, an entity can treat the player or another entity only when its
accepted profile proves the art tier, reachability, consent/conflict authority, and
any compensation/access contract.

## Error Handling, Repair, Retry, and Rollback

Validation fails closed for, at minimum:

- missing, duplicate, stale, confusable, historical, wrong-owner, or cross-realm wound
  identities;
- a wound without exact event/source/target authority;
- severity above an event, destination-strain, conflict-mode, or source hard cap;
- omission of a proven guaranteed wound;
- too many consequence slots, hidden multi-component packing, or component power
  outside the severity envelope;
- an effect whose wound source/link/target/realm does not match;
- a linked effect attempting to mutate wound state;
- missing treatment routes, unreachable hidden-route discovery, or incomplete
  procedure/course/guaranteed contracts;
- stale resource, item, skill/art, healer, facility, location, consent, or target
  references;
- healing by an insufficient Spiritual Healing tier;
- impossible severity regression, reopening a healed wound, or reusing a terminal
  attempt/cycle/event seal;
- partially updated carriers, index, effects, resources, history, scheduler, or
  player output.

Repair is bounded. The GM receives the exact rejected semantic path, expected closed
shape/range, safe readable context, and preserved non-offending authored content. It
does not receive client IDs, hidden target data, resource seals, or permission to
rewrite unrelated state. Repair recomputes against the same pending snapshot; a
changed target/event/roll requires abandoning the pending turn and starting a new
accepted event.

The pending-turn snapshot covers wound carriers, wound identity/history, linked
effect carriers/index, resources/inventory, characteristics, quests, scheduler state,
journals, and final player output. A crash or rejection restores all of them or
commits all of them. Retry-safe operation keys prevent duplicate wounds, effects,
charges, recovery ticks, rolls, notifications, and history rows.

## Privacy and GM Context

The GM receives enough exact technical context to author and repair a legal wound,
but player projections remain visibility-aware. Hidden symptoms, undiscovered cures,
private NPC wounds, secret provider conditions, GM-only effects, opaque identities,
and internal seals never leak through `/раны`, `/лечить`, browser payloads, or console
fallbacks.

A visible independent effect may cross-link to a visible wound, and a visible wound
may summarize its visible effects. Saref's deliberate memory suppression, secret
oaths, curses, and similar independent effects remain visible only according to their
own effect visibility contract.

## GM Prompts, Documentation, and Worked Examples

The GM does not read implementation code during play. Every new command, field,
validator rule, pending packet, repair loop, effect link, treatment route, service
profile, spiritual-conflict outcome, and role must therefore be documented with
worked examples in the same implementation slices.

Required coverage includes at least:

- a Mortal freely authored wound with a visible treatment route;
- a Mortal hidden diagnosis path and an alternative world-specific cure;
- a failed/partial treatment and deterministic resource handling;
- a spiritual strain transition where the GM declines an allowed wound;
- a spiritual wound materialized below the computed maximum;
- a guaranteed wound trigger;
- a rejected over-severity or over-budget wound repair;
- in-conflict healing, self-healing, natural recovery, and NPC recovery;
- Elyara command payment and negotiated compensation;
- Shining faction healer visibility without mandatory public access;
- healed history and independent permanent legacy;
- independent `memory_suppression` alongside, rather than inside, a spiritual wound.

Afterlife contract work must update and guard, where applicable:

- `OtherGuides/Afterlife_Contract_Matrix.md`;
- `Examples/E_CLI_Afterlife_Turns.txt`;
- `Examples/example_validation_manifest.json`;
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`;
- `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`;
- daemon/launcher GM prompt entrypoints and any explicit pending/control inventory.

Mortal GM guides and examples must be synchronized in the same way. A code-only
GM-authored capability is not complete.

## Implementation Strategy

Issue #1536 remains the umbrella GitHub task and closes only after all accepted
behavior ships. One Spec Kit feature owns the shared contract. Implementation is
split into independently reviewable, non-half-exposed slices:

### Slice 1: common wound kernel

- closed wound envelope and realm-adapter interfaces;
- identity index, owner carriers, history, and transition planner;
- effect source/link authority, slot accounting, power envelopes;
- atomic repair/rollback/replay protection;
- no incomplete player command is enabled.

### Slice 2: Mortal materialization and treatment

- formal and narrative wound-opportunity adapters;
- free GM construction without a wound catalog;
- diagnosis/treatment routes and natural recovery/deterioration policies;
- exact resource/source/provider/facility resolution;
- Mortal GM examples and projections required for a complete Mortal route.

### Slice 3: spiritual conflict and healing

- danger modes, trauma-pressure maximum, persistent spiritual wounds, and conflict
  outcomes;
- Spiritual Resilience and Spiritual Healing arts;
- in-conflict healing, safe-cycle sessions, natural NPC/player recovery;
- afterlife scheduler and entity-profile integration.

### Slice 4: commands, providers, parity, and documentation

- `/раны`, History, `/лечить`, aliases, nearby target selection, diagnosis flow;
- browser/console parity;
- Elyara service, pricing/roleplay compensation;
- Shining `healing_support` role and access semantics;
- complete prompts, examples, matrices, manifests, source guards, and end-to-end
  lifecycle verification.

The internal contract may merge before exposure, but no slice may expose a command or
GM output path whose accepted lifecycle is incomplete. Separate PRs may implement the
slices while sharing the same approved Spec Kit feature and issue traceability.

## Testing Strategy

Implementation follows test-driven development. Each behavior starts with the
smallest failing test at the owning boundary.

### Unit/contract coverage

- wound envelope, identity, carrier/index agreement, history transitions;
- event envelopes, guarantees, severity caps, spiritual formula boundaries;
- consequence slot counting and profile-specific power bounds;
- wound/effect source-link isolation and independent-effect survival;
- Spiritual Healing tier gate, margin bands, natural 1/20, OD cost floor;
- safe-cycle attempt sealing, natural progress thresholds, worsening reset;
- Mortal route shape, hidden discoverability, requirement logic, recovery policies;
- price multiplier/rounding, payment, compensation, consent, target binding;
- healed archival and legacy separation.

### Integration coverage

- Mortal create -> diagnose -> stabilize/treat -> recover -> heal -> History;
- physical alternative cures across at least two setting styles without a wound
  catalog;
- spiritual exchange -> GM decision -> bounded materialization -> final narration;
- no-wound, lower-than-maximum, guaranteed, worsening, and rejected proposals;
- conflict defeat outcomes and always-optional soul dissipation;
- combat healing and out-of-combat self/provider healing;
- Guardian/resident natural recovery and standard-art progression;
- player treatment of a Guardian/other entity;
- Elyara service and Shining healer access;
- duplicate/replay/crash rollback across wound, effects, resources, scheduler, and
  output;
- `/раны` active-only/history privacy and console/browser parity;
- GM examples, manifests, matrix, and source guards.

During implementation, use the smallest relevant `Focused` lane, then one meaningful
`Fast` checkpoint. Immediately before each merge, run one `PreMerge` control without
an adjacent redundant Fast run. Because the feature changes afterlife
documentation/examples and spiritual-conflict validation, run the relevant focused
documentation tests and one conditional `FullValidation` control. Run exhaustive
`RegressionIntegration` for the spiritual-conflict boundary when that slice changes
the complete matrix or when focused failures require it.

The clean feature-worktree baseline before design editing passed Fast `4339/4339` in
`00:04:33.8143170`, result directory
`20260826-154530-205-21516-38708e3e7c1243dbb26267659d11b6b4-fast`.

## Saref Memory-Suppression Audit

`memory_suppression` is deliberately not modeled as a wound by default. It is a
Saref-owned independent defeat outcome/effect and survives healing of an unrelated
spiritual wound. Trauma-caused memory loss may instead be one wound consequence when
the event explicitly says the wound caused it.

During Spec Kit research, inspect whether current `memory_suppression` is a complete
materialized active effect or only audit metadata. If it is only metadata, create a
separate linked GitHub task before #1536 closes. That gap belongs to Saref defeat/effect
materialization and must not silently expand the wound kernel.

## Explicit Non-Goals

- No migration of non-empty legacy wound saves.
- No catalog of ready-made Mortal or spiritual wounds.
- No inference of wound severity, anatomy, cure, or mechanics from prose/name alone.
- No replacement or duplication of the #1535 effect engine.
- No general redesign of Mortal combat damage, afterlife XP, or entity progression.
- No mandatory spiritual wound after every battle or temporary universal fatigue.
- No automatic soul dissipation.
- No full afterlife inventory; afterlife treatment uses arts, effects/relic authority,
  world time, access contracts, and Ink Feather services.
- No mandatory public clinic for the entire Shining Abode.
- No automatic conversion of physical wounds into spiritual wounds on realm change.

## Completion Criteria

The feature is complete only when:

1. Every accepted wound has stable identity, exact owner/realm/source/event authority,
   severity, explicit location profile, consequences, treatment/recovery policy, and
   history.
2. GM-authored ordinary wounds remain optional and bounded; proven guaranteed
   triggers remain enforceable.
3. Mortal and spiritual wounds share identity/lifecycle safety without losing their
   different occurrence and treatment models.
4. Wound-owned effects apply and end atomically, cannot mutate the wound, and never
   remove unrelated effects.
5. Creation, worsening, complication, diagnosis, stabilization, active treatment,
   natural recovery, healing, legacy, and History transitions are deterministic,
   bounded, retry-safe, and rollback-safe.
6. Spiritual conflict, arts, defeat outcomes, natural entity recovery, Elyara, and
   Shining faction healer roles obey the accepted rules.
7. Console and browser commands provide parity, nearby hidden-ID targeting, privacy,
   active-only primary lists, and separate healed history.
8. Mortal and afterlife GM prompts, documentation, examples, manifests, matrices, and
   source guards teach and prove the complete lifecycle.
9. Spec Kit artifacts remain internally consistent and all required focused,
   conditional, Fast, and PreMerge verification evidence is green.
