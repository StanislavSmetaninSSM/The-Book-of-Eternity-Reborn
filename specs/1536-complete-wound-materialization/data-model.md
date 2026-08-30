# Data Model: Complete Wound Materialization and Healing

**Feature**: `1536-complete-wound-materialization`  
**Source issue**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)  
**Schema policy**: Direct pre-alpha cutover to version 1; no legacy reader or migration.

## 1. Canonical topology

| Root | Authority | Purpose |
| --- | --- | --- |
| `game_state/wounds/wound_identity_index.json` | client-owned | One stable identity and one active carrier occurrence per wound |
| `game_state/wounds/wound_history.json` | client-owned | Append-only accepted transition/replay/terminal evidence |
| `game_state/wounds/wound_commands.json` | client-owned command staging | Exact materialization/treatment/diagnosis/recovery intents for one accepted request |
| `game_state/wounds/wound_opportunity_receipts.json` | client-owned append-only | Durable accepted `none`/`materialize` decisions and cold-replay authority |
| `game_state/control/pending_wound_resolutions.json` | client-owned pending | Bounded GM construction/repair work, exact receipts, and immutable authority |
| `game_state/control/pending_mortal_wound_occurrences.json` | client-owned pending | Signed seven-kind Mortal occurrences captured by the active pending-turn snapshot |
| `game_state/player/wounds.json` | composed client/GM semantic carrier | Mortal player active wounds |
| `game_state/npcs/npc_wounds.json` | composed client/GM semantic carrier | Named Mortal NPC active wounds, separate from effects |
| `game_state/combat/enemies.json` / `allies.json` | existing combatant authority | `activeWounds[]` on exact combatants or group members |
| `game_state/meta/afterlife_entity_profiles.json` | existing afterlife actor authority | `activeWounds[]`, standard arts, and optional healing service per persistent actor |
| `game_state/meta/guardian_abode_residents.json` | existing Shining roster authority | Visible resident primary role, including `healing_support`, bound to an exact actor profile |
| `game_state/meta/afterlife_spiritual_conflict_state.json` | existing conflict authority | Danger mode, sealed wound opportunities/results, and bounded defeat outcome |

Every path read or written by one accepted plan has an exact before-image and is
covered by the pending-turn snapshot. Wound roots join effect, resource, owner,
scheduler, journal, quest, and output roots in the same accepted-mechanics publication.

## 2. Common scalar types

### Exact identifiers

Client IDs and source references use normalized non-empty identifiers, ordinal
comparison, no leading/trailing whitespace, no control/format characters, and no
confusable fallback. The client owns permanent identities.

### Enumerations

| Type | Values |
| --- | --- |
| `realm` | `mortal_world`, `chaos_sea`, `shining_abode` |
| `domain` | `physical`, `spiritual` |
| `severity` | `I`, `II`, `III`, `IV` |
| `lifecycle` | `active`, `healed` |
| `careState` | `fresh`, `untreated`, `stabilized`, `recovering`, `healed` |
| `visibility` | `public`, `known_to_player`, `hidden`, `gm_only` |
| `locationKind` | `anatomical`, `systemic`, `mental`, `spiritual_axis`, `other` |
| `transitionKind` | `create`, `worsen`, `complicate`, `diagnose`, `author_alternative_treatment`, `stabilize`, `treat`, `recover`, `heal`, `legacy`, `archive` |
| `diagnosisResult` | `success`, `failure` |
| `diagnosisFailurePolicy` | `no_reveal` |
| `routeMode` | `procedure`, `course`, `guaranteed` |
| `recoveryMode` | `progressive`, `requires_stabilization`, `no_natural_recovery` |
| `conflictDangerMode` | `training`, `controlled`, `hostile`, `annihilation` |
| `compensationKind` | `ink_feathers`, `favor`, `debt`, `quest`, `allegiance`, `free_aid` |

Death and soul dissipation are terminal outcomes in their owning lifecycle contracts,
not wound severities.

## 3. Owner coordinate

```json
{
  "realm": "mortal_world",
  "ownerKind": "npc",
  "ownerId": "client-owned-exact-owner-id",
  "carrierPath": "game_state/npcs/npc_wounds.json"
}
```

Allowed `ownerKind` values are `player`, `npc`, `combatant`, `combatant_member`,
`player_soul`, `guardian`, `resident`, `radiant_actor`, and `afterlife_actor`.
`carrierPath` is derived from the kind/realm and must match it. Afterlife actor kinds
bind to one accepted profile identity. Mortal combatants bind to the exact current
combat root and promotion authority.

