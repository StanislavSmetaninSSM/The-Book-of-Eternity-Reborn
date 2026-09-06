# T070 Scalar Course Publication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Execute one reviewed task and one bounded C# lane at a time.

**Goal:** Publish genuinely accepted scalar course milestones and interruptions through
the existing atomic treatment pipeline, including exact course-pointer and resource history.

**Architecture:** Extend the shared outcome-admission and scalar projection seam with a
closed course selection, reuse the existing outcome intents/severity rematerializer,
and preserve the sole accepted-mechanics publisher. Freeze and independently recompute
course selection evidence in outcome preparations. No new command, resource protocol,
course history format or second publisher is needed.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T070. Source audit retained in Git
metadata `sdd/t070-course-publication-source-audit.md`; it is evidence, not requirements.

**Status:** Parent source/spec self-review complete. Execute after acceptance of the
preceding alternative-repair checkpoint; the dispatch brief records the exact BASE.

## Global Constraints

- Stay in `E:/Games/worktrees/boe-1536-wound-materialization` on the existing
  `1536-complete-wound-materialization` branch; no remote mutation or `.serena` access.
- No migration, old-save fallback, alternate publisher, synthetic accepted authority,
  test-installed course history, guessed resource receipt, or unsupported-intent success.
- Keep the two original stale-history tests unchanged. The defect is missing production
  course publication in their setup, not their existing pre-reservation history checks.
- This completes the scalar course contour only. `heal`, `add_recovery`,
  `remove_complication`, `add_complication`, and `apply_deterioration` retain explicit
  unsupported publication until their real producers land under T070. Do not remove
  those approved route mechanics or call the entire course/feature complete.
- Preserve every currently supported guaranteed/procedure scalar result and its exact
  critical/Fate/resource behavior. Non-course treatment must preserve an active course.
- `CanonicalStateNormalizer` is the sole publisher; current milestone settlement,
  wound/effects/history/identity/output must remain one rollback-safe transaction.
- Pure model/grammar/seal tests stay Fast; filesystem/lease/publication/restart tests
  stay Integration. No runner, lane, category, time-limit or project-file changes.
- One implementer and one bounded C# lane. Do not rerun a broad lane after each edit.
- Synchronize the Mortal GM guide and one real worked scalar course example with its
  production/source guards. This changes no afterlife command or spiritual mechanic.

## Binding approved requirements

The source contract is `specs/1536-complete-wound-materialization/contracts/mortal-wound-treatment.md`:
empty result is active-milestone-only; positive milestones are monotone; interruption
is the sole adverse branch; course start sets the pointer, continuations own the exact
same pointer, completion/interruption clears it; other route types may run during a
course without clearing it; only final completed milestones append the route; stale
history and exact replay classification precede reservations.

| Selected course case | Disposition | Allowed scalar result | Route completion | Active pointer |
| --- | --- | --- | --- | --- |
| first or later intermediate | active | empty or existing positive scalar grammar | None | set at first, preserve same later |
| first/singleton or later final | completed | non-empty existing positive scalar grammar | AppendOnce only when route absent | null after final |
| trusted interruption after start | interrupted | sole no_improvement | None | clear exact owned pointer |

The existing positive scalar grammar is singleton stabilize, or at most one stabilize
plus one/two reduce_severity operations, each 1–2 and aggregate 1–2. Keep original
ordered operations; do not replace them with a new generic mutation payload.

### Task 1: Exact scalar course selection, pointer projection and atomic publication

**Files:**

- Modify `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`:
  mode-aware admission, grammar, scalar shell, versioned preparation seal and independent
  finalization agreement. The existing preparation and planner types may become partial.
