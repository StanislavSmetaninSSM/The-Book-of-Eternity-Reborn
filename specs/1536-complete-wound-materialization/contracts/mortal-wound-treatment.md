# Contract: Mortal Wound Diagnosis, Treatment, and Recovery

**Feature**: `1536-complete-wound-materialization`  
**Issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

## Design boundary

Mortal wounds are authored for the current universe. The client validates a closed
mechanical language but does not provide a catalog of wound names, anatomy, medicines,
or complete cures. Antibiotics and bandages can be valid in one setting; healing
crystals and crystalline dust can be valid in another.

## Complete Mortal wound requirement

At creation, every Mortal wound must include:

- explicit physical/systemic/mental/other location profile and readable nature;
- severity I-IV and consequence set within its budget;
- visible/hidden symptoms, prognosis, and complication facts;
- at least one mechanically complete treatment route;
- a natural recovery policy and optional deterioration policy;
- reachable diagnosis evidence for every hidden route.

The client never derives any of these fields from the wound name or narrative prose.

## Treatment routes

All requirements in one route are conjunctive. Separate routes are alternatives.
Mortal route visibility is `public`, `known_to_player`, or `hidden`; a `gm_only` route
is invalid because it has no player-reachable treatment lifecycle. Public/player-known
routes must be present in `knownRouteIds[]`; hidden routes enter that set only after
diagnosis. Known/completed IDs resolve exact routes, and completed routes are known.
Routes may be added later only through an accepted evidence-backed
`author_alternative_treatment` transition. That transition appends exactly one bound
complete route and one history row. A visible route also appends its ID to the known
set; a hidden route instead appends exactly one bound reachable diagnosis path. It
cannot replace, reorder, delete, or reinterpret an existing route/path or rewrite any
prior history row. Its pre-response request binds the active wound, accepted evidence
event, operation key, and before fingerprint; after the GM authors the route/path, the
client re-resolves requirements and seals separate accepted-transition evidence over
the request, complete proposal, and proposed after-image before staging a command.

### Procedure

A procedure declares exact requirements, resource consumption policy, one registered
check formula, one explicit critical policy, one closed client-resolved modifier source,
and ordered outcome bands before the roll is consumed. Version 1 uses
`mortal_wound_procedure_v1` with an accepted d20. The resolver derives
`total = naturalRoll + modifier` and `margin = total - effectiveDifficulty` with checked
64-bit arithmetic; callers never submit dice, modifier, total, margin, selected band, or
result category. `modifierSource` is the closed union `{ "kind": "fixed_zero" }` or
`{ "kind": "resolved_skill_tier", "requirementIndex": N }`. The latter zero-based
index must name one exact satisfied route `skill_tier` requirement; the modifier is that
T060 row's current tier and its resolved owner coordinate is the roll actor. This keeps
both provider expertise and an explicitly authored target-role check legal. For
`fixed_zero`, the provider is the roll actor. No characteristic, display text, or
unrelated skill is an implicit bonus.

`effectiveDifficulty` is the authored non-negative signed-32-bit `difficulty` plus the
sum of every active current complication's integer `treatmentDifficultyModifier`.
That sum and its narrowing are checked; complication modifiers affect procedure checks
only, not courses or guaranteed treatment. `naturalRoll` is exactly 1-20 from the sealed
accepted pool. The accepted active effect snapshot contributes only exact roll-actor-
targeted `roll_modifier` components whose operations contain `skill_check`: at least one
advantage and no disadvantage yields `advantage`, at least one disadvantage and no
advantage yields `disadvantage`, both cancel to `normal`, and no contribution is
`normal`. Stacks and multiple same-direction sources do not escalate the mode; version 1
has no great/dire mode. Normal claims one d20, advantage/disadvantage claim two and
select respectively the higher/lower natural value; equal dice select the lower source
index. An arithmetic or pool-exhaustion failure rejects without accepting an attempt.

The existing Fate Shield mechanic is not bypassed. When and only when the selected
natural die is 1 and the exact roll actor is `player/player_current`, the procedure-check
factory asks a shared production Fate Shield arbiter for the same oldest eligible
carrier (`createdAtTurn`, then `effectId`) and eligibility rules used by the existing GM
`EffectAcceptedEventReportCatalog.Compose` path. It stores a nullable prepared reaction
candidate with exact `EffectId`, `TriggerId`, accepted carrier/effect fingerprint, and
`PreparedReactionFingerprint` in the sealed check authority. Selector logic is extracted
and reused; it is not copied, and the legacy GM five-argument Compose API plus its
leading-dice evidence contract remain unchanged.

Preparation is batch-safe. The same registry reconstructs Fate Shield candidates already
held by full sealed procedure requests in typed wound commands, pending packets, and
accepted history for the binding, excludes those effect IDs, and provisionally reserves
the next oldest eligible shield in deterministic accepted-transition order. Exact retry
gets the same candidate; changed request conflicts. Successful request persistence
confirms the reservation and failed sealing/persistence releases it. Two natural-1
procedures therefore cannot seal the same shield, including across restart.

At resolution the exact client-owned seam is
`EffectAcceptedEventReportCatalog.ResolvePreparedMortalWoundCriticalReaction(
MortalWoundTreatmentAttemptRequest request,
MortalWoundTreatmentAcceptedStateAuthority acceptedState)`. It returns
`MortalWoundCriticalReactionResolutionResult` with exactly `IsValid`, frozen `Issues`,
and nullable immutable `Intent`. The existing GM `Compose(JsonNode?, int, string,
IReadOnlyList<int>, EffectCarrierCatalogInput)` overload remains byte/API compatible.
Both paths delegate eligibility and oldest-carrier selection to one extracted
`FateShieldReactionArbiter`; the treatment path never fabricates a GM report or leading-
dice array.

The accepted-input coordinator rejects a response that contains both a typed procedure-
treatment request and a legacy GM `owner_critical_failure` report for the same accepted
turn before either path confirms a Fate claim. The exact issue code is
`wound_treatment_fate_reaction_cross_surface_duplicate`; rejection releases every
provisional die, Fate, and resource claim. The legacy report path remains legal for
non-treatment Mortal actions, while multiple typed treatment requests are coordinated
by the shared reservation registry.

The adapter accepts only a sealed procedure request and its matching lease-bound accepted
state, recomputes the prepared candidate, and returns no carrier mutation. Its
`MortalWoundCriticalReactionIntent` contains exactly `EventType=owner_critical_failure`,
`EventRef`, `CausalEventRef`, `Turn`, `Realm=mortal_world`, `TargetKind=player`,
`TargetId=player_current`, `EffectId`, `TriggerId`, `AcceptedEffectFingerprint`,
`PreparedReactionFingerprint`, `RequestFingerprint`, and `IntentFingerprint`.
`EventRef` and `CausalEventRef` are produced by one versioned client writer from the
accepted session generation/request/snapshot/turn plus the exact treatment `AttemptId`
and event role; they are unique per attempt and never reuse the generic GM
`turn_N:mortal_action_roll:1` coordinate. Every field participates in the intent seal.
Natural 1 without a candidate remains `critical_failure` and selects the last band.
Natural 1 with a valid candidate becomes exact `critical_failure -> failure` and selects
the first authored `failed_attempt` band. The shield is consumed even when that first
failed band is also the last, because the semantic event transformation still occurred.
NPC roll actors never trigger the player shield. T070 either commits its consume/expire
intent atomically with the wound/result/history plan or writes nothing; a stale candidate
rejects the complete publication.

Procedure mode evidence uses one closed mapping: selected 20 is
`critical_success -> critical_success`; selected 2-19 is `ordinary -> ordinary`;
selected 1 without shield is `critical_failure -> critical_failure`; selected 1 with
the prepared shield is `critical_failure -> failure`. Only the final case has an
all-present reaction effect ID/trigger ID/reaction fingerprint triple. That fingerprint
is the resolved typed lifecycle intent seal, not the prepared candidate seal. Persisted
selected band ID/index must equal the outer resolution fields.

Every outcome row has exactly `bandId`, nullable inclusive `minimumMargin` and
`maximumMargin`, `category`, and ordered `result`. There are 2-16 rows in authored
best-to-worst order. Together they form one gapless non-overlapping partition of every
integer margin: the first upper bound is null, the last lower bound is null, adjacent
bounds touch exactly, and every intermediate row has both bounds. Categories are
monotone `success` then optional `partial_success` then `failed_attempt`; the first row
is `success` and the last is `failed_attempt`. Multiple adjacent rows may share a
category, but category is never inferred from result operations. Band IDs are exact and
case/Unicode-confusable unique within the route; selected outcome indices are zero-based.
Structurally, every `success` row must contain a positive operation kind (`stabilize`,
`add_recovery`, `reduce_severity`, `remove_complication`, or `heal`) and may contain no
`no_improvement`, `add_complication`, or `apply_deterioration`. Before every fresh
procedure attempt, the planner independently simulates every row against the sealed
current wound and requires every row to be a legal complete transition. Each `success`
row additionally requires at least one actual monotone improvement. This includes the
natural-20 first row, prevents route completion from being earned by a categorized
no-op, and prevents an inapplicable partial/failed row from becoming a free reroll. The
persisted route remains structurally valid when the wound changes, but a fresh attempt
cannot start until all of its possible declared rows are currently applicable.

