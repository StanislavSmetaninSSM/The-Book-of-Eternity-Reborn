# Wound Materialization v1

## Kind-specific safe wound repair transport

Wound repair transport: every wound_materialization_repair packet requires candidateKind, exactly construct_wound, repair_wound, author_alternative_treatment, or narrate_acquisition. Missing or mismatched tags fail; there is no tagless reader. Construction/narration uses requiredResponseShape.woundDecisions[0].proposal.correctOnly and repeats display.acquisitionNarration verbatim. Alternative authoring uses requiredResponseShape.woundTreatmentAuthorings[0].route.correctOnly and requiredResponseShape.woundTreatmentAuthorings[0].diagnosisPath.correctOnly, with immutable authoringRequestRef and decision=author. Preserve every non-offending sibling and ordered array position; replace invalid required values, omit unknown fields, and append a missing route reveal only when explicitly requested, preserving its entire old prefix. A non-offending null diagnosisPath remains explicit null. Recipe base/correctOnly objects are instructions, not submitted route members. NeedsAnotherRepair is local staged progress only: refresh the privately bound rejected draft and its fingerprint before another bounded repair; never reuse the old packet for a changed draft. Public transport cannot reconstruct private alternative repair authority: there is no live alternative repair adapter. Do not fabricate an accepted command, receipt, operation key, wound identity, or canonical/evidence seal from this example. Alternative treatment adds no acquisitionNarration obligation; it does not describe receiving a new wound. A valid decline has two explicit null payloads and no repair work; correction cannot switch author to decline.

Same-scene Mortal joint severity repair: answer only the current client-issued construct_wound packet for the same turn. Keep the complete scene, opportunityRef, materialize decision, acquisitionNarration, every non-offending proposal field, sibling and array position unchanged; correct only the permitted severity leaf. After client publication consumes the public wound draft, keep the exact accepted narrative_response.json including timestamp and formatting; do not recreate the consumed draft. Changed output is held for recovery, not silently normalized or reaccepted. A III-to-II correction is an example, not a new severity rule: the current sealed opportunity and packet determine the allowed range. Preserve valid unrelated submitted world-time and progression report content; after the client consumes a matching report, do not recreate it on a retry. The client owns the retained confirmed treatment, private retry state, mechanical publication, progression acknowledgement, story and cleanup; the GM supplies no replacement command, claim, receipt, seal or lifecycle acknowledgement. An active training, level-up or new-QTE task cannot be cleared or skipped to force this bounded repair contour; retain the task and answer the actual repair or refusal rather than adding unrelated mechanical work. For a newly selected joint wave the client freezes interface_updates.json once before mechanical publication; preserve that client-published interface image on continuation, including timestamp and formatting. The client may finish an already computed interface publication after interruption without asking the GM to repeat it. If the client reports a frozen interface error, the same operation remains held: do not rewrite the response, delete private pending files or repeatedly resubmit it to force recomputation; follow only a new explicit client-owned recovery or whole-operation cancellation. The GM authors no interface-result record or protocol version, and a retained older wave stays on its existing client-selected route.

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
Selected non-death treatment `apply_deterioration` now uses the atomic graph publisher.
The direct treatment `heal`, explicit death and legacy producers remain pending implementation; their approved authored
contracts remain in the complete route model. Scheduled natural recovery uses a separate client-owned continuation.
Mortal recovery admission example (`mortal_wound_recovery_history_guard_v1`): the
client has accepted a wound and sealed the current pending snapshot. If its current
history is missing or malformed, the planner returns `InvalidHistory` with the
original `wound_history_*` parser issue before recovery arithmetic. A valid but
changed history returns `mortal_wound_recovery_history_mismatch`; an unreadable or
unsafe history file returns `mortal_wound_recovery_history_read_failed`. The same
check applies after reopening the session. This is invalid authority, not a
missed dose, an elapsed recovery tick or permission to improvise healing. Equivalent
JSON formatting remains valid; arithmetic uses the accepted snapshot's sealed world minute,
not a newly edited live clock. The client must restore accepted history or prepare
and export a fresh accepted snapshot after a legitimate publication. Never hand-write history, a tick, an anchor or a receipt.
This admission check does not publish recovery, healing or a receipt; the admitted
resolution is composed into the existing common plan, whose coordinated publisher
alone writes recovery state, effects, identity, history and the closed receipt.

Mortal recovery publication example (`mortal_wound_recovery_publication_v1`):
an already accepted stabilized rank-II physical wound has anchor100, cadence10,
progress0, threshold2 and carryOverflow=true. At minute135 the client counts three
new intervals: the first threshold lowers severity to I, leaving progress1.
Every severity change actually terminates the previous effects and rematerializes
the lawful consequences with fresh generations. The original anchor stays100,
consumed intervals become3 and the next due minute stays140. At136 there is no
new interval; at140 one new point reaches the rank-I threshold and a separate
full-heal stage terminates its effects and removes its active carrier. The full
terminal wound snapshot remains in verified history. All stages belong to one
common publication; no intermediate state is written. A graph that cannot fit
the resulting slot budget is rejected without silently dropping consequences.
After reopening at the same signed minute, the client returns the original
four-field receipt without a new transition or write, including after full heal.
This replay requires the same accepted final wound state. A later genuine treatment
transition can retain the old tick key while starting a new stabilization epoch:
for example, a blocked check at110 followed by accepted stabilization at110 permits
a fresh NotDue check with next due120. Complete history must prove that intervening
transition and its exact current wound; the new check then has its own replay receipt.
Missing, changed or reordered linked history is InvalidHistory before live-clock
arithmetic. Multiple elapsed deterioration intervals trigger one declared adverse
result and consume all elapsed intervals; increase_severity adds one rank and
resets progress to0. A death_contour is retained as a typed lifecycle handoff;
the separate death consumer remains unfinished. GM authors the complete recovery
policy in the ordinary wound proposal, never tick keys, epochs, receipt hashes
or recovery transitions. No new GM command or response surface is introduced.
Selected direct and policy additions use the graph publication contract below.

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
An exact current skill with canonical `active=false` cannot authorize treatment.
Catalog membership supplies its lifecycle, but does not overwrite that activity flag;
similarly named `isActive` and caller-authored lifecycle fields grant no authority.
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

## spiritual_wound_source_envelope_v1

In Chaos Sea and Shining Abode, an optional source declaration lives only at
`profiles[].specialArts[].spiritualWoundEnvelope` in
`game_state/meta/afterlife_entity_profiles.json`, including the player_soul profile;
never in soul_state.afterlifeCombatProfile.artTiers. The owning special art must
already be materialized before the harmful event. Keep its ordinary owner,
`baseOperation`, tier, cost, and `combatEffect` contract.

The declaration is a closed object containing exactly `schemaVersion` equal to 1,
`maximumSeverityRank` as an integer 0..4, and `guaranteedSeverityRank` as null or
an integer 1..maximumSeverityRank. There are no extra, duplicate, confusable, or
display-name field aliases. A present null or malformed declaration is invalid.

An absent declaration means neutral IV as the source cap and no guarantee. It
preserves ordinary harmful-strain eligibility, including for a special art:
absence is not a prohibition and does not make injury mandatory. Rank 0 explicitly
forbids a wound from that declared source. A lower cap restricts the source; it
does not raise the formula/resilience, destination strain, or danger limits.

A guaranteed severity still requires a proven trigger and all harder limits.
A contradiction fails validation; do not silently raise the maximum, lower the
guarantee, or convert it to an optional wound. Art cost, zero cost, passive prose,
`specialArtAudit`, and a current wound decision cannot establish prior source
authority. Never add or strengthen the declaration after seeing the roll.

The client checks the current profile shape and independently acquires the
original source under the signed turn snapshot. This source-only preparation is
not an admitted wound opportunity or proof of successful resource/effect reduction.
Do not manufacture wound decisions, receipts, extra dice, or a publication from
this declaration alone. The GM still authors the wound's nature and consequences
only when the client supplies a valid opportunity. Mortal wounds keep their
setting-authored source/treatment contract; this field is afterlife-only.

