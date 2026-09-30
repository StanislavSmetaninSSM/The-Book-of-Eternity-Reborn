# Effect Materialization v1

This is the shared Mortal World and afterlife contract for active runtime effects. The GM authors static source policy and top-level lifecycle requests; the client owns canonical instances, identity, stacking, time, history, receipts, indexes, pending state, publication, and rollback.

## 1. One authoring route

Static effect policy belongs in an exact current source entity under `activeEffectDefinitions[]`. A turn may then request lifecycle only through these optional top-level arrays:

```json
{
  "effectChanges": [],
  "effectResolutionReceipts": [],
  "effectEventReports": []
}
```

`effectChanges[]` accepts `apply`, `dispel`, and `remove`. `effectResolutionReceipts[]` answers an exact client-created bounded request. `effectEventReports[]` reports closed audited events to a registered client adapter; it never chooses an effect or trigger. None of these arrays is canonical state: after an accepted turn the client consumes the transient command root.

Never author or resend canonical `activeEffects[]`, combat `activeBuffs[]` / `activeDebuffs[]`, afterlife `combatConditions[]`, `effect_identity_index.json`, or `pending_effect_resolutions.json`. Never invent `effectId`, transition/history/receipt IDs, remaining lifetime, current stacks, carrier paths, deadlines, or terminal state. A present non-empty legacy carrier is rejected; there is no runtime migration or compatibility route.

## 2. Complete source definition

Every definition is a closed object containing all of these sections:

```text
schemaVersion, definitionKey, display, allowedRealms, allowedTargetKinds,
components, parameterBounds, stacking, lifetime, triggers, removal, links
```

The representative GM source selector kind is one of:

```text
skill, spiritual_art, item, wound, quest, location, hazard, faction,
world_event, fate_card, combat_action
```

Client-owned `wound_legacy` is a separate canonical source kind, not an additional GM `effectChanges[].source` selector. A lasting mechanical consequence of an accepted heal uses `source.kind=wound_legacy` and `source.sourceId=legacyId`; a source-bound canonical lifetime derives `lifetime.linkKind=wound_legacy` and `targetId=legacyId` from that source owner. Authored legacy definitions require `links=[]` and retain their existing lifetime policy, never canonical `linkKind` or `targetId` lifetime fields. Structural validity is not creation authority: only the sealed wound treatment/effect transaction may create this source and its effect, and accepted typed legacy history supplies durable reload authority. Healing cleans up exact `sourceKind=wound` effects, not the independent legacy source; later effect removal does not erase legacy provenance. Never write, copy, or repair these canonical coordinates manually. The T070 heal/history/reload producer remains pending; this structural vocabulary alone does not enable that flow.

The exact source ID must resolve in the accepted composed state. Display names, array positions, case variants, whitespace variants, confusable aliases, historical IDs, and prose are never authority. Same-turn materialized item/location owners use the exact temporary source reference exported by their accepted plan; the client seals it to a permanent ID.

`display.category` is `buff`, `debuff`, `condition`, `environmental`, or `mixed`. `display.visibility` is `visible`, `hidden`, or `gm_only`. Hidden and GM-only mechanics may affect the internal accepted snapshot, but ordinary player output must not reveal their name, description, value, source, existence, selector, or technical DTO.

## 3. Registered component profiles

The eleven general profiles are listed here. Wound Materialization adds the exact eight
spiritual-wound profiles in section 7.1; they are first-class registered profiles, not
aliases of the general afterlife condition profile.

1. `characteristic_modifier`
2. `roll_modifier`
3. `resistance_modifier`
4. `periodic_damage`
5. `periodic_restore`
6. `action_control`
7. `event_reaction`
8. `wound_consequence`
9. `afterlife_combat_condition`
10. `periodic_spend`
11. `periodic_gain`

`periodic_spend` has the closed payload `resource`, positive `amount`, and
`floorPolicy`; `periodic_gain` has `resource`, positive `amount`, and `capPolicy`.
They produce Spend/Gain, so they can affect `energy` or `spiritual_action_points`
only when the exact target owns that resource and the accepted source authorizes
the component. They do not accept `damageType`. Damage/Restore profiles remain
unchanged and cannot substitute for AP spending/recovery. Use the existing floor
and cap vocabulary; registered resource bounds remain authoritative. In particular,
`cannot_reduce_below_one` rejects an effect spend that would leave less than one.

