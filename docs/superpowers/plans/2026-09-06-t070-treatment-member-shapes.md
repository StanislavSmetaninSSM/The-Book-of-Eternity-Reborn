# T070 Complete Standalone Mortal Treatment Members Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Parse a complete alternative route and diagnosis path without inventing a
wound, while retaining every local structural rule and the stronger full-wound validator.

**Architecture:** Separate severity-independent detached component/slot traversal from
real-severity limits in the existing catalog and adapter. Then expose explicit standalone
member parsers that share the existing closed outer readers, nested validators and typed
builders/writers. Shape success is a detached draft, never gameplay or publication authority.

**Tech Stack:** C#/.NET 8, System.Text.Json, immutable collections, xUnit, PowerShell 7.

**Tracking:** GitHub [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T059/T070. Approved contracts:
`data-model.md` Alternative treatment authoring authority and accepted commands;
`plan.md` T067 production-owned sealing boundary. This is the internal validation
prerequisite for the already required closed command codec, not a new game mechanic.
Execute after the sealed alternative reducer checkpoint, before accepted-command parsing.

## Global Constraints

- Stay in `E:/Games/worktrees/boe-1536-wound-materialization`, existing
  `1536-complete-wound-materialization` branch. Preserve `.serena/` and unrelated edits.
- No migration, compatibility parser, dual write, or old-save fallback.
- Mortal wounds have arbitrary GM-authored setting-specific consequences and treatment,
  not a predefined catalog of injuries or cures. Added routes must use the existing
  complete registered route constructor, not a new permissive fragment schema.
- No fabricated wound, owner, policy, severity, complication collection or authority.
  In this explicitly Mortal standalone API, `mortal_world` is the known real realm.
- Shape parsing is not fresh world authority. Full-context parsing, factories/reducers,
  and later canonical/signed-source publication checks remain mandatory.
- Share one implementation of each closed schema and local graph rule. Do not leave
  complication/legacy payloads opaque or reject an existing registered operation wholesale.
- `CanonicalStateNormalizer` remains the sole publisher. This internal extraction
  exposes no incomplete player command or GM-authored response capability.
- Fast stays physically isolated, deterministic, and bounded to five minutes. No file,
  lease, restart, publication, or rollback tests in these pure tasks.
- Use the bounded PowerShell 7 test runner; never overlap C# lanes. No broad Fast,
  Integration, FullValidation, or PreMerge is needed for this pure checkpoint.
- No push, PR, merge, issue closure, new branch, or session cleanup.

### Task 1: Shared detached local envelope and slot validation

**Files:**
- Modify: `BookOfEternityClient/Services/WoundConsequenceEnvelopeCatalog.cs` (only the
  detached traversal and shared scalar/action/reaction helpers it uses).
- Modify: `BookOfEternityClient/Services/WoundPersistedConsequenceEnvelopeAdapter.cs`.
- Create: `BookOfEternityClient.Tests/WoundDetachedConsequenceShapeTests.cs`.
- Read regression: `BookOfEternityClient.Tests/WoundConsequenceEnvelopeTests.cs` and
  `BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityContractTests.cs`.

Do not edit route parsers, command/response/publication code, old tests or parent records.
If factoring requires a cohesive partial file, first report its exact responsibility;
do not copy the traversal into a parallel implementation.

**Interfaces:** Existing full-context APIs and their callers keep their signatures.
Add precisely named local entry points using the existing detached projection/result types:

```csharp
// WoundConsequenceEnvelopeCatalog
internal static WoundDetachedMortalEnvelopeValidationResult ValidateDetachedMortalShape(
    string authorPath,
    IReadOnlyList<WoundDetachedMortalEffectRef> effects);

// WoundPersistedConsequenceEnvelopeAdapter
internal static WoundPersistedConsequenceEnvelopeValidationResult ValidatePrevalidatedDetachedShape(
    string authorPath,
    IReadOnlyList<WoundPersistedConsequenceDefinition> definitions,
    IReadOnlyList<WoundPersistedConsequenceRoot> roots,
    bool requireExactGlobalSlotAgreement,
    int? persistedSlotsUsed = null);
```

The adapter remains **prevalidated**: its caller diagnoses missing/duplicate local
definitions and unresolved roots first. It must not claim complete source-definition
validation. Shared detached component traversal still validates registered components.

- [ ] **Step 1: Add and observe pure RED boundary/behavior tests**

Use raw complete component JSON, real detached constructors, and production APIs. A
missing new API is an initial interface RED, not a passed behavior test. Record it, then
record actual assertion RED for behavior as the minimal seam becomes callable. Example:

```csharp
[Fact]
public void Shape_DefersRealSeverityButPreservesMechanicalCoordinate()
{
    using var document = JsonDocument.Parse("""
        {"componentId":"strength_penalty","profile":"characteristic_modifier",
         "priority":0,"payload":{"characteristic":"strength","operation":"flat",
         "value":-4,"cap":null}}
        """);
    var effects = new[] { new WoundDetachedMortalEffectRef(
        "definition_root", "draft.definition", new[] { document.RootElement.Clone() }) };
    var shape = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortalShape("draft", effects);
    Assert.True(shape.IsValid, string.Join("; ", shape.Issues.Select(issue => issue.Code)));
    Assert.Single(shape.Slots);
    var light = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
        new WoundDetachedMortalEnvelopeRequest(1, "draft", effects));
    Assert.False(light.IsValid);
}
```

Add actual cases for: zero/non-decimal/capped-zero scalar rejected even without rank;
safe scalar cap preserving exact effective value; duplicate mechanical coordinates;
action cost syntax, null modifier outside cost mode, protected/disallowed forbid actions;
allowed forbid shape versus real rank I rejection; apply-definition local expansion and
real rank I rejection; missing/duplicate/unused expansion, wrong maxExpansion, nested
reaction, parameterized child using real edge parameters and invalid bound payload;
periodic slots retaining `HasDeferredPeriodicAuthority`; root-direct target not counted
again as an unbound child. Adapter cases cover missing/extra/wrong reciprocal profile,
persisted slot count mismatch, absolute structural maximum, and real low-rank budget.
Use small local builders, not a copy of the large existing test class. Every invalid case
asserts a relevant diagnostic/path, not merely an exception or nonempty arbitrary result.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundDetachedConsequenceShapeTests"
```

- [ ] **Step 2: Factor one local traversal with explicit optional real context**

Keep the existing request-null/rank-invalid diagnostics on full-context entry points.
The new API takes no rank and cannot fabricate one. A private common core may take
`int? severityRank`; null means no real rank was supplied, not rank zero or four.
Both entry points must run the same component, parameter, reaction and coordinate code.
Only the following predicates use a supplied rank:

```csharp
decimal? limit = severityRank is int actualRank
    ? (string.Equals(operation, "percent", StringComparison.Ordinal)
        ? PercentageLimit(actualRank)
        : actualRank)
    : null;
if (effective == 0m || (limit.HasValue && !WithinAbsoluteLimit(effective, limit.Value)))
{
    Add(issues, effectivePath, "wound_consequence_magnitude_exceeded",
        limit is decimal actualLimit
            ? $"nonzero effective absolute {operation} modifier <= {actualLimit.ToString(CultureInfo.InvariantCulture)} at severity rank {severityRank}"
            : "one exact nonzero effective decimal wound modifier",
        effective.ToString(CultureInfo.InvariantCulture));
    return;
}
```

Likewise separate action-cost magnitude, forbid minimum rank, and apply-definition
minimum rank from local validation. Preserve unchanged full-context verdicts and
diagnostics; rank rejection must not bypass the shape entry point's local expansion
checks. The exact decimal/cap parsing, nonzero rule, component profiles, action allowlist,
modifier syntax, bounded flattening, exactly matched/used expansion evidence,
`maxExpansion=2`, no nested reaction, and deterministic slots are independent of rank.
Reuse the existing roll/periodic coordinate helpers without changing skill scope.

Keep actual reaction parameters through `WoundDetachedMortalReactionExpansionRef` and
`EffectComponentParameterBinder.Bind`; do not replace them with defaults or count raw
unbound children. Preserve the parent definition validator's separate parameter authority.

- [ ] **Step 3: Share adapter graph preparation and reciprocal slot checks**

Extract existing `ValidateResolvedGraph` projection once. Choose the appropriate catalog
entry point from presence of the real rank, then share all root/slot comparison logic:

```csharp
var validation = severityRank is int actualRank
    ? WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
        new WoundDetachedMortalEnvelopeRequest(actualRank, authorPath, effects))
    : WoundConsequenceEnvelopeCatalog.ValidateDetachedMortalShape(authorPath, effects);
