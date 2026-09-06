# T070 Selected Non-Death Policy Publication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax. One implementation/C# owner; parent owns acceptance.

**Goal:** Publish selected Mortal treatment `increase_severity` and nested
`add_complication` policies through the existing atomic wound/effect/resource/history
transaction, including mixed ordered results and exact replay.

**Architecture:** T067's accepted private policy preparation supplies the complete
typed policy and ordinal-owned bindings; live admission still reobtains T069.
Pre-roll applicability and selected publication share the same pure policy graph
projection. One ordered simulation produces one final graph, one effect batch and
one final canonical wound assembly; rematerialization depends on original versus
final severity, not on the presence of an intermediate reduction operation.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/{spec,plan,tasks}.md`, T067/T069/T070,
`contracts/mortal-wound-treatment.md`, `contracts/wound-effects-and-atomicity.md`,
and `contracts/wound-canonical-lifecycle.md` in that feature. Accepted baseline
`55fdc7f5` records private preparation runtime `177a734e` plus test assertion
`caa2ef17`. Direct-add publication and its final continuity proof already exist.

## Global Constraints

- Stay on `1536-complete-wound-materialization` in
  `E:/Games/worktrees/boe-1536-wound-materialization`; preserve unrelated `.serena/`.
- Keep both existing T069 `Create` factories and their current-admission checks,
  exact public property sets and existing fingerprint byte formulas unchanged.
- The policy intent retains exactly the four common public properties plus
  `PolicyRef` and `DeteriorationAuthorityFingerprint`. No new serialized/public
  property, public constructor, command, receipt field, raw policy input or migration.
- One typed intent per original declared operation; ordinal is zero-based. Never
  replace a policy operation with a fake direct addition or negative reduction.
- Selected publication supports only authenticated `increase_severity` and
  `add_complication`. Explicit `death_contour`, `heal`, legacy publication and
  scheduled recovery producers remain open, not deleted or represented as healing.
  Original rank-IV increase is inapplicable, even after a preceding reduction.
- T069 remains authoritative against the ORIGINAL accepted wound. Preview of a
  death policy remains an applicability check only, not a terminal/publication grant.
- T067 owns complication IDs, definition/application maps and root operation keys;
  accepted effect materialization owns effect IDs. No T070 identity formula, fake
  creation packet, suffix-on-collision, hidden cache or direct canonical write.
- Preserve the complete tagged working reference: origin, result index, original
  operation index and local spelling. Direct and policy additions with the same
  local spelling are distinct; actual semantic definition-key collisions reject.
- Validate the complete graph after EVERY operation. Final unchanged Mortal
  physical rank also requires original slot budget and retained source continuity.
  Do not prune effects, lower requirements or reset budget to make a result fit.
- Same-rank retained roots keep identities, carrier payloads, index generation and
  causal lineage. Only final rank change closes/rematerializes all original roots.
  New roots are parentless. Full terminal/topology proof precedes outcome finalization.
- New root AND child-only skill selectors require Offered/Current authority; retained
  accepted selectors remain continuations. Policy diagnostics name the canonical
  policy source path, never a fake direct-draft or GM-authored path.
- Every published result uses one existing authenticated graph batch, including
  effectless 0/0 batches, and one coordinated wound/effect/resource/history receipt.
  Validation failure/rollback and exact cold replay must preserve whole-tree safety.
- The required unfinished legacy RED remains visible. No test deletion, skipping,
  weakened validation or inference of approval for the unanswered legacy choice.
- Use the bounded PowerShell runner, smallest meaningful Focused controls, one Fast
  at the checkpoint and one conditional FullValidation for the changed GM example.
  Do not run PreMerge or unrelated Deep/Lifecycle suites. Fast stays five minutes.
- Update Mortal GM guide, worked example, manifest and source/example guards in this
  change. No afterlife mechanic/command/contract is added by this Mortal publisher.

## Scope and task boundary

This is one coherent publisher task, not a standalone budget refactor followed by
a second gameplay task. Its admission, graph, effect proof and documentation must
be reviewed together. It does not close T070, T177, #1536, spiritual healing, natural
recovery, death or legacy work. The parent owns Spec Kit checkpoint changes.

### Task 1: Atomic selected non-death policy graph publication

**Files:**

- Create `BookOfEternityClient/Services/MortalWoundTreatmentPolicyGraphProjection.cs`:
  pure shared typed increase/add projection, no authority or permanent identities.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.Requests.cs`:
  retain the exact private `PrepareComplexGraphApplicability` signature and original
  T069 factory call; delegate only its non-death projection.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentWorkingWoundSimulator.cs`:
  final unchanged-rank budget continuity beside existing root/body continuity.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`
  and `.Course.cs`: private seals, grammar, complete tagged selected compilation,
  derived net-worsening declaration, final scalar/event decoration and handoff.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentSeverityRematerializationPlanner.cs`,
  `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` and
  `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs`: consume final-rank
  compilation, tagged IDs and exact source paths through existing proof/assembler.
- Create `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.DeteriorationPublication.cs`:
  real accepted-state, coordinated publication, rollback and cold-replay coverage.
- Modify `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.DeteriorationPreparation.cs`:
  evolve only the obsolete non-death-unsupported assertion; retain private/public
  evidence tests and pin still-unsupported publication to explicit death.
- Modify `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ComplicationGraphApplicability.cs`:
  before-die/claim policy authority and original/final budget admission negatives.
- Modify `BookOfEternityClient.Tests/MortalWoundTreatmentWorkingGraphProjectionTests.cs`:
  deterministic shared projection/continuity tests, no fixture-backed tests in Fast.
- Modify `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`, `docs/testing.md`,
  `specs/1505-test-suite-performance/research.md`: add the new heavy source to BOTH
  arrays and reviewed table; reviewed count 65 -> 66. No runner/category redesign.
- Modify `OtherGuides/Wound_Materialization_Contract.md`,
  `Examples/E_CLI_Wound_Materialization.txt`, `Examples/example_validation_manifest.json`,
  `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs` and
  `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`:
  truthful supported boundary and parser-executed complete policy procedure example.

Do not edit T069 factories, the policy/route grammar parsers, private preparation
runtime, reducer acceptance rules, lifecycle/death owners, common transaction
protocol, public DTOs or spiritual services. If a concrete failing invariant cannot
be resolved in this scope, report the exact seam before expanding it.

**Interfaces consumed:**

```csharp
// Existing interfaces; preserve their ownership and public shapes.
bool MortalWoundApplyDeteriorationOutcomeIntent.TryGetPreparedDeterioration(
    MortalWoundTreatmentAttemptRequest request,
    MortalWoundApplyDeteriorationOperation operation,
    out MortalWoundTreatmentDeteriorationPreparation? preparation);
