# T070 Treatment Recovery Publication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Execute one reviewed task and one bounded C# lane at a time.

**Goal:** Publish every already-legal ordered `add_recovery` result combined with the
currently implemented nonterminal treatment operations through the existing atomic pipeline.

**Architecture:** Extend the existing immutable outcome preparation's typed payload
seals and scalar projection. Recovery points accumulate with checked signed-64-bit
arithmetic; they do not introduce an implicit recovery tick, severity change or heal.
Reuse the existing severity rematerializer only when the selected result explicitly
contains a reduction, and retain the sole accepted-mechanics publisher.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T070, FR-064 and
`contracts/mortal-wound-treatment.md`. Read-only evidence is retained at
`sdd/t070-nonscalar-outcome-source-audit.md` in Git metadata.

**Execution gate:** Scalar-course implementation `a393a4a0..3c0c77d1` is independently
reviewed and parent-accepted. Its correction commit changes tests/examples only;
the shared production interfaces were checked against `7be8e356`. Parent records the
subsequent scoped bookkeeping commit as the exact task BASE in the dispatch brief.

## Global Constraints

- Stay in `E:/Games/worktrees/boe-1536-wound-materialization` on the existing
  `1536-complete-wound-materialization` branch. No remote mutation or `.serena` access.
- The complete selected result is bounded to eight operations. `no_improvement` is
  sole-only; an empty result is legal only for an active course milestone.
- Positive `add_recovery.points` is signed 32-bit. All ordered recovery-point
  accumulation and application to signed-64-bit canonical progress uses checked
  arithmetic. Overflow rejects before die consumption or publication.
- Aggregate severity reduction is at most two without a `heal`. Do not change the
  route parser's complete union or remove approved operations to make this stage pass.
- This producer changes recovery progress only, plus the ordinary attempt/transition/
  route/course shell and changes explicitly selected by other implemented operations.
  Reaching a threshold never invents a tick, severity reduction or healed wound.
- Retain exact course selection, live/detached authority, Fate/critical reaction,
  resource settlement, replay and rollback behavior. No null-authority fallback,
  fabricated receipt, snapshot fixture or test-installed accepted treatment history.
- `heal`, `remove_complication`, `add_complication` and `apply_deterioration` remain
  tracked producers, not silently supported or deleted. This does not close T070,
  natural recovery T069, T177 or #1536.
- Pure parser/projection/source-guard tests stay Fast; genuine accepted-state,
  persistence, lease, publication and restart tests stay Integration. No runner,
  lane-definition, project-file, timeout or concurrency changes.
- One implementer and one C# lane. Use smallest Focused selections; no ritual repeat
  of the preceding course task's meaningful Fast checkpoint or unrelated full lanes.
- If a correct focused filter also runs coherent extra passing rows, verify that the
  required subset is present in the actual TRX and report both counts. Do not rerun
  unchanged code solely to make the total equal an expected count. Still reject
  missing owners, actual duplicate descriptor execution or an unintended broad suite.
- Synchronize the existing Mortal worked procedure example and its actual parser
  guards. There is no new GM field, afterlife command or spiritual mechanic.

### Task 1: Sealed ordered recovery accumulation and actual publication

**Files:**

- Modify `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`:
  `IntentSeal`, ordered agreement, preparation fingerprint, supported positive grammar,
  Prepare's intent switch and scalar-shell projection.
- Create `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.RecoveryPublication.cs`:
  new `RecoveryPublication_` real-pipeline owners using the existing partial family's
  fixture; no copied accepted-state runtime implementation.
- Modify `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` only for the additional
  filename in both exact Integration-family/heavy-source arrays. Synchronize the current
  count/inventory in `docs/testing.md` and `specs/1505-test-suite-performance/research.md`.
- Modify `OtherGuides/Wound_Materialization_Contract.md`,
  `Examples/E_CLI_Wound_Materialization.txt`, `Examples/example_validation_manifest.json`
  and `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs` for the
  existing complete worked procedure and accurate implemented/pending status.
- No change to natural recovery, effect allocation, history codec, source authority,
  capability limits or accepted-command/resource protocol is anticipated. Escalate a
  genuine additional producer dependency with actual source/failure evidence.

**Existing interfaces and source facts:**

```csharp
MortalWoundTreatmentOutcomePublicationPlanner.Prepare(
    acceptedState, request, resolution, acceptedState.CurrentGameMinute);
MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
    preparation, resolution, effectBatch, applicationResults);
MortalWoundTreatmentSeverityReductionPlanner.Project(before, steps, eventRef);
```

