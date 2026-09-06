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

**Status:** Accepted on 2026-09-06, exact task range `a393a4a0..3c0c77d1`.
Independent Spec review is compliant and task quality approved, with I1/I2/M1 resolved.
Parent inspected the complete change and all 28 actual summary/TRX/build artifact sets.
Final corrective owners pass pure42/42 and Integration46/46, with clean builds,
no skipped tests, duplicate descriptors, timeout or incomplete process cleanup.
The earlier Fast control remains fail-fast2655/2656, not a full Fast success; its
create-fixture failure is corrected and covered by the final focused control.
The unchanged mandatory legacy RED and the remaining T070/T177/#1536 work stay open.

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
  stay Integration. The source-backed migration below moves only preparation tests
  that now require real accepted authority. No runner, lane-definition, time-limit
  or project-file changes.
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
- Modify `BookOfEternityClient/Services/MortalWoundCriticalReactionPublicationPlanner.cs`
  for the course-specific no-reaction admission described in the source-backed
  implementation corrections below; preserve the existing procedure reaction path.
- Modify `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs` only to populate
  the treatment history row's course ID and milestone ordinal from its validated
  continuation resolution instead of the old hardcoded nulls.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentCapabilityAuthority.cs`
  only at the candidate's `HasCurrentModeAuthority` typed-mode gate: course uses the
  same parsed pre-turn history and full existing fresh-authority revalidation.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentResourceReservationRegistry.cs`
  only for exact pending course/milestone exclusivity in its shared `Restore` seam,
  after exact-operation replay and before any new reservation mutation.
- Modify `BookOfEternityClient.Tests/MortalWoundTreatmentSeverityReductionPlannerTests.cs`
  to retain its pure `Project_` coverage while moving only authority-dependent tests
  and their dependent helpers into
  `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.SeverityReductionPlanner.cs`.
  Reuse the existing genuine scenario/accepted-request fixture; preserve semantic
  and diagnostic coverage and record an exact old-to-new test/row inventory.
- Modify `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` only to add the two new
  Integration partial filenames to its exact resolver-family and reviewed-heavy source
  arrays. Preserve exact inventory equality. Parent synchronizes the corresponding
  `docs/testing.md` count and `specs/1505-test-suite-performance/research.md` inventory.
- Create `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.CoursePublication.cs`:
  actual course scalar publication, tamper, settlement, restart and rollback regressions.
- Modify `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.cs`
  in the shared successful publication helper and the source-backed fresh-fixture
  initialization/current-history handoff below; keep the two original stale-history
  methods and their assertions unchanged.
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

- [x] **Step 1: Observe actual setup RED before adding the new producer**

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

- [x] **Step 1a: Use authentic persisted holds and coordinated publication in the fixture**

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

- [x] **Step 2: Add independent course selection and the closed grammar**

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

- [x] **Step 3: Derive and seal course pointer changes**

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

- [x] **Step 4: Verify actual lifecycle and synchronize GM example**

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
| `ActivePositiveMilestonePreservesCourseAndRetainedEffectGraph` | Start at severity III with one retained legal root. First milestone stabilize then reduce severity 1; final milestone reduce severity 1, both with actual dose claims. First result retains the exact new active course ID and rematerializes retained roots under fresh IDs at severity II. Final reaches severity I, stays stabilized, rematerializes the retained graph, clears pointer and appends route once. |
| `NonCourseTreatmentPreservesTheExactActivePointer` | Use severity III with an empty course start, reduction1 intermediate and reduction1 final, plus a known guaranteed stabilization route before fixture creation. Publish the real empty course start, move to next turn and resolve the guaranteed route against that current wound. Require unchanged course ID before/after, then next course ordinal remains 2 and the remaining course is applicable. |
| `ChangedSelectionRejectsEvenWithFreshOuterResolutionSeal` | Use the resealed-construction code below with one changed axis per row: course ID, milestone ordinal, disposition, interruption, selected index, category, mode, consumption trigger, route fingerprint and route completion. Both initial Prepare and final preparation agreement must reject, no after-image/publication. |
| `PreparationOwnsDetachedSelectionAndRejectsChangedPointer` | Prepare one valid empty active milestone twice: identical fingerprints, no writes. Detached copy finalizes identically. Change before/after pointer or selected primitive in the supplied resolution graph and recompute public outer seals; original preparation must refuse it. |
| `PostWriteFailureRestoresPointerItemsEffectsAndHistoryThenRetriesOnce` | Use existing `ResourcePublicationFailureInjection` via fixture hooks. Arm failure at wound history after the target carrier has actually changed. Run real accepted-mechanics publication, require the injected write was reached and complete before-image restored (pointer/items/effect identity/wound identity/history/output). Retry the same retained accepted request through the production coordinator and require one spend and one treat row. |

