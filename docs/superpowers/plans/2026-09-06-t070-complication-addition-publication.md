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

**Task1 parent acceptance (2026-09-07):** implementation `f3c3c0c..8057a262` plus
review correction `8057a262..ff5ddcb8` are accepted for this bounded task. Independent
review I1 is closed by the existing canonical retained treatment/policy/diagnosis
owners, with Spec compliant / quality Approved and no remaining correction findings.
Parent inspected complete diffs/reports and all25 original plus all10 correction
artifact sets. Fresh correction controls: new pure7/7, real admission2/2, full owner
463passed plus required legacy1/464, Integration20/20, afterlife121/121, FullValidation
1856/1856 in9:04.8574487. ONE Fast6006passed plus required legacy1/6007 is an
incomplete fail-fast subset, not full success. Builds/cleanup clean, no timeout or
duplicates. See `2026-09-06-t070-retained-state-parity.md` for source-confirmed
exception/SourcePath alignment and evidence. All ten Task1 steps are complete;
Task2, T070, T177 and #1536 remain open. No canonical rule or gameplay was relaxed.

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
- T177 source-confirmed verification correction, approved during Task1: in
  `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.DeteriorationPolicyAuthority.cs`,
  only `DeteriorationPolicyAuthority_RejectsSlotOverflowWithRootsAndDefinitionsAvailable`
  restores the original two-slot consequence fixture before accepted-state creation:
  `scenario.Before["consequences"] = WoundContractTestData.CreateActiveWound()["consequences"]!.DeepClone();`.
  The shared CreateScenario at accepted f3c3c0c1993–2002 already removes slot/root/
  definition1 and sets slotsUsed1, while this unchanged row still requires2.
  Preserve all four existing2-count assertions and the same T069 rejection oracle.
  No shared fixture, gameplay validator, policy definition or outcome grammar edit.
  Existing failing artifact225229-237-41592-a3a3894a34fe4185a81a35ab1ca1a5aa is
  the RED; rerun the coherent26-row prerequisite/policy selection after this correction.
- Parent source-audit correction: the old string ownership helper used `base_wound`
  as its base-root sentinel, colliding with an otherwise legal complicationId of
  that exact spelling. The shared tagged owner uses null for base and a reference
  for complication ownership, preserving the intended disjoint-domain rule. Add
  exact regression rows in `BookOfEternityClient.Tests/WoundMaterializationContractTests.cs`
  using its existing CreateSingleLeafWound(bindLeaf:true, leafPolicy:"replace") and
  CreateComplication helpers: base-vs-complication `base_wound` rejects at the existing
  edge definitionKey path/code; both roots in the same such complication remain legal.
  Do not reserve/ban the name or add a parser exception. This is a source-proven
  ambiguity correction, not a claim that this edge was observed RED before extraction.
  Include these rows in the final canonical/graph union; no additional production
  change or duplicate broad lane is needed solely for the new test coverage.
- Parent pre-acceptance preservation finding: BASE ParseDefinitions records valid
  definitionKey before rejecting forbidden links. Its extracted converter must not
  move that bookkeeping after a possible null result and silently drop the aggregate
  duplicate-definition diagnostic. Add a real `ProposalComposition_` regression in
  `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs` with distinct
  local refs, duplicate exact/confusable definition keys and a forbidden link in
  one definition. Require the original forbidden-link and duplicate-definition
  code/path ordering together; keep one shared conversion/validation owner. Observe
  the smallest real RED before any correction. FullValidation already running may
  finish; report its exact code boundary and reverify the changed diagnostic owner
  after any narrow correction, with no misleading final-evidence claim.

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

- [x] **Step 1: Write the failing graph and admission owners.**

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

- [x] **Step 2: Import and freeze the one working graph.**

   Import the original canonical wound once into detached immutable scalar, definition,
   root, complication, ownership and slot data. Tag existing complication IDs/root
   effect IDs separately from `AddedComplicationRef(ordinal, localRef)` and new root
   references. Add a distinct policy origin tag where needed. No symbolic coordinate
   may be serialized into canonical `effectId`, history, T067 map or result entry.
- [x] **Step 3: Extract the existing converter and graph validators.**

   Reuse the existing typed draft conversion, marker binding, real wound source link,
   parsed definition facts, graph readers and persisted severity adapter. The graph
   wrapper adds provenance/ownership, not a parallel effect model. A reversible
   internal tuple encoding is allowed only as a detached validator reference.
- [x] **Step 4: Share one ordered scalar/graph loop.**

   Apply operations in authored order and validate the entire graph after every
   operation: all existing complication/definition/root/slot/power/marker/edge caps,
   source/target/realm/ownership, reachability, replacement domain, stack and lifecycle
   rules. Preserve checked arithmetic and result grammar. Do not validate final
   counts only or regroup removals/reductions ahead of additions.
- [x] **Step 5: Share pruning and exact append operations.**

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