- Create `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.Course.cs`:
  cohesive exact selection/pointer helpers; not a separate course publisher.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentResolver.Semantics.cs` only
  to strengthen its existing course mode-evidence recomputation and extract the exact
  shared route-completion/consumption derivations when reused by admission/finalization.
- Create `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.CoursePublication.cs`:
  actual course scalar publication, tamper, settlement, restart and rollback regressions.
- Modify `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs`
  only in the shared successful publication fixture helper; keep the two original
  stale-history methods and their assertions unchanged.
- Generalize the existing private persistence/coordinated-publication helpers in
  `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ProcedurePublication.cs`;
  mechanically rename their callers in that file, `MortalWoundTreatmentResolverTests.cs`,
  `MortalWoundTreatmentResolverTests.Persistence.cs`,
  `MortalWoundTreatmentResolverTests.ProcedureAuthority.cs`, and
  `MortalWoundTreatmentResolverTests.Replay.cs`. These are test fixture changes only;
  do not change the callers' scenarios or assertions.
- Modify `OtherGuides/Wound_Materialization_Contract.md`,
  `Examples/E_CLI_Wound_Materialization.txt`, `Examples/example_validation_manifest.json`,
  owning wound documentation/example guards and the exact manifest-family dictionary.
- Do not change the existing resource reservation/finalization, request schema, typed
  history codec or high-level stale-history classifier merely to get setup through.
  Escalate a genuine additional producer dependency with source/failure evidence.

**Existing interfaces:**

```csharp
MortalWoundTreatmentDetachedSealValidator.IsValid(request);
MortalWoundTreatmentDetachedSealValidator.TryGetRoute(request, out var route);
MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(
    resolution, out var modeEvidenceFingerprint);
MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
    request, resolution.DeclaredResult, acceptedState,
    out var expectedIntents, out var intentIssues);
```

The first helper validates complete nested request authorities, not only the outer
request fingerprint. TryGetRoute yields the exact sealed route and verifies its
fingerprint against requirement/resource authority. RecomputeCourseEvidence already
derives selected milestone/interruption, category, index and declared operations from
the course route. It currently omits some outer course coordinates: add exact course
ID, milestone ordinal and route fingerprint agreement there, with direct negative tests.
No full-wound raw JSON or live filesystem lookup belongs in detached outcome matching.

- [ ] **Step 1: Observe actual setup RED before adding the new producer**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseRequirementClassifier_StaleParsedHistoryIsInvalidAuthorityNotUnsatisfied|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseHighLevelPreparation_StaleHistoryRejectsBeforeResourceReservation"
```

Expected current RED: `mortal_wound_treatment_publication_slice_unsupported` when the
test publishes its actual initial empty course milestone. Keep both tests and the
actual ComposeAndPublishTreatment → accepted plan → normalizer route unchanged.
Their first course milestone has an empty result; the second consumes a fresh current
antibiotic dose and reduces severity; the original third heals and remains outside
this scalar stage. Use a new scalar-only route variant for complete-course tests,
not a weakened or skipped original healing completion assertion.

The parent source audit also establishes a second fixture dependency: successful
course milestones hold an item dose, but the old shared `ComposeAndPublishTreatment`
neither distributes the accepted command nor uses the held-resource coordinator.
After observing the outcome RED, repair that fixture through the real production
pipeline below. Keep both frozen test bodies unchanged. This is not permission to
relax either production hold gate.

- [ ] **Step 1a: Use authentic persisted holds and coordinated publication in the fixture**

Rename the existing private `PersistAndRehydrateProcedurePublication` helper to
`PersistAndRehydrateTreatmentPublication`, replace the hardcoded rehydration mode
`"procedure"` with `initialRequest.Mode`, and use treatment-neutral explanatory text.
Retain every existing request/resolution equality assertion. The final helper is:

```csharp
private static TreatmentFlow PersistAndRehydrateTreatmentPublication(
    AcceptedStateFixture fixture, TreatmentFlow initial, string boundary)
{
    var initialRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(initial.Request);
    PersistTreatmentCommand(fixture, ComposeTreatmentCommand(initial,
        "The accepted treatment is persisted before canonical publication."));
    var restored = Assert.Single(AssertValidPersistedCatalog(
        RestoreCurrentPersistedTreatmentCatalog(fixture), boundary + " persisted treatment"),
        candidate => string.Equals(
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(candidate).RequestFingerprint,
            initialRequest.RequestFingerprint, StringComparison.Ordinal));
    var rehydrated = RehydratePersistedTreatment(fixture, initialRequest.Mode, restored);
    Assert.Equal(CanonicalValue(initial.Request), CanonicalValue(rehydrated.Request));
    Assert.Equal(CanonicalValue(initial.Resolution), CanonicalValue(rehydrated.Resolution));
    return rehydrated;
}
```

