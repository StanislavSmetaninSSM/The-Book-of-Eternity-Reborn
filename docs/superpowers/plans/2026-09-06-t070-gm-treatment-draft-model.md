# T070 GM Treatment Draft Model — Required Task 1 Detail

Tracking: GitHub #1536, T059/T070. This is part of Task 1 of
`2026-09-06-t070-alternative-treatment-response-repair.md`, not a separately dispatchable
task. Its globals, tests, documentation and review gate apply. The approved removal
dialect is in `specs/1536-complete-wound-materialization/data-model.md`: GM input uses
`complicationRef`, canonical input uses `complicationId`. Do not rename a ref temporarily
into an ID, invent a selector map, wrap executable canonical removal as a GM operation,
or replace the complete typed draft with an opaque JSON route.

## Files and shared validation boundary

Create `Services/GmTreatmentRouteDraft.cs` and cohesive partials
`Services/MortalWoundTreatmentContract.GmDrafts.cs`, `.GmDraftWriting.cs`, and
`.RouteWriting.cs` under `BookOfEternityClient/`. Modify only the shared route-shape
entrypoint in `.MemberShapes.cs`, the selector dialect/removal branch in
`MortalWoundTreatmentContract.cs`, and scalar/envelope writer extraction in
`MortalWoundTreatmentModel.cs`. All full-wound and canonical entrypoints retain their
existing behavior; no resolver/reducer/factory or canonical operation type changes.

The private validation context gains one explicit dialect, defaulting to canonical:

```csharp
private enum RemovalSelectorDialect { CanonicalId, GmComplicationRef }
private sealed record ValidationContext(
    string Realm, WoundValidationContext? Wound,
    RemovalSelectorDialect RemovalDialect = RemovalSelectorDialect.CanonicalId);
```

Only the existing `ValidateOperation` removal case changes:

```csharp
case "remove_complication":
    var selectorField = context.RemovalDialect == RemovalSelectorDialect.GmComplicationRef
        ? "complicationRef" : "complicationId";
    ValidateObject(operation, path, Set("kind", selectorField), issues);
    ValidateIdentifier(operation, selectorField, path, issues);
    break;
```

All eight operation rules, eleven requirement kinds, three route modes, numeric bounds,
resource policies, result order, interruption restrictions and complete nested effect/
legacy graphs continue through the same existing validator. Do not globally replace
fields named `complicationRef`: nested newly authored complications use their own
already-validated declaration/ownership relation. Existing diagnosis facts retain the
approved `route:<routeId>` / `complication:<complicationId>` grammar and empty `check`.
No new diagnosis fact-selector dialect or gameplay check is introduced here.

## Complete distinct draft types

Put these in `GmTreatmentRouteDraft.cs`. They deliberately cannot be supplied where a
canonical `MortalWoundTreatmentRouteDefinition` or removal operation is required.