- [x] **Step 6: Enforce exact reference and semantic-key namespaces.**

   Enforce exact/confusable direct-add complication refs across each result and nested local
   refs within their namespaces. Technical local spellings may repeat across distinct
   namespaces. Definition and stack keys stay authored exact coordinates and must
   pass combined-graph collision checks; never rename them to fit.
   Existing canonical IDs and policy-origin symbols are not direct author refs.
   Preserve the direct-result namespace from `ValidateAddComplication` rather than
   making identical local spelling across distinct direct/policy origins collide;
   their global definition/stack keys still share the same collision checks.
- [x] **Step 7: Preserve the canonical facade and wire procedure graph admission.**

   Preserve `Simulate` and the envelope callback: canonical-only graph exports call
   the old callback and canonical-round-trip its result. Without a callback, its
   complex operations remain unsupported. Existing successful consumers still obtain
   the same nonnull canonical `WorkingWound`. Procedure admission uses `SimulateGraph`
   and a typed graph callback, never a fake envelope or a nullable substitute.
- [x] **Step 8: Bridge existing typed policy applicability without publication decisions.**

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
- [x] **Step 9: Verify existing callers and mixed adverse sequence preservation.**

   Keep guaranteed/course positive callers and direct T067 course interruption path
   unchanged. Policy/direct additions accumulate in both orders; a legal policy
   sibling band must not prevent a supported selected direct-add band from resolving.
   The selected policy publication identity/death/secondary-state decisions remain
   outside this task. Document preview preservation separately from future publication.
- [x] **Step 10: Synchronize, verify, commit and obtain independent review.**

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

**Required source-confirmed supplement (2026-09-07):**
`2026-09-07-t070-selected-add-reducer-continuity.md` adds the exact reducer,
shared final same-rank continuity and named test allowlist. Read it completely
before those edits; it is part of this Task 2, not a separate workstream.

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

**Concrete private interfaces and owner sequence:**

Source preflight below is against accepted `f3c3c0c` before Task1. Rebase line
anchors/type names to the independently accepted Task1 commit before generating
the brief; do not use a concurrent unaccepted implementation as authority. No
new gameplay choice, public schema, publisher or effect identity writer is needed.

Parent source rebase on Task1 commit `8057a262`, accepted through correction `ff5ddcb8`:
SimulateGraph is the designed3-argument entry at54, WithScalars111, TryAppend141,
ValidateGraph163 and OriginalTransitionTurn27 match the interfaces below; ff5ddcb8
adds the shared retained treatment/policy and surviving accepted-diagnosis checks
inside ValidateGraph. Selected graph validation must retain those canonical gates.
The existing response coordinate helper is now Parsing1315, still private and
byte-equivalent. The final converter's validated-key out parameter is private;
Task2 uses ConvertTreatmentComplicationGraph unchanged. All publication/assembler/
T067 owners and named preservation tests below are unchanged from f3c3c0c, and
the parent rechecked their names/signatures. Generate the actual execution BASE
after the parent acceptance documentation commit, and record it in the execution brief.

T067 remains the binding owner in `MortalWoundTreatmentResolver.Semantics.cs`:

```csharp
internal sealed record MortalWoundTreatmentComplicationRootBinding(
    string LocalDefinitionRef, string DefinitionRef,
    string ApplicationRef, string OperationKey);

internal sealed class MortalWoundTreatmentComplicationBindingPreparation
{
    // Private constructor; immutable/detached values, no retained mutable input.
    internal string ComplicationRef { get; }
    internal string ComplicationId { get; }
    internal ImmutableArray<MortalWoundTreatmentReferenceBinding> DefinitionReferenceBindings { get; }
    internal ImmutableArray<MortalWoundTreatmentReferenceBinding> ApplicationReferenceBindings { get; }
    internal string PreparationFingerprint { get; }
    internal ImmutableArray<MortalWoundTreatmentComplicationRootBinding> Roots { get; }
}

// On existing MortalWoundTreatmentOutcomeIntentComposer:
internal static MortalWoundTreatmentComplicationBindingPreparation PrepareComplicationBindings(
    string requestFingerprint, int operationOrdinal,
    MortalWoundComplicationProposalDraft draft, string declaredOperationFingerprint);
```

Move the current `ComposeAddComplicationIntent` lines795–834 into that helper:
exact zero-based invariant ordinal, complication digest, authored definition order,
root-bearing application subset, and version1 preparation fields remain byte-for-byte
equivalent. The outer `complication_intent` fingerprint/factory stays unchanged.
Share the actual response helper `WoundResponseInputComposer.CreateLocalIdentifier`
(Parsing1377) by changing its accessibility, not by copying its formula. Private
root keys use `("wound_root_operation", requestFingerprint, mappedDefinitionRef)`.
Do not add these private keys to the existing public preparation fingerprint.