Worked source: `spiritual_wound_source_envelope_v1` in
`Examples/E_CLI_Afterlife_Turns.txt`. The source-art fragment proves authoring and
closed parsing, not a full actor update, accepted battle, or wound publication.

## spiritual_wound_source_action_v1

In Chaos Sea and Shining Abode, a harmful spiritual action may optionally name its
intended wound target. Put `spiritualWoundTarget` only on the player exchange object
or on the opposition `incomingAction` object:

```json
{
  "spiritualWoundTarget": {
    "actorType": "guardian",
    "actorId": "guardian_frame",
    "retraumaWoundRef": "wound_torn_resonance"
  }
}
```

`spiritualWoundTarget` is a closed object. It contains exactly `actorType`, `actorId`,
and optional `retraumaWoundRef`; all present values are raw non-empty exact strings with
no surrounding whitespace. There are no aliases, case variants, confusable spellings,
or second wound-reference field. Do not put the target in `specialArtAudit`,
`specialArtAudits[]`, a resolution summary, or a wound decision.

The acting player targets the opposition side; an opposition `incomingAction` targets
the player side. An explicit actor must be an original member of the affected side.
When the object is absent, the client selects the original affected-side lead; the GM
does not restate or recompute that default. `retraumaWoundRef` may identify only one
canonical prior active spiritual wound whose original carrier owner, realm, semantic
identity, and immutable history all agree. Source preparation never makes a wound
removed or changed in the current candidate valid; later admission must still prove
current candidate agreement.

For two sources in the same original turn, when the client has already created
this side's one wound and the later source's maximum exceeds its current rank,
the later source without an explicit older-wound re-trauma target uses
the current owned conflict wound as its worsening target. The GM does not put
that new wound's future or allocated ID into the exchange or response. The
client's next offer raises the minimum severity above the current rank and
rejects a second rank-I creation. An explicit older-wound target continues to
use its separate `retraumaWoundRef` proof. In a later turn, the client also
verifies the signed conflict receipt and current wound history before reusing
that same wound. An optional source at or below its current rank offers only
`none`. An already satisfied guarantee advances through the separate
client-owned `guarantee_satisfied` path described below.

When one closed exchange harms both sides and neither side has a newly created
wound in that conflict yet, the client offers each side's source in causal
order. The GM may materialize one wound for each affected side with target-
specific complete proposals and exact acquisition narration for both. The
shared exchange coordinate does not merge those wounds: each side has its own
new wound ID, create transition and receipt row. If a side already owns its
conflict wound, that side follows its same-conflict worsen, `none`, or
`guarantee_satisfied` route independently of the other side.

The GM never authors `traumaPressure`, `sourceSeverityCap`, `maximumSeverityRank`,
`guaranteedSeverityRank`, or `spiritualWoundEnvelope` on an exchange, incoming action,
or audit. The client derives the harmful margin, destination strain, danger cap, source
cap, guarantee, tier, dice, and source binding from signed originals. Do not invent
dice, operation evidence, damage, or a successful outcome for a passive source,
champion coordination, or a dice-free voluntary action.

A newly terminal harmful result carries `resolution.terminalExchange`. This is the
complete existing exchange witness, including its full `before`, `after`, matchup,
action-cost, and matching complete `diceAudit` when contested. It is not a totals
summary. The resolution-level `diceAudit` must equal the terminal exchange audit; the
exchange coordinate and dice cannot be reused from a retained/current exchange or a
second terminal result.

A source-local preparation is not an admitted wound, opportunity, decision, receipt,
successful resource/effect publication, or accepted turn. Start, escalation, missing
same-turn prefix, terminal closure, passive source, champion coordination, and
dice-free voluntary contours remain pending until the client-owned C/D/E stages prove
their actual authority. Never remove one of those lawful contours merely to avoid the
pending requirement. Soul dissipation remains a separate optional dissipation choice
in both real realms; it is never implied by this source field or terminal witness.

Worked fragments: `spiritual_wound_source_action_v1` in
`Examples/E_CLI_Afterlife_Turns.txt`. The first fragment is executable shape proof for
both legal action locations. The second is a complete contested terminal witness that
the signed production preparation retains as `TerminalClosure`; neither fragment is a
full profile update, wound admission, receipt, or accepted turn.

## spiritual_wound_explicit_decline_receipt_v1

Internal guaranteed source evidence: the client-owned source witness retains its
canonical exchange/art payloads and, only when `guaranteedSeverityRank` is present,
a mandatory closed `guaranteePreimage` object with `exchangeJsonBase64` and
`specialArtJsonBase64`. These are the exact prepared strings used by the existing
original guarantee producer, including their property order. Strict Base64,
UTF-8, duplicate-free JSON and canonical semantic agreement are required; the
complete witness digest covers this child. Missing or foreign proof refuses
before publication. A non-guaranteed source forbids the child. This is the current
producer contract, not a migration or fallback for missing historical evidence.
The actual original/source owner still reconstructs and binds the witness;
self-consistent hashes alone grant no source or publication authority. The GM
does not author these private fields and must not copy them into narrative/UI.
Existing special-art envelopes, counterharm, costs and guarantee hash rules are
unchanged; no new GM command field is introduced.

When the same-turn client continuation offers a positive spiritual wound source
without a guaranteed severity, the GM may answer with an explicit decline:
`{"opportunityRef":"spiritual_wound_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","decision":"none"}`.
Use the exact offered `opportunityRef`; do not add a `proposal`, `woundRef`,
severity or acquisition narration. An omitted decision remains unfinished input,
and a guaranteed source cannot be declined. The already admitted exchange and
ordinary resource/effect mechanics still require their normal completion.
For a later optional source on the same conflict side, if the existing wound
already meets that source's maximum, the client offers `none` as the only legal
choice. The same response above completes that source without a second wound;
a direct `materialize` attempt fails even when it bypasses the displayed offer.

After C2 has completed and cold replay has reconstructed the exact owner packet,
the private C3 reducer derives an append-only source row and `none` decision row
for each positive source. The decision binds its full source witness, signed
original turn, instance, global and instance ordinals, continuation generation
and wave. Its selected severity, wound ID and transition ID are null. This
receipt-only companion after-image counts as touched work in the common plan;
it creates no wound, carrier/index/history change or acquisition notification.
The GM never authors the receipt root. Live GameEngine/daemon dispatch and
accepted publication await C4; the example demonstrates the accepted response
shape and private reduction contract, not a currently live player flow.

See `spiritual_wound_private_roots_v1` in `Examples/E_CLI_Afterlife_Turns.txt`
for the worked decline and empty private-root shapes.

## spiritual_wound_materialize_receipt_v1

For an offered spiritual wound, the GM may choose `materialize` and supply the
ordinary wound proposal with the exact offered `opportunityRef`. The final scene
must include the proposal's acquisition narration. The GM does not choose a
canonical wound ID, transition ID or private receipt row. After every C2 source
has a decision, the private C3 reducer pairs each `materialize` choice with its
actual source-bound wound insertion in exchange order. It copies the wound and
transition IDs from the reducer's history, while `none` decisions keep null IDs.
Several decisions may share an exchange, and a later insertion may worsen an
earlier wound; the receipt retains both transitions in causal order.
Within one completed conflict packet, the client rejects a second newly created
wound for the same side. An explicitly targeted, separately proven older active
wound remains eligible for re-traumatization. The current cold C2 continuation
freezes action targets before decision replay, so it does not yet provide a
GM-authored dependent reference to a wound created earlier in that same packet.

The completed accepted plan combines the final wound carrier, identity index,
history, effects and receipt under exact original before-images. This C3 result
is detached and unpublished. Live GameEngine/daemon dispatch, transaction
write/read-back and player notification still await C4. The client rejects an
unproved or mismatched insertion instead of accepting a proposal as proof.

## spiritual_wound_guarantee_satisfied_receipt_v1