Rename `ComposeAndPublishCoordinatedProcedureTreatment` to
`ComposeAndPublishCoordinatedTreatment` (its existing body is already mode-neutral).
The existing `ComposeCoordinatedProcedurePlan` may likewise be named
`ComposeCoordinatedTreatmentPlan`; mechanically update only its actual callers.
Do not copy the three existing bodies into a parallel course-only fixture pipeline.

At the top of `ComposeAndPublishTreatment`, before calling the real planner, route
successful held course milestones through that same pair of generalized helpers:

```csharp
var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
if (request.Mode == "course" &&
    request.ResourceAuthority.ReservationDisposition == "held")
{
    var rehydrated = PersistAndRehydrateTreatmentPublication(
        fixture, flow, "accepted course milestone publication");
    return ComposeAndPublishCoordinatedTreatment(fixture, rehydrated, proposal);
}
```

The remainder of the existing direct helper is unchanged. This specifically leaves
no-hold course interruption on its ordinary direct normalization path; it does not
invent a procedure/Fate transaction for a course. Existing resource confirmation comes
only from `PersistTreatmentCommand` -> production `StateDistributor`; that helper
releases/reacquires the lease, so the returned freshly rehydrated flow is mandatory.
Do not call the reservation confirmation API manually, pass the old live accepted
state after distribution, or substitute a test-installed history for real publication.
The production resource/request/history APIs remain unchanged.

- [ ] **Step 2: Add independent course selection and the closed grammar**

In ValidateCommon, preserve current accepted-state/coordinate/before-wound checks.
Also require the complete detached validator for both the request argument and the
resolution's RequestAuthority before comparing their exact fingerprints. Admit mode
exactly guaranteed/procedure/course, require request/resolution mode equality and
AttemptDisposition=AcceptedTerminal. Only course may carry interruption/course fields;
all non-course resolutions retain exact null course ID/ordinal/disposition.

For course, require exact typed MortalWoundCourseModeAuthority and
MortalWoundCourseModeEvidence; request and outer/evidence ordinal must equal the mode
authority ordinal; outer/evidence course ID/disposition must agree; route fingerprint
must equal the exact selected route and CourseStartAuthority route fingerprint; evidence
ResolvedAtGameTimeMinutes must equal the accepted current minute. Then recompute mode,
resolution-authority and declared-result seals with the existing functions. The
disposition/index/category must descend from the selected route row, not the supplied
outer string. A trusted interruption has null SelectedOutcomeIndex; otherwise it equals
MilestoneOrdinal - 1. Course must have no CriticalReactionIntent.

Keep TryCompose and OrderedIntentsAgree (which already handles zero arrays). Use this
selection-aware grammar, moving the unchanged positive portion out of the old helper:

```csharp
private static bool HasSupportedGrammar(
    string mode, bool interruption, string? courseDisposition,
    IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents)
{
    if (mode == "course")
    {
        if (interruption)
            return courseDisposition == "interrupted" && intents.Count == 1 &&
                intents[0] is MortalWoundNoImprovementOutcomeIntent;
        return courseDisposition switch
        {
            "active" => intents.Count == 0 || HasPositiveScalarGrammar(intents),
            "completed" => HasPositiveScalarGrammar(intents),
            _ => false
        };
    }
    return (intents.Count == 1 &&
            intents[0] is MortalWoundNoImprovementOutcomeIntent) ||
        HasPositiveScalarGrammar(intents);
}

private static bool HasPositiveScalarGrammar(
    IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents)
{
    if (intents.Count == 1 && intents[0] is MortalWoundStabilizeOutcomeIntent)
        return true;
    if (intents.Count is < 1 or > 3 || intents.Any(static intent => intent is not
            (MortalWoundStabilizeOutcomeIntent or MortalWoundReduceSeverityOutcomeIntent)))
        return false;
    var stabilizations = intents.Count(static intent => intent is MortalWoundStabilizeOutcomeIntent);
    var reductions = intents.OfType<MortalWoundReduceSeverityOutcomeIntent>().ToArray();
    if (stabilizations > 1 || reductions.Length is < 1 or > 2 ||
        reductions.Any(static reduction => reduction.Steps is < 1 or > 2))
        return false;
    try
    {
        var aggregate = reductions.Aggregate(0,
            static (sum, reduction) => checked(sum + reduction.Steps));
        return aggregate is >= 1 and <= 2;
    }
    catch (OverflowException) { return false; }
}
```

Derive route completion identically in the resolver and publisher: for a course,
`!Interruption && CourseDisposition == "completed" && !routeAlreadyCompleted`
means AppendOnce, otherwise None. Preserve the existing procedure/guaranteed success
formula. Derive ConsumptionTrigger from the exact route's ResourcePolicy: interrupted
means none; otherwise category when ConsumeOn contains it, else none. Prefer small
shared helpers over a second formula dialect. Resource finalization independently
revalidates and selects actual claims; never derive consumption from a guessed cost.

Retain the stable publication error code `mortal_wound_treatment_publication_slice_unsupported`,
but name the actual admitted scalar modes in its Expected text. Every rejected axis
returns no recomposed intents/preparation and acquires no publication reservation.

- [ ] **Step 3: Derive and seal course pointer changes**

Use one helper from admission and scalar-shell reconstruction:

```csharp
private static bool TryProjectActiveCourseId(
    WoundMaterializationEnvelope before,
    MortalWoundTreatmentAttemptRequest request,
    MortalWoundTreatmentResolution resolution,
    out string? projected)
{
    projected = before.Care.ActiveCourseId;
    if (resolution.Mode != "course")
        return resolution.CourseId is null && resolution.CourseMilestoneOrdinal is null &&
            resolution.CourseDisposition is null;
    if (request.ModeAuthority is not MortalWoundCourseModeAuthority authority ||
        request.MilestoneOrdinal != authority.MilestoneOrdinal ||
        resolution.CourseMilestoneOrdinal != authority.MilestoneOrdinal ||
        !string.Equals(resolution.CourseId, authority.CourseId, StringComparison.Ordinal))
        return false;
    var first = authority.MilestoneOrdinal == 1;
    var ownsPointer = string.Equals(before.Care.ActiveCourseId,
        authority.CourseId, StringComparison.Ordinal);
    switch (resolution.CourseDisposition)
    {
        case "active" when !resolution.Interruption &&
                (first ? before.Care.ActiveCourseId is null : ownsPointer):
            projected = authority.CourseId;
            return true;
        case "completed" when !resolution.Interruption &&
                (first ? before.Care.ActiveCourseId is null : ownsPointer):
            projected = null;
            return true;
        case "interrupted" when resolution.Interruption &&
                authority.MilestoneOrdinal > 1 && ownsPointer:
            projected = null;
            return true;
        default:
            return false;
    }
}
```

CreateScalarShell must require that projection and set ActiveCourseId alongside
LastAttemptId. Keep its existing transition, anchors, completed-route handling and
all unrelated fields. Stabilization and severity projection already use record `with`
and must preserve the projected pointer. A non-course treatment during a course leaves
it unchanged, including a standalone stabilization/reduction attempt.