```

`ValidateGlobalSlotAgreement`, declared `persistedSlotsUsed` equality, and per-root
profile multiset reciprocity are unconditional. Separate only the rank-dependent
aggregate limit at the end of `ValidatePrevalidatedPerRootProfileAgreement`. Retain
the applicable absolute `MaxConsequences` structural ceiling and accurate direct-root
versus unbound-child accounting. Unresolved roots remain diagnosed by the owning
complete graph validator; do not silently promote this helper to full graph authority.

- [ ] **Step 4: Verify, self-review and commit the bounded extraction**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~WoundDetachedConsequenceShapeTests|FullyQualifiedName~WoundConsequenceEnvelopeTests|FullyQualifiedName~MortalWoundTreatmentCapabilityContractTests|FullyQualifiedName~MortalWoundTreatmentSeverityReductionPlannerTests"
```

Record the selected filter and test count. All rows must pass with a clean build,
no timeout/duplicate ID and complete cleanup. Self-review the shared call paths and
commit only owned code/tests: `refactor(wounds): separate detached shape from severity (#1536)`.
Parent inspects evidence and performs independent spec/quality review before Task 2.

### Task 2: Complete standalone route/path parsing and canonical members

**Files:**
- Create: `BookOfEternityClient/Services/MortalWoundTreatmentContract.MemberShapes.cs`.
- Modify: `BookOfEternityClient/Services/WoundMaterializationContract.cs` (extract shared
  per-route/path outer readers and recursive duplicate detection access only).
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs` (shared local
  versus real-wound context validation, preserving existing full-context API).
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentContract.WoundGraph.cs` (retain
  local graph/policy checks, gate only actual owner and rank applicability).
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentModel.cs` (reuse private
  builders and canonical route/path writers, no alternate JSON dialect).
- Create: `BookOfEternityClient.Tests/MortalWoundTreatmentMemberShapeTests.cs`.

Do not edit factory/reducer seals, commands, GM response, publication or parent records.
Task 1's reviewed local envelope API is the dependency; do not alter its implementation
without reporting a concrete missed requirement and adding its covering test.

**Interfaces:** Add explicit immutable shape results in the new partial file:

```csharp
internal sealed record MortalWoundTreatmentRouteShapeParseResult(
    bool IsValid, ImmutableArray<ValidationIssue> Issues,
    MortalWoundTreatmentRouteDefinition? Route);