```csharp
using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal abstract record GmTreatmentRouteDraft(string RouteId, string DisplayName,
    string Visibility, ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy, string SourcePath)
{
    internal abstract string Mode { get; }
}
internal sealed record GmProcedureRouteDraft(string RouteId, string DisplayName,
    string Visibility, ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy, MortalWoundProcedureResolution Resolution,
    ImmutableArray<GmProcedureBandDraft> Bands, string SourcePath)
    : GmTreatmentRouteDraft(RouteId, DisplayName, Visibility, Requirements, ResourcePolicy, SourcePath)
{
    internal override string Mode => "procedure";
}
internal sealed record GmCourseRouteDraft(string RouteId, string DisplayName,
    string Visibility, ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy, MortalWoundCourseResolution Resolution,
    ImmutableArray<GmCourseMilestoneDraft> Milestones, GmCategoryResultDraft Interruption, string SourcePath)
    : GmTreatmentRouteDraft(RouteId, DisplayName, Visibility, Requirements, ResourcePolicy, SourcePath)
{
    internal override string Mode => "course";
}
internal sealed record GmGuaranteedRouteDraft(string RouteId, string DisplayName,
    string Visibility, ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy, MortalWoundGuaranteedResolution Resolution,
    GmCategoryResultDraft Outcome, string SourcePath)
    : GmTreatmentRouteDraft(RouteId, DisplayName, Visibility, Requirements, ResourcePolicy, SourcePath)
{
    internal override string Mode => "guaranteed";
}
internal sealed record GmProcedureBandDraft(string BandId, long? MinimumMargin,
    long? MaximumMargin, string Category, ImmutableArray<GmTreatmentOperationDraft> DeclaredResult);
internal sealed record GmCourseMilestoneDraft(int Ordinal, long AfterMinutes,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements, string Category,
    string Completion, ImmutableArray<GmTreatmentOperationDraft> DeclaredResult);
internal sealed record GmCategoryResultDraft(string Category,
    ImmutableArray<GmTreatmentOperationDraft> DeclaredResult);
internal abstract record GmTreatmentOperationDraft { internal abstract string Kind { get; } }
internal sealed record GmNoImprovementDraft() : GmTreatmentOperationDraft
{ internal override string Kind => "no_improvement"; }
internal sealed record GmStabilizeDraft() : GmTreatmentOperationDraft
{ internal override string Kind => "stabilize"; }
internal sealed record GmAddRecoveryDraft(int Points) : GmTreatmentOperationDraft
{ internal override string Kind => "add_recovery"; }
internal sealed record GmReduceSeverityDraft(int Steps) : GmTreatmentOperationDraft
{ internal override string Kind => "reduce_severity"; }
internal sealed record GmRemoveComplicationDraft(string ComplicationRef) : GmTreatmentOperationDraft
{ internal override string Kind => "remove_complication"; }
internal sealed record GmAddComplicationDraft(MortalWoundComplicationProposalDraft ComplicationDraft)
    : GmTreatmentOperationDraft { internal override string Kind => "add_complication"; }
internal sealed record GmApplyDeteriorationDraft(string PolicyRef) : GmTreatmentOperationDraft
{ internal override string Kind => "apply_deterioration"; }
internal sealed record GmHealDraft(ImmutableArray<MortalWoundHealLegacyDraft> Legacies)
    : GmTreatmentOperationDraft { internal override string Kind => "heal"; }
internal sealed record GmTreatmentRouteShapeParseResult(bool IsValid,
    ImmutableArray<ValidationIssue> Issues, GmTreatmentRouteDraft? Route);
```

## Complete shared parser and GM assembly

The new GM partial below reuses existing scalar/nested builders in the same partial
contract. Replace the old `ParseRouteShape` try/body with the shown shared-core call;
keep `ParseDiagnosisPathShape` unchanged. Both entrypoints retain the same guarded
exception classes and original duplicate scanning. No successful partial draft exists.

```csharp
// Replacement body of existing ParseRouteShape(JsonElement value, string path):
ArgumentException.ThrowIfNullOrWhiteSpace(path);
var issues = new List<ValidationIssue>();
var route = ReadValidatedRouteShape(value, path, issues,
    RemovalSelectorDialect.CanonicalId, BuildRoute);
return new(route is not null && issues.Count == 0, issues.ToImmutableArray(), route);
```