Example shape:

```json
{
  "routeId": "clean_and_suture",
  "displayName": "Очистить и ушить рану",
  "visibility": "known_to_player",
  "mode": "procedure",
  "requirements": [
    { "kind": "item_quantity", "itemRef": "sterile_thread", "quantity": 1, "ownerRole": "provider" },
    { "kind": "item_quantity", "itemRef": "antiseptic", "quantity": 1, "ownerRole": "provider" },
    { "kind": "skill_tier", "capabilityRef": "field_medicine", "minimumTier": 2, "actorRole": "provider" },
    { "kind": "facility", "facilityRef": "clean_work_surface" }
  ],
  "resourcePolicy": {
    "reserveBeforeResolution": true,
    "consumeOn": ["success", "partial_success", "failed_attempt"],
    "refundOn": ["cancelled", "validation_failed", "rolled_back"],
    "mutations": [
      { "kind": "consume_requirement", "scope": "common", "milestoneOrdinal": null, "requirementIndex": 0 },
      { "kind": "consume_requirement", "scope": "common", "milestoneOrdinal": null, "requirementIndex": 1 }
    ]
  },
  "resolution": {
    "formulaKey": "mortal_wound_procedure_v1",
    "difficulty": 15,
    "rollSource": "accepted_d20",
    "criticalPolicy": "natural_20_first_natural_1_last",
    "modifierSource": {
      "kind": "resolved_skill_tier",
      "requirementIndex": 2
    }
  },
  "outcomes": [
    {
      "bandId": "clean_success",
      "minimumMargin": 5,
      "maximumMargin": null,
      "category": "success",
      "result": [
        { "kind": "stabilize" },
        { "kind": "reduce_severity", "steps": 1 }
      ]
    },
    {
      "bandId": "clean_partial",
      "minimumMargin": 0,
      "maximumMargin": 4,
      "category": "partial_success",
      "result": [
        { "kind": "stabilize" },
        { "kind": "add_recovery", "points": 1 }
      ]
    },
    {
      "bandId": "clean_no_improvement",
      "minimumMargin": -4,
      "maximumMargin": -1,
      "category": "failed_attempt",
      "result": [{ "kind": "no_improvement" }]
    },
    {
      "bandId": "clean_complication",
      "minimumMargin": null,
      "maximumMargin": -5,
      "category": "failed_attempt",
      "result": [
        {
          "kind": "add_complication",
          "complicationDraft": {
            "complications": [
              {
                "complicationRef": "irritation",
                "kind": "pain",
                "state": "active",
                "displayName": "Раздражённые края раны",
                "treatmentDifficultyModifier": 1,
                "visibility": "known_to_player"
              }
            ],
            "consequenceDefinitions": [
              {
                "definitionRef": "irritation_grip_limit",
                "definition": {
                  "schemaVersion": 1,
                  "definitionKey": "irritation-grip-limit",
                  "display": {
                    "name": "Болезненный хват",
                    "description": "Раздражённые края раны мешают крепко удерживать предметы.",
                    "category": "debuff",
                    "visibility": "visible"
                  },
                  "allowedRealms": ["mortal_world"],
                  "allowedTargetKinds": ["player"],
                  "components": [
                    {
                      "componentId": "irritation_grip_control",
                      "profile": "action_control",
                      "priority": 100,
                      "payload": { "action": "use_item", "operation": "restrict" }
                    }
                  ],
                  "parameterBounds": {},
                  "stacking": {
                    "stackKey": "irritation-grip-limit",
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
                    "dispelCategories": [],
                    "cureKinds": ["wound_treatment"],
                    "onSourceLoss": "expire",
                    "onConditionLoss": null,
                    "manualAuthorities": []
                  },
                  "links": []
                },
                "root": {
                  "ownership": {
                    "kind": "complication",
                    "complicationRef": "irritation"
                  },
                  "slots": [
                    {
                      "profileKey": "action_control",
                      "readableSummary": "Боль мешает крепко удерживать предметы."
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
```

Outcome results are arrays of closed objects, never colon-encoded strings. The complete
version-1 union is `no_improvement`, `stabilize`, positive bounded `add_recovery`,
`reduce_severity` by one/two, exact existing `remove_complication`, complete
`add_complication`, exact current-policy `apply_deterioration`, and `heal` with complete
bounded legacy drafts. `no_improvement` must be the sole operation. A course milestone
may use an empty result only while `completion=active`, because milestone advancement is
itself its declared effect. The final milestone and every other accepted outcome are
non-empty, the complete course has at least one applicable positive treatment operation,
and every result is bounded to eight operations. Every result contains at most one
`heal`, and that `heal` must be its final operation; the combined legacy limit is
therefore aggregate over the selected result and no operation can execute after the
terminal follow-up transition.

All ordered recovery-point accumulation and application to signed-64-bit canonical
progress uses checked arithmetic. Capability aggregate comparison widens signed-32-bit
values and limits to checked signed 64-bit before summing. Overflow rejects before die
consumption or publication. Aggregate severity reduction is at most two without a
`heal`; a result ending in its one legal `heal` may aggregate at most three steps only
to reach working severity I before the separate follow-up heal.

Canonical persisted routes use exactly
`{ "kind": "remove_complication", "complicationId": "..." }`. GM-authored route
proposals instead use exactly `complicationRef`: in an initial wound it must resolve to
one same-proposal complication ref; in later alternative authoring it must copy one
bounded opaque existing-complication selector supplied by the client packet.
`WoundResponseInputComposer` alone resolves/allocates and rewrites that ref to the exact
canonical ID before contract parsing. Canonical input rejects `complicationRef`, proposal
input rejects `complicationId`, and unresolved/duplicate/cross-wound/confusable refs
reject the complete proposal.

“Positive” is closed to an applicable state-changing `stabilize`, `add_recovery`,
`reduce_severity`, `remove_complication`, or `heal`. `no_improvement`,
`add_complication`, and `apply_deterioration` never count. Course completeness evaluates
all milestone operations in ordinal order against a detached copy of the starting
working wound; at least one must be positive when reached, and each actual milestone is
still revalidated against current canonical state.

`add_complication.complicationDraft` reuses the existing GM-safe response-local wound
sub-proposal exactly: it has only `complications` and `consequenceDefinitions`, the first
contains exactly one current `ComplicationProposal` row, and every non-null root in the
second uses the current `definitionRef/definition/root/ownership/slots` shape and is
owned by that one `complicationRef`. Definitions carry exact empty `links`; the existing
wound proposal adapter derives local application/operation refs, permanent IDs, wound
source links, slot ordinals, and fingerprints. An empty `consequenceDefinitions` array
declares a non-effectful complication; an effectful one, as above, must carry the complete
graph and pass ordinary severity/slot limits. No parallel `ownedEffectDraft`, accepted
root application, result-map, or caller fingerprint shape exists.
The planner allocates permanent complication and legacy IDs, plus response-local effect
application references, only after this branch is selected. It never allocates a
permanent effect ID; the accepted #1535 effect planner alone does so.
`heal.legacies` is the exact closed union `cosmetic|mechanical_effect`.
A cosmetic row contains exactly `localLegacyRef`, `kind`, and `readableSummary`. A
mechanical row adds exactly `effectDraft={schemaVersion,definitions,applications}`.
Each array contains 1-5 rows, reusing the current wound source definition/root bounds.
A definition wrapper contains only response-local
`definitionRef` plus a complete #1535 `definition` with `links=[]`; an application
contains only response-local `applicationRef`, same-draft `definitionRef`, and complete
closed `parameters`. All three refs/keys are exact/confusable unique, external definition
references are forbidden, and target/source/event/ordinals/carrier state/fingerprints/IDs
are derived.
There is no open entity kind or skill/trait/other branch: arbitrary mechanics use the
complete #1535 component language.

One `heal` contains at most eight total legacy rows. Every `localLegacyRef` is an exact
identifier and exact/Unicode-confusable unique across that array. Permanent capability
limits separately bound cosmetic and mechanical-effect counts, but both are aggregate
over the whole result and their sum can never exceed eight.

Nested mechanical-legacy refs are local to their legacy draft. The client namespaces
each technical definition/application ref as `localLegacyRef/localRef`; different legacy
drafts may reuse a local spelling, but their namespaced refs, derived permanent IDs, and
the persisted application result map must remain collision-free against the working
wound/source graph.

Across every `add_complication` in one selected result, the single proposal
`complicationRef` is exact/Unicode-confusable unique. The adapter namespaces all nested
definition/application/derived operation refs by selected operation ordinal plus that
complication ref before the #1535 handoff and rejects collisions with sibling operations
or the working wound's source graph. Local spellings may repeat across drafts only
because their namespaced forms are distinct.