`MortalWoundAddRecoveryOutcomeIntent.Points` is already production-derived and sealed
by T067. `WorkingWoundSimulator.TryApply` already performs only checked addition.
The wound parser allows progress up to `Int64.MaxValue` independently of the threshold.
The severity projector changes severity and slot budget, not progress/threshold/anchors;
therefore the existing scalar-shell-then-aggregate-reduction architecture remains valid.
Preparation independently reconstructs the scalar shell in `AgreesWith`; extend that
same path, not a caller-authored after-image or an extra publisher.

- [x] **Step 1: Add the owning real fixture and initial publication RED**

The existing `CreateOrderedReductionScenario`, `PrepareProcedurePublicationScenario`,
`PersistAndRehydrateTreatmentPublication` and `ComposeAndPublishCoordinatedTreatment`
helpers are available in the same Integration partial class. Build local authored
routes before the genuine fixture exports any authority:

The new partial uses `System.Globalization`, `System.Text.Json.Nodes`,
`BookOfEternityClient.Core`, `BookOfEternityClient.Services` and xUnit. In particular,
`FileSystemManagerHooks` belongs to `BookOfEternityClient.Core`; use the existing typed
`row.TreatmentResult.Receipt` for treatment history, not the abstract result base.

```csharp
private static ResolverScenario CreateRecoveryPublicationScenario(
    string mode, string category, string shape, long progress = 0)
{
    var tokens = shape.Split(',');
    var scenario = CreateOrderedReductionScenario(mode, category, "r1", 1);
    var operations = new JsonArray(tokens.Select(token => (JsonNode)(token switch
    {
        "s" => new JsonObject { ["kind"] = "stabilize" },
        "r1" => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
        "r2" => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
        _ when token.StartsWith("a", StringComparison.Ordinal) => new JsonObject
        {
            ["kind"] = "add_recovery",
            ["points"] = int.Parse(token.AsSpan(1), CultureInfo.InvariantCulture)
        },
        _ => throw new ArgumentOutOfRangeException(nameof(shape), token, null)
    })).ToArray());
    var route = scenario.Before["treatment"]!["routes"]![0]!;
    foreach (var band in route["outcomes"]!.AsArray().OfType<JsonObject>())
        band["result"] = operations.DeepClone();
    scenario.Before["recovery"]!["currentStepProgress"] = progress;
    scenario.Before["recovery"]!["currentStepThreshold"] = 2;
    return PrepareProcedurePublicationScenario(scenario with
    {
        OperationKey = "operation_t070_recovery_" + mode + "_" +
            category + "_" + shape.Replace(',', '_'),
        ExpectedIntentCount = tokens.Length
    });
}

[Fact]
public void RecoveryPublication_AccumulatesWithoutImplicitThresholdTransition()
{
    var scenario = CreateRecoveryPublicationScenario("procedure", "success", "a2", 1);
    using var fixture = AcceptedStateFixture.Create(scenario);
    var before = fixture.ReadCurrentWound();
    var initial = ResolveCurrentTreatment(fixture, "procedure",
        scenario.OperationKey, scenario.RouteId);
    var flow = PersistAndRehydrateTreatmentPublication(fixture, initial, "recovery publication");
    ComposeAndPublishCoordinatedTreatment(fixture, flow);
    var after = fixture.ReadCurrentWound();
    Assert.Equal(3L, after.Recovery.CurrentStepProgress);
    var beforeRoot = CanonicalWoundRoot(before);
    var afterRoot = CanonicalWoundRoot(after);
    Assert.True(JsonNode.DeepEquals(beforeRoot["severity"], afterRoot["severity"]));
    var expectedRecovery = beforeRoot["recovery"]!.DeepClone();
    expectedRecovery["currentStepProgress"] = 3L;
    Assert.True(JsonNode.DeepEquals(expectedRecovery, afterRoot["recovery"]));
    Assert.Equal(before.Care.State, after.Care.State);
    Assert.Single(fixture.ReadCurrentHistory().State!.Transitions,
        static row => row.Kind == "treat");
    fixture.RestartForReplay();
    Assert.Equal(3L, fixture.ReadCurrentWound().Recovery.CurrentStepProgress);
    var tree = CaptureResolverFixtureTree(fixture.Root);
    var replay = ProbePublishedTreatment(fixture, flow.Request);
    Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
    AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
}
```