## 4. Wound identity index

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "woundId": "client-owned",
      "realm": "mortal_world",
      "ownerKind": "player",
      "ownerId": "player_current",
      "carrierPath": "game_state/player/wounds.json",
      "domain": "physical",
      "status": "active",
      "createdAtTurn": 42,
      "createdEventRef": "accepted-event-ref",
      "lastTransitionOrdinal": 3,
      "terminalTransitionId": null,
      "semanticFingerprint": "sha256:..."
    }
  ]
}
```

### Index invariants

- `woundId` is globally unique and appears at most once in active carriers.
- Every active entry resolves to exactly one carrier wound with matching owner, realm,
  domain, chronology, and fingerprint.
- A healed entry resolves to no active carrier and exactly one terminal history row.
- Status changes only `active -> healed`.
- An active identity cannot be retargeted or moved except through a typed accepted
  combatant-persistence transition preserving owner authority.
- Physical and spiritual domains never convert in place.

## 5. Wound history

```json
{
  "schemaVersion": 1,
  "nextOrdinal": 18,
  "transitions": [
    {
      "transitionId": "client-owned",
      "woundId": "client-owned",
      "ordinal": 17,
      "woundTransitionOrdinal": 3,
      "kind": "diagnose",
      "turn": 45,
      "eventRef": "accepted-event-ref",
      "operationKey": "retry-safe-key",
      "beforeFingerprint": "sha256:...",
      "afterFingerprint": "sha256:...",
      "sourceFingerprint": "sha256:...",
      "attemptId": "client-owned-attempt-id",
      "transitionResult": {
        "kind": "diagnose",
        "diagnosisPathId": "gm-local-stable-within-wound",
        "result": "failure",
        "revealedFacts": [],
        "resultFingerprint": "sha256:..."
      },
      "readableSummary": "Диагноз пока не установлен.",
      "terminal": false
    }
  ]
}
```

History is append-only, ordered, and client-authored. `operationKey` is unique for the
semantic event/attempt/course milestone/recovery cycle. Every non-null `attemptId` is
also unique, and every non-null `(courseId, courseMilestoneOrdinal)` pair is unique;
exact semantic replay returns the original typed receipt, while reuse under a changed
operation or result conflicts. Course coordinates are either both null or both present,
and accepted ordinals for one course are contiguous from 1. The before/after fingerprint
chain must be contiguous for each wound. Exactly one terminal `heal` evidence row
prevents a replay from reopening or reapplying the wound. Later `legacy` and `archive`
rows are nonterminal audit/projection records that preserve the sealed terminal wound
fingerprint. The player History projection reads a sanitized subset; internal IDs and
fingerprints are never projected.

`transitionResult` is null for transition kinds whose durable replay result is already
fully represented by their existing typed coordinates and before/after state. It is a
required closed object for `diagnose`, `author_alternative_treatment`, `treat`, and
`legacy`. A diagnosis
result repeats the exact path, terminal-attempt `success|failure`, complete-or-empty ordered
`revealedFacts[]`, and recomputable result fingerprint; this remains durable even when
failure leaves the wound after-state unchanged. An alternative-authoring result stores
the safe authoring request reference, appended route/path IDs, their fingerprints, and
the recomputable accepted result fingerprint. A treatment result stores the exact
resolved route/mode/outcome category and ordered declared result plus its roll, course,
or guaranteed-source proof; this remains durable when treatment leaves wound mechanics
unchanged. `decline` has no transition row. The
history parser, replay probe, and already-accepted receipt compare and return the exact
typed result rather than treating matching before/after fingerprints as proof of the
same semantic attempt.

A `legacy` result is a closed union. Cosmetic rows contain exactly `kind=legacy`,
`legacyKind=cosmetic`, `legacyId`, `localLegacyRef`, `woundId`,
`terminalTransitionId`, `readableSummary`, and `resultFingerprint`. Mechanical rows add
exactly `sourceKind=wound_legacy`, `sourceId` equal to `legacyId`, owner/realm,
the complete detached `effectDraft`, its `sourceExportFingerprint`, an ordered exact
`applicationResults[]` map whose rows contain `applicationRef`, accepted `effectId`, and
`materializationFingerprint`, the `effectPlanFingerprint`, and the recomputable result
fingerprint. They preserve the terminal wound's
before/after fingerprint and remain nonterminal audit/source authority; neither effect
removal nor archive deletes them.

The alternative-authoring history result shape is exactly:

```json
{
  "kind": "author_alternative_treatment",
  "authoringRequestRef": "safe-opaque-request-ref",
  "addedRouteId": "new-complete-route",
  "addedDiagnosisPathId": null,
  "routeFingerprint": "sha256:...",
  "diagnosisPathFingerprint": null,
  "resultFingerprint": "sha256:..."
}
```

The common treatment history result shape is exactly. This worked row uses a separate
no-requirement `simple_bandage` route with `modifierSource.kind=fixed_zero`; it is not the
skill-based `clean_and_suture` route from the treatment contract example:

```json
{
  "kind": "treat",
  "mode": "procedure",
  "attemptDisposition": "accepted_terminal",
  "routeId": "simple_bandage",
  "routeFingerprint": "sha256:...",
  "requestAuthority": {
    "mode": "procedure",
    "coordinates": {
      "schemaVersion": 1,
      "sessionId": "exact-session",
      "sessionGeneration": "stable-session-generation",
      "requestId": "exact-request",
      "snapshotToken": "exact-snapshot",
      "operationKey": "client-owned-retry-safe-key",
      "attemptId": "client-owned-attempt-id",
      "woundId": "exact-active-wound",
      "routeId": "simple_bandage",
      "expectedBeforeFingerprint": "sha256:...",
      "eventRef": "accepted-event-ref",
      "eventKind": "accepted-event-kind",
      "eventAuthorityId": "accepted-event-authority-id",
      "eventSemanticFingerprint": "sha256:...",
      "turn": 45,
      "realm": "mortal_world",
      "providerKind": "npc",
      "providerId": "npc_healer_001",
      "targetKind": "player",
      "targetId": "player_current",
      "locationId": "loc_field_clinic_001",
      "contextFingerprint": "sha256:...",
      "acceptedStateFingerprint": "sha256:...",
      "coordinatesFingerprint": "sha256:..."
    },
    "milestoneOrdinal": null,
    "modeAuthority": {
      "sourcePath": "input/turn_request.json",
      "rollMode": "normal",
      "rollActorKind": "npc",
      "rollActorId": "npc_healer_001",
      "rollContributions": [],
      "sourceIndices": [0],
      "sourceRolls": [12],
      "selectedSourceIndex": 0,
      "naturalRoll": 12,
      "modifier": 0,
      "complicationDifficultyModifier": 0,
      "effectiveDifficulty": 10,
      "requirementAuthorityFingerprint": "sha256:...",
      "coordinatesFingerprint": "sha256:...",
      "acceptedStateFingerprint": "sha256:...",
      "preparedCriticalReaction": null,
      "authorityFingerprint": "sha256:..."
    },
    "requirementAuthority": {
      "mode": "procedure",
      "contextFingerprint": "sha256:...",
      "acceptedStateFingerprint": "sha256:...",
      "routeFingerprint": "sha256:...",
      "courseId": null,
      "courseMilestoneOrdinal": null,
      "courseCoordinateFingerprint": null,
      "courseRequirementStatus": null,
      "interruptionReason": null,
      "scopes": [
        {
          "scope": "common",
          "courseMilestoneOrdinal": null,
          "status": "satisfied",
          "bindings": [],
          "failureWitnesses": [],
          "authorityFingerprint": "sha256:..."
        }
      ],
      "authorityFingerprint": "sha256:..."
    },
    "resourceAuthority": {
      "reservationDisposition": "not_required",
      "reservationId": null,
      "coordinatesFingerprint": "sha256:...",
      "acceptedStateFingerprint": "sha256:...",
      "routeFingerprint": "sha256:...",
      "courseId": null,
      "courseMilestoneOrdinal": null,
      "courseCoordinateFingerprint": null,
      "requirementAuthorityFingerprint": "sha256:...",
      "policy": {
        "reserveBeforeResolution": true,
        "consumeOn": ["success", "partial_success", "failed_attempt"],
        "refundOn": ["cancelled", "validation_failed", "rolled_back"],
        "mutations": []
      },
      "claims": [],
      "authorityFingerprint": "sha256:..."
    },
    "requestFingerprint": "sha256:..."
  },
  "resultCategory": "partial_success",
  "selectedOutcomeIndex": 1,
  "interruption": false,
  "declaredResult": [
    { "kind": "stabilize" },
    { "kind": "add_recovery", "points": 1 }
  ],
  "consumptionTrigger": "partial_success",
  "resolutionAuthorityFingerprint": "sha256:...",
  "modeEvidence": {
    "rollMode": "normal",
    "rollActorKind": "npc",
    "rollActorId": "npc_healer_001",
    "sourceIndices": [0],
    "sourceRolls": [12],
    "selectedSourceIndex": 0,
    "naturalRoll": 12,
    "modifier": 0,
    "total": 12,
    "baseDifficulty": 10,
    "complicationDifficultyModifier": 0,
    "effectiveDifficulty": 10,
    "margin": 2,
    "originalOutcome": "ordinary",
    "resolvedOutcome": "ordinary",
    "selectedBandId": "simple_partial",
    "selectedOutcomeIndex": 1,
    "reactionEffectId": null,
    "reactionTriggerId": null,
    "reactionFingerprint": null,
    "acceptedRollFingerprint": "sha256:..."
  },
  "routeCompletion": "none",
  "resultFingerprint": "sha256:...",
  "receiptFingerprint": "sha256:..."
}
```

`modeEvidence` is a closed union selected by `mode`. Procedure evidence is exactly the
fields above. Course evidence is exactly `courseId`, `milestoneOrdinal`,
`courseStartedAtGameTimeMinutes`, `resolvedAtGameTimeMinutes`, `clockEvidenceFingerprint`,
and `courseDisposition=active|completed|interrupted`. Guaranteed evidence is exactly
`capabilityRef`, `actorRole`, `skillId`, `sourceSemanticFingerprint`, and
`capabilityProofFingerprint`. A course interruption has
`selectedOutcomeIndex=null`, `interruption=true`, the route's exact declared
interruption result/category, and `consumptionTrigger=none`. Other accepted outcomes
repeat their declared category as the trigger only when that category occurs in sealed
`resourceAuthority.policy.consumeOn`; otherwise their trigger is `none` and every held
claim is released rather than consumed.

`requestAuthority` is the complete recursively closed serialized
`MortalWoundTreatmentAttemptRequest`, not a fingerprint placeholder. Its exact
`resourceAuthority` is the pre-resolution reservation authority defined below. Its `mode` must
equal the sibling result mode, every coordinate must agree with the enclosing history
row, and its recomputed `requestFingerprint` participates in `resultFingerprint`.
There is no sibling `requestFingerprint` field. Procedure `modeAuthority` has exactly
the procedure-check fields shown above. Course `modeAuthority` is the complete immutable
`MortalWoundCourseModeAuthority`: it contains nested game-time authority, course ID/
ordinal, due/deadline/window disposition, complete `CourseStartAuthority` with detached
typed starting wound, course-coordinate/coordinate/accepted-state fingerprints, and
authority fingerprint.
Guaranteed `modeAuthority` is the complete immutable capability
proof defined below, including its operation limits. The parser recomputes all nested
coordinate, mode-authority, request, and result fingerprints; a syntactically valid
carried string is never accepted as proof.

`requestAuthority.requirementAuthority` is the complete immutable bundle accepted by the
live resolution, not only its fingerprint. It occurs only once in serialized treatment
history; the live resolution's `RequirementAuthority` property is that same immutable
object, not a second serialized copy. The history parser recomputes its context,
accepted-state, route, common/course authority, scoped-row, aggregate non-overbooking,
and bundle seals before deriving a replay receipt. This preserves restart auditability
for procedure, guaranteed, satisfied course, and trusted-unsatisfied interruption.
Only the already-verified detached receipt projects
`RequirementAuthorityFingerprint=RequirementAuthority.AuthorityFingerprint`; it does
not expose the bundle rows or turn them back into consumption authority.

Every accepted treatment has `attemptDisposition=accepted_terminal`; this is not the
history row's wound-lifecycle `terminal` flag. Treatment rows remain `terminal=false`.
Only the separate accepted `heal` row may be wound-terminal. Rejected, cancelled, or
rolled-back work produces no treatment history row.

### Transient Mortal treatment attempt authorities

`MortalWoundTreatmentAcceptedStateAuthority` is created only by
`MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(
FileSystemManager, CanonicalWriteLease, MortalWoundTreatmentAuthority.Context, string woundId)`.
The caller may obtain `Context` only through the strict production `ParseContext` selection parser;
the exporter verifies that selection against the live turn and internally derives the binding/events.
Under that one lease it strictly composes the unchanged T060 snapshot, world minute,
current player/NPC skill source, accepted combat-actor treatment projection, accepted effect mechanics, complete wound carriers,
wound identity index, and wound history; it selects and seals the exact current wound
and complete history fingerprints. Its result has exactly
`IsValid`, frozen `Issues`, and nullable `Authority`; the authority exposes no raw JSON,
dice collection, caller fingerprint, or public constructor. Absolute root identity and
live revision are runtime-only admission guards. Durable fingerprints use stable session
generation, session/request/snapshot/turn, semantic context, and semantic canonical file
hashes including wound carriers/index/history, so restart or root relocation does not
invalidate accepted history.

`MortalWoundTreatmentAttemptCoordinates` is produced only by
`MortalWoundTreatmentPlanner.CreateAttemptCoordinates(acceptedState, before,
operationKey, routeId, eventRef)` and contains exactly `SchemaVersion`, `SessionId`,
`SessionGeneration`, `RequestId`, `SnapshotToken`, `OperationKey`, derived `AttemptId`,
`WoundId`, `RouteId`, `ExpectedBeforeFingerprint`, `EventRef`, `EventKind`,
`EventAuthorityId`, `EventSemanticFingerprint`, `Turn`, `Realm`,
`ProviderKind`, `ProviderId`, `TargetKind`, `TargetId`, `LocationId`,
`ContextFingerprint`, `AcceptedStateFingerprint`, and `CoordinatesFingerprint`. Target
and realm must equal the Mortal wound owner, and `before` must equal the authority's
selected current wound semantically and by recomputed fingerprint. On a new resolution,
the valid history parse result must equal the authority's complete history fingerprint.
`EventRef` must resolve exactly once in `WoundAcceptedTurnBinding.AcceptedEvents`, and
the matching event kind/authority ID/semantic fingerprint are sealed. `OperationKey`
must be an exact identifier unique across reconstructed command/pending/history attempts.
The deterministic identity input is the
complete ordered semantic coordinate set except derived ID/fingerprint. There is no
constructor accepting a caller context, attempt/course ID, route, band, outcome, or
terminal flag. The result contains exactly `IsValid`, frozen `Issues`, and nullable
`Coordinates`.

Procedure `resolution.modifierSource` is the closed union exact
`{kind=fixed_zero}` or `{kind=resolved_skill_tier, requirementIndex}`. The zero-based
index must point to one satisfied T060 `skill_tier` row; its current tier is the modifier
and its resolved owner is the roll actor. `fixed_zero` uses modifier zero and the
provider roll actor. `MortalWoundProcedureCheckAuthority.Create(coordinates, route,
before, requirementAuthority, acceptedState)` derives that fact, adds every active
complication's `treatmentDifficultyModifier` to authored difficulty with checked
arithmetic, and filters accepted active roll-actor effects to `roll_modifier` components
containing `skill_check`. Advantage only selects `advantage`, disadvantage only selects
`disadvantage`, both cancel to `normal`; stacks/repetition do not escalate and version 1
has no great/dire mode. Normal uses one die, the other modes two; advantage selects the
higher and disadvantage the lower, with lower source index winning ties.

For selected natural 1 and exact roll actor `player/player_current`, the check factory
also asks a shared production Fate Shield arbiter for the same eligible oldest carrier
ordering used by `EffectAcceptedEventReportCatalog`. The sealed authority's nullable
prepared candidate fixes effect ID, trigger ID, accepted-effect fingerprint, and its own
fingerprint before resolution. The existing GM five-argument report API and leading-dice
contract remain unchanged; this typed adapter accepts only the production procedure
check authority. The registry reconstructs candidates reserved by full sealed procedure
requests in command/pending/history authority for the same binding, excludes those effect
IDs, and provisionally selects the next oldest in accepted-transition order. Exact retry
reuses it; successful persistence confirms it and failed sealing/persistence releases it,
so two procedures cannot claim one shield even across restart. Revalidation yields an immutable reaction/lifecycle intent, never a
carrier mutation. Without a candidate, evidence records
`critical_failure -> critical_failure` and the last band. With one, it records
`critical_failure -> failure`, selects the first authored `failed_attempt` band, and
consumes the shield atomically in T070 even if both indices are the same. NPC roll actors
never qualify. Mode evidence always persists exact closed original/resolved outcome,
selected band ID/index, and all-null or all-present reaction effect/trigger/fingerprint
fields.

The exact revalidation seam is
`EffectAcceptedEventReportCatalog.ResolvePreparedMortalWoundCriticalReaction(
MortalWoundTreatmentAttemptRequest request,
MortalWoundTreatmentAcceptedStateAuthority acceptedState)`. Its
`MortalWoundCriticalReactionResolutionResult` contains exactly `IsValid`, frozen
`Issues`, and nullable immutable `Intent`. The intent contains exactly
`EventType=owner_critical_failure`, deterministic per-attempt `EventRef` and
`CausalEventRef`, `Turn`, `Realm=mortal_world`, `TargetKind=player`,
`TargetId=player_current`, `EffectId`, `TriggerId`, `AcceptedEffectFingerprint`,
`PreparedReactionFingerprint`, `RequestFingerprint`, and `IntentFingerprint`.
Both event refs are written from stable accepted session-generation/request/snapshot/
turn plus the exact `AttemptId` and event role; they cannot collide across treatment
attempts or reuse the generic GM roll ref. Every field is sealed. The existing GM
five-argument `Compose` method and its leading-dice evidence shape remain unchanged;
both paths use one extracted `FateShieldReactionArbiter` for eligibility and oldest-
carrier ordering.

One accepted response may not submit both a typed procedure-treatment request and a
legacy GM `owner_critical_failure` effect report for that same accepted turn. The
common accepted-input coordinator detects the cross-surface pair before either Fate
candidate is confirmed and rejects the whole response with
`wound_treatment_fate_reaction_cross_surface_duplicate`, releasing every provisional
die, Fate, and resource claim. Legacy GM reports remain legal for non-treatment Mortal
actions, and multiple typed treatment requests remain legal through the shared
reservation registry.

The mapping is closed: selected 20 is
`OriginalOutcome=critical_success, ResolvedOutcome=critical_success`; selected 2-19 is
`ordinary, ordinary`; selected 1 without a shield is
`critical_failure, critical_failure`; selected 1 with the prepared shield is
`critical_failure, failure`. Only the final mapping has an all-present reaction triple.
Its `ReactionFingerprint` is the resolved typed lifecycle intent fingerprint, not the
candidate's `PreparedReactionFingerprint`. `SelectedBandId` and
`SelectedOutcomeIndex` must equal the outer resolution fields exactly.

The production dice registry reconstructs occupied indices from all strictly valid full
sealed procedure requests in typed wound commands, pending packets, and accepted history
for the same session/request/snapshot. In deterministic accepted-transition order it
provisionally allocates the lowest contiguous free one/two-index span. Exact retry gets
the same claim; changed coordinate/mode conflicts. Successful request persistence under
the same lease confirms the claim, while failed sealing/persistence releases it. Restart
therefore cannot issue an index held by a pending request.

`MortalWoundProcedureCheckAuthority` contains exactly `SourcePath`, `RollMode`,
`RollActorKind`, `RollActorId`, frozen ordered `RollContributions` rows of `EffectId`,
`ComponentId`, and `Contribution`, frozen `SourceIndices`, frozen `SourceRolls`,
`SelectedSourceIndex`, `NaturalRoll`, `Modifier`,
`ComplicationDifficultyModifier`, `EffectiveDifficulty`,
`RequirementAuthorityFingerprint`, `CoordinatesFingerprint`,
`AcceptedStateFingerprint`, nullable immutable `PreparedCriticalReaction`, and
`AuthorityFingerprint`. The candidate contains exactly `EffectId`, `TriggerId`,
`AcceptedEffectFingerprint`, and `PreparedReactionFingerprint`. It contains no caller
total, margin, category, band, or result.

`MortalWoundGameTimeAuthority.Create(acceptedState, coordinates)` returns
`MortalWoundGameTimeAuthorityResult` with exactly `IsValid`, frozen `Issues`, and
nullable `Authority`. Its authority contains exactly `ClockKind`, `SourcePath`,
`CurrentTimeInMinutes`, `CoordinatesFingerprint`, `AcceptedStateFingerprint`, and
`AuthorityFingerprint`. No wall time, JSON, numeric value, or caller seal is accepted.

Course mode next calls
`MortalWoundCourseModeAuthority.Create(acceptedState, coordinates, before, history,
gameTimeAuthority)`. Its result contains exactly
`Disposition=TooEarly|Ready|DeadlineExceeded|InvalidAuthority`, frozen `Issues`, and
nullable immutable `Authority`; only `Ready|DeadlineExceeded` carry authority. The
authority contains exactly the immutable game-time authority, `CourseId`,
`MilestoneOrdinal`, `DueAtGameTimeMinutes`, `DeadlineAtGameTimeMinutes`,
`WindowDisposition=ready|deadline_exceeded`, complete immutable `CourseStartAuthority`,
`CourseCoordinateFingerprint`, `CoordinatesFingerprint`, `AcceptedStateFingerprint`,
and `AuthorityFingerprint`. The versioned course-coordinate fingerprint binds exact
course ID, milestone ordinal, wound/route fingerprints, and the complete course-start
authority fingerprint; bundle, resource, request, resolution, and history recompute the
same value.
`TooEarly` is a nonterminal rejection before requirement classification/reservation.

On ordinal 1, `before.Care.ActiveCourseId` must be null. The course-start authority stores
exactly the deterministic course ID, route ID/fingerprint, complete detached typed
`StartingWound`, its fingerprint, start minute, accepted-state/coordinate fingerprints,
and its own fingerprint. On continuation, the current active-course ID must equal the
unique contiguous prior history course/route and that original authority is restored and
recomputed byte-semantically; no current wound or hash-only baseline substitutes for it.
Another course cannot start while the pointer is non-null. A legal procedure or guaranteed
route may still target the wound during a course; a terminal heal clears the pointer, and
subsequent course use then rejects. Final milestone or interruption also clears it.

The structural route parser merely requires every procedure success band and the complete
course to contain positive operation kinds, and forbids `no_improvement`,
`add_complication`, and `apply_deterioration` in procedure success/course milestone
results. It never revalidates old routes against the wound's later current state. Before a
new procedure/guaranteed attempt, the planner simulates every possible result against the
sealed current `before`; every result must be a legal complete transition and every
success must additionally produce at least one actual monotone improvement. At course start it simulates the full ordinal sequence against
`CourseStartAuthority.StartingWound` and requires at least one actual improvement. Later
milestones use that durable completeness proof and separately validate the selected
milestone against current canonical state.

`MortalWoundTreatmentAttemptRequest` is a closed immutable union produced only by
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
nullable `Request`. The request
contains exactly `Mode`, `Coordinates`, `MilestoneOrdinal`, `ModeAuthority`,
`RequirementAuthority`, `ResourceAuthority`, and
`RequestFingerprint`; `MilestoneOrdinal` is present only for course mode, while
`ModeAuthority` is exactly the procedure-check, complete course-mode, or canonical capability proof
type. The request fingerprint binds the complete coordinates plus the applicable mode
authority, full requirement authority, resource authority, and ordinal under one
versioned domain. A new operation creates coordinates and
seals a request once; repair/retry restores that request from client-owned pending/
accepted evidence. Rebuilding it from a current after-state is not replay.

Tests and production reach those sealers only through the exact public-to-assembly
orchestration factories
`MortalWoundTreatmentPlanner.PrepareProcedureRequest(acceptedState, history, before,
operationKey, routeId, eventRef)`, `PrepareCourseMilestoneRequest(...)`, and
`PrepareGuaranteedRequest(...)`, all with that same six-argument surface and all
returning `MortalWoundTreatmentAttemptRequestResult`. Each factory internally creates
coordinates and resolves the route from `before`. Procedure and guaranteed preparation
then create the complete requirement bundle, build their typed mode authority, prepare
resources, and seal the request. For procedure mode, after the bundle is satisfied and
before the check factory may reserve dice/Fate evidence, the planner performs the exact
all-band current-applicability simulation described below. Course preparation instead creates game-time and the
complete course-mode authority first, then creates the milestone-bound requirement
bundle, prepares resources, and seals the request.
Callers cannot construct a bundle/witness, inject a course baseline, or choose a
reservation. Any failed stage releases every provisional claim and returns no request.

The exact internal production bundle factories used by that orchestration are
`MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(acceptedState,
coordinates, before)`, `CreateForGuaranteed(acceptedState, coordinates, before)`, and
`CreateForCourseMilestone(acceptedState, coordinates, before, history,
MortalWoundCourseModeAuthority)`. The first two return
`MortalWoundTreatmentRequirementAuthorityBundleResult` with exactly `IsValid`, frozen
`Issues`, and nullable `Authority`, and produce authority only for fully satisfied common
requirements. The course method returns `MortalWoundCourseRequirementAuthorityResult`
with exactly `Status=Satisfied|Unsatisfied|InvalidAuthority`, frozen `Issues`, and
nullable full bundle `Authority`; satisfied/unsatisfied have one, invalid does not.
Factories select the route/scopes themselves, invoke unchanged T060, build complete
success/failure witnesses, and accept no requirement array, context/snapshot, or caller
fingerprint.

Before acceptance the full serialized request, including its nested complete
`RequirementAuthority` and `ResourceAuthority`, is stored under the typed `treat`
authority in `game_state/wounds/wound_commands.json`, and in
`game_state/control/pending_wound_resolutions.json` whenever a bounded GM repair or
construction wave is required. The accepted transition copies the exact detached
request into `transitionResult.requestAuthority` in
`game_state/wounds/wound_history.json`. Command, pending, and history parsers use the
same canonical serializer/parser; none stores only a request fingerprint. The outer
command `operationKey`, history `attemptId`/course coordinates, and all duplicated
request coordinates must agree exactly.

The same request may legitimately appear in both command and pending storage during one
repair wave. Every parser and dice/Fate/resource registry coalesces byte-semantic exact
copies by `(OperationKey, AttemptId, RequestFingerprint)` into one logical request/claim.
A differing request, bundle, resource authority, or outer coordinate under either reused
semantic coordinate is a conflict/invalid state; exact copies never double-reserve or
look like duplicate operations.

After a true cold restart from copied durable bytes under a different filesystem root
with fresh process-local registries, the typed accepted-transition command/pending parser
restores the complete detached client-owned submitted request before any current-state lookup; its operation
key, attempt ID, and request fingerprint are therefore available without rebuilding an
authority from the current wound or reusing cached canonical-root identity. Failed
persistence or rollback releases every provisional die, Fate, and resource claim, leaves
no durable command/pending/history duplicate, and permits one exact retry.
For an unaccepted persisted command, that restored request enters the ordinary
mode-specific T067 `Create*Attempt(Request, History, Before, AcceptedState)` reducer. The
reducer uses fresh canonical authority to recreate the exact full Resolution, including
ordered `OutcomeIntents` and nullable `CriticalReactionIntent`; the typed pair then enters
the existing six-argument T070 publication. The command result remains history-shaped and
does not persist actionable intents. Its declared result, mode evidence, resolution/result
fingerprints, and all nested request/result agreements are independently recomputed, so a
post-seal semantic replacement is invalid at parse or recompose time.
`WoundHistoryParseResult.ProbeTreatmentAttempt(string
operationKey, string attemptId, string requestFingerprint)` returns one immutable
`MortalWoundTreatmentReplayProbeResult` with exactly `Status`, frozen `Issues`, nullable
restored `Request`, and nullable original typed `Receipt`. `Status` is
`NotFound|ExactReplay|Conflict|InvalidHistory`. An invalid parse maps directly to
`InvalidHistory` with its frozen parse issues; the valid-only `WoundHistoryState` never
needs a diagnostic state or test constructor. The probe first recomputes the stored
coordinates, mode-authority, request, and result fingerprints and validates every
enclosing-row agreement; `InvalidHistory` dominates all other classifications.
`ExactReplay` returns detached request and receipt without consulting a current wound,
snapshot, route, resource, clock, or skill source. Reuse of the operation or attempt
coordinate under another request fingerprint is `Conflict`. Only `NotFound` requires
non-null current wound and accepted-state authority. Each mode entry point takes
`Request`, `WoundHistoryParseResult`, nullable `Before`, and nullable `AcceptedState` in
that order and probes before dereferencing either live argument. Guaranteed mode exports
the current proof from accepted state and requires equality with the sealed proof; the
publication boundary exports it again immediately before commit.

`MortalWoundCourseRequirementAuthorityResult` contains exactly `Status`, frozen `Issues`,
and nullable full `Authority`. `Status` is `Satisfied|Unsatisfied|InvalidAuthority`.
The factory selects common plus the exact requested route milestone itself and binds the
complete course-mode/start authority; it does not accept an arbitrary requirement
array. On `Satisfied|Unsatisfied`, `Authority` is the complete immutable milestone-bound
bundle, including unchanged successful T060 bindings and typed failure witnesses; on
`InvalidAuthority` it is null. Trustworthy current
predicate loss is `Unsatisfied`; malformed/ambiguous/
cross-realm authority or changed route/history/coordinate/seal is `InvalidAuthority`.

Every new resolution embeds one immutable
`MortalWoundTreatmentRequirementAuthorityBundle` with exactly `Mode`,
`ContextFingerprint`, `AcceptedStateFingerprint`, `RouteFingerprint`, nullable
`CourseId`, nullable `CourseMilestoneOrdinal`, nullable `CourseCoordinateFingerprint`,
nullable `CourseRequirementStatus=Satisfied|Unsatisfied`, nullable
`InterruptionReason=deadline_exceeded|requirements_unsatisfied`, frozen `Scopes`, and
`AuthorityFingerprint`. A scope authority has exactly
`Scope=common|course_milestone`, nullable `CourseMilestoneOrdinal`,
`Status=Satisfied|Unsatisfied`, frozen `Bindings`,
frozen `FailureWitnesses`, and `AuthorityFingerprint`; scope occurs once and milestone
scope exists only for course mode. Course bundle coordinates are all-present and equal
to the complete course-mode authority; non-course coordinates are all null. Only the
course-milestone scope repeats the exact ordinal; the common scope keeps it null. A
binding has exactly its zero-based local
`RequirementIndex`, one unchanged T060 `ResolvedRequirement`, one complete typed
`SuccessWitness`, and `BindingFingerprint`. Every authored requirement index appears
exactly once as either a successful binding or a failure witness, never both, and common
rows are never repeated in milestone scope.

Non-course bundles require every course coordinate, course status, and interruption
reason to be null. Course bundles require all three course coordinates plus a real
status; `InterruptionReason` is present exactly for an accepted course interruption and
is null for a satisfied accepted milestone.

The success witness is a closed union keyed by the same `Kind`. Its common coordinates
are exactly `Scope`, `RequirementIndex`, `Kind`, `AuthorityRef`, `SnapshotToken`, `Realm`,
nullable owner/provider/target kind/ID and `LocationId`, plus `WitnessFingerprint`.
Its kind payload stores every non-display mechanical value needed by the unchanged T060
row writer: item count/available count/reservation/lifecycle/active; resource current/
available value/reservation/lifecycle/active; skill tier/lifecycle/active plus selected
actor lifecycle/active/reachability/current location and required presence; capability
lifecycle/active plus the same actor evidence; provider lifecycle/active/reachability/
current location/presence; consent status/lifecycle/active plus exact provider/target
bindings and their required reachability/presence; facility lifecycle/active/available/
location plus required actor presence; location lifecycle/active plus required actor
presence; or quest/effect/environment state/lifecycle/active with their exact target or
location coordinate. It also repeats the authored requested quantity, minimum tier, or
required state when applicable. All inapplicable fields are absent inside the closed
kind payload, not nullable aliases.

The history parser reconstructs the exact T060 input row from the success witness,
recomputes `ResolvedRequirement.AuthorityFingerprint` through the shared unchanged T060
writer, verifies all public-row/witness coordinates and values, then recomputes binding,
scope, and bundle seals. A carried successful-row hash is never trusted, and diagnostics/
display names are not persisted. T060's three-argument resolver and public row shape stay
unchanged.

A failure witness contains exactly `Scope`, `RequirementIndex`, `Kind`, `AuthorityRef`,
closed `LossReason=authority_absent|quantity_insufficient|reserved|tier_insufficient|
retired|inactive|state_mismatch|owner_unavailable|provider_unreachable|consent_absent|
facility_unavailable|wrong_location|actor_not_present`, nullable immutable
`Observation`, and `WitnessFingerprint`. `authority_absent` alone has null observation.
Every other reason carries a closed kind-specific observation with the same complete
non-display source/context slice as that kind's success witness, even though one required
predicate is false: exact realm/owner/provider/target/current-and-required location;
requested and total/available quantity plus reservation state; required/current tier or
state; lifecycle/active; availability; reachability; presence; and consent as applicable.
No generic nullable value bag exists. The kind/reason matrix deterministically requires
the offending value (`reserved`, retired lifecycle, inactive, insufficient available
quantity/tier, unequal state/location, false availability/reachability/presence/consent)
while preserving all sibling values needed to replay unchanged T060. These are typed
mechanical witnesses, not persisted `ValidationIssue` diagnostics.

`resourcePolicy` is closed and declarative. `reserveBeforeResolution` is exactly `true`.
`consumeOn` is an ordered exact-unique subset of categories reachable by that mode
(`success|partial_success|failed_attempt` for procedure, only `success` for course or
guaranteed) and may be empty. `refundOn` is exactly the ordered
`cancelled|validation_failed|rolled_back` set. `mutations` contains only 0-64 exact-
unique `{kind=consume_requirement, scope=common|course_milestone,
milestoneOrdinal, requirementIndex}` selectors. A common selector has null ordinal; a
course selector has its exact positive milestone ordinal. Each selector resolves once to
an authored `item_quantity|resource_quantity` requirement in that scope. It consumes the
full declared quantity only when the selected category is in `consumeOn`; unlisted
quantity requirements are reserved as tools/preconditions but released without
consumption. Raw delta/value/write payloads are forbidden.

After the provisional procedure-check, complete course-mode, or capability authority exists but
before band/milestone/guaranteed resolution, production calls the corresponding exact
overload:

```csharp
MortalWoundTreatmentResourcePreparationResult PrepareProcedure(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    WoundMaterializationEnvelope before,
    MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
    MortalWoundProcedureCheckAuthority modeAuthority)

MortalWoundTreatmentResourcePreparationResult PrepareCourse(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    WoundMaterializationEnvelope before,
    MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
    MortalWoundCourseModeAuthority modeAuthority)

MortalWoundTreatmentResourcePreparationResult PrepareGuaranteed(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    WoundMaterializationEnvelope before,
    MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
    MortalWoundTreatmentCapabilityProof modeAuthority)