The exact immutable T067 -> T070 handoff is a closed
`MortalWoundTreatmentOutcomeIntent` union. Every branch has exactly zero-based
`OperationOrdinal`, `Kind`, `DeclaredOperationFingerprint`, and `IntentFingerprint`.
`no_improvement|stabilize` add nothing; `add_recovery` adds `Points`;
`reduce_severity` adds `Steps`; `remove_complication` adds canonical `ComplicationId`;
`apply_deterioration` adds `PolicyRef` plus T069 authority fingerprint;
`add_complication` adds authored ref, deterministic complication ID, ordered local-to-
namespaced definition/application bindings, and preparation fingerprint; `heal` adds
its derived child coordinates, preparation fingerprint, and ordered legacy seeds. Each
seed has ordinal, local ref, deterministic legacy ID, kind, local-to-namespaced ref maps,
declared-legacy fingerprint, and seed fingerprint.

The intent array has exactly one row per typed `DeclaredResult` operation in identical
order, including `no_improvement`. Every authored field and operation fingerprint is
recomputed; only deterministic IDs/ref maps are additional authority. T070 may build
ordinary wound/#1535 batches from this typed pair but cannot parse raw route JSON or
allocate/rename/reinterpret IDs. Any cardinality, order, kind, value, binding, collision,
or fingerprint disagreement rejects before composition.

Complication and legacy IDs derive from the sealed request, operation/legacy ordinal,
and local ref. A mechanical
legacy's `legacyId` is its source ID under client-only, non-public,
non-GM-materializable `sourceKind=wound_legacy`. Same-turn authority is the sealed legacy
seed plus immutable preparation; after finalization its complete reload authority
persists in a typed legacy history row linked to the terminal wound. The accepted #1535 planner alone
allocates every effect ID. The row persists the ordered exact
`applicationRef -> effectId/materializationFingerprint` result map and effect-plan seal,
so a multi-root source is reconstructable after restart. Active-wound healing never captures this independent source;
later effect removal cannot delete its history/provenance. Legacies materialize only in
the separate terminal heal composition. An incomplete/open payload invalidates the route
before a die is consumed.

`MortalWoundHealLegacyPlanner.Prepare(binding, resolution, workingWound)` returns exactly
`MortalWoundHealLegacyPreparationResult(IsValid, Issues, Preparation)`. Its immutable
preparation contains only `LegacyDraftBindings`, frozen ordered
`EffectOperationBatches`, and `PreparationFingerprint`; it contains no effect ID or
durable history intent. `EffectOperationBatches` has exactly one immutable
`WoundEffectOperationBatch` per selected `mechanical_effect` legacy, in ascending legacy
ordinal, and no batch for a cosmetic legacy. Each batch exports exactly one independent
`sourceKind=wound_legacy` source whose source ID equals that legacy seed's deterministic
`LegacyId`; definitions or applications from two legacy sources may never share one
batch. Its namespaced application refs and source/preparation seals agree exactly with
the corresponding seed and draft binding. Zero mechanical legacies therefore produce
an exact empty batch array. The preparation fingerprint binds the complete ordered
bindings and batch array. After #1535 planning,
`MortalWoundHealLegacyPlanner.Finalize(preparation, acceptedEffectPlan)` returns exactly
`MortalWoundHealLegacyFinalizationResult(IsValid, Issues, Finalization)`. Finalization
contains resolved immutable `LegacyBindings`, durable typed `HistoryIntents`, the exact
ordered per-legacy namespaced application result maps, effect-plan fingerprint, and
finalization fingerprint. It recomputes the preparation, every batch/source boundary,
the effect plan, and every `applicationRef -> effectId/materializationFingerprint`
agreement before atomic publication. Missing, extra, reordered, merged, or split legacy
batches reject the whole plan.

The finalization's exact `ApplicationResults` value is a frozen ordered array of
`MortalWoundHealLegacyApplicationResultGroup`, one per prepared mechanical batch in the
same order. A group contains exactly `LegacyOrdinal`, `LegacyId`,
`SourceExportFingerprint`, frozen ordered non-empty `Results` of the existing
`EffectAcceptedApplicationResult` type, and `ResultGroupFingerprint`. Its result order
equals that batch's root-application order. Cosmetic legacies produce no group. The
group fingerprint binds every complete accepted result, not merely effect IDs.

The request owns the primary `treat` coordinates. The follow-up heal derives its child
operation key/transition/event/causal refs from the sealed request plus fixed role
`heal`; each legacy row derives distinct coordinates from that request plus role
`legacy`, zero-based ordinal, and exact local legacy ref. Collision with existing or
sibling operation/event/history coordinates rejects the whole plan. Exact replay exits
before planning and regenerates none of these rows or coordinates.

References above are illustrative semantic refs. They must resolve through exact item,
capability, actor, and location authority, not through display strings. All hard
requirements, route/wound identity, before-state, and roll seals are checked before
critical semantics. Natural 20 then selects the first row; natural 1 first establishes
the last-row `critical_failure`, after which only the prepared shared Fate Shield
transformation may remap final selection to the first `failed_attempt` row as ordinary
`failure`. Rolls 2-19 select the row containing the computed margin. A critical never
bypasses a missing requirement and never creates an undeclared result.

### Course

A course uses the same closed route envelope. It declares ordered doses/time
milestones in `outcomes`, exact common and per-milestone requirements, one game-time
clock, a maximum late window, interruption behavior, and a final transition. Starting
the first milestone creates one client course identity. Each accepted milestone has a
unique attempt ID, operation key, and `(courseId, ordinal)` coordinate and cannot be
consumed twice.

```json
{
  "routeId": "antibiotic_course",
  "displayName": "Пройти курс антибиотика",
  "visibility": "known_to_player",
  "mode": "course",
  "requirements": [
    { "kind": "provider", "providerRef": "field_medic_01" }
  ],
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
  "resolution": {
    "clockKind": "world_time.currentTimeInMinutes",
    "maximumGapMinutes": 600
  },
  "outcomes": [
    {
      "ordinal": 1,
      "afterMinutes": 0,
      "requirements": [
        { "kind": "item_quantity", "itemRef": "antibiotic_dose", "quantity": 1, "ownerRole": "target" }
      ],
      "category": "success",
      "completion": "active",
      "result": []
    },
    {
      "ordinal": 2,
      "afterMinutes": 480,
      "requirements": [
        { "kind": "item_quantity", "itemRef": "antibiotic_dose", "quantity": 1, "ownerRole": "target" }
      ],
      "category": "success",
      "completion": "active",
      "result": []
    },
    {
      "ordinal": 3,
      "afterMinutes": 960,
      "requirements": [
        { "kind": "item_quantity", "itemRef": "antibiotic_dose", "quantity": 1, "ownerRole": "target" }
      ],
      "category": "success",
      "completion": "completed",
      "result": [{ "kind": "heal", "legacies": [] }]
    }
  ],
  "interruption": {
    "category": "failed_attempt",
    "result": [{ "kind": "no_improvement" }]
  }
}
```

Ordinal 1 must have `afterMinutes=0`; ordinals are contiguous from 1; `afterMinutes`
strictly increases; every milestone has `category=success`; only the last row has
`completion=completed`. A non-empty milestone result may contain only monotone positive
operation kinds; `no_improvement`, `add_complication`, and `apply_deterioration` are
forbidden in every milestone result. Interruption is the sole adverse course branch. For milestone `n`,
`due = courseStartedAt + afterMinutes` and
`deadline = due + maximumGapMinutes`, using checked canonical-minute arithmetic. Both bounds
are inclusive. `due - 1` is a nonterminal validation rejection; `due` through
`deadline` accepts the milestone; `deadline + 1` accepts exactly the declared
interruption, with an on-deadline milestone taking precedence. Elapsed time is exact
game-world authority from `world_time.currentTimeInMinutes`, never wall-clock time.
Course start, current game time, `afterMinutes`, and `maximumGapMinutes` are
non-negative signed 64-bit integers; any
addition overflow rejects without accepting or interrupting an attempt.

Ordinal 1 is legal only when `before.care.activeCourseId` is null. Its complete
`MortalWoundCourseModeAuthority` derives the deterministic course ID and stores an
immutable `CourseStartAuthority` containing route identity/fingerprint, complete typed
`StartingWound` plus its fingerprint, start minute, and accepted-state/coordinate seals.
At this boundary the planner simulates the complete milestone sequence against that
detached starting wound and requires at least one actual monotone improvement; the route
parser itself performs only closed-shape/positive-kind checks. Continuation restores and
recomputes this authority from unique contiguous history and requires the current
`activeCourseId`, route, course, and next ordinal to agree. A second course cannot start
while the pointer is non-null.

A procedure or guaranteed route may legally modify the wound during an active course;
each later milestone still revalidates its selected operations against the current wound
while preserving the original course-start completeness proof. A terminal heal clears
the pointer and makes later milestones reject. Final completion or interruption also
clears it.

Replay/conflict classification runs first. For a new operation the planner validates
typed coordinates/history/clock integrity, rejects too-early work, and only then invokes
the separate current-milestone requirement authority. It returns `Satisfied`,
`Unsatisfied`, or `InvalidAuthority`: exact current canonical absence/consumption,
unavailability, inactive capability, withdrawn consent, or lost co-presence is
`Unsatisfied`; malformed, ambiguous/confusable/cross-realm, changed sealed predicate/
coordinate/history, or fingerprint evidence is `InvalidAuthority`. At course start any
non-satisfied result rejects without creating a course. Once active, `Unsatisfied` or a
missed deadline applies only the declared interruption result and clears the course;
`InvalidAuthority` still rejects without fabricating interruption. Interruption consumes
neither the unmet milestone nor any future milestone. Intermediate accepted milestones advance course state even when
`result=[]`; they do not implicitly add recovery or reduce severity.