The frozen preparation must independently cover the new selection. Retain a private
primitive-only selection record with Mode, AttemptDisposition, Interruption,
ConsumptionTrigger, CourseId, CourseMilestoneOrdinal, CourseDisposition, RouteFingerprint,
and the independently recomputed ModeEvidenceFingerprint. Constructor ownership and
DetachedCopy preserve its exact immutable values; no caller-owned graph/set is retained.
AgreesWith compares every selected primitive and reruns complete detached request,
mode/resolution/result recomputation before recreating the scalar shell. Existing
ordered IntentSeal and reduction projection agreement remain unchanged.

ComputePreparationFingerprint includes those selected primitives in fixed declared
order, using invariant number/bool serialization, and increments its internal
PreparationDomain version from 1 to 2. No compatibility seal. Existing continuation/
reservation/publication seals already nest this fingerprint and need no parallel
course-field list or version change merely for a nested seal.

All finalization calls, including direct callers and detached-copy controls, must
reject changed selection/pointer/resolution graphs even if a caller recomputes only
the old outer hashes. Do not trust a Success flag or treat hash syntax as provenance.

- [ ] **Step 4: Verify actual lifecycle and synchronize GM example**

Add the following actual three-milestone lifecycle regression to the new Integration
partial. The fixture has a legal one-root severity-II graph, real inventory and current
history; only its new scalar-only route replaces the final heal with stabilization.
The existing healing route and its completion test remain untouched.

```csharp
private static ResolverScenario CreateScalarCoursePublicationScenario()
{
    var scenario = CreateScenario(
        "course_first_milestone_is_ready_at_inclusive_due_time", "course");
    scenario.Before["treatment"]!["routes"]![0]!["outcomes"]![2]!["result"] =
        new JsonArray(new JsonObject { ["kind"] = "stabilize" });
    return scenario with
    {
        OperationKey = "operation_t070_scalar_course",
        History = CreateCurrentWoundHistory(scenario.Before),
        SeedCanonicalWoundEffects = true
    };
}

[Fact]
public void CourseScalarPublication_ThreeMilestonesRestartAndCompleteWithoutHealing()
{
    var scenario = CreateScalarCoursePublicationScenario();
    using var fixture = AcceptedStateFixture.Create(scenario);
    string? courseId = null;
    for (var ordinal = 1; ordinal <= 3; ordinal++)
    {
        if (ordinal > 1)
        {
            fixture.RestartForReplay();
            fixture.PrepareNextTurn(41 + ordinal, (ordinal - 1) * 480,
                "scalar_course_" + ordinal);
        }
        var flow = ResolveCurrentTreatment(fixture, "course",
            scenario.OperationKey + "_" + ordinal, scenario.RouteId);
        AssertCourseAcceptedResolution(flow, ordinal,
            ordinal == 3 ? "completed" : "active", interruption: false);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        courseId ??= resolution.CourseId;
        Assert.False(string.IsNullOrWhiteSpace(courseId));
        Assert.Equal(courseId, resolution.CourseId);
        Assert.Equal(ordinal == 3 ? "AppendOnce" : "None", resolution.RouteCompletion);
        ComposeAndPublishTreatment(fixture, flow);
        var wound = fixture.ReadCurrentWound();
        Assert.Equal(ordinal == 3 ? null : courseId, wound.Care.ActiveCourseId);
        Assert.Equal(ordinal == 1 ? 2 : 1, wound.Severity.Rank);
        Assert.Equal(8 - ordinal, fixture.ReadPlayerItemCount("antibiotic_dose"));
        fixture.AssertItemIdentityIndexValid();
        Assert.Equal(ordinal, fixture.ReadCurrentHistory().State!.Transitions
            .Count(static row => row.Kind == "treat"));
        var replay = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
        AssertClosedTreatmentReceipt(ReadRequiredProperty(replay, "Receipt"),
            flow.Request, flow.Resolution);
    }
    fixture.RestartForReplay();
    Assert.Null(fixture.ReadCurrentWound().Care.ActiveCourseId);
    Assert.Equal(3, fixture.ReadCurrentHistory().State!.Transitions
        .Count(static row => row.Kind == "treat"));
}
```

