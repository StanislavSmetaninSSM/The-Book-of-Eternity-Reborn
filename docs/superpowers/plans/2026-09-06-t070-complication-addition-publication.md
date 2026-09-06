# T070 Ordered Complication Addition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Execute one independently reviewed task and one bounded C# lane at a time; checklist steps are tracked below.

**Goal:** Validate cumulative treatment complication graphs before selection and
publish selected direct `add_complication` operations through the existing atomic
wound/effect treatment path, preserving the complete approved operation union.

**Architecture:** One detached, symbolic working graph supplies both applicability
and selected compilation. Only T067 binds complication identities and local reference
maps; only the common effect planner allocates runtime effects. Existing canonical
simulator callers retain their current facade and behavior. Selected addition,
selective removal and severity reduction compose into one final graph and one batch.

**Tech Stack:** C#/.NET 8, immutable collections, System.Text.Json, xUnit, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T070, FR-027/FR-064,
`contracts/mortal-wound-treatment.md` and `contracts/wound-effects-and-atomicity.md`
under that feature.
Source/design evidence: Git metadata `sdd/t070-addition-projection-design-audit.md`,
including its pinned follow-up section9, and `t070-adverse-outcome-source-audit.md`.
The design report's source BASE is `51c97c24`. Source pre-flight is rebased through
accepted selective removal `58f29a5c` and test-only fixture checkpoint `c7301340`.
Concrete interface/test-owner evidence is `sdd/t070-addition-graph-interface-preflight.md`;
its parent-corrected zero-based T067 ordinal is authoritative.

**Execution gate:** Neither task starts before selective removal and both T177
fixture-alignment tasks are parent-accepted. The known unfinished legacy RED is not
a prerequisite to symbolic graph work and must remain explicit.
Task2 starts only after Task1's actual diff, evidence and independent review are
accepted. Record an exact committed BASE separately for each task. This plan does not
mark T070, T177 or #1536 complete and does not authorize remote mutation.

## Global Constraints

- Remain in `E:/Games/worktrees/boe-1536-wound-materialization` on
  `1536-complete-wound-materialization`; do not access or stage unrelated `.serena`.
- Preserve existing positive treatment, course interruption, failed-procedure T067
  policy authority, critical reactions, replay, resource and rollback behavior.
  No new public command, GM-authored runtime identity, migration or alternate publisher.
- `heal` and `apply_deterioration` keep their approved authoring/resolution contracts
  and explicit unfinished publication boundaries. Do not restrict legal sibling
  procedure bands merely because their selected publisher is unfinished.
- Preserve eight batch properties, thirteen rematerialization authority properties,
  the six-argument treatment composition entry, and the original rematerializer
  `Prepare`/`Agrees` names/signatures without new overloads (name-only reflection).
- Do not reparse raw route JSON, fabricate accepted-state authority, allocate IDs
  before selection, suffix colliding keys or accept a digest alone as authority.
- Existing full lineage, identity, carrier, source, resource, detached-selection and
  independent agreement gates remain authoritative. Public flags cannot substitute
  for graph provenance. `CanonicalStateNormalizer` remains the sole publisher.
- Use one implementation/C# owner. Pure deterministic graph/contract/source guards
  stay Fast; actual filesystem/lease/accepted-pipeline/restart tests stay Integration.
  Do not change runner defaults or hide the required unfinished legacy RED.
- Follow the constitution and GM synchronization guardrails. Each task records its
  Mortal/afterlife documentation decision; changed shared canonical validation needs
  afterlife coverage and the conditional FullValidation lane.

### Task 1: One complete symbolic graph and cumulative pre-roll applicability

**Files and ownership:**

- Create `BookOfEternityClient/Services/MortalWoundTreatmentWorkingGraphProjection.cs`
  for immutable tagged coordinates, ordered graph operations and validation
  adaptation. Do not create a second definition AST or separate scalar simulator.
- Refactor `BookOfEternityClient/Services/MortalWoundTreatmentWorkingWoundSimulator.cs` into the existing canonical
  facade plus a distinctly named internal `SimulateGraph` sharing the same ordered
  loop/operations. Keep existing result/delegate/signatures unchanged.
- Narrowly extract the existing reference-based destination validation/scalar delta
  from `BookOfEternityClient/Services/MortalWoundTreatmentSeverityReductionPlanner.cs` for that shared
  operation. Preserve the canonical `Project` entry/projection fingerprint. Symbolic
  roots cannot be sent to it as fake prior effect IDs; the canonical and symbolic
  paths must call the same underlying destination rules.
- Extract typed complication marker/source-link/root-slot conversion from
  `BookOfEternityClient/Services/WoundResponseInputComposer.Parsing.cs` into
  `BookOfEternityClient/Services/WoundResponseInputComposer.TreatmentComplications.cs`;
  creation parsing and treatment projection must call the one conversion core.
  Keep creation-owned allocation in its current ingress.