// Accepted private preparation exposes detached Policy, Draft, ComplicationBinding,
// WoundSourcePath, OperationOrdinal and Fingerprint, plus AgreesWith(request, op, intent).
MortalWoundTreatmentWorkingGraphSimulation MortalWoundTreatmentWorkingWoundSimulator.SimulateGraph(
    WoundMaterializationEnvelope? startingWound,
    IEnumerable<ImmutableArray<MortalWoundTreatmentOperation>> declaredResults,
    MortalWoundTreatmentPreparedGraphOperationReducer preparedOperationReducer);
```

**Interfaces produced:**

```csharp
internal static class MortalWoundTreatmentPolicyGraphProjection
{
    internal static MortalWoundTreatmentPreparedGraphOperationResult Project(
        MortalWoundTreatmentWorkingGraphProjection before,
        WoundWorkingOperationAddress address,
        MortalWoundDeteriorationResultKind resultKind,
        MortalWoundComplicationProposalDraft? draft);
}
internal sealed record MortalWoundTreatmentSelectedAddition(
    WoundWorkingReference Reference,
    MortalWoundTreatmentComplicationBindingPreparation Binding);
internal sealed record MortalWoundTreatmentSelectedDefinitionOrigin(
    int OperationOrdinal, int DefinitionOrdinal, string DefinitionKey,
    string MappedDefinitionRef, string ComponentsPath);
// Selected compilation replaces HasReduction with FinalSeverityChanged and stores
// a derived AllowsWorsening; Additions is ImmutableArray<...SelectedAddition>.
// These are private implementation interfaces, not serialized game contracts.
```

- [ ] **Step 1: Establish real selected-publication RED and narrow test placement.**

Create the Integration partial in the existing partial test class. Reuse the real
fixture, persisted-command rehydration, effect batch and publication helpers. Start
with this test; `CreatePolicyPreparationScenario` and every helper below already
exist in the same class. Do not fake an intent or bypass accepted-state admission.

```csharp
[Theory]
[InlineData("increase_severity", false)]
[InlineData("add_complication", true)]
public void DeteriorationPublication_RealSelectedPolicyPublishes(string kind, bool effectless)
{
    var scenario = CreatePolicyPreparationScenario(kind, effectless);
    using var fixture = AcceptedStateFixture.Create(scenario);
    var before = fixture.ReadCurrentWound();
    var count = fixture.ReadNpcItemCount("sterile_thread");
    var flow = PersistAndRehydrateTreatmentPublication(fixture,
        ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId),
        "selected non-death policy");
    var prepared = PreparedPolicyAt(flow, 0);
    var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
    var batch = Assert.Single(plan.WoundStageBundle!.PreparedPlan.EffectOperationBatches);
    if (effectless)
    {
        Assert.Empty(batch.RootApplications);
        Assert.Empty(batch.TerminalOperations);
    }
    else
    {
        Assert.Single(batch.RootApplications);
        Assert.Single(batch.TerminalOperations);
    }
    using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        publication.CompleteAtFullPipelineEnd();
    var after = fixture.ReadCurrentWound();
    Assert.Equal(kind == "increase_severity" ? before.Severity.Rank + 1 : before.Severity.Rank,
        after.Severity.Rank);
    if (effectless)
        Assert.Equal(prepared.ComplicationBinding!.ComplicationId,
            Assert.Single(after.Complications).ComplicationId);
    Assert.Equal(count - 1, fixture.ReadNpcItemCount("sterile_thread"));
    var transition = Assert.Single(fixture.ReadCurrentHistory().State!.Transitions,
        row => row.Kind == "treat");
    AssertClosedTreatmentReceipt(transition.TreatmentResult!.Receipt, flow.Request, flow.Resolution);
}
```

Run before runtime edits:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.DeteriorationPublication_RealSelectedPolicyPublishes"
```

Expected semantic failure is current `mortal_wound_treatment_publication_slice_unsupported`
from real common composition, not a missing fixture, compile error or timeout.
Record actual artifact and selected test names. Add the source inventory entry in
both arrays/table/count before the first final boundary control.

- [ ] **Step 2: Pin final budget admission before implementing the shared projection.**