Interruption is exactly category `failed_attempt`. Its result is either sole
`no_improvement` or only complete harmful `add_complication`/`apply_deterioration`
operations. It cannot stabilize, add recovery, reduce severity, remove a complication,
heal, or add a legacy; deliberately losing a requirement cannot become free treatment.
For interruption specifically, `add_complication` must have exact empty
`consequenceDefinitions` and its sole complication must have strictly positive
`treatmentDifficultyModifier` 1-4. An effectful or zero-difficulty complication is not
provably adverse and is rejected. `apply_deterioration` must reference a T062 policy
whose typed transition is strictly worsening; no neutral/beneficial policy qualifies.

Every accepted course result stores route fingerprint, course ID, start and resolved
game-time coordinates, clock-evidence fingerprint, milestone ordinal, course
disposition (`active|completed|interrupted`), ordered declared result, and result
fingerprint in typed durable history. This reconstructs the course after restart; the
wound's `activeCourseId` is only an agreeing current pointer. Repeating the same
operation with identical semantics returns the original receipt. Reusing an attempt ID
or `(courseId, ordinal)` under another operation, skipping/reordering a milestone, or
continuing a completed/interrupted course conflicts.

### Guaranteed route

A guaranteed route also uses the closed common envelope. It has no ad hoc GM success
flag. Its resolution binds one exact actor role and one exact already materialized
capability; the requirements contain the matching `source_capability` row; and its one
outcome declares the deterministic result and `success` category.

```json
{
  "routeId": "exact_healing_source",
  "displayName": "Применить закреплённый источник исцеления",
  "visibility": "known_to_player",
  "mode": "guaranteed",
  "requirements": [
    {
      "kind": "source_capability",
      "capabilityRef": "exact-materialized-healing-source",
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
    "capabilityRef": "exact-materialized-healing-source",
    "actorRole": "provider"
  },
  "outcomes": [
    {
      "category": "success",
      "result": [
        { "kind": "remove_complication", "complicationId": "infection_01" },
        { "kind": "heal", "legacies": [] }
      ]
    }
  ],
  "interruption": null
}
```

The guarantee does not live in the transient requirement snapshot. It is an optional
extension on the exact current canonical player/NPC active or passive skill row:

```json
{
  "skillId": "skill_field_medicine_01",
  "mortalWoundTreatmentCapabilities": [
    {
      "schemaVersion": 1,
      "capabilityRef": "exact-materialized-healing-source",
      "woundDomain": "physical",
      "minimumSeverityRank": 1,
      "maximumSeverityRank": 1,
      "operationLimits": {
        "mayStabilize": true,
        "maximumRecoveryPoints": 0,
        "maximumSeverityReductionSteps": 0,
        "removableComplicationKinds": ["infection"],
        "mayHealAtSeverityI": true,
        "maximumCosmeticHealLegacies": 0,
        "maximumMechanicalEffectHealLegacies": 0
      }
    }
  ]
}
```

The snippet omits the containing skill's ordinary required fields. A skill with this
extension must have one exact permanent `skillId`; an idless legacy skill can satisfy an
ordinary T060 capability requirement but cannot prove a guarantee. Each extension-
bearing row's ID must be exact/confusable unique against every current active/passive
skill row of that actor, including a row without the extension; duplicate or confusable
skill identities invalidate provenance. Capability refs are exact/confusable unique
across both current skill kinds of the actor. The lease-bound
`woundDomain` field is exactly `physical` in version 1; `spiritual`, unknown, or
caller-extensible values invalidate the extension. The lease-bound
accepted-state export factory creates an immutable proof binding snapshot, exact
context/coordinates/accepted-state seals, owner, skill kind/ID, source semantic
fingerprint, capability limits, and proof fingerprint. A mastery gate,
when needed, is an ordinary sibling `skill_tier` requirement resolved by unchanged T060.
A shape-valid JSON fixture or the unchanged T060 capability row cannot create that
proof.

That same current extension-bearing skill row is the sole canonical source for the
matching ordinary `source_capability` requirement. The T066 snapshot composer projects
from it one unchanged T060 `actors[].capabilities[]` row containing only
`capabilityRef`, diagnostic skill `displayName`, current `lifecycle`, and current
`active`; it never projects guarantee limits or proof seals. Missing, duplicate, idless,
inactive, or ambiguous skill sources yield no authoritative row, and a caller cannot
pair an unrelated transient capability with the canonical guarantee proof.

The exact capability/role must agree across the route resolution, successful T060
requirement authority, selected actor, and separate canonical proof. The proof must be
current, cover the wound domain/severity, and bound every ordered
route operation: the example may remove `infection_01` only because that complication's
canonical kind is `infection`, then heal only at rank I with no undeclared legacy.
The singleton result must contain at least one applicable positive operation;
`no_improvement`, `add_complication`, and `apply_deterioration` are never guaranteed
success. The capability limits likewise cannot all be zero/false/empty.
Before every fresh guaranteed attempt, the planner simulates that singleton against the
sealed current wound and requires at least one legal actual monotone improvement; the
structural parser does not freeze applicability to an obsolete creation-time state.
Recovery points, severity-reduction steps, and both legacy counts are aggregate limits
over the complete ordered singleton result, not per-operation allowances. Repeating an
operation cannot multiply the guarantee; removals must match the declared complication
kind set and the common eight-operation bound, and at most one `heal` is legal.
Ordinary active capability presence is not a guarantee. The source may still require
items, charges, a provider, facility, consent, or elapsed time. Any failed sibling
requirement or stale/missing skill proof rejects before an attempt is accepted. Issue
#1533 must retain this optional wound capability when it later completes the general
Mortal skill lifecycle.

The guaranteed request seals the initially selected canonical proof. For a new attempt,
the planner requires a fresh current export with the same proof/source fingerprint, and
the publication composer repeats that comparison against the final composed skill state,
not merely the pre-plan root. Its exact seam is
`MortalWoundTreatmentCapabilityAuthority.ExportForPublication(acceptedState,
coordinates, capabilityRef, actorRole, AcceptedMechanicsPlan publicationPlan)`. If the
plan touches the selected player's active/passive skill root or the selected NPC skill
root, the exporter validates and reads that root's final after-image; otherwise it reads
the lease-bound current root. It accepts no detached JSON. The resulting proof must equal
the sealed proof and its source semantic fingerprint exactly; a same-turn removal,
retirement, ID/capability change, or limit change rejects and rolls back the whole plan.
Exact replay returns before these live gates and may supply no current proof; it cannot
publish again.

Capability export diagnostics use one closed, stable boundary vocabulary.  The
`FilePath` values below name typed treatment coordinates rather than an incidental JSON
array index, so the same proof failure is reported identically for player/NPC and
active/passive skill roots:

| Failure | Exact code | Exact `FilePath` |
| --- | --- | --- |
| a combatant/member target has no canonical player/NPC promotion for a target-owned source | `mortal_wound_treatment_capability_actor_promotion_required` | `treatmentCapability.target` |
| route actor role disagrees with the selected proof owner | `mortal_wound_treatment_capability_binding_mismatch` | `treatmentCapability.actorRole` |
| canonical source owner disagrees with the sealed actor binding | `mortal_wound_treatment_capability_binding_mismatch` | `treatmentCapability.sourceOwner` |
| selected current/final skill source is absent | `mortal_wound_treatment_capability_source_missing` | `treatmentCapability.source` |
| selected current/final source is inactive or retired | `mortal_wound_treatment_capability_source_inactive` | `treatmentCapability.source.lifecycle` |
| selected current/final skill identity is exact-duplicate or Unicode-confusable | `mortal_wound_treatment_capability_source_ambiguous` | `treatmentCapability.source.skillId` |
| final source ID, capability, domain, limits, or semantic fingerprint differs from the sealed proof | `mortal_wound_treatment_capability_publication_mismatch` | `treatmentCapability.publicationPlan` |

The exporter returns the scenario-specific row above; it must not replace it with a
generic invalid-authority issue from an unrelated earlier boundary.

`heal` never skips the canonical severity-I gate. Ordered outcome primitives are
evaluated against a working state; a route for a higher severity must first declare
enough legal reductions to reach I, after which `heal` requests the separate canonical
follow-up transition. The example therefore applies only to a severity-I wound.

## Resolution, attempts, and consumption

No raw JSON, die array, numeric modifier, clock value, skill root, caller binding, or
caller fingerprint is an accepted new-attempt input. The production integration first
parses only source-shaped selection JSON through `MortalWoundTreatmentAuthority.ParseContext`
and then calls `MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(
FileSystemManager, CanonicalWriteLease, MortalWoundTreatmentAuthority.Context, string woundId)`.
Under the active canonical lease this exporter revalidates that selection against the
live turn and internally derives the binding/event set before it strictly composes the unchanged
T060 context/snapshot, reads canonical world time, current player/NPC skill sources, and
the accepted combat-actor treatment projection,
loads the accepted effect snapshot, and strictly reads the complete current wound
carriers, wound identity index, and wound history. It selects the exact `woundId` once
and binds its owner/fingerprint plus the complete history fingerprint. It also binds the canonical root identity, session
generation, and revision as live admission state. It returns
`MortalWoundTreatmentAcceptedStateAuthorityResult` with exactly `IsValid`, frozen
`Issues`, and nullable immutable `Authority`. The authority exposes typed coordinates
and production fingerprints, never source JSON or a mutable dice collection; no public
constructor/parser creates it from a fixture.

