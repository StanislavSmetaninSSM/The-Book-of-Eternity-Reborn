# Wound Materialization v1

## Kind-specific safe wound repair transport

Wound repair transport: every wound_materialization_repair packet requires candidateKind, exactly construct_wound, repair_wound, author_alternative_treatment, or narrate_acquisition. Missing or mismatched tags fail; there is no tagless reader. Construction/narration uses requiredResponseShape.woundDecisions[0].proposal.correctOnly and repeats display.acquisitionNarration verbatim. Alternative authoring uses requiredResponseShape.woundTreatmentAuthorings[0].route.correctOnly and requiredResponseShape.woundTreatmentAuthorings[0].diagnosisPath.correctOnly, with immutable authoringRequestRef and decision=author. Preserve every non-offending sibling and ordered array position; replace invalid required values, omit unknown fields, and append a missing route reveal only when explicitly requested, preserving its entire old prefix. A non-offending null diagnosisPath remains explicit null. Recipe base/correctOnly objects are instructions, not submitted route members. NeedsAnotherRepair is local staged progress only: refresh the privately bound rejected draft and its fingerprint before another bounded repair; never reuse the old packet for a changed draft. Public transport cannot reconstruct private alternative repair authority: there is no live alternative repair adapter. Do not fabricate an accepted command, receipt, operation key, wound identity, or canonical/evidence seal from this example. Alternative treatment adds no acquisitionNarration obligation; it does not describe receiving a new wound. A valid decline has two explicit null payloads and no repair work; correction cannot switch author to decline.

This is the GM-facing contract for constructing a physical or spiritual wound after
the client has exposed one exact wound opportunity. It applies in the Mortal World,
Chaos Sea, and Shining Abode. A wound is an independently treatable entity; it is not
a status line, an active effect, a combat condition, death, or soul dissipation.

There is no catalog of ready-made wounds. The GM authors the name, nature, symptoms,
prognosis, setting-appropriate treatment model, and narration. The client validates a
closed schema, the event-owned severity limit, registered mechanical primitives, and
all permanent identity and publication work. This is a direct schema-v1 technical
cutover with no migration of non-empty legacy saves.

Independently suppressing a wound-owned effect is not wound healing: its canonical
root binding and slot remain even when its carrier is gone. On an already authorized
wound transition, a retained legal coordinate rematerializes as a NEW runtime effect
identity with the exact old canonical root as its sole first-create parent, including
when that root is expired, dispelled, removed, or replaced. The complete terminal
history remains unchanged. The client proves the full lineage and exact definition-
derived owner/source/target/stack coordinates; missing carriers are not fallback
authority. The GM authors neither runtime IDs nor historical parents. This shared
rule grants no new spiritual treatment or still-pending healing art.

## wound_constructor_v1

Read the client-authored opportunity before answering. Its `opportunityRef`, realm,
domain, `maximumSeverity`, optional guaranteed result, and safe context are authority;
they are not suggestions. Return exactly one matching entry in `woundDecisions`:

```json
{
  "woundDecisions": [
    {
      "opportunityRef": "wound_opportunity_public_17",
      "decision": "materialize",
      "woundRef": "fractured_resonance_local",
      "proposal": {
        "classification": {
          "woundType": "Трещина духовного резонанса",
          "locationProfile": {
            "kind": "spiritual_axis",
            "readableLocus": "узор воли"
          }
        },
        "display": {
          "name": "Трещина в узоре воли",
          "description": "Ритм души сбивается при попытке удержать давление.",
          "visibleSymptoms": ["дрожащий контур души", "срывающийся внутренний ритм"],
          "prognosis": "Без исцеления помеха сохранится в следующих конфликтах.",
          "visibility": "known_to_player",
          "acquisitionNarration": "Удар проходит сквозь защиту, и узор вашей воли покрывается светящейся трещиной."
        },
        "severity": "II",
        "complications": [],
        "consequenceDefinitions": [],
        "treatment": {
          "diagnosisPaths": [],
          "routes": [],
          "knownRouteIds": [],
          "completedRouteIds": []
        },
        "recovery": {
          "mode": "progressive",
          "clockKind": "afterlife_world_time",
          "cadence": 86400,
          "currentStepProgress": 0,
          "currentStepThreshold": 3,
          "lastTickKey": null,
          "blockers": [],
          "carryOverflow": true,
          "deteriorationPolicy": null
        }
      }
    }
  ]
}
```

## mortal_wound_treatment_scalar_course_v1

GM authors this complete `treatment` member inside an ordinary complete Mortal wound
proposal. It is not a canonical-state patch and does not enable a fresh alternative-treatment adapter.
The field medic and medicines here belong to this worked setting, not a global medical catalog.