Both profiles support deterministic triggers and bounded `resource_delta` receipts
through the normal pending-effect protocol. Events are `resource_spent` and
`resource_gained`; a reaction to its own event still obeys use budgets and causal
closure. A one-use recovery on `resource_spent` can restore one AP after an
ordinary two-AP cost: 6 -> 4 -> 5. The GM must author the source definition and
trigger, not write 5 into the canonical ledger or fabricate the client receipt.
These generic profiles do not expand any wound-specific consequence allowlist.

Each component has an exact unique `componentId`, registered `profile`, integer `priority`, and the closed payload required by that profile. Numeric values must be finite and inside source-owned `parameterBounds`. Narrative `name`, `description`, `reason`, `effectSummary`, or custom JSON never creates mechanics.

Triggers name exact registered events and exact component IDs. `resolutionMode=deterministic` is resolved entirely by the client. `resolutionMode=bounded_receipt` may create `pending_effect_resolutions.json`; the GM must return the exact allowed result in a full-turn resubmission and must not copy the pending DTO into player-visible output.

Bounded receipt waves use one sequential rule: Answer the current safe packet only. Resubmit the same complete semantic turn with receipts only for that packet; the client carries earlier-wave terminal bindings. The original candidate, mutation authority, and source authority remain immutable/client-owned. A receipt supplies only an allowed result and reason; it never reconstructs, retargets, merges, or changes the protected causal origin.

### Exact roll scope: one common profile

Every `roll_modifier.payload` is a closed object with exactly `operations`,
`contribution`, and `scope`. `contribution` is `advantage` or `disadvantage`.
There are exactly two closed scope variants. Broad `scope.kind=all` has no `skillId`:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "all" }
}
```

Focused `scope.kind=skill` requires exactly one non-empty permanent canonical
`skillId`, and its operations must equal exactly `operations=["skill_check"]`:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "skill", "skillId": "skill_lockpicking" }
}
```

Broad scope still applies only to the declared registered operations and exact effect
target/realm; it does not mean every actor or every operation. The common roll operation
registry is exactly `attack_roll`, `defense_roll`, `skill_check`, `saving_throw`,
`damage_roll`, and `initiative_roll`; spiritual operations do not extend that registry.
Missing scope never
means broad scope. Extra fields, a `skillId` under `all`, arrays or multiple IDs under
`skill`, aliases, names, and focused operations other than the single `skill_check`
are invalid. `scope` is semantic source policy, not a scalar `parameterBounds` target.

Before selecting a focused scope, read the client-authored
`turn_request.json.effectSkillScopeCatalog`. Its closed schema-v1 projection has
`schemaVersion` and `targets`; every target row has exact `realm`, `kind`, `targetId`,
and `skills`, and each selectable skill has `skillId` and a readable `displayName`.
The skillId must come from the exact target row. This is a bounded, deterministic,
detached advisory catalog, not a writable authority grant. The client independently
rebuilds authority from canonical offered pre-turn roots and the composed final
accepted state. A new focused binding needs the same exact usable active/passive
skill owned by that target in both states. Thus same-response new skills are not selectable;
a skill removed or disabled in that same response is not selectable either. An empty
or missing matching row does not authorize an invented selector. Unknown, wrong-owner,
idless, inactive, terminal, duplicate, ambiguous, or Unicode-confusable identities
fail closed. There is no fuzzy or display-name matching.

For an already accepted focused component, legitimate later absence or unavailability
derives dormancy: later missing skill makes the component inactive without healing/removing the wound.
This does not itself change effect/wound identity, severity, treatment, lifetime, or
history. A similarly named skill or a newly learned different ID does not inherit the
component. Only restoration of the same permanent canonical ID in usable, unambiguous
current authority reactivates it, provided the effect itself is still active. Normal
effect expiry and wound treatment continue under their own policies. The trusted roll
context must match exact actor, realm, operation, and focused skill before the existing
advantage/disadvantage reduction; repeated same-direction contributions do not escalate,
and opposite directions cancel.

For wound-owned definitions, one whole broad or focused component consumes one slot:
one focused component consumes one consequence slot. Multiple declared broad operations
do not expand that component into extra slots. Affecting two exact skills requires two
components and two slots within the wound's severity envelope; scope never increases
power or grants a duplicate consequence coordinate. Physical wounds remain free,
setting-specific GM-authored entities: there is no catalog of ready-made wounds.