The public `MortalWoundTreatmentResolution.Create` helper deliberately recomputes the
outer hashes. A negative test must use it for constructible changed axes so the
rejection proves independent selected course semantics, not merely a stale outer
fingerprint. It already rejects a mode different from the request mode: retain that
construction rejection and independently mutate a detached resolution to cover
Prepare/Finalize rejection instead of weakening the constructor:

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

- [x] **Step 4a: Add one complete GM-authored worked course and executable guards**

Add marker `mortal_wound_treatment_scalar_course_v1` and this same complete treatment
JSON to `OtherGuides/Wound_Materialization_Contract.md` and
`Examples/E_CLI_Wound_Materialization.txt`. This is the `treatment` member authored
inside the ordinary complete Mortal wound proposal, not a canonical-state patch or
an enabled fresh alternative-treatment adapter.

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

- [x] **Step 4b: Run the exact final owning controls once**

During implementation select the new failure/method with Focused. Once the complete
matrix is green, run these bounded owning selections; every filter term must select
at least one real test in discovery/TRX. Record exceptions if a genuine prerequisite
fails; do not erase it from the report or claim full course completion.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentContractTests|FullyQualifiedName~MortalWoundTreatmentMemberShapeTests|FullyQualifiedName~GmTreatmentRouteDraftTests|FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatmentScalarCourseDocumentation_|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatmentReduceSeverityDocumentation_"
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

- [x] **Step 5: Commit and independent task review**

Report every RED/intermediate/GREEN artifact with command/count/wall/exit/build/cleanup/
skip/duplicate/timeout details, changed files and actual GM/afterlife rationale. Parent
inspects actual artifacts/diff and obtains independent spec/quality review. This scalar
contour does not close T070/T177/#1536 or authorize remote push/merge.

## Source-backed implementation corrections

The parent inspected the actual lower-level fixtures and producer gates after the
new Integration RED `20260906-155219-134-27256-97a1791cbdf6441fbcce42f951695dec-focused`.
These corrections preserve the approved gameplay and broaden only the implementation
scope required to exercise it:

- `MortalWoundTreatmentWorkingWoundSimulator.TryApply` forbids stabilizing an already
  stabilized wound. The new active-positive fixture therefore uses the corrected
  III → II → I route in the table above, not repeated stabilization. Keep complete
  course-start applicability validation and the original terminal-healing tests.
- `MortalWoundCriticalReactionPublicationPlanner.Compose` previously admitted only
  guaranteed treatment without a reaction and rejected every non-procedure mode.
  Admit course with no critical reaction only after exact request/resolution mode,
  current accepted-state binding, complete detached authority and independently
  recomputed course selection checks. Cover unexpected reaction intent and changed,
  foreign or stale authority directly. Reuse cohesive shared checks; do not weaken
  the procedure reaction/reservation path or invent a course reaction event.
- The owning pure severity-reduction fixtures directly constructed preparations
  and resolutions with null request authority and placeholder hashes. Independent
  finalization validation makes these invalid. Source inspection confirms there is
  no existing pure accepted-authority producer: the private accepted-state authority
  owns a filesystem, write lease, canonical root revision and current history, and
  actual request factories require that live authority. Under the user's explicit
  Fast/runtime taxonomy instruction, move only the dependent preparation/finalization
  and rematerialization-DAG tests to the Integration partial above. Keep every pure
  `Project_` test and its helpers Fast. Use genuine detached requests produced by the
  existing Integration fixture; lower-level checks do not each need full publication.
  Preserve ordered reductions/stabilization, selectors, zero-root rematerialization,
  authority-DAG and exact diagnostic assertions; add a separate authority-free rejection.
  Do not turn positive tests into rejection tests, bypass production validation,
  fabricate live authority or introduce stored authority-snapshot fixtures. Record exact
  moved/retained test and theory-row counts, cover both owning projects, and include
  the new method prefix in the final Integration selection.