Add a theory to `ComplicationGraphApplicability.cs` using its real offered/claimed
die and resource-count helpers. Give the accepted wound one retained passive slot
and a legal current rank/budget; make positive sibling bands `stabilize` (applicable
at every starting rank) and the failed band the listed operation sequence. Policy
is the existing exact `increase_severity` policy. Call
`PrepareProcedurePublicationScenario` AFTER all canonical edits and use the real
request path before resolution. Keep the existing reflected callback signature.

```csharp
[Theory]
[InlineData(3, 3, "i,r1", true)]
[InlineData(3, 3, "r1,i", false)]
[InlineData(2, 1, "r1,i", true)]
[InlineData(2, 2, "r1,i", false)]
[InlineData(2, 2, "i,r1", true)]
[InlineData(2, 1, "i,r1", false)]
public void ComplicationGraphApplicability_PolicyFinalBudget(
    int rank, int budget, string sequence, bool expected)
{
    var scenario = CreatePolicyPreparationScenario("increase_severity");
    scenario.Before["severity"]!["rank"] = rank;
    scenario.Before["severity"]!["value"] = rank == 2 ? "II" : "III";
    scenario.Before["consequences"]!["slotBudget"] = budget;
    var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
    foreach (var band in route["outcomes"]!.AsArray().OfType<JsonObject>())
        band["result"] = band["category"]!.GetValue<string>() == "failed_attempt"
            ? new JsonArray(sequence.Split(',').Select(token => (JsonNode)(token == "i"
                ? new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" }
                : new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })).ToArray())
            : new JsonArray(new JsonObject { ["kind"] = "stabilize" });
    var legal = route.DeepClone();
    var legalId = scenario.RouteId + "_legal";
    legal["routeId"] = legalId;
    legal["outcomes"]!.AsArray().OfType<JsonObject>()
        .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")
        ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" });
    scenario.Before["treatment"]!["routes"] = new JsonArray(route.DeepClone(), legal);
    scenario.Before["treatment"]!["knownRouteIds"] = new JsonArray(scenario.RouteId, legalId);
    scenario = PrepareProcedurePublicationScenario(scenario with { ExpectedIntentCount = 2 });
    using var fixture = AcceptedStateFixture.Create(scenario);
    var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(fixture.GetAcceptedState());
    var beforeCount = fixture.ReadNpcItemCount("sterile_thread");
    var tree = CaptureResolverFixtureTree(fixture.Root);
    var request = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state,
        fixture.ReadCurrentHistory(), fixture.ReadCurrentWound(), scenario.OperationKey,
        scenario.RouteId, fixture.AcceptedEventRef(state));
    Assert.True(request.IsValid == expected, DescribeIssues(request.Issues));
    if (expected)
    {
        Assert.Equal(new[] { 0, 1 },
            Assert.IsType<MortalWoundProcedureCheckAuthority>(request.Request!.ModeAuthority).SourceIndices);
        AssertSingleHeldClaim(request.Request, "sterile_thread");
    }
    else
    {
        Assert.Contains(request.Issues, row => row.Code == "mortal_wound_treatment_procedure_band_inapplicable");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        Assert.Equal(beforeCount, fixture.ReadNpcItemCount("sterile_thread"));
        var fresh = MortalWoundTreatmentPlanner.PrepareProcedureRequest(state,
            fixture.ReadCurrentHistory(), fixture.ReadCurrentWound(), scenario.OperationKey + "_legal",
            legalId, fixture.AcceptedEventRef(state));
        Assert.True(fresh.IsValid, DescribeIssues(fresh.Issues));
        Assert.Equal(new[] { 0, 1 },
            Assert.IsType<MortalWoundProcedureCheckAuthority>(fresh.Request!.ModeAuthority).SourceIndices);
        AssertSingleHeldClaim(fresh.Request, "sterile_thread");
    }
}
```

Place the six-row theory under prefix
`ComplicationGraphApplicability_PolicyFinalBudget`. Run its exact filter with
Integration Focused/10 minutes before adding the final continuity gate. Existing
runtime should incorrectly admit the three same-rank/different-budget vectors;
an earlier canonical/route fixture rejection is not the intended RED. At least
one runtime-admitted invalid-budget row must fail for the specified reason.

Implement the complete pure helper:

```csharp
namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentPolicyGraphProjection
{
    internal static MortalWoundTreatmentPreparedGraphOperationResult Project(
        MortalWoundTreatmentWorkingGraphProjection before,
        WoundWorkingOperationAddress address,
        MortalWoundDeteriorationResultKind resultKind,
        MortalWoundComplicationProposalDraft? draft)
    {
        if (resultKind == MortalWoundDeteriorationResultKind.AddComplication)
        {
            if (draft is null) return new(false, false, null);
            var applicable = before.TryAppendComplication(draft,
                WoundWorkingReferenceOrigin.PolicyAddition, address, out var appended);
            return new(applicable, false, appended);
        }
        if (resultKind != MortalWoundDeteriorationResultKind.IncreaseSeverity || draft is not null)
            return new(false, false, null);
        var rank = checked(before.Scalars.Severity.Rank + 1);
        var value = rank switch { 2 => "II", 3 => "III", 4 => "IV", _ => string.Empty };
        if (value.Length == 0) return new(false, false, null);
        var after = before.WithScalars(before.Scalars with
        {
            Severity = before.Scalars.Severity with { Rank = rank, Value = value }
        });
        return after.ValidateGraph("mortalWoundTreatment.workingGraph").IsEmpty
            ? new(true, false, after) : new(false, false, null);
    }
}
```

In the request callback, leave direct addition and the original accepted-state
T069 acquisition intact. Replace its duplicated non-death projection with:

```csharp
if (policy.ResultKind == MortalWoundDeteriorationResultKind.DeathContour)
    return new(true, false, before); // applicability only, no terminal authority
var draft = policy.ResultKind == MortalWoundDeteriorationResultKind.AddComplication
    ? MortalWoundTreatmentContract.BuildValidatedComplicationDraft(
        policy.Result.GetProperty("complicationDraft")) : null;
return MortalWoundTreatmentPolicyGraphProjection.Project(before, address, policy.ResultKind, draft);
```

Inside the existing Mortal physical SAME-FINAL-RANK block of `SimulateGraph`,
before comparing roots/definition bodies, add:

```csharp
if (final.Scalars.SlotBudget != starting.Scalars.SlotBudget)
    return NotApplicable();
```

Keep all per-operation graph, retained-route and canonical policy validations.
Increase preserves slot budget, progress and care; reduction keeps its existing
result-rank budget rule. Creation maximum stays immutable and is NOT a cap on
future current rank. Do not normalize either operation to make net-zero fit.

Add deterministic tests in the existing pure graph class for increase preserving
budget/care/recovery, IV rejection and mismatched typed kind/draft/death rejection.
They use its actual parsed canonical helpers, not a file-backed fixture. Execute
the new precise filters RED then GREEN as each behavior is introduced; where the
extracted old behavior already passes, report preservation, not a new RED.

- [ ] **Step 3: Admit and seal the complete private policy packet, not public hashes.**

Before editing admission, add real-resolution forgery tests in the new publication
partial under `DeteriorationPublication_PrivateAuthority`: replace a selected
prepared policy intent with the old five-argument `Create` carrying identical six
public fields; borrow a valid packet from another request; change canonical policy
body without changing policyRef; corrupt private null/default/map/seal fields.
Use independent clones for each axis. Initial positive publication remains RED
until Steps 4-6, but negative tests must assert the precise rejecting boundary,
no escaping exception and no canonical/resource/output mutation.

Change `IntentSeal.From/AgreesWith` to receive
`MortalWoundTreatmentAttemptRequest`, not only `requestFingerprint`. Keep all current
scalar and direct-add seals; add a private policy seal that stores the checked
detached packet and validates BOTH stored and incoming private preparations:

```csharp
private sealed record PolicySeal(MortalWoundTreatmentDeteriorationPreparation Prepared)
{
    internal bool AgreesWith(MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentOperation operation, MortalWoundTreatmentOutcomeIntent intent)
    {
        if (operation is not MortalWoundApplyDeteriorationOperation policy ||
            intent is not MortalWoundApplyDeteriorationOutcomeIntent selected ||
            !selected.TryGetPreparedDeterioration(request, policy, out var incoming) ||
            incoming is null || !Prepared.AgreesWith(request, policy, selected)) return false;
        return incoming.Fingerprint == Prepared.Fingerprint;
    }
}
```

`PolicySeal` is captured only after `TryGetPreparedDeterioration` succeeds. A null
policy seal must reject a policy intent/operation, just as missing AdditionSeal
rejects direct addition. The stored packet's agreement is a semantic check;
digest equality without BOTH validated payloads is insufficient.

Extend `OrderedIntentsAgree` to take the actual request and original declared list.
Preserve existing field/type/ordinal and direct-map comparisons. For each policy
pair, require the original typed operation and each intent's successful private
accessor before comparing checked fingerprints. Update its two runtime callers:
preparation `AgreesWith` and fresh `ValidateCommon` recomposition.

In `DetachSelectedResolution`, iterate by original ordinal. A policy intent must
pass its private accessor against the original request/operation BEFORE invoking
trusted `DetachedCopy`; malformed private members must not escape as copy errors.
The copy preserves all incoming public fields; do not reconstruct clean public
fields from private data. Keep current direct-draft/map detachment unchanged.

Change `HasSupportedGrammar` to receive the resolution and intent list so the
private operation/request context is available at BOTH existing call sites. Its
harmful branch accepts direct add or privately validated non-death policy only:

```csharp
var harmful = intents.Any(intent => intent is MortalWoundAddComplicationOutcomeIntent
    or MortalWoundApplyDeteriorationOutcomeIntent);
// Preserve the current harmful mode/category gate exactly:
var permittedContext =
    (resolution.Mode == "procedure" && resolution.ResultCategory is "partial_success" or "failed_attempt") ||
    (resolution.Mode == "course" && resolution.Interruption && resolution.CourseDisposition == "interrupted");
// For each policy intent, retrieve its private preparation using the matching
// declared operation and require ResultKind IncreaseSeverity or AddComplication.
// Remaining scalar intents pass existing HasPositiveScalarGrammar(..., allowEmpty:true).
```

Do not widen guaranteed/success harmful results, interruption timing, course dose
rules, aggregate reduction <=2, max operations8, explicit death or terminal healing.
`ValidateCommon` still obtains the current accepted state and freshly recomposes
T067 through unchanged registry-current T069 before granting publication.

- [ ] **Step 4: Compile direct and policy operations into one tagged final graph.**

Add selected-graph tests under `DeteriorationPublication_TaggedGraph` before
changing compilation. Cover effectful/effectless policy additions, an effectless
policy repeated at ordinals0/1, and a direct/policy addition with the SAME local
complication spelling but distinct semantic definition keys. Assert complication
IDs against `PreparedPolicyAt(flow, ordinal).ComplicationBinding`, not a T070
formula. For direct intents use their existing T067 IDs/maps. Include the
negative repeated effectful policy whose actual semantic definition keys collide;
it must reject at pre-roll admission, not silently rename the authored definition.