Existing `MortalWoundTreatmentOutcomePreparation.IntentSeal` gains a private
addition seal: detached typed draft, exact ordered T067 bindings and private root
rows. Use the existing `DeclaredFingerprint(index, operation)` and T067 helper to
recompute it in independent agreement. Extend `IntentsEqual` for every addition
field; comparing fingerprints alone is insufficient. Explicitly clone typed draft
JsonElements/arrays and the selected operation array. There is no generic existing
`CloneResolution` owner to call and no permission to add a process-local ID registry.

Pin a graph path ONLY for an authenticated selected direct addition; preserve the
old canonical no-addition preparation/projection/fingerprints, including mixed
removal/reduction. New private packet types belong to outcome preparation:

```csharp
internal sealed record WoundFinalAssemblyScalars(
    WoundSeverity Severity, WoundCare Care, WoundRecovery Recovery,
    WoundTreatment Treatment, WoundLastTransition LastTransition, int SlotBudget);
internal sealed record MortalWoundTreatmentSelectedRoot(
    WoundWorkingReference Reference, string DefinitionKey,
    WoundRootOwnershipDomain OwnershipDomain,
    ImmutableArray<WoundEffectSlotAgreement> Slots, string? OriginalEffectId,
    MortalWoundTreatmentComplicationRootBinding? AdditionBinding);
internal sealed record MortalWoundTreatmentSelectedDefinitionOrigin(
    int OperationOrdinal, int DefinitionOrdinal, string DefinitionKey,
    string MappedDefinitionRef);
internal sealed class MortalWoundTreatmentSelectedGraphCompilation
{
    // Private constructor; all getters return immutable/deep-detached state.
    internal WoundMaterializationEnvelope Before { get; }
    internal WoundFinalAssemblyScalars FinalScalars { get; }
    internal WoundWorkingOwnedGraph FinalGraph { get; }
    internal ImmutableArray<MortalWoundTreatmentSelectedRoot> FinalRoots { get; }
    internal ImmutableArray<MortalWoundTreatmentComplicationBindingPreparation> Additions { get; }
    internal ImmutableArray<MortalWoundTreatmentSelectedDefinitionOrigin> NewDefinitionOrigins { get; }
    internal ImmutableArray<string> OrderedRemovalIds { get; }
    internal ImmutableArray<string> SelectedTerminalRootIds { get; }
    internal bool HasReduction { get; }
    internal bool RequiresEffectBatch { get; }
    internal string Fingerprint { get; }
}
internal sealed record MortalWoundTreatmentSelectedGraphCompilationResult(
    MortalWoundTreatmentSelectedGraphCompilation? Compilation,
    IReadOnlyList<ValidationIssue> Issues);

// Existing OutcomePublicationPlanner owns the selected compiler:
internal static MortalWoundTreatmentSelectedGraphCompilationResult CompileSelectedGraph(
    WoundMaterializationEnvelope before, MortalWoundTreatmentAttemptRequest request,
    MortalWoundTreatmentResolution resolution, string transitionId, long currentGameMinute);
// Existing OutcomePreparation owns its private replay/state:
internal bool TryRecomposeSelectedGraph(out MortalWoundTreatmentSelectedGraphCompilation? compilation);
internal WoundMaterializationEnvelope GetPreparedCarrierImage();
```

`HasReduction`, `RequiresEffectBatch`, partitions and origins are derived from frozen
authenticated operations, never supplied flags. Bind final graph/scalars, operation
order, full maps/private keys and origins in the private compilation fingerprint;
reuse existing rematerialization authority's `ProjectionFingerprint` for this path.
Do not change any of the frozen13 authority fields,8 batch fields, six-argument
Compose, or the existing uniquely named8-argument Prepare/10-argument Agrees entries.

**Scalar sequencing is exactly one operation pass.** Extract these PRIVATE helpers
in `MortalWoundTreatmentOutcomePublicationPlanner`:

```csharp
private sealed record TreatmentPublicationMetadata(
    string LastAttemptId, string? ActiveCourseId,
    WoundTreatment Treatment, WoundLastTransition LastTransition);
private static TreatmentPublicationMetadata CreatePublicationMetadata(
    WoundMaterializationEnvelope before, MortalWoundTreatmentAttemptRequest request,
    MortalWoundTreatmentResolution resolution, string transitionId);
private static (WoundCare Care, WoundRecovery Recovery) ApplyStabilizationAnchors(
    WoundCare care, WoundRecovery recovery,
    WoundLastTransition selectedTransition, long currentGameMinute);
private static WoundFinalAssemblyScalars DecorateSelectedGraphScalars(
    MortalWoundTreatmentWorkingGraphProjection working,
    TreatmentPublicationMetadata metadata, MortalWoundTreatmentResolution resolution,
    long currentGameMinute);
```