- The exact migration inventory is 19 methods / 41 existing rows (12 Facts and seven
  Theories with 29 rows) into the Integration partial, retaining method suffixes;
  four `Project_` methods / five rows (three Facts and a two-row Theory) stay Fast.
  The separate missing-authority regression adds one Integration row.
- The next actual publication RED
  `20260906-155848-940-28828-5cfb242a92e74a57bec9e491ad2f1981-focused` is
  `wound_history_treatment_result_coordinate_mismatch`: the shared accepted wound
  planner hardcodes null course coordinates while its sealed receipt contains them.
  Populate only those two row fields from `continuation.Resolution.CourseId` and
  `.CourseMilestoneOrdinal`. Keep the existing history schema, codec and exact receipt
  validator unchanged; assert row/receipt equality after publication and restart.
- Candidate validation next failed at `publication_provenance_mismatch` in
  `20260906-160606-417-8112-968a22a4521649d6b963ccd2892b5280-focused` (two migrated
  positive fixture tests pass, actual course lifecycle fails). Source inspection of
  `HasCurrentModeAuthority` and the complete existing fresh-authority validator confirms
  an obsolete procedure-only precondition. Admit exactly the typed course route/mode
  authority pair alongside procedure, then retain the same history parse and
  `MortalWoundTreatmentFreshAuthorityValidator.FindMismatch` call, including real course
  time/start/history/requirements recomposition. No early-true course or new authority.
- New Integration partials require exact source-inventory synchronization: add
  `MortalWoundTreatmentResolverTests.CoursePublication.cs` and
  `MortalWoundTreatmentResolverTests.SeverityReductionPlanner.cs` to both owning Fast
  source arrays; reviewed-heavy entries increase from 58 to 60. The owning class's
  Integration category remains unchanged. Include `FastTestBoundaryTests` in the pure
  control; preserve historical test-result evidence when updating the current docs.
- Original publishing scenarios used an empty default history despite already-existing
  wound identity. At the top of `AcceptedStateFixture.Create`, before writes and after
  caller-authored wound changes, initialize only `Mode=="course"` with the exact empty
  default `CreateHistory()` value to `CreateCurrentWoundHistory(scenario.Before)` and
  `SeedCanonicalWoundEffects=true`. This is initial creation history and authentic source
  effects, never an installed course/treat row. Preserve explicitly supplied nonempty or
  stale histories. `ExecutePreparedFlow` retains its scenario parse assertions but passes
  `fixture.ReadCurrentHistory()` to real preparation/resolution after fixture creation;
  do not pass the now-stale parsed default. Include its original course theory in controls.
- The NEW non-course-interference fixture also needs a complete applicable remaining
  course: start severityIII with empty first stage, reduction1 second and reduction1
  final; the independent guaranteed treatment stabilizes between stages. Retain the
  original pointer-preservation and next-ordinal assertions; no repeated stabilization.
- The worked scalar-course JSON is a complete `treatment`, not a fragment: add its
  previously omitted required `diagnosisPaths: []` in BOTH documents and this plan.
  The guard must embed exactly that unmodified documented object. Do not synthesize a
  missing member only inside the guard or downgrade the documented contract to a fragment.
- The measured final Integration control
  `20260906-162730-219-19636-22b9e2fc2c7042f8a54b01cfc5a08bf4-focused` ran112 rows in
  7:15.323 with an explicit10-minute Focused limit:103 pass, nine fail, no timeout or
  cleanup failure. All28 new scalar rows,42 migrated rows and the two frozen stale-history
  tests passed. Source-backed remaining fixture corrections are limited to adding the
  already-existing `SkillId` to `AssertRequirementBinding`'s exact closed property list,
  and preserving the original one-root consequence graph in `CreateCombatTreatmentScenario`
  with the correct `combatant` effect target kind (including combatant-member owners),
  followed by genuine initial history/effect seeding for actual procedure publication.
  Do not weaken the property-list equality, original carrier assertions or production
  severity envelope. These eight fixture rows receive a narrow correcting control.