Replace the raw addition list with `MortalWoundTreatmentSelectedAddition` records.
Use the complete complication reference as its dictionary key. During the original
declared-operation loop, keep current ordinal/type/declared-fingerprint checks.
Direct additions call the unchanged binding owner. Policy operations retrieve the
accepted private preparation and use its typed draft/binding without reparsing
new raw input or manufacturing direct operations. Capture policies by original
ordinal for the simulation callback, including increase with no draft/binding.

```csharp
var reference = new WoundWorkingReference(origin, new(0, i), draft.Complication.ComplicationRef);
// origin is DirectAddition for a direct operation and PolicyAddition for a policy.
additions.Add(new(reference, binding));
byReference.Add(reference, binding);
for (var j = 0; j < draft.ConsequenceDefinitions.Length; j++)
{
    var definition = draft.ConsequenceDefinitions[j];
    origins.Add(new(i, j, definition.Definition.GetProperty("definitionKey").GetString()!,
        binding.DefinitionReferenceBindings[j].NamespacedRef,
        componentsPrefix + $".consequenceDefinitions[{j}].definition.components"));
}
```

The client-derived `componentsPrefix` is exactly:

```csharp
// Direct operation i:
$"treatmentPublication.outcome.declaredResult[{i}].complicationDraft"
// Policy operation i, using its checked private packet:
prepared.WoundSourcePath + ".recovery.deteriorationPolicy.result.complicationDraft"
```

Compilation is valid with zero additions if it contains an authenticated selected
increase. Preserve exact/confusable collision checks across permanent complication
IDs and root operation keys; complete graph validation also owns actual semantic
definition-key collision checks. Remove the unused selected compilation property
whose `Additions.Length != 0` incorrectly claims to govern effect-batch need.
The actual `RequiresEffectBatch(preparation)` already uses selected recomposition.

Run ONE `SimulateGraph` over the ORIGINAL before and original selected list. Its
callback has two legitimate branches: existing direct append with DirectAddition,
and exact ordinal prepared policy projected through the shared helper. No callback
death, fake additions, reordered list, intermediate permanent ID or finalizer.

For final complication identity, use full reference equality:

```csharp
string ComplicationId(WoundWorkingReference reference) =>
    reference.Origin == WoundWorkingReferenceOrigin.Existing ? reference.Value :
    byReference.TryGetValue(reference, out var binding) ? binding.ComplicationId :
    throw new InvalidOperationException("Foreign complication coordinate.");
```

A new root reference has its local DEFINITION ref as Value, not local complication
ref. Select its addition by the same origin and full address, then select its
single root binding by `LocalDefinitionRef == root.Reference.Value`. Reject missing
or ambiguous matches; never confuse the two local namespaces. Existing roots keep
their original effect IDs. Root ownership comes from the exact final graph's
complication reference through `ComplicationId`.

Derive compilation's rank decisions only AFTER the complete simulation:

```csharp
var finalSeverityChanged = working.Scalars.Severity.Rank != before.Severity.Rank;
var allowsWorsening = working.Scalars.Severity.Rank > before.Severity.Rank &&
    policies.Values.Any(prepared => prepared.Policy.ResultKind ==
        MortalWoundDeteriorationResultKind.IncreaseSeverity);
var terminals = (finalSeverityChanged
    ? before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId)
    : removals.SelectMany(id => before.Complications.Single(row => row.ComplicationId == id).OwnedEffectIds))
    .ToImmutableArray();
```

Seal BOTH derived booleans, full tagged references, source diagnostic paths and
complete ordered binding data in the existing private compilation fingerprint.
Keep the original request/before/result/transition/scalars/graph/roots/removal and
terminal fields. Add each checked policy preparation fingerprint in original
ordinal order, including increase-only packets. Do not serialize internal-only
binding properties by default JSON and accidentally seal `{}`: explicitly append
the existing ID, preparation fingerprint, definition/application map rows and roots.

Preparation's constructor and `Prepare` must select graph compilation for direct
addition OR non-death policy, not only direct addition. The selected branch keeps
before as provisional carrier image, no temporary severity-reduction projection.
Use the same operation predicate in both locations; closed grammar has already
checked exact private policy authority. Detached recomposition must derive the
same selected graph fingerprint from the same original resolution.

- [ ] **Step 5: Publish one final rank, graph and effect generation.**

Add actual publication tests under `DeteriorationPublication_FinalRank` before
changing consumers. Use a single passive retained root so legal rank-I results
are meaningful. For starting I/II/III, override positive sibling bands to an
applicable stabilization so a stale reduction sibling does not reject the fixture.
Assert original I/II/III -> II/III/IV; original IV increase remains request-invalid,
including a selected list that starts with reduction. Keep maximumAtCreation
immutable (III -> IV with maximum III is legal).

For each legal Step2 net-zero vector, assert identical final severity event, slot
budget, retained root ID/carrier payload/index generation and causal lineage.
For III/3 `increase,reduce2`, assert final II/2 and exactly one final replacement
generation, not one generation per intermediate operation. Add remove+policy
addition coverage, including a retained reaction root and child-only descendant.

Replace selected `.HasReduction` uses with `.FinalSeverityChanged` in:

- `MortalWoundTreatmentOutcomePublicationPlanner.cs`: selected handoff root set and
  retained-source check (baseline901/906), plus compilation constructor/property.