An empty result is legal only for an active milestone. The client consumes only the current milestone's dose,
never future doses. Course start sets and continuations preserve the client's exact active course ID;
completion or trusted interruption clears it. Route completion is not necessarily full wound healing:
this course reduces severity II to I and stabilizes the wound, which remains active.
Stale history is invalid authority, not a missed-dose outcome; other treatment does not cancel a course.
A different pending attempt for the same course milestone conflicts even without resource claims; exact retry is inert.
Source IDs, course IDs, time evidence, attempt/operation coordinates and fingerprints remain client-owned.
The remaining producers `heal`, `add_complication` and
`apply_deterioration` are pending implementation; their approved authored contracts remain supported
by the complete route model, but scalar publication does not pretend to execute them.

Ordered `remove_complication` is published through the same atomic treatment/effect batch.
The client selects the exact canonical complication ID and retires only its declared roots
and their validated first-create generation/reaction descendants. Closure starts even at an
already terminal root; terminal identities and their history remain. Unrelated roots and
descendants retain their identities and payloads. An effectless complication still requires
an authenticated zero-operation batch with exact retained-root lineage. Removal alone does
not change severity, its last-change event, or natural-recovery anchors; an explicitly ordered
severity reduction rematerializes only surviving root coordinates. The client alone rewrites
same-proposal `complicationRef` into canonical ownership/removal identity.
See `wound_mortal_roll_scope_skill_v1` in `Examples/E_CLI_Wound_Materialization.txt`:
extracting the ashglass shard removes its grip restriction and stabilization enables recovery,
while the base cut's exact-skill hindrance remains. This is not full healing.

Врач проводит три этапа: первую перевязку без немедленного улучшения, ослабление раны через
480 минут и стабилизацию через 960 минут. На каждом этапе расходуется одна текущая доза.
Завершение курса не означает исчезновение раны; пропущенный срок или подтверждённое отсутствие
нужного условия прерывает курс без расхода следующей дозы. Повтор уже принятого этапа не тратит
лекарство снова. Другое лечение само по себе не отменяет курс.

```json
{
  "diagnosisPaths": [],
  "routes": [{
    "routeId": "field_clinic_recovery_course",
    "displayName": "Последовательный курс лечения в полевой клинике",
    "visibility": "known_to_player",
    "mode": "course",
    "requirements": [{ "kind": "provider", "providerRef": "field_medic_01" }],
    "resourcePolicy": {
      "reserveBeforeResolution": true,
      "consumeOn": ["success"],
      "refundOn": ["cancelled", "validation_failed", "rolled_back"],
      "mutations": [
        { "kind": "consume_requirement", "scope": "course_milestone", "milestoneOrdinal": 1, "requirementIndex": 0 },
        { "kind": "consume_requirement", "scope": "course_milestone", "milestoneOrdinal": 2, "requirementIndex": 0 },
        { "kind": "consume_requirement", "scope": "course_milestone", "milestoneOrdinal": 3, "requirementIndex": 0 }
      ]
    },
    "resolution": { "clockKind": "world_time.currentTimeInMinutes", "maximumGapMinutes": 600 },
    "outcomes": [
      { "ordinal": 1, "afterMinutes": 0, "requirements": [{ "kind": "item_quantity", "itemRef": "antibiotic_dose", "quantity": 1, "ownerRole": "target" }], "category": "success", "completion": "active", "result": [] },
      { "ordinal": 2, "afterMinutes": 480, "requirements": [{ "kind": "item_quantity", "itemRef": "antibiotic_dose", "quantity": 1, "ownerRole": "target" }], "category": "success", "completion": "active", "result": [{ "kind": "reduce_severity", "steps": 1 }] },
      { "ordinal": 3, "afterMinutes": 960, "requirements": [{ "kind": "item_quantity", "itemRef": "antibiotic_dose", "quantity": 1, "ownerRole": "target" }], "category": "success", "completion": "completed", "result": [{ "kind": "stabilize" }] }
    ],
    "interruption": { "category": "failed_attempt", "result": [{ "kind": "no_improvement" }] }
  }],
  "knownRouteIds": ["field_clinic_recovery_course"],
  "completedRouteIds": []
}
```


`woundRef` is response-local only. Never author `woundId`, `effectId`,
`complicationId`, transition identity, history, owner IDs, fingerprints, receipts,
accepted transition progress, or canonical carrier post-state. The client allocates and binds all
client-owned authority after the complete proposal passes validation.

The proposal is closed. It contains exactly:

- `classification`: readable wound nature plus a legal location profile;
- `display`: name, description, visible symptoms, prognosis, visibility, and the
  exact acquisition narration used in the final scene;
- severity I-IV, never a severity V death surrogate;
- bounded complications and complete consequence-definition wrappers;
- setting-authored treatment/diagnosis routes and a closed recovery policy.

Physical treatment remains setting-specific: medicine, herbs, crystals, procedures,
facilities, or another evidence-backed route may be appropriate. Do not infer one
universal medicine list. Every Mortal wound must include at least one complete treatment
route with exact requirements, resource policy, resolution, and outcomes; an empty
`treatment.routes` array is invalid. A declared route does not itself supply its provider,
items, or skills: the client validates their current authority when treatment is attempted.
Spiritual wounds use the afterlife healing and natural-time
contracts described by their owning feature stages.

