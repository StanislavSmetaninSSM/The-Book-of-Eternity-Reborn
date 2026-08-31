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
semantic decision. For each packet, use `preservedProposal` as the base, add or replace
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