- The two procedure rows of
  `CombatTarget_ProcedureAndCourseResolveAndPublishAgainstTheExactAcceptedCarrier`
  then exposed the older direct publication helper's unconfirmed reservation: even
  `not_required` owns an agreement. In that owning test only, route procedure success
  through genuine persistence/rehydration and the existing coordinated publisher;
  preserve its course branch and all original actor/carrier/history assertions.
  Do not widen the global helper or production reservation gate. Cover both exact rows.
- The ninth failure exposed actual pending-course uniqueness: the reservation registry
  keyed only by operation, allowing another operation for the same course and milestone.
  `ResourceComposer` always calls `ReserveTreatmentResources` for both held and
  `not_required` authorities, including trusted interruptions; cold `RestoreConfirmed`
  uses the same `Restore`, and normal release/rollback removes the same live agreement.
  In `Restore`, after exact-operation retry handling and before new agreement/aggregate/
  map mutation, reject an existing non-null exact `CourseId` plus milestone ordinal.
  Do not filter by resource disposition or add a second registry/schema. Preserve
  finalized-history replay. Add six real `CourseScalarPublication_` matrix rows:
  held/no-resource/trusted-interruption crossed with live/cold restoration. Each proves
  exact retry, different-operation conflict with unchanged tree, and idempotent release
  followed by legal reacquisition; use real first publication/distribution/restoration.
  Keep the original collision test unchanged. Synchronize the corresponding guide/example
  rule without adding GM-authored identities or manual canonical-edit instructions.

After these corrections, cover the changed behavior and its concrete dependency owners
with narrow final controls. Retain the unaffected verified rows from the112-row control
with an exact final-code coverage explanation; do not repeat all112 merely as a ritual.
The single meaningful final Fast checkpoint is still required.

The implementer records each correcting RED and final covering selection in the
same task report; these additions are included in its exact BASE-to-final-HEAD review.

## Explicit continuation after this scalar stage

### Post-review correction scope (2026-09-06)

Independent review of `a393a4a0..7be8e356` requires removing the unused copied
`CreateRankFourReactionWound` from the Integration partial (I1). Keep the pure owner.
Move the scalar worked example inside the existing content/CDATA wrapper and make
its owning source guard reject placement outside that wrapper (M1).

Parent call-site inspection and focused reviewer confirmation found one additional
authority-dependent caller: four rows of
`WoundEffectBatchPlannerTests.SkillScope_AcceptedTreatmentRematerializationDoesNotRebindOrRequireProposalCoordinates`
reflect into the now-removed synthetic resolution/preparation/input helpers (I2).
Observe that exact RED; migrate only this theory into the existing Integration
`ResolverTests.SeverityReductionPlanner.cs` partial. Use the genuine accepted request,
persistence/rehydration and coordinated treatment plan to obtain its prepared batch
and effect input. Preserve all four scenarios and their unchanged-selector/internal
diagnostic/no-proposal-coordinate assertions; no synthetic authority or copied whole
effect-input factory. The five invalid-mapping Fast rows remain pure and unchanged.
This adds four existing rows to the previous 41-row migration; no extra heavy file.

The actual final Fast also exposed a T169/T174 fixture omission under T177/#1536:
`EffectAcceptedTurnInputComposerWoundTests.Compose_FeedsPreparedWoundDirectlyIntoTypedEffectStage`
does not supply its real original-proposal diagnostic mapping. In that positive
fixture only, construct the exact response-local decision0/definition0 component
coordinate, call `EffectApplicationDiagnosticLocations.BindPreparedSources`, attach
it to the composed input, and assert non-null scope authority plus exact permanent
key/path/section resolution. Preserve all existing success/application/termination/
raw-command assertions. The actual production service already supplies this mapping;
do not change the composer/planner or weaken the missing/duplicate/wrong-path guards.

One correction pass covers I1/I2/M1 and this observed pure fixture failure, through
bounded Focused pure and Integration controls. The prior fail-fast Fast is not a
full-suite success; no automatic broad rerun is required for these small edits.
Parent records exact additional row ownership and carries the remaining legacy RED
and incomplete overall Fast control forward. T070/T177/#1536 stay open.

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