## wound_mortal_diagnosis_identity_v1

Within one Mortal wound, `diagnosisPathId` values must be exact and case/Unicode-confusable unique.
Choose a genuinely distinct ID for each diagnosis path; changing
only capitalization or using visually confusable characters is invalid. Selection
uses the exact ID, never fuzzy or display-name matching. This identifies possible
examinations; it does not assert that an examination succeeded or grant treatment.

## wound_optional_creation_v1

An ordinary wound is optional. For an ordinary eligible event the GM may decline:

```json
{
  "opportunityRef": "wound_opportunity_public_17",
  "decision": "none"
}
```

The GM may instead choose any severity from I through the opportunity's
`maximumSeverity`, including a lower severity than the maximum. Do not create a wound
without a current opportunity, reuse an opportunity for a second wound, turn harmless
or training fiction into an injury, or reopen a healed identity. A later trauma creates
a new opportunity; worsening requires the exact active wound authority supplied by the
client.

## wound_guaranteed_creation_v1

A pre-materialized source may carry a guaranteed result. In that case `none` is
illegal and the proposal must use the promised severity/result within the harder
realm or mode cap. The GM still authors what the wound is and how it is narrated, but
cannot weaken, strengthen, omit, or replace the sealed guarantee. A contradictory
source guarantee and event cap fails before the GM is asked to invent a compromise.

Death and soul dissipation are separate outcomes. They are never required merely
because a wound opportunity exists, and soul dissipation always remains a separate
optional winner decision under its own contract.

## wound_acquisition_narration_v1

`display.acquisitionNarration` must describe this exact accepted wound and must appear
verbatim in the final GM-authored scene. After acceptance, re-read the scene and make
the acquisition unmistakable to the player. The client then adds a deterministic,
escaped notification such as:

```text
Получена духовная рана: Трещина в узоре воли (II). Подробнее: /раны
```

If the proposal is valid but the final scene omits the exact acquisition sentence, the
repair target is the narrative output only. Do not rewrite unrelated state, change the
event, or invent new authority while repairing narration.

## wound_effect_separation_v1

A wound and its mechanics have separate lifecycles. The wound proposal embeds complete
#1535 `activeEffectDefinitions[]` source policies inside
`consequenceDefinitions[].definition`; every definition has an empty `links` array
because the client binds the new wound source. A wrapper may declare one direct root
and its readable severity slots. The GM does not add an ordinary `effectChanges[]`
application for that root: the client derives a fingerprinted typed root batch and the
shared effect engine remains the sole allocator of effect identity.

A wound-owned source is not publicly materializable. Later reaction descendants may
run only through their sealed definition graph. Effect removal never heals or deletes the wound.
Treatment or recovery is a separate accepted wound transition; independently
dispelled or expired effects do not alter wound bytes, severity, care, or history.

Conversely, healing retires the wound-owned active root/descendant group without
touching unrelated effects. Saref memory suppression and other independently sourced
effects are not wound consequences merely because the fiction mentions injury.

## wound_roll_scope_v1

A physical wound uses the shared `roll_modifier` from
`OtherGuides/Effect_Materialization_Contract.md`, not a wound-only roll mechanic.
Its payload contains exactly `operations`, `contribution`, and one closed `scope`.
The contribution is `advantage` or `disadvantage`; a harmful wound consequence uses
the latter within the severity envelope. The two legal forms are:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "all" }
}
```

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "skill", "skillId": "skill_lockpicking" }
}
```

`scope.kind=all` is exactly `{ "kind": "all" }`: it forbids `skillId` and remains
limited to the exact target/realm and declared registered operations. `scope.kind=skill`
is exactly `{ "kind": "skill", "skillId": "<canonical-id>" }`: it requires one
non-empty permanent ID and exactly `operations=["skill_check"]`. Missing scope is
invalid, never an implicit broad default. Extra fields, name/alias selectors, arrays,
multiple IDs, or focused `attack_roll`, `guard`, or mixed operations are invalid.
The common roll operation registry is exactly `attack_roll`, `defense_roll`,
`skill_check`, `saving_throw`, `damage_roll`, and `initiative_roll`; `guard` is not a
legal common roll operation even under broad scope.
The selector is semantic policy, not a scalar `parameterBounds` value.

Read `turn_request.json.effectSkillScopeCatalog` before writing the selector. It has
`schemaVersion: 1` and `targets[]`; each target row carries exact `realm`, `kind`,
`targetId`, and `skills[]` containing `skillId` and `displayName`.
The skillId must come from the exact target row. The detached bounded catalog is
advisory: editing it cannot grant a skill, owner, or binding. The client independently
checks the offered pre-turn canonical catalog and the final composed accepted roots;
the same exact usable active/passive skill must belong to the wound owner in both.
Consequently same-response new skills are not selectable, and same-response removal
or disablement rejects the new binding. Missing target catalogs, stale/wrong-owner
IDs, idless or inactive skills, terminal rows, duplicates, ambiguity, and Unicode-
confusable identities fail closed. There is no fuzzy or display-name matching.