- Narrowly extract reference-based graph validation from
  `BookOfEternityClient/Services/WoundMaterializationContract.cs` and
  `BookOfEternityClient/Services/WoundPersistedConsequenceEnvelopeAdapter.cs`. Canonical input
  still supplies actual effect IDs; symbolic input supplies detached references.
  Do not replace the parser or copy its graph/ownership/lifecycle rules.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentPlanner.Requests.cs` only at procedure applicability
  and the shared typed policy-authority adapter. Extract an internal typed policy
  complication-draft wrapper from the existing `BookOfEternityClient/Services/MortalWoundTreatmentModel.cs` builder
  using its existing typed conversion; no new public or persisted policy field.
- Add pure `BookOfEternityClient.Tests/MortalWoundTreatmentWorkingGraphProjectionTests.cs`
  and `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ComplicationGraphApplicability.cs` for
  real pre-roll rejection/claim ownership. Register that heavy source in both
  `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` inventories; synchronize
  `docs/testing.md` and `specs/1505-test-suite-performance/research.md` counts.
- Update `OtherGuides/Wound_Materialization_Contract.md`, the complete worked route
  in `Examples/E_CLI_Wound_Materialization.txt`, `Examples/example_validation_manifest.json`
  and `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs` for
  cumulative admission. Existing parser/resolver/policy examples remain legal.
  Inspect `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`
  and `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` for shared
  validation impact. No new spiritual mechanic or GM input is introduced.

**Interfaces (Task1 produces these for Task2):**

Place the shared reference records beside the existing owned-graph contract; keep
the treatment state/loop in its own planned file. All JSON/collections are detached
before storage and cannot expose mutable caller aliases. This is a reference view
over the existing definition JSON, not a second effect component language:

```csharp
internal enum WoundWorkingReferenceOrigin { Existing, DirectAddition, PolicyAddition }
internal readonly record struct WoundWorkingOperationAddress(int ResultIndex, int OperationIndex);
internal readonly record struct WoundWorkingReference(
    WoundWorkingReferenceOrigin Origin, WoundWorkingOperationAddress Address, string Value);
internal sealed record WoundWorkingComplication(
    WoundWorkingReference Reference, string Kind, string State, string DisplayName,
    int TreatmentDifficultyModifier, ImmutableArray<WoundWorkingReference> OwnedRoots,
    string Visibility);
internal sealed record WoundWorkingDefinition(WoundWorkingReference Reference, JsonElement Definition);
internal sealed record WoundWorkingRoot(WoundWorkingReference Reference, string DefinitionKey);
internal sealed record WoundWorkingEntry(
    int Slot, string ProfileKey, WoundWorkingReference Root, string ReadableSummary);
internal sealed record WoundWorkingOwnedGraph(
    ImmutableArray<WoundWorkingComplication> Complications,
    ImmutableArray<WoundWorkingDefinition> Definitions,
    ImmutableArray<WoundWorkingRoot> Roots,
    ImmutableArray<WoundWorkingEntry> Entries);
internal sealed record WoundWorkingGraphContext(
    string WoundId, string Lifecycle, WoundOwnerCoordinate Owner,
    WoundClassification Classification, WoundSeverity Severity,
    WoundCare Care, WoundRecovery Recovery, int SlotBudget, int SlotsUsed);
internal sealed record MortalWoundTreatmentWorkingScalars(
    WoundSeverity Severity, WoundCare Care, WoundRecovery Recovery, int SlotBudget);
internal sealed record MortalWoundTreatmentWorkingGraphSimulation(
    bool IsApplicable, bool Improved, MortalWoundTreatmentWorkingGraphProjection? WorkingGraph);
internal sealed record MortalWoundTreatmentPreparedGraphOperationResult(
    bool IsApplicable, bool Improved, MortalWoundTreatmentWorkingGraphProjection? After);
internal delegate MortalWoundTreatmentPreparedGraphOperationResult
    MortalWoundTreatmentPreparedGraphOperationReducer(
        MortalWoundTreatmentWorkingGraphProjection before,
        WoundWorkingOperationAddress address, MortalWoundTreatmentOperation operation);
```

Existing references use the factory-owned sentinel address `(-1,-1)`. A symbolic
reference includes its exact origin, result/operation address and local value.
Complication ownership is stored only in `OwnedRoots`, not in another independently
authoritative root-domain field. Definition/stack keys remain unchanged global keys.
T067 outcome `OperationOrdinal` is **zero-based**, as `Resolver.Semantics.cs:687`
and `OutcomeIntents.cs:506-509` require. The selected result's `OperationIndex` maps
to that exact authenticated ordinal, not to the effect planner's separate ordinals.

The internal `MortalWoundTreatmentWorkingGraphProjection` class owns one deep-cloned
original `_baseline` for unchanged metadata, plus `Scalars` and the sole current
`Graph`. Its constructor is private. Original topology in `_baseline` is never used
as current topology. SlotsUsed is derived from graph entries; SlotBudget is stored
independently. Pin these member contracts (implementation uses the owners below):

The shared scalar switch needs only this immutable metadata value from the original
envelope; do not expose its stale original topology as an accessible current graph:

```csharp
internal int OriginalTransitionTurn => _baseline.LastTransition.Turn;
```

```text
static MortalWoundTreatmentWorkingGraphProjection? FromCanonical(WoundMaterializationEnvelope? candidate)
bool TryExportExisting(out WoundMaterializationEnvelope? wound)
MortalWoundTreatmentWorkingGraphProjection WithScalars(MortalWoundTreatmentWorkingScalars scalars)
MortalWoundTreatmentWorkingGraphProjection WithGraph(WoundWorkingOwnedGraph graph)
bool TryRemoveExistingComplication(string complicationId, out MortalWoundTreatmentWorkingGraphProjection? after)
bool TryAppendComplication(MortalWoundComplicationProposalDraft draft, WoundWorkingReferenceOrigin origin, WoundWorkingOperationAddress address, out MortalWoundTreatmentWorkingGraphProjection? after)
ImmutableArray<ValidationIssue> ValidateGraph(string path)
```

`TryExportExisting` refuses even an effectless symbolic complication and reconstructs
only real canonical coordinates over baseline metadata, then uses the existing
canonical parser. It reconstructs definition facts from final definitions, not old
cached facts. Fact construction remains the one existing `BuildOwnedEffectDefinitionFacts`
owner. All input arrays/JsonElements and nested scalar collections are cloned/frozen.

Pin the distinctly named simulator entry and private shared loop contract:

```csharp
internal static MortalWoundTreatmentWorkingGraphSimulation SimulateGraph(
    WoundMaterializationEnvelope? startingWound,
    IEnumerable<ImmutableArray<MortalWoundTreatmentOperation>> declaredResults,
    MortalWoundTreatmentPreparedGraphOperationReducer preparedOperationReducer);