Its durable `ContextFingerprint` binds the exact realm, provider kind/ID, target kind/ID,
location ID, stable session generation, and accepted session/request/snapshot/turn.
`AcceptedStateFingerprint` additionally binds the stable strict semantic file
hashes/projections used for
turn dice, world minute, T060 requirements, current capability skill rows, and active
effect mechanics, plus current wound carriers/index/history. Neither fingerprint serializes an absolute root path, process-local
root identity, or live revision. Those remain non-serialized registry/lease admission
guards, so relocating or restarting the same valid game cannot poison durable replay.
Test fixtures must enter through a temporary `FileSystemManager` and the same
registry/lease path; a shape-valid detached object grants no authority.

`MortalWoundTreatmentPlanner.CreateAttemptCoordinates(
MortalWoundTreatmentAcceptedStateAuthority acceptedState,
WoundMaterializationEnvelope before, string operationKey, string routeId,
string eventRef)` is used only for a new operation. It derives the accepted binding's
session/request/snapshot/turn, the exact context coordinates, wound identity,
expected-before fingerprint, and deterministic attempt ID. The target coordinate must
equal the wound owner and the realm must be `mortal_world`; callers cannot submit a
detached route, context, attempt ID, or result seal. The supplied `before` must equal the
accepted state's selected current wound byte-semantically and by recomputed fingerprint.
`eventRef` must resolve exactly once in the accepted binding's typed event set; its kind,
authority ID, and semantic fingerprint are copied into the coordinates and all participate
in the seal. `operationKey` must be an exact identifier unique across the reconstructed
typed command/pending/history attempt set.
For every new resolution, the supplied valid `WoundHistoryParseResult` must likewise
equal the accepted state's complete canonical history fingerprint. It returns
`MortalWoundTreatmentAttemptCoordinatesResult` with exactly `IsValid`, frozen `Issues`,
and nullable `Coordinates`.

After route and hard-gate validation, procedure mode calls
`MortalWoundProcedureCheckAuthority.Create(coordinates, route, before,
requirementAuthority, acceptedState)`. It derives the modifier and exact roll actor from
the route's closed `modifierSource`, derives roll mode from the accepted effect snapshot,
adds every active complication difficulty modifier, and provisionally claims dice from
a generation-scoped production registry. The registry first reconstructs occupied
indices from every strictly valid full sealed procedure request already present in typed
wound commands, pending packets, and accepted history for the same
session/request/snapshot, in deterministic accepted-transition order. Claims are keyed
by operation/coordinate seal, allocate the lowest contiguous still-free one/two-index
span, return the same claim for an exact retry, and conflict on changed coordinate or
roll mode. A claim is made only after all pre-roll hard gates pass. Successful request
sealing and persistence confirm it under the same canonical lease; rejected sealing or
failed persistence releases the provisional claim and cannot advance the durable cursor.
Restart rebuild therefore cannot issue an index already held by an unaccepted pending
request. Exact history replay probes before registry construction or claiming.

The immutable `MortalWoundProcedureCheckAuthority` contains exactly `SourcePath`,
`RollMode=normal|advantage|disadvantage`, `RollActorKind`, `RollActorId`, frozen ordered
`RollContributions` rows of exact `EffectId`, `ComponentId`, and
`Contribution=advantage|disadvantage`, frozen `SourceIndices`, frozen `SourceRolls`,
`SelectedSourceIndex`, `NaturalRoll`, `Modifier`, `ComplicationDifficultyModifier`,
`EffectiveDifficulty`, `RequirementAuthorityFingerprint`, `CoordinatesFingerprint`,
`AcceptedStateFingerprint`, nullable immutable `PreparedCriticalReaction`, and
`AuthorityFingerprint`. The prepared reaction contains exactly `EffectId`, `TriggerId`,
`AcceptedEffectFingerprint`, and `PreparedReactionFingerprint`. The authority has no caller total, margin,
band, category, or outcome.

The client next calls exactly one of
`SealProcedureRequest(MortalWoundTreatmentAttemptCoordinates,
MortalWoundProcedureCheckAuthority, MortalWoundTreatmentRequirementAuthorityBundle,
MortalWoundTreatmentResourceReservationAuthority)`,
`SealCourseMilestoneRequest(MortalWoundTreatmentAttemptCoordinates, int,
MortalWoundCourseModeAuthority, MortalWoundTreatmentRequirementAuthorityBundle,
MortalWoundTreatmentResourceReservationAuthority)`, or
`SealGuaranteedRequest(MortalWoundTreatmentAttemptCoordinates,
MortalWoundTreatmentCapabilityProof, MortalWoundTreatmentRequirementAuthorityBundle,
MortalWoundTreatmentResourceReservationAuthority)`.
Each returns
`MortalWoundTreatmentAttemptRequestResult` with exactly `IsValid`, frozen `Issues`, and
nullable immutable `Request`. This closed request binds its complete context/accepted-
state coordinates, mode authority, complete requirement authority, pre-resolution
resource authority, and one production
`RequestFingerprint`. Its exact properties are `Mode`, `Coordinates`, nullable
`MilestoneOrdinal`, immutable `ModeAuthority`, immutable full `RequirementAuthority`,
immutable `ResourceAuthority`, and `RequestFingerprint`; mode authority is respectively
procedure-check, complete course-mode, or capability proof. Repair or
retry restores the same request instead of recomputing coordinates from an after-state.

Tests and production reach those low-level sealers only through
`MortalWoundTreatmentPlanner.PrepareProcedureRequest(acceptedState, history, before,
operationKey, routeId, eventRef)`, `PrepareCourseMilestoneRequest(...)`, and
`PrepareGuaranteedRequest(...)`, each with that exact six-argument surface and returning
`MortalWoundTreatmentAttemptRequestResult`. They select the route and create coordinates
themselves. Procedure/guaranteed then create the full requirement bundle and matching
mode authority before resource preparation. Procedure performs its production-owned
all-band current-applicability simulation after the bundle is satisfied and before
`MortalWoundProcedureCheckAuthority.Create` may reserve any die or Fate candidate.
Course creates game-time and complete course-mode authority first, then its milestone-
bound bundle and resources. Any failed stage releases all provisional claims. Callers
cannot inject requirements, witnesses, dice, a course baseline, reservation, or
fingerprint.

The internal bundle entry points are exactly
`MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(acceptedState,
coordinates, before)`, `CreateForGuaranteed(acceptedState, coordinates, before)`, and
`CreateForCourseMilestone(acceptedState, coordinates, before, history,
MortalWoundCourseModeAuthority)`. The first two return
`MortalWoundTreatmentRequirementAuthorityBundleResult` with exactly `IsValid`, frozen
`Issues`, and nullable full `Authority`. The course form returns
`MortalWoundCourseRequirementAuthorityResult` with exactly
`Status=Satisfied|Unsatisfied|InvalidAuthority`, frozen `Issues`, and nullable full
`Authority`; only invalid authority has null authority. Each factory selects its exact
route/scopes and builds success/failure witnesses itself.

Exact mode entry points are:

```csharp
MortalWoundTreatmentResolutionResult CreateProcedureAttempt(
    MortalWoundTreatmentAttemptRequest request,
    WoundHistoryParseResult history,
    WoundMaterializationEnvelope? before,
    MortalWoundTreatmentAcceptedStateAuthority? acceptedState)

MortalWoundTreatmentResolutionResult CreateCourseMilestoneAttempt(
    MortalWoundTreatmentAttemptRequest request,
    WoundHistoryParseResult history,
    WoundMaterializationEnvelope? before,
    MortalWoundTreatmentAcceptedStateAuthority? acceptedState)

MortalWoundTreatmentResolutionResult CreateGuaranteedAttempt(
    MortalWoundTreatmentAttemptRequest request,
    WoundHistoryParseResult history,
    WoundMaterializationEnvelope? before,
    MortalWoundTreatmentAcceptedStateAuthority? acceptedState)
```

`MortalWoundGameTimeAuthority.Create(acceptedState, coordinates)` and
`MortalWoundTreatmentCapabilityAuthority.ExportCurrent(acceptedState, coordinates,
capabilityRef, actorRole)` return respectively `MortalWoundGameTimeAuthorityResult` and
`MortalWoundTreatmentCapabilityProofResult`, each with exactly `IsValid`, frozen
`Issues`, and nullable typed `Authority`/`Proof`. They read only the already lease-bound
accepted state, accept no JSON/value/fingerprint, and verify coordinate/context seals.
The publication-only sibling `ExportForPublication(acceptedState, coordinates,
capabilityRef, actorRole, publicationPlan)` has the same proof-result shape and selects
the exact final composed skill after-image whenever that canonical root is touched.
The game-time authority contains exactly `ClockKind`, `SourcePath`,
`CurrentTimeInMinutes`, `CoordinatesFingerprint`, `AcceptedStateFingerprint`, and
`AuthorityFingerprint`. First-course identity is derived through the same versioned
deterministic identity pattern as accepted wound IDs. Later calls reconstruct it from
typed history, so retry before or after restart cannot allocate another course.