```csharp
using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentContract
{
    internal static GmTreatmentRouteShapeParseResult ParseGmRouteDraftShape(JsonElement value, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        var route = ReadValidatedRouteShape(value, path, issues,
            RemovalSelectorDialect.GmComplicationRef, BuildGmRoute);
        return new(route is not null && issues.Count == 0, issues.ToImmutableArray(), route);
    }

    private static T? ReadValidatedRouteShape<T>(JsonElement value, string path,
        List<ValidationIssue> issues, RemovalSelectorDialect dialect, Func<WoundTreatmentRoute, T> build)
        where T : class
    {
        try
        {
            WoundMaterializationContract.FindDuplicateProperties(value, path, issues);
            if (issues.Count == 0)
            {
                var route = WoundMaterializationContract.ReadTreatmentRoute(value, path, issues);
                if (route is not null)
                {
                    ValidateRoute(route, path, new ValidationContext("mortal_world", null, dialect), issues);
                    if (issues.Count == 0) return build(route);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            FormatException or OverflowException)
        {
            AddInvalid(issues, path, "one complete typed Mortal wound treatment route", exception.GetType().Name);
        }
        return null;
    }

    private static GmTreatmentRouteDraft BuildGmRoute(WoundTreatmentRoute route)
    {
        var requirements = BuildRequirements(route.Requirements);
        var policy = BuildResourcePolicy(route.ResourcePolicy);
        return route.Mode switch
        {
            "procedure" => new GmProcedureRouteDraft(route.RouteId, route.DisplayName, route.Visibility,
                requirements, policy, BuildProcedureResolution(route.Resolution),
                route.Outcomes.Select(value => new GmProcedureBandDraft(Text(value, "bandId"),
                    NullableInt64(value.GetProperty("minimumMargin")), NullableInt64(value.GetProperty("maximumMargin")),
                    Text(value, "category"), BuildGmOperations(value.GetProperty("result")))).ToImmutableArray(), route.SourcePath),
            "course" => new GmCourseRouteDraft(route.RouteId, route.DisplayName, route.Visibility,
                requirements, policy, BuildCourseResolution(route.Resolution),
                route.Outcomes.Select(value => new GmCourseMilestoneDraft(value.GetProperty("ordinal").GetInt32(),
                    value.GetProperty("afterMinutes").GetInt64(),
                    BuildRequirements(value.GetProperty("requirements").EnumerateArray().ToArray()),
                    Text(value, "category"), Text(value, "completion"),
                    BuildGmOperations(value.GetProperty("result")))).ToImmutableArray(),
                BuildGmCategoryResult(route.Interruption!.Value), route.SourcePath),
            "guaranteed" => new GmGuaranteedRouteDraft(route.RouteId, route.DisplayName, route.Visibility,
                requirements, policy, BuildGuaranteedResolution(route.Resolution),
                BuildGmCategoryResult(route.Outcomes[0]), route.SourcePath),
            _ => throw new InvalidOperationException("Validated GM route has an unknown mode.")
        };
    }

    private static GmCategoryResultDraft BuildGmCategoryResult(JsonElement value) =>
        new(Text(value, "category"), BuildGmOperations(value.GetProperty("result")));
    private static ImmutableArray<GmTreatmentOperationDraft> BuildGmOperations(JsonElement values) =>
        values.EnumerateArray().Select(BuildGmOperation).ToImmutableArray();
    private static GmTreatmentOperationDraft BuildGmOperation(JsonElement value)
    {
        if (Text(value, "kind") == "remove_complication")
            return new GmRemoveComplicationDraft(Text(value, "complicationRef"));
        return BuildOperation(value) switch
        {
            MortalWoundNoImprovementOperation => new GmNoImprovementDraft(),
            MortalWoundStabilizeOperation => new GmStabilizeDraft(),
            MortalWoundAddRecoveryOperation op => new GmAddRecoveryDraft(op.Points),
            MortalWoundReduceSeverityOperation op => new GmReduceSeverityDraft(op.Steps),
            MortalWoundAddComplicationOperation op => new GmAddComplicationDraft(op.ComplicationDraft),
            MortalWoundApplyDeteriorationOperation op => new GmApplyDeteriorationDraft(op.PolicyRef),
            MortalWoundHealOperation op => new GmHealDraft(op.Legacies),
            _ => throw new InvalidOperationException("Validated GM operation has an unknown non-removal kind.")
        };
    }
}
```

## Shared writing and complete GM writer

Extract the existing field-writing bodies into the following `.RouteWriting.cs` helpers.
Both old canonical and new GM writers call them. This is serialization reuse only,
not converting a GM route to a canonical route or giving it executable authority.

