# Contract: Spiritual Conflict Wounds and Afterlife Healing

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Scope

This contract extends the existing afterlife strain/conflict, actor profile,
progression schedule, resource, Guardian, and Shining roster authorities. It does not
introduce HP, a full afterlife inventory, mandatory wounds, automatic soul dissipation,
or universal temporary fatigue.

## Conflict danger mode

Each spiritual conflict starts with one declared client-validated mode:

| Mode | Wound rule | Dissipation rule |
| --- | --- | --- |
| `training` | forbidden unless explicitly escalated before the exchange | forbidden |
| `controlled` | GM optional, maximum II | forbidden |
| `hostile` | GM optional, maximum IV subject to formula/strain cap | unavailable unless another existing terminal authority applies |
| `annihilation` | GM optional, maximum IV subject to formula/strain cap | optional winner choice with existing exact authority |

Mode is player-visible before the first harmful exchange. A mode change is an explicit
accepted escalation/de-escalation event, never inferred after seeing a roll.

## Wound opportunity formula

For each harmful accepted strain transition the client computes:

```text
traumaPressure = harmfulMargin
               + 2 * (appliedArtTier - targetResilienceTier)
               + 2 * (newStrainRank - 1)
               + 3 * extraJumpSteps
```

Raw formula band:

| Pressure | Maximum |
| ---: | --- |
| `< 8` | none |
| `8-12` | I |
| `13-17` | II |
| `18-22` | III |
| `>= 23` | IV |

The final maximum is the minimum of raw band, destination-strain cap, danger-mode cap,
and source cap. `harmfulMargin` is the sealed harmful margin before any normalized
critical-success display adjustment. Natural 1/20 do not raise the maximum. All audit
inputs are client-computed or copied from validated conflict authority.

The user-approved source rule (2026-09-08) is no additional ordinary-action cap:
the source term is neutral IV unless an exact pre-materialized canonical source
declares a stricter limit. A special art without that declaration has the same
ordinary harmful-strain eligibility and no guaranteed wound. Art cost, zero cost,
a passive role, or a caller-supplied ceiling cannot establish or remove eligibility.

A special ceiling or guaranteed severity must be proven from the source's accepted
declaration before the harmful event. The current wound proposal or exchange cannot
invent or strengthen that authority. A proven guaranteed trigger requires its
declared result only inside all applicable hard limits; it cannot override training,
the formula, destination strain, or a stricter source ceiling. Contradictory proof
must reject, not increase the maximum or silently turn the guarantee into an ordinary
optional result. This rule does not itself prove that the source event or its
resource/effect reductions succeeded.

The exact destination-strain cap is:

| Destination | Rank | Cap |
| --- | ---: | --- |
| `clear` | 0 | none |
| `strained` | 1 | I |
| `fractured` | 2 | II |
| `overwhelmed` | 3 | III |
| `broken` | 4 | IV |

Boundary tests cover every threshold and multi-rank jump. A caller cannot substitute a
looser table or derive the cap from prose.

## GM authority

When final maximum is at least I, the GM may:

- decline the wound;
- create a unique spiritual wound at any lower/equal severity;
- when this side already received its one conflict wound, worsen only that exact wound
  within the new maximum;
- explicitly target an older active wound only when the accepted action carried a
  re-trauma reference.

The GM authors the wound's nature, spiritual locus, consequences, prognosis, and
acquisition narration. The client validates exactly one legal afterlife consequence per
severity step and rejects overpowered/illegal axes.

Every accepted consequence is one of the eight registered persistent spiritual-wound
#1535 profiles on the exact actor/profile carrier. It is not an
`afterlife_combat_condition`, never targets `spiritual_conflict_side`, and never creates
or mutates a `combatConditions[]` row. An active conflict projects only a typed derived
owner-to-current-side contribution; conflict closure removes that evidence while the
wound, source graph, root binding, and actor-carried effect remain under their own
accepted lifecycles.

## One-new-wound-per-side seal

Conflict state holds client-owned per-side evidence:

- first accepted create opportunity/result;
- new conflict `woundId`, if created;
- consumed opportunity keys;
- exact explicit re-trauma targets.