A rejected selector publishes no partial mechanics and is never silently broadened,
renamed, or rebound. For a safely repairable wound selector failure, the current
`wound_materialization_repair` packet preserves the original response-local coordinate,
for example `woundDecisions[0].proposal.consequenceDefinitions[0].definition.components[0].payload.scope.skillId`.
Use its `preservedProposal` and correct only its listed paths, then resubmit the complete
semantic turn through the owning repair protocol. Do not repair canonical state or
change protected source/target/opportunity authority. If no exact lawful selector is
available under that packet, obtain fresh authority rather than guess. Ordinary effects
remain subject to their own bounded repair eligibility in section 9; a selector error
does not create a new permission to edit an accepted source. Exact replay preserves the
scope; changing `kind` or `skillId` changes the request.

Use safe in-world Russian names in player text, such as «Помеха к проверкам навыков»
or «Помеха к проверкам навыка „Взлом“». Technical IDs, catalog rows, repair coordinates,
and hidden-effect existence remain operator-only. Worked broad/focused wound examples
are `wound_mortal_roll_scope_all_v1` and `wound_mortal_roll_scope_skill_v1` in
`Examples/E_CLI_Wound_Materialization.txt`; the ordinary-source counterpart is
`effect_mortal_roll_scope_skill_v1` in `Examples/E_CLI_Effect_Materialization.txt`.

### 3.1 Closed event reactions

`event_reaction.payload` has one exact `eventType`, `resultKind`, `dependency`,
and `maxExpansion` from 1 through 64. Its six result kinds are:

- `apply_definition`: requires a same-source `definitionKey` plus closed
  `parameters`; the referenced definition must cover the same realms/targets;
- `trigger_component`: requires one periodic damage/restore `componentId` in
  the same definition and executes deterministically;
- `bounded_receipt`: requires one periodic component and uses an exact pending
  request/receipt instead of letting the GM invent mechanics;
- `event_outcome`: requires one transition from the closed client registry,
  such as `owner_critical_failure: critical_failure -> failure`; it records the
  source-owned event transformation without replacing lifetime advancement;
- `suspend`: creates the client-owned suspension transition for the reacting
  effect;
- `remove`: creates the client-owned terminal removal transition.

The dependency is exactly `before_current_event`, `after_component`, or
`after_current_event`. `trigger_component` and `bounded_receipt` are causal
resource-component edges and therefore require `after_component`; an
unconditional resource component belongs directly in the trigger.
`after_component` additionally requires an exact `afterComponentId` selected
by the same trigger and runs only if that exact mutation applied; replay,
skipped work, and `narrated_no_state_change` do not satisfy it. For a terminal,
downstream, or outcome reaction discovered from a resource event,
`before_current_event` still follows the resource transition that emitted the
event—it precedes continuation of the derived event, not its producer.
Once a trigger is accepted, its unconditional `before_current_event` and
`after_current_event` reactions still run even if a child mutation is skipped,
clamped to no change, or recognized as an exact replay; only dependent
`after_component` output is withheld, and an accepted consuming trigger still
spends exactly one use.
Resource-event routing preserves both the outer producer/event requirement and
every component-local dependency. An `after_component` child therefore needs
its exact predecessor as well as the outer producer; the client never replaces
one requirement with the other.

Each reaction component has its own source-declared execution budget for the
whole accepted transition, and all reactions together have a hard ceiling of
64. Direct and reaction dispatch of the same component, ambiguous references,
dependency/downstream cycles, conflicting budgets, same-stack downstream
application without explicit `replace`, and either budget overflow fail before
publication. Canonical child effects may contain client-owned
`chronology.causalEventRef`; this is derived replay/causality evidence and is
never a GM-authored field.

The client freezes every actual-event candidate batch before output. A final
use or accepted unconditional deferred suspend/remove immediately reserves its
exact effect against later event boundaries, but cannot cancel siblings already
accepted in the frozen batch. Reaction-created and replacement effects become
eligible only on the next accepted mechanics transition.

For `apply_definition(policy=replace)`, the client—not the GM—derives the exact
pre-reaction target from realm, target, source, and `stackKey`. The
`definitionKey` is excluded, so the reaction may replace another effect at that
coordinate.
That exact target identity is immutable pre-reaction authority.
Actual release immediately blocks that old target from later or nested event
boundaries. An `after_current_event` replacement reserves it as soon as the
frozen batch is accepted. Already accepted siblings still finish.