```csharp
private static void WriteRouteHeader(Utf8JsonWriter writer, string id, string name,
    string visibility, string mode, ImmutableArray<MortalWoundTreatmentRequirement> requirements,
    MortalWoundTreatmentResourcePolicy policy)
{
    writer.WriteStartObject();
    writer.WriteString("routeId", id); writer.WriteString("displayName", name);
    writer.WriteString("visibility", visibility); writer.WriteString("mode", mode);
    WriteRequirements(writer, requirements); WriteResourcePolicy(writer, policy);
}
private static void WriteProcedureResolutionPayload(Utf8JsonWriter writer, MortalWoundProcedureResolution value)
{
    writer.WritePropertyName("resolution"); writer.WriteStartObject();
    writer.WriteString("formulaKey", value.FormulaKey); writer.WriteNumber("difficulty", value.Difficulty);
    writer.WriteString("rollSource", value.RollSource); writer.WriteString("criticalPolicy", value.CriticalPolicy);
    writer.WritePropertyName("modifierSource"); writer.WriteStartObject();
    writer.WriteString("kind", value.ModifierSource.Kind);
    if (value.ModifierSource is MortalWoundResolvedSkillTierModifierSource skill)
        writer.WriteNumber("requirementIndex", skill.RequirementIndex);
    writer.WriteEndObject(); writer.WriteEndObject();
}
private static void WriteProcedureBandPayload(Utf8JsonWriter writer, string id, long? minimum,
    long? maximum, string category, Action<Utf8JsonWriter> writeResult)
{
    writer.WriteStartObject(); writer.WriteString("bandId", id);
    WriteNullableInt64(writer, "minimumMargin", minimum); WriteNullableInt64(writer, "maximumMargin", maximum);
    writer.WriteString("category", category); writeResult(writer); writer.WriteEndObject();
}
private static void WriteCourseResolutionPayload(Utf8JsonWriter writer, MortalWoundCourseResolution value)
{
    writer.WritePropertyName("resolution"); writer.WriteStartObject();
    writer.WriteString("clockKind", value.ClockKind); writer.WriteNumber("maximumGapMinutes", value.MaximumGapMinutes);
    writer.WriteEndObject();
}
private static void WriteCourseMilestonePayload(Utf8JsonWriter writer, int ordinal, long afterMinutes,
    ImmutableArray<MortalWoundTreatmentRequirement> requirements, string category, string completion,
    Action<Utf8JsonWriter> writeResult)
{
    writer.WriteStartObject(); writer.WriteNumber("ordinal", ordinal); writer.WriteNumber("afterMinutes", afterMinutes);
    WriteRequirements(writer, requirements); writer.WriteString("category", category);
    writer.WriteString("completion", completion); writeResult(writer); writer.WriteEndObject();
}
private static void WriteGuaranteedResolutionPayload(Utf8JsonWriter writer, MortalWoundGuaranteedResolution value)
{
    writer.WritePropertyName("resolution"); writer.WriteStartObject();
    writer.WriteString("capabilityRef", value.CapabilityRef); writer.WriteString("actorRole", value.ActorRole);
    writer.WriteEndObject();
}
private static void WriteCategoryResultPayload(Utf8JsonWriter writer, string category,
    Action<Utf8JsonWriter> writeResult)
{
    writer.WriteStartObject(); writer.WriteString("category", category);
    writeResult(writer); writer.WriteEndObject();
}
private static void WriteResultArray<T>(Utf8JsonWriter writer, ImmutableArray<T> operations,
    Action<Utf8JsonWriter, T> writeOperation)
{
    writer.WritePropertyName("result"); writer.WriteStartArray();
    foreach (var operation in operations) writeOperation(writer, operation);
    writer.WriteEndArray();
}
```

In the existing canonical writer, replace only corresponding bodies with calls:

```csharp
// WriteRouteCanonical header, before the existing typed switch:
WriteRouteHeader(writer, route.RouteId, route.DisplayName, route.Visibility, route.Mode,
    route.Requirements, route.ResourcePolicy);
// WriteProcedure resolution, then each existing band:
WriteProcedureResolutionPayload(writer, route.Resolution);
WriteProcedureBandPayload(writer, band.BandId, band.MinimumMargin, band.MaximumMargin,
    band.Category, output => WriteOperations(output, band.DeclaredResult));
// WriteCourse resolution, each milestone, then interruption object:
WriteCourseResolutionPayload(writer, route.Resolution);
WriteCourseMilestonePayload(writer, milestone.Ordinal, milestone.AfterMinutes, milestone.Requirements,
    milestone.Category, milestone.Completion, output => WriteOperations(output, milestone.DeclaredResult));
WriteCategoryResultPayload(writer, route.Interruption.Category,
    output => WriteOperations(output, route.Interruption.DeclaredResult));
// WriteGuaranteed resolution and sole outcome object:
WriteGuaranteedResolutionPayload(writer, route.Resolution);
WriteCategoryResultPayload(writer, route.Outcome.Category,
    output => WriteOperations(output, route.Outcome.DeclaredResult));
// WriteOperations delegates its unchanged per-operation switch to the leaf below:
WriteResultArray(writer, operations, WriteCanonicalOperation);
```

Move the existing per-operation body, without semantic changes, to this exact leaf:

```csharp
private static void WriteCanonicalOperation(Utf8JsonWriter writer, MortalWoundTreatmentOperation operation)
{
    writer.WriteStartObject(); writer.WriteString("kind", operation.Kind);
    switch (operation)
    {
        case MortalWoundNoImprovementOperation:
        case MortalWoundStabilizeOperation: break;
        case MortalWoundAddRecoveryOperation value: writer.WriteNumber("points", value.Points); break;
        case MortalWoundReduceSeverityOperation value: writer.WriteNumber("steps", value.Steps); break;
        case MortalWoundRemoveComplicationOperation value: writer.WriteString("complicationId", value.ComplicationId); break;
        case MortalWoundAddComplicationOperation value: WriteComplicationDraft(writer, value.ComplicationDraft); break;
        case MortalWoundApplyDeteriorationOperation value: writer.WriteString("policyRef", value.PolicyRef); break;
        case MortalWoundHealOperation value: WriteLegacies(writer, value.Legacies); break;
        default: throw new InvalidOperationException("Unknown typed treatment operation.");
    }
    writer.WriteEndObject();
}
```

Complete new `.GmDraftWriting.cs` partial (imports `System.Text.Json`; namespace
`BookOfEternityClient.Services`; enclosing `internal static partial class MortalWoundTreatmentContract`):

```csharp
internal static void WriteGmRouteDraft(Utf8JsonWriter writer, GmTreatmentRouteDraft route)
{
    ArgumentNullException.ThrowIfNull(writer); ArgumentNullException.ThrowIfNull(route);
    using var stream = new MemoryStream();
    using (var buffer = new Utf8JsonWriter(stream)) WriteGmRouteUnchecked(buffer, route);
    using var document = JsonDocument.Parse(stream.ToArray());
    var parsed = ParseGmRouteDraftShape(document.RootElement, "gm_route");
    if (!parsed.IsValid)
        throw new InvalidOperationException("Cannot write an invalid GM route: " +
            string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
    document.RootElement.WriteTo(writer);
}
private static void WriteGmRouteUnchecked(Utf8JsonWriter writer, GmTreatmentRouteDraft route)
{
    WriteRouteHeader(writer, route.RouteId, route.DisplayName, route.Visibility, route.Mode,
        route.Requirements, route.ResourcePolicy);
    switch (route)
    {
        case GmProcedureRouteDraft value:
            WriteProcedureResolutionPayload(writer, value.Resolution);
            writer.WritePropertyName("outcomes"); writer.WriteStartArray();
            foreach (var band in value.Bands)
                WriteProcedureBandPayload(writer, band.BandId, band.MinimumMargin, band.MaximumMargin,
                    band.Category, output => WriteResultArray(output, band.DeclaredResult, WriteGmOperation));
            writer.WriteEndArray(); writer.WriteNull("interruption"); break;
        case GmCourseRouteDraft value:
            WriteCourseResolutionPayload(writer, value.Resolution);
            writer.WritePropertyName("outcomes"); writer.WriteStartArray();
            foreach (var milestone in value.Milestones)
                WriteCourseMilestonePayload(writer, milestone.Ordinal, milestone.AfterMinutes, milestone.Requirements,
                    milestone.Category, milestone.Completion,
                    output => WriteResultArray(output, milestone.DeclaredResult, WriteGmOperation));
            writer.WriteEndArray(); writer.WritePropertyName("interruption");
            WriteCategoryResultPayload(writer, value.Interruption.Category,
                output => WriteResultArray(output, value.Interruption.DeclaredResult, WriteGmOperation)); break;
        case GmGuaranteedRouteDraft value:
            WriteGuaranteedResolutionPayload(writer, value.Resolution);
            writer.WritePropertyName("outcomes"); writer.WriteStartArray();
            WriteCategoryResultPayload(writer, value.Outcome.Category,
                output => WriteResultArray(output, value.Outcome.DeclaredResult, WriteGmOperation));
            writer.WriteEndArray(); writer.WriteNull("interruption"); break;
        default: throw new InvalidOperationException("Unknown GM treatment route draft.");
    }
    writer.WriteEndObject();
}
private static void WriteGmOperation(Utf8JsonWriter writer, GmTreatmentOperationDraft operation)
{
    if (operation is GmRemoveComplicationDraft remove)
    {
        writer.WriteStartObject(); writer.WriteString("kind", "remove_complication");
        writer.WriteString("complicationRef", remove.ComplicationRef); writer.WriteEndObject(); return;
    }
    MortalWoundTreatmentOperation payload = operation switch
    {
        GmNoImprovementDraft => new MortalWoundNoImprovementOperation(),
        GmStabilizeDraft => new MortalWoundStabilizeOperation(),
        GmAddRecoveryDraft value => new MortalWoundAddRecoveryOperation(value.Points),
        GmReduceSeverityDraft value => new MortalWoundReduceSeverityOperation(value.Steps),
        GmAddComplicationDraft value => new MortalWoundAddComplicationOperation(value.ComplicationDraft),
        GmApplyDeteriorationDraft value => new MortalWoundApplyDeteriorationOperation(value.PolicyRef),
        GmHealDraft value => new MortalWoundHealOperation(value.Legacies),
        _ => throw new InvalidOperationException("Unknown GM non-removal operation draft.")
    };
    WriteCanonicalOperation(writer, payload);
}
```