Use semantic JSON equality for collection-containing records, not collection instance equality.
Provider `sterile_thread` is the procedure's exact existing consumed item; guaranteed
fixtures have an actual materialized provider capability with `maximumRecoveryPoints=2`.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.RecoveryPublication_AccumulatesWithoutImplicitThresholdTransition"
```

Expected RED is `mortal_wound_treatment_publication_slice_unsupported`, not an invalid
fixture, history or authority setup. Fix only a demonstrated fixture prerequisite.

- [x] **Step 2: Retain and recompute the exact recovery payload**

Add nullable `int? RecoveryPoints` to the existing private immutable `IntentSeal` after
`ReductionSteps`; append this value in `From`, compare it in `AgreesWith`, and preserve
the existing detached-copy implementation. The added expressions are:

```csharp
(intent as MortalWoundAddRecoveryOutcomeIntent)?.Points

RecoveryPoints == (intent as MortalWoundAddRecoveryOutcomeIntent)?.Points
```

Add the exact typed comparison to `OrderedIntentsAgree`:

```csharp
(left as MortalWoundAddRecoveryOutcomeIntent)?.Points !=
    (right as MortalWoundAddRecoveryOutcomeIntent)?.Points
```

Increment only the preparation fingerprint format from `"2"` to `"3"`; every ordered
intent appends points after reduction steps, including null for other branches:

```csharp
fields.Add((intent as MortalWoundAddRecoveryOutcomeIntent)?.Points
    .ToString(CultureInfo.InvariantCulture));
```

The publication/cache/continuation seals already include preparation fingerprint.
No stored-version migration, public field or alternate accepted effect batch is added.

- [x] **Step 3: Extend the complete positive grammar and scalar projection**

Replace only the private positive scalar grammar, leaving the surrounding mode-specific
empty/sole-no-improvement handling intact:

```csharp
private static bool HasPositiveScalarGrammar(
    IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents)
{
    if (intents.Count is < 1 or > MortalWoundTreatmentContract.MaxOutcomeOperations)
        return false;
    var stabilizations = 0;
    var reductionSteps = 0;
    foreach (var intent in intents)
    {
        switch (intent)
        {
            case MortalWoundStabilizeOutcomeIntent:
                if (++stabilizations > 1) return false;
                break;
            case MortalWoundAddRecoveryOutcomeIntent recovery when recovery.Points > 0:
                break;
            case MortalWoundReduceSeverityOutcomeIntent reduction
                when reduction.Steps is >= 1 and <= 2:
                reductionSteps = checked(reductionSteps + reduction.Steps);
                if (reductionSteps > 2) return false;
                break;
            default:
                return false;
        }
    }
    return true;
}
```

Prepare's registered-intent switch adds `case MortalWoundAddRecoveryOutcomeIntent: break;`
alongside stabilization; aggregate reduction remains unchanged. The scalar-shell loop
uses the existing stabilization helper and adds this exact ordered branch:

```csharp
else if (intent is MortalWoundAddRecoveryOutcomeIntent recovery)
{
    scalarAfter = scalarAfter with
    {
        Recovery = scalarAfter.Recovery with
        {
            CurrentStepProgress = checked(
                scalarAfter.Recovery.CurrentStepProgress + recovery.Points)
        }
    };
}
```

Do not reset anchors, threshold, carry policy, blockers or last tick for recovery-only
results. The existing preparation catch and detached agreement must reject overflow.
Sealed upstream applicability still rejects before creating/consuming a live roll claim.

- [x] **Step 4: Complete the production-backed matrix and tamper/rollback controls**

All new methods use `RecoveryPublication_`; derive every positive request through the
real existing fixture. Preserve original tests. Cover the following exact cases:

| Method suffix | Concrete case and required evidence |
| --- | --- |
| `ThresholdAndMaximumProgressRemainNonterminal` | Procedure `a1` from progress0,1,2 and `long.MaxValue-1`; expect1,2,3,MaxValue. Repeat severity-I threshold crossing with one legal retained root: active wound, no heal row, no legacy and no severity change. Preserve all other recovery members and complete effects/identities. |
| `OrderedRecoverySupportsEveryImplementedCombination` | Procedure success shapes `a2`, `a1,a1`, `s,a1`, `a1,s`, `a1,r1`, `r1,a1`, `a1,r1,s,a1`, `r1,a1,r1`, and eight `a1` operations. Preserve exact intent ordinals/points and final progress; reductions use genuine fresh retained roots, recovery-only retains exact root IDs. |
| `SelectedCategoryAndGuaranteeKeepExistingCompletionRules` | Procedure success/partial_success/failed_attempt with `a1`, plus guaranteed `a1,a1` and `s,a2`. Assert exact actual category/route completion, one charge where policy applies, correct die settlement, and genuine capability maximum; no grant from route authoring alone. |
| `CourseAccumulatesAcrossRealMilestonesAndRestart` | Local scalar-course variant: empty first stage, second `a1,reduce_severity(1)`, final `a1,stabilize`. Actual0/480/960-minute publications consume only each current dose, preserve/clear pointer correctly, progress2, exact course row/receipt coordinates, cold replay inert. |
| `OverflowRejectsBeforeReservationAndPublication` | Procedure and guaranteed progressMaxValue with `a1`; procedure progressMaxValue-1 with `a1,a1`. Call the genuine high-level preparation result directly, not the success-only helper. Require invalid request/no request, unchanged tree/items/history, then prove an independently legal route can still reserve the original die/item. Reuse the existing checked-difficulty/slot-overflow rejection setup pattern. |
| `DetachedPreparationRejectsChangedRecoveryPayload` | Start with valid recovery preparation. Change points, ordinal, operation kind, ordered position, or provisional progress independently. Construct fresh outer resolution seals where legal; original preparation/finalization must reject exact changed payload and expose no after-image. Keep a same-input detached-copy positive control. |
| `PostWriteFailureRestoresProgressResourcesAndHistoryThenRetriesOnce` | Persist/rehydrate procedure `a2`, compose one coordinated plan, arm existing ResourcePublicationFailureInjection at WoundHistoryState.HistoryPath after actual target-carrier write. Require hook fired/earlier write observed/exact complete tree restored, then retry the same held request/plan once: one charge, one progress increment and one treat row. |

For recovery-only effect preservation, compare the current canonical effect carrier and
identity projections, not the whole player file that intentionally gains wound progress.
Use a coherent non-reactive consequence for strict unchanged-effect threshold fixtures.
The shared characteristic-modifier helper defaults to magnitude2; the recovery fixture
must explicitly use magnitude1 so its same retained graph remains legal at severityI.
Its passive definition has an empty triggers array, not an unrelated trigger substitution.
Do not strip last-transition chronology or whole identity-history arrays to hide a
periodic fixture's ordinary turn-trigger evidence. If using such a periodic fixture,
assert its unchanged prior history plus the exact protocol-justified trigger suffix.
For member preservation, compare `CanonicalWoundRoot(before)["recovery"]` and after with
only `currentStepProgress` changed. In mixed stabilization cases assert the existing
stabilization anchor semantics separately rather than demanding an unchanged anchor.

Reuse the actual rollback pattern:

```csharp
var fault = new ResourcePublicationFailureInjection();
// Create the owning real fixture with BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync.
var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
var tree = CaptureResolverFixtureTree(fixture.Root);
fault.Arm(WoundHistoryState.HistoryPath, fixture.TargetCarrierPath,
    ReadCanonicalBytes(fixture, fixture.TargetCarrierPath), fixture.FileSystem);