```

Each result contains exactly `IsValid`, frozen `Issues`, and nullable immutable
`Authority`. No overload accepts a route, policy JSON, caller claim, quantity, or
fingerprint: it resolves the exact route from `before`/coordinates and verifies the
accepted-state/mode/bundle seals. Procedure and guaranteed preparation require a fully
satisfied bundle. Course preparation itself rechecks clock/status precedence: too early
or invalid authority rejects; ordinal-1 unsatisfied requirements reject without creating
a course; only an already active course's trusted interruption creates no held claims;
an accepted satisfied milestone reserves its current scopes.

`MortalWoundTreatmentResourceReservationAuthority` contains exactly
`ReservationDisposition=held|not_required`, nullable deterministic `ReservationId`,
`CoordinatesFingerprint`, `AcceptedStateFingerprint`, `RouteFingerprint`,
nullable `CourseId`, nullable `CourseMilestoneOrdinal`, nullable
`CourseCoordinateFingerprint`,
`RequirementAuthorityFingerprint`, immutable typed `Policy`, frozen ordered `Claims`,
and `AuthorityFingerprint`. Course coordinates are all-present and must equal both the
course-mode and requirement-bundle coordinates; non-course coordinates are all null.
A held claim contains exactly `Scope`, `RequirementIndex`,
`Kind=item_quantity|resource_quantity`, `AuthorityRef`, `Realm`, `OwnerKind`, `OwnerId`,
positive `Quantity`, `SuccessWitnessFingerprint`, and `ClaimFingerprint`. All current
quantity requirements are claims; policy mutation selectors decide which are eventually
consumed. `not_required` has null ID and empty claims and is legal only for a trusted
already-active course interruption or when the current satisfied scopes contain no
quantity requirement.

The lease/generation-scoped reservation registry reconstructs held claims from every
strictly valid complete `treat` request (which owns its full bundle) in command/pending state for the
binding, rejects aggregate overbooking against the witnessed availability, returns the
same reservation for exact retry, and conflicts on changed coordinates/policy/bundle.
Accepted history marks a reservation finalized rather than held. Preparation is
provisional; successful request sealing+persistence confirms it, while preparation,
sealing, validation, cancellation, or persistence failure releases it together with any
provisional die/Fate claim. Restart cannot reserve a quantity already held by pending
work.

Aggregate non-overbooking is checked across successful bindings in all scopes before a
consuming result. Non-course bundles have null course fields and one satisfied common
scope. Every course runs both scopes after the too-early clock gate; invalid authority
rejects, aggregate status is satisfied only if both scopes are satisfied, and
`deadline_exceeded` dominates `requirements_unsatisfied` when both apply. An unsatisfied
accepted interruption therefore retains complete recomputable success and loss evidence.

After resolution T068 accepts exactly
`MortalWoundTreatmentResourceComposer.Finalize(MortalWoundTreatmentResolution)` and is
the only resource/item mutation composer. It recomputes request/resource/bundle/policy/
claim seals and derives the selected mutation selectors. A held authority plus a trigger
present in `Policy.ConsumeOn` emits exact typed full-quantity consume intents only for
matching current-scope selectors and releases every other held claim. Trigger `None`
emits release-only finalization; `not_required` emits an empty plan. A category/policy/
reservation mismatch rejects. Commit marks the deterministic reservation finalized;
cancel, validation failure, or rollback releases it. Exact replay calls neither prepare
nor finalize.

`MortalWoundTreatmentResolutionResult` contains exactly `Disposition`, frozen `Issues`,
nullable immutable `Resolution`, and nullable immutable `ReplayReceipt`; disposition is
`Rejected|Resolved|ExactReplay|Conflict`. The accepted resolution contains exact mode/
coordinates and exactly the public properties `Mode`, `Coordinates`,
`AttemptDisposition`, `ResultCategory`, `SelectedOutcomeIndex`, `Interruption`,
`DeclaredResult`, `OutcomeIntents`, nullable immutable `CriticalReactionIntent`,
`ConsumptionTrigger`, `CourseId`,
`CourseMilestoneOrdinal`, `CourseDisposition`, immutable full `RequestAuthority`,
`RequirementAuthority`,
`ResourceAuthority`, `ModeEvidence`, `RouteFingerprint`, `ResolutionAuthorityFingerprint`,
`RequestFingerprint`, `ResultFingerprint`, and `RouteCompletion`. Inapplicable course fields are null;
`AttemptDisposition` is `AcceptedTerminal`; `RouteCompletion` is `AppendOnce|None`.
`SelectedOutcomeIndex` is the zero-based selected procedure band, exactly
`CourseMilestoneOrdinal - 1` for an accepted course milestone, `0` for a guaranteed
singleton, and null for a course interruption; outer resolution, mode evidence,
history, and receipt must agree.
Only `Resolved` has a non-null `Resolution`, whose `OutcomeIntents` are actionable once.
The resolution's coordinates, mode evidence, complete requirement bundle, resource
authority, course fields, and request fingerprint must agree exactly with their nested
copies in `RequestAuthority`; the result fingerprint binds the complete request rather
than trusting an outer fingerprint. This is the self-contained input on which resource
finalization recomputes every request/bundle/policy/claim seal.
`CriticalReactionIntent` is non-null only for the mitigated player-natural-1 case,
participates in `ResultFingerprint`, and is revalidated/consumed only by T070. History
and receipt retain audit evidence/fingerprint but no actionable reaction intent.
Only `ExactReplay` has a non-null detached original typed `ReplayReceipt`.
`MortalWoundTreatmentReceipt` is the exact type of both outer `ReplayReceipt` and probe
`Receipt`; it contains exactly `Mode`, `Coordinates`, `AttemptDisposition`,
`ResultCategory`, `SelectedOutcomeIndex`, `Interruption`, immutable `DeclaredResult`,
`ConsumptionTrigger`, `CourseId`, `CourseMilestoneOrdinal`, `CourseDisposition`,
`RequirementAuthorityFingerprint`, `ResourceAuthorityFingerprint`, immutable `ModeEvidence`, `RouteFingerprint`,
`ResolutionAuthorityFingerprint`, `RequestFingerprint`, `ResultFingerprint`,
`RouteCompletion`, and `ReceiptFingerprint`. It has no outcome/resource/wound/effect/
history/publication intent or requirement rows. Rejected
and conflicting results have both payloads null. Neither payload exposes raw JSON,
resource/item mutation, or writable collections.

## 6. Active carrier roots

### Mortal player

```json
{
  "schemaVersion": 1,
  "owner": {
    "realm": "mortal_world",
    "ownerKind": "player",
    "ownerId": "player_current"
  },
  "activeWounds": []
}
```

### Named NPCs

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "npcId": "exact-existing-npc-id",
      "activeWounds": []
    }
  ]
}
```

### Combatants and afterlife profiles

The existing object receives `activeWounds[]`; no parallel name-based object is
created. The wound index stores its exact owner coordinate. Spiritual-conflict
participants use the corresponding persistent afterlife profile; conflict state does
not become a second carrier.

## 7. Active wound envelope

```json
{
  "schemaVersion": 1,
  "woundId": "client-owned",
  "lifecycle": "active",
  "owner": {},
  "origin": {},
  "classification": {},
  "display": {},
  "severity": {},
  "care": {},
  "complications": [],
  "consequences": {},
  "treatment": {},
  "recovery": {},
  "relations": {},
  "lastTransition": {}
}
```

### 7.1 Origin

```json
{
  "eventRef": "accepted-event-ref",
  "sourceKind": "combat_action",
  "sourceId": "exact-materialized-source-id",
  "sourceState": "active",
  "createdAtTurn": 42,
  "createdAtCycleId": null,
  "opportunityId": "client-owned",
  "guaranteedTriggerId": null,
  "readableCause": "Удар осколком стекла во время обвала."
}
```

The exact source adapter proves realm, target, event, and capability state. A guarantee
must have been materialized before the trigger event and be present in the sealed
opportunity.

### 7.2 Classification and location

```json
{
  "domain": "physical",
  "woundType": "Рваная режущая травма",
  "locationProfile": {
    "kind": "anatomical",
    "readableLocus": "левая ладонь и основание большого пальца",
    "authorityKind": "body_part",
    "authorityRef": "left_hand",
    "affectedSide": "left"
  }
}
```

`authorityKind`, `authorityRef`, and `affectedSide` are optional closed adapter fields.
No value is inferred from `woundType`, name, or prose.

### 7.3 Display

```json
{
  "name": "Рваная рана ладони",
  "description": "Края раны расходятся при попытке сжать пальцы.",
  "visibleSymptoms": ["кровотечение", "боль при хвате"],
  "prognosis": "Без очистки вероятно воспаление.",
  "visibility": "known_to_player",
  "acquisitionNarration": "Осколок вспарывает ладонь..."
}
```

All text is bounded untrusted text. Acquisition narration is required on creation and
must make the accepted wound legible. The client also emits a deterministic short
notification.

### 7.4 Severity

```json
{
  "value": "II",
  "rank": 2,
  "maximumAtCreation": "II",
  "lastChangeEventRef": "accepted-event-ref"
}
```

`rank` is the derived numeric representation 1-4 and must agree with `value`.
`maximumAtCreation` preserves the maximum severity sealed by the originating
opportunity; it may be higher than the severity selected by the GM and never changes
on later transitions. A transition supplies explicit before/after severity; impossible
improvement/worsening and severity V fail closed.

### 7.5 Care

```json
{
  "state": "untreated",
  "stabilizedAtTurn": null,
  "activeCourseId": null,
  "lastAttemptId": null
}
```

Legal state progression is bounded by the transition table below. A complication can
be treated without silently changing severity or care state unless the route's sealed
outcome declares both transitions.

### 7.6 Complication

```json
{
  "complicationId": "client-owned-within-wound",
  "kind": "infection",
  "state": "active",
  "displayName": "Начавшееся воспаление",
  "treatmentDifficultyModifier": 2,
  "ownedEffectIds": ["client-owned-effect-id"],
  "visibility": "known_to_player"
}
```

Complication `kind` is one of `bleeding`, `infection`, `pain`, `impairment`,
`systemic_instability`, `spiritual_instability`, or `other`; this is a mechanical
primitive list, not a complete wound catalog. `treatmentDifficultyModifier` is an
integer 0-4 and participates only where the selected treatment resolver declares it.
Genre-specific manifestation remains in bounded display/prognosis text, and any
mechanical result still uses a registered consequence effect.

### 7.7 Consequences

The nested #1535 definition body in this overview is abbreviated with empty objects and
arrays for readability; canonical state contains every required validated field and a
non-empty registered component set.

```json
{
  "slotBudget": 2,
  "slotsUsed": 2,
  "ownedEffectSources": {
    "definitions": [
      {
        "schemaVersion": 1,
        "definitionKey": "wound_bleeding_root",
        "display": {},
        "allowedRealms": ["mortal_world"],
        "allowedTargetKinds": ["player"],
        "components": [],
        "parameterBounds": {},
        "stacking": {},
        "lifetime": {
          "mode": "source_bound",
          "activePredicate": "active",
          "onSourceLoss": "expire"
        },
        "triggers": [],
        "removal": {},
        "links": [
          { "kind": "wound", "targetId": "client-owned-wound-id", "role": "source" }
        ]
      }
    ],
    "rootBindings": [
      {
        "effectId": "client-owned-effect-id",
        "definitionKey": "wound_bleeding_root"
      }
    ]
  },
  "entries": [
    {
      "slot": 1,
      "profileKey": "periodic_damage",
      "effectId": "client-owned-effect-id",
      "readableSummary": "Рана продолжает кровоточить."
    },
    {
      "slot": 2,
      "profileKey": "action_control",
      "effectId": "client-owned-effect-id",
      "readableSummary": "Хват левой рукой затруднён."
    }
  ]
}
```

`ownedEffectSources` is mandatory in schema version 1, including when both arrays are
empty. This is a direct technical cutover: the client does not accept or migrate a
slot-only wound shape. The object itself, either array, and every array element are
non-null. A legal non-mechanical Mortal wound persists exact empty arrays and produces a
detached zero-operation typed effect batch; it finalizes with an empty result map, no
allocated effect identity, and no changed effect after-image.

`definitions` is the complete detached #1535 source-definition graph owned by this
wound, not merely definitions that have an active effect instance now. Every definition
has an exact/confusable-unique `definitionKey`, the exact wound source link shown above,
the wound's realm/target authority, and one legal #1535 lifetime admitted by the wound
severity envelope. A `source_bound` lifetime, when selected, must bind the exact wound;
other accepted lifetimes may expire or suspend an effect without changing the wound. Every
`apply_definition` edge resolves inside this array. Every definition without a direct
root binding is reachable from exactly one directly bound definition; orphan unbound
definitions fail closed. A directly bound definition may also be an
`apply_definition` target permitted by #1535 only when that target uses the exact legal
`replace` stacking policy and belongs to the same reconstructed root-ownership domain as
the producing definition. That later application retires the prior target identity when
one is present, receives a fresh runtime effect identity, and does not create an
additional canonical root binding for the descendant instance. `independent`, `stack`,
`refresh`, or `merge` on a root-bound reaction target is invalid. Cycles, cross-wound
links, cross-ownership-domain edges, and unresolved edges fail closed. The graph
contains at most five definitions.

Every wound-owned definition has an exact/confusable-unique `stacking.stackKey` within
the complete graph and exact `stacking.maxStacks = 1`. Remaining stacking fields obey
the ordinary #1535 policy combinations (`independent` uses `atMaximum = no_change`).
This bounds the active/suspended group to at most one instance per definition while
retaining legal refresh, replace, merge, and reaction reuse behavior.

This bound is semantic, not arbitrary: every complete definition has at least one
component. The `event_reaction` producer consumes one consequence slot and every
flattened non-marker component of its reachable leaf consumes its own slot. Therefore a
severity-IV wound with one mechanical downstream-only definition can have at most the
reaction root, two other one-slot mechanical roots, that leaf, and one zero-slot marker:
five definitions total. Four ordinary one-slot roots plus a reaction producer and a
mechanical leaf require at least six definitions and at least six slots before any
marker, so they fail the severity envelope rather than expanding the graph bound. If the
leaf is the sole zero-slot marker, no separate marker root is legal. Nested wound-owned
reaction expansion is forbidden by the version-1 envelope. A zero-edge graph is legal;
at most one `apply_definition` edge and reachable leaf are permitted. When present, that
edge has exact #1535 `maxExpansion = 2`: one reaction plus one reachable leaf
definition. This remains one semantic wound expansion.

`rootBindings` contains only effects materialized directly by wound creation,
complication, or severity rematerialization. Effect IDs and bound definition keys are
both exact/confusable-unique; every binding maps one client-owned `effectId` to one
present graph definition. An unbound downstream definition referenced through
`apply_definition` has no root binding and receives no reserved effect ID; the normal
effect runtime allocates its identity only when the reaction is accepted. Response-local
`LocalWoundRef` and root `applicationRef` values never enter canonical wound state. The
wound planner never allocates a permanent effect ID; the accepted effect plan returns
the exact `applicationRef -> effectId` results used by finalization. There are at most
five root bindings.

An ephemeral `WoundRootLineageAuthority` is reconstructed from `rootBindings` and the
active complications' pairwise-disjoint `ownedEffectIds`. Each root is assigned exactly
one ownership domain: `base_wound` or its exact `complicationId`. Same-turn root
applications carry the corresponding domain before effect IDs exist. The authority is
not a new persisted wound field, but its ordered rows participate in the accepted source
export seal. A reaction child inherits its producer's domain. Before mutation, the
effect planner rejects stack/refresh/merge/replace behavior whose producer and incumbent
belong to different domains; #1535 has no inverse contribution capable of undoing half
of such a cross-domain mutation during later selective complication removal.

Every root-bound definition has exact empty `{}` `parameterBounds` and its root
application uses exact empty parameters; authored values are frozen in the definition's
components. An unbound downstream definition may expose bounded parameters only when
the persisted `apply_definition` component carries the exact future application
parameters. A bound definition reused as a reaction target therefore also receives
empty parameters.

Canonical wound serialization writes definitions in exact ordinal `definitionKey`
order, root bindings by `effectId` and then `definitionKey`, and consequence entries by
slot. Complete definitions use the shared recursive canonical object writer; nested
arrays retain their #1535-defined semantic order. The whole-wound semantic fingerprint
covers the full graph, root bindings, and entries. Canonical state persists no separate
source fingerprint; prepared accepted-turn plans carry their own immutable
`SourceExportFingerprint`.

Every `complication.ownedEffectIds` value resolves to a root binding; complications do
not list reaction-descendant IDs or duplicate slot truth, and their owned root sets are
pairwise disjoint. A direct root create transition has empty `sourceEffectIds`; a
reaction-created child's first `create` transition records exactly its producing effect
ID. Later replace/stack/refresh transition arrays are not ownership-parent edges.
Removing a complication
starts from its declared roots even when a root is terminal, traverses only validated
first-`create` causal parent-to-child identity edges inside the same exact wound source,
definition graph, and ownership domain, and terminates active/suspended members of that
closure. Every source-group identity is visited at most once; cycles, more than one
causal `sourceEffectId` on a child's first `create`, duplicate same-kind causal evidence,
foreign-source edges, or graph-missing definition keys fail closed. A legal replacement
may separately record old-to-new succession in `replace` transition evidence; that
lifecycle edge is not a second causal ownership parent and is ignored by selective
ownership traversal. Other roots/descendants owned by the still-active wound remain
unchanged.

After selective complication resolution, the complication, its declared root bindings,
and all slot entries pointing to those roots are absent and `slotsUsed` is recomputed.
Definitions are pruned only when no remaining root binding can reach them; a still-bound
or still-reachable definition remains. If an active/suspended same-source effect uses a
definition that would be pruned, the transition fails as a lineage disagreement. The
global effect identity index/history retains terminal provenance, while the active wound
contains no stale complication mechanics. The remaining graph and recomputed slots must
pass the current severity envelope or be paired with a legal same-plan severity/
replacement-consequence transition.

Each slot entry maps to exactly one root binding whose effect's exact source is this
wound. One root may own several slots. A directly materialized `wound_consequence`
marker is the sole legal root binding with zero slots. A marker may instead be a
reaction-only descendant; either way there is at most one marker definition across the
whole graph. Any later reaction-created instance has no reciprocal slot or additional
root binding: its worst-case
mechanical components were already charged to the originating reaction root. All other
roots have at least one slot. The effect may contain only the component composition
allowed by the registered wound consequence profile at the current severity. Mortal
`slotsUsed <= rank`; spiritual `slotsUsed == rank`. A Mortal wound with zero mechanical
slots is valid only when it has an active complication or a care/recovery constraint
whose presence changes a legal lifecycle transition; display text alone is never
sufficient. Direct roots have exact/confusable-unique logical stack coordinates and each
must create a new effect identity. Severity change first terminates the whole old active
wound-source group and then replaces the complete graph and root-binding set atomically;
it does not preserve effect identity for unchanged definitions.

### 7.7.1 Closed Mortal component envelopes

Allowed generic effect profiles are `characteristic_modifier`, `roll_modifier`,
`resistance_modifier`, `periodic_damage`, `periodic_restore`, `action_control`, and
`event_reaction`; `wound_consequence` is a zero-slot source/display marker, with at
most one marker across the wound-owned effect set. One independent characteristic,
roll operation, resistance, periodic resource operation, action, or worst-case reaction
result consumes one slot.

| Per-slot limit | I | II | III | IV |
| --- | ---: | ---: | ---: | ---: |
| Absolute flat characteristic/resistance modifier | 1 | 2 | 3 | 4 |
| Absolute percent characteristic/resistance modifier | 5% | 10% | 20% | 30% |
| Periodic amount / accepted exact resource maximum | 5% | 10% | 20% | 30% |
| Absolute action cost modifier | 1 | 2 | 3 | 4 |
| `grant` | one action/slot | one | one | one |
| `restrict` | one action/slot | one | one | one |
| `forbid` | none | none | one non-safety action/slot | one non-safety action/slot |
| Reaction definition expansion | none | none | one fully budgeted | one fully budgeted |

For scalar modifiers, a non-null cap is evaluated exactly as runtime evaluates it:
apply `minimum`, then `maximum`, and compare the resulting nonzero modifier to the
severity limit. Both endpoints use the exact decimal contract. A larger finite raw
value is legal when that runtime result is inside the envelope; an unrepresentable raw
number fails closed and never disappears from slot accounting.

Periodic values are quantum-aligned without exceeding the cap and execute no more than
once per accepted source event. Every worst-case spawned reaction component consumes
its own slot. The one-expansion-per-wound limit counts only wound-owned expansions;
bounded independent effect siblings remain outside wound slot and expansion budgets.
Before ownership classification, each raw effect proposal has a separate structural
work bound of 64 flattened reaction-expansion rows. That bound does not turn into a
wound budget and does not discard bounded independent rows.
`forbid` can target only `attack`, `cast`, or `movement`; `defend`,
`use_item`, `interact`, and `escape` cannot be forbidden. Aggregate restrictions
preserve inspection, communication, help, treatment, and exit.

### 7.7.2 Closed spiritual consequence profiles

| Profile | Axis | I | II | III | IV |
| --- | --- | --- | --- | --- | --- |
| `spiritual_roll_hindrance` | `rollMode` | one operation | one | one | one |
| `spiritual_action_cost_burden` | `actionCostAudit` | +1 | +1 | +2 | +3 |
| `spiritual_position_burden` | `conflictPosition` | one step | one | up to two | up to two |
| `spiritual_control_burden` | `controlState` | forbidden | one step | one | one |
| `spiritual_strain_burden` | side strain | forbidden | forbidden | one extra step | one extra step |
| `spiritual_tempo_burden` | `tempoAdvantage` | deny one gain | same | same | same |
| `spiritual_counter_burden` | `counterPayoff` | reduce one step | same | same | same |
| `spiritual_art_restriction` | one standard combat art | forbidden | forbidden | restrict | forbid |

Each row instance targets one declared non-safety operation/family and consumes one
slot. Duplicate profile/operation coordinates are invalid. Healing, inspection,
communication, help, withdrawal, surrender, negotiation, and dissipation choice cannot
be targeted. Eligible operation keys are exactly `pressure`, `counter`, `guard`,
`maneuver`, `binding`, `break_binding`, `force_binding`, `force_incarnation`,
`incarnation_resistance`, `champion_coordination`, and `recover_spiritual_power`.
The art-restriction subset is the ten combat arts above excluding
`force_incarnation`; it is a dedicated closed operation set rather than a projection of
the extensible standard-art registry, so later noncombat and healing arts remain
ineligible.
Spiritual severity requires exactly `rank` legal entries.

All eight profile identifiers above are deterministic registered #1535 component
primitives with only the `profile_specific` merge reducer and the exact closed payload
`{ operation, axis, magnitude }`. The common registry owns
closed payload/type/domain validation and deterministic execution/projection metadata;
the wound envelope owns the severity table, exact magnitude at that severity, slot
budget, coordinate uniqueness, and safe-exit checks. They are not represented by
`afterlife_combat_condition`. The exact ordinal operation set, fixed axis, and JSON
magnitude domain for each profile are defined by the common-registry table in
`contracts/wound-effects-and-atomicity.md`; no name, prose, numeric-string, fractional,
null, object, or array inference is permitted.