Extend the owning new `CourseScalarPublication_` prefix with these exact cases. Use
production preparation/resolution, not a hand-installed course start/history. Keep
route variants local to the new file; they are authored test routes, not canonical
authority. Capture the fixture tree after legitimate preparation and prove rejected
publication is write-free.

| New method suffix | Concrete setup and required assertions |
| --- | --- |
| `SingletonCompletedAppendsOnceWithoutLeavingActivePointer` | Replace outcomes with `CourseMilestone(1, 0, "completed", [{kind:stabilize}])`, resource mutations with `CourseMutation(1)`; real first publication leaves an active wound, null pointer and exactly one completed route; exact replay is inert. |
| `FinalAlreadyCompletedRouteDoesNotAppendAgain` | Initialize the new singleton route as already completed in a valid before-image; resolve final stabilization; require `RouteCompletion=None`, one retained route entry and null pointer, one actual treat history row. |
| `ActivePositiveMilestonePreservesCourseAndRetainedEffectGraph` | First milestone stabilize then reduce severity 1; final milestone stabilize, both with actual dose claims. First result retains the exact new active course ID and rematerializes all original retained roots under fresh IDs at severity I. Final clears pointer and appends route once. |
| `NonCourseTreatmentPreservesTheExactActivePointer` | Add a known guaranteed stabilization route before fixture creation; publish the real empty course start, move to next turn and resolve the guaranteed route against that current wound. Require unchanged course ID before/after, then next course ordinal remains 2. |
| `ChangedSelectionRejectsEvenWithFreshOuterResolutionSeal` | Use the resealed-construction code below with one changed axis per row: course ID, milestone ordinal, disposition, interruption, selected index, category, mode, consumption trigger, route fingerprint and route completion. Both initial Prepare and final preparation agreement must reject, no after-image/publication. |
| `PreparationOwnsDetachedSelectionAndRejectsChangedPointer` | Prepare one valid empty active milestone twice: identical fingerprints, no writes. Detached copy finalizes identically. Change before/after pointer or selected primitive in the supplied resolution graph and recompute public outer seals; original preparation must refuse it. |
| `PostWriteFailureRestoresPointerItemsEffectsAndHistoryThenRetriesOnce` | Use existing `ResourcePublicationFailureInjection` via fixture hooks. Arm failure at wound history after the target carrier has actually changed. Run real accepted-mechanics publication, require the injected write was reached and complete before-image restored (pointer/items/effect identity/wound identity/history/output). Retry the same retained accepted request through the production coordinator and require one spend and one treat row. |

The public `MortalWoundTreatmentResolution.Create` helper deliberately recomputes the
outer hashes. A negative test must use it so the rejection proves independent selected
course semantics, not merely a stale outer fingerprint:

```csharp
var altered = MortalWoundTreatmentResolution.Create(
    resolution.Mode, resolution.Coordinates, resolution.AttemptDisposition,
    resolution.ResultCategory, resolution.SelectedOutcomeIndex, resolution.Interruption,
    resolution.DeclaredResult, resolution.OutcomeIntents, resolution.CriticalReactionIntent,
    resolution.ConsumptionTrigger, resolution.CourseId + "_foreign",
    resolution.CourseMilestoneOrdinal, resolution.CourseDisposition, request,
    resolution.ModeEvidence, resolution.RouteFingerprint, resolution.RouteCompletion);
var invalid = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(
    acceptedState, request, altered, acceptedState.CurrentGameMinute);
Assert.False(invalid.IsValid);
Assert.Null(invalid.Preparation);
Assert.Contains(invalid.Issues, static issue => issue.Code ==
    "mortal_wound_treatment_publication_slice_unsupported");
```

For the other axes use the same constructor with exactly that selected argument changed.
Keep the valid route, complete request and untouched baseline constant. For finalization,
prepare before alteration and pass the altered resolution to the existing Finalize
signature; require invalid/no After. Keep the original course ModeAuthority and
ModeEvidence separately covered by exact detached validation, including outer/evidence
course-ID/ordinal/route mismatches. Do not weaken a type or add a test-only production API.