A second create for the same side is invalid. A later opportunity may worsen the new
conflict wound. Declining an earlier opportunity does not consume the side's ability to
receive a later wound, but that exact earlier event cannot be replayed.

## Defeat and dissipation

Every non-training defeat commits one registered bounded outcome that prevents the
losing side from immediately repeating the same aggression/goal. It may be a retreat,
binding, concession, loss of position, temporary access block, or another legal
resolution already supported by the conflict contract. This outcome is required even
when the GM declines a wound.

Soul dissipation:

- is never automatic;
- is never wound severity V;
- is available only with annihilation mode and exact existing winner/terminal proof;
- remains optional even when available;
- may be replaced by a softer legal outcome chosen by the winner/GM.

## Standard arts

Add these visible standard arts at tier 0 for every current profile:

| ID | Russian name | Kind | Mechanical use |
| --- | --- | --- | --- |
| `spiritual_resilience` | Духовная стойкость | passive | target tier in wound opportunity; normal progression |
| `spiritual_healing` | Духовное исцеление | active/capability | diagnosis, treatment gate/check, natural recovery, service capability |

Both progress through the existing afterlife actor/soul art progression rules to tier
V. `spiritual_resilience` is not a combat operation. `spiritual_healing` is admitted as
a healing operation through an explicit operation-to-art mapping and tactical/matchup
contract.

## Active healing resolver

Tier gate: a healer may reduce severity only when `H >= W`. Tier 0 can diagnose and
explain insufficiency. The gate is checked before roll bands, including natural 20.

```text
total      = d20 + 2H + validatedModifiers
difficulty = 10 + 2W + complications
margin     = total - difficulty
```

| Result | Wound outcome |
| --- | --- |
| margin `>= 8` or natural 20 | reduce two steps, bounded at healed |
| margin `0..7` | reduce one step |
| margin `-1..-4` | add one natural-recovery point |
| margin `<= -5` or natural 1 | no active improvement |

A natural 1 overrides a numerically successful total. A result that would cross below
severity I heals the wound and archives it. The transition rematerializes effects at
the resulting severity.

### Weaker consequences: approved authorship, 2026-10-27

The GM authors the weaker consequence graph needed for a computed lower severity;
the client determines the healing/recovery result and validates the proposed
consequences and their application. Existing destination profile/magnitude rules,
exact `rank` slot count, target/source ownership and #1535 materialization remain
mandatory. A legal current graph need not have a legal subset at the next rank:
for example, a severity-III strain burden is unavailable at II. The client does
not invent replacement profiles or choose survivors by effect ID or slot order.
Persisted slots are correlation ordinals, not a healing priority.

Missing or invalid destination consequences use the ordinary repair path with
the original retained attempt/draw. Wound/effect/time/History publication remains
one atomic outcome. The GM cannot replace the client-computed severity, roll,
result band or cycle cost. This clarification records the approved rule; the
executable spiritual continuation and response adapter remain implementation WIP.

### In conflict

- base cost 5 spiritual action points;
- ordinary tier/cost reduction through `AfterlifeActionCostRules`;
- absolute floor 2;
- typed resource spend through `AfterlifeSpiritualConflictResourceOutcome` and common
  accepted mechanics;
- explicit counterable tactical/matchup semantics;
- no natural safe-cycle recovery unless the conflict closes and a separate safe cycle
  is accepted.

### Outside conflict

- one treatment session advances exactly one accepted safe afterlife cycle;
- no action points are spent;
- one active attempt per wound per cycle regardless of healer;
- self-treatment spends no currency or item;
- provider compensation is processed atomically when applicable;
- failure still advances the world and applies only ordinary natural recovery once.

### Client-owned request preparation (T104/T108 WIP)