The seven shared non-removal payloads are serialized through existing canonical leaf
syntax; this is not a generic canonical-operation wrapper. The only removal draft has
only `ComplicationRef` and never reaches that conversion. No full route or selected
selector is converted before real authority is available.

## Regression starter and required coverage

Create `BookOfEternityClient.Tests/GmTreatmentRouteDraftTests.cs`. Before implementation,
add this real two-way dialect regression alongside complete all-mode/all-operation tests:

```csharp
[Fact]
public void Removal_UsesOnlyUnresolvedGmRefAndRejectsCanonicalDialect()
{
    var route = WoundContractTestData.CreateActiveWound()["treatment"]!["routes"]![0]!.DeepClone().AsObject();
    route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
    {
        ["kind"] = "remove_complication", ["complicationRef"] = "offered_selector"
    });
    var element = JsonSerializer.SerializeToElement(route);
    var parsed = MortalWoundTreatmentContract.ParseGmRouteDraftShape(element, "author.route");
    Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
    var draft = Assert.IsType<GmProcedureRouteDraft>(parsed.Route);
    var removal = Assert.IsType<GmRemoveComplicationDraft>(Assert.Single(draft.Bands[0].DeclaredResult));
    Assert.Equal("offered_selector", removal.ComplicationRef);
    Assert.False(MortalWoundTreatmentContract.ParseRouteShape(element, "canonical.route").IsValid);
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream)) MortalWoundTreatmentContract.WriteGmRouteDraft(writer, draft);
    var written = JsonNode.Parse(stream.ToArray())!;
    Assert.True(JsonNode.DeepEquals(route, written));
    Assert.Null(written["outcomes"]![0]!["result"]![0]!["complicationId"]);
    route["outcomes"]![0]!["result"]![0]!.AsObject().Remove("complicationRef");
    route["outcomes"]![0]!["result"]![0]!["complicationId"] = "existing_complication";
    Assert.False(MortalWoundTreatmentContract.ParseGmRouteDraftShape(
        JsonSerializer.SerializeToElement(route), "author.route").IsValid);
    Assert.True(MortalWoundTreatmentContract.ParseRouteShape(
        JsonSerializer.SerializeToElement(route), "canonical.route").IsValid);
}
```

Pin exact path/code in the expanded invalid cases: forbidden/both/missing/null/non-string
selectors, recursive original duplicates, missing fields, invalid mode/outcome/category,
limits/overflow, graph/component/parameter/slot faults, source-path omission and immutable
detachment. Cover removal in every legal outcome/interruption position; do not add a new
repeated-removal prohibition. Cover all modes, all eight operations and existing eleven
requirement kinds with legitimate positive/negative result semantics, complete nested
complication effects and heal legacies. Preserve existing canonical writers byte/semantic
projections and factory member seals. Unknown draft subtypes and manually inconsistent
typed drafts must not produce a valid emitted response.

Run the exact Task 1 controls from the owning plan, extended there with this new class
and the five named initial/canonical selector regressions. No new runtime authority,
resolver, accepted publication, diagnosis check or resource-consumption policy is part
of this local shape task. The response example must include a complete alternative
removal using an offered opaque `complicationRef` and its opposite-dialect negative.