`CreatePublicationMetadata` moves only existing1092–1119: same course ownership,
AppendOnce/None route order, attempt pointer, checked ordinal and request turn.
Old `CreateScalarShell` keeps its signature and original single intent loop after
applying that metadata. Its old `ApplyStabilization` sets state/removes blocker and
calls the extracted anchor helper. Old reduction Project/fingerprint behavior stays.

Addition path authenticates first, computes metadata from ORIGINAL Before but does
not install it before graph import, then calls Task1 `SimulateGraph` ONCE with the
single selected result array. That pass alone applies stabilization state/blockers,
recovery arithmetic, reduction, removal and direct append in authored order. The
direct callback uses Task1's converter/projection with `DirectAddition` provenance;
policy or heal publication is still unsupported here, without invalidating legal
unselected sibling bands. Never call `CreateScalarShell` on the addition path.

After the pass, overlay only LastAttemptId/ActiveCourseId, Treatment and LastTransition
from metadata. If authenticated selected stabilization exists, stamp its selected
turn, create `WoundRecoveryAnchor("stabilization", currentGameMinute, transitionId)`,
and clear only a `not_stabilized` deterioration anchor. Do not call stabilization's
state/blocker operation again; without stabilization, preserve all existing anchors.
If reduction exists, stamp Severity.LastChangeEventRef from selected coordinates'
EventRef without re-subtracting rank or resetting budget. Recovery comes verbatim
from the working graph. Validate with these decorated scalars; no second simulation,
no repeated removal, no implicit progress threshold or carry-over. Final course/
lifecycle evidence is rechecked after decoration.

**Canonical staging and exact transition stamp:** addition preparation's
`GetPreparedCarrierImage` returns actual original canonical Before; final state lives
only in the authenticated private packet until effect IDs exist. Do not manufacture
a canonical reduction projection or fake provisional IDs to satisfy existing
`AgreesWith`/Finalize reduction assumptions. Update their PRIVATE graph-path checks,
prepared-template comparison/storage/fingerprinting at MortalTreatmentPublication
784/1129/1143/1172, and preserve old no-addition behavior.

Original carrier is not owner-only: `EffectAcceptedTurnPlanner.ValidateWoundSourceExport`
currently compares transition kind to `wound.LastTransition.Kind` unconditionally.
For the authenticated addition packet only, compare to FinalScalars.LastTransition
instead. Independently recompute `CreateTransitionId(resolution)` using the existing
owner, then verify exact `(id, checked(before.ordinal+1), request.turn, "treat")`.
Require preparation/continuation/sole allocated transition IDs all equal that ID,
and stamp turn == request turn == binding turn. Caller-supplied `transitionId` is not
self-authenticating. Keep source owner/origin/realm/active/nonmaterializable/input
fingerprint/opportunity/event checks. First-treatment Before.kind=create and an
actual earlier-treat before-image must both be tested; a stale treat kind must not
mask a wrong ID, ordinal or turn.

PreparedWounds consumer audit at BASE: WoundAcceptedTurnPlan837–906 cloning/storage
and2167 fingerprint bind full original template plus batches; MortalTreatmentPublication
770–785 equality,1129/1143 storage,1172 hash must use the selected carrier accessor.
EffectPlanner2738/2812 read IDs,2946 `PrepareWoundBatchApplications` uses batch lookup,
2992 reads original terminal baseline. Source validator3466 is the unconditional
final-kind read; severity-event checks are create/worsen only. Generic prepared
validation2934–2983 returns early after treatment agreement, and ComposeFinalPlan
850–857 dispatches treatment before the ordinary finalizer. IsEmptyWoundStage3335
reads only count: required0/0 still has one wound and one batch. Do not change
ordinary/no-addition handling to accommodate the private packet.

**One existing batch compiler and two independent checks:** factor helpers inside
existing SeverityRematerializationPlanner.PrepareReduction rather than create another
publisher. Reuse original-before/event/source/index proof199–251, complete final
definition export270–292, application builder300–381, terminal owner384–400 and
retained-lineage403–418. Existing-coordinate rematerialization retains existing
Coordinate formula and exact original prior root; added roots always use T067's
mapped application/private operation key and null predecessor, even before reduction.
Effect mechanics/operation ordinals remain positive/consecutive, unlike T067's zero
based declared ordinal. Existing Agrees independently reruns Prepare; preserve both
its prepare-stage and final-stage invocations.

Expose only a private authenticated packet lookup under the existing
`WoundAcceptedTurnPlanner.TryGetTreatmentSelectedGraph(prepared, out compilation)`;
recompose/check private continuation and preparation without recursive agreement.
An addition lacking its packet must fail closed, not fall back to ordinary behavior.
`PrepareWoundBatchApplications` must use that packet instead of inferring full
rematerialization from roots.Count!=0. Exact terminal selection stays under the
existing owner; retain the0/0 lineage proof. Private ValidateGenerationPredecessors
gets the authenticated view: bijection is over surviving ORIGINAL applications,
all NEW applications are parentless, and original carrierless terminal ancestors
still require the same WoundRootGenerationAuthority proof. Never weaken to a count
check or accept an arbitrary flag supplied in the public batch.