If accepted consuming activations belong to that retired target, the client
validates their immutable `N, N-1, ...` budgets and records every use in
activation order. Those uses remain on the retired identity immediately before
the target's single authoritative `replace`. Intermediate and final replacement identities keep their complete
source-created lifetimes. Even on the old effect's final use, its terminal state
is `replaced`.

### 3.2 Dedicated Mortal QTE receipt transport

If a selected Mortal QTE terminal outcome reaches `bounded_receipt`, the client
enters `awaiting_receipt` and publishes a closed request with
`requestKind=qte_deferred_effect_resolution` at
`input/qte_effect_resolution_request.json`. The GM returns the closed
correlated envelope at `output/qte_effect_resolution_receipts.json`; the helper
then writes `ready/qte_effect_resolution_complete.json` last.

This is a receipt-only task and not an ordinary turn. Read only the current
request's `safePacket`, answer its current wave through
`effectResolutionReceipts[]`, and do not read or reuse `input/turn_request.json`.
Do not create a pending-turn snapshot, run story or progression work, write
narrative/interface/canonical state, advance lifecycle, or increment the turn
counter. Construct only the receipt objects in memory and call
`Complete-BoeQteEffectResolution -Receipts $receipts` as the last action. The
helper copies exact correlation, rejects extra fields or disallowed results,
writes the receipt envelope, and publishes the ready marker last; the GM never
writes either transport file directly.
Do not call `Complete-BoeValidationRepair`: the dedicated QTE receipt transport
does not reuse the ordinary validation-repair request, helper, ready marker, or
full-turn resubmission loop.

On restart, the client resumes the same sealed continuation and current wave.
Its `resolvedWaveBindings` and the same preallocated identities preserve all
earlier-wave bindings as client-owned evidence. A later wave has a new
`requestId`, `waveId`, `waveOrdinal`, and pending fingerprint but keeps the same
session, continuation, selected terminal, full-turn, and semantic-turn
authority. Answer the current safe packet only; never resend earlier receipts
or reconstruct hidden mechanics. Until the terminal wave succeeds, no resource
or effect after-image, QTE history, story, progression, or turn counter is
published. The final wave publishes the complete selected QTE outcome
atomically. Missing, stale, tampered, cross-session, cross-generation, or
mis-correlated transport fails closed before any mechanics write.

## 4. Stacking

The five policies are:

- `independent`: create a separate identity up to the source bound;
- `stack`: preserve one identity and increment its client-owned count;
- `refresh`: preserve identity and reset/extend only the declared lifetime;
- `replace`: retire the old identity and atomically create the replacement;
- `merge`: preserve one identity and use only a reducer registered by every component profile.

The source owns `stackKey`, `maxStacks`, `atMaximum`, `refreshMode`, and `mergeRule`. The GM never submits current stack count or computes the post-stack payload.
An explicit incoming `replace` may replace one prior valid policy at the same
coordinate; the retired effect does not need to already use `replace`. Multiple
prior identities at that coordinate are ambiguous and fail before publication.

## 5. Lifetime

The eight lifetime modes are:

- `turns`: positive turns, advanced at the declared owner/world phase;
- `uses`: positive uses consumed only by declared events;
- `until_time`: positive duration resolved against `world_time.currentTimeInMinutes`; the client writes the deadline;
- `scene`: exact scene/conflict binding with declared exit behavior;
- `source_bound`: exact source link plus registered `active`, `carried`, `equipped`, or `unlocked` predicate;
- `condition_bound`: registered machine-evaluable condition and exact operands;
- `permanent`: no numeric sentinel, with source-authorized closure unless explicitly irreversible;
- `manual`: exact non-empty cure/dispel/removal authority.

The GM never uses a magic duration to mean permanent, expired, or removed. Apply, removal, trigger execution, use consumption, expiry, cleanup, and source/condition loss follow the client scheduler and are idempotent under the accepted event.

## 6. Apply, dispel, remove

An apply request identifies exact target, exact source definition, bounded parameters, exact accepted event, and a readable reason:

```json
{
  "operation": "apply",
  "target": { "kind": "player", "targetId": "player_current" },
  "source": {
    "kind": "skill",
    "sourceId": "skill_battle_focus",
    "definitionKey": "battle-focus"
  },
  "parameters": {},
  "eventRef": { "kind": "accepted_turn", "authorityId": "turn_42" },
  "reason": "Герой удерживает боевое сосредоточение."
}
```