Before signed snapshot capture, the client stages one closed source envelope at
`game_state/wounds/wound_commands.json` (the existing wound command root).
This schemaVersion2 `spiritual_self_treatment_request` is intentionally disjoint
from accepted schemaVersion1 commands: it has no `snapshotToken` or `commands`.
It contains exact sessionId/requestId/turn, a request fingerprint and one attempt
with attemptId/woundId/woundFingerprint, realm/cycleOrdinal/cycleKey, complete
source fingerprint and `roll: {mode, values}`. The retained mode determines one
normal die or two advantage/disadvantage dice; the actual healer/art/effect check
is recomputed by the client. A request alone grants no wound or cycle authority.

This is a client-only preparation surface, never GM-authored output. The GM must
not create or rewrite it. Current implementation is not connected to a player
handler or accepted publication yet. Snapshot authentication, GM destination
consequences, actual wound/effect/world/History application and operational GM
examples remain the following functional block; unavailable UI wording remains.
Exact pending retry preserves the original bytes and draw; a different request,
changed source or invalid envelope cannot overwrite it. No currency/item/OD,
wound, world schedule or History mutation is part of request staging.

## Natural recovery

Every safe cycle adds `1 + Spiritual Healing tier` points to each eligible active
spiritual wound. Thresholds for the current step are I=2, II=4, III=6, IV=8. Overflow
carries through later steps; worsening resets current-step progress.

Cycle identity comes from the accepted `ProgressionScheduleService` contour and
`game_state/control/progression_schedule.json`, not the wall clock. The same cycle key
cannot tick twice. Active conflict/unsafe time is not a safe cycle.

The rule applies to player soul, Guardians, residents, leaders, radiant actors, and
other persistent afterlife profiles. Entity automatic progression may improve the art
tier; player-soul handling must not be skipped merely because the generic profile
progression loop has a different owner path.

## Healing another entity

The player may treat a reachable consenting Guardian/resident/other entity when exact
profile authority proves the art tier and wound. An entity may similarly treat the
player or another entity. Unwilling treatment requires a legal conflict action.

No provider lookup uses a typed name or visible array index. The command binds the
selected nearby entity to a hidden exact profile identity and revalidates it before
commit.

## Elyara guarantee

The built-in Guardian `elyara` / Элиара Последней Раны must satisfy all of these
client-protected invariants:

- initially present and discoverable from first Chaos Sea entry even when another active Guardian
  was selected;
- Spiritual Healing tier V, protected from GM/profile downgrade;
- initial public location `Лазарет Незаживающего Света`, with accepted relocation preserved;
- public service multiplier 100%;
- Ink Feather prices 25/50/100/200 for severity I-IV;
- negotiated favor/debt/quest/free-aid compensation when her character contract allows;
- every route feeds the same active-healing resolver and one safe cycle.

The existing system Guardian manifest/dossier, fresh-game profile construction,
availability logic, prompts, and tests must all agree. Library `alwaysAvailable` alone
is not sufficient service materialization.

These initial guarantees do not make Elyara immortal or permanently present at the
Lazaret. «Пойти к Элиаре за лечением» is one universal command resolving her current
canonical location, including the Shining Abode when she accompanies the player,
subject to ordinary reachability/access. After her death the command is unavailable.
Both UIs and command execution revalidate life/location/access; stale offers reject
before payment, travel or treatment. Bootstrap/normalization must not relocate,
resurrect or duplicate her to restore service availability.

## Shining faction healer role

Every materialized Shining faction must have at least one visible resident in
`game_state/meta/guardian_abode_residents.json` whose primary role is
`healing_support`. Its exact actor profile must prove Spiritual Healing tier I-V.

- The role is visible with the ordinary faction roster.
- Multiple healers are legal.
- Tier reflects faction fiction and progresses normally; there is no fixed universal
  tier.
- The role proves faction support capability, not public service.
- A public/restricted command service exists only when an explicit
  `healingServiceProfile` supplies availability, location, price, and access.
- A visible but inaccessible healer remains visible and returns a readable access
  requirement rather than disappearing.

### Current interactive access display