private static MortalWoundTreatmentWorkingGraphSimulation SimulateCore(
    MortalWoundTreatmentWorkingGraphProjection starting,
    IEnumerable<ImmutableArray<MortalWoundTreatmentOperation>> declaredResults,
    MortalWoundTreatmentPreparedGraphOperationReducer? prepareComplex,
    Func<MortalWoundTreatmentWorkingGraphProjection, MortalWoundReduceSeverityOperation,
        MortalWoundTreatmentWorkingGraphProjection?> projectReduction,
    Func<MortalWoundTreatmentWorkingGraphProjection,
        MortalWoundTreatmentWorkingGraphProjection?> normalizeAfter);
```

The boundary delegates are chosen privately by `Simulate`/`SimulateGraph`; callers
cannot substitute their normalization/reduction rules. Move the existing one loop
and scalar switch rather than copying it. Preserve canonical callback invocation,
null/default/terminal handling, sole-only no-improvement and OR accumulation of
Improved. Keep per-result grammar at the existing route parser; do not impose an
eight-operation cap over a whole multi-result course. Procedure closure calls:

```csharp
(working, address, operation) => PrepareComplexGraphApplicability(
    working, address, operation, acceptedState, coordinates)
```

Its helper has these exact final parameter types, not a new authority object:
`MortalWoundTreatmentWorkingGraphProjection`, `WoundWorkingOperationAddress`,
`MortalWoundTreatmentOperation`, `MortalWoundTreatmentAcceptedStateAuthority`,
`MortalWoundTreatmentAttemptCoordinates`; it returns the prepared graph result above.
The typed converter is `WoundResponseInputComposer.ConvertTreatmentComplicationGraph`
with `(MortalWoundComplicationProposalDraft draft, string owningWoundId,
WoundWorkingReferenceOrigin origin, WoundWorkingOperationAddress address)` and returns
`WoundWorkingOwnedGraph`. Only DirectAddition/PolicyAddition are legal origins there.

**Canonical validation and reduction preservation boundary:**

- Keep raw canonical shape/count/diagnostic gates in their existing parser wrapper.
  Extract `ImportOwnedGraph(IReadOnlyList<WoundComplication>, WoundConsequences)` and
  `ValidateOwnedGraph(WoundWorkingGraphContext, WoundWorkingOwnedGraph, string path,
  int diagnosticSlotsUsed, List<ValidationIssue>)` under `WoundMaterializationContract`.
  Move its existing definition/root/ownership/edge/slot rules once. Canonical roots
  retain actual identifier/confusable diagnostics; symbolic roots use the tagged
  namespace and local value without hiding malformed identifiers behind hashes.
- Existing canonical Parse calls common `EffectSourceDefinitionContract.ValidateArray`
  and owned-graph rules, **not** the persisted component envelope adapter. Do not add
  that adapter to every canonical/afterlife parse during extraction. Preserve its raw
  count bailout, validation ordering and error paths/codes. Symbolic procedure admission
  runs both families over the whole graph after each operation.
- Graph admission checks16 complications,5 definitions/roots,4 entries, valid current
  rank and0..4 SlotBudget before traversal. Reuse the parser's bounded-check helpers;
  those outer shape checks are not all inside its current owned-graph method.
- Adapt already-validated tagged references to stable injective `root_0`/`definition_0`
  references only for `WoundPersistedConsequenceEnvelopeAdapter.ValidateDetached`.
  They never enter canonical IDs, T067 maps or result entries. Original reference/key
  collision checks occur before this table. Preserve authored slot ordering and
  common effect-definition diagnostics; do not replace the existing authored
  `ValidatePrevalidatedDetached` multi-error path.
- In `MortalWoundTreatmentSeverityReductionPlanner`, extract existing argument checks
  as `ValidateReductionArguments(int steps, string eventRef, List<ValidationIssue>)`,
  checked subtraction/minimum as `TryGetReductionRank(int rank, int steps, out int result,
  List<ValidationIssue>)`, and the one scalar delta as `ApplyReductionScalars` returning
  a record `MortalWoundTreatmentReductionScalars(WoundSeverity Severity, int SlotBudget)`.
  Its complete scalar implementation is:

  ```csharp
  internal static MortalWoundTreatmentReductionScalars ApplyReductionScalars(
      WoundSeverity before, int resultingRank, string resultingLastChangeEventRef) =>
      new(before with
      {
          Value = SeverityValue(resultingRank),
          Rank = resultingRank,
          LastChangeEventRef = resultingLastChangeEventRef
      }, resultingRank);
  ```

  Reuse existing private `SeverityValue`; retain MaximumAtCreation. Extract the existing
  destination adapter invocation behind a distinct reference-taking overload accepting
  rank, `IReadOnlyList<WoundPersistedConsequenceDefinition>`,
  `IReadOnlyList<WoundPersistedConsequenceRoot>`, slotsUsed and issues. Keep its existing
  path, `requireExactGlobalSlotAgreement:true` and `persistedSlotsUsed` behavior.
  The new overload body is the existing adapter call, not a copied power validator:

  ```csharp
  internal static WoundPersistedConsequenceEnvelopeValidationResult ValidateDestinationGraph(
      int resultingRank,
      IReadOnlyList<WoundPersistedConsequenceDefinition> definitions,
      IReadOnlyList<WoundPersistedConsequenceRoot> roots,
      int slotsUsed,
      List<ValidationIssue> issues)
  {
      var validation = WoundPersistedConsequenceEnvelopeAdapter.ValidateDetached(
          resultingRank, ProjectionPath + ".before.consequences", definitions, roots,
          requireExactGlobalSlotAgreement: true, persistedSlotsUsed: slotsUsed);
      issues.AddRange(validation.Issues);
      return validation;
  }
  ```

  The existing canonical wrapper keeps its exact definition/root packaging and calls
  this overload. `ValidateReductionArguments` and `TryGetReductionRank` return bool,
  moving the existing steps/event and checked minimum-rank blocks unchanged, including
  early returns, paths and order; true means no issue was added by that helper.
  Canonical `Project` retains before/after parsing, root ordering and version1 fingerprint;
  graph reduction calls the same helpers without making a canonical projection for
  unresolved roots. Policy increase does NOT use the reduction scalar helper: it still
  changes Rank/Value only and preserves SlotBudget/event/care/anchors.

**Implementation steps:**

- [ ] **Step 1: Write the failing graph and admission owners.**

   Write failing pure cumulative/order/collision tests and a real request rejection
   showing an invalid nonselected band leaves the lowest-free die unclaimed.
   Keep canonical facade/callback regression bodies unchanged as preservation controls.
   Start the new pure owner with these concrete detachment/export regressions:

   ```csharp
   private static WoundMaterializationEnvelope ReadCanonicalWound()
   {
       var parsed = WoundMaterializationContract.Parse(
           WoundContractTestData.CreateActiveWound().ToJsonString(), "workingGraph.before");
       Assert.True(parsed.IsValid, string.Join(" | ", parsed.Issues.Select(issue => issue.Code)));
       return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
   }

   [Fact]
   public void GraphProjection_CanonicalImportExportIsExactAndDetached()
   {
       var original = ReadCanonicalWound();
       var symptoms = original.Display.VisibleSymptoms.ToList();
       var borrowed = original with { Display = original.Display with { VisibleSymptoms = symptoms } };
       var expected = WoundMaterializationContract.SerializeCanonical(borrowed);
       var graph = Assert.IsType<MortalWoundTreatmentWorkingGraphProjection>(
           MortalWoundTreatmentWorkingGraphProjection.FromCanonical(borrowed));
       symptoms.Add("caller changed the borrowed list");
       Assert.True(graph.TryExportExisting(out var exported));
       Assert.Equal(expected, WoundMaterializationContract.SerializeCanonical(exported!));
   }

   [Fact]
   public void GraphProjection_UnresolvedGraphCannotExportCanonicalCoordinates()
   {
       var graph = Assert.IsType<MortalWoundTreatmentWorkingGraphProjection>(
           MortalWoundTreatmentWorkingGraphProjection.FromCanonical(ReadCanonicalWound()));
       var draft = new MortalWoundComplicationProposalDraft(
           new WoundComplicationProposalDraft("new_complication", "impairment", "active",
               "Натяжение края раны", 1, "known_to_player"),
           ImmutableArray<WoundConsequenceDefinitionProposalDraft>.Empty);
       Assert.True(graph.TryAppendComplication(draft, WoundWorkingReferenceOrigin.DirectAddition,
           new WoundWorkingOperationAddress(0, 0), out var changed));
       Assert.Empty(changed!.ValidateGraph("workingGraph.after"));
       Assert.Equal(graph.Graph.Complications.Length + 1, changed.Graph.Complications.Length);
       Assert.False(changed.TryExportExisting(out var exported));
       Assert.Null(exported);
   }
   ```

   Use namespace `BookOfEternityClient.Tests` and the existing Services/xUnit imports,
   plus `System.Collections.Immutable`. The complete remaining named oracles and
   bounded exact filters are specified below; these two do not replace cumulative
   admission or real before-die evidence.

- [ ] **Step 2: Import and freeze the one working graph.**

   Import the original canonical wound once into detached immutable scalar, definition,
   root, complication, ownership and slot data. Tag existing complication IDs/root
   effect IDs separately from `AddedComplicationRef(ordinal, localRef)` and new root
   references. Add a distinct policy origin tag where needed. No symbolic coordinate
   may be serialized into canonical `effectId`, history, T067 map or result entry.
- [ ] **Step 3: Extract the existing converter and graph validators.**

   Reuse the existing typed draft conversion, marker binding, real wound source link,
   parsed definition facts, graph readers and persisted severity adapter. The graph
   wrapper adds provenance/ownership, not a parallel effect model. A reversible
   internal tuple encoding is allowed only as a detached validator reference.
- [ ] **Step 4: Share one ordered scalar/graph loop.**

   Apply operations in authored order and validate the entire graph after every
   operation: all existing complication/definition/root/slot/power/marker/edge caps,
   source/target/realm/ownership, reachability, replacement domain, stack and lifecycle
   rules. Preserve checked arithmetic and result grammar. Do not validate final
   counts only or regroup removals/reductions ahead of additions.
- [ ] **Step 5: Share pruning and exact append operations.**

   Move reachability pruning and slot compaction into this one graph operation;
   the accepted `TryRemoveComplication` canonical adapter delegates to it. Preserve
   the exact selected original ID rule; no syntax for removing an unallocated new
   complication is introduced. Positive scalar transitions use the same rules and
   after-images as the existing simulator and severity projector.
   `TryAppendComplication` calls the one converter, appends its complete fragment,
   offsets new entry slots by the existing entry count with checked arithmetic,
   validates the entire union and returns the detached after graph only on success.
   Both direct/policy callbacks and example tests use this same append operation.
   It never allocates accepted IDs or rewrites definition/stack keys.

   The complete union operation is:

   ```csharp
   internal bool TryAppendComplication(
       MortalWoundComplicationProposalDraft draft,
       WoundWorkingReferenceOrigin origin,
       WoundWorkingOperationAddress address,
       out MortalWoundTreatmentWorkingGraphProjection? after)
   {
       after = null;
       if (origin is not (WoundWorkingReferenceOrigin.DirectAddition or
                          WoundWorkingReferenceOrigin.PolicyAddition) ||
           address.ResultIndex < 0 || address.OperationIndex < 0)
           return false;
       try
       {
           var fragment = WoundResponseInputComposer.ConvertTreatmentComplicationGraph(
               draft, _baseline.WoundId, origin, address);
           var addedEntries = fragment.Entries.Select(entry => entry with
           {
               Slot = checked(entry.Slot + Graph.Entries.Length)
           });
           var candidate = WithGraph(new WoundWorkingOwnedGraph(
               Graph.Complications.AddRange(fragment.Complications),
               Graph.Definitions.AddRange(fragment.Definitions),
               Graph.Roots.AddRange(fragment.Roots),
               Graph.Entries.AddRange(addedEntries)));
           if (!candidate.ValidateGraph("mortalWoundTreatment.workingGraph").IsEmpty)
               return false;
           after = candidate;
           return true;
       }
       catch (Exception exception) when (exception is ArgumentException or
                                          InvalidOperationException or
                                          JsonException or OverflowException)
       {
           return false;
       }
   }
   ```

   The converter supplies fragment slots starting at1, preserves definition/stack
   keys and detached JSON, binds the real wound link/markers via the existing helpers,
   and rejects malformed typed proposals with the same caught argument/JSON failure
   family. `WithGraph` clones all borrowed arrays/JSON before the candidate escapes.

- [ ] **Step 6: Enforce exact reference and semantic-key namespaces.**

   Enforce exact/confusable direct-add complication refs across each result and nested local
   refs within their namespaces. Technical local spellings may repeat across distinct
   namespaces. Definition and stack keys stay authored exact coordinates and must
   pass combined-graph collision checks; never rename them to fit.
   Existing canonical IDs and policy-origin symbols are not direct author refs.
   Preserve the direct-result namespace from `ValidateAddComplication` rather than
   making identical local spelling across distinct direct/policy origins collide;
   their global definition/stack keys still share the same collision checks.
- [ ] **Step 7: Preserve the canonical facade and wire procedure graph admission.**

   Preserve `Simulate` and the envelope callback: canonical-only graph exports call
   the old callback and canonical-round-trip its result. Without a callback, its
   complex operations remain unsupported. Existing successful consumers still obtain
   the same nonnull canonical `WorkingWound`. Procedure admission uses `SimulateGraph`
   and a typed graph callback, never a fake envelope or a nullable substitute.
- [ ] **Step 8: Bridge existing typed policy applicability without publication decisions.**

   Obtain policy authority from the unchanged T069 factory against the original
   accepted state and coordinates. Preserve fingerprints and the established
   failed-procedure path despite the historical internal `treatment_interruption`
   scope label. Lift only its already typed result into the graph:
   - increase: checked current Rank/Value delta only, with the existing SlotBudget,
     care/anchor/progress fields preserved and the complete graph revalidated;
   - policy addition: append its fully validated typed subproposal under a distinct
     symbolic policy origin, with no permanent policy complication ID/ref map;
   - explicit death contour: unchanged graph plus exact policy-kind/fingerprint
     applicability evidence; no inferred terminal, care or later-operation rule.
   Preserve the T069 original-wound admission gate even if a prior symbolic removal
   could otherwise make a baseline-invalid policy fit. Do not forge successor authority.

   Expose only the existing typed builder, not a new JSON parser/authority factory:

   ```csharp
   internal static MortalWoundComplicationProposalDraft BuildValidatedComplicationDraft(
       JsonElement draft) => BuildComplicationDraft(draft);
   ```

   It lives in `MortalWoundTreatmentContract` beside the private builder in
   `MortalWoundTreatmentModel.cs`. The sole new policy caller supplies
   `authorityResult.Authority.Policy.Result.GetProperty("complicationDraft")`
   only after successful T069 creation. The GM JSON contains `complications:[...]`
   (exactly one), while the resulting typed record has singular `Complication`.
   Never confuse these two dialects or replace the validated policy with raw route JSON.
   For direct and policy append, return exactly
   `new MortalWoundTreatmentPreparedGraphOperationResult(applicable, false, after)`
   from the shared TryAppend result; failure must return null After.
- [ ] **Step 9: Verify existing callers and mixed adverse sequence preservation.**

   Keep guaranteed/course positive callers and direct T067 course interruption path
   unchanged. Policy/direct additions accumulate in both orders; a legal policy
   sibling band must not prevent a supported selected direct-add band from resolving.
   The selected policy publication identity/death/secondary-state decisions remain
   outside this task. Document preview preservation separately from future publication.
- [ ] **Step 10: Synchronize, verify, commit and obtain independent review.**

    Synchronize documentation/examples/guards, inspect the exact diff, run bounded
    controls, and commit only this task's implementation. Obtain fresh independent
    Spec+Quality review; parent confirms actual artifacts before acceptance.

**Minimum test oracles:**

- Complete graph cap boundaries and cumulative overflow (including an unselected
  band), exact/confusable collision and legitimate namespaced repeated local spelling.
- At rankIII with base1slot + old complication1slot,
  `remove(old), reduce(1), add(new1slot)` fits rankII; an ordering with overflowing
  intermediate reduction rejects even when its final graph would fit. Include a
  second reduction/removal/add permutation and a reachable child definition.
- Existing canonical stabilization/recovery/removal/reduction/no-op/heal applicability
  and callback-only deterioration after-images remain unchanged; no callback still
  rejects prepared adverse work. Existing canonical effect graph rejection survives
  extraction, including spiritual exact-slot and zero-slot lifecycle rules.
- Policy-add/direct-add both orders retain all prior graph nodes and detect real
  overflow/collision. Increase preserves existing SlotBudget and original T069 scope.
  Explicit death before/after symbolic addition preserves proof without terminalizing.
- Preserve the existing `mixed_interruption` canonical round-trip row and failed-
  procedure direct-add/increase-policy T067 intent rows; these are not T070 publisher
  evidence. Missing/wrong typed policy authority cannot become an applicable draft.
- No acceptance registry, filesystem, ID allocator or mutable caller alias in the
  pure graph. Real request rejection leaves die/Fate/resource claims untouched.

**Executable GM example and guard (Task1):**

Retain the full `mortal_wound_treatment_reduce_severity_v1` route in BOTH the guide
and CLI document, including unchanged successful reduction, partial recovery,
requirements, resource policy, margins and roll authority. Replace only its failed
band's result with this complete typed effectful complication subproposal:

```json
[
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
            "allowedRealms": ["mortal_world"],
            "allowedTargetKinds": ["player"],
            "components": [
              {
                "componentId": "strained_edge_item_control",
                "profile": "action_control",
                "priority": 100,
                "payload": { "action": "use_item", "operation": "restrict" }
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
              "dispelCategories": ["physical_restoration"],
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
```

The existing complete guard
`WoundTreatmentReduceSeverityDocumentation_UsesParsedOrderedMortalRoute` must retain
its guide/CLI deep-equality, complete-wound Parse/ParseProjection, success/partial
assertions and destination projector checks. After `treatment`/`route` are parsed,
append this actual graph conversion check (no example payload repair in the test):

```csharp
var failed = Assert.Single(route.Bands, static band =>
    string.Equals(band.Category, "failed_attempt", StringComparison.Ordinal));
var addition = Assert.IsType<MortalWoundAddComplicationOperation>(
    Assert.Single(failed.DeclaredResult));
var failedBefore = Assert.IsType<MortalWoundTreatmentWorkingGraphProjection>(
    MortalWoundTreatmentWorkingGraphProjection.FromCanonical(parsedWound));
Assert.True(failedBefore.TryAppendComplication(
    addition.ComplicationDraft, WoundWorkingReferenceOrigin.DirectAddition,
    new WoundWorkingOperationAddress(0, 0), out var failedAfter));
Assert.Empty(failedAfter!.ValidateGraph(marker + ".failed_graph"));
Assert.Equal(3, failedAfter.Graph.Entries.Length);
Assert.Equal(parsedWound.Severity, failedAfter.Scalars.Severity);
Assert.Equal(parsedWound.Complications.Count + 1, failedAfter.Graph.Complications.Length);
Assert.False(failedAfter.TryExportExisting(out _));
```

Use these exact normative sentences in both documents and extend the existing
manifest/guard requiredText checks with them:

> The client validates the complete cumulative graph after every authored operation,
> including unselected procedure bands, before claiming a die or resources.
> Local complication and definition references are not permanent runtime identities.
> Applicability preview does not authorize publication of an unfinished outcome.

Task1 documents pre-roll support and explicitly preserves the current selected-add
publication boundary; Task2 removes that boundary only with real publication evidence.
Do not describe the failed band as scalar-only after this edit. Existing negative
alternative/repair examples remain deliberately negative. The effect definition uses
the established fully worked action_control shape, empty author links and a real
client-bound wound link; no canonical wound/effect ID or private seal is GM-authored.
Update the existing manifest validationRoute with this graph path; no speculative
new runtime route, daemon prompt entrypoint or duplicate partial example is needed.

**Exact verification selectors (Task1):**

Name the seven new pure methods `GraphProjection_CanonicalImportExportIsExactAndDetached`,
`GraphProjection_UnresolvedGraphCannotExportCanonicalCoordinates`,
`GraphProjection_OrderedAddRemoveReduceUsesOneCurrentGraph`,
`GraphProjection_ReferenceNamespacesDoNotHideSemanticKeyCollisions`,
`GraphProjection_WholeGraphCapsAndOwnershipMatchCanonicalRules`,
`GraphProjection_ReductionMatchesCanonicalProjectWithoutPruning` and
`GraphProjection_CanonicalFacadeRetainsPreparedCallbackContract`. Use theories
for the stated boundary/order rows. Name the three new Integration methods
`ComplicationGraphApplicability_CumulativeOverflowRejectsBeforeDieClaim`,
`ComplicationGraphApplicability_InterleavedRemoveReduceAddUsesAuthoredOrder` and
`ComplicationGraphApplicability_PreservesTypedPolicyAndDeathSiblingBands`.
The real new pre-roll fixture must resolve through the existing request/die path,
not a helper-inserted claim; prove the next legal route receives the same lowest die.

Run the smallest relevant new owner RED/GREEN first. At the final coherent checkpoint,
these source-pinned selections are the required union, each once on its final code:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentWorkingGraphProjectionTests|FullyQualifiedName~WoundMaterializationContractTests|FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests|FullyQualifiedName~WoundConsequenceEnvelopeTests.DetachedMortalEnvelope_"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentContractTests.Parse_EffectfulComplication|FullyQualifiedName~MortalWoundTreatmentContractTests.CanonicalRoundTrip_UsesTypedWriterForEveryTreatmentPayloadFamily|FullyQualifiedName~MortalWoundTreatmentContractTests.ProposalComposition_|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatment|FullyQualifiedName~FastTestBoundaryTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.ComplicationGraphApplicability_"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.PrerequisiteAuthority_WorkingWound|FullyQualifiedName~MortalWoundTreatmentResolverTests.PrerequisiteAuthority_CourseSequenceValidatesCompleteConsequenceEnvelope|FullyQualifiedName~MortalWoundTreatmentResolverTests.DeteriorationPolicyAuthority_|FullyQualifiedName~MortalWoundTreatmentResolverTests.OutcomeIntents_AreProductionDerivedOneForEachDeclaredOperation|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.PublicationPrerequisite_EffectlessUntreatedWoundAcceptsGuaranteedStabilization"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureReduction_Destination|FullyQualifiedName~MortalWoundTreatmentResolverTests.ProcedureReduction_RejectedBandLeavesLowestFreeDieForNextLegalRoute"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
git diff --check
```

Expected: selected graph/canonical/converter/callback/adverse/pre-roll/example
controls pass with exact prior diagnostics and no weakened assertions. One Fast may
still expose the known unfinished legacy RED; report actual executed subset. Full
FullValidation is required here because the shared canonical owner and examples
change, even though Task1 introduces no afterlife gameplay. Inspect actual artifact
membership and timeout/cleanup; discovery is never passing evidence. Register the
new heavy partial from the current inventory count62 to63 in both inventories/docs.
Commit only Task1's named implementation/test/docs files after scoped diff inspection:
`git commit -m "feat(wounds): validate cumulative complication graph before selection (#1536)"`.
Do not stage this parent-owned plan or Spec Kit progress; parent updates them after
fresh independent Spec+Quality review and actual evidence acceptance.

### Task 2: Selected direct-add binding and single-batch publication

**Files and ownership:**

- Extend the accepted graph/converter from Task1. Factor T067's current
  `ComposeAddComplicationIntent` under `BookOfEternityClient/Services/MortalWoundTreatmentResolver.Semantics.cs`
  into one private/internal pure binding preparation plus the unchanged public factory.
- Extract the existing response-local-coordinate writer in
  `BookOfEternityClient/Services/WoundResponseInputComposer.Parsing.cs` for reuse, without changing its domain/formula.
- Extend private outcome preparation/seals in
  `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.cs`; extend the existing
  `BookOfEternityClient/Services/MortalWoundTreatmentSeverityRematerializationPlanner.cs` compiler and its independent
  agreement, not a new publisher or reflected entrypoint.
- Narrow shared additions in `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentPublication.cs`,
  `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs` and
  `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`: derived topology,
  mixed retained/new final assembly, selective terminals, predecessor partition and
  new-versus-retained skill authority. Preserve the accepted terminal-generation owner.
- Add `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ComplicationAdditionPublication.cs`
  and pure graph/binding/batch controls. Update heavy inventories/docs as in Task1.
- Synchronize Mortal guide, complete worked example, manifest and executable guards.
  Check shared afterlife matrix/example/source-guard coverage explicitly.

**Implementation steps:**

- [ ] **Step 1: Write selected-addition and exact binding regressions.**

   Write real selected same-rank addition and authenticated effectless-batch REDs,
   then exact T067 formula/detachment/map-tamper pure REDs. Do not install accepted
   treatment history manually or create a fake result to satisfy publication.
- [ ] **Step 2: Extract the owning T067 binding preparation.**

   T067-owned binding preparation receives exact RequestFingerprint, zero-based
   operation ordinal, typed draft and recomputed declared-operation fingerprint.
   Preserve its complication ID, both ordered public maps and version1 preparation
   fingerprint byte-for-byte. Add only detached ephemeral private root rows.
   Root operation keys use the existing response-coordinate writer with prefix
   `wound_root_operation` and fields `[requestFingerprint, mappedDefinitionRef]`;
   the complete domain is `book_of_eternity.wound.response_local_coordinate`, version1.
   Creation and T067 call one actual helper. T070 consumes/recomputes the owning
   preparation; it does not invent an alternative key derivation or accepted identity.
- [ ] **Step 3: Seal and independently reconstruct the exact typed addition.**

   Freeze and compare each full typed draft, ordered ComplicationRef/ID, both maps,
   preparation fingerprint and private root rows. Reconstruct from exact typed
   request/ordinal after restart; no public/persisted field, process-local identity
   cache, raw route parsing or acceptance shortcut is added.
- [ ] **Step 4: Bind only authenticated selected direct symbols.**

   Replay the shared ordered graph, then bind only direct added symbols to their
   exact T067 complication IDs and mapped definition/application refs. Validate
   missing/extra/reordered/confusable/borrowed maps and key collisions. Policy symbols
   cannot masquerade as direct additions. The common effect stage alone supplies
   final canonical effect IDs through complete application results.
- [ ] **Step 5: Derive and revalidate private final topology.**

   Derive a sealed private selected compilation: original before, ordered operation
   evidence, final graph/rank, surviving old coordinates, new coordinates/maps,
   removed original roots and whether a selected reduction requires rematerialization.
   Seal and independently recompute full content, not only public fingerprints.
- [ ] **Step 6: Compile exactly one final batch.**

   | Selected operations | Applications | Existing lineage | Terminal selection |
   | --- | --- | --- | --- |
   | Same-rank additions | New roots only | All retained original roots | None |
   | Same-rank removal + addition | New roots only | Surviving original roots | Selected removed-root live closure |
   | Any reduction + additions/removals | Every final root exactly once | No old materialized roots retained | Entire original-root live closure |
   | Effectless addition | No roots for that addition | All surviving originals | Only separately selected removals |

   Required batch is derived from addition/removal/reduction intents, including0/0;
   never from nonempty operation arrays. Export the complete final definition graph.
   Preserve retained same-rank payload/history and exact original ownership. Fresh
   addition roots are parentless even if added before a reduction; surviving old
   coordinates have exactly their original canonical prior root, including a valid
   carrierless terminal predecessor. No transient generation is ever published.
- [ ] **Step 7: Authenticate the exact predecessor partition.**

   Replace shared inferences such as 'any application means full rematerialization'
   only with authenticated private topology. Exact predecessor bijection applies
   to surviving original coordinates; new coordinates require no predecessor.
   Preserve selective terminal ownership and retained-lineage checks even at0/0.
- [ ] **Step 8: Separate new skill admission from accepted continuation.**

   Newly added roots must use normal fresh Offered/Current skill validation; they
   cannot inherit the existing blanket accepted-treatment skill continuation exception.
   Surviving accepted definitions keep the existing exact continuation behavior even
   if the old skill is no longer offered. Test both through independent effect checks.
- [ ] **Step 9: Assemble one final wound and correct the required-batch diagnostic.**

   Extend the one final wound assembler to combine unchanged retained bindings with
   new application results. Do not feed old roots as fake application results or
   overwrite them via the previous all-applications-only assembly. Revalidate final
   graph, slots, source export, result-map cardinality and all complete seals.
   Resolve accepted-removal review Minor M1 at the same branch: choose the actual
   required-batch diagnostic from private topology instead of claiming every
   unchanged-severity result requires a null batch. Add an exact diagnostic assertion
   to a missing/invalid required-batch negative; do not alter its rejection behavior.
- [ ] **Step 10: Verify atomic publication and obtain independent review.**

    Publish effects, identities, wound, resources, history and output through the
    existing transaction; prove post-write rollback, same-plan retry and cold replay.
    Preserve critical reactions and legal mode outcomes. Synchronize docs, verify,
    commit this task only and obtain independent review/parent artifact acceptance.

**Minimum test oracles:**

- Same-rank effectful/effectless additions, exact new complication identity and maps,
  retained full carrier/index history, new parentless roots and no fabricated0/0 effect.
- Multiple additions, local spelling namespaces, real definition/stack collisions,
  selected binding/map/ordinal/private key tampering, and independently resealed wrong
  graph/proof/batch/source/result rows. Frozen public member-shape tests stay unchanged.
- Removal+addition and reduction+addition in multiple legal orders materialize only
  the final graph once; exact old terminal/live parents and selective causal teardown.
- New specific-skill selector admission versus retained skill-loss continuation;
  neither all-skills nor existing exact-skill behavior is weakened.
- Real failed-procedure and course-interruption supported direct-add outcomes;
  existing positive-mode grammar must not be relaxed to allow adverse operations.
  Natural1/Fate and natural20 behavior retain their existing exact critical rules.
- Missing required batch/proof or changed retained lineage rejects effectless0/0;
  resource/receipt atomicity, byte-exact rollback, retry and cold replay remain real
  accepted-pipeline evidence, not a helper-installed history assertion.

## Verification and acceptance

Controller self-review before Task1 dispatch: shared types/signatures and canonical
source owners were checked against accepted removal plus test-only alignments. The
original transition-turn accessor, JSON `complications` array versus typed singular
property, zero-based T067 ordinal, shared reduction return type, direct/policy local
namespaces and exact preservation filters are pinned above. Task1 has no gameplay
choice gate and no runtime/publication authority. Full policy publication and heal
legacy are explicitly uncovered by these two bounded tasks and remain separately
tracked under T070; no whole-feature closure is implied. Before Task2 dispatch,
rebase its compiler/binding seams and owner selectors to the independently accepted
Task1 code and generate a fresh task-specific brief with that exact BASE.

For each task, pin actual test-owner names in its generated brief after source
pre-flight; do not dispatch a vague full-family sweep. Run the smallest relevant RED
and GREEN selections through `scripts/test-csharp.ps1`, then a coherent exact union
of changed/new owners plus their preservation controls. Use Focused Integration10m
for related filesystem owners; split into disjoint selections or expand up to15m only
with measured/source-backed need. No duplicate unchanged broad run to chase a total.

Run one Fast at a meaningful task checkpoint. If the lane fails fast, report actual
executed rows and the omitted remainder rather than claiming a complete green lane.
The unfinished `WoundLegacySource_SurvivesWithoutActiveWoundButIsNeverPubliclyMaterializable`
RED remains explicit; do not delete/skip/weaken it or infer the unanswered legacy
preparation choice. FullValidation(default15m) is required for a changed shared
canonical/afterlife docs/examples boundary, with full AfterlifeDocumentationCoverageTests;
otherwise record the narrow no-update rationale. No PreMerge or unrelated Deep/Lifecycle
lane before this feature is genuinely ready for integration.

Each report contains exact BASE/HEAD, changed files, actual artifact directories,
summary/TRX/build/timeout/cleanup facts, no invented passing counts, docs decisions,
remaining scope and an exact review package. Independent reviews are evidence inputs;
only the parent may accept after inspecting diffs/artifacts and resolving findings.
After Task2, direct addition is accepted only; full healing/legacy and actual policy
publication remain explicit next work under T070, alongside the rest of #1536.