The first operation uses `turn_<turn>`; later operations use their exact ordinal `turn_<turn>_effect_<n>`. Dispel/remove requests may use an opaque existing `effectId` only when it is supplied in technical context and must include exact target, accepted event, and source-authorized counteraction/removal authority. Never infer a target or effect from a display name.

Client-owned mechanics may expose a closed built-in source without asking the GM to persist a fake source entity. Built-in definitions are registered, validated, fingerprinted, and resolved through the same source authority as canonical entity definitions; their application additionally requires an exact sealed accepted-turn grant. The current built-in is Ink Feather Fate Shield:

```json
{
  "kind": "fate_card",
  "sourceId": "builtin_ink_feather_fate_shield",
  "definitionKey": "fate-shield-next-critical-failure"
}
```

It is granted only by exact Mortal action marker `[INK_FEATHER_ACTION: FATE_SHIELD]`, materializes one independent one-use `event_reaction`, consumes on registered event `owner_critical_failure`, and never authorizes a direct carrier write. Unknown built-in grants or source-declared application authorities fail closed.

The GM reports that later event through `effectEventReports[]` with exact
`mortal_action_roll` evidence: closed target `player/player_current`, exact
`rollMode`, leading sealed `diceIndexes`, selected index/value, and
`critical_failure -> failure`. The selected die must be the correct sealed d20
for that roll mode and exactly `1`. The client selects the oldest eligible
shield and its source-owned deterministic trigger; the GM never sends
`effectId`, `triggerId`, remaining uses, or carrier post-state. Missing authority,
wrong dice/outcome, duplicate reports, and client-owned selectors fail closed.

## 7. Wounds are independent entities

A wound may own a complete definition graph and exact wound source link. In a new-wound
proposal the GM supplies each complete definition under
`woundDecisions[].proposal.consequenceDefinitions[]` with exact empty `links`; the
client binds the accepted wound source. The exported wound source is
non-materializable to ordinary GM `effectChanges[]`. Only the client-derived typed root
batch may create its declared direct roots, while later descendants execute through the
sealed common reaction engine.

The exact wound link grants read-only source/loss authority to the effect planner.
Effect removal never heals or deletes the wound. Treatment is a separate accepted
wound transition; an effect-only turn cannot mutate wound bytes, severity, treatment
state, or wound identity. Healing retires the exact wound-owned root/descendant group
but preserves unrelated effects and global terminal provenance.

## 7.1 wound_spiritual_profiles_v1