The severity budget counts a whole component, not its operation count or scope fields:
one focused component consumes one consequence slot. A whole broad component likewise
consumes one slot even when it lists several allowed operations. To affect two exact
skills, author two components and two slots within the severity envelope. The complete
component belongs to one root's `slots[]` entry with `profileKey: "roll_modifier"`;
the selector never adds power or permits duplicate consequences at the same exact
`(operation, scope.kind, focused skillId)` coordinate. Keep the complete definition's
`links` empty, and never add an ordinary `effectChanges[]` application for a wound root.
This is only a mechanical selector: there is no catalog of ready-made wounds. The GM
still chooses the injury's setting-specific physical nature, name, symptoms, location,
prognosis, treatment model, and acquisition scene.

After successful materialization, legitimate skill loss only derives dormancy:
later missing skill makes the component inactive without healing/removing the wound.
Skill unavailability alone does not change wound/effect identity, severity, treatment,
duration, or history. A same-name or similar-name replacement with a different ID stays
unaffected; only restoration of the same permanent ID as a usable, unambiguous current
skill can reactivate a still-active component. Normal expiry, healing, and treatment
remain separate. Authorized treatment rematerialization preserves the unchanged accepted
selector even while it is dormant; it does not pick a replacement skill. A focused
component applies only when the trusted roll's exact actor, realm, `skill_check`, and
skill ID match, with the common non-escalating advantage/disadvantage reduction.

An invalid new selector publishes no wound, effect, history, or output. For a safely
repairable selector failure, use the current `wound_materialization_repair` packet and
its exact original path, such as
`woundDecisions[0].proposal.consequenceDefinitions[0].definition.components[0].payload.scope.skillId`.
Start from `preservedProposal`, correct only the listed paths, and resubmit the complete
semantic turn with the acquisition narration preserved verbatim. Never broaden the
scope implicitly, guess an ID, rewrite canonical state, or change the bound opportunity,
owner, source, or event. If the packet has no lawful exact correction, obtain fresh
authority. Changing the selector is not an exact replay; see the repair rules below.

Player-facing text uses Russian skill names or a readable unavailable-skill explanation,
never `skillId`, catalog rows, repair paths, or hidden-effect details. The two complete
Mortal examples are `wound_mortal_roll_scope_all_v1` and
`wound_mortal_roll_scope_skill_v1` in `Examples/E_CLI_Wound_Materialization.txt`.
For afterlife, spiritual arts are not Mortal skill IDs: the common example's
`operations=["defense_roll"]` uses `scope.kind=all` and contributes only to a real typed
defense roll. Spiritual guard is not a roll_modifier operation or a `defense_roll` alias;
it uses the dedicated spiritual condition/wound-profile contracts. The eight dedicated
spiritual-wound profiles keep their own payload contract rather than borrowing the
Mortal `effectSkillScopeCatalog`.

## wound_mortal_deterioration_policy_v1

A Mortal physical wound may declare one non-null deterioration policy when an unmet
care condition can make the wound worse over canonical Mortal-world time. Use `null`
when the wound has no such rule. This is setting-authored policy, not a universal list
of infections, medicines, complications, or deadlines.

The non-null object is closed and contains exactly:

- `policyRef`: one exact GM-authored policy identifier;
- `unmetConditions`: an array containing exactly one exact condition identifier;
- `graceMinutes`: a non-negative signed 64-bit number of canonical minutes before the
  first deterioration can become due;
- `cadenceMinutes`: a positive signed 64-bit number of canonical minutes between later
  deterioration opportunities;
- `result`: exactly one closed typed result.

The adverse result is exactly one of:

- `{ "kind": "increase_severity" }`, which raises severity by one tier only while the
  active wound is below IV;
- `{ "kind": "add_complication", "complicationDraft": ... }`, where the draft contains
  exactly one complete setting-specific complication with treatment difficulty 1-4
  and an optional complete wound-consequence graph;
- `{ "kind": "death_contour" }`, which requests the separate Mortal death lifecycle
  handoff and never writes death state directly.

At severity IV, `increase_severity` is inapplicable and is never silently converted to
death. If death is the authored consequence, select `death_contour` explicitly.
`no_change` and `add_recovery` are recognized only so the client can report that they
are not strictly worsening; they grant no deterioration authority. An unknown or open
result is malformed rather than a fallback.

For `add_complication`, the client also checks the current wound before granting
authority: at most 16 complications and four total consequence slots. It separately
allows five total root bindings and five total owned effect definitions. The ordinary
accepted wound reducer revalidates the complete allocated after-image before publication.
The policy itself never grants
the GM permission to edit a canonical wound, effect, history row, clock, anchor,
fingerprint, receipt, or lifecycle state.

Worked GM example — untreated contamination may add one effectless, independently
treatable infection complication after its grace period:

```json
{
  "policyRef": "untreated_contamination",
  "unmetConditions": ["not_stabilized"],
  "graceMinutes": 720,
  "cadenceMinutes": 1440,
  "result": {
    "kind": "add_complication",
    "complicationDraft": {
      "complications": [
        {
          "complicationRef": "spreading_infection",
          "kind": "infection",
          "state": "active",
          "displayName": "Распространяющееся заражение",
          "treatmentDifficultyModifier": 2,
          "visibility": "known_to_player"
        }
      ],
      "consequenceDefinitions": []
    }
  }
}
```

## wound_spiritual_profiles_v1

Spiritual wound mechanics use exactly these eight registered deterministic profiles:

| Profile | Fixed axis | Legal severity envelope |
| --- | --- | --- |
| `spiritual_roll_hindrance` | `rollMode` | I-IV, one declared operation |
| `spiritual_action_cost_burden` | `actionCostAudit` | +1 at I-II, +2 at III, +3 at IV |
| `spiritual_position_burden` | `conflictPosition` | one adverse step at I-II, at most two at III-IV |
| `spiritual_control_burden` | `controlState` | unavailable at I; one step at II-IV |
| `spiritual_strain_burden` | `sideStrain` | unavailable at I-II; one step at III-IV |
| `spiritual_tempo_burden` | `tempoAdvantage` | deny one gain at I-IV |
| `spiritual_counter_burden` | `counterPayoff` | reduce one payoff step at I-IV |
| `spiritual_art_restriction` | `artAvailability` | unavailable at I-II; restrict at III; forbid at IV |

Each component has the exact closed payload `{ operation, axis, magnitude }` and only
the registered `profile_specific` merge reducer. The wound severity owns the number of
slots and legal magnitude. These profiles target persistent afterlife actors and MUST NOT
be encoded as `afterlife_combat_condition`, target a spiritual conflict side, or
duplicate `combatConditions[]`. The client projects an accepted persistent actor effect
onto its current conflict side only while that exact actor is an unambiguous participant.

Inspection, communication, help, treatment, withdrawal, surrender, negotiation, and
the separate dissipation choice remain safe. A wound profile cannot block them.

## wound_treatment_scene_authority_v1

Mortal wound treatment may use three exact closed version-1 setting rows inside the
exact current location's `customStates[]`:

- `mortal_wound_treatment_facility` contains exactly `kind`, `schemaVersion=1`,
  an exact `facilityId`, non-empty `displayName`, and boolean `available`;
- `mortal_wound_treatment_environment` contains exactly `kind`, `schemaVersion=1`,
  an exact `environmentId`, non-empty `displayName`, and exact setting-specific
  `state`;
- `mortal_wound_treatment_consent` contains exactly `kind`, `schemaVersion=1`,
  exact `consentRef`, non-empty `displayName`, typed `providerKind/providerId`, typed
  `targetKind/targetId`, and `status` equal to `granted` or `withdrawn`.

Actor kinds are exactly `player`, `npc`, `combatant`, or `combatant_member`. The client
rebinds realm, current location, actor identity, co-presence, lifecycle, and
reachability from canonical state; do not author those fields in a scene row. IDs are
exact and case/Unicode-confusable unique within their kind. Environment uniqueness is
by `environmentId`, so two distinct environment IDs may share one exact `state`.
There is no universal catalog of facilities, environments, medicines, or consent
records: their exact IDs and environment states are setting-specific.

An unavailable facility (`available=false`) and withdrawn consent
(`status=withdrawn`) are valid retained negative evidence. Unrecognized custom states
remain ordinary setting data and grant no treatment authority. A malformed recognized
row rejects; it is not silently ignored. Reserved treatment rows are location-only:
placing one in link `customStates[]` rejects the link.

### Existing and new location authoring

For a known location, author these rows only through
`worldMapUpdates.locationUpdates[]`. Send the complete replacement `customStates[]`
and preserve every unrelated sibling that must survive. Do not resend a known
location through `currentLocationData` merely to change treatment rows.

A same-turn new selected location carries its complete array in its ordinary complete
`currentLocationData` creation envelope. A same-turn new remote location carries its
complete array in its ordinary complete `worldMapUpdates.newLocations[]` envelope.
The creation envelope still needs every field and materialization section required by
Mortal Location Materialization v1. See
`Examples/E_CLI_Wound_Materialization.txt` for all three routes and the link rejection.

## mortal_wound_treatment_reduce_severity_v1

This is one complete procedure route authored for an untreated rank-III Mortal
physical wound. Its retained consequence graph has exactly two slots and is already
legal at rank II. The GM authors the wound graph and route result; the GM does not
author permanent wound/effect identity or publication authority.