Course preparation then calls
`MortalWoundCourseModeAuthority.Create(acceptedState, coordinates, before, history,
gameTimeAuthority)`. Its result contains exactly
`Disposition=TooEarly|Ready|DeadlineExceeded|InvalidAuthority`, frozen `Issues`, and
nullable immutable `Authority`; only ready/deadline-exceeded carry authority. The
authority contains the immutable game-time authority, course ID, milestone ordinal,
due/deadline minute, closed window disposition, complete `CourseStartAuthority`,
course-coordinate/coordinate/accepted-state fingerprints, and its own fingerprint. The
versioned course-coordinate fingerprint binds course ID/ordinal, wound/route fingerprints,
and the complete start-authority fingerprint and is recomputed by every later envelope.
The start authority
contains the complete typed starting wound, not only its hash. Too-early rejects before
requirement classification or reservation.

Before acceptance, the complete canonical request—including its nested full requirement
bundle and resource authority, not only their fingerprints—is persisted
inside the client-owned typed `treat` authority in `wound_commands.json` and any bounded
pending/repair packet. Accepted history stores that same complete detached request as
`transitionResult.requestAuthority`; it does not create a second sibling bundle copy. The
command/pending/history parser recomputes request coordinates/mode authority and the
bundle's context/accepted-state/route/common/course/scoped-row/non-overbooking seals,
then recomputes result/receipt fingerprints and rejects every outer disagreement. The
detached receipt alone projects the verified bundle fingerprint without rows or
consumption authority.

The same request may coexist in command and pending storage during one repair wave.
Parsers plus dice/Fate/resource registries coalesce byte-semantic exact copies by
`(OperationKey, AttemptId, RequestFingerprint)` into one logical request/claim. Any
different request, nested bundle/resource authority, or outer coordinate under a reused
operation or attempt coordinate is a conflict; exact copies never double-reserve.

After restart that typed command/pending authority supplies the client-owned operation
key, attempt ID, and request fingerprint without consulting current state.
`WoundHistoryParseResult.ProbeTreatmentAttempt(operationKey, attemptId,
requestFingerprint)` returns exactly `Status=NotFound|ExactReplay|Conflict|InvalidHistory`,
frozen `Issues`, nullable restored `Request`, and nullable original typed `Receipt`.
Because the method belongs to the parse result rather than valid-only
`WoundHistoryState`, `!IsValid` maps directly to `InvalidHistory` with the frozen parse
issues; no diagnostic state or test-only constructor is required. Stored seal or row
inconsistency also produces `InvalidHistory` and dominates replay/conflict. An exact
match returns detached request and receipt without constructing or reading current
wound/resources/accepted state/clock/capability and emits nothing. Reuse of an operation
key or attempt ID with another request fingerprint conflicts. Only `NotFound` requires
non-null `before` and `acceptedState` and may enter new-attempt validation. The three
entry points perform this probe before dereferencing either nullable live argument.

Every new resolution carries one immutable
`MortalWoundTreatmentRequirementAuthorityBundle`. It contains exactly `Mode`,
`ContextFingerprint`, `AcceptedStateFingerprint`, `RouteFingerprint`, nullable
`CourseId`, nullable `CourseMilestoneOrdinal`, nullable `CourseCoordinateFingerprint`,
nullable `CourseRequirementStatus=Satisfied|Unsatisfied`, nullable
`InterruptionReason=deadline_exceeded|requirements_unsatisfied`, frozen `Scopes`, and
`AuthorityFingerprint`. Each scope authority has exactly
`Scope=common|course_milestone`, nullable `CourseMilestoneOrdinal`,
`Status=Satisfied|Unsatisfied`, frozen `Bindings`,
frozen `FailureWitnesses`, and `AuthorityFingerprint`. Scope occurs once; milestone
scope exists only for course mode. Course bundle coordinates are all-present and equal
the complete course-mode authority; non-course coordinates are all null. Only the
milestone scope repeats the ordinal. Each authored local requirement index appears exactly
once as either a binding containing its unchanged successful T060 row, one complete typed
success witness, and a binding fingerprint, or a typed failure witness, never both.
Common evidence is never copied into milestone scope.

Non-course bundles require all course coordinates/status/reason null. Course bundles
require all three coordinates plus a real status; interruption reason is present exactly
for an accepted interruption and null for a satisfied accepted milestone.

The success witness is a closed kind-discriminated mechanical snapshot, not a diagnostic
or carried hash. Its common fields bind scope/index/kind/ref/snapshot/realm and applicable
owner/provider/target/location coordinates. Its exact kind payload preserves all
non-display values consumed by the unchanged T060 row writer: quantity totals and
availability, reservation, lifecycle/active state, tier, state, facility availability,
actor lifecycle/reachability/location/presence, and consent as applicable. Inapplicable
fields are absent from each closed variant. The history parser uses the shared unchanged
T060 fingerprint writer to reconstruct and verify the stored resolved row from this
witness before recomputing binding/scope/bundle seals. It never trusts the row's carried
fingerprint, and T060's public three-argument API/row shape remains unchanged.

A failure witness contains exactly `Scope`, zero-based `RequirementIndex`, `Kind`,
`AuthorityRef`, closed `LossReason=authority_absent|quantity_insufficient|reserved|
tier_insufficient|retired|inactive|state_mismatch|owner_unavailable|
provider_unreachable|consent_absent|facility_unavailable|wrong_location|
actor_not_present`, nullable immutable kind-specific `Observation`, and
`WitnessFingerprint`. Only `authority_absent` has null observation. Every other variant
preserves the same complete mechanical source/context slice as its success witness—
realm/owners, current+required location, requested total+available quantity, reservation,
lifecycle/active, tier/state, availability/reachability/presence/consent as applicable—
with a closed reason/value matrix proving the failed predicate. This is not a generic
nullable bag or `ValidationIssue` text and lets history replay reserved, retired,
elsewhere, unavailable, and other trustworthy T060 failures exactly.

`resourcePolicy` is closed: `reserveBeforeResolution` is exactly true; `consumeOn` is an
ordered exact-unique subset of categories reachable by the route mode and may be empty;
`refundOn` is exactly `cancelled`, `validation_failed`, `rolled_back` in that order; and
`mutations` contains only bounded exact-unique
`{kind=consume_requirement, scope=common|course_milestone, milestoneOrdinal,
requirementIndex}` selectors. Common selectors have null ordinal; course selectors name
one exact positive milestone. Every selector resolves once to an authored
`item_quantity|resource_quantity` requirement. It means consume that requirement's full
quantity if the selected category occurs in `consumeOn`; quantity requirements without a
selector remain reservable tools/preconditions and are released. Raw deltas, values, and
write paths are invalid.

After the provisional procedure-check, complete course-mode, or capability authority is built and
before semantic resolution, `MortalWoundTreatmentResourceComposer` is called through
exact typed `PrepareProcedure`, `PrepareCourse`, or `PrepareGuaranteed` overloads. Each
takes only matching accepted state, coordinates, exact `before`, complete requirement
bundle, and respectively `MortalWoundProcedureCheckAuthority`,
`MortalWoundCourseModeAuthority`, or `MortalWoundTreatmentCapabilityProof`. It resolves
the route/policy internally and returns
`MortalWoundTreatmentResourcePreparationResult` with exactly `IsValid`, frozen `Issues`,
and nullable immutable `Authority`; no overload accepts route/policy JSON, claims,
quantities, or fingerprints. Too-early/invalid course authority rejects, a trustworthy
ordinal-1 unsatisfied bundle rejects, an already-active trustworthy course interruption
holds nothing, and a satisfied consuming-capable attempt reserves
all current item/resource requirements before outcome selection.

The authority contains exactly `ReservationDisposition=held|not_required`, nullable
deterministic `ReservationId`, coordinate/accepted-state/route/requirement fingerprints,
nullable `CourseId`, nullable `CourseMilestoneOrdinal`, nullable
`CourseCoordinateFingerprint`, immutable typed `Policy`, frozen `Claims`, and
`AuthorityFingerprint`. Course coordinates are all-present and equal the course-mode and
bundle coordinates; non-course values are all null. Each held claim
contains exact scope/index/item-or-resource/ref/realm/owner/positive quantity, its success-
witness fingerprint, and claim fingerprint. `not_required` has null ID/empty claims and
is legal only for a trusted already-active interruption or current satisfied scopes
without quantity requirements.
The lease/generation registry reconstructs held claims from strictly valid complete
command/pending requests including their nested bundles, checks aggregate availability, and returns the same
reservation for exact retry. Accepted history marks it finalized. Failed preparation,
request sealing/persistence, cancellation, validation, or rollback releases the resource,
die, and Fate provisional claims; restart cannot overbook pending work.

The bundle binds the same accepted-state/context/route seals, and aggregate
non-overbooking is checked across successful bindings in all scopes before a consuming
resolution.