A definition containing one of these profiles has only afterlife allowed realms and a
non-empty subset of persistent actor target kinds: `player` (for `player_soul` in an
afterlife realm), `guardian`, `resident`, `radiant_actor`, or `afterlife_actor`. It
forbids `spiritual_conflict_side`, contains exactly one wound link with role `source`,
and otherwise uses an ordinary valid #1535 lifetime. The normal persistent form is
`source_bound` with `activePredicate = active` and `onSourceLoss = expire`; independent
effect expiry or suppression does not heal the wound or erase its canonical source
graph/root binding.

The payload remains owner-relative in canonical state. A typed current-conflict
projector resolves the exact persistent target actor to exactly one accepted conflict
participant and side, then derives a private `SpiritualWoundConflictContribution`.
`actionCostAudit` resolves to its player/opposition branch and `sideStrain` resolves to
`playerSideStrain`/`oppositionSideStrain`; other axes retain their registered name.
Absent, duplicate, wrong-realm, or side-ambiguous membership fails closed. The derived
contribution is never stored as an effect, wound, or combat condition, and
`combatConditions[]` remains byte-identical when only wound contributions change.

### 7.8 Treatment

```json
{
  "diagnosisPaths": [],
  "routes": [],
  "knownRouteIds": [],
  "completedRouteIds": []
}
```

At least one route must be mechanically complete for every active Mortal wound.
Spiritual wounds use the standard healing resolver rather than bespoke routes, but may
include visible provider/access facts in projection.

### 7.9 Recovery

The GM-authored recovery proposal contains only the declared mode/cadence/threshold/
blocker/policy fields below.  `recoveryAnchor` and `deteriorationAnchor` are absent or
null in that proposal; any non-null value is a client-owned-field error.  On accepted
creation, T070 derives the canonical anchors from the exact canonical
`world_time.currentTimeInMinutes` and accepted create transition ID, and the canonical
carrier may then contain the following additional client-owned state:

`MortalWoundRecoveryAuthoringAuthority.ValidateProposal(JsonObject proposedWound,
string path)` is the authoring boundary for this distinction. Its closed result is
exactly `IsValid` and frozen `Issues`; it allows absent/null anchors and reports a
non-null proposal anchor at its exact recovery path before accepted-state composition.

```json
{
  "mode": "requires_stabilization",
  "clockKind": "world_time.currentTimeInMinutes",
  "recoveryAnchor": {
    "anchorKind": "creation",
    "anchorMinute": 1260,
    "anchorTransitionId": "client-owned"
  },
  "deteriorationAnchor": {
    "conditionKey": "not_stabilized",
    "anchorMinute": 1260,
    "anchorTransitionId": "client-owned"
  },
  "cadence": 1440,
  "currentStepProgress": 0,
  "currentStepThreshold": 3,
  "lastTickKey": null,
  "blockers": ["not_stabilized"],
  "carryOverflow": true,
  "deteriorationPolicy": null
}
```

For Mortal wounds `cadence` is a positive signed 64-bit count of canonical world
minutes and uses the same `world_time.currentTimeInMinutes` authority as treatment
courses; it never uses wall time or a parallel seconds counter.

`recoveryAnchor` and `deteriorationAnchor` are independent closed canonical state.
`recoveryAnchor` supplies the next progressive cadence: at `due - 1` no transition is
eligible, while `due` and later evaluate `elapsedCadences = floor((now-anchor)/cadence)`
with checked signed-64-bit arithmetic and publish `nextRecoveryAnchorMinute`.  A
time jump must consume that exact count in one typed recovery intent and move the next
anchor past `now`; the same clock event therefore cannot create a second tick.  A
stabilization accepted at minute `S` rebases only `recoveryAnchor` to `S`, so blocked
days never become immediate catch-up healing.

`deteriorationAnchor` is allocated only when its exact `conditionKey` first becomes
unmet (or re-enters that condition) and is cleared when it ceases.  It is not reset by
a recovery-progress tick.  Its first deterioration deadline is
`anchorMinute + graceMinutes`, inclusive: `grace - 1` does nothing, `grace` and later
evaluate the checked elapsed deterioration cadence and publish the next deadline.
Both anchors carry the authoritative allocating transition ID; callers and GM output
cannot supply minutes, tick keys, fingerprints, history rows, or anchors.

For `requires_stabilization`, a sealed stabilization at minute `S` clears the
`not_stabilized` deterioration anchor and leaves the recovery anchor rebased to `S`.
If a later accepted `worsen` transition re-enters `not_stabilized`, T070 derives
`care.state=untreated`, clears `stabilizedAtTurn`, restores that blocker, and allocates a
fresh deterioration anchor whose `anchorMinute` is the accepted canonical minute and
whose `anchorTransitionId` is the exact published worsening transition ID. The recovery
anchor remains byte-semantically unchanged. The re-trauma source is a closed
source-shaped `MortalWoundOpportunityAdapter.ComposeAcceptedResponse(fs, lease,
sourceEvent, gameResponse)` input containing no binding, event fingerprint, transition
ID, or anchor. The adapter reads the live pending-turn manifest, derives the accepted
event/binding and sealed opportunity, and returns the ordinary
`WoundResponseInputCompositionResult`; only `StateDistributor` followed by
`ValidationService.ValidateAcceptedTurnRawEffectMaterializationAsync()` may admit it to
the common accepted plan.

The planner returns exactly `Disposition`, `Issues`, `ReplayReceipt`, and `Resolution`.
Its successful resolution has only typed recovery-progress, deterioration, or
death-handoff intents; it has no direct wound/death/history mutation.  T070 composes
those intents into the existing `AcceptedMechanicsPlan` authority and
`CanonicalStateNormalizer` is the sole atomic publisher of carrier, identity, history,
receipt, and after-images. Initial create is the existing sealed
`WoundAcceptedTurnPlanner` `Prepare -> effect batch -> Finalize` path, not a recovery
or generic-reducer shortcut. Stabilization also creates a second sealed bundle from the
canonical before-state through that exact contour; no caller provides an after-image or
a raw reducer result. A legal stabilization is instead carried by the sealed T067
treatment request/resolution through T070's six-argument treatment-publication pipeline;
the recovery continuation obtains its binding anew from the four-argument T066
accepted-state export. T070 passes each sealed bundle to its future
`AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(fs, lease, bundle)`, which
builds/registers the ordinary common plan from canonical roots. The registered plan
retains the exact non-null bundle plus input/preparation/effect/final/bundle seals and
wound carrier/identity/history after-images for publication.

The detached persisted recovery receipt is a closed four-field value:
`AuthorityFingerprint`, `ReceiptFingerprint`, `TickKey`, and `WoundId`.  Its exact
authority, tick, and wound coordinates equal the sealed recovery resolution and the
durable history row; it contains neither a mutation nor a caller-supplied receipt
payload.

For spiritual wounds `clockKind=afterlife_safe_cycle`, cadence is one safe cycle,
threshold derives from current severity, and progress per cycle derives from the
owner's accepted Spiritual Healing tier.

### 7.10 Relations and last transition

```json
{
  "relations": {
    "priorWoundId": null,
    "legacyRefs": [],
    "independentEffectRefs": []
  },
  "lastTransition": {
    "transitionId": "client-owned",
    "ordinal": 1,
    "turn": 42,
    "kind": "create"
  }
}
```

Relations are provenance only. They grant no cross-entity lifecycle authority.

## 8. Wound opportunity

```json
{
  "schemaVersion": 1,
  "opportunityId": "client-owned",
  "sessionId": "exact-session",
  "requestId": "exact-request",
  "snapshotToken": "exact-snapshot",
  "eventRef": "accepted-event-ref",
  "owner": {},
  "domain": "spiritual",
  "profileKey": "afterlife_strain_transition_v1",
  "minimumSeverity": null,
  "maximumSeverity": "II",
  "guaranteedTrigger": null,
  "inputEvidence": {},
  "authorityFingerprint": "sha256:..."
}
```

`minimumSeverity` is null for ordinary opportunities. A guaranteed trigger supplies
its exact required result but remains subject to hard realm/mode caps; contradictory
source contracts fail before asking the GM to invent a compromise. The GM response is
one of:

```json
{ "opportunityRef": "...", "decision": "none" }
```

or

```json
{
  "opportunityRef": "...",
  "decision": "materialize",
  "woundRef": "gm-local-ref",
  "proposal": {}
}
```

The client allocates permanent identities after complete validation.

### 8.1 Signed Mortal accepted occurrence

The client-owned pending root
`game_state/control/pending_mortal_wound_occurrences.json` is captured by the active
pending-turn snapshot before the GM receives an opportunity. It has the closed shape:

```json
{
  "schemaVersion": 1,
  "occurrences": [
    {
      "occurrenceId": "client-owned",
      "opportunityRef": "safe-public-correlation",
      "sourceSessionId": "exact-source-session",
      "sourceRequestId": "exact-source-request",
      "sourceSnapshotToken": "exact-source-snapshot",
      "sourceTurn": 41,
      "producerOperationKey": "mortal_wound_source_operation_...",
      "producerCandidateOrdinal": 0,
      "producerCandidateCount": 1,
      "adapterKind": "formal",
      "acceptedEventOrdinal": 0,
      "acceptedEvents": [
        {
          "eventRef": "accepted-event-ref",
          "kind": "formal_retrauma",
          "authorityId": "accepted-source-coordinate",
          "semanticFingerprint": "sha256:..."
        }
      ],
      "acceptedEventsFingerprint": "sha256:...",
      "owner": {
        "realm": "mortal_world",
        "ownerKind": "player",
        "ownerId": "player_current",
        "carrierPath": "game_state/player/wounds.json"
      },
      "domain": "physical",
      "profileKey": "mortal_formal_retrauma_v1",
      "source": {
        "kind": "formal_retrauma",
        "sourceId": "accepted-source-coordinate",
        "state": "accepted"
      },
      "outcome": {
        "kind": "harmful",
        "maximumSeverityRank": 2,
        "readableCause": "Повторная травма уже поврежденной руки."
      },
      "hardMaximumSeverityRank": 4,
      "minimumSeverityRank": null,
      "guaranteedTrigger": null,
      "safeContext": {
        "target": "поврежденная рука",
        "cause": "повторный удар",
        "allowedLocationKinds": ["anatomical"]
      },
      "worseningTarget": {
        "woundId": "exact-active-wound-id",
        "causeKind": "retrauma"
      },
      "sourceResultFingerprint": "sha256:...",
      "candidateFingerprint": "sha256:...",
      "occurrenceFingerprint": "sha256:..."
    }
  ]
}
```

The seven exact adapter kinds are `formal`, `qte`, `combat`, `trap`, `check`,
`hazard`, and `narrative`. A registered client producer reduces each harmful accepted
typed result to one immutable occurrence-candidate batch, and the source-result common
accepted plan atomically publishes the complete batch to this root. Harmless results
produce no occurrence candidate or public opportunity. Only the next active pending-
turn snapshot may make a sealed harmful row available to the GM. A current combatant, hazard,
source definition, die, or prose row is only supporting source evidence and cannot
create an occurrence by itself. The
source-shaped adapter input repeats only `schemaVersion`, `adapterKind`,
`acceptedEventOrdinal`, `opportunityRef`, `owner`, `domain`, `profileKey`, `source`,
`outcome`, `safeContext`, and an absent-for-create or complete non-null
`worseningTarget`. It must agree exactly with one signed occurrence. It never carries
the hard maximum, minimum/guarantee, occurrence or producer identity, source or decision
session/request/snapshot/turn authority, accepted-event kind/ID/ref/fingerprint,
transition identity, receipt, proposal, anchor, or after-image. Explicit null
`worseningTarget` is invalid rather than an alias for absence.

The shared event primitive uses a closed `WoundAcceptedResponseEventProjection`
(`sessionId`, `requestId`, `snapshotToken`, positive `turn`, ordered typed event
coordinates) and zero or more `WoundSelectedEventEvidence` values keyed by exact
zero-based ordinal. Selected evidence carries semantic facts but no fingerprint; the
composer derives the existing opportunity-evidence seal. Generic rows retain the
existing `accepted-wound-event-authority-v1` byte formula and production property order,
so this hardening requires no migration. The result returns both the complete immutable
event authority and `acceptedEventsFingerprint`. Raw `JsonObject`, caller-provided event
fingerprints, free-string selection, and ambiguous exact/confusable `(kind, authorityId)`
pairs are not accepted at this boundary. Producer, later adapter, and validator wiring
must each derive this projection/evidence from their own typed accepted result or the
current signed occurrence; a GM correlation or parsed command is never that authority.

For a later decision, the adapter first recomposes the historical complete event vector
under `sourceSessionId`, `sourceRequestId`, `sourceSnapshotToken`, and `sourceTurn` and
requires byte-semantic equality with the occurrence's stored vector/fingerprint. It then
rebinds the same ordered coordinate triples to the active decision snapshot and derives
selected evidence for every selected ordinal in the complete producer batch. Selected
wound semantics stay stable; generic sibling fingerprints are recomputed for the active
snapshot. The current opportunity therefore seals the active recomposed
`acceptedEventsFingerprint`; copying the historical source-set fingerprint or composing
only the selected row is invalid.

The registered producer seam is deliberately separate from correlation adapters. A
producer projects only after its client-owned accepted result is finalized. An empty
harm list means harmless; no separate caller-authored harmful flag exists. A producer
kind without such a typed result remains unregistered and fails closed until its actual
resolver supplies owner/profile/source/outcome/cap/location/create-or-worsen authority.
It is forbidden to synthesize that missing result from current combatant, hazard,
resource-event, die, response-fragment, or narrative-prose presence.

The pending root contains at most 32 occurrences. Each occurrence contains 1-160 exact
accepted events in original order; `acceptedEventOrdinal` is zero-based and must select
one row, while `acceptedEventsFingerprint` is independently recomputed over the whole
array. `producerOperationKey` is the stable source-result replay coordinate: an exact
candidate retry resolves the existing row and changed semantics under the same key
conflict. Every candidate also seals a zero-based `producerCandidateOrdinal` and the
positive complete `producerCandidateCount`; a source-result batch must contain exactly
the unique contiguous ordinals `0..count-1`. Pure append logic validates this set and
orders it by ordinal, so caller enumeration order cannot change the after-image.
Occurrence IDs, public refs, candidate fingerprints, and semantic candidate coordinates
are exact/confusable unique. `producerOperationKey` is unique per batch, not per row:
every row sharing it must agree exactly on source session/request/snapshot token/turn,
candidate count, exact `adapterKind`, source-result authority, and source-result
fingerprint, while
`(producerOperationKey, producerCandidateOrdinal)` is exact/confusable unique across
pending and consumed rows. Candidate and occurrence
fingerprints are domain/versioned and recomputed over every semantic field, including
the ordered event set; neither is accepted as caller authority.

Version 1 uses the existing length-prefixed UTF-8 SHA-256 writer (including its null
sentinel). Every integer below uses invariant decimal text. `acceptedEventsFingerprint`
uses domain `book_of_eternity.wound.accepted_event_set`, version `1`, event count, then
for every row its zero-based index, `eventRef`, `kind`, `authorityId`, and
`semanticFingerprint` in original order.
`candidateFingerprint` uses domain
`book_of_eternity.mortal_wound.occurrence_candidate`, version `1`, followed in order by:
source session/request/snapshot token/turn; producer operation key, candidate
ordinal/count; adapter kind, selected event ordinal, and the recomputed event-set
fingerprint; owner realm/kind/
ID/carrier path; domain and profile; source kind/ID/state; outcome kind/maximum/readable
cause; hard maximum, nullable minimum, and nullable recomputed guarantee-authority
fingerprint; safe target/cause, location count, then each zero-based location index and
value in order;
`create|worsen` plus nullable wound/cause coordinates; and the source-result fingerprint.
The client derives `occurrenceId` as `mortal_wound_occurrence_` plus the candidate hash
hex and `opportunityRef` as `mortal_wound_` plus the same hex. `occurrenceFingerprint`
uses domain `book_of_eternity.mortal_wound.occurrence`, version `1`, then that occurrence
ID, public ref, and candidate fingerprint. Parsing recomputes the event-set, candidate,
derived identities, and occurrence fingerprint rather than trusting persisted copies.
The typed append candidate therefore has no occurrence ID, public ref, event-set
fingerprint, candidate fingerprint, occurrence fingerprint, or guarantee-authority
fingerprint fields; the pure append planner derives all six. Rows in one producer batch share the exact complete accepted-
event array/fingerprint, although different candidates may select different ordinals.

The pure occurrence append API is
`PlanAppend(MortalWoundOccurrenceState before,
MortalWoundOccurrenceCandidateBatch batch,
MortalWoundOpportunityReceiptState consumedReceipts)`. The non-empty batch owns an
immutable list of semantic candidates with the fields above but none of the derived
fields just listed. It must contain exactly one complete producer key/count and every
ordinal `0..count-1`; caller order is ignored and the derived append order is ordinal.
Its `BatchFingerprint` uses domain
`book_of_eternity.mortal_wound.occurrence_candidate_batch`, version `1`, producer key,
candidate count, then each ordinal and derived candidate fingerprint in ordinal order.
The result is closed to `Disposition`, `Issues`, `State`, and `BatchFingerprint`, where
disposition is exactly `appended|exact_replay|conflict`. Exact replay requires every
candidate to match either one pending occurrence or one consumed receipt; a mixture is
normal after partial decision processing. A missing ordinal, changed coordinate/seal,
or pending/consumed duplicate is conflict and returns no after-state. A new append is
all-or-nothing under the 32-row pending bound.

The construction seam is exact: `MortalWoundOccurrenceCandidateBatch` takes only
`IReadOnlyList<MortalWoundOccurrenceCandidate>`. Each candidate constructor takes, in
schema order, source session/request/snapshot token/turn; producer key/ordinal/count;
adapter kind; selected ordinal plus `IReadOnlyList<WoundAcceptedEventAuthority>`; owner; domain;
profile; typed source; typed outcome; hard maximum; nullable minimum; nullable existing
`WoundGuaranteedTriggerEvidence`; safe context; nullable typed worsening target; and
source-result fingerprint. The small typed records are
`MortalWoundOccurrenceSource(Kind, SourceId, State)`,
`MortalWoundOccurrenceOutcome(Kind, MaximumSeverityRank, ReadableCause)`, and
`MortalWoundOccurrenceWorseningTarget(WoundId, CauseKind)`.

Mortal owners are exactly `player|npc|combatant|combatant_member` on their registered
carrier paths, and domain is exactly `physical`. A persisted occurrence outcome is
exactly `harmful` with rank I-IV; a harmless typed result is rejected before candidate
publication, and later source ingress cannot invent a row for it.
`hardMaximumSeverityRank` is I-IV and is sealed by the typed producer.
Safe context uses unique closed location kinds and bounded readable text. Source kind,
ID, and state remain typed-producer coordinates whose kind-specific canonical agreement
is revalidated by T064 rather than an open authorization surface.

An ordinary occurrence has explicit null `minimumSeverityRank` and
`guaranteedTrigger`. A guaranteed occurrence has a rank I-IV minimum and one complete
pre-materialized trigger containing trigger/source coordinates, realm/domain/owner,
required rank, `materializedAtTurn`, source-contract fingerprint, and the recomputed
existing `book_of_eternity.wound.guaranteed_trigger_authority` v1 fingerprint. Its
required rank equals the minimum, is within both outcome and hard maxima, its source/
realm/domain/owner exactly match the occurrence, its source state is `active`, and it was
materialized before `sourceTurn`. Partial, caller-sealed, stale, or contradictory
guarantees reject. This preserves the mandatory-result behavior after a cold restart;
a guaranteed occurrence cannot become an ordinary optional one.

The non-null pair has this exact closed property order and shape:

```json
{
  "minimumSeverityRank": 2,
  "guaranteedTrigger": {
    "triggerId": "exact-pre-materialized-trigger",
    "sourceKind": "exact-occurrence-source-kind",
    "sourceId": "exact-occurrence-source-id",
    "sourceState": "active",
    "realm": "mortal_world",
    "domain": "physical",
    "owner": {
      "realm": "mortal_world",
      "ownerKind": "player",
      "ownerId": "player_current",
      "carrierPath": "game_state/player/wounds.json"
    },
    "requiredSeverityRank": 2,
    "materializedAtTurn": 40,
    "sourceContractFingerprint": "sha256:...",
    "authorityFingerprint": "sha256:..."
  }
}
```

The guarantee-authority fingerprint uses domain
`book_of_eternity.wound.guaranteed_trigger_authority`, version `1`, followed by
`triggerId`, source kind/ID/state, realm, domain, owner realm/kind/ID/carrier path,
required severity rank, materialized turn, and source-contract fingerprint. Both integer
fields use invariant decimal text. The append candidate supplies the evidence fields but
not `authorityFingerprint`; the pure planner derives it.