```json
{
  "diagnosisPaths": [],
  "routes": [
    {
      "routeId": "clean_and_close_rank_iii",
      "displayName": "Очистить, стабилизировать и закрыть глубокую рану",
      "visibility": "known_to_player",
      "mode": "procedure",
      "requirements": [
        {
          "kind": "item_quantity",
          "itemRef": "sterile_dressing",
          "quantity": 1,
          "ownerRole": "provider"
        },
        {
          "kind": "skill_tier",
          "capabilityRef": "field_medicine",
          "minimumTier": 2,
          "actorRole": "provider"
        }
      ],
      "resourcePolicy": {
        "reserveBeforeResolution": true,
        "consumeOn": ["success", "partial_success", "failed_attempt"],
        "refundOn": ["cancelled", "validation_failed", "rolled_back"],
        "mutations": [
          {
            "kind": "consume_requirement",
            "scope": "common",
            "milestoneOrdinal": null,
            "requirementIndex": 0
          }
        ]
      },
      "resolution": {
        "formulaKey": "mortal_wound_procedure_v1",
        "difficulty": 15,
        "rollSource": "accepted_d20",
        "criticalPolicy": "natural_20_first_natural_1_last",
        "modifierSource": {
          "kind": "resolved_skill_tier",
          "requirementIndex": 1
        }
      },
      "outcomes": [
        {
          "bandId": "clean_close_success",
          "minimumMargin": 5,
          "maximumMargin": null,
          "category": "success",
          "result": [
            { "kind": "stabilize" },
            { "kind": "reduce_severity", "steps": 1 }
          ]
        },
        {
          "bandId": "clean_close_partial",
          "minimumMargin": 0,
          "maximumMargin": 4,
          "category": "partial_success",
          "result": [
            { "kind": "stabilize" },
            { "kind": "add_recovery", "points": 2 }
          ]
        },
        {
          "bandId": "clean_close_failed",
          "minimumMargin": null,
          "maximumMargin": -1,
          "category": "failed_attempt",
          "result": [
            {
              "kind": "add_complication",
              "complicationDraft": {
                "complications": [
                  {
                    "complicationRef": "strained_wound_edge_local",
                    "kind": "impairment",
                    "state": "active",
                    "displayName": "Натяжение края раны",
                    "treatmentDifficultyModifier": 1,
                    "visibility": "known_to_player"
                  }
                ],
                "consequenceDefinitions": [
                  {
                    "definitionRef": "strained_edge_item_control_local",
                    "definition": {
                      "schemaVersion": 1,
                      "definitionKey": "strained-wound-edge-item-control",
                      "display": {
                        "name": "Движения натягивают край раны",
                        "description": "После неудачного закрытия раны использование предметов вызывает болезненное натяжение.",
                        "category": "debuff",
                        "visibility": "visible"
                      },
                      "allowedRealms": [
                        "mortal_world"
                      ],
                      "allowedTargetKinds": [
                        "player"
                      ],
                      "components": [
                        {
                          "componentId": "strained_edge_item_control",
                          "profile": "action_control",
                          "priority": 100,
                          "payload": {
                            "action": "use_item",
                            "operation": "restrict"
                          }
                        }
                      ],
                      "parameterBounds": {},
                      "stacking": {
                        "stackKey": "wound_strained_edge_item_control",
                        "policy": "independent",
                        "maxStacks": 1,
                        "atMaximum": "no_change",
                        "refreshMode": null,
                        "mergeRule": null
                      },
                      "lifetime": {
                        "mode": "source_bound",
                        "activePredicate": "active",
                        "onSourceLoss": "expire"
                      },
                      "triggers": [],
                      "removal": {
                        "dispelCategories": [
                          "physical_restoration"
                        ],
                        "cureKinds": [
                          "wound_treatment"
                        ],
                        "onSourceLoss": "expire",
                        "onConditionLoss": null,
                        "manualAuthorities": []
                      },
                      "links": []
                    },
                    "root": {
                      "ownership": {
                        "kind": "complication",
                        "complicationRef": "strained_wound_edge_local"
                      },
                      "slots": [
                        {
                          "profileKey": "action_control",
                          "readableSummary": "Натяжение края раны ограничивает использование предметов."
                        }
                      ]
                    }
                  }
                ]
              }
            }
          ]
        }
      ],
      "interruption": null
    }
  ],
  "knownRouteIds": ["clean_and_close_rank_iii"],
  "completedRouteIds": []
}
```

The client validates the unchanged retained graph against the destination-rank slot
and power envelope before consuming the accepted roll. If the same graph exceeds the
rank-II destination-rank slot and power envelope, the client rejects the route; it
never prunes or weakens mechanics to make the result fit.

After the ordered successful result applies `stabilize` and then
`reduce_severity { steps: 1 }`, the client retires the current wound-owned effect
group and rematerializes client-owned fresh effect IDs for every retained root.
Only a newly successful category completes the route. `partial_success` and
`failed_attempt` do not complete the route. The partial band retains its declared
scalar publication contract; the failed complication band is preview-only here.