internal sealed record MortalWoundDiagnosisPathShapeParseResult(
    bool IsValid, ImmutableArray<ValidationIssue> Issues,
    MortalWoundDiagnosisPathDefinition? DiagnosisPath);

// MortalWoundTreatmentContract, same partial as the existing typed model.
internal static MortalWoundTreatmentRouteShapeParseResult ParseRouteShape(
    JsonElement value, string path);
internal static MortalWoundDiagnosisPathShapeParseResult ParseDiagnosisPathShape(
    JsonElement value, string path);
internal static void WriteRouteCanonical(
    Utf8JsonWriter writer, MortalWoundTreatmentRouteDefinition route);
internal static void WriteDiagnosisPathCanonical(
    Utf8JsonWriter writer, MortalWoundDiagnosisPathDefinition diagnosisPath);
```

Writers emit one complete object, no containing property. Existing `WriteCanonical`
delegates to them; nulls/order/empty V1 `check` and omission of `SourcePath` stay exact.
Shape success requires zero issues and a complete detached typed value; invalid input
returns issues and null value. It must never leak parser exceptions or partial success.

- [ ] **Step 1: Add and observe standalone parser RED cases**

Use the actual shared wound builder for valid route extraction:

```csharp
[Fact]
public void ParseRouteShape_ReturnsCompleteDetachedProcedure()
{
    var wound = WoundContractTestData.CreateActiveWound();
    var raw = JsonSerializer.SerializeToElement(wound["treatment"]!["routes"]![0]);
    var result = MortalWoundTreatmentContract.ParseRouteShape(raw, "command.result.route");
    Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Code)));
    Assert.IsType<MortalWoundProcedureRouteDefinition>(result.Route);
    Assert.Equal("command.result.route", result.Route!.SourcePath);
}
```

Add complete valid procedure/course/guaranteed routes, all registered operation families,
effectful add-complication and cosmetic/mechanical heal legacies. Cover nested unknown,
missing and duplicate fields using raw original JSON (not JsonNode duplicate collapse),
mode mismatches, resource selector bounds/local linkage, modifier requirement index,
procedure band gaps/overlap/order, course ordinal/time/interruption, guaranteed local
capability matching, nondecimal/bounded counts, operation order and exclusive outcomes.
For nested effect graphs cover local refs, parameter bounds/binding, source predicate,
root/profile reciprocity and reachability. Mutating/disposal of original inputs must
not change parsed value or complete canonical round-trip output.

Diagnosis tests cover exact identifier/token grammar, all member fields, empty V1 check,
fact bounds/order/uniqueness, hidden nonempty prerequisites, registered requirements,
failurePolicy, and facts referring to existing wound members absent from this fragment.
Such external membership is intentionally not inferred from a standalone fragment.

Paired shape/full-context cases are required: a locally valid policyRef versus a real
different wound policy; target applicability versus a real incompatible owner; a valid
powerful consequence graph versus low real severity; and locally valid diagnosis facts
versus missing wound members/unseeded whole graph. Full parsing must retain rejection.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentMemberShapeTests"
```