**Fresh definition scope, including child-only definitions:** current blanket
`acceptedContinuation = transitionKind == "treat"` cannot cover an added root.
Use the sealed selected definition origins above to locate each NEW definition in
its exact typed operation/draft, e.g.
`treatmentPublication.outcome.declaredResult[i].complicationDraft.consequenceDefinitions[j].definition.components`.
Do not fabricate `woundDecisions` or widen public creation diagnostic grammar.
Validate the exact new definition subset (including every reachable child-only
definition) with existing EffectRollSkillScopeAuthority.ValidateNewComponents and
the exact wound target. Each new root application also has AcceptedContinuation=false
and follows existing ValidateBoundApplicationComponents fresh checks. Retained
definitions keep accepted continuation despite later skill loss. Missing/foreign
resealed private origins reject before allocation. Keep ordinary creation paths and
diagnostic codes intact; bind origins in private agreement, not a new public field.
If a NEW root or child definition declares an exact skill selector, absent scope
authority must also reject at that typed selector path; do not let the existing
nullable `authority?.ValidateNewComponents` convention become permission to skip
fresh selection on this new private packet path. Production Compose always builds
Offered/Current authority (EffectAcceptedTurnInputComposer285/837), so this is a
missing-authority rejection, not an empty fabricated catalog or a new GM field.
Retained continuation and all-scope-only definitions keep their existing semantics.

**Two slot rewrites must use one final graph:** DeriveEffectResults currently
sorts new application IDs and starts slots at1; BuildFinalWound then independently
does so again and replaces all roots with applications. For the selected packet,
both must use its final graph order and actual retained/new/rematerialized mapping.
Never supply a retained root as a fake new application result.

```csharp
internal sealed record WoundFinalRootAssemblyRow(
    string EffectId, string DefinitionKey, WoundRootOwnershipDomain OwnershipDomain,
    ImmutableArray<WoundEffectSlotAgreement> Slots);
internal static FinalWoundResult BuildFinalTreatmentWound(
    MortalWoundTreatmentSelectedGraphCompilation compilation,
    WoundEffectOperationBatch batch,
    IReadOnlyDictionary<string, EffectAcceptedApplicationResult> applicationByRef);
private static FinalWoundResult BuildFinalWoundCore(
    WoundMaterializationEnvelope before, WoundFinalAssemblyScalars scalars,
    IReadOnlyList<WoundComplication> complications,
    IReadOnlyList<WoundEffectSourceDefinition> definitions,
    IReadOnlyList<WoundFinalRootAssemblyRow> roots);
```

Extract existing JSON construction/final Parse once into the common core. Existing
three-argument BuildFinalWound wrapper keeps ordinary ID-sort semantics; new selected
wrapper resolves actual retained IDs and complete real application results in graph
slot order, including new complication ownership. Retained source/index/carrier
payloads stay unchanged. Parse the full canonical after-image only after all actual
IDs exist, then use existing treat reducer/contributions. Required effectless0/0
still runs this assembly. AllowsWorsening remains false: it gates severity increase,
not direct complication addition. Extend only the private supported selected grammar
for approved adverse procedure/course-interruption results, not positive-mode grammar.

**Procedure category preservation:** the existing authoring owner forbids adverse
operations only in `success` rows (Contract837–864); ordinary `partial_success`
and `failed_attempt` rows may retain their approved mixed ordered results. Do not
accidentally implement a failed-only direct-add publisher because the primary
worked example uses a failed band. Extend private `HasSupportedGrammar` from the
authenticated selected category as needed, preserving positive `success`, course
milestone and guaranteed restrictions. An addition path must still reject any
selected unsupported heal/policy intent as a whole; `Any(addition)` is not permission
to skip the complete supported-operation/selection gate. No authoring grammar or
existing T067/T069 fingerprint changes are needed for this preservation.

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

**Executable RED fixtures and exact owner controls:**

New pure file `BookOfEternityClient.Tests/MortalWoundTreatmentComplicationBindingTests.cs`
uses real typed constructors and T067's existing fingerprint writer:

```csharp
[Fact]
public void BindingPreparation_PreservesExactT067CoordinatesAndFingerprint()
{
    var draft = new MortalWoundComplicationProposalDraft(
        new WoundComplicationProposalDraft("edge", "pain", "active", "Edge pain", 1,
            "known_to_player"),
        ImmutableArray<WoundConsequenceDefinitionProposalDraft>.Empty);
    var operation = new MortalWoundAddComplicationOperation(draft);
    var requestFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "binding-test" });
    var declared = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(0, operation);
    var binding = MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
        requestFingerprint, 0, draft, declared);
    Assert.Empty(binding.Roots);
    Assert.Empty(binding.DefinitionReferenceBindings);
    Assert.Empty(binding.ApplicationReferenceBindings);
    var complicationId = "mortal_wound_complication_" +
        MortalWoundTreatmentIdentityWriter.Digest("complication", requestFingerprint, "0", "edge");
    Assert.Equal(complicationId, binding.ComplicationId);
    Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
    {
        "book_of_eternity.mortal_wound_treatment.complication_preparation", "1",
        requestFingerprint, "0", "edge", complicationId, declared
    }), binding.PreparationFingerprint);
}
```