- `MortalWoundTreatmentSeverityRematerializationPlanner.cs`: selected applications
  and retained-lineage branch (baseline311/413).
- `EffectAcceptedTurnPlanner.cs`: full rematerialization, retained roots, original
  applications and root result cardinality (baseline3031/3059/3659/3749).
- `WoundAcceptedTurnPlanner.cs`: final root ID choice (baseline1395).

Do not rename/change unrelated scalar-only reduction variables or projection APIs.
All original roots/descendants close only on final rank change. Otherwise close
only explicitly removed original complication lineage; retained causal topology
and generation remain exact, including a terminal/carrierless original parent.
All new roots remain parentless. Existing effect batch binding, aggregate bounds,
application map cardinality and full thirteen-property rematerializer proof remain
in force BEFORE `Outcome.Finalize` at `ContinuationFinalPlanAgrees`.

Final scalar decoration uses original/final rank, not reduction-list membership:

```csharp
var originalSeverity = resolution.RequestAuthority.RouteSourceWound.Severity;
var severity = working.Scalars.Severity with
{
    LastChangeEventRef = working.Scalars.Severity.Rank == originalSeverity.Rank
        ? originalSeverity.LastChangeEventRef : resolution.Coordinates.EventRef
};
```

Preserve existing attempt/course metadata and explicit stabilization-anchor updates.
Selected adverse policy itself does not reset care/progress/anchors; formal
retrauma through `worsen` is a different ingress. Keep recovery policy immutable.

In `BuildFinalTreatmentWound`, replace the ambiguous direct-only local-ref lookup:

```csharp
compilation.Additions.Single(add => add.Reference == row.Reference).Binding.ComplicationId
```

Use this only for non-existing complication references. All final root IDs still
come from accepted effect results or exact retained originals, and only the
existing `BuildFinalWoundCore` writes the complete detached after-image once.

Change private `CreateDeclaredOutcome` to accept the authenticated compilation
or derive its value at the already checked selected branch. `AllowsWorsening`
is true ONLY when that compilation's sealed derived value is true; scalar-only
publication remains false. Do not expose a caller-authored permission flag.
The existing reducer remains unchanged and verifies final rank, allowed scope,
root replacement, budget, active attempt and exact recovery policy as before.

Run Step1/Step4/Step5 named controls; expected all semantic publication positives
GREEN. Once support is actually present, update the P1 boundary test fixture to
`death_contour` for its unsupported-publication assertion while retaining its
six-member public shape and independent original-fingerprint oracle. Do not skip
it or retain an obsolete expectation that non-death is unsupported.

- [ ] **Step 6: Prove selectors and sealing through real composition and rollback.**

`SelectedDefinitionPath` returns the compilation's sealed exact `ComponentsPath`:

```csharp
private static string SelectedDefinitionPath(MortalWoundTreatmentSelectedDefinitionOrigin origin) =>
    origin.ComponentsPath;
```

Before this change, add new root AND child-only exact-skill failure tests under
`DeteriorationPublication_SkillAuthority`. Use real root-bound reaction/child
fixtures from `WoundContractTestData.CreateRootBoundReactionComplicationDefinitions`.
All child definitions must be reachable and have correct root slots; avoid
borrowing invalid historical movement payloads for the stricter policy parser.
Fail missing Offered/current skill after a valid accepted request and assert the
exact canonical policy path ending at the actual failing component/selector.
Then prove both authorities present permit publication and retained accepted
selectors survive current skill loss with original generation. Ordinary foreign
new definitions cannot borrow retained-source continuation authority.

Extend `DeteriorationPublication_PrivateAuthority` to cover post-preparation graph
tampering: full tagged origin/address, mapped ID, components path, derived rank
booleans, final root topology, root/child definition order and private packet.
Change one field on an independent cloned object per axis, keep copied seal stale,
and assert rejection before finalization and whole-tree equality. Use the real
existing final proof entry point; do not add a weak second proof service or broadly
catch programmer exceptions to turn them into successful validation.

Run the narrow selector tests before/after source path wiring; the initial RED
must show the incorrect direct-draft path or unsupported authority boundary, not
invalid canonical fixture construction. Report already-passing inherited guards
as preservation. Then run the new private-authority group plus original P1
`PolicyPreparation_` coverage on restored runtime. Every new guard gets tests
written before its implementation; never claim compile/fixture failures as RED.

- [ ] **Step 7: Verify real modes, atomic recovery and replay.**

Add tests under `DeteriorationPublication_Lifecycle` with the existing real fixture
and coordinated helpers, not a new stand-in transaction implementation:

- partial procedure `[add_recovery, apply_deterioration]`: exact progress increase,
  selected policy addition/increase, no route completion, one treatment receipt;
- interrupted course: publish its earlier supported milestone first, advance the
  actual accepted minute/turn, select interruption policy at its real ordinal,
  clear only the course pointer, do not consume current/future dose;
- procedure natural1/natural20 with Fate Shield: preserve existing critical policy
  precedence, attempt/result fingerprint and resource/critical-reaction settlement;
- policy add with no definitions/roots: real sealed 0/0 batch, empty application
  result map, one T067 complication, unchanged retained root generation;
- policy increase and effectful policy add: successful persistence/restart replay
  has one terminal receipt, no second resource payment or effect generation;
- injected failure after an earlier canonical resource write: byte-exact complete
  tree restoration, retained exact command/hold, retry same plan once, then cold
  replay with `ExactReplay` and no writes.

Use the existing fault hooks and actual receipt probe. The core rollback oracle is:

```csharp
var tree = CaptureResolverFixtureTree(fixture.Root);
fault.Arm(WoundHistoryState.HistoryPath, fixture.TargetCarrierPath,
    ReadCanonicalBytes(fixture, fixture.TargetCarrierPath), fixture.FileSystem);
var error = Assert.Throws<CanonicalStateWriteException>(() =>
    PublishCachedResourcePlanOpen(fixture, flow, plan));
Assert.Equal(WoundHistoryState.HistoryPath, error.RelativePath);
Assert.True(fault.Fired);
Assert.True(fault.ObservedEarlierResourceWrite);
AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
    publication.CompleteAtFullPipelineEnd();
fixture.RestartForReplay();
var replayTree = CaptureResolverFixtureTree(fixture.Root);
var replay = ProbePublishedTreatment(fixture, flow.Request);
Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
AssertResolverFixtureTreeUnchanged(fixture.Root, replayTree);
```

`fault` is the existing `ResourcePublicationFailureInjection`, installed through
`FileSystemManagerHooks.BeforeCanonicalMutationAsync` before fixture construction.
`flow` is persisted/rehydrated real resolution and `plan` the actual coordinated
plan, as in Step1. Add exact complication/root/history/resource assertions after
retry, not just a non-null result. Ordinary successful-turn finite effect ticks
remain ordinary lifecycle behavior; do not suppress them to force equality. Only
rejected/rolled-back transactions require byte-exact whole-tree restoration.

Run exact new group filters with Integration Focused/10 minutes after each useful
change. Split large measured groups by test ownership instead of repeatedly timing
out; an evidence-backed Focused15 override is permitted by existing policy. Do not
run the entire growing resolver class or full solution as a development loop.

- [ ] **Step 8: Synchronize the real GM capability and executable example.**

Update both the early unfinished-producer paragraph and selected-graph paragraph
in the Mortal guide/example. Keep the direct-add guarantees and add these exact
shared statements to their source guard and scene-authority manifest required text:

```text
Selected non-death apply_deterioration outcomes publish through the same authenticated treatment graph batch.
The client reobtains the original accepted wound policy; the GM supplies policyRef, never private preparation or permanent identities.
Only a change between original and final severity rematerializes retained roots; an unchanged final rank also preserves the original slot budget.
Policy root and child-only selector diagnostics name recovery.deteriorationPolicy.result.complicationDraft at the canonical wound source.
Death, heal and legacy publication remain unfinished.
```

Replace only the now-obsolete `Policy, heal and legacy publication remain unfinished.`
wording. Preserve the distinction between natural recovery scheduling and selected
treatment policy publication. Explain procedure partial/failed and actual interrupted
course contexts, original-IV rejection, untouched care/progress unless explicitly
declared, and lack of implicit death. A net-zero rank sequence with a different
budget is rejected BEFORE die/resource claims; no hidden automatic budget repair.

Add the following named section `mortal_wound_treatment_selected_policy_v1` to
both the guide and example. It is a complete authored policy and complete procedure
route to install into an untreated rank-III Mortal physical wound, not a complete
canonical wound or public receipt. The original retained graph must fit rank II.
The failed treatment produces rank IV through its existing canonical policy.

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

Add a production parser test in the existing main example partial, whose
`ParseNamedJsonFences` helper is already available:

```csharp
[Fact]
public void MortalWoundSelectedPolicyWorkedExample_ParsesExactPolicyAndProcedure()
{
    var example = Assert.Single(ParseNamedJsonFences(
        "E_CLI_Wound_Materialization.txt", "mortal_wound_treatment_selected_policy_v1"));
    var policy = MortalWoundDeteriorationPolicyContract.Parse(
        JsonSerializer.SerializeToElement(example["recovery"]!["deteriorationPolicy"]),
        "example.recovery.deteriorationPolicy", "mortal_world", "player", 3);
    Assert.True(policy.IsValid, string.Join(" | ", policy.Issues.Select(issue => issue.Code)));
    Assert.Equal(MortalWoundDeteriorationResultKind.IncreaseSeverity, policy.Policy!.ResultKind);
    var rawRoute = Assert.Single(example["treatment"]!["routes"]!.AsArray());
    var route = MortalWoundTreatmentContract.ParseRouteShape(
        JsonSerializer.SerializeToElement(rawRoute), "example.treatment.routes[0]");
    Assert.True(route.IsValid, string.Join(" | ", route.Issues.Select(issue => issue.Code)));
    var procedure = Assert.IsType<MortalWoundProcedureRouteDefinition>(route.Route);
    var failed = Assert.Single(procedure.Bands, row => row.Category == "failed_attempt");
    var operation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(Assert.Single(failed.DeclaredResult));
    Assert.Equal(policy.Policy.PolicyRef, operation.PolicyRef);
}
```

Add manifest entry `wound_mortal_selected_policy_v1`, file
`E_CLI_Wound_Materialization.txt`, realm `Mortal World`, statePath
`Mortal wound recovery.deteriorationPolicy and treatment.routes[]`, responseSurface
`canonical policyRef selected by the accepted treatment command`, validationKind
`production-validator`. Its validationRoute names the exact example test above
and both production parsers. Its coverageLimit explicitly says policy/route parsing
and exact ref linkage only; real selected atomic publication, rollback, resources
and replay are proved by Integration `DeteriorationPublication_` tests. RequiredText
contains the section name, policyRef, routeId, the five exact shared statements
above and `client-owned`. Include the entry in the main manifest family's expected
dictionary, and update the existing scene-authority limit to state non-death
supported / death-heal-legacy unfinished. Do not claim shape tests publish state.