For `spiritual_action_cost_burden`, the exact owner's applicable burden is paid
before `recover_spiritual_power`, including failure; insufficient funds reject.
`force_incarnation` has no base cost and requires only the positive wound burden
in its seven-field 0/0/0 audit; without burden it remains free without audit.
The action-only recovery audit and ordered Spend/reactions/Gain rule are documented
under `spiritual_wound_special_action_costs_v1` in
`OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, with GM-authored fragments
in `Examples/E_CLI_Afterlife_Turns.txt`. Other unresolved profiles are unchanged.

Wound Materialization v1 extends the common component registry with exactly eight
deterministic spiritual-wound profiles:

| Profile | Exact axis | Exact magnitude domain |
| --- | --- | --- |
| `spiritual_roll_hindrance` | `rollMode` | `disadvantage` |
| `spiritual_action_cost_burden` | `actionCostAudit` | integer 1, 2, or 3 |
| `spiritual_position_burden` | `conflictPosition` | integer 1 or 2 |
| `spiritual_control_burden` | `controlState` | integer 1 |
| `spiritual_strain_burden` | `sideStrain` | integer 1 |
| `spiritual_tempo_burden` | `tempoAdvantage` | `deny_one_gain` |
| `spiritual_counter_burden` | `counterPayoff` | `reduce_one_step` |
| `spiritual_art_restriction` | `artAvailability` | `restrict` or `forbid` |

Every component uses the closed `{ operation, axis, magnitude }` payload and only the
`profile_specific` merge reducer. The common registry validates exact payload shape,
operation set, fixed axis, magnitude type/value, deterministic resolution, afterlife
realm, persistent-actor target, and exact wound source link. The wound contract alone
validates severity availability, severity-specific magnitude, slot count, duplicate
coordinates, and aggregate safety.

These profiles MUST NOT masquerade as `afterlife_combat_condition`, target a spiritual
conflict side, or write `combatConditions[]`. Their canonical effect remains on the
persistent actor. A client-owned projector may derive a temporary current-conflict
contribution only after resolving that actor to one exact participant and side. Effect
expiry/removal does not heal the wound, and wound healing retires only the exact
wound-owned source group. See `OtherGuides/Wound_Materialization_Contract.md` and the
two named spiritual-wound examples in `Examples/E_CLI_Effect_Materialization.txt`.

## 8. Afterlife adapter

Afterlife actors store the same canonical `activeEffects[]` contract in their validated profile. Spiritual conflict conditions use the same identity/lifecycle engine through the specialized `afterlife_combat_condition` profile. The five player-facing condition kinds are `mark`, `ward`, `burden`, `opening`, and `vow`; their source definitions bind the exact conflict side, affected operations, legal axes, bounded turns/uses/scene lifetime, counterplay, payoff, and visibility.

The GM requests their lifecycle through `effectChanges[]` and must never author combatConditions[]. Current accepted contributions are read only from `afterlifeSpiritualConflictPreview.conditionMechanics` with `source=accepted_effect_mechanics_snapshot_v1`, and each contribution is cited only on its exact audited axis. Hidden/`gm_only` conditions remain private.

Shining blessing entitlement is not a generic active effect. Blessing allocation, rerolls, memory selection, and resource capacity remain on their dedicated client-owned Shining contracts and must not be recreated as an effect definition or carrier.

## afterlife_roll_scope_v1

The common closed roll payload also applies in Chaos Sea and Shining Abode. In the
existing `afterlife_effect_profile_v1` worked example, `operations=["defense_roll"]`
uses broad `scope.kind=all`; it is not a focused `skill_check`. This component contributes
only when the client actually resolves a typed `defense_roll` for the exact actor and
realm. In particular, spiritual guard is not a roll_modifier operation and is not an
alias for `defense_roll`. It receives no contribution merely because the fiction says
the actor guards. Spiritual `guard` uses its dedicated afterlife condition/wound-profile
contracts; this common-source example does not change the spiritual combat model.
A spiritual art may own this source definition, but spiritual arts are not Mortal skill IDs.
Do not reinterpret an
`artId`, an art name, a tier, or an afterlife actor ID as a focused skill selector.
`effectSkillScopeCatalog` exposes canonical Mortal player/NPC skills, not a spiritual-art
catalog; an afterlife-only target without the exact selectable Mortal skill row cannot
receive a new focused binding. The dedicated spiritual-wound profiles retain their own
closed operation/axis/magnitude rules and do not gain skill scope through this change.
See `Examples/E_CLI_Afterlife_Turns.txt` under `afterlife_roll_scope_v1`.

## 9. Failure, repair, and privacy

Unknown/missing/duplicate fields, wrong types, pseudo-mechanics, unauthorized source/target/realm/link, client-owned identity, direct carrier/index edits, stale events, and malformed receipts fail before publication with zero canonical writes.

Only one exact semantic omission may receive a bounded repair packet. Identity/source/target/realm/stack/history/receipt/direct-mutation failures are protected and never retargeted. Before any actionable repair, the client restores the validated baseline. For `effect_materialization_repair`, root `fullTurnResubmissionRequired=true` requires a coherent full-turn resubmission; an effect-only patch, empty retry, or stale replay is rejected. Root `requiredResubmissionPaths` is the exact list of changed GM-authored command/output surfaces, never client-owned preparation/publication state. The client restores or republishes `system_mods.json`, `progression_schedule.json`, the resource definitions/state/history/owner-authority quartet, `pending_effect_resolutions.json`, and `effect_identity_index.json`; the GM never writes them or adds them to the replay list. Technical paths, IDs, pending DTOs, repair diagnostics, and exception text stay operator-only.

## 10. Worked examples

- Mortal construction, all profiles, policies, lifetimes, sources, wound independence, the exact eight spiritual-wound profiles, one complete spiritual-wound source graph, rejection, and bounded repair: `Examples/E_CLI_Effect_Materialization.txt`.
- Fate Shield purchase plus a later sealed critical-failure event report: `Examples/E_CLI_Ink_Feather_Actions.txt`.
- Afterlife profile and all five spiritual-condition lifecycles: `Examples/E_CLI_Afterlife_Turns.txt`.