- [ ] **Step 2: Extract closed per-member readers and typed entry points**

Extract from `ParseTreatmentRoutes`/`ParseDiagnosisPaths` their existing one-object
readers, preserving exact diagnostics and source paths. Both full array loops and
standalone methods call these same readers; retain array cardinality and exact/confusable
uniqueness at the collection level. Recursively reject duplicate properties on original
JSON before any JsonNode conversion or detached typed construction.

The standalone pipeline is: recursive duplicate check, shared outer reader, shared
complete local validation, typed builder on zero issues. Reuse `BuildRoute` and
`BuildDiagnosisPath` inside the same partial class after validation. Keep guarded
exception-to-issue handling analogous to `ParseProjection`, not catch-and-success.

- [ ] **Step 3: Separate only missing wound-context predicates**

Retain one mode-validation implementation. Internally represent absent wound context
explicitly; preserve real `Realm` separately. Do not introduce a public skipValidation
flag or make missing owner/severity silently default. Full `ValidateRoutes` keeps its
existing signature and always supplies the actual wound context.

Always validate requirements, resource policy and local common/milestone selectors;
complete procedure resolution/bands/modifier requirements; course times/milestones and
adverse-only interruption; guaranteed actor/capability plus exact local requirement;
all result operations, bounds, ordering, no_improvement exclusivity, terminal heal and
aggregate reduction limit. Only `apply_deterioration.policyRef` membership needs the
actual policy; its exact identifier syntax does not.

For nested complication/legacy drafts, always run current complete wrapper/definition/
root/application parsing, common effect validation for `mortal_world`, local reference
and parameter checks, graph reachability, source policies and Task 1 local slot adapter.
Only actual target membership, rank III/IV applicability and the real severity envelope
need full context. Preserve the existing client-owned proposal marker normalization;
do not invent another wound or reject existing add_complication/heal capabilities.

Split diagnosis fact grammar from membership. A token is exactly `route:` or
`complication:` plus an existing-contract exact identifier. Standalone parsing checks
grammar; full diagnosis additionally checks real wound membership, known/completed
consistency and the existing least-fixed-point graph. Share empty V1 check validation,
requirements and hidden nonempty prerequisites rather than duplicating them.

- [ ] **Step 4: Reuse canonical member writers and verify the complete boundary**

Rename/expose existing private route writer to `WriteRouteCanonical`; extract the inline
diagnosis writer into `WriteDiagnosisPathCanonical`. Delegate full treatment serialization
to both; preserve full display text, every mechanical field and ordered array exactly.
Round-trip through actual Utf8JsonWriter and each new parser in the focused tests.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentMemberShapeTests|FullyQualifiedName~MortalWoundTreatmentContractTests|FullyQualifiedName~MortalWoundTreatmentCapabilityContractTests|FullyQualifiedName~MortalWoundDeteriorationPolicyContractTests|FullyQualifiedName~WoundMaterializationContractTests|FullyQualifiedName~MortalWoundDiagnosisTests.Parse_|FullyQualifiedName~MortalWoundDiagnosisTransitionTests|FullyQualifiedName~MortalWoundAlternativeTransitionTests|FullyQualifiedName~WoundDetachedConsequenceShapeTests"
```

All rows must pass; report exact RED/GREEN evidence, paths/counts, clean build and
cleanup. Do not include still-unimplemented command/response tests to manufacture a
green claim or waive them. Self-review and commit only owned files with
`refactor(wounds): share complete standalone treatment member validation (#1536)`.
Parent inspects and independently reviews; no agent report alone closes tasks.

## Completion and GM synchronization boundary

These two internal tasks preserve existing full-wound gameplay validation and publish
no new command or response. No Mortal/afterlife prompt, example, manifest, matrix or
daemon update is required for the extraction alone. Record this rationale with evidence.
The next command/GM-response/fresh-publication tasks must provide the worked GM examples
and all affected docs/guards in their same change. T070/T177/#1536 remain open.