Write the new source/example guard before the corresponding prose/example change.
Run its focused filter RED then GREEN; use `-FocusedProject Integration` for the
example test. Check `Rules/Block_2.txt`, `Rules/Block_12.txt`, current main CLI prompt
entrypoints and the afterlife matrix for references to the old capability wording.
They need no edit if they delegate to this guide and do not assert the obsolete
boundary. Record that result rather than claiming they were updated. No new
afterlife response field, pending file or behavior is introduced by this task.

- [ ] **Step 9: Perform bounded coherent preservation and one broad checkpoint.**

Run all new publication groups through the smallest named Integration selections;
capture counts, artifacts, duration, no timeout, no duplicates and cleanup. A
single combined new-class prefix is acceptable only if its measured constituent
groups fit the requested bound. Do not rerun every group merely to inflate counts.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.DeteriorationPublication_"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.PolicyPreparation_|FullyQualifiedName~MortalWoundTreatmentResolverTests.ComplicationGraphApplicability_"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentWorkingGraphProjectionTests|FullyQualifiedName~FastTestBoundaryTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatment"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
```

Actual shared effect/rematerializer changes additionally require the existing
direct-add whole partial, removal publication, severity handoff and scalar course
owners. Select by their existing method prefixes, one owner per Focused/10 minute
run; inspect measured previous artifacts first and use the allowed15 bound for an
owner whose actual duration needs it. Do not combine the whole resolver or run
unrelated lifecycle suites. Preserve the accepted Task2 same-rank reaction/copy/
topology/terminal controls. New runtime means prior Task2 evidence is a baseline,
not sufficient final coverage of changed consumers.

At the meaningful final checkpoint, run exactly once each:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

Fast may still stop on the unchanged required legacy authority/registration RED.
Report actual completed TRX union, the sole expected failure and uncompleted
discovery count; never call it all green. A new unrelated failure is diagnosed,
not labeled pre-existing without evidence. FullValidation is conditional here
because the GM worked example and example manifest really change; inspect all
actual TRXs and the new exact example row, not just the summary. Do not repeat
Fast just before FullValidation or PreMerge. FastBoundary's runner probe folders
are not additional real Fast controls.

Every RED is described honestly: semantic assertion, fixture failure, compile-only
failure or timeout. Existing passing negative guards are preservation, not new
test-first proof. Run tests before each new behavior's implementation; an initially
unsupported pipeline can mask later negative assertions, so prove them again after
the corresponding positive path becomes valid. Do not claim guards were observed
to fail individually if the earlier stage rejected first.

- [ ] **Step 10: Self-review, scoped commits and independent task gate.**

Use small coherent commits after verified implementation increments. Before every
commit inspect only the task allowlist, actual diff and staged diffcheck; no
`git add .`, no `.serena`, no parent-owned plan/tasks in an implementer commit.

```powershell
git diff --check
git diff --stat
git status --short
git diff --cached --check
git commit -m "feat(wounds): publish selected non-death policies atomically (#1536) [skip ci]"
```

Stage explicit task paths from the Files list after inspecting them. The final
report gives recorded BASE and actual HEAD, all paths, implemented boundaries,
actual RED/GREEN evidence per behavior, Fast's incomplete legacy caveat, complete
FullValidation evidence, direct/afterlife preservation and GM documentation update
or no-update rationale. Include any fixture-only corrections and any unfulfilled
plan requirement; never silently turn a test-first failure into success history.

Parent generates the exact BASE..HEAD diff package for independent Spec Compliance
AND Code Quality review, inspects the source diff and actual artifacts, and owns
final task acceptance/checkpoints. No remote push, PR, merge, issue closure or
whole-feature completion is part of this bounded task.

## Parent self-review

### Source-backed fixture correction during implementation

The real inherited procedure fixture owns the accepted advantage packet [0,1],
not the singleton [0] originally shown in Step2. Artifact `20260907-041522`
exposed three fixture-only equality failures on legal rows alongside the three
intended semantic invalid-budget admission failures. Parent inspected all seven
actual TRX rows and the failing assertions. Step2 now preserves the exact [0,1]
packet before/after invalid admission; no game roll or resource mechanic changes.
The private bare-public-DTO negative already passes baseline and is preservation.

The approved selected-treatment policy types remain intact. This plan implements
the non-death producer without pretending scheduled recovery/death/healing exist.
It retains original-wound T069 authority and uses accepted private T067 data, not
public hashes alone. The single pure projection is shared by preview/publication;
the reducer's same-final-rank budget invariant is checked before claims. Final
rank, full tagged coordinates, new-vs-retained selector authority and original
causal generations reach all existing consumers and the final proof.

The existing original five-argument intent factory is intentionally still usable
as an unauthenticated DTO and must fail publication. Private-copy corruption is
checked before trusted copy. Zero-addition increase and effectless 0/0 batch are
explicit positive cases. Exact diagnostics come from the canonical source, not
the direct-draft path. GM example/manifest tests describe their limited coverage
truthfully. Death remains a typed future lifecycle obligation; no active-wound
heal/archive shortcut, invented recovery TickKey or unapproved legacy choice.

Requirements coverage: original/current authority and private packet (Step3),
ordered applicability/budget (Step2), exact identity/mapping and policy graph
(Step4), final rank/atomic effects (Step5), selectors/tamper (Step6), course/critical/
rollback/replay (Step7), GM contract/example (Step8), bounded evidence and review
(Steps9-10). Whole-feature omissions remain explicitly tracked, not waived.