Assert.Throws<CanonicalStateWriteException>(() => PublishCachedResourcePlanOpen(fixture, flow, plan));
Assert.True(fault.Fired);
Assert.True(fault.ObservedEarlierResourceWrite);
AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
publication.CompleteAtFullPipelineEnd();
```

- [x] **Step 5: Update the existing complete GM example and exact guards**

In BOTH the guide and CLI example, under `mortal_wound_treatment_reduce_severity_v1`,
change only `clean_close_partial.result` from sole stabilization to:

```json
[
  { "kind": "stabilize" },
  { "kind": "add_recovery", "points": 2 }
]
```

Keep the complete treatment JSON identical between documents. Immediately after its
existing explanation add the same text to both:

> The partial result stabilizes and adds two recovery points with checked signed-64-bit arithmetic. With progress 1 and threshold 2, the accepted result has progress 3 and the wound stays active at its unchanged severity. Reaching the threshold does not itself trigger a recovery tick, severity reduction or healing. The GM authors points, never canonical progress or recovery anchors; the client applies the selected ordered operations and exact resource policy atomically. Partial success does not complete the route.

Extend the existing `WoundTreatmentReduceSeverityDocumentation_UsesParsedOrderedMortalRoute`
guard to assert the parsed partial band's exact ordered stabilize/add_recovery(2) payload,
then run `MortalWoundTreatmentWorkingWoundSimulator.Simulate` with that result on a wound
whose progress is1/threshold2 and require progress3, unchanged severity and applicability.
Retain the existing success reduction/destination/privacy assertions. Update the existing
`wound_treatment_scene_authority_v1` manifest description/validation route/coverage limit
and required prose; do not add a duplicate family or claim this pure guard proves publication.
Update scalar-course status prose/guard so pending list excludes only `add_recovery` and
still includes all four unresolved producers. Do not modify the repair example's deliberate
invalid mode or any hidden authoring dialect to demonstrate publication.

- [x] **Step 6: Run final bounded owners, inspect evidence and commit**

Add the new Integration source filename to both exact Fast inventories; the reviewed-heavy
source count becomes61 after the accepted course stage's60. Keep the Integration class
category and all historical test-results unchanged when synchronizing docs.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests|FullyQualifiedName~FastTestBoundaryTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatmentReduceSeverityDocumentation_|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatmentScalarCourseDocumentation_"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.RecoveryPublication_|FullyQualifiedName~MortalWoundTreatmentResolverTests.CourseScalarPublication_|FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureScalarOutcomePlanner_|FullyQualifiedName~MortalWoundTreatmentResolverTests.OutcomeIntents_AreProductionDerivedOneForEachDeclaredOperation|FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureCheckAuthority_OutcomeIntentClosedUnionAndPayloadsAreExact|FullyQualifiedName~ExampleDocumentationValidationTests.MortalWoundScalarCourseWorkedExample_|FullyQualifiedName~ExampleDocumentationValidationTests.CompleteEffectMaterializationManifest_CoversEveryRequiredWorkedFamily"
```