Non-course bundles have all course coordinate fields null and complete successful common rows.
Every course first applies the too-early clock gate and then runs the current requirement
classifier even when its deadline has elapsed, so stale/malformed authority cannot be
turned into an accepted interruption. `InvalidAuthority` always rejects. A trustworthy
course bundle always records its actual `CourseRequirementStatus=Satisfied|Unsatisfied`,
with aggregate status satisfied only when both scopes are satisfied.
When both deadline and predicate failure apply, `deadline_exceeded` deterministically
dominates `requirements_unsatisfied`; the latter is used only before the deadline. After
resolution T068 calls exactly
`MortalWoundTreatmentResourceComposer.Finalize(MortalWoundTreatmentResolution)`. It
recomputes request/resource/bundle/policy/claim seals. A held authority plus a trigger
listed by `Policy.ConsumeOn` emits typed full-quantity consumes only for matching current-
scope mutation selectors and releases every other claim. Trigger `None` is release-only;
`not_required` is empty. Any policy/category/reservation mismatch rejects. The resolver
owns neither registry mutation nor item/resource mutation.

The production resolver returns
`MortalWoundTreatmentResolutionResult` with exactly `Disposition`, frozen `Issues`,
nullable `Resolution`, and nullable `ReplayReceipt`, not wound, inventory, resource,
history, or effect mutations. Only `Resolved` carries a resolution and its one-shot
outcome intents; only `ExactReplay` carries a detached original receipt with no intents;
`Rejected|Conflict` carry neither. An accepted procedure, course milestone/interruption, or
guaranteed route has `attemptDisposition=accepted_terminal` even when its declared
result is `no_improvement` or harmful. Rejection, cancellation, or rollback records no
terminal attempt. Attempt terminality is derived by the resolver and cannot be supplied
as a caller boolean. It is independent from wound lifecycle terminality: a treatment
history row remains `terminal=false`; only the separate accepted `heal` row is
`terminal=true`.

The resolution contains the exact route/mode and attempt coordinates, selected authored
outcome (or interruption), result category, ordered typed outcome intents, embedded
requirement-authority bundle, mode-specific roll/clock/capability proof, resolution
authority fingerprint, result fingerprint, and `consumptionTrigger`. Its exact public
properties are `Mode`, `Coordinates`, `AttemptDisposition`, `ResultCategory`,
`SelectedOutcomeIndex`, `Interruption`, `DeclaredResult`, `OutcomeIntents`,
nullable immutable `CriticalReactionIntent`, `ConsumptionTrigger`, `CourseId`,
`CourseMilestoneOrdinal`, `CourseDisposition`,
immutable full `RequestAuthority`, `RequirementAuthority`, `ResourceAuthority`,
`ModeEvidence`, `RouteFingerprint`,
`ResolutionAuthorityFingerprint`, `RequestFingerprint`, `ResultFingerprint`, and
`RouteCompletion`. The trigger is exactly
`success`, `partial_success`, `failed_attempt`, or `none`; procedure, every accepted
milestone, and guaranteed results use their declared category only when sealed
`ResourceAuthority.Policy.ConsumeOn` contains it, otherwise `none`; course interruption
always uses `none`. T068 alone finalizes the previously prepared reservation into typed
consumption/release intents.

Every duplicated coordinate, mode evidence, complete bundle, resource authority, course
field, and fingerprint must agree exactly with its nested value in `RequestAuthority`;
the result seal binds that full request. `Finalize(resolution)` can therefore recompute
all request/bundle/policy/claim seals without any live lookup. `SelectedOutcomeIndex` is
the zero-based procedure band, `CourseMilestoneOrdinal - 1` for an accepted milestone,
`0` for guaranteed mode, and null for interruption, with exact outer/mode/history/
receipt agreement.

`CriticalReactionIntent` is non-null only for a mitigated player natural 1. It is the
exact `MortalWoundCriticalReactionIntent` returned by
`ResolvePreparedMortalWoundCriticalReaction(request, acceptedState)`, participates in
`ResultFingerprint`, and is consumed/revalidated only by T070. History and Receipt
persist its audit evidence/fingerprint but never this actionable intent.

`MortalWoundTreatmentReceipt` is the one exact detached replay type returned both as
`MortalWoundTreatmentReplayProbeResult.Receipt` and
`MortalWoundTreatmentResolutionResult.ReplayReceipt`. It has exactly `Mode`,
`Coordinates`, `AttemptDisposition`, `ResultCategory`, `SelectedOutcomeIndex`,
`Interruption`, immutable `DeclaredResult`, `ConsumptionTrigger`, `CourseId`,
`CourseMilestoneOrdinal`, `CourseDisposition`, `RequirementAuthorityFingerprint`,
`ResourceAuthorityFingerprint`, immutable `ModeEvidence`, `RouteFingerprint`, `ResolutionAuthorityFingerprint`,
`RequestFingerprint`, `ResultFingerprint`, `RouteCompletion`, and
`ReceiptFingerprint`. It reconstructs the accepted semantic result but contains no
`OutcomeIntents`, requirement rows, consumption/resource/wound/effect/history intent, or
publication authority. The history parser recomputes its receipt fingerprint from the
complete stored request and treatment result; the probe and outer result never expose a
second receipt shape.

`completedRouteIds[]` is a monotone set-in-authored-order audit that a route completed at
least once, not a lock that makes it unusable. A procedure appends its route exactly once
only for category `success`; `partial_success` and `failed_attempt` do not. A course
appends exactly once only when its final milestone declares `completion=completed`; an
intermediate milestone or interruption does not. An accepted guaranteed route appends
exactly once. `RouteCompletion=AppendOnce` iff the current qualifying success sees the
route absent from sealed `before.completedRouteIds`; a later qualifying success uses
`None`. A later legal use receives a new attempt identity, while exact replay never
appends again.

Durable history requires exact uniqueness for every non-null `attemptId` and every
`(courseId, milestoneOrdinal)` pair in addition to `operationKey`. Exact semantic replay
returns the prior typed treatment result; changed roll, time, course, capability,
requirement, route, before-state, result, or fingerprint under any reused coordinate is
a conflict. Procedure and guaranteed attempts have null course coordinates; a course
milestone or interruption has both course ID and ordinal. If a treatment schedules a
follow-up heal, the attempt belongs only to the `treat` row and the `heal` row does not
duplicate that attempt identity.

Replay classification precedes every fresh-state gate. An exact accepted replay returns
the stored receipt without rechecking already consumed items/doses, a capability changed
by its accepted use, or the wound after-image, and emits no new terminal/resource/wound
intent. A coordinate conflict also stops there. Only a new semantic operation proceeds
to current wound, requirement, roll/clock/capability, reservation, and publication
checks. This ordering cannot make an unaccepted or rolled-back attempt look accepted,
because those cases have no durable history row.

## Diagnosis paths and hidden routes

A diagnosis path has a readable `displayName`, exact `visibility`, closed
`requiresKnownFacts[]`, exact world `requirements[]`, one sealed `check`, exact typed
`reveals[]`, and version-1 `failurePolicy=no_reveal`. Known facts use only
`route:<routeId>` and `complication:<complicationId>` references inside the same wound.
Each of the three arrays is independently bounded to 16 entries;
`requiresKnownFacts[]` and `reveals[]` are unique and ordered.

The client computes a least fixed point. It starts with `knownRouteIds[]` and public or
player-known complications. Public/player-known paths become available when all known
fact prerequisites are present. Hidden paths require a non-empty prerequisite set and
become available only after it is satisfied; GM-only paths never establish player
reachability. Each available path adds exactly its declared reveals. Every hidden route
must enter the fixed point.

This structural graph is separate from fresh world authority. Before accepting the
wound or a later route, the client also proves that at least one path for each hidden
route can become reachable through exact current gameplay: visible structured wound
facts, skills,
providers, items, facilities, locations, quests, or other registered requirements.
These are invalid:

- a hidden route revealed only by itself;
- an unseeded cycle of hidden routes/complications and diagnosis prerequisites;
- a private GM fact with no attainable discovery path;
- a display-name-only item/provider/location reference;
- a path requiring an impossible or wrong-realm capability.

Diagnosis result evidence is client-sealed and closed to `success` or `failure`.
Success reveals the complete ordered fact set declared by exactly one reachable path.
Failure reveals nothing, changes no known fact, and remains one terminal retry-safe
attempt. Tier/skill failure may return a readable need for better expertise without
leaking the hidden route or complication. Resource consumption for either result uses
the ordinary accepted atomic resource policy; the diagnosis result itself carries no
raw inventory mutation authority.

## Exact requirements

The final attempt revalidates:

- item/resource identity, quantity, ownership, availability, and reservation;
- skill or capability identity/tier and active state;
- healer/provider identity, consent, reachability, and realm;
- facility/location identity and target presence;
- quest/effect/environment state;
- ordered prior milestones and game-time evidence;
- wound identity, severity, care state, complication set, and route version.

If any reference is stale, nothing is consumed. Removing a referenced item, skill,
effect, or facility during an active course uses the route's explicit interruption
policy or fails closed; no implicit migration is performed.

The Mortal version-1 authority-resolved requirement members in this slice are closed:

- `item_quantity`: exactly `kind`, exact permanent `itemRef`, positive integer
  `quantity`, and `ownerRole=provider|target`;