For a later guaranteed source on the same conflict side, the client compares
the exact current conflict wound with the source's pre-existing guaranteed
rank. If that wound already meets or exceeds the guarantee, C2 advances
automatically without a new GM `woundDecisions` command. C3 records
`guarantee_satisfied` with the same `woundId` and its
`satisfiedSeverityRank` at that source boundary. `selectedSeverityRank` and
`transitionId` are null; no second wound, effect insertion or wound-history
transition is created. A later lawful worsening does not rewrite the rank in
this receipt. This also applies when the existing rank is at or above the
new source's calculated hard maximum, including a zero maximum.
The ordinary accepted plan requires this authenticated C3 receipt even when
the current turn inserts no wound at all.

If the current conflict wound is below the guarantee, a `materialize`
decision must worsen that wound to exactly the guaranteed rank within the
hard maximum. If no exact owned conflict wound proves satisfaction and the
guarantee exceeds the maximum, the source fails closed. The GM cannot decline
a guaranteed source or name a substitute older wound; an explicit older-wound
re-trauma retains its separate target authority. Cold replay reconstructs
the same client-owned outcome from signed source, instance, side and current
wound evidence; changed evidence rejects instead of silently skipping the
source. See `spiritual_wound_private_roots_v1` in
`Examples/E_CLI_Afterlife_Turns.txt` for the worked continuation.

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

