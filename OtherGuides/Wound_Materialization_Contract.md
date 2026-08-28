# Wound Materialization v1

This is the GM-facing contract for constructing a physical or spiritual wound after
the client has exposed one exact wound opportunity. It applies in the Mortal World,
Chaos Sea, and Shining Abode. A wound is an independently treatable entity; it is not
a status line, an active effect, a combat condition, death, or soul dissipation.

There is no catalog of ready-made wounds. The GM authors the name, nature, symptoms,
prognosis, setting-appropriate treatment model, and narration. The client validates a
closed schema, the event-owned severity limit, registered mechanical primitives, and
all permanent identity and publication work. This is a direct schema-v1 technical
cutover with no migration of non-empty legacy saves.

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
universal medicine list. Spiritual wounds use the afterlife healing and natural-time
contracts described by their owning feature stages.

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

## Validation, replay, and repair

Unknown fields, wrong types, duplicate or stale decisions, over-maximum severity,
missing guaranteed results, illegal profile power, GM-authored client identity, and
detached narration fail before wound/effect/history/output publication. The same
accepted decision replays idempotently; a changed decision, scene, opportunity, event,
snapshot, owner, realm, source, or guarantee requires fresh authority.

Repair packets expose only the offending GM-authored path, legal bound, and safe
context. They never expose hidden owner bindings, permanent IDs, source seals,
canonical before-images, or internal paths. Resubmit the complete corrected semantic
turn through the owning repair protocol.

## Worked examples

- The exact eight spiritual component fragments and one complete GM-authored spiritual
  wound source graph are in `Examples/E_CLI_Effect_Materialization.txt` under
  `wound_spiritual_profiles_v1` and `wound_spiritual_source_worked_v1`.
- Optional, lower-severity, guaranteed, and rejected constructor responses are in
  `Examples/E_Block_5.txt`.
- The common effect source, lifetime, reaction, and repair rules remain normative in
  `OtherGuides/Effect_Materialization_Contract.md`.