- `resource_quantity`: exactly `kind`, exact registered `resourceRef`, positive integer
  `quantity`, and `ownerRole=provider|target`;
- `skill_tier`: exactly `kind`, exact materialized `capabilityRef`, integer
  `minimumTier`, and `actorRole=provider|target`;
- `source_capability`: exactly `kind`, exact materialized `capabilityRef`, and
  `actorRole=provider|target`;
- `provider`: exactly `kind` and the selected exact permanent `providerRef`;
- `consent`: exactly `kind`, exact current `consentRef`, exact `providerRef`, and exact
  `targetRef`;
- `facility`: exactly `kind` and exact materialized `facilityRef`;
- `location`: exactly `kind`, exact materialized `locationRef`, and
  `targetRole=target`;
- `quest_state`: exactly `kind`, exact materialized `questRef`, and exact
  `requiredState`;
- `effect_state`: exactly `kind`, exact active `effectRef`, exact `requiredState`, and
  `targetRole=target`;
- `environment`: exactly `kind`, exact registered `environmentRef`, and exact
  `requiredState`.

Roles are semantic bindings to the already selected treatment context, never display
names or caller-provided actor IDs. `source_capability` does not imply the provider: its
explicit `actorRole` decides whether the provider or target must own the capability.
Provider and target are typed actor coordinates rather than display names. Procedure and
course context admits exact current `player|npc|combatant|combatant_member` coordinates;
any of them may be the physical-wound target, and one coordinate may fill both roles for
self-treatment. Combat coordinates come only from the accepted combat root and existing
promotion authority, never an array index. Guaranteed proof authority remains canonical
player/NPC skill authority: a combatant target may receive a provider-owned guarantee,
but a target-owned guarantee first requires the existing accepted persistence/promotion
transition to the corresponding canonical player/NPC owner.
Facility/environment authority must be bound to the context's exact current location;
location and effect requirements additionally bind the exact selected target. These
objects declare predicates only and never contain reservations, mutations, consumed
amounts, or caller-authored authority fingerprints.

## Resource and item handling

The treatment service emits typed resource/item intents into the common
accepted-mechanics plan. It never writes inventory or resource ledgers directly.

1. Resolve exact requirements and complete success/failure witnesses against the fresh snapshot.
2. Build the typed mode authority provisionally; no die/Fate claim is yet confirmed.
3. Prepare and exclusively reserve every current item/resource quantity under the sealed route policy.
4. Seal and persist one request, atomically confirming resource and any die/Fate claims.
5. Resolve the check/course/guarantee and derive a policy-aware consumption trigger.
6. Finalize only declared `consume_requirement` selectors, compose all after-images, and commit once.
7. Release every other claim on a non-consuming result, cancellation, rejection, or rollback.

A valid failed procedure may consume only selected supplies when `consumeOn` says so. A
reusable tool can remain a quantity requirement without a mutation selector. A repair
round restores the same reservation authority and never reserves or consumes a second set.

## Natural recovery

Each wound selects exactly one mode:

- `progressive`: a declared valid time/event cadence adds bounded progress;
- `requires_stabilization`: the cadence is blocked until stabilization;
- `no_natural_recovery`: only treatment improves the wound.

The policy declares clock source, cadence, threshold per step, blockers, overflow,
and allowed result. Optional deterioration declares exact unmet conditions, grace
period/cadence, and bounded complication/severity/death-contour result. The client owns
tick keys and progress arithmetic.

Mortal elapsed-time policies use only canonical
`world_time.currentTimeInMinutes`; cadence/grace values are positive/non-negative
signed 64-bit minute counts with checked arithmetic. They do not introduce a seconds
counter or consult wall-clock time.

The GM proposal leaves client-owned `recoveryAnchor` and `deteriorationAnchor` absent
or null; a non-null proposal value rejects. At accepted create T070 allocates `recoveryAnchor` from the
current canonical minute plus the accepted create transition ID. `recoveryAnchor` is
the progressive schedule anchor: `due-1` is not due; at `due` and later a single typed
intent carries the checked elapsed cadence count and a next anchor strictly after the
current minute. A committed tick advances that schedule, so the same clock event has no
second effect. Accepted stabilization at minute `S` rebases this schedule to `S`; time
spent blocked never becomes catch-up healing.

`MortalWoundRecoveryAuthoringAuthority.ValidateProposal(proposedWound, path)` is the
closed authoring check: its only public result fields are `IsValid` and immutable
`Issues`, and it rejects a non-null client-owned anchor at the exact proposal path.

Optional deterioration uses a distinct client-owned `deteriorationAnchor`, allocated
only when its exact unmet condition begins or re-enters. It is not reset by a recovery
progress tick. Its first deadline is `conditionAnchor + graceMinutes` inclusive:
`grace-1` has no deterioration and `grace`/`grace+1` use checked elapsed policy cadence.
The anchor contains its allocating transition ID and cannot be forged by a caller.

`MortalWoundRecoveryPlanner.Plan(fs, lease, binding, woundId)` has no caller-supplied
clock, plan, fingerprint, mutation, receipt, history, tick, or policy argument. Its
four-field result is exactly `Disposition`, `Issues`, `ReplayReceipt`, and
`Resolution`; resolution emits only closed typed progress/deterioration/death-handoff
intents. Death is a lifecycle-owner handoff, not direct wound/death mutation. T070's
accepted-plan composers register these intents with common `AcceptedMechanicsPlan`
authority and only the normal accepted-plan publisher writes canonical roots.
Initial wound create remains the existing sealed `WoundAcceptedTurnPlanner` preparation,
effect-batch, and finalization flow; recovery may not introduce an alternate create
writer. Stabilization must likewise read the canonical before-state and produce its own
complete sealed `Prepare -> #1535 effect batch -> Finalize`
`AcceptedMechanicsWoundStageBundle`; it may not publish a generic reducer result or a
caller-authored after-image. Stabilization is instead a selected, sealed T067 treatment
request/resolution consumed by the existing six-argument
`WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(fs, lease,
gameResponse, acceptedState, request, resolution)` pipeline. The recovery planner
obtains its binding only through
`MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(fs, lease, context, woundId)`;
no test or caller may manufacture an event/binding/fingerprint. T070's future overload passes each sealed bundle to
`AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(fs, lease, bundle)`, which
builds/registers the ordinary common plan from canonical roots.
`MortalWoundRecoveryAcceptedPlanComposer.Compose` returns the same exact sealed
`WoundStageBundle` beside its common plan and receipt, so the caller can verify the
registered plan's full input/preparation/effect/final/bundle fingerprint agreement.
The only public intent shapes are closed:
`MortalWoundRecoveryProgressIntent(AuthorityFingerprint, ElapsedCadences,
NextRecoveryAnchorMinute, RecoveryAnchorMinute, TickKey, WoundId)`,
`MortalWoundRecoveryDeteriorationIntent(AuthorityFingerprint, ElapsedCadences,
NextDeteriorationAnchorMinute, PolicyRef, TickKey, WoundId)`, and
`MortalWoundDeathHandoffIntent(AuthorityFingerprint, PolicyRef, TickKey, WoundId)`.
None carries a mutation, writer, after-image, history row, or JSON payload.

The recovery planner rejects a checked cadence/deadline/next-anchor time overflow with
the sole issue `mortal_wound_recovery_checked_time_overflow` at
`game_state/world/world_time.json`. A neutral or beneficial deterioration policy rejects
with the sole issue `mortal_wound_deterioration_policy_not_strictly_worsening` at the
canonical player-carrier path
`game_state/player/wounds.json.activeWounds[0].recovery.deteriorationPolicy` for the
selected wound. A replay after normal publication reopens/revalidates the persisted
history receipt before consulting the live clock; it returns the full closed receipt,
not an in-memory/cache projection.

Treatment interruption uses the same T069 classification, never raw policy JSON:
`MortalWoundDeteriorationPolicyAuthority.Create(acceptedState, coordinates, policyRef)`
binds the exact current wound/policy/attempt coordinates for a resolver, while the
five-argument canonical-state factory remains the recovery planner seam. Both return
the same closed authority result and accept no injected fingerprint or authority.

Prose such as "через несколько дней стало лучше" is not recovery evidence. A repeated
turn or clock event cannot tick twice.

## Treatment of another entity

The guided action lists Self first, then visible reachable targets. It never accepts a
raw NPC name or opaque ID. A conscious capable target must consent outside conflict. A
helpless friendly target may be treated when relationship/conflict authority permits.
An unwilling target requires a legal conflict action.

The selected hidden target binding is revalidated before resource reservation and once
again at commit. Duplicate visible names receive readable context without exposing
technical identity.

## Worked cross-setting acceptance pair

The implementation fixtures must prove at least two unrelated wounds without a wound
catalog:

1. Post-apocalyptic wound: contaminated laceration; bandage/antiseptic/antibiotic
   course or skilled debridement route.
2. Magical-world wound: crystalline burn; resonant crystal dust and a tuned healing
   focus or specialist ritual route.

Both use the same closed requirement/outcome primitives, exact authority, resource
transaction, history, and UI projection, while their names, fiction, items, skills,
and treatment logic are independently authored.