Add effectful rows using Task1's accepted typed constructor/definition fixtures.
`BindingPreparation_RootKeysUseExistingResponseCoordinateWriter` asserts authored
map order `0/edge/localDefinition`, application subset and byte-exact shared writer
keys, plus original version1 preparation oracle including map fields in the same
order. `BindingPreparation_DetachesAndNamespacesWithoutHidingCollisions` tests
input/output mutation, ordinal0 versus1, independent local namespaces and retained
global definition/stack-key collisions. Formula copies are test oracles only.

New Integration file is exactly
`BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ComplicationAdditionPublication.cs`.
Use the real shared failed-procedure fixture for the primary example, not a
`success` band with adverse operations. Also cover legal partial success below:

```csharp
private static ResolverScenario CreateAdditionPublicationScenario(bool effectless)
{
    var scenario = CreateRecoveryPublicationScenario("procedure", "failed_attempt", "a1");
    var draft = CreateEffectfulComplicationDraft("t070_edge");
    if (effectless) draft["consequenceDefinitions"] = new JsonArray();
    var failed = scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!
        .AsArray().OfType<JsonObject>().Single(row =>
            row["category"]!.GetValue<string>() == "failed_attempt");
    failed["result"] = new JsonArray(new JsonObject
    {
        ["kind"] = "add_complication", ["complicationDraft"] = draft
    });
    return PrepareProcedurePublicationScenario(scenario with
    {
        OperationKey = "operation_t070_direct_add_" + (effectless ? "empty" : "effectful"),
        ExpectedIntentCount = 1
    });
}

[Theory]
[InlineData(false)]
[InlineData(true)]
public void ComplicationAdditionPublication_SameRankPreservesOldGraphAndRequiresBatch(bool effectless)
{
    var scenario = CreateAdditionPublicationScenario(effectless);
    using var fixture = AcceptedStateFixture.Create(scenario);
    var before = fixture.ReadCurrentWound();
    var oldCarrier = fixture.ReadPlayerEffectCarrier();
    var oldIndex = fixture.ReadEffectIdentityIndex();
    var flow = PersistAndRehydrateTreatmentPublication(fixture,
        ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId),
        "direct addition");
    var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
    var prepared = plan.WoundStageBundle!.PreparedPlan;
    var batch = Assert.Single(prepared.EffectOperationBatches);
    Assert.Empty(batch.TerminalOperations);
    Assert.Equal(effectless ? 0 : 1, batch.RootApplications.Count);
    Assert.All(batch.RootApplications, root => Assert.Null(root.PriorRootEffectId));
    Assert.Equal("effect_t070_recovery_characteristic",
        Assert.Single(batch.RootLineageAuthority, row => row.EffectId is not null).EffectId);
    Assert.True(WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
        prepared.TreatmentContinuationAuthority!, out var continuation));
    Assert.NotNull(continuation.RematerializationAuthority);
    using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        publication.CompleteAtFullPipelineEnd();
    var after = fixture.ReadCurrentWound();
    Assert.Equal(before.Severity, after.Severity);
    var added = Assert.IsType<MortalWoundAddComplicationOutcomeIntent>(
        Assert.Single(((MortalWoundTreatmentResolution)flow.Resolution).OutcomeIntents));
    Assert.Contains(after.Complications, row => row.ComplicationId == added.ComplicationId);
    var carrierAfter = fixture.ReadPlayerEffectCarrier();
    var indexAfter = fixture.ReadEffectIdentityIndex();
    const string retainedId = "effect_t070_recovery_characteristic";
    Assert.True(JsonNode.DeepEquals(
        oldCarrier["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId),
        carrierAfter["activeEffects"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId)));
    Assert.True(JsonNode.DeepEquals(
        oldIndex["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId),
        indexAfter["entries"]!.AsArray().Single(row => row!["effectId"]!.GetValue<string>() == retainedId)));
    Assert.Equal(oldIndex["entries"]!.AsArray().Count + (effectless ? 0 : 1),
        indexAfter["entries"]!.AsArray().Count);
    if (effectless)
    {
        Assert.True(JsonNode.DeepEquals(oldCarrier, carrierAfter));
        Assert.True(JsonNode.DeepEquals(oldIndex, indexAfter));
    }
    var treatment = Assert.Single(fixture.ReadCurrentHistory().State!.Transitions, row => row.Kind == "treat");
    AssertClosedTreatmentReceipt(treatment.TreatmentResult!.Receipt, flow.Request, flow.Resolution);
}
```