The existing console political inspection and browser faction detail may display only
publicly declared current service terms for an exact visible materialized healer.
Absent/private/malformed service terms do not erase a confirmed healer role and do not
expose private conditions. Declared availability is not proof of player-specific access.
Feather tariff display requires explicit ink_feathers and respects the existing H>=W
capability limit; other compensation alone implies no currency offer. Read/back/cancel
is inert. Shining command treatment/negotiated execution remains unimplemented by this
read-only surface; role or displayed terms never authorize a quote, charge or healing.
No historical broken/dissolved-faction invariant decision is made by this view.

## Service price and compensation

```text
base(I/II/III/IV) = 25 / 50 / 100 / 200 Ink Feathers
multiplier         = integer 50..200 percent
quote              = ceil(base * multiplier / 100)
```

The quote is sealed before confirmation. An accepted attempt charges exactly once,
including partial success/failure. Cancellation, validation failure, or rollback leaves
no charge. Ink Feather mutation uses the existing typed accepted resource/accounting
authority; UI-local direct subtraction is forbidden.

An accepted roleplay agreement may replace currency with favor, debt, quest,
allegiance, or free aid. It must be materialized before treatment and cannot alter the
tier gate, roll, wound/target, safe-cycle cost, or retry key.

## Documentation contract

Because this changes afterlife state, scheduler, response, command, and GM authoring,
the same implementation must update:

- `OtherGuides/Afterlife_Contract_Matrix.md`;
- `Examples/E_CLI_Afterlife_Turns.txt`;
- `Examples/example_validation_manifest.json`;
- `TaskGuides/CLI_Step_Main.txt` and relevant rule blocks;
- `AfterlifeContractRegistry` if a pending/control root is introduced;
- Guardian/Shining prompts and Elyara manifest/dossier;
- afterlife documentation coverage and example validation tests.


spiritual_public_service_v7: shared /лечить offers a confirmed exact public Ink Feather
quote for the player's own spiritual wound while co-located with alive Elyara in safe
Chaos Sea. Canonical guard_system_elyara_001 has lifeState alive|dead, HealingV, public
100%; initial abode_system_elyara_001 is preserved only at fresh seed, never restored
after accepted relocation/death. Prices25/50/100/200, multiplier50..200%, round UP.
The client owns quote/payment/control/attempt/draw in private wound_commands family7,
one actual common C4 wallet+wound/effects/History/cycle publication and strict result7
replay. Accepted failure/partial charges once; cancel/invalid/rollback charges zero.
No GM inkFeatherChanges or manual fee/receipt/roll. Read the current full service
section and worked example; author only requested weaker graphs, progression report
and actual first-person healer reasoning. Remote/Shining/negotiated routes remain
unavailable in this first slice; stale life/location/access/price/funds refuses.


elyara_current_location_v1: /элиара (/elyara), or «Пойти к Элиаре за лечением»,
resolves ONE exact alive canonical Elyara across all profile realms, using current
realm/locationId and matching service locationRef. guardians[].abode is HOME metadata,
not current presence. Never respawn, duplicate, resurrect or move her to satisfy a route.
A confirmed [ELYARA_VISIT_V1] retains the client's complete sealed Action, including
its ordinary [CHAOS_SEA_TRAVEL] target. The already-discovered target's actual host
owns activeGuardian, even when that host is not Elyara. Preserve her original current
life/place/service; current GM edits cannot mint pre-turn discovery or permission.
ONE ordinary travel cycle includes normal natural recovery, but ZERO active treatment,
healing die or fee. On arrival open a NEW /лечить quote and confirm it separately.
Co-located same-Elyara treatment also works in safe Shining Abode: availability=active,
currentHallId must
resolve one current hall and her explicit current location must match; the existing
family7 paid owner publishes one Shining cycle, leaving the inactive Chaos contour alone.
Author only requested spiritualHealingGraphs, ordinary report and real healer memory;
NO GM inkFeatherChanges/payment receipt, new quote/control or private permission.
Read/back/cancel is inert. Death/stale/unknown/discovery/access loss refuses. Remote
Shining halls and cross-realm access require an already-supported ordinary route;
no implicit hall travel, return/reentry, teleport or Enlightenment reset is added.
Negotiated services and the historical-faction healer decision remain OPEN. See the
elyara_current_location_v1 worked example; faction-healer display itself grants no treatment.