The client validates the complete cumulative graph after every authored operation, including unselected procedure bands, before claiming a die or resources.
The same canonical rules revalidate retained treatment routes and deterioration policy at the resulting severity, and diagnosis facts against remaining accepted complications. A symbolic local reference never restores a removed accepted diagnosis fact.
Local complication and definition references are not permanent runtime identities.
Applicability preview does not authorize publication of an unfinished outcome.

The failed band declares a complete effectful complication; its selected-add publication
remains unfinished. This pre-roll preview admits the fully validated graph but does not
allocate wound/effect identities or authorize that unfinished selected publisher.

The partial result stabilizes and adds two recovery points with checked signed-64-bit arithmetic. With progress 1 and threshold 2, the accepted result has progress 3 and the wound stays active at its unchanged severity. Reaching the threshold does not itself trigger a recovery tick, severity reduction or healing. The GM authors points, never canonical progress or recovery anchors; the client applies the selected ordered operations and exact resource policy atomically. Partial success does not complete the route.

## Validation, replay, and repair

Unknown fields, wrong types, duplicate or stale decisions, over-maximum severity,
missing guaranteed results, illegal profile power, GM-authored client identity, and
detached narration fail before wound/effect/history/output publication. The same
accepted decision replays idempotently; a changed decision, scene, opportunity, event,
snapshot, owner, realm, source, or guarantee requires fresh authority.

### wound_repair_retry_v1

An invalid proposal does not partially enter the game. For example, if the sealed
opportunity has `maximumSeverity: "II"` and the GM proposes severity III, validation
reports `wound_severity_above_opportunity`. Before asking for a correction, the client
restores the exact before-image of every rollback-tracked canonical and player-output
file touched by the rejected turn. No wound, wound-owned effect, payment, history
transition, notification, or narrative output from that attempt survives.

When the errors are safely repairable, the client emits one bounded current repair wave
of 1-64 `wound_materialization_repair` packets: one packet per rejected candidate, with
one or more issues in each packet. Its opaque binding envelope contains the exact
`sessionId`, `requestId`, `snapshotToken`, `candidateRef`, and `semanticFingerprint`.
Its sanitized semantic payload contains only the offending paths, legal bounds, safe
event/target/realm context, `preservedProposal`, and `requiredResponseShape`. It never
exposes hidden owner bindings, permanent IDs, source seals, canonical before-images,
private paths, or unrelated response content.

Use every packet only for its bound session, request, snapshot, candidate, and rejected
proposal. Correct every packet and every listed path in the current repair wave, then
resubmit them together in one complete corrected semantic turn through the owning
repair protocol. Reproduce the complete original response and preserve every unrelated
semantic decision. For each construction/narration packet, use `preservedProposal` as the base, add or replace
only the paths listed by
`requiredResponseShape.woundDecisions[0].proposal.correctOnly`, and include
`display.acquisitionNarration` verbatim in the complete final scene. A patch fragment,
an omitted sibling packet, or a direct canonical file edit is not a valid retry.

### wound_stale_repair_packet_v1

A repair wave is single-authority, not reusable authority. If any packet's session,
request, snapshot, opportunity, accepted event, target, roll, generation, rejected
proposal, or command root no longer matches, stop. Do not adapt the old wave, guess IDs,
or repeat the roll. Request fresh authority and answer the new current opportunity.

Only one exact complete corrected retry may consume the bound wave. A second attempt
with changed semantic content is a conflict, even when the prose seems equivalent.
The client either accepts every packet atomically or keeps the restored exact
before-image unchanged.

### wound_rollback_replay_v1

The following is an internal history resolver guarantee, not a currently dispatched
GM/player receipt. When an owning accepted treatment/recovery stage invokes the
resolver after acceptance, an exact transport/crash replay resolves to the same
already-accepted receipt from persisted history. The resolver itself performs no
second charge, no second history transition, no new wound/effect publication, no
course or cycle advance, no notification, and no output rewrite.

The operation key selects the persisted transition. An unknown operation key is no replay match
and requires fresh authority. For a matched operation key, the event,
attempt, treatment course and milestone, recovery cycle, payment fingerprint, output
fingerprint, and readable result must also match exactly; changing any of those
coordinates is a conflicting replay and never creates a second transition under the old
operation key.

## Worked examples

- Complete rank-II broad and exact-skill physical wounds, including the advisory catalog,
  scene, source definition, and one-component/one-slot accounting, are in
  `Examples/E_CLI_Wound_Materialization.txt` under `wound_mortal_roll_scope_all_v1`
  and `wound_mortal_roll_scope_skill_v1`.
- The exact eight spiritual component fragments and one complete GM-authored spiritual
  wound source graph are in `Examples/E_CLI_Effect_Materialization.txt` under
  `wound_spiritual_profiles_v1` and `wound_spiritual_source_worked_v1`.
- Optional, lower-severity, guaranteed, and rejected constructor responses are in
  `Examples/E_Block_5.txt`.
- The complete invalid-severity, bounded retry, stale-packet, rollback, and exact
  replay walkthrough is in `Examples/E_Block_12.txt` under
  `wound_repair_retry_v1`.