`worseningTarget` is absent for create and non-null only for an explicit worsen. A null,
partial, stale, foreign, terminal, duplicate, or inferred target is invalid. The
Mortal worsening cause is exactly `deterioration|retrauma`. The
occurrence selects one ordinal in the complete accepted-event set, but the binding seals
the whole ordered set reconstructed independently at composition and validation.
The strict state parser preserves canonical row order and rejects malformed, duplicate,
confusable, over-limit, or same-batch out-of-order state; only the typed append planner
normalizes caller batch enumeration into ascending candidate ordinal. Historical append/consumption agreement is enforced by
T070 against the signed before-image; the parser is never treated as proof that an
unsigned earlier root had the same rows.

Both this root and `game_state/wounds/wound_opportunity_receipts.json` are foundational
members of `WoundAcceptedTurnSnapshotContract.RequiredPaths`. When a distributed wound
command is admitted, `ValidationService` reads their exact signed before-images, requires
the live client-owned files to remain byte-identical, parses both closed schemas, and
validates their cross-root consumed-occurrence agreement. For every submitted command
opportunity it resolves exactly one signed occurrence by client-owned `occurrenceId`,
historically recomposes the complete producer event vector, rebinds that vector to the
active decision snapshot, and rebuilds owner/profile/source/outcome/cap/guarantee and
optional worsening authority. Worsening resolves only against the signed wound-carrier
before-image. The rebuilt opportunity must equal the submitted authority field-for-field;
a merely self-consistent command seal is insufficient. All signed prior receipts are
projected into ordinary decision receipts for strict command recomposition.

### 8.2 Opportunity decision receipts

The append-only client-owned root
`game_state/wounds/wound_opportunity_receipts.json` stores every accepted decision,
including `none`:

```json
{
  "schemaVersion": 1,
  "nextOrdinal": 2,
  "receipts": [
    {
      "receiptId": "client-owned",
      "ordinal": 1,
      "opportunityId": "client-owned",
      "opportunityAuthorityFingerprint": "sha256:...",
      "sessionId": "exact-session",
      "requestId": "exact-request",
      "snapshotToken": "exact-snapshot",
      "turn": 42,
      "eventRef": "accepted-event-ref",
      "eventSemanticFingerprint": "sha256:...",
      "consumedEventSelection": {
        "acceptedEventOrdinal": 0,
        "adapterKind": "formal",
        "authorityKind": "formal_retrauma",
        "authorityId": "accepted-source-authority",
        "outcomeKind": "harmful",
        "maximumSeverityRank": 2,
        "readableCause": "Повторная травма подтверждена принятым результатом."
      },
      "sourceSessionId": "exact-source-session",
      "sourceRequestId": "exact-source-request",
      "sourceSnapshotToken": "exact-source-snapshot",
      "sourceTurn": 41,
      "producerOperationKey": "mortal_wound_source_operation_...",
      "producerCandidateOrdinal": 0,
      "producerCandidateCount": 1,
      "sourceResultFingerprint": "sha256:...",
      "candidateFingerprint": "sha256:...",
      "occurrenceFingerprint": "sha256:...",
      "decision": "none",
      "decisionFingerprint": "sha256:...",
      "operationKey": "wound_operation_...",
      "woundId": null,
      "transitionId": null,
      "receiptFingerprint": "sha256:..."
    }
  ]
}
```

The receipt root contains at most 20,000 rows. `opportunityId` is the exact consumed
`occurrenceId`. Ordinals are contiguous from one and `nextOrdinal` is exactly the next
row; IDs, opportunity IDs, and decision operation keys are exact/confusable unique.
Producer operation keys repeat only for candidates from the same batch and obey the
same batch agreement plus `(producerOperationKey, producerCandidateOrdinal)` uniqueness
across the union of pending occurrences and receipts. Receipt fingerprints are domain/
versioned and recomputed over every row field except themselves. The closed
`consumedEventSelection` retains the selected ordinal plus the exact six-field semantic
evidence used by `WoundOpportunityEventEvidenceFingerprint`; the parser recomputes
`eventSemanticFingerprint` from it and rejects a merely resealed disagreement. This is
the minimum durable evidence needed to reconstruct a producer batch after one candidate
has moved from pending occurrences into receipts without persisting a second copy of the
complete occurrence. T070 enforces append-
only agreement against the
signed before-image; exact canonical snapshot/write-lease authority is the anti-
truncation boundary used by the rest of client-owned canonical state.

The version-1 `receiptId` seed uses the same length-prefixed writer with domain
`book_of_eternity.mortal_wound.opportunity_receipt_id`, version `1`, then opportunity ID,
decision operation key, and decision fingerprint; the ID is `mortal_wound_receipt_` plus
the seed hash hex. `receiptFingerprint` uses domain
`book_of_eternity.mortal_wound.opportunity_receipt`, version `1`, then every persisted
receipt field in schema order except itself. The seven nested selection fields occur
immediately after `eventSemanticFingerprint` in the hash vector and in their schema
order. Nullable wound/transition coordinates use the writer's null sentinel. The parser
recomputes the selected-event semantic fingerprint, derived receipt ID, and receipt
fingerprint. Source-result, full candidate semantics, opportunity, and decision
seals remain typed input authorities whose exact cross-root agreement is checked by the
pure append/agreement planners; the candidate-to-ID-to-occurrence chain itself is locally
recomputed and is not opaque input.
The receipt parser additionally proves every locally derivable link:
`opportunityId == mortal_wound_occurrence_ + candidate hash hex`, the public occurrence
ref reconstructed from the candidate hash reproduces `occurrenceFingerprint`, and
`operationKey == wound_operation_ + decision hash hex`. It cannot recompute the full
candidate or decision seal from the receipt projection alone, so their signed source and
decision authorities remain mandatory T070 inputs.
The decision draft does not repeat caller-supplied occurrence/source/batch/event fields.
It carries only the decision-turn binding, client-owned opportunity ID used for exact
lookup, opportunity-authority fingerprint, decision authority, and conditional wound/
transition coordinates. The planner itself resolves the occurrence from the current
parsed pending before-state for a new decision, then projects every consumed-source and
selected-event field. A detached occurrence object is never accepted as membership
proof.

The pure decision API is
`PlanConsumeAndAppend(MortalWoundOccurrenceState pendingBefore,
MortalWoundOpportunityReceiptState receiptBefore,
MortalWoundOpportunityReceiptDraft draft, WoundHistoryState plannedHistory)`. Passing
the parsed complete planned history state avoids a forgeable caller projection: `none`
requires no transition with the decision operation key, while `materialize` requires
exactly one ordinary `create|worsen` transition agreeing on transition/wound/turn/event/
operation/source authority. The closed result contains `Disposition`, `Issues`,
`OccurrenceState`, `ReceiptState`, `Receipt`, and `HistoryAgreement`; disposition is
exactly `appended|exact_replay|conflict`. A new accepted decision atomically removes the
exact current pending row and appends its receipt in the two returned after-states.
Exact replay is resolved by opportunity ID in the durable receipt state before pending
lookup, returns a detached original receipt, and changes neither state. A changed replay,
an opportunity absent from both states, or the same tuple in both states conflicts and
returns no after-states. `ValidateConsumedOccurrenceAgreement(pending, receipts)` checks
the complete pending-plus-consumed batch partition, cross-root tuple/identity uniqueness,
and shared source session/request/snapshot token/turn/count/source-result agreement.
For a mixed producer batch, the surviving pending occurrence supplies the immutable
complete ordered event vector, while every consumed receipt supplies its independently
recomputable selected-event evidence. The event composer requires the union to contain
every candidate ordinal exactly once and rejects missing, duplicate, reordered, or
cross-semantics evidence before rebinding the remaining opportunity to a new snapshot.

`MortalWoundOpportunityDecisionBinding` is exactly session ID, request ID, snapshot
token, and turn. `MortalWoundOpportunityReceiptDraft` is exactly that binding,
`OpportunityId`, opportunity-authority fingerprint, decision, decision fingerprint,
operation key, and nullable wound/transition IDs; it has no occurrence object or repeated
consumed-source/event coordinates.

The root rejects malformed, duplicate, reordered, or conflicting rows. A `none` receipt
has null `woundId` and `transitionId` and no wound-history row. A `materialize` receipt
has both exact values and must match exactly one `create|worsen` history row on
`transitionId`, `woundId`, `turn`, `eventRef`, `operationKey`, and
`sourceFingerprint == opportunityAuthorityFingerprint`. Exact cold replay returns the
original detached receipt without a new command or transition. The production adapter
supports that replay while the same active decision snapshot is retained: the signed
snapshot still contains the complete pre-consumption occurrence, while the current
append-only receipt/live pending partition proves whether it was consumed. After
terminal cleanup or preparation of a newer snapshot, the historical source correlation
is stale and is not replay authority. Replay is indexed first by opportunity ID: reusing
the same opportunity with a different decision or any
changed session/request/snapshot/turn/event/fingerprint/operation/transition coordinate
conflicts. A new signed occurrence/snapshot produces a new opportunity. The common
accepted plan composes the receipt after-image together with any wound/effect/history
changes, consumes the pending occurrence, and remains the sole publisher; the adapter
and state distributor never write either canonical root directly.

Before projecting any durable receipt into ordinary response recomposition, the adapter
and `ValidationService` call the same exact-replay classifier with the freshly rebuilt
full opportunity, active turn, expected decision receipt, conditional wound/transition
IDs, and parsed history. It compares the complete opportunity/session/request/snapshot/
turn/event/decision/operation/coordinate authority and then enforces history agreement.
`ValidateHistoryAgreement` also audits every current receipt even when no replay is
requested. A locally self-consistent reseal therefore cannot suppress a new decision or
replace a missing or foreign history transition.

The live receipt/history partition additionally preserves the complete signed history
as an exact ordered prefix. Every history row appended after that prefix must match
exactly one newly appended `materialize` receipt, and every newly appended
`materialize` receipt must match exactly one appended nonterminal `create|worsen` row
on transition/wound/turn/event/operation/source authority. A newly appended `none`
receipt justifies no history row. Replacing a signed row, appending an unreceipted row,
or appending a materialization receipt without its row conflicts before recomposition.

The validation read is one lease-bound strict snapshot over all five wound carriers,
the wound identity and history roots, and the occurrence and receipt roots. It requires
byte-hash authority, one non-conflicting active lifecycle context, and recursive
duplicate-property preflight before `JsonNode` materialization. The dedicated player
and NPC wound roots plus identity, history, occurrence, and receipt roots must remain
byte-identical to their signed before-images. The shared combat-enemy, combat-ally, and
afterlife-profile roots instead preserve an independently parsed canonical projection
of every wound occurrence, owner coordinate, and complete wound envelope; unrelated
ordinary fields in those shared roots may continue through their own accepted mechanic.
A missing/malformed shared root or any changed, added, removed, moved, or duplicated
`activeWounds` authority conflicts. A second owner-only read, text-equal
BOM/encoding mutation of a dedicated root, or duplicate wound ID in another signed
carrier cannot create a different worsening view.

An accepted empty wound stage remains a real sealed stage but contributes no wound
transition or wound effect batch. It must preserve the ordinary effect plan's complete
non-empty `EventInput.events` vector and the resulting canonical effect publication;
it cannot replace that vector with an empty wound event set.

The durable receipt also retains the consumed occurrence's source
session/request/snapshot token/turn, producer operation key and batch ordinal/count,
source-result/candidate/occurrence
fingerprints. The source-result T070 transaction reads both pending occurrences and
receipts as signed before-images before append. For every complete retried batch it
combines pending and consumed rows by producer operation key: all exact candidates mean
no new occurrence, while any changed coordinate/fingerprint conflicts. Thus consumption
cannot reopen the original source result as a fresh opportunity.

## 9. Treatment route model

### Diagnosis path

```json
{
  "diagnosisPathId": "gm-local-stable-within-wound",
  "displayName": "Проверить признаки заражения",
  "visibility": "known_to_player",
  "requiresKnownFacts": [],
  "requirements": [],
  "check": {},
  "reveals": ["route:clean_and_suture", "complication:infection"],
  "failurePolicy": "no_reveal"
}
```

`requiresKnownFacts[]` and `reveals[]` are unique ordered arrays of at most 16 exact
`route:<routeId>` or `complication:<complicationId>` facts that resolve inside the
same wound. The initial
known set is `knownRouteIds[]` plus complications whose visibility is `public` or
`known_to_player`. A `public` or `known_to_player` path becomes structurally available
when all of its known-fact prerequisites are in the set. A `hidden` path requires at
least one such prerequisite and becomes available only after all of them are known. A
`gm_only` path never proves player reachability. Applying available paths adds their
declared facts until a least fixed point is reached. Every hidden route must be in that
fixed point; a self-dependency, an unseeded strongly connected component, a dangling
fact, or a path available only through `gm_only` knowledge is invalid.

This fixed-point check proves only the intra-wound discovery graph. A separate fresh
authority proof must establish that each selected path's item, skill, provider,
facility, location, quest, capability, and other registered world requirements can be
reached in the current realm. The structural parser never scans or guesses world state.

Diagnosis resolution uses one client-sealed attempt authority:

```json
{
  "commandRef": "client-owned",
  "operationKey": "client-owned-retry-safe-key",
  "attemptId": "client-owned",
  "woundId": "exact-active-wound",
  "diagnosisPathId": "gm-local-stable-within-wound",
  "expectedBeforeFingerprint": "sha256:...",
  "pathFingerprint": "sha256:...",
  "requirementAuthorityFingerprint": "sha256:...",
  "checkResultFingerprint": "sha256:...",
  "authorityFingerprint": "sha256:..."
}
```

Its result evidence is:

```json
{
  "result": "success",
  "revealedFacts": ["route:clean_and_suture", "complication:infection"],
  "resultFingerprint": "sha256:..."
}
```

`result` is exactly `success` or `failure`. Success carries the complete ordered
`reveals[]` of one reachable path. Failure carries an empty `revealedFacts[]`, changes
no known fact, and is still a terminal retry-safe attempt. Version 1 supports only
`failurePolicy=no_reveal`; readable failure output may state the need for better
expertise but must not name an unrevealed route or complication.

### Route

```json
{
  "routeId": "clean_and_suture",
  "displayName": "Очистить и ушить рану",
  "visibility": "known_to_player",
  "mode": "procedure",
  "requirements": [],
  "resourcePolicy": {},
  "resolution": {},
  "outcomes": [],
  "interruption": null
}
```

For a Mortal wound, route visibility is closed to `public`, `known_to_player`, or
`hidden`; `gm_only` is not a treatment route because it has no player-reachable
lifecycle. Every public/player-known route ID occurs in `knownRouteIds[]`. A hidden
route ID is absent until already diagnosed, but once revealed it remains in
`knownRouteIds[]` without rewriting the route's authored visibility. Every known or
completed route ID resolves exactly once in `routes[]`, and every completed route is
also known.

The common route object never gains mode-specific top-level aliases. Its exact
`resolution`, `outcomes`, and `interruption` shapes are selected by `mode`:

| Mode | `resolution` | `outcomes` | `interruption` |
|---|---|---|---|
| `procedure` | exactly `formulaKey=mortal_wound_procedure_v1`, integer `difficulty`, `rollSource=accepted_d20`, `criticalPolicy=natural_20_first_natural_1_last`, and closed `modifierSource=fixed_zero|resolved_skill_tier(requirementIndex)` | 2-16 exact categorized inclusive margin bands | required null |
| `course` | exactly `clockKind=world_time.currentTimeInMinutes`, non-negative integer `maximumGapMinutes` | 1-32 contiguous ordinal milestones with increasing `afterMinutes`, closed requirements, exact `category=success`, `active|completed` completion, and ordered result | required exact `category=failed_attempt`/result object |
| `guaranteed` | exactly `capabilityRef` and `actorRole=provider|target` | exactly one category/result object whose category is `success` | required null |

A procedure band has exactly `bandId`, nullable inclusive `minimumMargin` and
`maximumMargin`, `category=success|partial_success|failed_attempt`, and non-empty
ordered `result`. Bands are authored best-to-worst and form a gapless, non-overlapping
partition of all integer margins: the first has null maximum, the last has null
minimum, adjacent finite bounds differ by exactly one, and all intermediate rows have
both bounds. Band IDs are exact and case/Unicode-confusable unique within the route.
Category rank cannot improve as rows descend; the first is `success` and
the last `failed_attempt`. Natural 20 selects index 0 and natural 1 first establishes the
final-index `critical_failure` only after every hard gate succeeds; a prepared shared
Fate Shield may then remap final selection to the first failed index as ordinary
`failure`. Rolls 2-19 use the one inclusive interval containing
the checked 64-bit margin. The closed modifier source is either exact zero/provider roll
actor or the current tier and resolved actor of one indexed satisfied `skill_tier` row.
Every active complication difficulty modifier is added to the authored non-negative
signed-32-bit difficulty. Accepted roll-actor `skill_check` roll effects reduce to
normal/advantage/disadvantage by same-direction collapse and opposite-direction
cancellation; normal claims one die, advantage/disadvantage two, and lower source index
wins an equal-dice tie. The selected natural die is 1-20, modifier is signed 32-bit,
finite band bounds are signed 64-bit, and every sum/narrowing is checked. Overflow or
insufficient unclaimed accepted dice rejects before attempt acceptance.

A course milestone has exactly `ordinal`, non-negative `afterMinutes`, closed
`requirements`, `category=success`, `completion`, and ordered `result`. Ordinal 1 has
`afterMinutes=0`; only the final milestone has `completion=completed`; intermediate
results may be empty because accepting the milestone itself advances course state. The
final result is non-empty, and the complete course contains at least one applicable
positive treatment operation; completing an all-empty course is invalid.
Course common requirements and current-milestone requirements are conjunctive. The
interruption object has exactly `category` and non-empty ordered `result`.
All course clock coordinates, `afterMinutes`, and `maximumGapMinutes` are non-negative
signed 64-bit integers; due/deadline addition is checked and overflow rejects rather
than completing or interrupting a milestone.

Every `result` is an array of at most eight closed typed operations. Procedure,
guaranteed, interruption, and final-course results are non-empty; only an intermediate
course milestone may use an exact empty array. Every result contains at most one `heal`;
when present it is the final operation, so no operation can run after the terminal
follow-up transition. The version-1 operation union is exactly `no_improvement`,
`stabilize`, `add_recovery`, `reduce_severity`, `remove_complication`,
`add_complication`, `apply_deterioration`, and `heal`. Representative compact canonical
rows are:

```json
[
  { "kind": "stabilize" },
  { "kind": "add_recovery", "points": 1 },
  { "kind": "reduce_severity", "steps": 1 },
  { "kind": "remove_complication", "complicationId": "infection_01" },
  { "kind": "apply_deterioration", "policyRef": "missed_care_policy" },
  { "kind": "heal", "legacies": [] }
]
```

`no_improvement` has only `kind` and must be the sole operation. `stabilize` likewise
has only `kind`; recovery points are positive signed 32-bit; severity steps are exactly
one or two; complication removal names one exact currently present complication;
`add_complication` carries the complete closed sub-proposal defined below;
`apply_deterioration` names the exact typed policy introduced by T062. `heal.legacies`
contains at most eight rows.

Every ordered `add_recovery` accumulation and application to signed-64-bit
`Recovery.CurrentStepProgress` uses checked 64-bit arithmetic. Guaranteed aggregate
comparison widens every signed-32-bit operation value and limit to checked signed
64-bit before summing. Any parse-time, simulation-time, resolution-time, or publication-
time overflow rejects before a die is consumed or an after-image is published.
Aggregate `reduce_severity.steps` is at most two when the selected result has no `heal`.
A result ending in its one legal `heal` may aggregate at most three reduction steps, but
only to reach a working severity of exactly I before the separate follow-up heal. T067
retains the existing non-healing reduction guard and updates the follow-up-heal gate to
validate this sealed ordered working-state contour.

The operation shown above is the canonical persisted dialect: it has
`complicationId`, never a response-local ref. A GM-authored initial wound proposal uses
exactly `{ "kind": "remove_complication", "complicationRef": "..." }`; that ref must
resolve case/confusable-exactly to one complication row in the same proposal. A later
`author_alternative_treatment` packet exposes bounded opaque existing-complication
selector refs, and the GM uses the same `complicationRef` field to copy exactly one of
them. `WoundResponseInputComposer` is the sole adapter: after allocating initial IDs or
resolving an alternative packet selector, it rewrites the operation to canonical
`complicationId` before `WoundMaterializationContract` parses/persists the route.
Canonical state rejects `complicationRef`; GM proposal input rejects `complicationId`.
Unresolved, duplicate, cross-wound, or confusable refs reject the complete proposal.

The exact closed version-1 `heal.legacies` union is:

```json
[
  {
    "localLegacyRef": "thin_scar",
    "kind": "cosmetic",
    "readableSummary": "После раны остался тонкий шрам."
  },
  {
    "localLegacyRef": "restricted_grip",
    "kind": "mechanical_effect",
    "readableSummary": "Повреждённая кисть хуже переносит длительное напряжение.",
    "effectDraft": {
      "schemaVersion": 1,
      "definitions": [
        {
          "definitionRef": "restricted_grip_definition",
          "definition": "complete detached #1535 definition with links=[]"
        }
      ],
      "applications": [
        {
          "applicationRef": "restricted_grip_root",
          "definitionRef": "restricted_grip_definition",
          "parameters": {}
        }
      ]
    }
  }
]
```