The existing `CourseTrustedUnsatisfiedInterruptionClearsPointerWithoutCompletingRoute`
and `CourseDeadlineDominatesSimultaneousTrustedUnsatisfiedRequirement` are the real
interruption controls: no extra dose, pointer cleared, no completed-route append.
Retain both original stale-history methods unchanged, including the successful request
after rejected stale history, which proves no prior reservation was leaked.

- [ ] **Step 4a: Add one complete GM-authored worked course and executable guards**

Add marker `mortal_wound_treatment_scalar_course_v1` and this same complete treatment
JSON to `OtherGuides/Wound_Materialization_Contract.md` and
`Examples/E_CLI_Wound_Materialization.txt`. This is the `treatment` member authored
inside the ordinary complete Mortal wound proposal, not a canonical-state patch or
an enabled fresh alternative-treatment adapter.

```json
{
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

Both documents explain these exact behaviors in player/GM language: empty result is
legal only for an active milestone; only the current milestone's dose is consumed;
start sets and continuations preserve the client's active course ID; completion or
trusted interruption clears it; route completion is not necessarily full wound healing;
stale history does not become a missed-dose outcome; other treatment does not cancel a
course. The medicines and provider in this worked setting are examples, not a global
medical catalog. Source IDs, course IDs, time evidence, attempt/operation coordinates
and fingerprints remain client-owned. Name the remaining five non-scalar producers
as pending implementation, while preserving their approved authored contracts.

Add exactly one manifest family `wound_mortal_scalar_course_v1`, update the exact-family
dictionary in `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`,
and add production guard
`ExampleDocumentationValidationTests.MortalWoundScalarCourseWorkedExample_ParsesCompleteRoute`:

```csharp
var treatment = Assert.Single(ParseNamedJsonFences(
    "E_CLI_Wound_Materialization.txt", "mortal_wound_treatment_scalar_course_v1"));
var rawRoute = Assert.Single(treatment["routes"]!.AsArray());
var parsed = MortalWoundTreatmentContract.ParseRouteShape(
    JsonSerializer.SerializeToElement(rawRoute), "example.treatment.routes[0]");
Assert.True(parsed.IsValid, string.Join(" | ", parsed.Issues.Select(issue =>
    $"{issue.Code}@{issue.FilePath}:{issue.Actual}")));
var course = Assert.IsType<MortalWoundCourseRouteDefinition>(parsed.Route);
Assert.Equal("field_clinic_recovery_course", course.RouteId);
```

This shape guard is not publication evidence: the actual three-milestone lifecycle
test above supplies that. Add
`PromptDocumentationCoverageTests.WoundTreatmentScalarCourseDocumentation_UsesCompleteMortalRoute`
to check guide/example JSON equality and the documented invariants, following the
existing ordered reduce-severity guard's production whole-wound Parse/ParseProjection
pattern. Use a severity-II one-root consequence graph valid at the severity-I destination;
never prune a player's graph at runtime. Reject copied client authority fields in the
worked JSON. Preserve old reduce-severity guard assertions and manifest entries.

- [ ] **Step 4b: Run the exact final owning controls once**

During implementation select the new failure/method with Focused. Once the complete
matrix is green, run these bounded owning selections; every filter term must select
at least one real test in discovery/TRX. Record exceptions if a genuine prerequisite
fails; do not erase it from the report or claim full course completion.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentContractTests|FullyQualifiedName~MortalWoundTreatmentMemberShapeTests|FullyQualifiedName~GmTreatmentRouteDraftTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatmentScalarCourseDocumentation_|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatmentReduceSeverityDocumentation_"
```