- The common effect source, lifetime, reaction, and repair rules remain normative in
  `OtherGuides/Effect_Materialization_Contract.md`.


## wound_mortal_alternative_response_v1

This checkpoint defines the strict local parser for a setting-authored alternative to an
existing Mortal wound's treatment graph. It does not enable live distribution yet.
Populate `woundTreatmentAuthorings[]` only when a future owning workflow has exposed
an offered client-bound request; do not invent a request or send this field during
ordinary play while live distribution is unavailable.

Every entry is one closed four-field object: `authoringRequestRef`, `decision`,
`route`, and `diagnosisPath`. All four fields are required. An `author` response
contains one complete setting-specific route. A `public` or `known_to_player` route
has an explicit-null `diagnosisPath`; a `hidden` route has one complete non-GM-only
path that reveals that route's exact `routeId`. A `decline` response uses explicit
null for both `route` and `diagnosisPath`.

The opaque `authoringRequestRef` must be copied exactly from the offered request.
Never add GM authority fingerprints, result fingerprints, client transition IDs,
permanent wound/effect IDs, owner coordinates, or publication claims. Inside a GM
draft, `remove_complication` selects an offered opaque `complicationRef`.
`complicationId` is reserved for the later client-bound canonical route and is
rejected here. Diagnosis facts retain the existing `route:<routeId>` and
`complication:<complicationId>` grammar; this local parsing checkpoint neither
invents a new fact selector nor discloses hidden canonical complication identities.
Each route below is a setting-specific example, never a universal treatment catalog.

### wound_mortal_alternative_response_visible_author_v1

A visible alternative may directly remove the complication represented by the opaque
selector offered with the client-bound request. Its path is explicitly null.

```json
{
  "response": "Знахарка предлагает извлечь осколок пепельного стекла.",
  "woundTreatmentAuthorings": [
    {
      "authoringRequestRef": "alternative_offer_ashglass_17",
      "decision": "author",
      "route": {
        "routeId": "extract_ashglass_shard",
        "displayName": "Извлечь осколок пепельного стекла",
        "visibility": "known_to_player",
        "mode": "guaranteed",
        "requirements": [
          {
            "kind": "source_capability",
            "capabilityRef": "ashglass_extraction",
            "actorRole": "provider"
          }
        ],
        "resourcePolicy": {
          "reserveBeforeResolution": true,
          "consumeOn": ["success"],
          "refundOn": ["cancelled", "validation_failed", "rolled_back"],
          "mutations": []
        },
        "resolution": {
          "capabilityRef": "ashglass_extraction",
          "actorRole": "provider"
        },
        "outcomes": [
          {
            "category": "success",
            "result": [
              {
                "kind": "remove_complication",
                "complicationRef": "offered_ashglass_fragment"
              }
            ]
          }
        ],
        "interruption": null
      },
      "diagnosisPath": null
    }
  ]
}
```

### wound_mortal_alternative_response_hidden_author_v1

A hidden alternative includes one complete path. The existing diagnosis-fact grammar is
unchanged, and the path reveals the exact new route.

```json
{
  "response": "Колокольный лекарь замечает скрытый способ унять внутренний звон.",
  "woundTreatmentAuthorings": [
    {
      "authoringRequestRef": "alternative_offer_bell_echo_18",
      "decision": "author",
      "route": {
        "routeId": "ritual_draw_bell_echo",
        "displayName": "Вывести колокольное эхо",
        "visibility": "hidden",
        "mode": "guaranteed",
        "requirements": [
          {
            "kind": "source_capability",
            "capabilityRef": "bell_echo_rite",
            "actorRole": "provider"
          }
        ],
        "resourcePolicy": {
          "reserveBeforeResolution": true,
          "consumeOn": ["success"],
          "refundOn": ["cancelled", "validation_failed", "rolled_back"],
          "mutations": []
        },
        "resolution": {
          "capabilityRef": "bell_echo_rite",
          "actorRole": "provider"
        },
        "outcomes": [
          {
            "category": "success",
            "result": [{ "kind": "stabilize" }]
          }
        ],
        "interruption": null
      },
      "diagnosisPath": {
        "diagnosisPathId": "trace_bell_echo",
        "displayName": "Проследить внутренний звон",
        "visibility": "hidden",
        "requiresKnownFacts": ["route:steady_breathing"],
        "requirements": [],
        "check": {},
        "reveals": ["route:ritual_draw_bell_echo"],
        "failurePolicy": "no_reveal"
      }
    }
  ]
}
```

### wound_mortal_alternative_response_decline_v1

Declining still resolves an offered request eventually, so both payload members are
present as explicit nulls.

```json
{
  "response": "Иного лечения в этих условиях нет.",
  "woundTreatmentAuthorings": [
    {
      "authoringRequestRef": "alternative_offer_decline_19",
      "decision": "decline",
      "route": null,
      "diagnosisPath": null
    }
  ]
}
```