Every filter term must select an actual test. If the coherent Integration group genuinely
exceeds its default budget, record measured evidence and use an explicit bounded timeout
per `docs/testing.md`; do not repeatedly optimize seconds or drop cases. No unrelated
FullValidation/DeepValidation/PreMerge is required for this Mortal-only scalar seam.
The final combined control explicitly allows ten minutes because the previous course
owning112-row control measured7:15.323 and its new34-row course family already includes
cold-root/resource lifecycle work. A known-expanded group need not first hit five minutes
just to rediscover that evidence; focused single-method implementation controls retain
the default budget. No persistent lane setting changes.

Measured execution correction: the combined Integration control182549 reached
10:00.2227223 with exit124 and no completed TRX, after exposing the fixture's invalid
rank-I modifier. Preserve that run as incomplete timed-out evidence. After the narrow
ordered-owner correction, split the exact required union into two sequential selections:
`RecoveryPublication_`, then the original filter with only that term removed. Each may
use the existing explicit ten-minute budget. Do not rerun the passed24-row pure control
unless its inputs change, and do not omit any required owner from the union.

Report each RED/GREEN artifact, exact commands/counts/wall/exit/build/cleanup/skip/duplicate/
timeout evidence, changed files and GM/afterlife rationale. Commit only the scoped changes.
Parent inspects actual diffs/artifacts and obtains independent Spec and Quality approval
before accepting this stage. T070 and #1536 remain open.

## Parent source/spec review

The complete eight-operation union, checked arithmetic and separate terminal-heal rules
were compared with FR-064 and the actual parser/simulator/capability authority. Existing
prepared scalar and severity stages retain recovery progress/anchors, so no natural-tick
semantics or effect-side operation is needed for recovery-only. The new matrix covers
all admitted modes, authored ordering, real resources, detached mutation, restart/replay
and post-write rollback. Legacy preparation's pending user choice does not affect this
accumulator-only producer; complication removal remains its separate lineage-aware stage.

## Parent acceptance — 2026-09-06

Accepted `48911240..189ba934`: independent Spec compliant / Quality approved, no
Critical or Important findings. Parent inspected the nine-file production/test/GM diff
and all19 actual summary/TRX/build artifact sets, including fixture corrections and
the incomplete ten-minute combined run. Final Focused pure24/24
(`20260906-182431-529-13304-4991a3a8db944f98a523f697a0bb2e85-focused`) and the exact
split Integration union12/12 +62/62
(`20260906-183958-416-468-5a5dac3995d0462c9cda0c0f7783ccea-focused`,
`20260906-184547-366-31900-04f419227f444160ab1b57e3648ab257-focused`) pass with clean
builds and cleanup, no skipped/duplicate rows or timeout. This is not a full Fast result.
The review's artifact caveat is resolved by that parent audit. Its unchanged Fate/
critical caveat is resolved by source inspection of the independent detached selection,
critical publication and continuation-fingerprint gates, plus the actual retained
course/selection controls; no dedicated new full Fate suite is claimed.

Minor M1 (redundant guaranteed-capability equality in the new Integration partial)
is retained for final whole-branch triage in the progress ledger. Mortal guide, complete
worked example, manifest and parser/simulator guards are synchronized; there is no new
afterlife contract. T070/T069/T177/#1536 remain open. Next execute the separately
specified selective-complication-removal publication stage in this same branch.