Extend this positive with exact retained slot mapping, new complication ownership,
new root create history with no original sourceEffectIds, exact complete application
map and the effectless zero-operation result. The fixture has one weak old rankIII
root; authoring two legal additions must vary the hardcoded `t061-irritation`
definition/stack keys in `CreateEffectfulComplicationDraft` (OutcomeIntents400/414).
Two unchanged helper copies are a collision negative, not a legal positive fixture.
ResolveCurrentTreatment is in ResolverTests2992, persisted rehydration and Compose
helpers in ProcedurePublication2288/2341, ordinary resource publication helper in
ResourcePublication5604. Preserve their real reservation/confirmation flow.

Add these methods/theory axes to the same new partial:

- `ComplicationAdditionPublication_RequiredEmptyBatchRejectsMissingOrBorrowedAuthority`:
  reuse removal's real cached-plan resealing at220–291 and final independent agreement.
  Null/wrong batch, borrowed proof and changed lineage reject at0/0. Direct Finalize
  missing-batch check asserts issue code AND Expected text. Required path text is
  `one authenticated treatment graph batch and exact application map (including empty)`;
  scalar optional path keeps null/empty expectation. Add that assertion to the existing
  removal negative too (Minor M1); do not weaken any rejection.
- `ComplicationAdditionPublication_OrderedMixedGraphMaterializesOnce`: legal add/remove/
  reduce permutations, same-rank selective removal, multiple additions, root-slot order,
  exact old live and carrierless-terminal parents, null new parents, one generation
  and no transient ID. Use actual removal lineage fixture for selective closure.
- `ComplicationAdditionPublication_SelectedBindingAndGraphTamperRejects`: changed full
  draft, ordinal, ComplicationId, ordered maps, mapped refs/private keys, graph/order,
  retained ownership/source, application result-slot/ID/extra/missing entries and
  independently resealed packet. Reuse CloneResolutionWithIntentsUnchecked and
  ResealRecoveryResolution from removal643–673; frozen public shapes unchanged.
- `ComplicationAdditionPublication_FinalStampIsIndependentOfOriginalKind`: original
  create and genuinely published earlier-treat positives; independently resealed
  final ID/kind/ordinal/turn/allocated-ID negatives at preparation AND effect boundary.
  A previous treatment must be published normally, not manually installed history.
- `ComplicationAdditionPublication_NewSkillRequiresFreshAuthorityWithoutCreationPacket`:
  root AND reachable child-only exact-skill definition axes, Offered/Current present/
  missing/unavailable, missing authority itself, borrowed/missing typed origins,
  no fabricated creation map.
  Keep retained-lost-skill accepted continuation; assert unavailable selector's existing
  wound binding code and exact typed path. Preserve ordinary creation diagnostics.
- `ComplicationAdditionPublication_DecoratorsDoNotReapplyRecoveryOrRemoval`: start from
  real effectless-add fixture, original progress1 and one original effectless
  complication, refresh baseline before authority. Legal failed-band permutations of
  add, recovery2, remove-original, stabilize and reduce1 finish at progress3/rankII,
  one removal, one added complication, one treatment. No-stabilize row retains anchors.
- `ComplicationAdditionPublication_StabilizationUsesSelectedStampAndMinute`: add+
  stabilize+recovery in legal orders; original transition turn differs from selected
  request turn. Assert actual selected turn/accepted minute/new transition ID and
  checked ordinal. Valid original not_stabilized deterioration anchor clears; a
  different valid condition remains. No invented invalid condition fixture.
- `ComplicationAdditionPublication_PostWriteRollbackRetryAndColdReplay`: reuse removal
  364–392 ResourcePublicationFailureInjection and FileSystemManagerHooks, arm history
  with target carrier's actual original bytes. Assert Fired and ObservedEarlierResourceWrite,
  full-tree byte/existence restoration, same-plan retry with exactly one result, then
  RestartForReplay/ProbePublishedTreatment ExactReplay and unchanged complete tree.
- `ComplicationAdditionPublication_InterruptedCourseAndCriticalFailureKeepApprovedRules`:
  actual course interruption plus natural1/Fate direct-add failure, legal unselected
  policy/death siblings, preserved natural20 success. Do not make adverse outcomes
  legal in positive course milestones/guaranteed routes.
- `ComplicationAdditionPublication_PartialSuccessKeepsMixedApprovedOutcome`:
  use the existing `CreateRecoveryPublicationScenario("procedure", "partial_success", "a1")`
  (RecoveryPublication12–64) so its real die/margin selection targets partial success.
  Append the effectful typed complication only to that partial row after the existing
  one-point recovery, then call PrepareProcedurePublicationScenario to refresh history.
  Resolve, persist/rehydrate and publish through the same coordinated path. Assert
  ResultCategory/receipt `partial_success`, RouteCompletion `None`, one recovery point
  (not two), unchanged severity/old root identity, the exact T067 new complication
  and new parentless root, and normal selected resource consumption. The unchanged
  success row remains positive-only. This proves category is not inferred from the
  mixture of operations and does not narrow the existing approved partial grammar.