A cosmetic draft has exactly `localLegacyRef`, `kind=cosmetic`, and bounded
`readableSummary`. A mechanical draft has exactly those first three fields with
`kind=mechanical_effect` plus `effectDraft`. The effect draft has exactly
`schemaVersion=1`, `definitions`, and `applications`, each a non-empty bounded array.
Each array contains from one through five rows, reusing
`WoundMaterializationContract.MaxOwnedEffectDefinitions` and
`MaxOwnedEffectRootBindings` respectively.
Every definition wrapper has exactly response-local `definitionRef` and one complete
detached valid #1535 `definition` whose `links` is exact empty `[]`; every application
has exactly response-local `applicationRef`, same-draft `definitionRef`, and complete
closed `parameters`. Definition refs, definition keys, and application refs are each
exact/confusable unique within that one legacy draft; every application resolves once inside the same draft, external
definitions are forbidden, and every unbound definition is reachable under the normal
#1535 graph rules. Target is derived from the treatment owner. Source kind/ID, accepted
event, operation ordinal, stack/lifetime carrier state, materialization fingerprints,
effect/transition IDs, and history coordinates are client-derived and forbidden in the
authored draft.
Version 1 deliberately has no open `entityKind`, `payload`, `skill`, `trait`, or `other`
branch: every arbitrary lasting mechanic is represented by the already complete effect
component language, while readable non-mechanical aftermath is cosmetic.
Every `localLegacyRef` is an exact identifier and the complete selected result is exact
and Unicode-confusable unique by that ref; collision is rejected structurally before
identity allocation or a roll. Because a result permits only one final `heal`, the
combined cosmetic plus mechanical legacy count is an aggregate maximum of eight for the
whole selected result, never a per-operation allowance.

The identity allocator namespaces every mechanical-legacy nested technical reference as
`localLegacyRef/localRef`; different legacy drafts may reuse their local definition or
application spellings without collision. The persisted application result map and every
#1535 handoff use only those namespaced references. The allocator rejects a collision
between any namespaced reference, derived permanent identity, or existing working-wound
source coordinate before creating an effect plan.

`add_complication` contains one complete `complicationDraft` with exactly
`complications` and `consequenceDefinitions`. It is the existing GM-safe wound proposal
sub-shape: `complications` has exactly one current response-local complication row and
each consequence row is the existing exact `definitionRef/definition/root` wrapper;
every non-null root's current `ownership` names that one complication ref and its current
`slots` are complete. Definitions have `links=[]`; the existing adapter alone derives
source links, application/operation refs, IDs, slot ordinals, and fingerprints. The
consequence array may be empty for a non-effectful complication. Its local references
are exact/confusable unique and an effectful graph obeys current #1535 and wound
slot/severity bounds. A parallel `ownedEffectDraft`, accepted root application,
result-map field, string token, complication-name lookup, raw mutation, or unknown
operation is invalid.

Within one selected result, the sole complication ref from every `add_complication`
operation is exact and Unicode-confusable unique across all such operations. The adapter
namespaces each nested definition/application/derived operation ref by the selected
operation ordinal plus that complication ref, and rejects any collision with another
selected operation or the working wound's existing source graph. Local spellings inside
different complication drafts may otherwise repeat because only their namespaced form
crosses the accepted-effect boundary.

`MortalWoundTreatmentOutcomeIntent` is the exact immutable T067 -> T070 handoff. It is a
closed union with common public properties exactly `OperationOrdinal`, `Kind`,
`DeclaredOperationFingerprint`, and `IntentFingerprint`. Its branch payloads are:

- `no_improvement` and `stabilize`: no additional fields;
- `add_recovery`: positive `Points`;
- `reduce_severity`: `Steps=1|2`;
- `remove_complication`: exact canonical `ComplicationId`;
- `apply_deterioration`: exact `PolicyRef` and recomputable
  `DeteriorationAuthorityFingerprint` from T069;
- `add_complication`: authored `ComplicationRef`, deterministic permanent
  `ComplicationId`, frozen ordered `DefinitionReferenceBindings` and
  `ApplicationReferenceBindings` mapping every local ref to its operation-ordinal/
  complication-ref namespaced ref, plus `PreparationFingerprint`;
- `heal`: immutable `HealChildCoordinates`, frozen ordered `LegacySeedBindings`, and
  `PreparationFingerprint`. Each seed contains exactly zero-based `LegacyOrdinal`,
  `LocalLegacyRef`, deterministic permanent `LegacyId`, `Kind`, frozen ordered local-to-
  namespaced definition/application reference bindings (empty for cosmetic),
  `DeclaredLegacyFingerprint`, and `SeedFingerprint`.

There is exactly one intent for every operation in `DeclaredResult`, in identical order,
including `no_improvement`; no extra or omitted intent is legal. The resolver recomputes
each declared-operation fingerprint from the already typed route result and requires
exact kind/value/ref agreement. Permanent complication/legacy IDs and all namespaced
refs derive only from the full request fingerprint, operation ordinal, and exact local
ref. T070 consumes these typed intents plus the matching typed `DeclaredResult`; it may
build the ordinary wound/#1535 batches but may neither parse raw route JSON nor allocate,
rename, or reinterpret an ID/ref. Cardinality, ordering, binding, or fingerprint
disagreement rejects before after-image composition.

When selected, permanent complication and legacy IDs are deterministic functions of the
sealed request fingerprint, operation/legacy ordinal, and exact local ref. A mechanical legacy's one durable
`legacyId` is also its source ID under the new client-only
`sourceKind=wound_legacy`. That source is non-public and non-GM-materializable. During
same-turn planning its authority is exactly the sealed T067 legacy seed plus immutable
legacy preparation; after finalization, the accepted typed `legacy` history row linked to
the terminal wound is its sole durable reload authority. The row persists the complete
definition graph, namespaced root applications, wound ID, terminal transition ID, owner,
realm, accepted application result map/effect-plan seal, and source fingerprint. The accepted #1535 effect
planner remains the sole effect-ID allocator. Healing terminates only effects owned by
the active `sourceKind=wound` graph and cannot capture the independent
`wound_legacy` source; later dispel/removal may end its effect but never deletes its
legacy/history/replay provenance.

`MortalWoundHealLegacyPlanner.Prepare(WoundAcceptedTurnBinding binding,
MortalWoundTreatmentResolution resolution, WoundMaterializationEnvelope workingWound)`
is the sole preparation API. It returns
`MortalWoundHealLegacyPreparationResult` with exactly `IsValid`, frozen `Issues`, and
nullable immutable `Preparation`. The preparation contains exactly immutable
`LegacyDraftBindings`, frozen ordered `EffectOperationBatches`, and a production-owned
`PreparationFingerprint`; it contains no effect ID and no durable history intent. The
array has exactly one immutable `WoundEffectOperationBatch` for each
`mechanical_effect` binding in ascending legacy ordinal and no row for a cosmetic
binding. Every batch exports one and only one `sourceKind=wound_legacy` source whose
source ID equals that binding's deterministic `LegacyId`; no batch may merge definitions
or applications from distinct legacy sources. Namespaced refs, source export, seed,
draft binding, and batch seals agree exactly. An all-cosmetic/empty legacy set produces
an exact empty batch array. `PreparationFingerprint` binds both ordered collections.

After #1535 planning, the sole completion API is
`MortalWoundHealLegacyPlanner.Finalize(MortalWoundHealLegacyPreparation preparation,
EffectAcceptedTurnPlan acceptedEffectPlan)`. It returns
`MortalWoundHealLegacyFinalizationResult` with exactly `IsValid`, frozen `Issues`, and
nullable immutable `Finalization`. The finalization contains exactly immutable resolved
`LegacyBindings`, typed durable `HistoryIntents`, the complete ordered per-legacy
`ApplicationResults`, `EffectPlanFingerprint`, and `FinalizationFingerprint`. It
recomputes the preparation, every batch/source boundary, the effect-plan seal, and every
namespaced `applicationRef -> effectId/materializationFingerprint` agreement before any
terminal heal or legacy history row can be published. Unknown branch, missing/extra
field, ID collision, missing/extra/reordered/merged/split batch or result-map entry, or
source/history mismatch rejects the whole atomic plan.

Finalization validates the production accepted-plan seal before classifying topology,
then compares the ordered structural batch key: every mechanical batch's ordered
`localLegacyRef` plus its ordered local root-application refs and their grouping. This
precedes request-derived namespaces, source seals, and exact seed/source/ref/request
agreement. Only a real structural difference emits
`mortal_wound_legacy_topology_mismatch`; a same-topology foreign request rejects without
that code.

`ApplicationResults` is exactly a frozen ordered array of
`MortalWoundHealLegacyApplicationResultGroup`, one row per prepared mechanical batch in
the same order and no row for a cosmetic legacy. Each group contains exactly
`LegacyOrdinal`, `LegacyId`, `SourceExportFingerprint`, frozen ordered non-empty
`Results` using the existing complete `EffectAcceptedApplicationResult` type, and
`ResultGroupFingerprint`. Result order equals the matching batch's root-application
order, and the group fingerprint binds every complete accepted result rather than an
effect-ID-only projection.

The selected request owns the primary `treat` operation/event coordinates. A terminal
follow-up heal derives one child operation key, transition ID, event ref, and causal ref
from the complete sealed request plus fixed role `heal`; each legacy row derives its own
coordinates from that same request plus fixed role `legacy`, zero-based legacy ordinal,
and exact `localLegacyRef`. These versioned client-owned derivations are pairwise exact/
confusable unique and every enclosing history row must agree with them. Any collision
with existing command/history/event coordinates rejects the whole plan before
publication. Exact replay returns its receipt before planning and therefore regenerates
none of the treat/heal/legacy rows or child coordinates.

Operations evaluate in authored order against one working wound. Every selectable
outcome must be applicable before its roll is consumed. `heal` is valid only after prior
operations have reached severity I and becomes a separate canonical follow-up heal;
legacies cannot appear outside it. An interruption is exactly category
`failed_attempt` and either sole `no_improvement` or only complete harmful
`add_complication`/`apply_deterioration` operations. It cannot produce a beneficial
state change.

The exact positive-operation set is an applicable state-changing `stabilize`,
`add_recovery`, `reduce_severity`, `remove_complication`, or `heal`.
`no_improvement`, `add_complication`, and `apply_deterioration` are never positive. A
course's mechanical completeness is checked by evaluating every milestone result in
ordinal order against one copy of the starting working wound; at least one operation in
that sequence must be positive when reached. Each real milestone later revalidates its
operations against current canonical state before acceptance.

For interruption specifically, `add_complication` must contain exact empty
`consequenceDefinitions` and its sole complication's
`treatmentDifficultyModifier` must be 1-4. An effectful or zero-difficulty complication
is not provably adverse and is invalid. `apply_deterioration` must resolve to a T062
typed policy whose transition is strictly worsening; neutral or beneficial policy is
invalid.

A guaranteed route contains exactly one matching `source_capability` requirement with
the same `capabilityRef` and `actorRole` as its resolution. Its singleton outcome is
not a caller or GM success assertion: the resolver must prove an active matching
canonical skill capability proof and validate every operation against its declared
limits before selecting it. Its result contains at least one applicable positive
operation and cannot contain `no_improvement`, `add_complication`, or
`apply_deterioration`. A transient capability row alone is insufficient.

### Alternative treatment authoring authority

A later world-specific cure is not smuggled through `diagnose`. Before asking the GM
to author anything, the client seals one transient request authority:

```json
{
  "schemaVersion": 1,
  "sessionId": "exact-session",
  "requestId": "exact-request",
  "snapshotToken": "exact-snapshot",
  "authoringRequestRef": "client-owned",
  "operationKey": "client-owned-retry-safe-key",
  "woundId": "exact-active-wound",
  "eventRef": "accepted-evidence-event",
  "expectedBeforeFingerprint": "sha256:...",
  "evidenceAuthorityFingerprint": "sha256:...",
  "authorityFingerprint": "sha256:..."
}
```

After a strict GM response resolves that request, the client re-resolves all route/path
requirements and seals accepted transition evidence:

```json
{
  "authoringRequestRef": "client-owned",
  "requestAuthorityFingerprint": "sha256:...",
  "operationKey": "client-owned-retry-safe-key",
  "woundId": "exact-active-wound",
  "eventRef": "accepted-evidence-event",
  "addedRouteId": "new-complete-route",
  "addedDiagnosisPathId": null,
  "expectedBeforeFingerprint": "sha256:...",
  "expectedAfterFingerprint": "sha256:...",
  "routeFingerprint": "sha256:...",
  "diagnosisPathFingerprint": null,
  "evidenceAuthorityFingerprint": "sha256:...",
  "requirementAuthorityFingerprint": "sha256:...",
  "authorityFingerprint": "sha256:..."
}
```

The matching accepted command result is:

```json
{
  "decision": "author",
  "route": {},
  "diagnosisPath": null,
  "resultFingerprint": "sha256:..."
}
```

The accepted `author_alternative_treatment` transition appends exactly the bound route
and one new history row. A visible route is appended to `knownRouteIds[]`. A hidden
route leaves `knownRouteIds[]` unchanged and requires one newly appended diagnosis path
whose fixed-point and fresh world-authority proofs are valid. Existing routes,
diagnosis paths, completed/known route order, and all earlier history rows are
byte-identical. Replacement, reordering, deletion, a second route/path, or an unbound
route/path fingerprint fails closed.

The GM-facing response uses one closed top-level array whose entries correlate only by
the safe opaque `authoringRequestRef`:

```json
{
  "woundTreatmentAuthorings": [
    {
      "authoringRequestRef": "safe-opaque-request-ref",
      "decision": "author",
      "route": {},
      "diagnosisPath": null
    }
  ]
}
```

`decision` is `author` or `decline`. `author` requires one complete route and requires
exactly one complete diagnosis path when that route is hidden; it requires null
`diagnosisPath` for a visible route. `decline` requires both payloads null and creates
no transition. The client, not the GM, resolves the safe request reference to the
sealed request authority, validates and seals the accepted transition evidence, writes
the accepted command, and allocates transition/history identity. A route/path
fingerprint is never accepted from the pre-response request or the GM.

### Requirement union

Each requirement has `kind`, exact reference/evidence fields, and a closed predicate.
Allowed kinds are:

- `item_quantity` and `resource_quantity`;
- `skill_tier`, `spiritual_art_tier`, and `source_capability`;
- `provider`, `consent`, and `relationship`;
- `facility`, `location`, and `reachable`;
- `quest_state` and `effect_state`;
- `elapsed_time`, `environment`, and `ordered_prior_step`.

All requirements within one route are AND. Different routes are OR. References are
re-resolved against the fresh snapshot immediately before commit. Removing a referenced
capability while a route/course is active fails closed unless the route declared an
interruption transition.

For Mortal version 1, the authority-resolved members frozen by T060/T066 have no
implicit role defaults. Their exact closed shapes are:

| `kind` | Required fields after `kind` |
|---|---|
| `item_quantity` | exact permanent `itemRef`, positive integer `quantity`, `ownerRole=provider|target` |
| `resource_quantity` | exact registered `resourceRef`, positive integer `quantity`, `ownerRole=provider|target` |
| `skill_tier` | exact materialized `capabilityRef`, integer `minimumTier`, `actorRole=provider|target` |
| `source_capability` | exact materialized `capabilityRef`, `actorRole=provider|target` |
| `provider` | selected exact permanent `providerRef` |
| `consent` | exact current `consentRef`, exact `providerRef`, exact `targetRef` |
| `facility` | exact materialized `facilityRef` |
| `location` | exact materialized `locationRef`, `targetRole=target` |
| `quest_state` | exact materialized `questRef`, exact `requiredState` |
| `effect_state` | exact active `effectRef`, exact `requiredState`, `targetRole=target` |
| `environment` | exact registered `environmentRef`, exact `requiredState` |

The roles bind only to the already selected typed treatment context. They are required,
cannot contain actor identities, and cannot be inferred from names, prose, or route
mode. `source_capability.actorRole` therefore explicitly decides whether the provider
or target must own the capability. Facility/environment rows bind the exact current
location; location/effect rows also bind the exact target. Requirement objects never
carry reservations, mutations, consumed values, or caller-authored authority seals.

### Transient Mortal treatment authority projection

T060/T066 use a closed internal version-1 adapter projection. It is parsed by
`MortalWoundTreatmentAuthority.ParseContext` / `ParseSnapshot`, exists only in memory,
and is never canonical game state or GM-authored input. The context has exactly:

```json
{
  "schemaVersion": 1,
  "realm": "mortal_world",
  "targetKind": "player",
  "targetId": "exact-permanent-actor-id",
  "providerKind": "npc",
  "providerId": "exact-permanent-actor-id",
  "currentLocationId": "exact-materialized-location-id"
}
```

Target/provider identity is always the ordinal pair `(actorKind, actorId)`; actor IDs
are not assumed globally unique across kinds. For procedure/course treatment either
coordinate may identify a player, named NPC, exact current `combatant`, or exact current
`combatant_member`; all four are legal physical-wound targets and provider/target may be
the same typed coordinate for self-treatment. Combat coordinates are read only from the
accepted combat root and carry its existing promotion authority; display names or member
array indices never substitute for IDs. A guaranteed capability proof remains sourced
only from a canonical player/NPC skill: a combatant wound may use a player/NPC provider
guarantee, while `actorRole=target` first requires the existing accepted persistence/
promotion transition to a canonical player/NPC owner. The snapshot has exactly
`schemaVersion=1`, exact `snapshotToken`, and the arrays `items`, `resources`, `actors`,
`facilities`, `locations`, `quests`, `effects`, and `environments`. Every object below
is closed. `displayName` is required diagnostic text but never authority or fingerprint
input. `lifecycle` is `active|retired`; `active=false` represents a current disabled or
otherwise unusable live row independently from permanent retirement.

| Array/member | Exact fields |
|---|---|
| `items[]` | `itemId`, `displayName`, `realm`, `ownerKind`, `ownerId`, positive `count`, non-negative `availableCount <= count`, `reservationState=available|reserved`, `lifecycle`, `active` |
| `resources[]` | `resourceRef`, `displayName`, `realm`, `ownerKind`, `ownerId`, non-negative `currentValue`, non-negative `availableValue <= currentValue`, `reservationState=available|reserved`, `lifecycle`, `active` |
| `actors[]` | `actorKind`, `actorId`, `displayName`, `realm`, `currentLocationId`, `lifecycle`, `active`, `reachable`, and closed `skills`, `capabilities`, `consents` arrays |
| `actors[].skills[]` | `capabilityRef`, `displayName`, integer `tier`, `lifecycle`, `active` |
| `actors[].capabilities[]` | `capabilityRef`, `displayName`, `lifecycle`, `active` |
| `actors[].consents[]` | `consentRef`, `displayName`, `providerKind`, `providerId`, `targetKind`, `targetId`, `status=granted|withdrawn`, `lifecycle`, `active` |
| `facilities[]` | `facilityId`, `displayName`, `realm`, `locationId`, `lifecycle`, `active`, `available` |
| `locations[]` | `locationId`, `displayName`, `realm`, `lifecycle`, `active`, closed `presentActors[]` |
| `locations[].presentActors[]` | exact `actorKind`, exact `actorId` |
| `quests[]` | `questId`, `displayName`, `realm`, exact current `state`, `lifecycle`, `active` |
| `effects[]` | `effectId`, `displayName`, `realm`, `targetKind`, `targetId`, exact current `state`, `lifecycle`, `active` |
| `environments[]` | `environmentId`, `displayName`, `realm`, `locationId`, exact current `state`, `lifecycle`, `active` |

The adapter that assembles this projection must source each field from the corresponding
already-validated canonical authority. Parsing never grants authority by itself. All
identities and state strings are exact ordinal identifiers; counts/available values and
tiers are integers. Arrays and nested collections are detached and immutable after
parse; no parsed member retains a raw `JsonNode`, `JsonElement`, or `JsonDocument`.
Missing/unknown fields, wrong types/version, an invalid context realm, or
contradictory numeric bounds reject the projection. A structurally well-formed foreign-
realm row or duplicate exact row is retained: the resolver must reject it against the
selected requirement with a reference-specific cross-realm or ambiguity issue instead
of letting a parser silently choose one. Resolver output remains the separately sealed
evidence described in the plan and cannot expose mutation or reservation intents.

Canonical-to-transient mapping is closed:

- current item carrier plus identity/current-transition quantity agreement projects only
  active carried rows; before T068, canonical count is both total and available and the
  reservation state is `available`;
- the resource definition/state/history/recomposed-owner quartet projects only
  `mortal_world`, integer, non-negative signed-32-bit rows owned by
  `player|npc|combatant|combat_group_member`; the last kind maps to
  `combatant_member`. Active rows expose their current value as available; suspended rows
  remain `lifecycle=active`, `active=false`, available zero. Valid decimal, non-actor,
  foreign-realm, negative, or out-of-range siblings are omitted rather than invalidating
  unrelated accepted authority;