For `spiritual_action_cost_burden`, the exact owner's applicable burden is paid
before `recover_spiritual_power`, including failure; insufficient funds reject.
`force_incarnation` has no base cost and requires only the positive wound burden
in its seven-field 0/0/0 audit; without burden it remains free without audit.
The action-only recovery audit and ordered Spend/reactions/Gain rule are documented
under `spiritual_wound_special_action_costs_v1` in
`OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, with GM-authored fragments
in `Examples/E_CLI_Afterlife_Turns.txt`. Other unresolved profiles are unchanged.

## spiritual_wound_position_v1

`spiritual_position_burden` changes the effective starting position of the matching
exchange, not its saved position. Use ranks `opposition_dominant=-2`,
`opposition_advantaged=-1`, `contested=0`, `player_advantaged=1`,
`player_dominant=2` and compute
`effective = clamp(canonicalBefore + oppositionBurden - playerBurden, -2, 2)`.
Sum each accepted component once for the exact acting participant and its declared
operation. Other actors, other operations and ended effects do not contribute.
Combine both sides before clamping; equal burdens cancel, and neither side's sum
is capped separately. Magnitude remains one step at I-II, at most two at III-IV.

The existing `conflict_position` modifier's `position` names this effective rank.
Effective zero requires no position row; otherwise use exactly one row on the
advantaged side with `value = 2 * abs(effective)`. Preserve every unrelated modifier,
the original dice and roll modes. Effective position also governs the positional
prerequisites of `binding` and `force_binding`; their existing setup and decisive
success alternatives remain available.

Keep `before.conflictPosition` canonical and write the ordinary action result in
`after.conflictPosition`. Do not subtract the burden from either saved field.
A successful or partially successful maneuver still requires an actual canonical
position change, with all existing strain and control restrictions. Repeated
actions do not erode position automatically. Healing or expiry removes only future
applicable burden and does not itself improve canonical position.

A newly inserted wound affects only later matching exchanges. Never recompute a
closed exchange using the current wound list. Cold restoration preserves the
original accepted context; a self-consistent retained audit does not authorize a
new exchange. For a dependent correction, follow the client's current exact repair
request and preserve its saved wound choice, closed prefix and original dice.
GM must not write client-owned wound/effect identities or add an effective-position
field. The worked `spiritual_wound_position_v1` fragments in
`Examples/E_CLI_Afterlife_Turns.txt` show the existing authorable fields.

For a client-issued `dependent_draft` caused by `spiritual_position_burden`, keep
the saved wound choice and use only the current `dependentDraftFields`. The
client derives the effective rank from the actual next exchange and its accepted
actors, operations and effects. A permitted modifier-array pointer is not blanket
permission to replace that array: preserve every nonposition row and its relative
order. Remove the obsolete position row, insert or replace the single prescribed
row on the correct side, or leave no position row at effective zero. Correct only
the listed totals, margin, outcome band and required natural-critical evidence
using the same dice and roll modes. Cost changes require their own listed
permissions; a position correction alone does not authorize them.

The original action, actor, art, target, canonical position, closed prefix and
saved choice remain unchanged. Never infer permission to rewrite an outcome or
`after` state from a dice correction. If changed arithmetic requires different
consequences, the client must supply separately proved current permissions;
otherwise that continuation remains blocked. Return `woundDecisions: []` in the
existing envelope and preserve the current `continuationId`. Cold recovery
reopens the saved selection; it does not invite a replacement decision.

Submit the complete prescribed arithmetic group together; partial or mixed
old/new groups are rejected. An unchanged draft remains diagnosable, but it cannot
advance while the original position error remains.

See the worked spiritual_wound_position_dependency_v1 example for a bounded
correction that preserves the result band and an independent modifier.

### spiritual_wound_dependent_frontiers_v1

A dependent correction may require several client-issued requests. Complete only
the currently issued frontier A: its complete prescribed arithmetic group and,
when required, both nonempty GM-authored critical texts. Keep the saved wound
choice, original dice, independent modifiers and closed prefix. Ready completes
only the issued frontier; it does not promise that every later exchange is ready.
After validating A, the client may issue a new frontier B with its own exact
continuationId. Read that new request and answer it separately with
`woundDecisions: []`. Do not reuse A's response or Ready for B.

Future B corrections are forbidden in A, even if their arithmetic would later
be valid. A listed modifier or critical-result pointer permits only the exact
prescribed structure; it cannot change independent rows or invent consequences.
Missing critical text, wrong scalars or unrelated edits cannot unlock B. The
client validates the entire candidate against A before discovering later fields.
The saved selection is not applied again between requests, and canonical
publication waits for complete accepted-turn validation.

File and worker routes follow the same rule. Worker checks before apply, after
apply and before Ready preserve rollback and current ownership. Cold recovery
reconciles outstanding transport before automatic continuation; public request
files are not authority. A stale Ready or changed private pair is rejected.
GM must not reconstruct private state, replace a choice, delete Ready or guess
the next correlation. See the worked A-to-B example under this marker in
`Examples/E_CLI_Afterlife_Turns.txt`.

The pre-turn preview has not selected the next operations or acting participants.
If an accepted active `spiritual_position_burden` belongs to a current participant,
`dicePreview` is null and `authoringReminders` explains the operation-dependent
position. Do not use canonical position as an unconditional mandatory modifier or
infer a zero burden from that null. Select the exact actors and operations first,
then calculate the effective rank and dice result using the rule above. Unaffected
previews retain their existing dice calculation; this preview grants no authority
to change canonical position or a closed exchange.

### spiritual_wound_binding_result_closure_v1

If a saved position wound removes the prerequisite of an originally valid last
`binding` or `force_binding`, follow only the current client-issued
`dependentDraftFields`. This bounded case requires the original successful
exchange to have changed only control: every other `before`/`after` field must
match. A lower dice margin alone does not grant a new outcome choice. An effective
`+1` or boolean `setup: true` alone cannot sustain `force_binding`; effective
`+2`, `setupState`/`bindingSetup = ready`, or decisive success can. A valid setup,
decisive success or sufficient effective advantage keeps the original result.

In response A, correct the entire exact arithmetic group and any independently
proved action cost fields, then choose an ordinarily valid `no_effect` or
`blocked`. Copy `before.controlState` into `after.controlState` exactly, including
its presence and value. Keep original dice, operation, setup, actors, canonical
position, strain, all other after fields, closed prefix and saved wound choice.
Do not correct the final `activeConflict.controlState` in A. An unrelated ordinary
validation error still rejects A. The client's temporary final-control echo
used during A validation is detached; GM and workers must not write it or any
private checkpoint, progress row, receipt or canonical state.

Only after actual A passes ordinary validation does the client issue response B.
B permits the one effective raw final-control carrier indicated by its exact
pointer and requires the validated last `exchange.after.controlState` value and
presence. Do not edit ignored wrapper siblings or a whole container. B needs its
own `continuationId`, response with `woundDecisions: []`, and Ready. Keep accepted
A unchanged. The client validates the actual cumulative raw draft after B and
publishes only after complete acceptance. Cold replay may derive B with zero
unaccepted exchanges; it creates no new exchange, wound choice or GM progress row.
See `spiritual_wound_binding_result_closure_v1` in
`Examples/E_CLI_Afterlife_Turns.txt` for the two authorable responses.

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
scalar publication contract; the failed complication band now publishes its selected direct addition.

The client validates the complete cumulative graph after every authored operation, including unselected procedure bands, before claiming a die or resources.
The same canonical rules revalidate retained treatment routes and deterioration policy at the resulting severity, and diagnosis facts against remaining accepted complications. A symbolic local reference never restores a removed accepted diagnosis fact.
Local complication and definition references are not permanent runtime identities.
Applicability preview does not authorize publication of an unfinished outcome.

Selected direct add_complication outcomes publish through one authenticated treatment graph batch.
Same-rank retained wound roots keep their exact identities, carrier payloads and index history.
Mixed remove/add results must preserve original root-definition bindings and unchanged definition bodies when the final severity is unchanged; this is checked before the roll. A fresh-key replacement or a legal later severity reduction remains possible.
An effectless addition still requires a sealed 0/0 batch and an empty application result map.
New root and child-only exact-skill selectors require fresh Offered/Current authority; retained selectors remain accepted continuations.
Selected non-death apply_deterioration outcomes publish through the same authenticated treatment graph batch.
The client reobtains the original accepted wound policy; the GM supplies policyRef, never private preparation or permanent identities.
Only a change between original and final severity rematerializes retained roots; an unchanged final rank also preserves the original slot budget.
Policy root and child-only selector diagnostics name recovery.deteriorationPolicy.result.complicationDraft at the canonical wound source.
Direct treatment death, heal and legacy publication remain unfinished.

Selected policy outcomes are legal only in procedure partial/failed results and actual interrupted courses.
An interrupted course clears only its course pointer and does not consume the current or future dose.
Policy increase/add itself leaves care, recovery progress and anchors untouched unless another declared
operation explicitly changes them. It does not imply death or schedule a natural recovery tick.
An original rank-IV increase is inapplicable even after an earlier reduction in the selected list.
A net-zero rank sequence with a different slot budget is rejected before die/resource claims; the client
never repairs that budget automatically, prunes effects or weakens retained requirements to make it fit.

In this failed-band example the client derives the complication identity and exact namespaced
definition/application bindings from the selected request. It creates only the new parentless root;
the old wound root is retained. A legal partial-success mixed result or trusted course interruption
can also publish direct additions without becoming a successful route completion. Selected
removal retires only its causal ownership closure; if the ordered result reduces severity, the
client materializes the final graph once and gives each surviving original root exactly its
original predecessor. New roots stay parentless even when added before reduction. No temporary
generation is published. Effects, wound, resources, history and output share the existing atomic
transaction; rejected publication and post-write rollback restore the complete original tree.
Ordinary unrelated effect lifecycle ticks continue normally during a successful turn.

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

## mortal_wound_treatment_selected_policy_v1

Install this complete authored recovery policy and complete procedure route into an untreated rank-III
Mortal physical wound whose retained consequence graph fits rank II. This is not a complete canonical
wound or public receipt. The failed attempt selects its existing canonical policy and produces rank IV;
maximumAtCreation remains unchanged. All permanent identities and publication authority are client-owned.

```json
{
  "recovery": {
    "deteriorationPolicy": {
      "policyRef": "untreated_deep_wound",
      "unmetConditions": ["not_stabilized"],
      "graceMinutes": 30,
      "cadenceMinutes": 10,
      "result": { "kind": "increase_severity" }
    }
  },
  "treatment": {
    "diagnosisPaths": [],
    "routes": [
      {
        "routeId": "clean_deep_wound_with_policy_risk",
        "displayName": "Очистить глубокую рану с риском ухудшения",
        "visibility": "known_to_player",
        "mode": "procedure",
        "requirements": [
          { "kind": "item_quantity", "itemRef": "sterile_dressing", "quantity": 1, "ownerRole": "provider" },
          { "kind": "skill_tier", "capabilityRef": "field_medicine", "minimumTier": 2, "actorRole": "provider" }
        ],
        "resourcePolicy": {
          "reserveBeforeResolution": true,
          "consumeOn": ["success", "partial_success", "failed_attempt"],
          "refundOn": ["cancelled", "validation_failed", "rolled_back"],
          "mutations": [
            { "kind": "consume_requirement", "scope": "common", "milestoneOrdinal": null, "requirementIndex": 0 }
          ]
        },
        "resolution": {
          "formulaKey": "mortal_wound_procedure_v1",
          "difficulty": 15,
          "rollSource": "accepted_d20",
          "criticalPolicy": "natural_20_first_natural_1_last",
          "modifierSource": { "kind": "resolved_skill_tier", "requirementIndex": 1 }
        },
        "outcomes": [
          {
            "bandId": "deep_wound_success", "minimumMargin": 5, "maximumMargin": null,
            "category": "success",
            "result": [{ "kind": "stabilize" }, { "kind": "reduce_severity", "steps": 1 }]
          },
          {
            "bandId": "deep_wound_partial", "minimumMargin": 0, "maximumMargin": 4,
            "category": "partial_success",
            "result": [{ "kind": "stabilize" }, { "kind": "add_recovery", "points": 2 }]
          },
          {
            "bandId": "deep_wound_failed", "minimumMargin": null, "maximumMargin": -1,
            "category": "failed_attempt",
            "result": [{ "kind": "apply_deterioration", "policyRef": "untreated_deep_wound" }]
          }
        ],
        "interruption": null
      }
    ],
    "knownRouteIds": ["clean_deep_wound_with_policy_risk"],
    "completedRouteIds": []
  }
}
```

## spiritual_wound_terminal_closure_v1

The internal publication boundary following this C3 plan is documented below as
spiritual_wound_common_publication_v1; live GM dispatch uses C4-B.

For the bounded private terminal path, an existing `resolve` response retains a
complete `terminalExchange` directly after the signed active baseline. Resolution
and exchange dice audits agree exactly; original operation, actors, costs and
source evidence remain authoritative. Follow the matching afterlife matrix and
worked example. Existing reward and reward anti-farm rules are unchanged.

The last action and its causal effects finish before opposition resource
retirement (Spend before Retire). A real completed owner proof permits one
immutable closure; original instance/start and earlier receipt rows remain
unchanged. An accepted prior closure is required before a reused display ID can
create a distinct instance. GM must not write private receipt/proof fields or
retire resource owners. Missing prefixes and broader terminal source families
remain explicit unfinished work, not implicit declines. C3 is an unpublished
common plan; live GameEngine/daemon publication uses the C4 common publisher.

## spiritual_wound_source_only_publication_v1

When every original exchange source has a zero ceiling and no guarantee, the
client returns `no_offer`: no wound decision is requested. For an already signed
`dangerMode: training` conflict, author the ordinary exchange, its complete dice
and cost audits, and narration. Do not change the signed danger declaration to
avoid a wound. No fabricated none, woundDecisions, wound command or C2 checkpoint
is needed; missing answers to positive offers still never mean decline.

The client exhausts the original exchange inventory and completes ordinary costs
and effects. Its instance-only receipt has no new source/decision rows; a terminal
exchange additionally produces the actual immutable closure, with Spend before
Retire. Existing reward rules and persistent actor wound/effect groups remain
unchanged. GM must not write private instance, closure, receipt or resource-owner
fields. A single common publisher commits the completed result without reallocating
identities. Legal empty private roots are bound by their exact bytes just like
absent roots; publication consumes them and failure restores signed bytes/absence.

This bounded warm handoff is used by live B3 routing.
After interruption, the client restores the signed original turn or retains the complete accepted turn.
Receipt-only state does not prove whole-turn acceptance.
The worked example is `spiritual_wound_source_only_publication_v1`
in `Examples/E_CLI_Afterlife_Turns.txt`.

## spiritual_wound_common_publication_v1

The internal C4-A common transaction receives the completed spiritual plan without
replanning, with one-shot publication authority and separate signed original,
draft and committed physical images. Uncommitted edits reject with zero writes;
failure after writes restores signed original bytes and signed absence. Successful
publication checks the receipt against the final wound/effect/history images and
consumes both private roots plus wound_commands, including none. GM must not write
private publication or receipt fields. The matching afterlife matrix and worked
example describe this client boundary; live GM dispatch uses C4-B.

Live accepted output: preserve every accepted acquisition and worsening description
in the final output/narrative_response.json.response, in exchange order. Creating
then worsening the same wound produces two player messages at the respective ranks;
none and guarantee_satisfied produce no extra acquisition notification. The client
binds the final narrative, interface and debug output to the accepted publication,
rejects substituted output, and consumes each accepted notification once. Refreshing
the same completed response does not replay those notifications. These are client
responsibilities: GM must not author notification IDs or publication/completion flags.
After interruption, the client restores the signed original turn or retains the complete accepted turn.
GM waits for the current client request and must not resend a saved choice.
Receipt-only state does not prove whole-turn acceptance; do not reconstruct private authority.

## spiritual_wound_continuation_envelope_v1

The existing repair/worker protocol has a closed optional
`spiritualWoundContinuation` envelope. Use it only when supplied by the client.
Copy schemaVersion 1 and exact continuationId. In phase `decision`, return one
current `woundDecisions` row: explicit none, or the existing complete materialize
proposal with woundRef. Respect the offered decisions, locations and severity;
a none-only offer can have a minimum rank above its maximum. In `dependent_draft`,
return an empty array and correct only the client-listed dependentDraftFields.
Never replace the saved choice or write private checkpoint, command or receipt roots.

Narration stays in output/narrative_response.json.response, with the ordinary
timestamp and preserved independent siblings. Request/response keys are exact-case;
unknown fields and duplicate keys are rejected. Missing response never means none,
and ordinary repair must not include this envelope. A completed decision proposal
may have empty changedFiles if no draft correction is needed; ordinary repair and
dependent correction retain their changedFiles requirement. These are structural
checks only: genuine owner admission and before-apply/before-ready rechecks remain
mandatory. The strict envelope parser alone does not enable live dispatch;
GameEngine authenticates and resumes C2. See the matching worked envelope example.

For the file helper, use `Complete-BoeValidationRepair
-SpiritualWoundContinuationJson <raw response-envelope JSON>` only for a current
client request containing this envelope. Pass the response object itself, without
another `spiritualWoundContinuation` wrapper. Explicit `none`, a complete
`materialize` proposal and `dependent_draft` with `woundDecisions: []` use the same
parameter. The helper rejects missing responses, stale correlation, unknown or
duplicate keys and case aliases before publishing Ready. Ordinary repair calls
`Complete-BoeValidationRepair` without this parameter and forbids a continuation
response. A decision request may have `errors: []`; continuation always has
`fullTurnResubmissionRequired: false` and never restores the turn baseline.

The helper compares exact request and Ready read witnesses under the canonical
write lock before atomic Ready publication. A changed request or newer Ready,
including a Ready for the same request, causes rejection without overwriting it.
An already published continuation Ready also blocks a new submission; wait for
the client to consume or reject it instead of replacing or deleting it.
Reread the current request after rejection; do not delete Ready or resend an old
response blindly. The helper does not consume C2: the GameEngine must authenticate
the current owner, read the scene and consume/resume the response under its lease.
Helper transport support does not establish completion of live GameEngine routing.
The worked file-helper commands are in `Examples/E_CLI_Afterlife_Turns.txt` under
`spiritual_wound_continuation_envelope_v1`.

## spiritual_wound_pending_submission_v1

Before a selected wound can affect a later original exchange, the private client
transport retains pendingSubmission in the existing checkpoint. It binds the exact
selected decision, original composed command including scene text, and real
selection ID/time prefix. The committed cursor and derived pending remain unchanged.
An already satisfied guarantee retains guarantee_satisfied with command null;
there is no new GM choice. Cold recovery uses the real original owners and must
not reoffer the saved choice or allocate its identities again. A corrected
continuation advances and clears pendingSubmission in one checkpoint replacement,
then updates derived pending. Failure before confirmed advancement preserves the
selected record; ambiguous read-back blocks. GM must not write this record, repair
its hashes, replace the saved command or treat a command file as replay authority.
The direct in-memory decision draft remains write-free. This private persistence
boundary alone does not authorize dispatch; GameEngine authenticates and resumes
C2 through C4-B. After interruption, the client restores the signed original turn or retains the complete accepted turn.
GM waits for the current client request and must not resend a saved choice.

The live entry uses checkpoint-first recovery when a confirmed checkpoint's
derived pending write fails. A repeated write failure retains the original turn
and saved choice for cold recovery; GM must not write private recovery files or
replace the decision. Wait for the current client request before another response.
The original completion signal stays unchanged while the current gameplay loop
stops. Explicit session re-entry uses that signal through the normal lifecycle;
the client does not mint a replacement GM success.
An ordinary repairable resource omission before C2 exists keeps the existing
full-turn repair route. Public request/Ready files never restore missing private
authority, and a failed initial capture with no ordinary errors stays rejected.

## wound_player_status_readonly_v1 — current read-only player status

`/статус` and `/status` immediately show the current player's visible active wounds
in Mortal World, Chaos Sea, or Shining Abode. A known wound and its already-known
methods do not require a diagnosis reveal gate. Hidden wounds, unknown routes,
private complications/effects and other owners remain private; future discoverability
is not current knowledge. Mortal wounds do not follow the soul into afterlife status.

This is a client-owned read-only view. The GM sends no response for opening it and
must not repair, rewrite, heal, charge resources, advance time, or stage commands just
because status was inspected. Canonical wound identity/history and accepted effects
remain client-owned. Dynamic names and descriptions are displayed as literal text.
Declared requirements describe a known method; they do not confirm resource
availability, an available helper, affordability or an exact price. Current accepted
Mortal requirement references become readable authorized labels. Known spiritual
methods currently show generic conditions; opaque route payloads are not a treatment
resolver or an Ink Feather quote.

Validated healed snapshots appear separately under `История ран`, never in the active
list. Missing or incoherent wound authority gives `Сейчас невозможно надёжно определить
состояние ран.` rather than claiming the player is healthy. This read-only status
inspection itself creates no treatment intent. The separate guided self-only route
below now owns `/раны`/`/wounds` and `/лечить`/`/treat` (`/исцелить`/`/heal` aliases);
helper/entity/combat routes and Ink Feather quotes remain outside this slice. See the
`wound_player_status_readonly_v1` display examples in
`Examples/E_CLI_Wound_Materialization.txt` and `Examples/E_CLI_Afterlife_Turns.txt`.


## spiritual_self_treatment_v2 — guided self treatment outside conflict

Known-wound assessment: opening `/лечить` compares a currently known visible
spiritual wound with the validated current `spiritual_healing` tier. H0 and positive
insufficient H explain the required wound rank even when no treatment is available.
This is assessment only; no GM turn, healing attempt/die, spending, wound/History
change or world cycle. A missing/mismatching source is unavailable, not H0. The
client keeps valid help options selectable and revalidates every confirmation.
No hidden diagnosis facts or new diagnosis roll/time rule are introduced.


In Chaos Sea / Shining Abode, `/раны` (`/wounds`) shows visible current-self active
wounds and separate healed History. `/лечить`, `/treat`, `/исцелить`, `/heal` opens
self wound selection plus confirmation. Tier0 may inspect known facts; treatment
requires mastered Spiritual Healing at least the selected severity I–IV. Hidden,
foreign and former-realm wounds are not selectable. Helper/entity/combat treatment
and Ink Feather service/quotes are not enabled by this self-only flow.

Opening/cancelling the form creates no gameplay mutation, draw or world cycle.
Confirmation queues only client-owned exact selection; staging under the actual
turn/control creates `game_state/wounds/wound_commands.json` schemaVersion2,
kind `spiritual_self_treatment_request`. GM MUST NOT write/replace/delete it, mint
snapshotToken, change the attempt/roll, or spend self OD. This is separate from v1
wound commands. The independent retained die is not taken from the GM dice pool.
Failure still advances one ordinary safe cycle. Active treatment runs before one
natural recovery grant to eligible self wounds; client publishes all wound/effect,
identity, History, scheduler and output members atomically. Healing removes the
active wound and retains one verified terminal History entry.
The client derives healing/recovery notifications from the finalized chronological
stages after publication, including the terminal wound image after carrier removal.
It preserves wound visibility and does not expose raw wound/effect IDs or snapshot
tokens. GM does not author an acquisition claim or extra receipt for these notices.

Read `input/turn_request.json.spiritualHealing` and its `destinationGraphs`.
The client computes active/natural destination ranks and opaque stageRefs from the
retained attempt. For EVERY requested stageRef, author exactly one row in
`output/interface_updates.json.spiritualHealingGraphs` with exactly `stageRef` and
`consequenceDefinitions`. Each definition wrapper has `definitionRef`, `definition`,
and `root` with `ownership` (`kind`, explicit nullable `complicationRef`) and `slots`
(`profileKey`, `readableSummary`), using the existing closed wound consequence grammar.
Use only client-provided complicationRefs. Definitions contain `links: []`; the
client binds predecessor identities, ownership and generations. No effect IDs,
manual wound edits, automatic survivor choice or substitute rank is accepted.
For III/H3/d12, for example, author the requested II graph (roll hindrance plus
cost burden1) and then I graph (roll hindrance). Both are independently authored;
the client checks profile restrictions, exact budget and intermediate retirement.
A missing/invalid graph enters repair with the SAME attempt/dice and no publication.
After publication consumes the request, interface validation still binds graphs to
the exact signed original and verified accepted stages. Ordinary dialogue normalization
remains legal; replacing a stageRef or its accepted consequence content is refused.

When destinationGraphs is empty, omit spiritualHealingGraphs (or use an empty
array); no graph is needed for rank-I terminal healing. Graphs-only interface payload
is legal only for the actual signed self-v2, explicit spiritual_guardian_aid_v3 / spiritual_guardian_help_v4 or authenticated natural family below;
unbound ordinary/Mortal payloads keep their prior refusal. Always include a fresh ISO timestamp. Write ordinary narrative,
mandatory NPC Scope reasoning and the progression report from actual progressionControl.
Never recreate consumed request/report or alter accepted output on replay. Retained
browser original bytes prove identity after cleanup; accepted cleanup also verifies
strict matching spiritual History/control/schedule/cohort, while settled means restored,
not healed. No additional GM receipt or lifecycle acknowledgement is authored.


spiritual_natural_recovery_v2 — ordinary safe-cycle recovery (explicit natural mode)

An ordinary accepted safe Chaos Sea / Shining Abode cycle may carry
`input/turn_request.json.spiritualHealing.mode="natural"`, schemaVersion2.
This projection is client DATA derived from the authenticated signed start:
sourceFingerprint, cycleOrdinal/cycleKey, ordered cohort (woundId, owner,
woundName, explicit healingTier0..5, woundFingerprint) and destinationGraphs.
It creates no wound_commands.json, treatment request, attemptId or healing roll.
General turn dice are unrelated; never draw or fabricate a treatment die.
Each persistent current-realm spiritual I–IV wound gains exactly `1 + H`, using
its own signed start-of-cycle tier. Hidden/offscreen eligibility is mechanical,
not permission to reveal NPC wounds. Thresholds I/II/III/IV are2/4/6/8; points carry
across weakening steps and terminal healing. A same-turn lawful art upgrade stays
accepted for the next cycle; ordinary resource costs and other allowed outcomes
remain part of their existing commands, while natural recovery itself charges nothing.

Copy EVERY supplied destinationGraphs.stageRef and complicationRef into the same
closed `output/interface_updates.json.spiritualHealingGraphs` grammar used above.
The client computes ranks, binds the graph to the full original cohort, retires
old/intermediate effects, writes exactly one complete schema2 natural History result
on the first recover row and advances the cycle atomically. The natural result has
request=null and no attempt/roll. Do not author that result or edit wound/effect
carriers/identities/history/scheduler. An empty cohort creates no fake result.
Missing graphs/report can be repaired on the SAME ordinary request/cycle; never
create a treatment request, repeat a natural tick or recreate a consumed report.
If an exact subsequent accepted_turn_output_artifact_repair requests fresh narrative
and interface artifacts, follow that packet only, preserve the graph bodies/refs and
debug, and keep all mechanics unchanged. Unbound/Mortal graph fields remain refused.

This ordinary-safe-cycle slice excludes active conflict, catchup, prepared
incarnation and unresolved terminal-owner/realm transfer ordering. Concurrent wound
commands/opportunity stages are explicitly refused before publication rather than
silently overwritten. It does not expose NPC/private wound details or implement
helper/entity active healing. Mortal mechanics are unchanged; no new Mortal output
or command is introduced. Read the worked spiritual_natural_recovery_v2 example in
Examples/E_CLI_Afterlife_Turns.txt and use the actual progressionControl report.


spiritual_treatment_participation_v1 — accepted permission to help the current Guardian

During an ordinary safe Chaos Sea turn the player may OFFER to heal the current
conscious/capable Guardian. Consent is affirmative roleplay evidence, never inferred
from friendliness, visibility, canTeach/canFight or mere presence. On the exact
current Guardian's existing complete profile, author the CLOSED nested
spiritualTreatmentParticipationChange: schemaVersion1, operation=grant|revoke,
healerId=player_soul, actual stable woundId, conscious/capable booleans and nonblank
readableEvidence. Grant requires both booleans true. Preserve the profile's other
required fields. profiles, afterlifeEntityProfiles and afterlifeEntityProfileUpdates
use the same rule; identical mirrored intent is one change, conflicting mirrors fail.

The client alone creates spiritualTreatmentParticipation with the real accepted
session/request/token/turn, exact Guardian/Abode/wound, accepted cycle, next eligible
cycle and primary transition. Never author, copy, delete or alter that canonical
record, a participationOutcomes witness, History or a receipt. Omitting the old
record in a legitimate profile replacement preserves it; revoke through the proposal.
The actual natural/common publisher commits FULL record + content digest in one
schemaVersion3/mode=natural result together with profile/wounds/effects/History and
one cycle, or rolls them all back. A raw/same-turn/failed proposal grants no healing
draw. The grant's natural tick may change woundFingerprint; consent scopes the
stable woundId, and later selection must use the fresh wound fingerprint.

Grant accepted in shared cycle10 permits only the NEXT safe cycle11. Missing,
expired, revoked, wrong healer/Guardian/Abode or stale wound refuses before any
healing entropy. Current revocation or movement also blocks unpublished aid.
This first authoring slice supports only player→current Guardian; no Mortal,
entity→player substitute consent, helpless/unwilling/conflict/paid/Shining aid.
Existing self request/resultv2 and no-participation natural resultv2 remain actively
produced closed current forms. V2 participation and payload-free V3 are invalid,
not migrations. This authoring step does not itself start treatment or let the GM
write an aid request. Read the worked participation example in E_CLI_Afterlife_Turns.txt.


spiritual_guardian_aid_v3 — player helps the current consenting Guardian

After an ordinary accepted spiritual_treatment_participation_v1 grant turn,
the player may confirm that current visible Guardian's known I–IV wound in the
shared /лечить flow. This first route is player→current Guardian in a safe Chaos
Abode, not Guardian→player, paid/conflict/helpless/Shining aid or implicit consent.
The client proves FULL accepted record/content digest, actual previous turn and
next eligible cycle BEFORE control writes and entropy. Successful Engine cleanup
removes the preceding input/turn_request.json; do not recreate it. Current
History/profile/schedule must independently prove the committed grant.

The actual turn_request.spiritualHealing schema3/mode=aid names healer and target,
healingTier (HEALER H), targetNaturalHealingTier (TARGET H), participationVerified,
retained roll/activeResult and computed destinationGraphs. Keep the client-owned
spiritual_treatment_request schema3/mode=aid in wound_commands unchanged, including
healer/target/activeSource/participation/attempt/requestFingerprint. A field saying
participationVerified is GM comparison DATA, not permission to mint a grant.
Self requestv2 and resultv2 remain current; no-participation natural resultv2 and
participation-natural resultv3 are separate closed forms. Aid resultv3 has the
exact accepted grant/active source, NO new participationOutcomes. Cross-version
payloads and self+new grant/revoke refuse; no migration or fallback.

Author exactly each requested destinationGraphs stageRef through the EXISTING
spiritualHealingGraphs closed consequenceDefinitions grammar. Do not infer target
natural H from healer H, roll again, alter wound/effect/History identities, author
client results or recreate consumed request/report. One active result precedes the
ONE universal natural pass; one safe cycle runs even on failure, with no aid OD
charge. Missing graphs repair retains the same attempt/dice. A second Stage call
refuses before another control/draw and preserves exact pending; continue that
same signed original through B/repair/I. Never delete pending to obtain a new roll.

For this scoped actual Guardian journey, publish Actor Brain first-person memory
through guardianThoughtJournalUpdates in game_state/meta/guardian_thought_journal.json,
with the canonical Guardian name in Relevant actors and a full Brain block. Keep
guardians.json/residents locality unchanged. Concurrent musings append in the
locality file is NOT qualified by this slice and may refuse. Current revocation,
move, missing/incapable consent or changed selected source blocks aid publication;
settled replay checks its accepted result, not renewed live consent. No changes to
Mortal treatment permission or other unsupported afterlife helper routes.


Client-owned locality normalization: owned locality normalization may canonicalize
Guardian/resident representation inside the SAME validated spiritual owner and
lease. It proves unchanged exact current Guardian/Abode/local presence before the
ordinary write, exact readback afterwards, then advances only live closing bytes.
The signed original, full witness, source fingerprint and attempt remain frozen.
This is not GM permission to alter locality, use implicit consent or suppress a
foreign-byte refusal. The same boundary covers grant and aid; current manual
musings/locality edits remain outside this first qualified route.

Client-owned complete profile projection: the SAME spiritual common plan applies
the admitted turn report's existing automatic actor progression and player-soul
mirrors once, alongside wound/effect/History/scheduler publication. Recovery and
active treatment use their signed start tiers; a lawful new art tier remains in
the final profile and applies to later cycles. The generic profile writer skips
only a complete root actually contributed by that private validated plan. Raw
profile currency/art mirrors cannot grant player currency or healing capability;
exact precommit source bytes and signed evidence remain frozen. This is client
composition, with no new GM field or permission.


spiritual_guardian_help_v4 — current Guardian helps the player after real confirmation

This is FREE PRIVATE help in the current safe Chaos Abode, not a public/paid US6
service. The player asks the exact visible current Guardian for help with a known
active I–IV player spiritual wound. GM may express the Guardian's willingness and
current conscious/capable facts, NEVER player consent. On that existing Guardian
profile only, author closed spiritualTreatmentParticipationChange schema2:
kind=provider_offer, operation=offer|revoke, targetId=player_soul, stable woundId,
providerConscious/providerCapable/targetConscious/targetCapable and readableEvidence.
All four facts must be true for offer. Healer identity comes from the exact
Guardian profile/current locality; do not supply client stamps or playerConsent.
All three profile representations use the same rule; mirrored exact intent is one.

The actual ordinary natural owner atomically commits the Guardian's record2 plus
FULL record/content digest in natural result4/History with the PLAYER wound's
natural tick, real binding/primary row and next eligible cycle. The Guardian need
not be wounded. Raw/same-turn/failed/rolled-back offers authorize no treatment.
A changed player wound fingerprint from that natural tick does not invalidate a
stable-wound offer; later selection binds the fresh exact wound fingerprint.

After normal Engine cleanup, /лечить lists Self first and shows the available
Guardian method for the player's wound. Menu receive_aid selection is unconfirmed;
only actual console confirmation or browser confirm_wound_treatment plus the
existing ConfirmAsync/one-shot/queued binding issues player_target_confirmed.
Cancellation creates no treatment request/draw/cycle. A serialized marker or GM
text is comparison DATA, not consent authority. Never write confirmation for the
player or manufacture an original/pending entry.

Before control/input writes or draw the client proves actual prior offer result,
exact next cycle, both actors' current condition, player wound and Guardian art/
locality. Active spiritual_treatment_request v4 has mode=aid,
direction=guardian_to_player, playerConfirmation=player_target_confirmed and exact
healer/target/participation/activeSource/retained attempt/dice. Preserve it. GM reads
the actual turn_request.spiritualHealing schema4/direction and authors ONLY its
requested spiritualHealingGraphs through the existing closed grammar. Guardian H
controls active math/roll effects; player H controls natural recovery. One active
then ONE full natural pass, one safe cycle and no private treatment charge.

Current revocation/move/condition/capability drift refuses before entropy or
publication; after a draw keep the SAME retained request/dice through repair.
Settled result4 resolves the actual historical offer, never renewed live consent
or the old grant schedule. Existing v2 self/plain natural and v3 player→Guardian/
consent-natural are STILL produced closed forms; cross-version fields, v4 missing
confirmation and mixed active+new offers refuse. Use actual Guardian Brain with
canonical name and guardianThoughtJournalUpdates, keeping locality roots unchanged.
No Mortal authority, unwilling/conflict, helpless, Shining helper or public/paid
permission is added. See the two-turn worked example and manifest.


spiritual_combat_healing_v1 — confirmed player-self healing in a direct Chaos duel

The existing /лечить choice/confirmation creates a client-owned combat_self intent
BEFORE the actual original snapshot. This is only standard spiritual_healing by
player_soul on its own current visible wound I–IV, conscious/capable, in the active
direct_duel Chaos conflict. The wound, conflict prefix, next exchange ordinal,
art H, exact sources and scheduler control must still agree. Other targets,
champions/assisted sides, special healing arts, other realms and public/paid US6
are unsupported. Passive spiritual_resilience is never an action. Healing-scoped
wound cost modifiers are not registered by this slice.

GM reads input/turn_request.json.spiritualHealing mode=combat_self with operation
heal_spiritual_wound, possibleDestinationGraphs and prepared healing source. Keep
the client intent untouched. Use ONLY the actual original preGeneratedDices1d20
pool in the ordinary C2 diceAudit; no safe attempt, no independent healing die.
Every selected/discarded marker and sourceIndex must match the existing roll
mode. Opposed margin resolves interruption; healing DC/margin is separately
client-derived from the SAME selected player die, art and wound complications.
The operation is counterable, baseCost5/minCost2, standard art reduction H:
I→4, II→3, III→2, V→2. It remains one typed afterlife_cost Spend even when legally
blocked/countered. A blocked outcome has ZERO healing stages; counterharm may
worsen the SAME original wound through its separate injury source/decision.

Author the declared conflict exchange via its existing state/update contract.
For each actually required weaker nonterminal stage, write ONLY the exact
requested stageRef and closed consequenceDefinitions under
output/interface_updates.json.spiritualHealingGraphs. Never mint wound IDs,
History results, resource transitions, healingSources/healingDecisions or receipts.
The existing paired C2/C3/C4 publishes those client-owned members atomically.
Missing graphs are repaired before the initial pair on SAME intent/original dice.
A later ordinary exchange uses the same saved dependent correction flow; it does
not permit a second healing action. Healing-only still requires pending schema2
and a durable outcome; no_offer/source-only cannot skip the action.

There is NO safe cycle: do not write progression_report.json. Scheduler-owned
zero-cycle control is backed by the actual signed combat basis; pending/catchup
backlog refuses rather than being cleared. LastTickKey and schedule/world bytes remain unchanged; no natural progress is
added, including a resolved failure or full heal.
Existing actual C4, output binding and I still validate authored graph semantics;
repair cannot change source, dice, selected decision or already accepted effects.
Retained source/outcome DATA does not grant publication or replay permission.


spiritual_resident_aid_v5 — player treats a consenting current-Abode resident

This bounded private free direction extends /лечить only in the current safe Chaos
Guardian Abode. The known active spiritual wound belongs to an exact visible,
conscious, capable, present resident. A resident and Guardian may share a textual
ID; full realm/kind/id/carrier coordinates and exact roster/profile/Guardian/Abode
agreement distinguish them. Presence, friendliness or a readable name is not consent.
No paid/public service, resident healer, Shining, combat or helpless exception.

During an ordinary turn, author only spiritualTreatmentParticipationChange schema3
on that existing resident profile: kind=resident_target_consent, operation=grant|revoke,
healerId=player_soul, woundId, conscious, capable, readableEvidence. Grant requires
both facts true; revoke may set them false. All three existing profile representations
use the same closed proposal. Never write the canonical record, accepted binding,
cycle stamps, client selection/request/die or History. The genuine natural owner
publishes record3/full witness in result5/History alongside that cycle's natural
recovery. It permits only the immediately following accepted turn/next safe cycle.

After normal cleanup, guided /лечить keeps Self first and offers the known exact
resident. Cancellation is inert; confirmation/staging rechecks current source,
consent and locality before one draw. The client owns selection/request/result
family5, mode=aid, direction=player_to_resident and complete typed healer/target.
Author only the requested spiritualHealingGraphs using existing weaker-graph grammar
and actual stageRefs/client complicationRefs; do not choose a new numerical result.
Player H resolves active healing, resident H resolves its single natural pass.
H3/d20=14 against III1 gives active II1 then targetH0 natural II2, ONE cycle/no fee/OD.
V3/v4 currently produced forms and immutable historical fingerprints stay unchanged.

Meaningful resident Actor Brain still needs first-person memory. For this protected
turn put spiritualResidentThoughtJournalUpdates in output/interface_updates.json
(or the identically named output-only GameResponse property), NOT raw journal
commands in the frozen roster file. Entries reuse the existing resident journal
format: required entryId,residentId,turn,timestamp,title,summary; optional existing
eventType,consequence,attitude,intent,tags. Use exact current participant/turn,
unique new entry IDs and UTC timestamp; preserve every prior entry. The SAME live
continuation guards the complete response, proves append-only DATA and feeds the
existing residentThoughtJournalUpdates normalizer. Its owned writer preserves typed
locality and proves exact physical before/after bytes. Ordinary resident interactions
keep their old journal mapping. No new journal path/cache/permission exists.
The accepted interface validator permits this comparison field only after exact
family5 publication, with the same authenticated original/response and complete
canonical journal append. Memory-only output is valid; unowned/unpublished or
wrong-family memory fields remain errors, never ordinary UI properties.

Late consent/locality/response drift refuses without reroll or partial acceptance.
Current settlement retains source/control/scheduler/cohort/identity/History guards.
Historical receipt parsing uses its recorded witness/grant/request/stages, never
asks a departed resident to consent again. A later changed scheduler after-image
is not permission to replay an old current settlement. See the two-turn worked
spiritual_resident_aid_v5 example; use the actual client packet, not copied IDs.


spiritual_resident_help_v6: /лечить also permits the opposite resident→player private
free direction in the CURRENT Chaos Guardian Abode. A preexisting visible conscious
capable resident HEALER authors only resident_provider_offer proposal4 (offer|revoke,
player_soul patient, exact wound and four conscious/capable facts). The real natural
owner publishes record4/result6; after normal cleanup, only the player's actual
confirmation permits request6 direction=resident_to_player/player_target_confirmed.
Preserve the original witness/source/attempt/dice. Helper H drives the active check;
patient H drives natural recovery; ONE cycle/no fee/OD. Author only requested weaker
spiritualHealingGraphs for the PLAYER wound and protected resident-HEALER memory via
output-only spiritualResidentThoughtJournalUpdates. Include the ordinary Guardian's
Actor Brain/journal when its usual progression changes it. No raw roster rewrite,
same-turn offer, automatic player consent, new cache, paid/public/helpless/combat or
Shining permission. Read the resident-provider worked example in
Examples/E_CLI_Afterlife_Turns.txt and Wound_Materialization_Contract.md. Existing
self2/Guardian3/4/resident-patient5 contracts and immutable receipts remain exact.

Offer4 is closed: schemaVersion,kind=resident_provider_offer,operation=offer|revoke,
targetId=player_soul,woundId,providerConscious,providerCapable,targetConscious,
targetCapable,readableEvidence. Offer requires all facts true; revoke may clear
facts but never authorizes treatment. Owner derives from the enclosing exact
resident profile, not a scalar Guardian ID. Record4 stores closed typed healer and
target, exact current guardianId/abodeId locality, the offer facts, accepted binding,
acceptedCycleOrdinal/eligibleCycleOrdinal and primaryTransitionId. Only the client
publishes it; natural result6 embeds the full outcome with the actual natural tick.
Its eligibility is only the immediately following accepted turn/next safe cycle.

Selection6 adds mode=receive_aid,direction=resident_to_player, exact healer/target/
locality, woundId/expectedWoundFingerprint,participationFingerprint and
confirmation=required|player_target_confirmed. It is client DATA, never a GM request
or permission. The real confirmed active request6 and result6 retain that direction,
full witness, source, sealed one attempt and die. The player remains wound/effect
owner; the resident is healer and protected-memory owner. Do not reuse resident-
patient5 target/effects or a Guardian4 header. The full memory append is checked
against the original roster/retained response and same owned publication; family6
has no general interface exemption. Cancellation/unconfirmed/unavailable offers
refuse before side effects. Current drift refuses without reroll. New offers and
admission/Stage/B/normalization/closing require fresh live typed locality; historical
and current-settlement reads retain existing record/control/source/schedule/cohort/
History checks without renewed presence. Departure preserves historical evidence,
never new permission. Both journals preserve the old prefix and append one exact
current entry. C4 restores submitted prepared request/die/raw memory command; outer
rollback restores accepted offer state. No changes to paid economics or Mortal
contracts; provider, DOM and fresh-process qualification are separate.


wound_target_navigation_v1: /лечить в текущей безопасной Обители Хранителя Моря Хаоса
сначала предлагает «Себя», затем видимых присутствующих Хранителя и жителей — даже
без согласия на лечение. Одноимённых обитателей различают только уже известные
роль, описание и Обитель; неразличимые варианты недоступны. Идентификаторы, скрытые
раны, мысли и отпечатки не раскрываются. После выбора показываются известные раны
и доступные прежние способы; цель без допустимого способа получает только оценку.
Выбор цели и отмена не дают согласия, не бросают кость и не расходуют цикл/ресурсы.
Получая частную помощь, игрок выбирает «Себя» как пациента, а затем предложение
помощи. Только отдельное подтверждение ставит прежнее действие в очередь; клиент
заново проверяет цель, рану, местность, поколение, согласие и актуальную UI-lease.
В shared browser API первый пакет содержит ТОЛЬКО wound_target, второй — ТОЛЬКО
wound_selection и confirm_wound_treatment. Ответы другого шага отвергаются.
Это навигация клиента, не новый GM response/pending/receipt и не новое право лечить.
Прежние self/aid3/4/5/6 и духовное боевое лечение сохраняют свои правила; для одиночной
Self-цели/боя остаётся прямой выбор раны. US5 целиком, DOM/provider и fresh-process
этим не квалифицируются. Пример: Examples/E_CLI_Afterlife_Turns.txt, wound_target_navigation_v1.