Task2 changes shared final assembly/effect validation and the existing worked example's
selected-publication capability. Update the existing `mortal_wound_treatment_reduce_severity_v1`
guide/CLI example and its guards to distinguish validated pre-roll graphs from actual
selected direct-add publication; do not claim policy/heal/legacy publication. Keep
its complete authored JSON and exact guide/CLI equality, and exercise its actual
failed-band addition through the new real publication fixture. Add a guard under
the existing `PromptDocumentationCoverageTests.WoundTreatment` prefix for the
truthful selected-batch/retained-history/effectless wording. Maintain its manifest
registration/requiredText and all older negative examples. No authored afterlife
contract changes, but shared core preservation requires full afterlife guards and
conditional FullValidation. Record this no-afterlife-contract-change rationale.

Register this additional heavy partial in both FastTestBoundaryTests inventories,
docs/testing.md and specs/1505-test-suite-performance/research.md: count63→64 after
Task1 acceptance. No branch split or broadened test lane.

After smallest RED/GREEN, the exact final coherent controls are:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentComplicationBindingTests|FullyQualifiedName~MortalWoundTreatmentWorkingGraphProjectionTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatment|FullyQualifiedName~FastTestBoundaryTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundEffectBatchPlannerTests.SkillScope_|FullyQualifiedName~WoundEffectBatchPlannerTests.NonMechanicalWound_SealsZeroOperationBatchAndEmptyEffectResult|FullyQualifiedName~WoundEffectBatchPlannerTests.EffectStage_WorsenWritesExactRetainedGenerationAndLeavesNewRootParentless|FullyQualifiedName~WoundEffectBatchPlannerTests.TerminalGeneration_|FullyQualifiedName~WoundEffectBatchPlannerTests.Finalize_CanonicallyRenumbersSlotsAfterReverseOrderedOpaqueIds|FullyQualifiedName~WoundEffectBatchPlannerTests.Finalize_ExactResultBuildsCanonicalGraphBindingsEntriesHistoryAndIntents"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.ComplicationAdditionPublication_"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.ComplicationRemovalPublication_"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.PrepareReductionBatch_|FullyQualifiedName~MortalWoundTreatmentResolverTests.FinalizeReduction_|FullyQualifiedName~MortalWoundTreatmentResolverTests.ReductionEffectHandoff_|FullyQualifiedName~MortalWoundTreatmentResolverTests.SkillScope_AcceptedTreatmentRematerializationDoesNotRebindOrRequireProposalCoordinates|FullyQualifiedName~MortalWoundTreatmentResolverTests.OutcomeIntents_AreProductionDerivedOneForEachDeclaredOperation|FullyQualifiedName~MortalWoundTreatmentResolverTests.OutcomeIntent_DerivedComplicationIdsAreStableForExactInputAndChangeWithSealedRequestOrLocalRef|FullyQualifiedName~MortalWoundTreatmentResolverTests.Parser_OutcomeIntentDuplicateAddComplicationRefRejectsAtTheSecondLocalRef"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 10 -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.RecoveryPublication_OrderedRecoverySupportsEveryImplementedCombination|FullyQualifiedName~MortalWoundTreatmentResolverTests.RecoveryPublication_AccumulatesWithoutImplicitThresholdTransition|FullyQualifiedName~MortalWoundTreatmentResolverTests.RecoveryPublication_ThresholdAndMaximumProgressRemainNonterminal|FullyQualifiedName~MortalWoundTreatmentResolverTests.RecoveryPublication_OverflowRejectsBeforeReservationAndPublication|FullyQualifiedName~MortalWoundTreatmentResolverTests.Prepare_StabilizeThenReducePreservesDeclaredOrdinalOrder|FullyQualifiedName~MortalWoundTreatmentResolverTests.Prepare_StabilizationPlacementIsBoundInOrderedPreparation"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
git diff --check
```

Selected tests must execute and pass; list final actual membership, build0warnings/
0errors, cleanup, timeout and duplicate facts. Fast may stop on unfinished legacy or
the documented console scheduling residual: report actual rows and omitted remainder,
not full-green evidence. FullValidation is one conditional shared-docs boundary run.
Rebase source-pinned selectors on accepted Task1 before dispatch; split a measured
oversized Integration selection into disjoint subsets or expand to15m with evidence.
Commit only Task2-owned source/tests/GM/docs/inventory after scoped diff review:
`git commit -m "feat(wounds): publish selected complication additions atomically (#1536)"`.
Parent owns this plan and Spec Kit acceptance; do not stage them. Fresh independent
Spec+Quality review and actual artifact audit are required before acceptance.

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