- player/NPC active/passive catalog membership supplies active lifecycle. Player active
  tier comes from the exact canonical mastery row, NPC active tier from
  `currentMasteryLevel`, and passive tier from `masteryLevel`. A valid active skill with
  no applicable mastery emits no tier row and never receives an invented zero/test tier;
- exact retained regular `quests[]` members map `status` to transient `state` and remain
  current/active query rows even when the status is completed or failed;
- an exact known NPC outside `NPCsInScene` remains an actor with `reachable=false`; only
  exact scene membership grants presence/reachability. Combat presence comes only from
  signed enemy/ally roots plus combat identity/promotion agreement, never a wound.

The parsed `Context` has private parser-origin provenance over its version and visible
coordinates. Accepted-state export requires version 1 and recomputes that provenance;
direct construction, a forged copy, or a missing/mismatched parser seal is invalid.

The completed T060 projection and its resolved row shape remain unchanged. In
particular, transient `actors[].capabilities[]` rows do not carry treatment guarantees
or source-contract fingerprints and parsing such a row never grants provenance.

### Canonical Mortal treatment scene states

The exact accepted current Mortal location may register treatment-only scene authority
inside its existing location `customStates[]`. Only the following three exact `kind`
values grant authority; every recognized object is closed and has `schemaVersion=1`:

| `kind` | Exact remaining fields | Projection rule |
|---|---|---|
| `mortal_wound_treatment_facility` | exact `facilityId`, non-empty `displayName`, boolean `available` | realm/location are inherited from the independently validated exact current location; lifecycle=`active`, active=`true` |
| `mortal_wound_treatment_environment` | exact `environmentId`, non-empty `displayName`, exact setting-specific `state` | realm/location are inherited from the exact current location; lifecycle=`active`, active=`true` |
| `mortal_wound_treatment_consent` | exact `consentRef`, non-empty `displayName`, `providerKind`, exact `providerId`, `targetKind`, exact `targetId`, `status=granted|withdrawn` | the exporter must resolve both typed actors exactly once in the same current location; lifecycle=`active`, active=`true`; the row is attached to the exact provider actor in the transient projection |

Actor kinds are exactly `player|npc|combatant|combatant_member`. Facility IDs,
environment IDs, and consent refs are exact/confusable-unique within their registered
kind. Environment state is an exact setting-specific value bound to one environment ID,
but two distinct environment IDs may share it. Unrecognized location `customStates[]`
rows remain setting data and
cannot satisfy a treatment requirement. A recognized malformed, duplicate, confusable,
wrong-location, or unresolved-actor row invalidates scene authority; it is not ignored.
Removing a registered row makes that exact authority absent. Retaining a facility with
`available=false` or consent with `status=withdrawn` preserves trustworthy negative
evidence for the unchanged T060 classifier. No recognized row may carry lifecycle,
reservation, fingerprint, location, or client-owned authority fields.

These kinds are reserved to location containers. A raw or canonical location, a same-turn
new-location candidate, and `worldMapUpdates.locationUpdates[]` validate them through an
explicit location-container entry point. A link `customStates[]` containing a reserved
treatment kind is invalid. For an existing location the update supplies the complete
replacement array and preserves unrelated siblings; new selected/remote locations carry
the complete array in their ordinary full creation envelope.

### Canonical Mortal wound-treatment skill capability

A guaranteed route uses a separate optional extension on a canonical current player or
NPC active/passive skill row:

```json
{
  "skillId": "skill_field_medicine_01",
  "mortalWoundTreatmentCapabilities": [
    {
      "schemaVersion": 1,
      "capabilityRef": "field_medicine_guaranteed_care",
      "woundDomain": "physical",
      "minimumSeverityRank": 1,
      "maximumSeverityRank": 2,
      "operationLimits": {
        "mayStabilize": true,
        "maximumRecoveryPoints": 1,
        "maximumSeverityReductionSteps": 1,
        "removableComplicationKinds": ["bleeding", "infection"],
        "mayHealAtSeverityI": true,
        "maximumCosmeticHealLegacies": 1,
        "maximumMechanicalEffectHealLegacies": 1
      }
    }
  ]
}
```

The snippet shows only the wound-specific extension; the containing skill must still be
a complete valid active/passive skill. An extension-bearing skill has a required exact
permanent `skillId`; idless skills remain legal under the pre-#1533 ordinary skill
contract but cannot prove a guarantee. Each extension-bearing row's `skillId` must be
exact and case/Unicode-confusable unique against every current active and passive skill
row for that actor, including rows without the extension; duplicate/confusable active-
versus-passive identities invalidate the extension authority. Capability refs are exact
and case/Unicode-confusable unique across both skill kinds for one actor. Ranks are 1-4 with minimum no
greater than maximum; numeric operation limits are non-negative signed
32-bit values, severity reduction is at most three, each legacy count is 0-8, and their
sum is at most the exact eight-row wound legacy bound. Complication kinds use the closed wound complication
enum. `maximumRecoveryPoints`, `maximumSeverityReductionSteps`, and both legacy limits
bound aggregate sums/counts across the whole ordered singleton guaranteed result, not
each operation independently; repeated operations cannot multiply an allowance.
`remove_complication` must match the declared kind set and the global eight-operation
bound, and at most one `heal` operation is legal. At least one positive operation limit
must be non-zero/true/non-empty; an all-zero capability is not a guarantee.
`woundDomain` is exactly the literal `physical` in schema version 1; `spiritual`, an
unknown domain, or a caller-extensible domain value rejects the entire capability row.

The player source rows are the fully composed current entries in
`game_state/player/skills_active.json.activeSkillChanges[]` and
`game_state/player/skills_passive.json.passiveSkillChanges[]`; NPC rows are the selected
current NPC's `activeSkills[]` and `passiveSkills[]`. Delta payloads, backups, GM response
objects, display names, and the T060 transient snapshot are not source authority.

The same exact current extension-bearing skill row is also the sole canonical source for
the route's ordinary T060 `source_capability` predicate. The T066 snapshot composer
deterministically projects one unchanged-shape `actors[].capabilities[]` row with exactly
`capabilityRef`, diagnostic `displayName` from the current skill, current `lifecycle`, and
current `active`; it projects no guarantee fields or fingerprints. Missing, duplicate,
idless, inactive, or ambiguous source skills cannot manufacture that row. T060 then
resolves the ordinary row unchanged, while the separate canonical exporter below proves
the guarantee; callers cannot supply two unrelated capability sources with the same ref.

`MortalWoundTreatmentCapabilityAuthority` exports an immutable proof only from the
exact current canonical player/NPC owner and skill row. The proof contains exactly
`SnapshotToken`, `SourcePath`, `OwnerKind`, `OwnerId`, `SkillKind`, `SkillId`, `CapabilityRef`,
`WoundDomain`, `MinimumSeverityRank`, `MaximumSeverityRank`, the immutable operation
limits, `ContextFingerprint`, `AcceptedStateFingerprint`, `CoordinatesFingerprint`,
`SourceSemanticFingerprint`, and `ProofFingerprint`. Presence/removal, exact
owner/skill/capability identity, and every mechanical limit participate in the
fingerprints; display text does not. A route that needs a mastery/tier gate declares an
ordinary sibling `skill_tier` requirement and leaves that proof to unchanged T060. A
JSON shape parser is only a strict fixture adapter and cannot create an accepted proof.
The full skill materialization feature
#1533 must preserve this extension rather than replace it with a parallel guarantee.

Accepted proofs are created only by
`MortalWoundTreatmentCapabilityAuthority.ExportCurrent(acceptedState, coordinates,
capabilityRef, actorRole)`. The lease-bound accepted state selects the exact player/NPC
owner and fixes the source path to the composed current player skill files or
`game_state/npcs/npc_core.json`; the result has exactly `IsValid`, frozen `Issues`, and
nullable `Proof` in `MortalWoundTreatmentCapabilityProofResult`. No API accepts source
JSON or a caller source/proof fingerprint.

Immediately before publication T070 calls
`MortalWoundTreatmentCapabilityAuthority.ExportForPublication(acceptedState,
coordinates, capabilityRef, actorRole, AcceptedMechanicsPlan publicationPlan)`. It has
the same exact result shape. If the final plan touches the selected player's active or
passive skill root, or the selected NPC skill root, the exporter strictly validates and
reads that final after-image; otherwise it reads the current root under the same lease.
It accepts no detached JSON. The final proof and source semantic fingerprint must exactly
equal the request's sealed proof, so a same-turn plan cannot consume a guarantee while
also removing, retiring, duplicating, or changing its skill/capability authority.

### Resource policy

```json
{
  "reserveBeforeResolution": true,
  "consumeOn": ["success", "partial_success", "failed_attempt"],
  "refundOn": ["cancelled", "validation_failed", "rolled_back"],
  "mutations": [
    { "kind": "consume_requirement", "scope": "common", "milestoneOrdinal": null, "requirementIndex": 0 }
  ]
}
```

Each mutation is only a selector for the full quantity of an exact authored item/resource
requirement; it is not a raw delta. Quantity requirements without selectors are still
reserved as tools/preconditions and then released. Course-milestone selectors also name
their exact ordinal. Resource/item mutations use their established typed authorities. A
route cannot write inventory or resource ledger values directly.

### Resolution and outcome bands

`procedure` binds a registered roll/check formula, one closed modifier source, and closed
ordered bands. Its production check authority contains the lease-bound roll actor,
normal/advantage/disadvantage contributions, durable contiguous pool claim, selected
natural die, requirement-derived modifier, summed complication difficulty, and seals.
The resolver derives checked 64-bit total and margin. Requirement, wound/route,
before-state, accepted-state, and roll seals have precedence over natural criticals.
Natural 20 selects the first band; natural 1 first establishes last-band critical
failure, then only a prepared Fate Shield may remap it to the first failed band; rolls
2-19 select by margin.
Caller-provided dice/index/modifier/total/margin/category/band/outcome/terminal fields are
invalid authority.

`course` binds an exact dose/milestone sequence. For milestone `n`, its inclusive
window is `[startedAt + afterMinutes, due + maximumGapMinutes]` under exact canonical
`world_time.currentTimeInMinutes` authority. Too early rejects without a terminal
attempt. After the too-early gate, the separate scoped requirement authority always classifies
fresh common/current-milestone evidence as `Satisfied`, `Unsatisfied`, or
`InvalidAuthority`: only trustworthy live predicate loss is `Unsatisfied`; malformed,
ambiguous, cross-realm, changed-coordinate/history, or fingerprint evidence is invalid
and rejects. Too late or `Unsatisfied` on an active course selects the declared
interruption; when both apply, `deadline_exceeded` dominates the interruption reason.
Exact-deadline `Satisfied` completion wins. Every accepted milestone/interruption is a terminal attempt,
but only final completion adds the route to `completedRouteIds[]`. Typed treatment
history stores sufficient route/course/clock evidence to reconstruct the next legal
milestone after reload.
Ordinal 1 requires `activeCourseId=null` and stores complete course-start authority with
the typed starting wound; continuation requires the agreeing pointer and next contiguous
history ordinal. A second course rejects. Procedure/guaranteed treatment may occur while
active, but heal/final completion/interruption clears the pointer.

`guaranteed` binds both a successful unchanged T060 `source_capability` row and a
separate canonical skill capability proof. Owner/role/capability identity, permanent
skill ID, source fingerprint, exact `woundDomain=physical`, rank, and sibling requirements
must agree. Each singleton result operation must remain within the proof's exact
operation limits and the singleton must make an actual monotone improvement against the
sealed current wound. No roll fields exist for this mode, and no natural critical, transient
JSON guarantee, or GM override participates.

Outcomes use only the typed closed operation union above. No outcome may reopen a healed
wound, change owner/domain, smuggle a name-derived complication, or carry an incomplete
mechanical payload. Ordered operations evaluate against a working state. `heal` is legal
only once that working state is severity I and schedules the separate canonical heal
transition with its complete legacy drafts; enough declared reductions may precede it,
but it never skips severity ranks. Every result has at most one final `heal`; aggregate
recovery/capability arithmetic is checked, and aggregate severity reduction is <=2
without heal or <=3 only to stage a final heal from working rank I. `no_improvement` is
exclusive and changes no wound mechanics.

Every procedure band categorized `success` structurally contains a positive kind and no
no-op/harm kind. Before each fresh attempt every band independently simulates against
the sealed current wound and must be a legal complete transition; every success band
must additionally produce at least one actual monotone improvement. The first/natural-20
band is not exempt. An inapplicable partial/failed band blocks the attempt before its
sealed die can become a free reroll, without invalidating the persisted route shape. A success-category
`no_improvement` or a result containing only complication/deterioration operations is invalid, so
`completedRouteIds[]` cannot record a no-op as completed. Partial and failed bands may
remain non-improving when otherwise legal.

The pure resolution boundary returns immutable typed outcome intents and one
`consumptionTrigger=success|partial_success|failed_attempt|none`; it never returns raw
item/resource mutations. The trigger equals the selected category only when the sealed
policy includes it; otherwise it is `none`. A course interruption always uses `none`, so
neither its unmet current milestone nor future milestones are consumed. T068 alone
prepares the pre-resolution reservation authority and finalizes release/typed mutations.

Treatment replay is independently unique by operation key, every non-null attempt ID,
and every non-null `(courseId, milestoneOrdinal)` pair. An exact repeat returns the
existing typed receipt. Reusing any coordinate with changed roll, time, route, wound,
requirement, capability, outcome, or result fingerprint conflicts. Procedure and
guaranteed attempts have no course coordinates; course milestones and interruptions
have both. Attempt terminality is derived from an accepted resolution; it is never a
caller-authored `TerminalAttempt` boolean and is distinct from terminal wound healing.

Replay/conflict lookup runs before fresh-state resolution. An exact replay is proven
from the original sealed semantic request and typed history result and returns that
receipt without consulting resources/capabilities already consumed or the current wound
after-image; it emits no intent. A new semantic operation alone proceeds to current
before-state and requirement validation. No durable row means validation failure,
cancellation, or rollback cannot take this replay path.

`completedRouteIds[]` records first successful completion in append order and never
disables an otherwise legal later attempt. A procedure appends only for category
`success`; a course only at its final `completion=completed` milestone; a guaranteed
route on its accepted singleton success. Partial/failed procedures, intermediate course
milestones, interruptions, and exact replays do not append. `RouteCompletion=AppendOnce`
iff this attempt is one of those qualifying successes and the route ID is absent from
the sealed before-image; a later qualifying success for an already recorded route uses
`None`. The composer independently derives the same value, so the ID remains unique and
the resolution/history fingerprint is deterministic.

## 10. Spiritual conflict additions

### Conflict danger envelope

```json
{
  "dangerMode": "hostile",
  "escalatedFromTraining": false,
  "annihilationAuthority": null,
  "sideWoundState": {
    "player": {
      "newWoundId": null,
      "opportunityCount": 0
    },
    "opposition": {
      "newWoundId": null,
      "opportunityCount": 0
    }
  }
}
```

Each accepted strain transition records immutable audit inputs:
`harmfulMargin`, `appliedArtTier`, `targetResilienceTier`, previous/new strain,
extra jump steps, raw trauma pressure, formula severity, destination cap, danger cap,
and final maximum. These are client-computed or copied from validated conflict
authority; the GM cannot author them.

The destination cap is exactly `clear -> none`, `strained -> I`, `fractured -> II`,
`overwhelmed -> III`, and `broken -> IV`. A multi-rank jump uses the accepted final
destination and separately records `extraJumpSteps`; no caller substitutes a looser
table.

If `newWoundId` is already set, a later eligible result may produce a worsen
opportunity for that identity, not another create opportunity. Existing older wounds
require an explicit `retraumaWoundRef` in the accepted action.

### Defeat outcome

```json
{
  "outcomeId": "client-owned",
  "kind": "retreat_lock",
  "targetSide": "opposition",
  "scope": "same_conflict_goal",
  "expiresAtCycle": 73,
  "sourceEventRef": "accepted-resolution-event"
}
```

The registered bounded kind may vary by legal resolution, but non-training defeat must
prevent immediate repetition of the same aggression. Soul dissipation remains in the
existing independent terminal authority.

## 11. Standard spiritual arts

Every persistent afterlife combat profile exposes:

```json
{
  "standardArts": {
    "spiritual_resilience": { "tier": 0, "experience": 0 },
    "spiritual_healing": { "tier": 0, "experience": 0 }
  }
}
```

Tier is 0-5 and experience/progression follows the existing accepted afterlife entity
progression pipeline. `spiritual_resilience` supplies the target tier used by the wound
opportunity. `spiritual_healing` supplies diagnosis, active-healing gate/check, natural
recovery rate, and service capability. An omitted tier in a complete current profile is
invalid after cutover; current bootstrap/profile fixtures receive tier 0 explicitly.

## 12. Spiritual healing attempt

```json
{
  "attemptId": "client-owned",
  "operationKey": "retry-safe-key",
  "woundId": "exact-active-wound",
  "target": {},
  "healer": {},
  "healingTier": 3,
  "woundSeverity": "II",
  "mode": "safe_cycle",
  "roll": 14,
  "validatedModifiers": 0,
  "complicationModifier": 1,
  "total": 20,
  "difficulty": 15,
  "margin": 5,
  "resultBand": "reduce_one",
  "cycleId": "client-owned-safe-cycle",
  "compensationReceiptId": null
}
```

### Resolver rules

1. Revalidate exact active wound, healer/target/realm/reachability/consent.
2. Reject severity reduction if healer tier is below wound rank; diagnosis may still
   return readable insufficiency.
3. In conflict, prove a legal counterable action and final action-point cost 2-5.
4. Outside conflict, allocate exactly one safe cycle and enforce one active attempt for
   this wound in that cycle.
5. Seal or client-generate the d20 through the existing roll authority; do not accept a
   replacement during repair/retry.
6. Apply the result band, then ordinary safe-cycle recovery exactly once when relevant.
7. Publish attempt, wound/effect/resource/scheduler/history/output after-images together.

An insufficient-tier healer cannot use natural 20 to bypass the gate. Natural 1 means
no active improvement even when numeric total would otherwise pass.

## 13. Natural spiritual recovery

For one accepted safe cycle:

```text
pointsAdded = 1 + currentSpiritualHealingTier
threshold(I)   = 2
threshold(II)  = 4
threshold(III) = 6
threshold(IV)  = 8
```

When progress reaches the current step threshold, severity falls one step or I heals;
subtract the threshold and continue with overflow. One cycle may cross several steps
only when its bounded points allow it. `lastTickKey` prevents double application.
Worsening resets current-step progress to zero. Recovery is suppressed during unsafe
or active-conflict time and resumes on a later safe cycle.

Examples:

- Tier 0, IV: 8+6+4+2 = 20 safe cycles to full healing.
- Tier V, IV: 6 points/cycle crosses total 20 points in 4 safe cycles.

## 14. Healing service profile

```json
{
  "providerId": "exact-afterlife-actor-id",
  "realm": "chaos_sea",
  "locationRef": "elyara_lazaret",
  "visibility": "public",
  "availability": "available",
  "minimumRelationship": null,
  "priceMultiplierPercent": 100,
  "compensationKinds": ["ink_feathers", "favor", "quest", "free_aid"],
  "accessConditions": []
}
```

Capability is proven by the actor's `spiritual_healing` tier, not by this profile.
Multiplier is an integer 50-200. Quote is
`ceil(basePrice[severity] * multiplier / 100)` and is sealed before confirmation.
Currency receipt is produced by the existing Ink Feather authority. A negotiated
non-currency agreement is an accepted exact contract/quest/debt/favor reference and
feeds the same attempt resolver.

### Elyara invariant

The built-in `elyara` profile is normalized/validated to:

- `spiritual_healing.tier = 5` and cannot be downgraded;
- fixed discoverable Lazaret location;
- public available service, multiplier 100;
- supported negotiated compensation from her accepted character contract.

### Shining faction invariant

Each accepted Shining faction has at least one visible roster resident in
`game_state/meta/guardian_abode_residents.json` whose
`primaryRole.key = healing_support` and whose `spiritual_healing.tier` is 1-5. The
resident's exact actor identity resolves to its afterlife profile. That profile may
omit `healingServiceProfile` or declare restricted access. The role is ordinary
visible roster information.

### Accepted wound-effect handoff

The in-memory handoff is closed and typed:

```text
WoundPreparedPlan
  WoundPreparationFingerprint
  EffectOperationBatches[]
  BaselineAuthority (internal only)
    PreTurnCarriers
    PreTurnIdentityIndex
    PreTurnHistory
    PreparedInputFingerprint
    AuthoritySeal

WoundEffectOperationBatch
  LocalWoundRef
  EffectSourceExport (Materializable = false)
  RootApplications[]
  TerminalOperations[]
  RootLineageAuthority[]
  SourceExportFingerprint
  TransitionAuthority (internal only)
    OpportunityId
    OpportunityAuthorityFingerprint
    OperationKey
    ReadableSummary
    MaximumSeverityRank
    PreparedInputFingerprint
    AuthoritySeal

WoundRootEffectApplication
  ApplicationRef
  MechanicsOrdinal
  OperationOrdinal
  DefinitionKey
  TargetSelector
  SourceSelector
  Parameters = {}
  SlotBindings[]
  ExpectedComponentCount
  ExpectedMaterializationFingerprint
  OwnershipDomain = base_wound | ComplicationId
  CausalEventRef

WoundRootEffectResult
  ApplicationRef
  Disposition = created_new_identity
  EffectId
  CreateTransitionId
  CreatedEventRef
  CausalEventRef
  SourceKey
  TargetKey
  CarrierCoordinate
  Materialization
    SlotBindings[]
    ComponentCount
    MaterializationFingerprint

WoundRootLineageAuthorityRow
  RootSelector = exactly one of ApplicationRef | EffectId
  DefinitionKey
  OwnershipDomain = base_wound | ComplicationId
```

`WoundAcceptedTurnPlanner.Prepare` allocates the wound identity but no effect identity.
For a new same-turn wound, `SourceSelector` contains its exact `sourceRef` and forbids
`sourceId`; an existing stable wound transition uses `sourceId`. The owner adapter maps
`player_soul -> player`, `combatant_member -> combatant`, and all other supported owner
kinds to the identically named #1535 target kind. A same-turn target uses `targetRef`;
an already stable target uses `targetId`.

The wound source is never publicly materializable: ordinary GM `effectChanges[]` cannot
apply any root or downstream definition. Only the fingerprinted typed batch grants
one-shot internal authority for its exact root tuple; later downstream materialization
is authorized only by the existing sealed #1535 reaction executor.

`TransitionAuthority` preserves the accepted transition facts that Finalize and the
real wound reducer/history require even when a valid wound has no mechanical roots.
It is immutable, internal, and neither persisted nor added to the public source-export
or preparation fingerprint format. Its domain/versioned `AuthoritySeal` binds the
prepared input fingerprint, local and permanent wound identities, opportunity identity
and fingerprint, operation key, readable summary, and maximum severity rank. Finalize
requires exact prepared-input agreement and independently recomputes this seal before
constructing reducer evidence or history. Thus a zero-operation batch cannot lose its
transition provenance, and an authority carrier from another prepared plan cannot be
mixed into the current plan.

`BaselineAuthority` preserves the detached wound-carrier, identity-index, and history
before-images required for final collection seals and atomic after-images. Its separate
domain/versioned seal binds the prepared input fingerprint and fixed-order canonical
bytes for all five optional carrier roots, the identity index, and history; null roots
remain distinct from empty objects. Finalize requires exact input-fingerprint agreement
and recomputes this seal before reading a baseline. The record is internal and does not
change the frozen public preparation-fingerprint encoding.

`ApplicationRef` is exact/confusable unique across the accepted turn and is never
canonical state. The effect composer derives a different replay-safe internal event
reference for every terminal and root application through
`CreateWoundEffectOperationEventRef(parentEventRef, mechanicsOrdinal,
operationOrdinal, operationKind)`. Its client-owned value is
`wound_effect:sha256:<canonical-tuple-hash>`, separate from public accepted-turn
authorities. For a root, that derived operation reference becomes exact
`chronology.createdEventRef`; the accepted wound event remains separately preserved as
exact client-owned `chronology.causalEventRef`. A terminal transition likewise uses its
own derived transition event and the same accepted wound event as causal authority. The
two fields may not be swapped, omitted, or collapsed merely because several operations
share one wound event. The effect planner processes terminal operations first,
allocates random opaque IDs only for newly created roots, and returns one detached
result per application. Wound finalization requires exact set/cardinality agreement and
also compares disposition, transition ID, both event refs, source/target keys, and
carrier coordinate, ordered slots, component count, and the materialization fingerprint
before persisting only `{effectId, definitionKey}` root bindings. It independently
recomputes the result fingerprint from the exact created active-effect after-image and
the prepared root parameters; comparing two carried fingerprint strings is insufficient.

All accepted event references seed the exact/confusable created-event exclusion set
before either effect identity factory runs. A derived wound operation reference must be
unique against that entire set and every other derived reference, even if an accepted
authority already uses the syntactically valid `wound_effect:sha256:...` namespace.
Likewise, the same-turn `kind=wound` entries in `EffectSourceAuthority` must be the exact
prepared export set rather than a superset: an unused extra entry rejects the handoff,
while unrelated pre-turn sources remain legal. Returned source entries use detached
definitions and detached/frozen satisfied-predicate evidence.

The `SlotBindings` on a prepared root use provisional correlation ordinals because no
permanent effect identity exists at Prepare. Once the effect plan has allocated random
opaque IDs, result `SlotBindings` use canonical post-allocation ordinals: roots are
ordered by actual `effectId`, each root keeps its prepared semantic slot order, and the
whole wound batch is numbered contiguously from 1. Finalize recomputes that mapping from
detached prepared roots and accepted results before writing consequence entries. Opaque
ID generation therefore remains random and is never made order-preserving merely to
stabilize slots.

`ExpectedMaterializationFingerprint` and result `MaterializationFingerprint` use domain
`book_of_eternity.wound.effect_materialization`, version `1`. In exact order, the
length-prefixed UTF-8 SHA-256 writer hashes domain, fingerprint version, exact source
realm/kind/source ID/definition key, positive definition/effect schema version, recursively
canonical compact parameters, component count, and each fully bound component's
zero-based original ordinal plus recursively canonical compact JSON. Every field is
encoded as `<UTF8-byte-count>:<field>`; output is lowercase `sha256:` plus 64 lowercase
hexadecimal characters. Recursive canonicalization sorts object keys ordinally and
preserves every array order. Version-1 direct-root parameters are exact `{}` but remain
an explicit hash input. The expected value is derived from the detached source
definition; the actual value is derived from the created effect after parameter binding.
It is internal handoff authority and is not persisted in canonical wounds or player
output. The consequence catalog remains the severity/power-policy authority.

`SourceExportFingerprint` is the exact per-source pre-effect seal. It covers source
schema/kind, permanent ID, optional local ref, state, non-materializable policy, realm,
owner, accepted causal event, the recursively canonical complete definitions, ordered
root-lineage-authority rows, and ordered root applications including application refs,
operation ordinals/kinds, target/source selectors, empty parameters, slot bindings, and
expected component counts/materialization fingerprints, and ownership domains. No newly
allocated root-result effect ID is an input to a new root
application or same-turn source export. Existing canonical root effect IDs may appear
only in terminal operations or root-lineage authority and are covered by the
`WoundPreparationFingerprint` or `SourceExportFingerprint`, respectively.

The staged contour uses non-interchangeable, internally computed seals:

1. `WoundPreparationFingerprint` covers the accepted binding/input authority, allocated
   wound/complication/transition IDs, detached wound drafts, local refs, every
   `SourceExportFingerprint`, and the ordered terminal-operation set.
2. `EffectInputFingerprint` covers the complete ordinary effect input plus the typed
   wound batches, their upstream seals, and every derived operation/causal event pair.
3. `EffectAcceptedTurnPlanFingerprint` covers that exact effect input, every allocated
   effect/transition ID, exact application and termination result maps including each
   actual component count/materialization fingerprint, and all effect carrier/identity
   before- and after-images from which that fingerprint can be recomputed.
4. `WoundFinalPlanFingerprint` covers the wound-preparation and effect-plan seals plus
   final canonical wounds, root bindings, entries, complications, identity/history/
   pending/scheduler/output after-images, and exact wound intents.
5. The common `AcceptedMechanicsPlan.PreparedPlanFingerprint` covers the common input
   fingerprint and authority fingerprints, every subordinate seal above, finalized
   wound/effect/resource after-images and receipts, touched/consumed paths, pending
   state, scheduler outcomes, and player-output bindings.

No constructor or caller supplies or self-asserts `PreparedPlanFingerprint`.
`AcceptedMechanicsPlanner` computes it only after semantic cross-stage agreement, and
`AcceptedMechanicsPlanCache` independently recomputes it from the detached returned plan
before validation and again on take/peek for publication. A source export, result map,
derived event, subordinate after-image, or final wound binding taken from another valid
plan therefore invalidates the common plan instead of receiving a fresh caller-chosen
seal.

The same recomputation rule applies at every earlier boundary: the effect composer
recomputes each source-export and wound-preparation seal from detached fields, the effect
planner recomputes its complete input seal and every expected/actual materialization
fingerprint, and wound finalization recomputes the effect-plan seal plus each created
effect's materialization fingerprint before consuming any result. A downstream stage
never treats a fingerprint property alone as proof of its payload.

Finalization derives the current prepared wound's exact reciprocal root-effect set from
four independent after-image views: the plan's active-effect list, runtime
resource-trigger carriers, publication carrier roots, and identity index. Every view
must contain exactly the accepted root IDs and no additional effect claiming a prepared
wound source. Each root identity must also agree on owner collection, complete stack
coordinate, source/target, accepted-turn chronology, and its sole create transition;
the transition ID must be present in `AllocatedTransitionIds`. These checks are semantic
and are repeated after effect-plan resealing.

## 15. Wound command staging

```json
{
  "schemaVersion": 1,
  "sessionId": "exact-session",
  "requestId": "exact-request",
  "snapshotToken": "exact-snapshot",
  "commands": [
    {
      "kind": "accepted_transition",
      "commandRef": "client-owned",
      "transitionKind": "diagnose",
      "operationKey": "client-owned-retry-safe-key",
      "authority": {},
      "result": {},
      "finalSceneText": "escaped accepted scene or null"
    }
  ]
}
```

The closed command discriminator is `opportunity_decision` for the already defined
create/worsen decision envelope or `accepted_transition` for one typed existing-wound
operation. Accepted transition kinds are `diagnose`,
`author_alternative_treatment`, `stabilize`, `treat`, `recover`, and `heal`; every kind
has a distinct closed authority/result payload and is independently recomposed before
consumption. `materialize` and `worsen` are decisions inside
`opportunity_decision`, not loose command aliases.

Every `accepted_transition` has exactly `kind`, `commandRef`, `transitionKind`,
`operationKey`, `authority`, `result`, and `finalSceneText`. For `diagnose`,
`authority` binds exact command/attempt/wound/path identity, the
expected before fingerprint, path fingerprint, fresh requirement authority, sealed
check result, and its own authority fingerprint. `result` carries the exact
`success|failure`, the required complete-or-empty `revealedFacts[]`, and a result
fingerprint. The command `operationKey` and `attemptId` become the corresponding
history replay coordinates.

For `author_alternative_treatment`, `authority` is the post-response accepted
transition evidence from section 9. It binds the transient request through
`requestAuthorityFingerprint`, repeats the exact operation/wound/event coordinates,
and seals the complete proposed before/after, route/path, fresh requirement, and
evidence-authority fingerprints. `result` is exactly `decision=author`, the complete
route, the optional required diagnosis path, and a recomputable result fingerprint.
The reducer recomputes both the result and authority instead of trusting either seal.
A decline produces no command or history row. `authoringRequestRef` is the safe
response correlation and exact command reference, while `operationKey` is the unique
history coordinate.

For `treat`, `authority` is produced only by `MortalWoundTreatmentPlanner`. It binds the
exact operation/attempt/wound/route/before-state coordinates, route fingerprint, fresh
requirement authority, and exactly one mode proof: accepted d20, course clock/history,
or materialized treatment-guarantee source contract. Its `request` member is the full
canonical `MortalWoundTreatmentAttemptRequest`; the outer command operation key and all
authority/result copies must agree with it. `result` is the exact closed typed treatment
history result from section 5, including the same full `requestAuthority`, the selected authored outcome or
interruption, category, ordered result, consumption trigger, mode evidence, and
recomputable fingerprints. The reducer derives the terminal-attempt intent from this
validated result; an input boolean cannot assert terminality. If ordered treatment
primitives reach severity I and then request `heal`, the planner emits the separately
validated follow-up heal intent without copying the treatment attempt ID onto the heal
history row.

Only the pure
`WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(binding,
resolution, finalSceneText)` creates a valid typed `treat` command row. Its second
argument is the self-contained accepted `MortalWoundTreatmentResolution`, whose embedded
full request and result are recomputed against the binding. The existing
`ComposeAcceptedTransitionCommandRoot(binding, WoundTransitionRequest,
finalSceneText)` remains a separate single method for the reducer transition surface;
there is no same-name treatment overload and no filesystem parameter on either composer.

Commands are one-shot, consumed only by the matching accepted plan, bound to the root
session/request/snapshot plus exact final-scene text, and absent in ordinary read-only
`/раны` queries. The GM cannot write the command root. No command is staged from a raw
response that fails strict kind-specific parsing, authority recomputation, or exact
typed recomposition.

## 16. Pending wound resolution

The pending root stores a bounded wave of at most 64 candidates and their exact safe
authority. It distinguishes `construct_wound`, `repair_wound`,
`author_alternative_treatment`, and `narrate_acquisition`. Every candidate contains a
kind, safe opaque candidate/correlation reference, exact semantic fingerprint, safe
readable context, offending paths, receipt identity, and transcript prefix.

A `construct_wound` or `repair_wound` candidate additionally contains:

- safe readable event/target/realm context;
- allowed decision (`none` or materialize) and severity range;
- closed domain/profile/location/complication/consequence/treatment schema;
- immutable event/target/roll/guarantee/attempt/cycle fingerprints;
- offending semantic paths and expected bounds for repair;
- preserved non-offending wound proposal fields.

An `author_alternative_treatment` candidate instead contains:

- the safe opaque `authoringRequestRef` and readable accepted-evidence summary;
- allowed decision (`decline` or `author`);
- the closed route/optional diagnosis-path schema and append-only constraints;
- internally sealed before/route/path/authority fingerprints and operation key;
- only the rejected route/path semantic leaves needed for bounded correction.

Its repair packet exposes `candidateKind=author_alternative_treatment`, preserves only
non-offending route/path content, and requires one corrected
`woundTreatmentAuthorings[]` entry with the exact safe `authoringRequestRef`, decision,
route, and diagnosisPath fields. It never asks the GM to resubmit a construction
`woundDecisions[]` entry for this candidate.

The GM-facing packet omits permanent client IDs, hidden target/private NPC data,
before/route/path/authority fingerprints, operation keys, resource seals, internal
owner coordinates, and unrelated response content. The client-owned pending root keeps
the sealed fields required for retry comparison. A receipt is accepted once. A changed
snapshot, wound fingerprint, evidence event, or semantic authority invalidates the
pending wave.

The client-owned root has one additional optional top-level field,
`submittedTreatmentRequests[]`. It is not a fifth candidate kind and is not copied into
the GM-facing packet. `WoundRepairPacketBuilder.ComposePendingRoot(binding, packets,
parsedCommand)` may create it only when `packets` is a real non-empty bounded repair wave
and `parsedCommand` is the successfully parsed/recomposed command root for the same
session/request/snapshot binding. Each exact-unique row carries the accepted-transition
outer coordinates and the full detached typed `treat` request, including the complete
requirement bundle and resource authority. The collection is absent—not an empty claim
surface—when there is no real repair wave or no submitted treatment request. Command and
pending copies are parsed independently, recomputed, and byte-semantically coalesced;
any nested or outer disagreement fails closed.

## 17. Transition state machine

| Transition | Required before | Legal after | Notes |
| --- | --- | --- | --- |
| `create` | no identity; valid opportunity | active fresh/untreated I-IV | Creates index, wound, history, effects atomically |
| `worsen` | active I-III or same-conflict wound | higher active severity, max IV | Resets current-step recovery; replaces effects |
| `complicate` | active wound | same or declared worse severity | Adds registered complication/effect only once |
| `diagnose` | active, discoverable path | same mechanics plus newly known facts | Never heals by itself |
| `author_alternative_treatment` | active Mortal wound; sealed evidence-backed authoring authority | same mechanics plus one appended complete route and optional required diagnosis path | Never rewrites an existing route/path or prior history |
| `stabilize` | active unstabilized | stabilized | May remove only declared complication/effect |
| `treat` | active; complete route/gate | sealed result | Attempt may fail and still be terminal/charged |
| `recover` | active; due valid clock/cycle | progress or lower severity/healed | Idempotent by tick key |
| `heal` | active severity I with sufficient result | healed and removed from carrier | Ends owned effects; appends terminal history |
| `legacy` | accepted heal or prior healed history | independent entity plus relation | Never keeps wound mechanically active |
| `archive` | healed terminal history | history-only projection | Cannot reopen or delete replay evidence |

No transition may lower severity without a treatment/recovery outcome, increase it
without an eligible event/deterioration/re-trauma, skip owner/realm validation, or
write an effect/resource/history root directly.

## 18. Player projection model

The shared application service returns typed player-facing blocks, not canonical JSON.

### Active list item

- display name and Roman severity;
- readable locus/cause known to the player;
- short visible symptoms/consequences;
- care/recovery summary;
- whether detail/treatment/diagnosis/help actions are currently available.

### Detail

- visible origin, location, symptoms, prognosis, linked effect summaries;
- current care and recovery progress in readable terms;
- known routes, requirements, risks, provider/access/quoted cost when proven;
- guided `Лечить` action.

### History

- healed wound name, readable origin, final outcome, cosmetic scar/legacy summary, and
  readable chronology;
- never present in the primary active list;
- no IDs, fingerprints, hidden routes/symptoms, private NPC facts, or validator terms.

### Target choice

The choice carries hidden exact binding but displays Self first and then reachable
visible entities. Duplicate names receive location/role/appearance context. Confirming
the choice revalidates target, wound, consent, provider, resources, and reachability.

## 19. Cross-root invariants

1. Every active index entry has exactly one active carrier occurrence and vice versa.
2. Every wound-owned active effect has one matching active wound, exact source
   coordinate, and `definitionKey` in that wound's persisted graph. Directly
   materialized roots have one reciprocal `rootBinding`; slot-consuming roots have one
   or more reciprocal entries. The sole zero-slot marker and later reaction descendants
   do not invent slots.
3. Wound/effect target, realm, source, and visibility authority agree.
4. Effect removal alone never mutates the wound.
5. Full healing uses the indexed exact `(realm, wound, woundId, definitionKey)` source
   coordinates grouped by `(realm, wound, woundId)` and the persisted definition graph
   to remove all and only that wound's active root and descendant effects from active
   carriers.
6. Healed wounds have no active carrier occurrence and remain in terminal history.
7. Independent effects, including Saref `memory_suppression`, survive unrelated wound
   transitions.
8. Every treatment/resource/provider/item/art/location reference resolves in the fresh
   canonical snapshot at commit.
9. Attempt, course milestone, recovery cycle, opportunity, and transition keys are
   globally replay-safe in their scopes.
10. Console and browser projection from the same canonical snapshot has equivalent
    actions, costs, results, and errors.
11. Invalid siblings cause no wound/effect/resource/scheduler/output after-image to
    publish.
12. All canonical text that reaches a player renderer is escaped/sanitized.

## 20. Version-1 bounds

| Collection/value | Limit |
| --- | ---: |
| Active wounds across all carriers | 2,000 |
| History transitions | 20,000 |
| Opportunity-decision receipts | 20,000 |
| Wound commands per accepted turn | 128 |
| Pending wound repair candidates | 64 |
| Pending Mortal accepted occurrences | 32 |
| Accepted events per Mortal occurrence | 160 |
| Treatment routes per wound | 32 |
| Diagnosis paths per wound | 32 |
| Requirements per route/path | 16 |
| Known-fact prerequisites or reveals per diagnosis path | 16 |
| Complications per wound | 16 |
| Consequences per wound | 4 |
| Heal legacy drafts / durable legacy relations per wound | 8 |
| Definitions per mechanical-effect legacy | 5 |
| Root applications per mechanical-effect legacy | 5 |
| Persisted wound-owned effect definitions per wound | 5 |
| Persisted wound-owned root effect bindings per wound | 5 |
| Persisted pre-turn wound-owned definitions across 2,000 active wounds | 10,000 |
| Persisted pre-turn wound-owned root bindings across 2,000 active wounds | 10,000 |
| Active/suspended wound-owned effect instances across active wounds | 10,000 |
| Same-turn wound-owned definitions across 32 transitions | 160 |
| Same-turn wound-owned root applications across 32 transitions | 160 |
| Same-turn mechanical-legacy definitions across all heal transitions | 160 |
| Same-turn mechanical-legacy root applications across all heal transitions | 160 |
| Effect proposals supplied to one wound-envelope validation | 128 |
| Components or cadence entries per effect proposal | 64 |
| Flattened reaction-expansion rows per effect proposal (structural work bound) | 64 |
| Wound-owned flattened reaction expansions per wound (semantic budget) | 1 |
| Resource-bound evidence entries per wound envelope | 128 |
| Wound transitions composed per turn | 32 |
| Readable text field | repository-standard bounded GM text limit |

Planning and agreement use indexed `woundId`, owner coordinate, effect ID, operation
key, and route ID maps. Limits fail closed before expensive cross-root composition.