The existing explicit `WoundLegacySource_SurvivesWithoutActiveWoundButIsNeverPubliclyMaterializable`
row is a recorded required legacy-source RED, not this scalar course defect. In this
owning broad class checkpoint it must remain visible as that one known failure until
the legacy producer lands. All other selected rows must pass. Do not skip it or invent
a source registration in this task to disguise the remaining full legacy work.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseScalarPublication_|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseRequirementClassifier_StaleParsedHistoryIsInvalidAuthorityNotUnsatisfied|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseHighLevelPreparation_StaleHistoryRejectsBeforeResourceReservation|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseContinuation_ReconstructsInclusiveWindowAfterPublishedStartAndRestart|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseHighLevelPreparation_TooEarlyCreatesNoRequestOrReservation|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseTrustedUnsatisfiedInterruptionClearsPointerWithoutCompletingRoute|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseDeadlineDominatesSimultaneousTrustedUnsatisfiedRequirement|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseContinuation_CoalescesExactRetryButRejectsIndependentCourseIdMilestoneCollision|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseStart_RejectsSecondActiveCourseWithExactPointerDiagnostic|FullyQualifiedName~MortalWoundTreatmentResolverTests.CombatTarget_ProcedureAndCourseResolveAndPublishAgainstTheExactAcceptedCarrier|FullyQualifiedName~MortalWoundTreatmentResolverTests.ResourceFinalization_FirstCourseConsumesOnlyCurrentMilestoneResourceAndReleasesCommonPrecondition|FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureScalarOutcomePlanner_|FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureRepeatedStabilization_RejectsResealedFalseAppendOnceCompletion|FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureSingletonStabilization_PublishesWoundAnchorsRouteAndHistoryOnce|FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureNoImprovement_PublishesAttemptHistoryWithoutInventingImprovement|FullyQualifiedName~ExampleDocumentationValidationTests.MortalWoundScalarCourseWorkedExample_|FullyQualifiedName~ExampleDocumentationValidationTests.CompleteEffectMaterializationManifest_CoversEveryRequiredWorkedFamily"
```

No artificial `InstallPublishedCourseStart` call counts as publication evidence.
No new afterlife boundary is changed: record this Mortal-only no-update rationale.
The preceding task already ran FullValidation for its changed shared transport; do not
repeat it here solely because time passed. Run no PreMerge before the branch is ready.
After the owning selections, run one meaningful `-Lane Fast` checkpoint using the
unchanged five-minute budget. Report its actual descriptor/count/results, including
any previously planned legacy/non-scalar gaps; diagnose a newly introduced regression
with source evidence and a narrow filter. Do not repeatedly run Fast or start fixing
unrelated unfinished features under this scalar task.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

- [ ] **Step 5: Commit and independent task review**

Report every RED/intermediate/GREEN artifact with command/count/wall/exit/build/cleanup/
skip/duplicate/timeout details, changed files and actual GM/afterlife rationale. Parent
inspects actual artifacts/diff and obtains independent spec/quality review. This scalar
contour does not close T070/T177/#1536 or authorize remote push/merge.

## Explicit continuation after this scalar stage

All existing non-scalar course routes remain authored/validated in the complete model.
Full course completion tests ending in heal and harmful interruption tests remain
mandatory with the shared non-scalar outcome producers. Their gaps must stay visible
in the parent ledger; do not skip, weaken, delete or relabel them as unrelated.

The separate lasting-legacy source/Prepare/Finalize/history/publication contour is
tracked in T070. Its preparation currently loses cosmetic summary/provenance under
the literal closed data-model wording; parent must explicitly reconcile that data
contract before implementation. No source-registration-only checkpoint can close it.

## Parent pre-dispatch review

The complete course rules at `contracts/mortal-wound-treatment.md:548-608` and
`:1267-1312` were compared with this bounded task: current-world minute and resource
selection remain client-owned; original history classification and course-start
completeness proof are reused; pointer/route completion/result seals are explicit;
approved non-scalar producers remain required rather than removed. Source audit
confirmed the existing outcome request/Prepare/Finalize signatures and the exact
`CourseStartAuthority`/mode-evidence fields. The final fixture follow-up corrected
the old helper's compose-before-persistence and direct-normalizer assumptions through
the existing distribution/rehydration/coordinator path. No conflicting gameplay rule
or new user choice is introduced by this scalar implementation stage.
